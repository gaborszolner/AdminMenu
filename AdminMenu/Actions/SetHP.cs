using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using SharedLibrary;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void SetHPAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            var setHPMenu = new CenterHtmlMenu(Msg.Get("SetHPMenuTitle"), this);
            setHPMenu.AddMenuOption("1", (controller, _) => { SetHP(adminPlayer, 1); });
            setHPMenu.AddMenuOption("10", (controller, _) => { SetHP(adminPlayer, 10); });
            setHPMenu.AddMenuOption("50", (controller, _) => { SetHP(adminPlayer, 50); });
            setHPMenu.AddMenuOption("100", (controller, _) => { SetHP(adminPlayer, 100); });
            setHPMenu.AddMenuOption("500", (controller, _) => { SetHP(adminPlayer, 500); });
            setHPMenu.AddMenuOption("1000", (controller, _) => { SetHP(adminPlayer, 1000); });
            setHPMenu.AddMenuOption("10000", (controller, _) => { SetHP(adminPlayer, 10000); });
            setHPMenu.AddMenuOption("100000", (controller, _) => { SetHP(adminPlayer, 100000); });
            setHPMenu.PostSelectAction = PostSelectAction.Close;
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, setHPMenu);
        }

        private static void SetHP(CCSPlayerController adminPlayer, int hp)
        {
            foreach (var player in PlayerHelper.GetAllPlayersAlive())
            {
                var pawn = player.PlayerPawn.Value;
                if (pawn != null)
                {
                    if (hp > pawn.MaxHealth)
                    {
                        pawn.MaxHealth = hp;
                    }
                    pawn.Health = hp;
                    Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
                }
            }
            Server.PrintToChatAll(Msg.Get("HPSetForAll", adminPlayer.PlayerName, hp));
            MenuManager.GetActiveMenu(adminPlayer)?.Close();
        }

    }
}
