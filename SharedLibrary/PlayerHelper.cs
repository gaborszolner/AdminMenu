using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace SharedLibrary
{
    public static class PlayerHelper
    {
        public static int GetAdminLevel(CCSPlayerController? player, string adminsFilePath)
        {
            if (player is null || player.AuthorizedSteamID is null || !File.Exists(adminsFilePath))
                return 0;

            string steamId = Utils.NormalizeSteamId2(player.AuthorizedSteamID.SteamId2);

            try
            {
                string json = File.ReadAllText(adminsFilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(steamId, out JsonElement entry) &&
                    entry.TryGetProperty("level", out JsonElement level))
                {
                    return level.GetInt32();
                }
            }
            catch { }

            return 0;
        }


        public static IEnumerable<CCSPlayerController> GetAllPlayers()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot);
        }

        public static IEnumerable<CCSPlayerController> GetAllPlayersAlive()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.PawnIsAlive && p.Team != CsTeam.Spectator && p.Team != CsTeam.None);
        }

        public static IEnumerable<CCSPlayerController> GetAllNonSpecPlayers()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.Team != CsTeam.Spectator && p.Team != CsTeam.None);
        }

        public static IEnumerable<CCSPlayerController> GetAllTerrorist()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.Team == CsTeam.Terrorist);
        }
        public static IEnumerable<CCSPlayerController> GetAllTerroristAlive()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.Team == CsTeam.Terrorist && p.PawnIsAlive);
        }

        public static IEnumerable<CCSPlayerController> GetAllCounterTerrorist()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.Team == CsTeam.CounterTerrorist);
        }

        public static IEnumerable<CCSPlayerController> GetAllCounterTerroristAlive()
        {
            return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.Team == CsTeam.CounterTerrorist && p.PawnIsAlive);
        }
    }
}
