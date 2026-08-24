
namespace SharedLibrary
{
    public class Config
    {
        public bool AutoTeamShuffleOnRoundStart { get; set; } = false;

        public int AutoTeamShuffleMinDifferentPercentage { get; set; } = 25;

        public string WelcomeMessage { get; set; } = "Welcome to the server {0}!";

        public int DateRangeForStatisticsInMonth { get; set; } = 3;

        public int MinimumKillCountToShowInTop { get; set; } = 300;

        public int MinimumPlayerCountToStatistic { get; set; } = 4;

        public bool EnableTopBottomCommand { get; set; } = true;

        public bool AllowSameName { get; set; } = true;

        public int MuteAfterDeathInSecounds { get; set; } = 0;

        public bool CanReviveTeammate { get; set; } = true;

        public int MinCTsForSiteRestrict { get; set; } = 4;

        public float ReviveHoldDurationSeconds { get; set; } = 10.0f;

        public float ReviveDeathWindowSeconds { get; set; } = 30.0f;

        public int ReviveHP { get; set; } = 10;

        public string Language { get; set; } = "en";

        public static Config LoadConfig(string configFile)
        {
            Config? config = null;
            try
            {
                if (!File.Exists(configFile))
                {
                    config = new Config();
                    var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configFile, json);
                    return config;
                }
                string fileContent = File.ReadAllText(configFile);
                config = System.Text.Json.JsonSerializer.Deserialize<Config>(fileContent);
            }
            catch { }


            return config ?? new Config();
        }
    }
}
