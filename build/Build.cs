using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;

class Build : NukeBuild
{
    const int RUST_DS_APPID = 258550;

    const string FILELIST_CONTENTS = "regex:Managed\\/.*\\.dll";
    const string OXIDE_RUST_WINDOWS_URL = "https://umod.org/games/rust/download?tag=public";

    public static int Main() => Execute<Build>(x => x.UpdateReferences);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild
        ? Configuration.Debug
        : Configuration.Release;

    [Parameter("DepotDownloader version")]
    readonly string DepotDownloaderVersion = "2.5.0";

    [CanBeNull]
    string _currentDepotDownloaderVersion;

    AbsolutePath ToolsDir => RootDirectory / "tools";
    AbsolutePath DownloadsDir => ToolsDir / "downloads";
    AbsolutePath AppDownloadDir => DownloadsDir / "app_258550";
    AbsolutePath DepotDownloaderDir => ToolsDir / "DepotDownloader";
    AbsolutePath DepotDownloaderExecutable => DepotDownloaderDir / "DepotDownloader.exe";
    AbsolutePath DepotDownloaderFileList => DepotDownloaderDir / "app_258550.filelist";
    AbsolutePath ReferencesDir => RootDirectory / "include/references";

    Target GetCurrentDepotDownloaderVersion =>
        _ =>
            _.Executes(() =>
            {
                if (DepotDownloaderExecutable.ToFileInfo().Exists)
                {
                    _currentDepotDownloaderVersion = FileVersionInfo
                        .GetVersionInfo(DepotDownloaderExecutable)
                        .ProductVersion!;
                    Log.Information(
                        "DepotDownloader version is: {Version}",
                        _currentDepotDownloaderVersion
                    );
                }
                else
                {
                    Log.Information("DepotDownloader executable not found");
                }
            });

    Target CreateDepotDownloaderFileList =>
        _ =>
            _.Executes(
                () =>
                    DepotDownloaderFileList
                        .TouchFile()
                        .WriteAllText(FILELIST_CONTENTS, eofLineBreak: true)
            );

    Target UpdateDepotDownloader =>
        _ =>
            _.DependsOn(GetCurrentDepotDownloaderVersion)
                .OnlyWhenDynamic(
                    () =>
                        _currentDepotDownloaderVersion == null
                        || _currentDepotDownloaderVersion != DepotDownloaderVersion
                )
                .Executes(async () =>
                {
                    using var httpClient = new HttpClient();

                    string requestUri =
                        $"https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_{DepotDownloaderVersion}/DepotDownloader-windows-x64.zip";

                    await using var contentStream = await httpClient.GetStreamAsync(requestUri);

                    DepotDownloaderDir.ToDirectoryInfo().Create();

                    using var ddZip = new ZipArchive(contentStream, ZipArchiveMode.Read);

                    foreach (ZipArchiveEntry entry in ddZip.Entries)
                    {
                        await using var fs = entry.Open();
                        AbsolutePath filePath = DepotDownloaderDir / entry.FullName;
                        await using var targetFs = new FileStream(filePath, FileMode.OpenOrCreate);

                        await fs.CopyToAsync(targetFs);
                        await targetFs.FlushAsync();
                    }
                });

    Target UpdateOriginalLibs =>
        _ =>
            _.DependsOn(UpdateDepotDownloader)
                .DependsOn(CreateDepotDownloaderFileList)
                .Executes(async () =>
                {
                    string arguments =
                        $"-app {RUST_DS_APPID} -dir {AppDownloadDir} -filelist {DepotDownloaderFileList}";

                    var process = ProcessTasks.StartProcess(
                        DepotDownloaderExecutable,
                        arguments: arguments,
                        workingDirectory: DepotDownloaderDir
                    );

                    int lastOutLine = 0;
                    while (!process.HasExited)
                    {
                        while (lastOutLine < process.Output.Count)
                        {
                            var currentLine = process.Output.Skip(lastOutLine).First();
                            if (currentLine.Type == OutputType.Std)
                            {
                                Log.Information("[DepotDownloader] {Message}", currentLine.Text);
                            }
                            else
                            {
                                Log.Error("[DepotDownloader] {Message}", currentLine.Text);
                            }

                            lastOutLine++;
                        }
                        await Task.Delay(100);
                    }

                    if (process.ExitCode != 0)
                    {
                        throw new Exception(
                            "DepotDownloader has exited with exit code " + process.ExitCode
                        );
                    }

                    Log.Information("DepotDownloader has finished execution");

                    AbsolutePath libsDirectory = AppDownloadDir / "RustDedicated_Data/Managed";

                    ReferencesDir.CreateOrCleanDirectory();

                    Log.Information(
                        "Moving dll files from {SourceDirectory} to {TargetDirectory}",
                        AppDownloadDir,
                        ReferencesDir
                    );

                    var dlls = libsDirectory.GlobFiles("*.dll");
                    int index = 1;
                    foreach (AbsolutePath dllPath in dlls)
                    {
                        Log.Debug(
                            "[{FileIndex} / {FileCount}] Moving file {FilePath}",
                            index,
                            dlls.Count,
                            dllPath
                        );
                        dllPath.MoveToDirectory(ReferencesDir);
                        index++;
                    }
                });

    Target UpdateOxideLibs =>
        _ =>
            _.DependsOn(UpdateOriginalLibs)
                .Executes(async () =>
                {
                    using var httpClient = new HttpClient();
                    Stream oxideZipStream = await httpClient.GetStreamAsync(OXIDE_RUST_WINDOWS_URL);

                    Log.Debug("Downloaded Oxide.Rust archive");

                    using var oxideZipArch = new ZipArchive(oxideZipStream, ZipArchiveMode.Read);

                    foreach (
                        ZipArchiveEntry entry in oxideZipArch.Entries.Where(
                            e => !e.FullName.EndsWith('/')
                        )
                    )
                    {
                        Log.Debug("Processing entry {EntryFullName}", entry.FullName);
                        AbsolutePath targetPath = ReferencesDir / entry.Name;
                        await using var entryFs = entry.Open();
                        await using var targetFs = new FileStream(
                            targetPath,
                            FileMode.OpenOrCreate
                        );

                        await entryFs.CopyToAsync(targetFs);
                        await targetFs.FlushAsync();
                    }
                });

    Target UpdateReferences => _ => _.DependsOn(UpdateOriginalLibs).DependsOn(UpdateOxideLibs);

    Target CleanReferencesDir => _ => _.Executes(ReferencesDir.CreateOrCleanDirectory);
}
