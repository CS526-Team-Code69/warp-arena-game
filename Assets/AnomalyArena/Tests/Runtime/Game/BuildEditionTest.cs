using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>What a page serves (BuildEdition): the Chinese page the whole game; the English root the Alpha; /beta/ the English Beta;
    /// off the web the whole game. No scene and no Unity runtime.</summary>
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
        }
    }
}
