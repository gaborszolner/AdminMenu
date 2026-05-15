using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void UnMuteAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                Server.PrintToChatAll($"{Msg.Get("PlayerUnmuted", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                targetPlayer.VoiceFlags = targetPlayer.VoiceFlags &= ~VoiceFlags.Muted;
                MenuManager.GetActiveMenu(adminPlayer)?.Close();
                Logger?.LogInformation($"{PluginPrefix} {targetPlayer.PlayerName} has been unmuted by {adminPlayer.PlayerName}.");
            });
        }
    }
}
