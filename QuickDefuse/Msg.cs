using System.Resources;
using SharedLibrary;

namespace QuickDefuse
{
    internal static class Msg
    {
        private static readonly ResourceManager _rm =
            new ResourceManager("QuickDefuse.Resources.Messages", typeof(Msg).Assembly);

        public static string Get(string key, params object[] args)
            => Localizer.Get(_rm, key, args);
    }
}
