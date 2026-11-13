using System;

namespace AnomalyArena
{
    /// <summary>
    /// The language the game opens in (one game, both languages; the English web page opens in English, the Chinese page in Chinese).
    /// In this order: the language the player last chose on the title; on the web, the page's address, Chinese when its path holds
    /// "/zh/"; otherwise English. English is the default since 2026-11-10 (until then the editor and the desktop followed the system
    /// language). Plain C# with no Unity call, so the rule is tested on its own; ArenaLanguage reads the inputs from Unity.
    /// </summary>
    public static class StartupLanguage
    {
        /// <summary>The locale codes of the game's two languages (Localization/Locales).</summary>
        public const string Chinese = "zh-Hans", English = "en";

        /// <summary>The start language's locale code, Chinese or English.</summary>
        /// <param name="savedChoice">What the player chose last time (empty or null when never; an unknown code counts as none).</param>
        /// <param name="webPage">The game runs in a web page.</param>
        /// <param name="pageAddress">The page's full address; only read on the web.</param>
        public static string Resolve(string savedChoice, bool webPage, string pageAddress)
        {
            string saved = Normalize(savedChoice);
            if (saved != null) return saved;
            return webPage && PagePathIsChinese(pageAddress) ? Chinese : English;
        }

        /// <summary>A saved or asked-for code as one of the two languages: any "zh..." is Chinese, any "en..." English, anything else none.</summary>
        public static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            code = code.Trim();
            if (code.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return Chinese;
            if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return English;
            return null;
        }

        /// <summary>The address's path holds "/zh/". The host, the query (after ?) and the fragment (after #) do not count, so
        /// "https://zh.example.com/" or "...?page=/zh/" stay English.</summary>
        public static bool PagePathIsChinese(string pageAddress) => PagePath(pageAddress).IndexOf("/zh/", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The address's path, without the scheme and the host, the query (after ?) and the fragment (after #); "/" for an
        /// address that has no path, "" for none. The page's build reads it too (BuildEdition).</summary>
        public static string PagePath(string pageAddress)
        {
            if (string.IsNullOrEmpty(pageAddress)) return "";
            string path = pageAddress;
            int end = path.IndexOfAny(new[] { '?', '#' });
            if (end >= 0) path = path.Substring(0, end);
            int scheme = path.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                int slash = path.IndexOf('/', scheme + 3);
                path = slash >= 0 ? path.Substring(slash) : "/";
            }
            return path;
        }
    }
}
