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

        private const float ReviveMaxRange = 150.0f;  // Source units
        private const float ReviveAimCosThreshold = 0.85f; // ~32° cone

        public static void ResetDeathTimes() => _deathTimes.Clear();

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
                    _reviveProgress.Remove(player.SteamID);
                    continue;
                }

                var target = FindAimedDeadTeammate(player);
                if (target == null)
                {
                    _reviveProgress.Remove(player.SteamID);
                    continue;
                }

                if (_reviveProgress.TryGetValue(player.SteamID, out var progress))
                {
                    // Switched aim to a different dead teammate — restart timer
                    if (progress.TargetSteamId != target.SteamID)
                    {
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
                if (!_deathTimes.TryGetValue(target.SteamID, out DateTime deathTime)
                    || (float)(Utils.GetServerTime() - deathTime).TotalSeconds > _config.ReviveDeathWindowSeconds)
                    continue;

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
    }
}
