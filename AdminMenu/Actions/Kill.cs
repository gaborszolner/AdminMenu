using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void KillAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, true, true, (CCSPlayerController targetPlayer) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerKilled", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                targetPlayer.CommitSuicide(true, true);
                MenuManager.GetActiveMenu(adminPlayer)?.Close();
            });
        }
    }
}
