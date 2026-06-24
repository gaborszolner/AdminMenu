namespace AdminMenu.Entries
{
    public class VoteState
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Options { get; set; } = [];
        public string InitiatorSteamID2 { get; set; } = string.Empty;
        public long StartTime { get; set; } = DateTime.UtcNow.Ticks;
        public const long VoteDurationTicks = 10 * TimeSpan.TicksPerSecond; // 10 seconds
    }
}
