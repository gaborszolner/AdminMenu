﻿using AdminMenu.Entries;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using GameStatistic;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Text.Json;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace AdminMenu
{
    public class AdminMenu : BasePlugin
    {
        public override string ModuleName => "AdminMenu";
        public override string ModuleVersion => "2.1";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "AdminMenu";

        public string PluginPrefix = $"[Admin]";

        private static string _adminsFilePath = string.Empty;
        private static string _bannedFilePath = string.Empty;
        private static string _statisticFilePath = string.Empty;
        private static string _mapListFilePath = string.Empty;
        private static bool _isWarmup = false;
        private static bool _isRoundEnded = false;

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            AddCommandListener("!admin", OpenAdminMenu);
            _adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
            _bannedFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "banned.json");
            _statisticFilePath = Path.Combine(ModuleDirectory, "..", "GameStatistic", "playerStatistic.json");
            _mapListFilePath = Path.Combine(ModuleDirectory, "..", "RockTheVote", "maplist.txt");
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _isRoundEnded = false;
            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _isRoundEnded = true;
            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;
            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
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
                if (!_isWarmup && !_isRoundEnded)
                {
                    Server.PrintToChatAll($"{PluginPrefix} Welcome to the server {player.PlayerName}!"); 
                }
            }

            return HookResult.Continue;
        }

        private int GetAdminLevelFromConfig(CCSPlayerController player)
        {
            if (player == null || player.AuthorizedSteamID == null)
            {
                return 0;
            }

            string steamId = player.AuthorizedSteamID.SteamId2;

            if (!File.Exists(_adminsFilePath))
            {
                Logger?.LogError($"File not found: {_adminsFilePath}");
                return 0;
            }

            try
            {
                string json = File.ReadAllText(_adminsFilePath);
                Dictionary<string, AdminEntry>? entries = JsonSerializer.Deserialize<Dictionary<string, AdminEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (entries == null)
                {
                    Logger?.LogError($"Failed to deserialize {_adminsFilePath} file: {json}");
                    return 0;
                }
                
                if (!entries.TryGetValue(steamId, out AdminEntry? possibleAdmin))
                {
                    return 0;
                }

                return possibleAdmin is null ? 0 : possibleAdmin.Level;
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error reading {_adminsFilePath} file: {ex.Message}");
                return 0;
            }
        }

        private bool IsBanned(CCSPlayerController? player)
        {
            if (player == null || player.AuthorizedSteamID == null)
            {
                return false;
            }

            try
            {
                string steamId = player.AuthorizedSteamID.SteamId2;

                if (!File.Exists(_bannedFilePath))
                {
                    Logger?.LogError($"File not found: {_bannedFilePath}");
                    return false;
                }

                string json = File.ReadAllText(_bannedFilePath);
                Dictionary<string, BannedEntry>? entries = JsonSerializer.Deserialize
                    <Dictionary<string, BannedEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (entries == null)
                {
                    Logger?.LogError($"Failed to deserialize file: {json}");
                    return false;
                }

                var possibleBanned = entries.Values.Where(entry => entry.Identity.Equals(steamId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (possibleBanned is null)
                {
                    return false;
                }
                else
                {
                    if (possibleBanned.Expiration < DateTime.Now)
                    {
                        entries.Remove(possibleBanned.Identity);
                        string updatedJson = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(_bannedFilePath, updatedJson);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error reading {_bannedFilePath} file: {ex.Message}");
                return false;
            }
        }

        private HookResult OpenAdminMenu(CCSPlayerController? adminPlayer, CommandInfo commandInfo)
        {
            if (adminPlayer is null || !adminPlayer.IsValid)
            {
                return HookResult.Continue;
            }

            if (commandInfo.GetCommandString is "!admin")
            {
                ShowMainMenu(adminPlayer);
            }

            return HookResult.Continue;
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
                ShowMainMenu(player);
            }
            else if (@event?.Text.Trim().ToLower() is "!mysteamid")
            {
                player?.PrintToChat($"SteamID2 : {player?.AuthorizedSteamID?.SteamId2}");
            }
            else if (@event?.Text.Trim().ToLower() is "!thetime")
            {
                Server.PrintToChatAll($"{DateTime.Now}");
                return HookResult.Handled;
            }
            else if (@event?.Text.Trim().ToLower() is "!status")
            {
                foreach (var statusPlayer in GetAllPlayers())
                {
                    Server.PrintToConsole($"Player: {statusPlayer.PlayerName} - {statusPlayer.AuthorizedSteamID?.SteamId2}");
                }
            }
            return HookResult.Continue;
        }

        private static IEnumerable<CCSPlayerController> GetAllPlayers()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot);
        }

        private void ShowMainMenu(CCSPlayerController adminPlayer)
        {
            int adminLevel = GetAdminLevelFromConfig(adminPlayer);

            if (adminLevel == 0)
            {
                adminPlayer.PrintToChat("You are not an admin.");
                return;
            }

            var mainMenu = new CenterHtmlMenu($"Choose action", this);
            if (adminLevel > 1)
            {
                mainMenu.AddMenuOption("Ban", BanAction);
                mainMenu.AddMenuOption("Kick", KickAction);
                mainMenu.AddMenuOption("Kill", KillAction);
                mainMenu.AddMenuOption("Slap", SlapAction);
                mainMenu.AddMenuOption("Set Team", SetTeamAction);
                mainMenu.AddMenuOption("Rename", RanameAction);
            }
            if (adminLevel > 2)
            {
                mainMenu.AddMenuOption("DropWeapon", DropWeaponAction);
                mainMenu.AddMenuOption("Respawn", RespawnAction);
                mainMenu.AddMenuOption("Set Admin", SetAdminAction);
                if (File.Exists(_mapListFilePath)) 
                { 
                    mainMenu.AddMenuOption("Change map", ChangeMapAction); 
                }
                if (File.Exists(_statisticFilePath)) 
                {
                    mainMenu.AddMenuOption("Team shuffle", TeamShuffle);
                }
            }
            if (adminLevel > 0)
            {
                mainMenu.AddMenuOption("Bot menu", BotMenuAction);
            }

            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, mainMenu);
        }

        private void TeamShuffle(CCSPlayerController controller, ChatMenuOption option)
        {
            string json = File.ReadAllText(_statisticFilePath);
            Dictionary<string, StatisticEntry>? stats = JsonSerializer.Deserialize<Dictionary<string, StatisticEntry>>(json);
            if (stats == null) return;

            var teamT = new List<string>();
            var teamCT = new List<string>();
            double scoreA = 0, scoreB = 0;

            var players = GetAllPlayers().Select(p =>
            {
                stats.TryGetValue(p.AuthorizedSteamID.SteamId2, out var s);
                s ??= new StatisticEntry(p.AuthorizedSteamID.SteamId2, p.PlayerName);
                return new  { p.AuthorizedSteamID.SteamId2, s };
            }).OrderByDescending(p => p.s.Score).ToList();

            foreach (var p in players)
            {
                if (teamT.Count <= teamCT.Count && scoreA <= scoreB)
                {
                    teamT.Add(p.SteamId2);
                    scoreA += p.s.Score;
                }
                else
                {
                    teamCT.Add(p.SteamId2);
                    scoreB += p.s.Score;
                }
            }

            foreach (var player in teamT)
            {
                var t = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.AuthorizedSteamID?.SteamId2 == player);
                if (t?.IsValid == true)
                {
                    t.SwitchTeam(CsTeam.Terrorist);
                    t.Respawn();
                }
            }

            foreach (var player in teamCT)
            {
                var ct = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.AuthorizedSteamID?.SteamId2 == player);
                if (ct?.IsValid == true)
                {
                    ct.SwitchTeam(CsTeam.CounterTerrorist);
                    ct.Respawn();
                }
            }

        }

        private void ChangeMapAction(CCSPlayerController player, ChatMenuOption option)
        {
            Dictionary<string, string> mapList = GetMaps(_mapListFilePath);
            var mapMenu = new CenterHtmlMenu($"Choose map", this);
            foreach (var map in mapList)
            {
                mapMenu.AddMenuOption(map.Key, (CCSPlayerController player, ChatMenuOption menuOption) =>
                {
                    if (Server.IsMapValid(map.Key))
                    {
                        Server.ExecuteCommand($"changelevel {map.Key}");
                    }
                    else if (map.Value is not null)
                    {
                        Server.ExecuteCommand($"host_workshop_map {map.Value}");
                    }
                    else
                    {
                        Server.ExecuteCommand($"ds_workshop_changelevel {map.Key}");
                    }
                });
            }

            MenuManager.OpenCenterHtmlMenu(this, player, mapMenu);
        }

        private Dictionary<string, string> GetMaps(string mapListFilePath)
        {
            Dictionary<string, string> mapList = [];
            if (File.Exists(mapListFilePath))
            {
                foreach (var line in File.ReadLines(mapListFilePath).Where(l => !l.StartsWith(@"//")))
                {
                    try
                    {

                        var parts = line.Split(':');

                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();

                            mapList[key] = value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError($"Error parsing line '{line}' in map list file: {ex.Message}");
                    }
                }
            }
            else
            {
                Logger?.LogError($"Map list file not found: {mapListFilePath}");
            }
            return mapList;
        }

        private void RanameAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) =>
            {
                string oldName = targetPlayer.PlayerName;
                targetPlayer.PlayerName = RandomString(12);
                Server.PrintToChatAll($"{PluginPrefix} {oldName} name has been renamed to {targetPlayer.PlayerName} by {adminPlayer.PlayerName}.");
            });
        }

        private static string RandomString(int length)
        {
            Random random = new();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string([.. Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)])]);
        }

        private void SetAdminAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) =>
            {
                var setAdminMenu = new CenterHtmlMenu($"Set admin level for {targetPlayer.PlayerName}", this);
                setAdminMenu.AddMenuOption("Level 1", (controller, _) =>
                {
                    SetAdminLevel(targetPlayer, 1);
                });
                setAdminMenu.AddMenuOption("Level 2", (controller, _) =>
                {
                    SetAdminLevel(targetPlayer, 2);
                });
                setAdminMenu.AddMenuOption("Level 3", (controller, _) =>
                {
                    SetAdminLevel(targetPlayer, 3);
                });
                setAdminMenu.AddMenuOption("Delete admin", (controller, _) =>
                {
                    SetAdminLevel(targetPlayer, 0);
                });
                setAdminMenu.PostSelectAction = PostSelectAction.Close;
                MenuManager.OpenCenterHtmlMenu(this, adminPlayer, setAdminMenu);
            });
        }

        private static void SetAdminLevel(CCSPlayerController targetPlayer, int adminLevel)
        {
            if (targetPlayer == null || targetPlayer.AuthorizedSteamID == null)
            {
                return;
            }

            UpdateConfig(targetPlayer, adminLevel);

            targetPlayer.PrintToChat($"Your admin level has been set to {adminLevel}.");

        }

        private static void UpdateConfig(CCSPlayerController targetPlayer, int adminLevel)
        {
            if (targetPlayer == null || targetPlayer.AuthorizedSteamID == null)
            {
                return;
            }

            var newEntry = new AdminEntry
            {
                Name = targetPlayer.PlayerName,
                Identity = targetPlayer.AuthorizedSteamID.SteamId2,
                Level = adminLevel
            };

            string steamId = targetPlayer.AuthorizedSteamID.SteamId2;
            Dictionary<string, AdminEntry> adminDictionary;
            if (File.Exists(_adminsFilePath))
            {
                string json = File.ReadAllText(_adminsFilePath);
                adminDictionary = JsonSerializer.Deserialize<Dictionary<string, AdminEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new Dictionary<string, AdminEntry>();
            }
            else
            {
                adminDictionary = [];
            }

            adminDictionary[steamId] = newEntry;

            var options = new JsonSerializerOptions { WriteIndented = true };

            string updatedJson = JsonSerializer.Serialize(adminDictionary, options);
            File.WriteAllText(_adminsFilePath, updatedJson);
        }

        private void DropWeaponAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) =>
            {
                string? weaponName = targetPlayer.Pawn.Value?.WeaponServices?.ActiveWeapon?.Value?.DesignerName;
                targetPlayer.DropActiveWeapon();
                if (weaponName != null)
                {
                    Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has dropped their weapon: {weaponName}");
                }
                else
                {
                    Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has dropped their weapon.");
                }
            });
        }

        private void SlapAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has been slapped by {adminPlayer.PlayerName}.");
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

        private void BotMenuAction(CCSPlayerController player, ChatMenuOption option)
        {
            var menu = new CenterHtmlMenu($"Choose bot action", this);

            menu.AddMenuOption("Kick All", (controller, _) =>
                { Server.ExecuteCommand("bot_kick all"); });
            menu.AddMenuOption("Add T", (controller, _) =>
                { Server.ExecuteCommand("bot_add_t"); });
            menu.AddMenuOption("Add CT", (controller, _) =>
                { Server.ExecuteCommand("bot_add_ct"); });

            MenuManager.OpenCenterHtmlMenu(this, player, menu);
        }

        private void RespawnAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) => 
            {
                Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has been respawned by {adminPlayer.PlayerName}.");
                targetPlayer.Respawn(); 
            });
        }

        private void BanAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) => { ChooseBanTimePlayer(adminPlayer, targetPlayer); });
        }

        private void KillAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) => 
            {
                Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has been killed by {adminPlayer.PlayerName}.");
                targetPlayer.CommitSuicide(true, true);
            });
        }

        private void KickAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, (CCSPlayerController targetPlayer) => 
            {
                Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has been kicked by {adminPlayer.PlayerName}.");
                targetPlayer.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED); 
            });


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
            int adminLevel = GetAdminLevelFromConfig(adminPlayer);

            teamsMenu.AddMenuOption("Terrorist",
                (CCSPlayerController controller, ChatMenuOption option) => { player.ChangeTeam(CsTeam.Terrorist); });

            if (adminLevel > 2)
            {
                teamsMenu.AddMenuOption("Terrorist + Respawn",
                (CCSPlayerController controller, ChatMenuOption option) => { player.ChangeTeam(CsTeam.Terrorist); player.Respawn(); });
            }

            teamsMenu.AddMenuOption("CounterTerrorist",
                (CCSPlayerController controller, ChatMenuOption option) => { player.ChangeTeam(CsTeam.CounterTerrorist); });

            if (adminLevel > 2)
            {
                teamsMenu.AddMenuOption("CounterTerrorist + Respawn",
                (CCSPlayerController controller, ChatMenuOption option) => { player.ChangeTeam(CsTeam.CounterTerrorist); player.Respawn(); });
            }

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
                    if (GetAdminLevelFromConfig(adminPlayer) < GetAdminLevelFromConfig(player))
                    {
                        adminPlayer.PrintToCenter($"You cannot perform actions on {player.PlayerName} as they have a higher admin level than you.");
                    }
                    else
                    {
                        playerAction(player);
                    }
                });
            }

            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, playerListMenu);
        }

        private void ChooseBanTimePlayer(CCSPlayerController adminPlayer, CCSPlayerController player)
        {
            if (player == null || player.AuthorizedSteamID == null)
            {
                Logger?.LogError("Player or SteamID is invalid.");
                return;
            }

            var banTimeMenu = new CenterHtmlMenu($"Expiration time?", this);
            banTimeMenu.AddMenuOption("10 min", (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, DateTime.Now.AddMinutes(10));
            });
            banTimeMenu.AddMenuOption("1 day", (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, DateTime.Now.AddDays(1));
            });
            banTimeMenu.AddMenuOption("1 week", (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, DateTime.Now.AddDays(7));
            });
            banTimeMenu.AddMenuOption("Permanent", (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, DateTime.MaxValue);
            });

            banTimeMenu.PostSelectAction = PostSelectAction.Close;
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, banTimeMenu);
        }

        private void BanPlayer(CCSPlayerController adminPlayer, CCSPlayerController player, DateTime banTime)
        {
            try
            {
                string steamId = player.AuthorizedSteamID.SteamId2;
                var bannedList = new Dictionary<string, BannedEntry>();

                if (File.Exists(_bannedFilePath))
                {
                    string json = File.ReadAllText(_bannedFilePath);
                    bannedList = JsonSerializer.Deserialize<Dictionary<string, BannedEntry>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new Dictionary<string, BannedEntry>();
                }

                var newEntry = new BannedEntry
                {
                    Identity = steamId,
                    Name = player.PlayerName,
                    BannedBy = adminPlayer.PlayerName,
                    Expiration = banTime
                };

                bannedList.Add(steamId, newEntry);

                string updatedJson = JsonSerializer.Serialize(bannedList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_bannedFilePath, updatedJson);
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED);
                Server.PrintToChatAll($"{PluginPrefix} {player.PlayerName} has been banned by {adminPlayer.PlayerName} until {banTime}.");
                Logger?.LogInformation($"{PluginPrefix} {player.PlayerName} has been banned by {adminPlayer.PlayerName} until {banTime}.");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error banning player: {ex.Message}");
            }
        }
    }
}