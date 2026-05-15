using System.Globalization;
using System.Resources;

namespace SharedLibrary
{
    public static class Localizer
    {
        public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;

        public static void Initialize(string? language)
        {
            CurrentCulture = language?.ToLowerInvariant() switch
            {
                "hu" => new CultureInfo("hu"),
                "de" => new CultureInfo("de"),
                "es" => new CultureInfo("es"),
                "pt" => new CultureInfo("pt"),
                "uk" => new CultureInfo("uk"),
                "zh" => new CultureInfo("zh"),
                "hi" => new CultureInfo("hi"),
                "tr" => new CultureInfo("tr"),
                "fr" => new CultureInfo("fr"),
                "ru" => new CultureInfo("ru"),
                _ => CultureInfo.InvariantCulture
            };
        }

        public static string Get(ResourceManager rm, string key, params object[] args)
        {
            string template = rm.GetString(key, CurrentCulture) ?? key;
            return args.Length > 0 ? string.Format(template, args) : template;
        }
    }
}
