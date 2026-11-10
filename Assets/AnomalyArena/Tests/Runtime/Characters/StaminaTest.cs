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
        public void Tick_RestingForRecoverDurationFromEmpty_RefillsCompletelyAndEndsExhaustion()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);

            sut.Tick(3f, false);

            Assert.That(sut.Value, Is.EqualTo(1f), "value");
            Assert.That(sut.Exhausted, Is.False, "exhausted");
        }

        [Test]
        public void Tick_EmptiedAndNotYetFull_DoesNotSprint()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);
            sut.Tick(2.9f, false);

            var actual = sut.Tick(0.01f, true);

            Assert.That(actual, Is.EqualTo(1f), "speed");
            Assert.That(sut.Exhausted, Is.True, "still exhausted just short of full");
        }

        [Test]
        public void Tick_EmptiedAndFullAgain_SprintsAgain()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);
            sut.Tick(3f, false);

            var actual = sut.Tick(0.01f, true);

            Assert.That(actual, Is.EqualTo(2f));
        }

        [Test]
        public void Tick_ReleasedWithASliverLeft_StartsAgainAtOnce()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(0.8f, true);
            sut.Tick(0.05f, false);

            var actual = sut.Tick(0.01f, true);

            Assert.That(sut.Exhausted, Is.False, "never ran out");
            Assert.That(actual, Is.EqualTo(2f), "any stamina left is enough to start");
        }

        [Test]
        public void Tick_SprintHeld_KeepsGoingUntilEmpty()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(0.6f, true);

            var actual = sut.Tick(0.2f, true);

            Assert.That(sut.Value, Is.GreaterThan(0f).And.LessThan(0.3f), "a little is left");
            Assert.That(actual, Is.EqualTo(2f), "the running sprint keeps going");
        }

        [Test]
        public void Tick_ExhaustedAndSprintHeld_RecoversInsteadOfSprinting()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);

            sut.Tick(1.5f, true);

            Assert.That(sut.Sprinting, Is.False, "sprinting");
            Assert.That(sut.Value, Is.EqualTo(0.5f).Within(1e-4f), "half refilled while the key stays down");
        }

        [Test]
        public void Refill_WhileExhausted_EndsExhaustion()
        {
            var sut = CreateSystemUnderTest();
            sut.Tick(1f, true);

            sut.Refill();

            Assert.That(sut.Exhausted, Is.False, "exhausted");
            Assert.That(sut.Tick(0.01f, true), Is.EqualTo(2f), "sprints at once");
        }
    }
}
