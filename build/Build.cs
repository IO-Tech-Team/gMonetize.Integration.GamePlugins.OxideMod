using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Build.Tasks;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

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
    AbsolutePath DepotDownloaderDir => ToolsDir / "DepotDownloader";
    AbsolutePath DepotDownloaderExecutable => DepotDownloaderDir / "DepotDownloader.exe";

    Target GetCurrentDepotDownloaderVersion =>
        _ =>
            _.Executes(() =>
            {
                if (DepotDownloaderExecutable.ToFileInfo().Exists)
                {
                    _currentDepotDownloaderVersion = FileVersionInfo
                        .GetVersionInfo(DepotDownloaderExecutable)
                        .ProductVersion!;
                }
            });

    Target UpdateDepotDownloader => _ => _
        .OnlyWhenDynamic(() => _currentDepotDownloaderVersion == null || _currentDepotDownloaderVersion != DepotDownloaderVersion)
        .Executes((Func<Task>)(async () =>
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
        }));

    Target UpdateOriginalLibs => _ => _.DependsOn(UpdateDepotDownloader).Executes(() => { });

    Target UpdateOxideLibs => _ => _.Executes(() => { });

    Target UpdateReferences => _ => _.DependsOn(UpdateOriginalLibs).DependsOn(UpdateOxideLibs);
}
