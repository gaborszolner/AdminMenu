using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void DropWeaponAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, true, true, (CCSPlayerController targetPlayer) =>
            {
                string? weaponName = targetPlayer.Pawn.Value?.WeaponServices?.ActiveWeapon?.Value?.DesignerName;
                targetPlayer.DropActiveWeapon();
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerDroppedWeapon", targetPlayer.PlayerName, weaponName)}");
            });
        }
    }
}
