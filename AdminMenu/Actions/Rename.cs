using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using SharedLibrary;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void RenameAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                if (adminPlayer?.AuthorizedSteamID == null)
                {
                    adminPlayer?.PrintToChat(Msg.Get("RenameStartError"));
                    return;
                }

                var adminSteam2 = adminPlayer.AuthorizedSteamID.SteamId2;

                _pendingRename ??= [];
                lock (_pendingRenameLock)
                {
                    _pendingRename[adminSteam2] = new PendingRenameEntry
                    {
                        TargetSteamId2 = targetPlayer.AuthorizedSteamID?.SteamId2 ?? string.Empty,
                        Expiration = Utils.GetServerTime().AddSeconds(30),
                        OldName = targetPlayer.PlayerName,
                        AdminName = adminPlayer.PlayerName,
                        AdminSteamId2 = adminSteam2
                    };
                }

                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("RenamePrompt", targetPlayer.PlayerName)}");
                MenuManager.GetActiveMenu(adminPlayer)?.Close();
            });
        }

        private class PendingRenameEntry
        {
            public string TargetSteamId2 { get; set; } = string.Empty;
            public DateTime Expiration { get; set; }
            public string OldName { get; set; } = string.Empty;
            public string AdminName { get; set; } = string.Empty;
            public string AdminSteamId2 { get; set; } = string.Empty;
        }
    }
}
