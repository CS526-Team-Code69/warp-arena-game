using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class V4NumbersTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [Test]
        public void Add_TwoDamageCards_MultiplyInsteadOfAdding()
        {
            var stats = new RunStats();
            stats.Add(StatId.Damage, .12f);
            stats.Add(StatId.Damage, .12f);
            Assert.That(stats.DamageMultiplier, Is.EqualTo(1.12f * 1.12f).Within(.0001f));
        }

        [Test]
        public void Add_ArmorAndAttackSpeed_DivideWithDiminishingReduction()
        {
            var stats = new RunStats();
            stats.Add(StatId.Protection, 25f);
            Assert.That(stats.DamageTakenMultiplier, Is.EqualTo(.8f).Within(.0001f), "100/(100+25)");
            stats.Add(StatId.Protection, 25f);
            Assert.That(stats.DamageTakenMultiplier, Is.EqualTo(100f / 150f).Within(.0001f), "second card still adds 25% effective HP");
            stats.Add(StatId.AttackSpeed, .15f);
            Assert.That(stats.CooldownMultiplier, Is.EqualTo(1f / 1.15f).Within(.0001f));
        }

        [Test]
        public void RollStatCard_DefaultRules_OffersOnlyMergedCardsWithDamageCarryingCrit()
        {
            var rules = ScriptableObject.CreateInstance<ProgressionRules>();
            try
            {
                var offered = rules.statCards.Where(card => !card.retired).ToArray();
                Assert.That(offered, Has.Length.EqualTo(8));
                var damage = offered.Single(card => card.stat == StatId.Damage);
                Assert.That(damage.bonus, Is.EqualTo(StatBonus.CritChance));
                Assert.That(damage.BonusAt(CardRarity.Common), Is.GreaterThan(0f));
                Assert.That(rules.statCards.Where(card => card.retired).Select(card => card.stat),
                    Is.EquivalentTo(new[] { StatId.Regeneration, StatId.Stamina, StatId.Impact, StatId.PickupRange, StatId.Luck, StatId.EnergySiphon }));
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [Test]
        public void ParMk_WaveSpacingThree_RisesEveryThreeWavesWithIncreasingDamage()
        {
            var roster = ScriptableObject.CreateInstance<WeaponRoster>();
            try
            {
                Assert.That(new[] { 1, 3, 4, 7, 10, 13, 15 }.Select(roster.ParMk), Is.EqualTo(new[] { 1, 1, 2, 3, 4, 5, 5 }));
                for (int mk = 2; mk <= roster.MaxMk; mk++)
                    Assert.That(roster.MkDamage(mk), Is.GreaterThan(roster.MkDamage(mk - 1)), $"Mk{mk}");
            }
            finally { Object.DestroyImmediate(roster); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task Add_FrostThenLightning_SlowsWithoutRootingAndMultiplies()
        {
            await Awaitable.NextFrameAsync();
            var gm = GameManager.Instance;
            gm.enabled = false;
            var enemy = gm.waves.preparedEnemies[0];
            enemy.Configure(enemy.definition, 1f, false);
            var status = ElementStatus.For(enemy);
            status.ResetForSpawn();

            status.Add(ElementKind.Frost, status.FrostThreshold * .5f);
            float chilled = status.SpeedMultiplier;
            Assert.That(chilled, Is.LessThan(1f).And.GreaterThan(status.Rules.lightningSlow), "half-way chill");

            status.Add(ElementKind.Lightning, status.Rules.lightningThreshold * status.ControlThresholdFactor + 1f);
            Assert.That(status.Paralysed, Is.True);
            Assert.That(status.BlocksMovement, Is.False, "paralysis no longer roots");
            Assert.That(status.SpeedMultiplier, Is.EqualTo(chilled * status.Rules.lightningSlow).Within(.0001f), "chill × paralysis");
        }
    }
}
