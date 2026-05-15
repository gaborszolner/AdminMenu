using System.Resources;

namespace SharedLibrary
{
    internal static class Msg
    {
        private static readonly ResourceManager _rm =
            new ResourceManager("SharedLibrary.Resources.Messages", typeof(Msg).Assembly);

        public static string Get(string key, params object[] args)
            => Localizer.Get(_rm, key, args);
    }
}
