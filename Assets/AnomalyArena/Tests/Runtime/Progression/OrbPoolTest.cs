using System.Linq;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class OrbPoolTest
    {
        ProgressionRules rules;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        RunProgression CreateProgression()
        {
            rules = ScriptableObject.CreateInstance<ProgressionRules>();
            var progression = new GameObject("Progression").AddComponent<RunProgression>();
            progression.Rules = rules;
            progression.ResetRun();
            return progression;
        }

        static OrbPool CreateSystemUnderTest(RunProgression progression)
        {
            var sut = new GameObject("Orbs").AddComponent<OrbPool>();
            sut.pool = new[] { new GameObject("Orb A").AddComponent<ExperienceOrb>(), new GameObject("Orb B").AddComponent<ExperienceOrb>() };
            sut.progression = progression;
            return sut;
        }

        [Test, CreateScene]
        public void Drop_EliteEnergyOrbWithEnergyUnlocked_SpawnsEnergyOrbLeftOfTheBody()
        {
            var progression = CreateProgression();
            progression.UnlockEnergy();
            var sut = CreateSystemUnderTest(progression);

            sut.Drop(Vector3.zero, 0f, dropEnergy: true);

            var actual = sut.pool.Single(orb => orb.InUse);
            Assert.That(actual.Kind, Is.EqualTo(OrbKind.Energy), "kind");
            Assert.That(actual.Value, Is.EqualTo(sut.energyPerOrb), "value");
            Assert.That(actual.Position.x, Is.EqualTo(-.45f).Within(.0001f), "offset");
        }

        [Test, CreateScene]
        public void Drop_EnergyStillLocked_SpawnsNoEnergyOrb()
        {
            var sut = CreateSystemUnderTest(CreateProgression());

            sut.Drop(Vector3.zero, 0f, dropEnergy: true);

            Assert.That(sut.pool, Has.None.Matches<ExperienceOrb>(orb => orb.InUse));
        }

        [Test, CreateScene]
        public void CollectAll_EnergyOrbOnField_RestoresEnergyPerOrb()
        {
            var progression = CreateProgression();
            progression.UnlockEnergy();
            progression.TrySpendEnergy(50f);
            var sut = CreateSystemUnderTest(progression);
            sut.Drop(Vector3.zero, 0f, dropEnergy: true);

            sut.CollectAll();

            Assert.That(progression.Energy, Is.EqualTo(progression.Stats.maxEnergy - 50f + sut.energyPerOrb).Within(.0001f));
        }
    }
}
