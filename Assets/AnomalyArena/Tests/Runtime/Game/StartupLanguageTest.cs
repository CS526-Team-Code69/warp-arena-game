using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>The start language's rule (StartupLanguage): the player's saved choice first; on the web the page's path, Chinese with
    /// "/zh/" in it; English otherwise (the default since 2026-11-10, when the system language stopped counting). No scene and no Unity
    /// runtime.</summary>
    [TestFixture]
    public class StartupLanguageTest
    {
        const string EnglishPage = "https://cs526-team-code69.github.io/warp-arena-game/";
        const string ChinesePage = "https://cs526-team-code69.github.io/warp-arena-game/zh/";

        [TestCase("zh-Hans", true, EnglishPage, ExpectedResult = StartupLanguage.Chinese, TestName = "Saved Chinese beats the English page")]
        [TestCase("en", true, ChinesePage, ExpectedResult = StartupLanguage.English, TestName = "Saved English beats the Chinese page")]
        [TestCase("en", false, "", ExpectedResult = StartupLanguage.English, TestName = "Saved English off the web")]
        [TestCase("zh-Hans", false, "", ExpectedResult = StartupLanguage.Chinese, TestName = "Saved Chinese beats the English default")]
        [TestCase("zh-CN", false, "", ExpectedResult = StartupLanguage.Chinese, TestName = "Any saved zh code is Chinese")]
        [TestCase("EN-us", false, "", ExpectedResult = StartupLanguage.English, TestName = "Any saved en code is English")]
        public string Resolve_SavedChoice_ComesFirst(string saved, bool web, string page) =>
            StartupLanguage.Resolve(saved, web, page);

        [TestCase(ChinesePage, ExpectedResult = StartupLanguage.Chinese, TestName = "The Chinese page")]
        [TestCase(ChinesePage + "index.html", ExpectedResult = StartupLanguage.Chinese, TestName = "The Chinese page's index file")]
        [TestCase(ChinesePage + "?debug=1#top", ExpectedResult = StartupLanguage.Chinese, TestName = "The Chinese page with a query and a fragment")]
        [TestCase("http://localhost:8000/zh/", ExpectedResult = StartupLanguage.Chinese, TestName = "A local copy of the Chinese page")]
        [TestCase(EnglishPage, ExpectedResult = StartupLanguage.English, TestName = "The English page")]
        [TestCase(EnglishPage + "index.html", ExpectedResult = StartupLanguage.English, TestName = "The English page's index file")]
        [TestCase(EnglishPage + "?lang=/zh/", ExpectedResult = StartupLanguage.English, TestName = "zh in the query does not count")]
        [TestCase(EnglishPage + "#/zh/", ExpectedResult = StartupLanguage.English, TestName = "zh in the fragment does not count")]
        [TestCase("https://zh.example.com/game/", ExpectedResult = StartupLanguage.English, TestName = "zh in the host does not count")]
        [TestCase("https://example.com/zhx/", ExpectedResult = StartupLanguage.English, TestName = "A folder only starting with zh does not count")]
        [TestCase("", ExpectedResult = StartupLanguage.English, TestName = "No address is English")]
        [TestCase(null, ExpectedResult = StartupLanguage.English, TestName = "A null address is English")]
        public string Resolve_WebWithoutSavedChoice_FollowsThePagePath(string page) =>
            StartupLanguage.Resolve("", true, page);

        [TestCase("", ExpectedResult = StartupLanguage.English, TestName = "Off the web the game starts in English")]
        [TestCase(ChinesePage, ExpectedResult = StartupLanguage.English, TestName = "An address is only read on the web")]
        public string Resolve_OffTheWebWithoutSavedChoice_IsEnglish(string page) =>
            StartupLanguage.Resolve(null, false, page);

        [TestCase("fr", ExpectedResult = StartupLanguage.English, TestName = "An unknown saved code counts as none")]
        [TestCase("   ", ExpectedResult = StartupLanguage.English, TestName = "A blank saved code counts as none")]
        public string Resolve_UnknownSavedChoice_FallsThroughToThePage(string saved) =>
            StartupLanguage.Resolve(saved, true, EnglishPage);
    }
}
