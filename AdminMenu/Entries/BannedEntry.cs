using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace AdminMenu.Entries
{
    public class BannedEntry : Entry
    {
        [JsonPropertyName("bannedby")]
        public string BannedBy { get; set; } = string.Empty;

        [JsonPropertyName("expiration")]
        public DateTime Expiration { get; set; } = DateTime.MaxValue;
    }
}