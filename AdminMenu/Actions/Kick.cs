using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void KickAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerKicked", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                targetPlayer.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                MenuManager.GetActiveMenu(adminPlayer)?.Close();
                Logger?.LogInformation($"{PluginPrefix} {targetPlayer.PlayerName} has been kicked by {adminPlayer.PlayerName}.");
            });
        }
    }
}
