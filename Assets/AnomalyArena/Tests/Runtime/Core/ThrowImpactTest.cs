using NUnit.Framework;

namespace AnomalyArena
{
    [TestFixture]
    public class ThrowImpactTest
    {
        [Test]
        public void DamageAt_FullSpeedReferenceBody_ReturnsBaseDamage()
        {
            var sut = new ThrowImpact { damage = 12f, sizeExponent = 1f };

            var actual = sut.DamageAt(1f, ThrowImpact.ReferenceRadius);

            Assert.That(actual, Is.EqualTo(12f));
        }

        [Test]
        public void DamageAt_HalfSpeed_ReturnsQuarterOfBaseDamage()
        {
            var sut = new ThrowImpact { damage = 12f, sizeExponent = 1f };

            var actual = sut.DamageAt(0.5f, ThrowImpact.ReferenceRadius);

            Assert.That(actual, Is.EqualTo(3f));
        }

        [Test]
        public void DamageAt_TwiceTheReferenceRadius_ReturnsTwiceTheBaseDamage()
        {
            var sut = new ThrowImpact { damage = 12f, sizeExponent = 1f };

            var actual = sut.DamageAt(1f, ThrowImpact.ReferenceRadius * 2f);

            Assert.That(actual, Is.EqualTo(24f));
        }

        [Test]
        public void DamageAt_AlmostNoSpeedLeft_ReturnsOne()
        {
            var sut = new ThrowImpact { damage = 12f, sizeExponent = 1f };

            var actual = sut.DamageAt(0.05f, ThrowImpact.ReferenceRadius);

            Assert.That(actual, Is.EqualTo(1f));
        }

        [Test]
        public void KnockbackAt_HalfSpeed_ReturnsHalfOfBaseKnockback()
        {
            var sut = new ThrowImpact { knockback = 5f, sizeExponent = 1f };

            var actual = sut.KnockbackAt(0.5f, ThrowImpact.ReferenceRadius);

            Assert.That(actual, Is.EqualTo(2.5f));
        }
    }
}
