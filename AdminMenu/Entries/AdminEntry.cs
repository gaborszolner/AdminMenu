using System.Text.Json.Serialization;

namespace AdminMenu.Entries
{
    public class AdminEntry : Entry
    {
        [JsonPropertyName("level")] // Higher number has more rights, 1-3
        public int Level { get; set; } = 0;

        [JsonPropertyName("flags")]
        public string[] Flags { get; set; } = [];

    }
}