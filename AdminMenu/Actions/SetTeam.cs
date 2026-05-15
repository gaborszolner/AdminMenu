using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void SetTeamAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                ShowTeamMenu(adminPlayer, targetPlayer);
            });
        }

        private void ShowTeamMenu(CCSPlayerController adminPlayer, CCSPlayerController targetPlayer)
        {
            var teamsMenu = new CenterHtmlMenu(Msg.Get("ChooseTeam"), this);
            int adminLevel = GetAdminLevel(adminPlayer);

            teamsMenu.AddMenuOption(Msg.Get("TeamTerrorist"),
                (CCSPlayerController controller, ChatMenuOption option) =>
                {
                    targetPlayer.SwitchTeam(CsTeam.Terrorist);
                    Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerAssignedTerrorist", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                });

            if (adminLevel > 2)
            {
                teamsMenu.AddMenuOption(Msg.Get("TeamTerroristRespawn"),
                (CCSPlayerController controller, ChatMenuOption option) =>
                {
                    targetPlayer.SwitchTeam(CsTeam.Terrorist); targetPlayer.Respawn();
                    Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerAssignedTerroristRespawn", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                });
            }

            teamsMenu.AddMenuOption(Msg.Get("TeamCounterTerrorist"),
                (CCSPlayerController controller, ChatMenuOption option) =>
                {
                    targetPlayer.SwitchTeam(CsTeam.CounterTerrorist);
                    Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerAssignedCT", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                });

            if (adminLevel > 2)
            {
                teamsMenu.AddMenuOption(Msg.Get("TeamCounterTerroristRespawn"),
                (CCSPlayerController controller, ChatMenuOption option) =>
                {
                    targetPlayer.SwitchTeam(CsTeam.CounterTerrorist); targetPlayer.Respawn();
                    Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerAssignedCTRespawn", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                });
            }

            teamsMenu.AddMenuOption(Msg.Get("TeamSpectator"),
                (CCSPlayerController controller, ChatMenuOption option) =>
                {
                    targetPlayer.ChangeTeam(CsTeam.Spectator);
                    Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerAssignedSpectator", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                });

            teamsMenu.PostSelectAction = PostSelectAction.Close;
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, teamsMenu);
        }
    }
}




