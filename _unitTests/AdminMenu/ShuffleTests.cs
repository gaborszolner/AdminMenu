using SharedLibrary;
using SharedLibrary.Entries;
using AdminMenuPlugin = global::AdminMenu.AdminMenu;

namespace _unitTests.AdminMenu
{
    [TestClass]
    public class ShuffleTests
    {
        // Creates a sorted (desc by Score) player list from (steamId, kills, deaths) tuples
        private static List<AdminMenuPlugin.Shuffle.PlayerShuffleData> CreatePlayers(
            params (string id, int kill, int dead)[] playerData)
            => playerData
                .Select(p => new AdminMenuPlugin.Shuffle.PlayerShuffleData(
                    p.id,
                    new PlayerStatEntry(p.id, p.id, kill: p.kill, dead: p.dead)))
                .OrderByDescending(p => p.Stats.Score)
                .ToList();

        // ── GetShuffleResult ─────────────────────────────────────────────────

        [TestMethod]
        public void GetShuffleResult_EmptyPlayers_BothTeamsEmpty()
        {
            var players = new List<AdminMenuPlugin.Shuffle.PlayerShuffleData>();

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, 1);

            Assert.AreEqual(0, result.TeamTSteamId2List.Count);
            Assert.AreEqual(0, result.TeamCTSteamId2List.Count);
        }

        [TestMethod]
        public void GetShuffleResult_SinglePlayer_GoesToTTeam()
        {
            var players = CreatePlayers(("STEAM_0:0:1", 100, 10));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, 1);

