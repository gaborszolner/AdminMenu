using System.Text.Json;

namespace SiteRestrict
{
    /// <summary>
    /// Stores which maps have their bombsite A/B labels switched.
    /// Persisted to a JSON file placed next to the plugin dll.
    /// </summary>
    public class SiteSwitchConfig
    {
        public HashSet<string> SwappedMaps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsSwapped(string mapName)
            => !string.IsNullOrEmpty(mapName) && SwappedMaps.Contains(mapName);

        /// <summary>
        /// Toggles the swapped state for the given map. Returns the new state.
        /// </summary>
        public bool Toggle(string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
                return false;

            if (SwappedMaps.Contains(mapName))
            {
                SwappedMaps.Remove(mapName);
                return false;
            }

            SwappedMaps.Add(mapName);
            return true;
        }

        public static SiteSwitchConfig Load(string configFile)
        {
            try
            {
                if (!File.Exists(configFile))
                {
                    var created = new SiteSwitchConfig();
                    created.Save(configFile);
                    return created;
                }

                string fileContent = File.ReadAllText(configFile);
                var config = JsonSerializer.Deserialize<SiteSwitchConfig>(fileContent);
                return config ?? new SiteSwitchConfig();
            }
            catch
            {
                return new SiteSwitchConfig();
            }
        }

        public void Save(string configFile)
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFile, json);
            }
            catch { }
        }
    }
}
