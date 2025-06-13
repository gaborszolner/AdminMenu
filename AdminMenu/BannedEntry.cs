using System.Text.Json.Serialization;

namespace AdminMenu
{
    public class BannedEntry : Entry
    {
        [JsonPropertyName("expiration")]
        public DateTime Expiration { get; set; } = DateTime.MaxValue;

    }
}