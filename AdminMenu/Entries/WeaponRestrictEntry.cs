using System.Text.Json.Serialization;

namespace AdminMenu.Entries
{
    public class WeaponRestrictEntry
    {
        [JsonPropertyName("maps")]
        public string[] Maps { get; set; } = [];
    }
}