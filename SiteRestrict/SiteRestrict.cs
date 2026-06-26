using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using SharedLibrary;

namespace SiteRestrict
{
    public class SiteRestrict : BasePlugin
    {
        public override string ModuleName => "SiteRestrict";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Restricts bomb planting to a single random site when CTs are low";

        public readonly string PluginPrefix = "[SiteRestrict]";

        private static (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)? _allowedZone = null;
        private static (float X, float Y, float Z)? _allowedCenter = null;
        private static (float X, float Y, float Z)? _otherCenter = null;
        private static string _allowedSiteName = "";
        private static bool _isRestricted = false;
        private static bool _isWarmup = false;
        private static Config _config = new();
        private CounterStrikeSharp.API.Modules.Timers.Timer? _messageTimer = null;

        public override void Load(bool hotReload)
        {
            _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
            SharedLibrary.Localizer.Initialize(_config.Language);

            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventBombBeginplant>(OnBombBeginPlant);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
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

        public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);
            if (player is null || !player.IsValid)
                return HookResult.Continue;

            if (@event?.Text.Trim().ToLower() is "!reload")
            {
                string adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
                if (PlayerHelper.GetAdminLevel(player, adminsFilePath) > 2)
                {
                    _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
                    SharedLibrary.Localizer.Initialize(_config.Language);
                    player.PrintToChat($"{PluginPrefix} {Msg.Get("ConfigsReloaded")}");
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            StopMessageTimer();
            _allowedZone = null;
            _allowedCenter = null;
            _otherCenter = null;
            _allowedSiteName = "";
            _isRestricted = false;

            if (_isWarmup)
                return HookResult.Continue;

            AddTimer(0.3f, SetupRestriction);
            return HookResult.Continue;
        }

        private void SetupRestriction()
        {
            if (_isWarmup)
                return;

            int ctCount = Utilities.GetPlayers()
                .Count(p => p.IsValid && p.Team == CsTeam.CounterTerrorist);

            if (ctCount >= _config.MinCTsForSiteRestrict)
                return;

            var sites = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_bomb_target")
                .Where(e => e.IsValid && e.AbsOrigin != null)
                .ToList();

            if (sites.Count < 2)
                return;

            var identified = IdentifySites(sites);
            if (identified == null)
                return;

            var (nameA, zoneA, centerA, nameB, zoneB, centerB) = identified.Value;

            if (Random.Shared.Next(2) == 0)
            {
                _allowedSiteName = nameA;
                _allowedZone = zoneA;
                _allowedCenter = centerA;
                _otherCenter = centerB;
            }
            else
            {
                _allowedSiteName = nameB;
                _allowedZone = zoneB;
                _allowedCenter = centerB;
                _otherCenter = centerA;
            }

            _isRestricted = true;

            _messageTimer = AddTimer(1.0f, () =>
            {
                if (!_isRestricted)
                    return;

                foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid))
                    p.PrintToCenter(Msg.Get("SiteAllowed", _allowedSiteName));
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            StopMessageTimer();
            _isRestricted = false;
            return HookResult.Continue;
        }

        private void StopMessageTimer()
        {
            _messageTimer?.Kill();
            _messageTimer = null;
        }

        private HookResult OnBombBeginPlant(EventBombBeginplant @event, GameEventInfo info)
        {
            if (!_isRestricted)
                return HookResult.Continue;

            var player = @event.Userid;
            if (player == null || !player.IsValid)
                return HookResult.Continue;

            var pawn = player.PlayerPawn.Value;
            if (pawn?.AbsOrigin == null)
                return HookResult.Continue;

            bool isAtAllowedSite;
            if (_allowedZone != null)
            {
                isAtAllowedSite = IsInsideZone(pawn.AbsOrigin, _allowedZone.Value);
            }
            else if (_allowedCenter != null && _otherCenter != null)
            {
                isAtAllowedSite = Distance(pawn.AbsOrigin, _allowedCenter.Value) <= Distance(pawn.AbsOrigin, _otherCenter.Value);
            }
            else
            {
                return HookResult.Continue;
            }

            if (isAtAllowedSite)
                return HookResult.Continue;

            // Wrong site — drop the bomb
            player.PrintToCenter(Msg.Get("WrongSite", _allowedSiteName));
                if (player.IsValid && player.PawnIsAlive)
                    player.DropActiveWeapon();

            return HookResult.Continue;
        }

        private static (
            string nameA,
            (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)? zoneA,
            (float X, float Y, float Z) centerA,
            string nameB,
            (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)? zoneB,
            (float X, float Y, float Z) centerB
        )? IdentifySites(List<CBaseEntity> sites)
        {
            CBaseEntity? siteA = null, siteB = null;

            foreach (var site in sites)
            {
                string entityName = (site.Entity?.Name ?? "").ToLower();
                if (entityName.Contains("_a") || entityName.EndsWith("a"))
                    siteA = site;
                else if (entityName.Contains("_b") || entityName.EndsWith("b"))
                    siteB = site;
            }

            if (siteA == null || siteB == null || siteA.AbsOrigin == null || siteB.AbsOrigin == null)
            {
                var sorted = sites.OrderBy(s => s.AbsOrigin!.X).ToList();
                if (sorted.Count < 2 || sorted[0].AbsOrigin == null || sorted[1].AbsOrigin == null)
                    return null;
                siteA = sorted[0];
                siteB = sorted[1];
            }

            return (
                "A", GetZoneAabb(siteA), GetSiteCenter(siteA),
                "B", GetZoneAabb(siteB), GetSiteCenter(siteB)
            );
        }

        private static (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ)? GetZoneAabb(CBaseEntity site)
        {
            var origin = site.AbsOrigin;
            if (origin == null) return null;
            var mins = site.Collision?.Mins;
            var maxs = site.Collision?.Maxs;
            if (mins == null || maxs == null) return null;

            if (MathF.Abs(maxs.X - mins.X) < 1f && MathF.Abs(maxs.Y - mins.Y) < 1f) return null;
            return (
                origin.X + mins.X, origin.Y + mins.Y, origin.Z + mins.Z,
                origin.X + maxs.X, origin.Y + maxs.Y, origin.Z + maxs.Z
            );
        }

        private static (float X, float Y, float Z) GetSiteCenter(CBaseEntity site)
        {
            var origin = site.AbsOrigin!;
            var mins = site.Collision?.Mins;
            var maxs = site.Collision?.Maxs;
            if (mins != null && maxs != null)
                return (
                    origin.X + (mins.X + maxs.X) * 0.5f,
                    origin.Y + (mins.Y + maxs.Y) * 0.5f,
                    origin.Z + (mins.Z + maxs.Z) * 0.5f
                );
            return (origin.X, origin.Y, origin.Z);
        }

        private static bool IsInsideZone(Vector pos, (float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ) zone)
        {
            return pos.X >= zone.MinX && pos.X <= zone.MaxX &&
                   pos.Y >= zone.MinY && pos.Y <= zone.MaxY &&
                   pos.Z >= zone.MinZ && pos.Z <= zone.MaxZ;
        }

        private static float Distance(Vector a, (float X, float Y, float Z) b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
