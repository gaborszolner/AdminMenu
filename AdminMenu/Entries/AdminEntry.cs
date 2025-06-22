using System.Text.Json.Serialization;

namespace AdminMenu.Entries
{
    public class AdminEntry : Entry
    {
        [JsonPropertyName("level")] // Bigger is better, 1-3
        public int Level { get; set; } = 0;

        [JsonPropertyName("flags")]
        public string[] flags { get; set; } = Array.Empty<string>();

    }
}