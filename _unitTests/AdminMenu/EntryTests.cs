using AdminMenu.Entries;

namespace _unitTests.AdminMenu
{
    [TestClass]
    public class EntryTests
    {
        // ── Entry (base class) ───────────────────────────────────────────────

        [TestMethod]
        public void Entry_DefaultIdentity_IsEmpty()
        {
            var entry = new Entry();

            Assert.AreEqual(string.Empty, entry.Identity);
        }

        [TestMethod]
        public void Entry_DefaultName_IsEmpty()
        {
            var entry = new Entry();

            Assert.AreEqual(string.Empty, entry.Name);
        }

        [TestMethod]
        public void Entry_SetIdentityAndName_ReturnsCorrectValues()
        {
            var entry = new Entry { Identity = "STEAM_0:0:12345", Name = "TestPlayer" };

            Assert.AreEqual("STEAM_0:0:12345", entry.Identity);
            Assert.AreEqual("TestPlayer", entry.Name);
        }

        // ── AdminEntry ───────────────────────────────────────────────────────

        [TestMethod]
        public void AdminEntry_DefaultLevel_IsZero()
        {
            var entry = new AdminEntry();

            Assert.AreEqual(0, entry.Level);
        }

        [TestMethod]
        public void AdminEntry_DefaultFlags_IsEmptyArray()
        {
            var entry = new AdminEntry();

            Assert.IsNotNull(entry.Flags);
            Assert.AreEqual(0, entry.Flags.Length);
        }

        [TestMethod]
        public void AdminEntry_SetLevel_ReturnsCorrectLevel()
        {
            var entry = new AdminEntry { Level = 3 };

            Assert.AreEqual(3, entry.Level);
        }

        [TestMethod]
        public void AdminEntry_SetFlags_ReturnsCorrectFlags()
        {
            var entry = new AdminEntry { Flags = ["ban", "kick", "mute"] };

            Assert.AreEqual(3, entry.Flags.Length);
            CollectionAssert.Contains(entry.Flags, "ban");
            CollectionAssert.Contains(entry.Flags, "kick");
            CollectionAssert.Contains(entry.Flags, "mute");
        }

        [TestMethod]
        public void AdminEntry_LevelRange_AcceptsValues1To3()
        {
            foreach (int level in new[] { 1, 2, 3 })
            {
                var entry = new AdminEntry { Level = level };
                Assert.AreEqual(level, entry.Level, $"Level {level} should be stored correctly");
            }
        }

        // ── BannedEntry ──────────────────────────────────────────────────────

        [TestMethod]
        public void BannedEntry_DefaultExpiration_IsMaxValue()
        {
            var entry = new BannedEntry();

            Assert.AreEqual(DateTime.MaxValue, entry.Expiration);
        }

        [TestMethod]
        public void BannedEntry_DefaultBannedBy_IsEmpty()
        {
            var entry = new BannedEntry();

            Assert.AreEqual(string.Empty, entry.BannedBy);
        }

        [TestMethod]
        public void BannedEntry_PastExpiration_IsConsideredExpired()
        {
            var entry = new BannedEntry { Expiration = DateTime.Now.AddDays(-1) };

            Assert.IsTrue(entry.Expiration < DateTime.Now, "Ban with past expiration should be expired");
        }

        [TestMethod]
        public void BannedEntry_FutureExpiration_IsNotExpired()
        {
            var entry = new BannedEntry { Expiration = DateTime.Now.AddDays(30) };

            Assert.IsTrue(entry.Expiration > DateTime.Now, "Ban with future expiration should not be expired");
        }

        [TestMethod]
        public void BannedEntry_SetBannedBy_ReturnsCorrectValue()
        {
            var entry = new BannedEntry { BannedBy = "STEAM_0:0:99999" };

            Assert.AreEqual("STEAM_0:0:99999", entry.BannedBy);
        }

        [TestMethod]
        public void BannedEntry_PermanentBan_ExpirationIsMaxValue()
        {
            var entry = new BannedEntry { Expiration = DateTime.MaxValue };

            Assert.AreEqual(DateTime.MaxValue, entry.Expiration);
        }

        // ── WeaponRestrictEntry ───────────────────────────────────────────────

        [TestMethod]
        public void WeaponRestrictEntry_DefaultMaps_IsEmptyArray()
        {
            var entry = new WeaponRestrictEntry();

            Assert.IsNotNull(entry.Maps);
            Assert.AreEqual(0, entry.Maps.Length);
        }

        [TestMethod]
        public void WeaponRestrictEntry_SetMaps_ReturnsCorrectMaps()
        {
            var entry = new WeaponRestrictEntry { Maps = ["de_dust2", "de_mirage"] };

            Assert.AreEqual(2, entry.Maps.Length);
            CollectionAssert.Contains(entry.Maps, "de_dust2");
            CollectionAssert.Contains(entry.Maps, "de_mirage");
        }
    }
}
