using AdminMenu.Entries;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using GameStatistic;
using Microsoft.Extensions.Logging;
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
        private static string _weaponRestrictFilePath = string.Empty;
        private static string _statisticFilePath = string.Empty;
        private static string _mapListFilePath = string.Empty;
        private static bool _isWarmup = false;
        private static bool _isRoundEnded = false;
        private static Dictionary<string, WeaponRestrictEntry>? _weaponRestrictEntry;
        private static Dictionary<string, AdminEntry>? _adminEntry;
        private static Dictionary<string, BannedEntry>? _bannedEntry;

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventItemPickup>(OnItemPickup);
            RegisterEventHandler<EventItemEquip>(OnItemEquip);
            AddCommandListener("!admin", OpenAdminMenu);

            _adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
            _bannedFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "banned.json");
            _weaponRestrictFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "weaponRestrict.json");
            _statisticFilePath = Path.Combine(ModuleDirectory, "..", "GameStatistic", "playerStatistic.json");
            _mapListFilePath = Path.Combine(ModuleDirectory, "..", "RockTheVote", "maplist.txt");
            
            _adminEntry = LoadDataFromFile<AdminEntry>(_adminsFilePath);
            _bannedEntry = LoadDataFromFile<BannedEntry>(_bannedFilePath);
            _weaponRestrictEntry = LoadDataFromFile<WeaponRestrictEntry>(_weaponRestrictFilePath);
        }

        private static Dictionary<string, T>? LoadDataFromFile<T>(string filePath)
        {
            if (File.Exists(filePath))
            {
                string? json = File.ReadAllText(filePath);
                try
                {
                    return JsonSerializer.Deserialize<Dictionary<string, T>>
                        (json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (Exception)
                {
                    Server.PrintToConsole($"Failed to deserialize file: {json}");
                    return null;
                }
            }
            else
            {
                return null;
            }
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

        private HookResult OnItemEquip(EventItemEquip @event, GameEventInfo info)
        {
            ThrowForbiddenWeapon(@event.Userid);
            return HookResult.Continue;
        }

        private HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
        {
            ThrowForbiddenWeapon(@event.Userid);
            return HookResult.Continue;
        }

        private void ThrowForbiddenWeapon(CCSPlayerController? player)
        {
            var pawn = player?.PlayerPawn.Value;

            if (player is null || !player.IsValid || pawn is null || _weaponRestrictEntry is null || _isWarmup)
            {
                return;
            }

            if (GetAdminLevel(player) > 2) { return; }

            var weapon = pawn.WeaponServices?.ActiveWeapon.Value;
            string weaponName = weapon?.DesignerName ?? string.Empty;
            string mapName = Server.MapName.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(weaponName)) { return; }

            if (_weaponRestrictEntry.ContainsKey(weaponName))
            {
                var restrictedWeaponMapList = _weaponRestrictEntry[weaponName];
                if (restrictedWeaponMapList is not null &&
                    (restrictedWeaponMapList.Maps.Contains("*") || restrictedWeaponMapList.Maps.Contains(mapName)))
                {
                    player.DropActiveWeapon();
                    weapon?.Remove();
                    player.PrintToChat($"{PluginPrefix} You cannot use {weaponName}.");
                }
            }
            else
            {
                return;
            }

            return;
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
                if (!_isWarmup)
                {
                    Server.PrintToChatAll($"{PluginPrefix} Welcome to the server {player.PlayerName}!");
                    player.PrintToChat($"{PluginPrefix} Type !help to see available commands.");
                }
            }

            return HookResult.Continue;
        }

        private static int GetAdminLevel(CCSPlayerController player)
        {
            if (player is null || player.AuthorizedSteamID is null)
            {
                return 0;
            }

            string steamId = player.AuthorizedSteamID.SteamId2;

            if (_adminEntry is null || !_adminEntry.ContainsKey(steamId))
            {
                return 0;
            }
            else
            {
                return _adminEntry[steamId].Level;
            }
        }

        private bool IsBanned(CCSPlayerController? player)
        {
            if (player is null || player.AuthorizedSteamID is null)
            {
                return false;
            }
            
            try
            {
                string steamId = player.AuthorizedSteamID.SteamId2;

                BannedEntry? possibleBanned = null;

                if (_bannedEntry is not null && _bannedEntry.ContainsKey(steamId))
                {
                    possibleBanned = _bannedEntry[steamId];
                    if (possibleBanned.Expiration < DateTime.Now)
                    {
                        _bannedEntry.Remove(possibleBanned.Identity);
                        WriteToFile(_bannedEntry, _bannedFilePath);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
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
            if (@event?.Text.Trim().ToLower() is "!admins")
            {
                string adminList = "Admins online: ";
                foreach (var adminPlayer in GetAllPlayers().Where(p => GetAdminLevel(p) > 0))
                {
                    adminList += $"{adminPlayer.PlayerName}, ";
                }
                adminList = adminList.TrimEnd(' ', ',');
                Server.PrintToChatAll($"{PluginPrefix} {adminList}");
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
                int adminLevel = GetAdminLevel(player);

                if (adminLevel > 2)
                {
                    foreach (var statusPlayer in GetAllPlayers())
                    {
                        Server.PrintToConsole($"Player: {statusPlayer.PlayerName} - {statusPlayer.AuthorizedSteamID?.SteamId2}");
                    }
                }
            }
            else if (@event?.Text.Trim().ToLower() is "!weapons")
            {
                string mapName = Server.MapName.Trim() ?? string.Empty;
                string weaponList = GetRestrictedWeapons(mapName);
                Server.PrintToChatAll($"{PluginPrefix} Restricted weapon on map {mapName}: {weaponList.Replace("weapon_", "")}");
            }
            else if (@event?.Text.Trim().ToLower() is "!help")
            {
                int adminLevel = GetAdminLevel(player);

                if (adminLevel > 2)
                {
                    Server.PrintToChatAll($"{PluginPrefix} Available commands: !admin, !admins, !mysteamid, !thetime, !weapons, !status (only level 3 admin), !help");
                }
                else
                {
                    Server.PrintToChatAll($"{PluginPrefix} Available commands: !admin, !admins, !mysteamid, !thetime, !weapons, !help");
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
            int adminLevel = GetAdminLevel(adminPlayer);

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
                mainMenu.AddMenuOption("DropWeapon", DropWeaponAction);
                mainMenu.AddMenuOption("Set Team", SetTeamAction);
                mainMenu.AddMenuOption("Rename", RanameAction);
            }
            if (adminLevel > 2)
            {
                mainMenu.AddMenuOption("Weapon (Un)Restrict", WeaponRestrictAction);
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
            var statEntry = LoadDataFromFile<StatisticEntry>(_statisticFilePath);

            if (statEntry is null)
            {
                return;
            }

            var players = GetAllPlayers().Select(p =>
            {
                statEntry.TryGetValue(p.AuthorizedSteamID.SteamId2, out var s);
                s ??= new StatisticEntry(p.AuthorizedSteamID.SteamId2, p.PlayerName);
                return new { p.AuthorizedSteamID.SteamId2, s };
            }).OrderByDescending(p => p.s.Score).ToList();

            int totalPlayers = players.Count();
            int maxTeamSizeT = totalPlayers / 2 + (totalPlayers % 2);
            int maxTeamSizeCT = totalPlayers / 2;

            var sorted = players.OrderByDescending(p => p.s.Score).ToList();

            double sumScoreT = 0;
            double sumScoreCT = 0;

            List<string> teamT = new();
            List<string> teamCT = new();

            foreach (var p in sorted)
            {
                bool teamTHasSpace = teamT.Count < maxTeamSizeT;
                bool teamCTHasSpace = teamCT.Count < maxTeamSizeCT;

                if (!teamTHasSpace)
                {
                    teamCT.Add(p.SteamId2);
                    sumScoreCT += p.s.Score;
                    continue;
                }

                if (!teamCTHasSpace)
                {
                    teamT.Add(p.SteamId2);
                    sumScoreT += p.s.Score;
                    continue;
                }

                if (sumScoreT <= sumScoreCT)
                {
                    teamT.Add(p.SteamId2);
                    sumScoreT += p.s.Score;
                }
                else
                {
                    teamCT.Add(p.SteamId2);
                    sumScoreCT += p.s.Score;
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
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                string oldName = targetPlayer.PlayerName;
                targetPlayer.PlayerName = RandomString(12);
                Server.PrintToChatAll($"{PluginPrefix} {oldName} has been renamed to {targetPlayer.PlayerName} by {adminPlayer.PlayerName}.");
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
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
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

        private void SetAdminLevel(CCSPlayerController targetPlayer, int adminLevel)
        {
            if (targetPlayer == null || targetPlayer.AuthorizedSteamID == null)
            {
                return;
            }

            UpdateAdminConfig(targetPlayer, adminLevel);

            targetPlayer.PrintToChat($"Your admin level has been set to {adminLevel}.");

        }

        private void UpdateAdminConfig(CCSPlayerController targetPlayer, int adminLevel)
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
            _adminEntry ??= [];
            _adminEntry[steamId] = newEntry;

            WriteToFile(_adminEntry, _adminsFilePath);
        }

        private void WeaponRestrictAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            string mapName = Server.MapName.Trim() ?? string.Empty;
            var restrictMenu = new CenterHtmlMenu($"Choose an action", this);

            restrictMenu.AddMenuOption("Restrict - this map", (controller, _) => { ShowRestrictWeaponsMenu(adminPlayer, true, mapName); });
            restrictMenu.AddMenuOption("Unrestrict - this map", (controller, _) => { ShowRestrictWeaponsMenu(adminPlayer, false, mapName); });
            restrictMenu.AddMenuOption("Restrict - all maps", (controller, _) => { ShowRestrictWeaponsMenu(adminPlayer, true, "*"); });
            restrictMenu.AddMenuOption("Unrestrict - all maps", (controller, _) => { ShowRestrictWeaponsMenu(adminPlayer, false, "*"); });
            restrictMenu.AddMenuOption("Show restricted weapons", ShowRestrictedWeapons(mapName));
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, restrictMenu);
        }

        private void ShowRestrictWeaponsMenu(CCSPlayerController adminPlayer, bool isRestrict, string mapName)
        {
            //3 missing weapons from api: "weapon_m4a1_silencer" == weapon_m4a1, "weapon_hkp2000" == "weapon_usp_silencer", "weapon_deagle" == "weapon_revolver"
            string[] WeaponAll = { "weapon_g3sg1", "weapon_scar20", "weapon_ssg08", "weapon_awp", "weapon_famas", "weapon_galilar", "weapon_ak47", "weapon_m4a1", "weapon_aug", "weapon_sg556", "weapon_bizon", "weapon_mac10", "weapon_mp5sd", "weapon_mp7", "weapon_mp9", "weapon_p90", "weapon_ump45", "weapon_m249", "weapon_negev", "weapon_mag7", "weapon_nova", "weapon_sawedoff", "weapon_xm1014", "weapon_cz75a", "weapon_deagle", "weapon_fiveseven", "weapon_elite", "weapon_glock", "weapon_hkp2000", "weapon_p250", "weapon_tec9" };
            var weaponMenu = new CenterHtmlMenu($"Choose weapon", this);

            foreach (var weapon in WeaponAll)
            {
                if (isRestrict)
                {
                    weaponMenu.AddMenuOption(weapon.Replace("weapon_", ""), (controller, _) => { RestrictWeapon(adminPlayer, weapon, mapName); });
                }
                else
                {
                    weaponMenu.AddMenuOption(weapon.Replace("weapon_", ""), (controller, _) => { UnrestrictWeapon(adminPlayer, weapon, mapName); });
                }
            }
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, weaponMenu);
        }

        private void UnrestrictWeapon(CCSPlayerController adminPlayer, string weapon, string unrestrictMapName)
        {
            _weaponRestrictEntry ??= [];

            if (_weaponRestrictEntry.ContainsKey(weapon))
            {
                if (unrestrictMapName == "*")
                {
                    _weaponRestrictEntry.Remove(weapon);
                }
                else
                {
                    if (_weaponRestrictEntry[weapon].Maps.Contains(unrestrictMapName))
                    {
                        if (_weaponRestrictEntry[weapon].Maps.First() == "*")
                        {
                            adminPlayer.PrintToChat($"{PluginPrefix} Unrestrict from all map first.");
                            return;
                        }
                        else
                        {

                            _weaponRestrictEntry[weapon].Maps = _weaponRestrictEntry[weapon].Maps.Where(m => m != unrestrictMapName).ToArray();
                        }
                    }
                    if (_weaponRestrictEntry[weapon].Maps.Length == 0)
                    {
                        _weaponRestrictEntry.Remove(weapon);
                    }
                }
            }
            else
            {
                adminPlayer.PrintToChat($"{PluginPrefix} {weapon} is not restricted on map {unrestrictMapName}.");
                return;
            }

            WriteToFile(_weaponRestrictEntry, _weaponRestrictFilePath);

            Server.PrintToChatAll($"{PluginPrefix} {weapon} has been unrestricted on map {unrestrictMapName} by {adminPlayer.PlayerName}.");
            MenuManager.GetActiveMenu(adminPlayer)?.Close();
        }

        private void RestrictWeapon(CCSPlayerController adminPlayer, string weapon, string restrictMapName)
        {
            _weaponRestrictEntry ??= [];

            if (!_weaponRestrictEntry.ContainsKey(weapon))
            {
                _weaponRestrictEntry.Add(weapon, new WeaponRestrictEntry { Maps = [restrictMapName] });
            }
            else
            {
                if (restrictMapName == "*")
                {
                    _weaponRestrictEntry[weapon].Maps = ["*"];
                }
                else
                {
                    if (!_weaponRestrictEntry[weapon].Maps.Contains(restrictMapName) && _weaponRestrictEntry[weapon].Maps.First() != "*")
                    {
                        _weaponRestrictEntry[weapon].Maps = _weaponRestrictEntry[weapon].Maps.Append(restrictMapName).ToArray();
                    }
                }
            }

            foreach (var currentPlayer in GetAllPlayers())
            {
                ThrowForbiddenWeapon(currentPlayer);
            }

            WriteToFile(_weaponRestrictEntry, _weaponRestrictFilePath);

            Server.PrintToChatAll($"{PluginPrefix} {weapon.Replace("weapon_", "")} has been restricted on map {restrictMapName} by {adminPlayer.PlayerName}.");
            MenuManager.GetActiveMenu(adminPlayer)?.Close();
        }

        private void WriteToFile<T>(T entry, string fileName)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJson = JsonSerializer.Serialize(entry, options);
                File.WriteAllText(fileName, updatedJson);
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error writing to file {fileName}: {ex.Message}");
                Server.PrintToConsole($"Error writing to file {fileName}");
            }
        }

        private Action<CCSPlayerController, ChatMenuOption> ShowRestrictedWeapons(string mapName)
        {
            return (controller, option) =>
            {
                string weaponList = GetRestrictedWeapons(mapName);
                Server.PrintToChatAll($"{PluginPrefix} Restricted weapon on map {mapName}: {weaponList.Replace("weapon_", "")}");
            };
        }

        private static string GetRestrictedWeapons(string mapName)
        {
            string weaponList = string.Empty;
            foreach (var weapon in _weaponRestrictEntry ?? [])
            {
                if (weapon.Value.Maps.Contains("*") || weapon.Value.Maps.Contains(mapName))
                {
                    weaponList += $"{weapon.Key}, ";
                }
            }
            weaponList = string.IsNullOrWhiteSpace(weaponList) ? "No restricted weapon." : weaponList.TrimEnd(' ', ',');
            return weaponList;
        }

        private void DropWeaponAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, true, true, (CCSPlayerController targetPlayer) =>
            {
                string? weaponName = targetPlayer.Pawn.Value?.WeaponServices?.ActiveWeapon?.Value?.DesignerName;
                targetPlayer.DropActiveWeapon();
                Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has dropped their weapon: {weaponName}");
            });
        }

        private void SlapAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, true, true, (CCSPlayerController targetPlayer) =>
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
            var botMenu = new CenterHtmlMenu($"Choose bot action", this);

            botMenu.AddMenuOption("Kick All", (controller, _) =>
                { Server.ExecuteCommand("bot_kick all"); });
            botMenu.AddMenuOption("Add T", (controller, _) =>
                { Server.ExecuteCommand("bot_add_t"); });
            botMenu.AddMenuOption("Add CT", (controller, _) =>
                { Server.ExecuteCommand("bot_add_ct"); });

            MenuManager.OpenCenterHtmlMenu(this, player, botMenu);
        }

        private void RespawnAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has been respawned by {adminPlayer.PlayerName}.");
                targetPlayer.Respawn();
            });
        }

        private void BanAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) => { ChooseBanTimePlayer(adminPlayer, targetPlayer); });
        }

        private void KillAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, true, true, (CCSPlayerController targetPlayer) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {targetPlayer.PlayerName} has been killed by {adminPlayer.PlayerName}.");
                targetPlayer.CommitSuicide(true, true);
            });
        }

        private void KickAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
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
            int adminLevel = GetAdminLevel(adminPlayer);

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

        private void ShowPlayerListMenu(CCSPlayerController adminPlayer, bool showOnlyAlive, bool showBots, Action<CCSPlayerController> playerAction)
        {
            var playerListMenu = new CenterHtmlMenu($"Choose a player", this);

            var players = Utilities
                .GetPlayers()
                .Where(p => p.IsValid)
                .Where(p => showBots || !p.IsBot)
                .Where(p => !showOnlyAlive || p.PawnIsAlive == true);

            foreach (var player in players)
            {
                playerListMenu.AddMenuOption(player.PlayerName, (controller, option) =>
                {
                    if (GetAdminLevel(adminPlayer) < GetAdminLevel(player))
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
            banTimeMenu.AddMenuOption("1 min", (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, DateTime.Now.AddMinutes(1));
            });
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

                _bannedEntry ??= [];
                _bannedEntry.Add(steamId, newEntry);
                bannedList.Add(steamId, newEntry);
                WriteToFile(bannedList, _bannedFilePath);

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