            Assert.AreEqual(1, result.TeamTSteamId2List.Count);
            Assert.AreEqual(0, result.TeamCTSteamId2List.Count);
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_TwoPlayers_EachTeamGetsOne(int method)
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 50, 10));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, method);

            Assert.AreEqual(1, result.TeamTSteamId2List.Count, $"Method {method}: T team should have 1 player");
            Assert.AreEqual(1, result.TeamCTSteamId2List.Count, $"Method {method}: CT team should have 1 player");
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_AllPlayersAssigned(int method)
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 150, 10),
                ("STEAM_0:0:2", 120, 12),
                ("STEAM_0:0:3", 100, 8),
                ("STEAM_0:0:4", 90, 15),
                ("STEAM_0:0:5", 80, 20),
                ("STEAM_0:0:6", 70, 18));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, method);

            int totalAssigned = result.TeamTSteamId2List.Count + result.TeamCTSteamId2List.Count;
            Assert.AreEqual(players.Count, totalAssigned, $"Method {method}: all players must be assigned");
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_OddPlayerCount_TTeamGetsExtraPlayer(int method)
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 90, 10),
                ("STEAM_0:0:3", 80, 10),
                ("STEAM_0:0:4", 70, 10),
                ("STEAM_0:0:5", 60, 10));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, method);

            Assert.AreEqual(3, result.TeamTSteamId2List.Count, $"Method {method}: T team should have 3");
            Assert.AreEqual(2, result.TeamCTSteamId2List.Count, $"Method {method}: CT team should have 2");
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_EvenPlayerCount_EqualTeamSizes(int method)
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 90, 10),
                ("STEAM_0:0:3", 80, 10),
                ("STEAM_0:0:4", 70, 10));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, method);

            Assert.AreEqual(2, result.TeamTSteamId2List.Count, $"Method {method}: T team should have 2");
            Assert.AreEqual(2, result.TeamCTSteamId2List.Count, $"Method {method}: CT team should have 2");
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_NoDuplicatePlayerIds(int method)
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 200, 5),
                ("STEAM_0:0:2", 150, 8),
                ("STEAM_0:0:3", 100, 12),
                ("STEAM_0:0:4", 80, 15),
                ("STEAM_0:0:5", 50, 20),
                ("STEAM_0:0:6", 30, 22),
                ("STEAM_0:0:7", 20, 25));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, method);

            var all = result.TeamTSteamId2List.Concat(result.TeamCTSteamId2List).ToList();
            Assert.AreEqual(all.Count, all.Distinct().Count(), $"Method {method}: no player should appear twice");
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_AllExpectedPlayerIdsPresent(int method)
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 200, 5),
                ("STEAM_0:0:2", 150, 8),
                ("STEAM_0:0:3", 100, 12),
                ("STEAM_0:0:4", 80, 15),
                ("STEAM_0:0:5", 50, 20),
                ("STEAM_0:0:6", 30, 22));

            var expected = players.Select(p => p.SteamId2).ToHashSet();

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, method);
            var assigned = result.TeamTSteamId2List.Concat(result.TeamCTSteamId2List).ToHashSet();

            Assert.IsTrue(assigned.SetEquals(expected), $"Method {method}: every player ID must appear in one team");
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_MethodNumber_IsStoredInResult(int method)
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 50, 10));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, method);

            Assert.AreEqual(method, result.MethodNumber);
        }

        [TestMethod]
        public void GetShuffleResult_InvalidMethodNumber_DifferenceIsMaxValue()
        {
            var players = CreatePlayers(("STEAM_0:0:1", 100, 10));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, 99);

            Assert.AreEqual(double.MaxValue, result.Difference);
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_DifferenceIsNonNegative(int method)
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 150, 5),
                ("STEAM_0:0:2", 100, 10),
                ("STEAM_0:0:3", 80, 20),
                ("STEAM_0:0:4", 40, 30));

            var result = AdminMenuPlugin.Shuffle.GetShuffleResult(players, method);

            Assert.IsTrue(result.Difference >= 0, $"Method {method}: difference must be >= 0");
        }

        // ── ShuffleMethod1 ───────────────────────────────────────────────────

        [TestMethod]
        public void ShuffleMethod1_IdenticalScorePlayers_DifferenceIsZero()
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 100, 10),
                ("STEAM_0:0:3", 100, 10),
                ("STEAM_0:0:4", 100, 10));
            var teamT = new List<string>();
            var teamCT = new List<string>();

            double diff = AdminMenuPlugin.Shuffle.ShuffleMethod1(players, 2, 2, teamT, teamCT);

            Assert.AreEqual(0.0, diff, 0.001);
        }

        [TestMethod]
        public void ShuffleMethod1_FillsTeamsToCapacity()
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 80, 10),
                ("STEAM_0:0:3", 60, 10),
                ("STEAM_0:0:4", 40, 10));
            var teamT = new List<string>();
            var teamCT = new List<string>();

            AdminMenuPlugin.Shuffle.ShuffleMethod1(players, 2, 2, teamT, teamCT);

            Assert.AreEqual(2, teamT.Count);
            Assert.AreEqual(2, teamCT.Count);
        }

        // ── ShuffleMethod2 ───────────────────────────────────────────────────

        [TestMethod]
        public void ShuffleMethod2_IdenticalScorePlayers_DifferenceIsZero()
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 100, 10),
                ("STEAM_0:0:3", 100, 10),
                ("STEAM_0:0:4", 100, 10));
            var teamT = new List<string>();
            var teamCT = new List<string>();

            double diff = AdminMenuPlugin.Shuffle.ShuffleMethod2(players, 2, 2, teamT, teamCT);

            Assert.AreEqual(0.0, diff, 0.001);
        }

        [TestMethod]
        public void ShuffleMethod2_FillsTeamsToCapacity()
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 80, 10),
                ("STEAM_0:0:3", 60, 10),
                ("STEAM_0:0:4", 40, 10),
                ("STEAM_0:0:5", 20, 10));
            var teamT = new List<string>();
            var teamCT = new List<string>();

            AdminMenuPlugin.Shuffle.ShuffleMethod2(players, 3, 2, teamT, teamCT);

            Assert.AreEqual(3, teamT.Count);
            Assert.AreEqual(2, teamCT.Count);
        }

        // ── ShuffleMethod3 ───────────────────────────────────────────────────

        [TestMethod]
        public void ShuffleMethod3_IdenticalScorePlayers_DifferenceIsZero()
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 100, 10),
                ("STEAM_0:0:3", 100, 10),
                ("STEAM_0:0:4", 100, 10));
            var teamT = new List<string>();
            var teamCT = new List<string>();

            double diff = AdminMenuPlugin.Shuffle.ShuffleMethod3(players, 2, 2, teamT, teamCT);

            Assert.AreEqual(0.0, diff, 0.001);
        }

        // ── ShuffleMethod4 ───────────────────────────────────────────────────

        [TestMethod]
        public void ShuffleMethod4_IdenticalScorePlayers_DifferenceIsZero()
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 100, 10),
                ("STEAM_0:0:3", 100, 10),
                ("STEAM_0:0:4", 100, 10));
            var teamT = new List<string>();
            var teamCT = new List<string>();

            double diff = AdminMenuPlugin.Shuffle.ShuffleMethod4(players, 2, 2, teamT, teamCT);

            Assert.AreEqual(0.0, diff, 0.001);
        }

        [TestMethod]
        public void ShuffleMethod4_FillsTeamsToCapacity()
        {
            var players = CreatePlayers(
                ("STEAM_0:0:1", 100, 10),
                ("STEAM_0:0:2", 80, 10),
                ("STEAM_0:0:3", 60, 10),
                ("STEAM_0:0:4", 40, 10));
            var teamT = new List<string>();
            var teamCT = new List<string>();

            AdminMenuPlugin.Shuffle.ShuffleMethod4(players, 2, 2, teamT, teamCT);

            Assert.AreEqual(2, teamT.Count);
            Assert.AreEqual(2, teamCT.Count);
        }

        // ── GetPercentageDifference integration ──────────────────────────────

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void GetShuffleResult_BestMethodHasLowestOrEqualDifference(int winnerMethod)
        {
            // Arrange a player set where all methods will run
            var players = CreatePlayers(
                ("STEAM_0:0:1", 300, 5),
                ("STEAM_0:0:2", 200, 10),
                ("STEAM_0:0:3", 100, 20),
                ("STEAM_0:0:4", 90, 25),
                ("STEAM_0:0:5", 80, 30),
                ("STEAM_0:0:6", 70, 35));

            var results = Enumerable.Range(1, 4)
                .Select(m => AdminMenuPlugin.Shuffle.GetShuffleResult(players, m))
                .ToList();

            double minDiff = results.Min(r => r.Difference);

            foreach (var r in results)
            {
                Assert.IsTrue(r.Difference >= minDiff - 0.001,
                    $"Method {r.MethodNumber} difference {r.Difference:F4} should be >= min {minDiff:F4}");
            }
        }
    }
}
