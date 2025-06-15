using System.Text.Json.Serialization;

namespace AdminMenu
{
    public class BannedEntry : Entry
    {
        [JsonPropertyName("bannedby")]
        public string BannedBy { get; set; } = string.Empty;

        [JsonPropertyName("expiration")]
        public DateTime Expiration { get; set; } = DateTime.MaxValue;

    }
}