using AdminMenu.Entries;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;
using SharedLibrary;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        public override string ModuleName => "AdminMenu";
        public override string ModuleVersion => "2.1";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "AdminMenu";

        public readonly string PluginPrefix = $"[Admin]";

        private static string _adminsFilePath = string.Empty;
        private static string _bannedFilePath = string.Empty;
        private static string _weaponRestrictFilePath = string.Empty;
        private string _playerStatDirectory => Path.Combine(ModuleDirectory, "..", "GameStatistic");
        private string _playerStatFileFullPath => Path.Combine(_playerStatDirectory, StatisticHelper.PlayerStatFileName);
        private static string _mapListFilePath = string.Empty;
        private static bool _isWarmup = false;
        private static Dictionary<string, WeaponRestrictEntry>? _weaponRestrictEntry;
        private static Dictionary<string, AdminEntry>? _adminEntry;
        private static Dictionary<string, BannedEntry>? _bannedEntry;
        private static Config _config = new();
        private static Dictionary<string, PendingRenameEntry>? _pendingRename = [];
        private static readonly object _pendingRenameLock = new();
        private static readonly object _dictionaryLock = new();

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnect);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventPlayerSpawned>(OnPlayerSpawned);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventItemPickup>(OnItemPickup);
            RegisterEventHandler<EventItemEquip>(OnItemEquip);
            AddCommandListener("!admin", OpenAdminMenu);

            _adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
            _bannedFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "banned.json");
            _weaponRestrictFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "weaponRestrict.json");
            _mapListFilePath = Path.Combine(ModuleDirectory, "..", "RockTheVote", "maplist.txt");
            _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
            SharedLibrary.Localizer.Initialize(_config.Language);

            _adminEntry = Utils.LoadDataFromFile<AdminEntry>(_adminsFilePath);
            _bannedEntry = Utils.LoadDataFromFile<BannedEntry>(_bannedFilePath);
            _weaponRestrictEntry = Utils.LoadDataFromFile<WeaponRestrictEntry>(_weaponRestrictFilePath);
        }

        private HookResult OnPlayerSpawned(EventPlayerSpawned @event, GameEventInfo info)
        {
            var targetPlayer = @event.Userid;

            if (targetPlayer is not null && !targetPlayer.IsBot && targetPlayer.IsValid)
            {
                if (!_config.AllowSameName &&
                    PlayerHelper.GetAllPlayers().Any(p =>
                        p.PlayerName == targetPlayer.PlayerName &&
                        p.SteamID != targetPlayer.SteamID))
                {
                    AddCounterToPlayerName(targetPlayer);
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            var targetPlayer = @event.Userid;

            if (_config.MuteAfterDeathInSecounds > 0 && !_isWarmup && targetPlayer is not null && targetPlayer.IsValid && !targetPlayer.IsBot)
            {
                var originalVoiceFlag = targetPlayer.VoiceFlags;
                targetPlayer.VoiceFlags |= VoiceFlags.Muted;
                AddTimer(_config.MuteAfterDeathInSecounds, () =>
                {
                    try
                    {
                        if (targetPlayer.IsValid)
                        {
                            targetPlayer.VoiceFlags = originalVoiceFlag;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError($"Error unmuting player after death: {ex.Message}");
                    }
                });
            }

            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;

            var storedStats =
                StatisticHelper.LoadMonthsStats(_playerStatDirectory, _config.DateRangeForStatisticsInMonth)
                ?? [];

            double ctSumScore = Math.Round(StatisticHelper.GetSumScores(storedStats, PlayerHelper.GetAllCounterTerrorist()), 1);
            double tSumScore = Math.Round(StatisticHelper.GetSumScores(storedStats, PlayerHelper.GetAllTerrorist()), 1);

            if (ctSumScore == 0 || tSumScore == 0)
            {
                return HookResult.Continue;
            }

            var currentDifferentPercentage = StatisticHelper.GetPercentageDifference(ctSumScore, tSumScore);

            if (PlayerHelper.GetAllNonSpecPlayers().Count() >= _config.MinimumPlayerCountToStatistic &&
                _config.AutoTeamShuffleOnRoundStart &&
                _config.AutoTeamShuffleMinDifferentPercentage <= currentDifferentPercentage)
            {
                Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Yellow}{Msg.Get("AutoShuffleCurrentDiff")} {ChatColors.Red}{currentDifferentPercentage:F2}%{ChatColors.Yellow}, {Msg.Get("AutoShuffleActivated")}");
                TeamShuffleAction(null, null);
                StatisticHelper.PrintTeamStat(StatisticHelper.LoadMonthsStats(_playerStatDirectory, _config.DateRangeForStatisticsInMonth));
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
            lock (_dictionaryLock)
            {
                _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
            }
            SharedLibrary.Localizer.Initialize(_config.Language);
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

        private HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var player = @event.Userid;

            if (player is null || player.IsBot)
            {
                return HookResult.Continue;
            }

            if (IsBanned(player, out string oldName))
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerBannedFromServer", player.PlayerName, oldName)}");
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED);
            }
            else
            {
                if (!_isWarmup)
                {
                    string welcomeMessage = $"{PluginPrefix} {string.Format(_config.WelcomeMessage, player.PlayerName)}";
                    var adminLevel = GetAdminLevel(player);
                    if (adminLevel > 1)
                    {
                        welcomeMessage += $" {Msg.Get("JoinSuffixAdmin")}";
                    }
                    else
                    {
                        welcomeMessage += $" {Msg.Get("JoinSuffixNotAdmin")}";
                    }

                    Logger?.LogInformation($"Player connect: {player.PlayerName} - SteamID2: {player.AuthorizedSteamID?.SteamId2} - AdminLevel: {adminLevel}");
                    Server.PrintToChatAll(welcomeMessage);
                    player.PrintToChat($"{PluginPrefix} {Msg.Get("TypeHelpHint")}");
                }
            }

            return HookResult.Continue;
        }

        public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);

            if (player is null || player.AuthorizedSteamID is null)
            {
                return HookResult.Continue;
            }

            if (_pendingRename is not null &&
                _pendingRename.Count != 0 &&
                _pendingRename.ContainsKey(player.AuthorizedSteamID.SteamId2))
            {
                RenamePlayer(player, @event.Text.Trim(), _pendingRename[player.AuthorizedSteamID.SteamId2]);
                return HookResult.Handled;
            }

            if (@event?.Text.Trim().ToLower() is "!admin")
            {
                ShowMainMenu(player);
            }
            else if (@event?.Text.Trim().ToLower() is "!admins")
            {
                ShowAdmins();
            }
            else if (@event?.Text.Trim().ToLower() is "!mysteamid")
            {
                player?.PrintToChat(Msg.Get("SteamIdDisplay", player?.AuthorizedSteamID?.SteamId2 ?? string.Empty, player?.SteamID.ToString() ?? string.Empty));
            }
            else if (@event?.Text.Trim().ToLower() is "!thetime")
            {
                Server.PrintToChatAll($"{Utils.GetServerTime()}");
            }
            else if (@event?.Text.Trim().ToLower() is "!weapons")
            {
                string mapName = Server.MapName.Trim() ?? string.Empty;
                string weaponList = GetRestrictedWeapons(mapName);
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("RestrictedWeaponsOnMap", mapName, weaponList.Replace("weapon_", ""))}");
            }
            else if (@event?.Text.Trim().ToLower() is "!status")
            {
                LogStatuses(player);
            }
            else if (@event?.Text.Trim().ToLower() is "!currentmap")
            {
                player?.PrintToChat(Server.MapName.Trim() ?? string.Empty);
            }
            else if (@event?.Text.Trim().ToLower() is "!reload")
            {
                ReloadConfigs(player);
            }
            else if (@event?.Text.Trim().ToLower() is "!help")
            {
                int adminLevel = GetAdminLevel(player);

                if (adminLevel > 2)
                {
                    player?.PrintToChat($"{PluginPrefix} {Msg.Get("HelpCommandsAdmin")}");
                }
                else
                {
                    player?.PrintToChat($"{PluginPrefix} {Msg.Get("HelpCommandsPlayer")}");
                }
            }
            return HookResult.Continue;
        }

        private static void AddCounterToPlayerName(CCSPlayerController player)
        {
            int counter = 1;
            string newPlayerName = player.PlayerName;

            while (PlayerHelper.GetAllPlayers().Any(p => p.PlayerName == newPlayerName + '(' + counter + ')'))
            {
                counter++;
            }
            player.PlayerName = newPlayerName + '(' + counter + ')';

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
                var entry = _adminEntry[steamId];
                if (entry.Identity != steamId)
                {
                    entry.Identity = steamId;
                    Utils.WriteToFile(_adminEntry, _adminsFilePath);
                }
                return entry.Level;
            }
        }

        private bool IsBanned(CCSPlayerController? player, out string oldName)
        {
            oldName = string.Empty;
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
                    if (possibleBanned.Expiration < Utils.GetServerTime())
                    {
                        _bannedEntry.Remove(steamId);
                        Utils.WriteToFile(_bannedEntry, _bannedFilePath);
                        return false;
                    }
                    else
                    {
                        oldName = possibleBanned.Name;
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

        private void ReloadConfigs(CCSPlayerController player)
        {
            int adminLevel = GetAdminLevel(player);
            if (adminLevel > 2)
            {
                lock (_dictionaryLock)
                {
                    _weaponRestrictEntry = Utils.LoadDataFromFile<WeaponRestrictEntry>(_weaponRestrictFilePath);
                    _adminEntry = Utils.LoadDataFromFile<AdminEntry>(_adminsFilePath);
                    _bannedEntry = Utils.LoadDataFromFile<BannedEntry>(_bannedFilePath);
                    _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
                }
                player.PrintToChat($"{PluginPrefix} {Msg.Get("ConfigsReloaded")}");
            }
        }

        private void LogStatuses(CCSPlayerController player)
        {
            int adminLevel = GetAdminLevel(player);

            if (adminLevel > 2)
            {
                foreach (var statusPlayer in PlayerHelper.GetAllPlayers())
                {
                    string status = $"Player: {statusPlayer.PlayerName} - {statusPlayer.AuthorizedSteamID?.SteamId2}";
                    Server.PrintToConsole(status);
                    Logger?.LogInformation(status);
                }
            }
        }

        private static void ShowAdmins()
        {
            string adminList = Msg.Get("AdminsOnlinePrefix");
            int adminCount = 0;
            foreach (var adminPlayer in PlayerHelper.GetAllPlayers().Where(p => GetAdminLevel(p) > 1))
            {
                adminCount++;
                adminList += $"{adminPlayer.PlayerName}, ";
            }
            adminList = adminList.TrimEnd(' ', ',');

            if (adminCount > 0)
            {
                Server.PrintToChatAll($"{adminCount} {adminList}");
            }
        }

        private void RenamePlayer(CCSPlayerController adminPlayer, string newName, PendingRenameEntry pendingRename)
        {
            if (_pendingRename is null || pendingRename is null || string.IsNullOrWhiteSpace(newName) || newName.StartsWith('!'))
            {
                return;
            }

            try
            {
                if (pendingRename.Expiration < Utils.GetServerTime())
                {
                    lock (_pendingRenameLock)
                    {
                        _pendingRename.Remove(pendingRename.AdminSteamId2);
                    }
                    adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("RenameExpired")}");
                    return;
                }

                try
                {
                    var target = PlayerHelper.GetAllPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId2 == pendingRename.TargetSteamId2);
                    if (target != null && target.IsValid)
                    {
                        target.PlayerName = newName;
                        Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerRenamed", pendingRename.OldName, newName, pendingRename.AdminName)}");
                        Logger?.LogInformation($"Player renamed: {pendingRename.OldName} to {newName} by {pendingRename.AdminName} (SteamID2: {pendingRename.TargetSteamId2})");
                    }
                    else
                    {
                        adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("TargetDisconnected")}");
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Error applying rename: {ex.Message}");
                    adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("RenameApplyError", ex.Message)}");
                }

                lock (_pendingRenameLock)
                {
                    _pendingRename.Remove(pendingRename.AdminSteamId2);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error processing pending rename: {ex.Message}");
            }
        }

        private void ShowMainMenu(CCSPlayerController adminPlayer)
        {
            int adminLevel = GetAdminLevel(adminPlayer);

            if (adminLevel == 0)
            {
                adminPlayer.PrintToChat(Msg.Get("NotAdminError"));
                return;
            }

            var mainMenu = new CenterHtmlMenu(Msg.Get("MainMenuTitle"), this);
            if (adminLevel > 1)
            {
                mainMenu.AddMenuOption(Msg.Get("MenuBan"), BanAction);
                mainMenu.AddMenuOption(Msg.Get("MenuKick"), KickAction);
                mainMenu.AddMenuOption(Msg.Get("MenuKill"), KillAction);
                mainMenu.AddMenuOption(Msg.Get("MenuSlap"), SlapAction);
                mainMenu.AddMenuOption(Msg.Get("MenuDropWeapon"), DropWeaponAction);
                mainMenu.AddMenuOption(Msg.Get("MenuSetTeam"), SetTeamAction);
                mainMenu.AddMenuOption(Msg.Get("MenuRename"), RenameAction);
                mainMenu.AddMenuOption(Msg.Get("MenuMute"), MuteAction);
                mainMenu.AddMenuOption(Msg.Get("MenuUnMute"), UnMuteAction);
            }
            if (adminLevel > 2)
            {
                mainMenu.AddMenuOption(Msg.Get("MenuWeaponRestrict"), WeaponRestrictAction);
                mainMenu.AddMenuOption(Msg.Get("MenuRespawn"), RespawnAction);
                mainMenu.AddMenuOption(Msg.Get("MenuSetAdmin"), SetAdminAction);
                mainMenu.AddMenuOption(Msg.Get("MenuSetHP"), SetHPAction);
                if (File.Exists(_mapListFilePath))
                {
                    mainMenu.AddMenuOption(Msg.Get("MenuChangeMap"), ChangeMapAction);
                }
                if (File.Exists(_playerStatFileFullPath))
                {
                    mainMenu.AddMenuOption(Msg.Get("MenuTeamShuffle"), TeamShuffleAction);
                }
            }
            if (adminLevel > 0)
            {
                mainMenu.AddMenuOption(Msg.Get("MenuBotHandle"), BotHandleAction);
            }

            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, mainMenu);
        }

        private void ShowPlayerListMenu(CCSPlayerController adminPlayer, bool showOnlyAlive, bool showBots, Action<CCSPlayerController> playerAction)
        {
            var playerListMenu = new CenterHtmlMenu(Msg.Get("ChoosePlayer"), this);
            int adminPlayerLevel = GetAdminLevel(adminPlayer);

            var players = Utilities
                .GetPlayers()
                .Where(p => p.IsValid)
                .Where(p => showBots || !p.IsBot)
                .Where(p => !showOnlyAlive || p.PawnIsAlive == true)
                .Where(p => adminPlayerLevel >= GetAdminLevel(p));

            foreach (var player in players)
            {
                playerListMenu.AddMenuOption(player.PlayerName, (controller, option) =>
                {
                    if (adminPlayerLevel < GetAdminLevel(player))
                    {
                        adminPlayer.PrintToCenter(Msg.Get("ActionDeniedHigherAdmin", player.PlayerName));
                    }
                    else
                    {
                        playerAction(player);
                    }
                });
            }

            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, playerListMenu);
        }

    }
}