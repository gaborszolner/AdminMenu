using System.Text.Json.Serialization;

namespace AdminMenu
{
    public class AdminEntry
    {
        [JsonPropertyName("identity")]
        public string Identity { get; set; } = string.Empty;

        [JsonPropertyName("flags")]
        public string[] Flags { get; set; } = Array.Empty<string>();
    }
}