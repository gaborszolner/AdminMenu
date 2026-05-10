using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SharedLibrary
{
    public static class PlayerHelper
    {
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
