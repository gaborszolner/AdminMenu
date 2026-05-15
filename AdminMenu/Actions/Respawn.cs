using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void RespawnAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerRespawned", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                targetPlayer.Respawn();
                MenuManager.GetActiveMenu(adminPlayer)?.Close();
            });
        }
    }
}
