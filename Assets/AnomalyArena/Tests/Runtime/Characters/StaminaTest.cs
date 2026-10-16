using NUnit.Framework;

namespace AnomalyArena
{
    [TestFixture]
    public class StaminaTest
    {
        private static Stamina CreateSystemUnderTest() => new Stamina
        {
            sprintDuration = 1f,
            recoverDuration = 3f,
            sprintMultiplier = 2f,
            minToStart = 0.3f,
        };

        [Test]
        public void Tick_SprintHeld_ReturnsSprintMultiplier()
        {
            var sut = CreateSystemUnderTest();

            var actual = sut.Tick(0.1f, true);

            Assert.That(actual, Is.EqualTo(2f));
        }

        [Test]
        public void Tick_SprintHeldForSprintDuration_EmptiesAndBecomesExhausted()
        {
            var sut = CreateSystemUnderTest();

            sut.Tick(1f, true);

            Assert.That(sut.Value, Is.EqualTo(0f), "value");
            Assert.That(sut.Exhausted, Is.True, "exhausted");
        }

        [Test]
        public void Tick_RestingForRecoverDurationFromEmpty_RefillsCompletely()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);

            sut.Tick(3f, false);

            Assert.That(sut.Value, Is.EqualTo(1f));
        }

        [Test]
        public void Tick_EmptyAndRecoveredBelowStartThreshold_DoesNotSprint()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);
            sut.Tick(0.5f, false);

            var actual = sut.Tick(0.01f, true);

            Assert.That(actual, Is.EqualTo(1f));
        }

        [Test]
        public void Tick_EmptyAndRecoveredAboveStartThreshold_SprintsAgain()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);
            sut.Tick(1f, false);

            var actual = sut.Tick(0.01f, true);

            Assert.That(actual, Is.EqualTo(2f));
        }

        [Test]
        public void Tick_ReleasedWithASliverLeft_CannotStartAgainUntilThreshold()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(0.8f, true);
            sut.Tick(0.05f, false);

            var actual = sut.Tick(0.01f, true);

            Assert.That(actual, Is.EqualTo(1f), "a quarter of stamina is not enough to start");
        }

        [Test]
        public void Tick_SprintStartedAboveThreshold_ContinuesBelowItUntilEmpty()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(0.6f, true);

            var actual = sut.Tick(0.2f, true);

            Assert.That(sut.Value, Is.LessThan(sut.minToStart), "stamina went under the start threshold");
            Assert.That(actual, Is.EqualTo(2f), "the running sprint keeps going");
        }
    }
}
