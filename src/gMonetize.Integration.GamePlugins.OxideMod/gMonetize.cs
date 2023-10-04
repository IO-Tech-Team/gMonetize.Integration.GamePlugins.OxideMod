using System.Diagnostics;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("gMonetize", "gMonetize Project", "2.0.0")]
    public class gMonetize : CovalencePlugin
    {
        private const string PERMISSION_USE = "gmonetize.use";
        public const string CMD_OPEN = "gmonetize.open";
        public const string CMD_CLOSE = "gmonetize.close";
        public const string CMD_NEXT_PAGE = "gmonetize.nextpage";
        public const string CMD_PREV_PAGE = "gmonetize.prevpage";
        public const string CMD_RETRY_LOAD = "gmonetize.retryload";
        public const string CMD_REDEEM_ITEM = "gmonetize.redeemitem";

        private static gMonetize Instance;

        [Conditional("DEBUG")]
        private static void LogDebug(string format, params object[] args)
        {
            Interface.Oxide.LogDebug("[gMonetize] " + format, args);
        }

        void Init()
        {
            Instance = this;
        }

        void OnServerInitialized()
        {
        }

        private bool CanUsePlugin(IPlayer player) => player.HasPermission(PERMISSION_USE);
    }
}