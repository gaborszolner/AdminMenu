namespace AdminMenu
{
    internal class Player(string identity, string name, int adminLevel)
    {
        public string Identity { get; set; } = identity;
        public string Name { get; set; } = name;
        internal int AdminLevel { get; set; } = adminLevel;
    }
}
