using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API.Modules.Entities;
using System.Numerics;

namespace AdminMenu
{
    public class AdminMenu : BasePlugin
    {
        public override string ModuleName => "AdminMenu";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "AdminMenu";

        public string PluginPrefix = $"[AdminMenu]";

        private static string _adminsFilePath = string.Empty;
        private static string _bannedFilePath = string.Empty;

        public override void Load(bool hotReload)
        {
            Logger?.LogInformation($"Plugin: {ModuleName} - Version: {ModuleVersion} by {ModuleAuthor}");
            Logger?.LogInformation(ModuleDescription);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            _adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
            _bannedFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "banned.json");
        }

        private HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var player = @event.Userid;

            if (player == null)
            {
                return HookResult.Continue;
            }

            if (IsBanned(player))
            {
                Server.PrintToChatAll($"{PluginPrefix} {player.PlayerName}, You are banned from this server.");
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED);
            }
            else
            {
                Server.PrintToChatAll($"{PluginPrefix} Welcome to the server {player.PlayerName}!");
            }

            return HookResult.Continue;
        }

        private bool IsPlayerInList(CCSPlayerController? player, string filePath, string listName)
        {
            if (player == null || player.AuthorizedSteamID == null)
            {
                return false;
            }

            try
            {
                string steamId = player.AuthorizedSteamID.SteamId2;

                if (!File.Exists(filePath))
                {
                    Logger?.LogError($"{listName} file not found: {filePath}");
                    return false;
                }

                string json = File.ReadAllText(filePath);
                var entries = JsonSerializer.Deserialize<Dictionary<string, AdminEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (entries == null)
                {
                    Logger?.LogError($"Failed to deserialize {listName} file: {json}");
                    return false;
                }

                return entries.Values.Any(entry => entry.Identity.Equals(steamId, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error reading {listName} file: {ex.Message}");
                return false;
            }
        }

        private bool IsBanned(CCSPlayerController? player)
        {
            return IsPlayerInList(player, _bannedFilePath, "Banned");
        }

        private bool IsAdmin(CCSPlayerController? player)
        {
            return IsPlayerInList(player, _adminsFilePath, "Admins");
        }

        public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);

            if (player == null)
            {
                return HookResult.Continue;
            }

            if (@event?.Text.Trim().ToLower() == "!admin")
            {
                if (IsAdmin(player))
                {
                    ShowMainMenu(player);
                }
                else
                {
                    player.PrintToChat("You are not an admin.");
                }
            }
            else if (@event?.Text.Trim().ToLower() == "!mysteamid")
            {
                player?.PrintToChat($"SteamID2 : {player?.AuthorizedSteamID?.SteamId2}");
                player?.PrintToChat($"SteamID3 : {player?.AuthorizedSteamID?.SteamId3}");
                player?.PrintToChat($"SteamID32: {player?.AuthorizedSteamID?.SteamId32}");
                player?.PrintToChat($"SteamID64: {player?.AuthorizedSteamID?.SteamId64}");
            }
            else if (@event?.Text.Trim().ToLower() == "!thetime")
            {
                Server.PrintToChatAll($"{DateTime.Now}");
                return HookResult.Handled;
            }
            else if (@event?.Text.Trim().ToLower() == "!status")
            {
                foreach (var statusPlayer in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
                {
                    Server.PrintToConsole($"Player: {statusPlayer.PlayerName} - {statusPlayer.AuthorizedSteamID?.SteamId2}");
                }
            }
            return HookResult.Continue;
        }

        private void ShowMainMenu(CCSPlayerController player)
        {
            var mainMenu = new CenterHtmlMenu($"Choose action", this);

            mainMenu.AddMenuOption("Ban", BanAction);
            mainMenu.AddMenuOption("Kick", KickAction);
            mainMenu.AddMenuOption("Kill", KillAction);
            mainMenu.AddMenuOption("Slap", SlapAction);
            mainMenu.AddMenuOption("DropWeapon", DropWeaponAction);
            mainMenu.AddMenuOption("Respawn", RespawnAction);
            mainMenu.AddMenuOption("Set Team", SetTeamAction);
            mainMenu.AddMenuOption("Bot menu", BotMenuAction);

            MenuManager.OpenCenterHtmlMenu(this, player, mainMenu);
        }

        private void DropWeaponAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) =>
            {
                targetPlayer.DropActiveWeapon();
            });
        }

        private void SlapAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) =>
            {
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

                targetPlayer.EmitSound("player/damage_taken");
            });
        }

        private void BotMenuAction(CCSPlayerController player, ChatMenuOption option)
        {
            var menu = new CenterHtmlMenu($"Choose action", this);

            menu.AddMenuOption("Kick All", (controller, _) =>
                { Server.ExecuteCommand("bot_kick all"); });
            menu.AddMenuOption("Add T", (controller, _) =>
                { Server.ExecuteCommand("bot_add_t"); });
            menu.AddMenuOption("Add CT", (controller, _) =>
                { Server.ExecuteCommand("bot_add_ct"); });

            MenuManager.OpenCenterHtmlMenu(this, player, menu);

        }

        #region Actions

        private void RespawnAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController player) => { player.Respawn(); });
        }

        private void BanAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController player) => { BanPlayer(player); });
        }

        private void KillAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController player) => { player.CommitSuicide(true, true); });
        }

        private void KickAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController player) => { player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED); });
        }

        private void SetTeamAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            var setTeamPlayerMenu = new CenterHtmlMenu($"Choose player", this);

            foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
            {
                setTeamPlayerMenu.AddMenuOption(player.PlayerName, (controller, option) =>
                {
                    ShowTeamMenu(adminPlayer, player);
                });
            }
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, setTeamPlayerMenu);
        }

        private void ShowTeamMenu(CCSPlayerController adminPlayer, CCSPlayerController player)
        {
            var teamsMenu = new CenterHtmlMenu($"Choose team", this);

            teamsMenu.AddMenuOption("Terrorist",
                (CCSPlayerController controller, ChatMenuOption option) => { player.ChangeTeam(CsTeam.Terrorist); });
            teamsMenu.AddMenuOption("CounterTerrorist",
                (CCSPlayerController controller, ChatMenuOption option) => { player.ChangeTeam(CsTeam.CounterTerrorist); });
            teamsMenu.AddMenuOption("Spectator",
                (CCSPlayerController controller, ChatMenuOption option) => { player.ChangeTeam(CsTeam.Spectator); });
            
            teamsMenu.PostSelectAction = PostSelectAction.Close;
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, teamsMenu);
        }

        private void ShowPlayerListMenu(CCSPlayerController adminPlayer, Action<CCSPlayerController> playerAction)
        {
            var playerListMenu = new CenterHtmlMenu($"Choose a player", this);

            foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid))
            {
                playerListMenu.AddMenuOption(player.PlayerName, (controller, option) =>
                {
                    playerAction(player);
                });
            }

            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, playerListMenu);
        }

        private void BanPlayer(CCSPlayerController player)
        {
            if (player == null || player.AuthorizedSteamID == null)
            {
                Logger?.LogError("Player or SteamID is invalid.");
                return;
            }

            try
            {
                string steamId = player.AuthorizedSteamID.SteamId2;
                var bannedList = new Dictionary<string, AdminEntry>();

                if (File.Exists(_bannedFilePath))
                {
                    string json = File.ReadAllText(_bannedFilePath);
                    bannedList = JsonSerializer.Deserialize<Dictionary<string, AdminEntry>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new Dictionary<string, AdminEntry>();
                }

                var newEntry = new AdminEntry
                {
                    Identity = steamId,
                    Flags = new List<string> { $"{player.PlayerName}" }.ToArray()
                };

                bannedList.Add(steamId, newEntry);

                string updatedJson = JsonSerializer.Serialize(bannedList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_bannedFilePath, updatedJson);
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED);
                Logger?.LogInformation($"Player {player.PlayerName} ({steamId}) has been added to the banned list.");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error banning player: {ex.Message}");
            }
        }

        #endregion
    }
}