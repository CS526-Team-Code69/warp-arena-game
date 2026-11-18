using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>What a page serves (BuildEdition): the Chinese page the whole game; the English root the Alpha; /beta/ the English Beta;
    /// /gold/ the English Gold; off the web the whole game. No scene and no Unity runtime.</summary>
    [TestFixture]
    public class BuildEditionTest
    {
        const string Site = "https://cs526-team-code69.github.io/warp-arena-game/";

        [TestCase(Site, ExpectedResult = BuildEdition.Edition.EnglishAlpha, TestName = "The site's root is the English Alpha")]
        [TestCase(Site + "index.html", ExpectedResult = BuildEdition.Edition.EnglishAlpha, TestName = "The root's index file is the English Alpha")]
        [TestCase(Site + "zh/", ExpectedResult = BuildEdition.Edition.Full, TestName = "The Chinese page is the whole game")]
        [TestCase(Site + "beta/", ExpectedResult = BuildEdition.Edition.EnglishBeta, TestName = "The beta folder is the English Beta")]
        [TestCase(Site + "beta/zh/", ExpectedResult = BuildEdition.Edition.Full, TestName = "A Chinese page under beta is the whole game too")]
        [TestCase(Site + "beta/index.html?debug=1#top", ExpectedResult = BuildEdition.Edition.EnglishBeta, TestName = "The Beta with a query and a fragment")]
        [TestCase("http://127.0.0.1:8735/beta/", ExpectedResult = BuildEdition.Edition.EnglishBeta, TestName = "A local copy of the Beta")]
        [TestCase(Site + "?page=/beta/", ExpectedResult = BuildEdition.Edition.EnglishAlpha, TestName = "beta in the query does not count")]
        [TestCase(Site + "#/zh/", ExpectedResult = BuildEdition.Edition.EnglishAlpha, TestName = "zh in the fragment does not count")]
        [TestCase("https://beta.example.com/game/", ExpectedResult = BuildEdition.Edition.EnglishAlpha, TestName = "beta in the host does not count")]
        [TestCase(Site + "gold/", ExpectedResult = BuildEdition.Edition.EnglishGold, TestName = "The gold folder is the English Gold")]
        [TestCase(Site + "gold/index.html?debug=1#top", ExpectedResult = BuildEdition.Edition.EnglishGold, TestName = "The Gold with a query and a fragment")]
        [TestCase("https://cs526-team-code69.github.io/warp-arena-gold/", ExpectedResult = BuildEdition.Edition.EnglishGold, TestName = "A site named for the Gold is the English Gold")]
        [TestCase(Site + "gold/zh/", ExpectedResult = BuildEdition.Edition.Full, TestName = "A Chinese page under gold is the whole game")]
        [TestCase(Site + "?page=/gold/", ExpectedResult = BuildEdition.Edition.EnglishAlpha, TestName = "gold in the query does not count")]
        [TestCase("", ExpectedResult = BuildEdition.Edition.EnglishAlpha, TestName = "A web page without an address is the English Alpha")]
        public BuildEdition.Edition Of_OnTheWeb_FollowsThePagePath(string page) => BuildEdition.Of(true, page);

        [TestCase("", TestName = "Off the web without an address")]
        [TestCase(Site, TestName = "Off the web an address is not read")]
        public void Of_OffTheWeb_IsTheWholeGame(string page) => Assert.That(BuildEdition.Of(false, page), Is.EqualTo(BuildEdition.Edition.Full));

        [Test]
        public void Current_InTheEditorAndTheTests_IsTheWholeGame()
        {
            Assert.That(BuildEdition.Current, Is.EqualTo(BuildEdition.Edition.Full));
            Assert.That(BuildEdition.IsCourseBuild, Is.False);
            Assert.That(BuildEdition.PlaysCutscenes, Is.True);
        }

        [TestCase(BuildEdition.Edition.Full, ExpectedResult = true, TestName = "The whole game plays the story's cutscenes")]
        [TestCase(BuildEdition.Edition.EnglishBeta, ExpectedResult = false, TestName = "The English Beta plays none")]
        [TestCase(BuildEdition.Edition.EnglishAlpha, ExpectedResult = false, TestName = "The English Alpha, which has no story, plays none")]
        [TestCase(BuildEdition.Edition.EnglishGold, ExpectedResult = true, TestName = "The English Gold plays them")]
        public bool CutscenesOn_TheWholeGameAndTheGold(BuildEdition.Edition edition) => BuildEdition.CutscenesOn(edition);

        [TestCase(BuildEdition.Edition.Full, ExpectedResult = "-full", TestName = "The whole game's rows end in -full")]
        [TestCase(BuildEdition.Edition.EnglishBeta, ExpectedResult = "-beta", TestName = "The Beta's rows end in -beta")]
        [TestCase(BuildEdition.Edition.EnglishAlpha, ExpectedResult = "-alpha", TestName = "The Alpha's rows end in -alpha")]
        [TestCase(BuildEdition.Edition.EnglishGold, ExpectedResult = "-gold", TestName = "The Gold's rows end in -gold")]
        public string RowLabel_NamesEachPage(BuildEdition.Edition edition) => BuildEdition.RowLabel(edition);

        [TestCase(false, BuildEdition.Edition.EnglishGold, ExpectedResult = true, TestName = "The English Gold starts illustrated")]
        [TestCase(false, BuildEdition.Edition.EnglishBeta, ExpectedResult = false, TestName = "The English Beta starts in whitebox")]
        [TestCase(false, BuildEdition.Edition.Full, ExpectedResult = false, TestName = "The whole game opened in English starts in whitebox")]
        [TestCase(true, BuildEdition.Edition.Full, ExpectedResult = true, TestName = "The whole game opened in Chinese starts illustrated")]
        public bool DefaultIllustrated_FollowsTheLanguageAndThePage(bool startedInChinese, BuildEdition.Edition edition) =>
            Art.DefaultIllustrated(startedInChinese, edition);
    }
}
