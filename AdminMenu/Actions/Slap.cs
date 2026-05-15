using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;


namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void SlapAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, true, true, (CCSPlayerController targetPlayer) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerSlapped", targetPlayer.PlayerName, adminPlayer.PlayerName)}");
                var pawn = targetPlayer.PlayerPawn.Value;
                if (pawn == null)
                {
                    return;
                }
                var currentPos = pawn.AbsOrigin;

                var random = new Random();
                float offsetZ = 100.0f + (float)(random.NextDouble() * 150.0f);

                var newPosition = currentPos + new Vector(0, 0, offsetZ);
                pawn.Teleport(newPosition, pawn.AbsRotation, null);
            });
        }
    }
}
