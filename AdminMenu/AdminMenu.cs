using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Text.Json;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace AdminMenu
{
    public class AdminMenu : BasePlugin
    {
        public override string ModuleName => "AdminMenu";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "AdminMenu";

        public string PluginPrefix = $"[Admin]";

        private static string _adminsFilePath = string.Empty;
        private static string _bannedFilePath = string.Empty;
        private static bool _isWarmup = false;

        public override void Load(bool hotReload)
        {
            Logger?.LogInformation($"Plugin: {ModuleName} - Version: {ModuleVersion} by {ModuleAuthor}");
            Logger?.LogInformation(ModuleDescription);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            _adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
            _bannedFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "banned.json");

            AddCommandListenerButtons();
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;
            return HookResult.Continue;
        }

        private void AddCommandListenerButtons()
        {
            AddCommandListener("1", OnKeyPress);
            AddCommandListener("2", OnKeyPress);
            AddCommandListener("3", OnKeyPress);
            AddCommandListener("4", OnKeyPress);
            AddCommandListener("5", OnKeyPress);
            AddCommandListener("6", OnKeyPress);
            AddCommandListener("7", OnKeyPress);
            AddCommandListener("8", OnKeyPress);
            AddCommandListener("9", OnKeyPress);
        }

        private HookResult OnKeyPress(CCSPlayerController? player, CommandInfo command)
        {
            if (player is not null)
            {
                var menu = MenuManager.GetActiveMenu(player);

                if (menu is null)
                {
                    return HookResult.Continue;
                }

                switch (command.GetCommandString)
                {
                    case "1": menu.OnKeyPress(player, 1); break;
                    case "2": menu.OnKeyPress(player, 2); break;
                    case "3": menu.OnKeyPress(player, 3); break;
                    case "4": menu.OnKeyPress(player, 4); break;
                    case "5": menu.OnKeyPress(player, 5); break;
                    case "6": menu.OnKeyPress(player, 6); break;
                    case "7": menu.OnKeyPress(player, 7); break;
                    case "8": menu.OnKeyPress(player, 8); break;
                    case "9": menu.OnKeyPress(player, 9); break;
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var player = @event.Userid;

            if (player is null || player.IsBot)
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
                try
                {
                    if (!_isWarmup)
                    {
                        var audioEvent = new EventTeamplayBroadcastAudio(true)
                        {
                            Sound = "./Resources/PleaseWelcome.wav",
                            Team = -1
                        };

                        audioEvent.FireEvent(false);
                    }
                }
                catch (Exception) { }
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

            if (player is null)
            {
                return HookResult.Continue;
            }

            if (@event?.Text.Trim().ToLower() is "!admin")
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
            else if (@event?.Text.Trim().ToLower() is "!mysteamid")
            {
                player?.PrintToChat($"SteamID2 : {player?.AuthorizedSteamID?.SteamId2}");
                player?.PrintToChat($"SteamID3 : {player?.AuthorizedSteamID?.SteamId3}");
                player?.PrintToChat($"SteamID32: {player?.AuthorizedSteamID?.SteamId32}");
                player?.PrintToChat($"SteamID64: {player?.AuthorizedSteamID?.SteamId64}");
            }
            else if (@event?.Text.Trim().ToLower() is "!thetime")
            {
                Server.PrintToChatAll($"{DateTime.Now}");
                return HookResult.Handled;
            }
            else if (@event?.Text.Trim().ToLower() is "!status")
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
                try
                {
                    targetPlayer.EmitSound("./Resources/Slap.wav");
                }
                catch (Exception){}
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