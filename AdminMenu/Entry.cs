using System.Text.Json.Serialization;

namespace AdminMenu
{
    public class Entry
    {
        [JsonPropertyName("identity")]
        public string Identity { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}