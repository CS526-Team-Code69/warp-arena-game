using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class MissileStrikeEffectTest
    {
        [Test]
        public void StrikePoint_AimPointWithinMaxRange_ReturnsAimPoint()
        {
            var sut = ScriptableObject.CreateInstance<MissileStrikeEffect>();
            sut.maxRange = 20f;

            var actual = sut.StrikePoint(Vector3.zero, new Vector3(3f, 0f, 4f));

            Assert.That(actual, Is.EqualTo(new Vector3(3f, 0f, 4f)));
        }

        [Test]
        public void StrikePoint_AimPointBeyondMaxRange_ReturnsPointAtMaxRange()
        {
            var sut = ScriptableObject.CreateInstance<MissileStrikeEffect>();
            sut.maxRange = 10f;

            var actual = sut.StrikePoint(Vector3.zero, new Vector3(0f, 0f, 30f));

            Assert.That(actual, Is.EqualTo(new Vector3(0f, 0f, 10f)));
        }

        [Test]
        public void StrikePoint_AimPointAboveTheGround_ReturnsPointOnTheGround()
        {
            var sut = ScriptableObject.CreateInstance<MissileStrikeEffect>();
            sut.maxRange = 20f;

            var actual = sut.StrikePoint(new Vector3(0f, 0.8f, 0f), new Vector3(3f, 0.8f, 4f));

            Assert.That(actual.y, Is.EqualTo(0f));
        }
    }
}
