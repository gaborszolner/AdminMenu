using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using SharedLibrary;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private static readonly Dictionary<ulong, (ulong TargetSteamId, DateTime StartTime)> _reviveProgress = new();

        public static readonly Dictionary<ulong, DateTime> _deathTimes = new();
        public static readonly Dictionary<ulong, (float X, float Y, float Z)> _deathPositions = new();
        private static readonly Dictionary<ulong, DateTime> _reviveWindowEndTime = new();
        private static readonly Dictionary<ulong, float> _reviveWindowFrozen = new();
        private static readonly Dictionary<ulong, HashSet<ulong>> _activeReviversForTarget = new();

        private const float ReviveMaxRange = 150.0f;  // Source units
        private const float ReviveAimCosThreshold = 0.85f; // ~32° cone

        public static void ResetDeathTimes()
        {
            _deathTimes.Clear();
            _deathPositions.Clear();
            _reviveWindowEndTime.Clear();
            _reviveWindowFrozen.Clear();
            _activeReviversForTarget.Clear();
        }

        private void OnTickRevive()
        {
            if(_config.CanReviveTeammate == false)
                return;

            foreach (var player in Utilities.GetPlayers()
                .Where(p => p.IsValid
                         && p.PawnIsAlive
                         && p.Team != CsTeam.Spectator
                         && p.Team != CsTeam.None))
            {
                bool isHoldingUse = (player.Buttons & PlayerButtons.Use) != 0;

                if (!isHoldingUse)
                {
                    if (_reviveProgress.TryGetValue(player.SteamID, out var oldProg))
                        RemoveReviver(player.SteamID, oldProg.TargetSteamId);
                    _reviveProgress.Remove(player.SteamID);
                    continue;
                }

                var target = FindAimedDeadTeammate(player);
                if (target == null)
                {
                    if (_reviveProgress.TryGetValue(player.SteamID, out var oldProg))
                        RemoveReviver(player.SteamID, oldProg.TargetSteamId);
                    _reviveProgress.Remove(player.SteamID);
                    continue;
                }

                if (_reviveProgress.TryGetValue(player.SteamID, out var progress))
                {
                    // Switched aim to a different dead teammate — restart timer
                    if (progress.TargetSteamId != target.SteamID)
                    {
                        RemoveReviver(player.SteamID, progress.TargetSteamId);
                        AddReviver(player.SteamID, target.SteamID);
                        _reviveProgress[player.SteamID] = (target.SteamID, Utils.GetServerTime());
                        player.PrintToCenter(Msg.Get("ReviveProgressStart", target.PlayerName, _config.ReviveHoldDurationSeconds));
                    }
                    else
                    {
                        float elapsed = (float)(Utils.GetServerTime() - progress.StartTime).TotalSeconds;
                        float remaining = _config.ReviveHoldDurationSeconds - elapsed;

                        if (elapsed >= _config.ReviveHoldDurationSeconds)
                        {
                            _reviveProgress.Remove(player.SteamID);
                            ReviveDeadTeammate(player, target);
                        }
                        else
                        {
                            int filled = (int)(elapsed / _config.ReviveHoldDurationSeconds * 10);
                            string bar = new string('|', filled) + new string('.', 10 - filled);
                            player.PrintToCenter(Msg.Get("ReviveProgressBar", target.PlayerName, bar, $"{remaining:F1}"));
                        }
                    }
                }
                else
                {
                    AddReviver(player.SteamID, target.SteamID);
                    _reviveProgress[player.SteamID] = (target.SteamID, Utils.GetServerTime());
                    player.PrintToCenter(Msg.Get("ReviveProgressStart", target.PlayerName, _config.ReviveHoldDurationSeconds));
                }
            }
        }

        private static CCSPlayerController? FindAimedDeadTeammate(CCSPlayerController player)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn?.AbsOrigin == null || pawn.EyeAngles == null)
                return null;

            double pitchRad = pawn.EyeAngles.X * Math.PI / 180.0;
            double yawRad   = pawn.EyeAngles.Y * Math.PI / 180.0;

            float fwdX = (float)(Math.Cos(pitchRad) * Math.Cos(yawRad));
            float fwdY = (float)(Math.Cos(pitchRad) * Math.Sin(yawRad));
            float fwdZ = (float)(-Math.Sin(pitchRad));

            // Approximate eye position (pawn origin + ~64 units height)
            float eyeX = pawn.AbsOrigin.X;
            float eyeY = pawn.AbsOrigin.Y;
            float eyeZ = pawn.AbsOrigin.Z + 64.0f;

            CCSPlayerController? bestTarget = null;
            float bestDot = ReviveAimCosThreshold;

            foreach (var target in Utilities.GetPlayers()
                .Where(p => p.IsValid
                         && !p.PawnIsAlive
                         && p.Team == player.Team
                         && p.SteamID != player.SteamID
                         && p.Team != CsTeam.Spectator
                         && p.Team != CsTeam.None))
            {
                if (!_reviveWindowFrozen.ContainsKey(target.SteamID))
                {
                    if (!_reviveWindowEndTime.TryGetValue(target.SteamID, out var endTime)
                        || Utils.GetServerTime() > endTime)
                        continue;
                }

                var targetPawn = target.PlayerPawn.Value;
                if (targetPawn?.AbsOrigin == null)
                    continue;

                float dx = targetPawn.AbsOrigin.X - eyeX;
                float dy = targetPawn.AbsOrigin.Y - eyeY;
                float dz = targetPawn.AbsOrigin.Z - eyeZ;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                if (dist > ReviveMaxRange || dist < 0.01f)
                    continue;

                float dot = (dx * fwdX + dy * fwdY + dz * fwdZ) / dist;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestTarget = target;
                }
            }

            return bestTarget;
        }

        private void ReviveDeadTeammate(CCSPlayerController reviverPlayer, CCSPlayerController targetPlayer)
        {
            if (!targetPlayer.IsValid || targetPlayer.PawnIsAlive)
                return;

            _reviveWindowEndTime.Remove(targetPlayer.SteamID);
            _reviveWindowFrozen.Remove(targetPlayer.SteamID);
            _activeReviversForTarget.Remove(targetPlayer.SteamID);
            targetPlayer.Respawn();

            // Set HP after a short delay so Respawn() has time to fully execute
            AddTimer(0.1f, () =>
            {
                try
                {
                    if (!targetPlayer.IsValid || !targetPlayer.PawnIsAlive)
                        return;

                    var targetPawn = targetPlayer.PlayerPawn.Value;
                    if (targetPawn != null)
                    {
                        targetPawn.Health = _config.ReviveHP;
                        Utilities.SetStateChanged(targetPawn, "CBaseEntity", "m_iHealth");

                        if (_deathPositions.TryGetValue(targetPlayer.SteamID, out var deathPos))
                        {
                            targetPawn.Teleport(new Vector(deathPos.X, deathPos.Y, deathPos.Z));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Error setting HP after revive: {ex.Message}");
                }
            });

            reviverPlayer.PrintToCenter(Msg.Get("ReviveSuccessful"));
            Server.PrintToChatAll(Msg.Get("PlayerRevived", reviverPlayer.PlayerName, targetPlayer.PlayerName, _config.ReviveHP));
        }

        private void AddReviver(ulong reviverSteamId, ulong targetSteamId)
        {
            if (!_activeReviversForTarget.TryGetValue(targetSteamId, out var set))
            {
                set = new HashSet<ulong>();
                _activeReviversForTarget[targetSteamId] = set;
            }

            if (set.Count == 0 && _reviveWindowEndTime.TryGetValue(targetSteamId, out var endTime))
            {
                float remaining = (float)(endTime - Utils.GetServerTime()).TotalSeconds;
                _reviveWindowFrozen[targetSteamId] = Math.Max(0f, remaining);
            }

            set.Add(reviverSteamId);
        }

        private void RemoveReviver(ulong reviverSteamId, ulong targetSteamId)
        {
            if (!_activeReviversForTarget.TryGetValue(targetSteamId, out var set))
                return;

            set.Remove(reviverSteamId);

            if (set.Count > 0)
                return;

            _activeReviversForTarget.Remove(targetSteamId);

            if (!_reviveWindowFrozen.TryGetValue(targetSteamId, out float frozenRemaining))
                return;

            _reviveWindowFrozen.Remove(targetSteamId);

            if (frozenRemaining <= 0)
                return;

            var newEndTime = Utils.GetServerTime() + TimeSpan.FromSeconds(frozenRemaining);
            _reviveWindowEndTime[targetSteamId] = newEndTime;

            AddTimer(frozenRemaining, () => NotifyReviveExpiredIfNeeded(targetSteamId, newEndTime));
        }

        private void NotifyReviveExpiredIfNeeded(ulong steamId, DateTime expectedEndTime)
        {
            if (!_reviveWindowEndTime.TryGetValue(steamId, out var currentEndTime) || currentEndTime != expectedEndTime)
                return;

            if (_reviveWindowFrozen.ContainsKey(steamId))
                return;

            var target = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId && !p.PawnIsAlive);
            if (target == null)
                return;

            target.PrintToCenter(Msg.Get("ReviveWindowExpired"));

            _reviveWindowEndTime.Remove(steamId);
            _deathTimes.Remove(steamId);
            _deathPositions.Remove(steamId);
        }
    }
}
