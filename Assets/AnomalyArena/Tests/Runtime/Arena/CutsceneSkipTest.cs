using NUnit.Framework;

namespace AnomalyArena
{

    [TestFixture]
    public class CutsceneSkipTest
    {
        const float Delay = 1f;

        [TestCase(false, false, false, true, 5f, false, TestName = "LeftClick_LongAfterStart_DoesNotSkip")]
        [TestCase(true, false, false, false, .5f, false, TestName = "Space_InsideTheDelay_DoesNotSkip")]
        [TestCase(false, false, true, true, .2f, false, TestName = "EscapeAndClick_InsideTheDelay_DoNotSkip")]
        [TestCase(true, false, false, false, 1f, true, TestName = "Space_AtTheDelay_Skips")]
        [TestCase(false, true, false, false, 2f, true, TestName = "Enter_AfterTheDelay_Skips")]
        [TestCase(false, false, true, false, 2f, true, TestName = "Escape_AfterTheDelay_Skips")]
        [TestCase(false, false, false, false, 9f, false, TestName = "NoInput_NeverSkips")]
        public void SkipsCutscene_InputAndTime_FollowTheRule(bool space, bool enter, bool escape, bool leftClick, float secondsIn, bool expected)
        {
            var actual = GameManager.SkipsCutscene(space, enter, escape, leftClick, secondsIn, Delay);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
