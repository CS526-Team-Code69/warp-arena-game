using System;
using System.Linq;
using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>The collection's rules that need no scene (ArenaCodex, ArenaCodexProgress): it lists every weapon but the retired ones,
    /// and what was met is kept in the saved settings. The whole test run keeps those in memory (TestSavedSettings); these tests only
    /// delete the records they touch, before and after.</summary>
    [TestFixture]
    public class CodexRulesTest
    {
        static readonly string[] Touched = { ArenaCodexProgress.Key("enemy", (int)EnemyKind.Grunt), ArenaCodexProgress.Key("enemy", (int)EnemyKind.Brute),
            ArenaCodexProgress.Key("skill", 0) };

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaPrefs.MemoryForTests, Is.True, "the test run keeps the saved choices in memory (TestSavedSettings)");
            Forget();
        }

        [TearDown]
        public void TearDown() => Forget();

        static void Forget()
        {
            foreach (var key in Touched) ArenaPrefs.Delete(key);
            ArenaCodexProgress.ForgetCache();
        }

        [Test]
        public void ListedWeapons_LeaveOutTheRetiredForms()
        {
            var listed = ArenaCodex.ListedWeapons().ToArray();
            var offered = Enum.GetValues(typeof(WeaponForm)).Cast<WeaponForm>().Where(form => !WeaponRoster.IsRetired(form));

            Assert.That(listed.Where(WeaponRoster.IsRetired), Is.Empty, "no retired weapon is listed");
            Assert.That(listed, Is.EquivalentTo(offered), "every other weapon is");
        }

        [Test]
        public void DiscoverEnemy_IsKept_OthersStayUnmet()
        {
            Assume.That(ArenaCodexProgress.EnemyDiscovered(EnemyKind.Grunt), Is.False, "the tests' settings start empty");

            ArenaCodexProgress.DiscoverEnemy(EnemyKind.Grunt);
            ArenaCodexProgress.ForgetCache(); // read back from the settings, not from what was remembered

            Assert.That(ArenaCodexProgress.EnemyDiscovered(EnemyKind.Grunt), Is.True, "a monster met is kept");
            Assert.That(ArenaCodexProgress.EnemyDiscovered(EnemyKind.Brute), Is.False, "one not met stays unmet");
        }

        [Test]
        public void DiscoverSkill_IsKept()
        {
            var skill = (SkillId)Enum.GetValues(typeof(SkillId)).GetValue(0);
            Assume.That(ArenaCodexProgress.SkillDiscovered(skill), Is.False, "the tests' settings start empty");

            ArenaCodexProgress.DiscoverSkill(skill);
            ArenaCodexProgress.ForgetCache();

            Assert.That(ArenaCodexProgress.SkillDiscovered(skill), Is.True);
        }
    }
}
