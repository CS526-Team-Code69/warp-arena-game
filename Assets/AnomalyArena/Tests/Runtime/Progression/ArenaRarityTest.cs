using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class ArenaRarityTest
    {
        [Test]
        public void ForWeapon_InfusionPreview_UsesSelectedVoidWithoutMutatingWeapon()
        {
            var weapon = new RunWeapon { element = ElementKind.Fire, axes = UpgradeAxis.Power };
            var card = new DraftCard(DraftCardKind.Weapon, "Infusion", "Void", _ => true,
                payload: new WeaponUpgradeOffer(weapon, UpgradeAxis.Infusion, ElementKind.Void));

            Assert.That(card.Rarity, Is.EqualTo(CardRarity.Void), "preview rarity");
            Assert.That(weapon.element, Is.EqualTo(ElementKind.Fire), "current weapon element");
        }

        [Test]
        public void ForWeapon_ReplacingVoidInfusion_RestoresUpgradeTierWithoutCountingInfusionTwice()
        {
            var weapon = new RunWeapon { element = ElementKind.Void, axes = UpgradeAxis.Infusion };

            var actual = ArenaRarity.ForWeapon(weapon, UpgradeAxis.Infusion, ElementKind.Frost);

            Assert.That(actual, Is.EqualTo(CardRarity.Rare));
        }

        [TestCase(UpgradeAxis.None, CardRarity.Common)]
        [TestCase(UpgradeAxis.Power, CardRarity.Rare)]
        [TestCase(UpgradeAxis.Power | UpgradeAxis.Tempo, CardRarity.Exceptional)]
        [TestCase(UpgradeAxis.Power | UpgradeAxis.Tempo | UpgradeAxis.Reach, CardRarity.Exceptional)]
        [TestCase(UpgradeAxis.Power | UpgradeAxis.Tempo | UpgradeAxis.Reach | UpgradeAxis.Twist, CardRarity.Epic)]
        public void ForWeapon_UpgradeCount_MapsToTier(UpgradeAxis axes, CardRarity expected)
        {
            var weapon = new RunWeapon { axes = axes };

            var actual = ArenaRarity.ForWeapon(weapon);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ForWeapon_FifthUpgradePreview_ReachesLegendary()
        {
            var weapon = new RunWeapon { axes = UpgradeAxis.Infusion | UpgradeAxis.Power | UpgradeAxis.Tempo | UpgradeAxis.Reach };

            var actual = ArenaRarity.ForWeapon(weapon, UpgradeAxis.Tame);

            Assert.That(actual, Is.EqualTo(CardRarity.Legendary));
        }

        [TestCase(1, CardRarity.Common)]
        [TestCase(2, CardRarity.Rare)]
        [TestCase(3, CardRarity.Exceptional)]
        public void ForSkill_OrdinarySkillLevel_MapsToTier(int level, CardRarity expected)
        {
            var actual = ArenaRarity.ForSkill(null, level);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void ForSkill_VoidDefinition_StaysVoidAtEveryExistingLevel(int level)
        {
            var definition = ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                definition.element = ElementKind.Void;

                var actual = ArenaRarity.ForSkill(definition, level);

                Assert.That(actual, Is.EqualTo(CardRarity.Void));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void Rank_AllTiers_FollowsDisplayOrderNotSerializedInts()
        {
            var tiers = new[] { CardRarity.Common, CardRarity.Rare, CardRarity.Exceptional, CardRarity.Epic, CardRarity.Legendary, CardRarity.Void };

            var actual = tiers.Select(ArenaRarity.Rank).ToArray();

            Assert.That(actual, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        }

        [TestCase(CardRarity.Common, CardRarity.Rare)]
        [TestCase(CardRarity.Rare, CardRarity.Exceptional)]
        [TestCase(CardRarity.Exceptional, CardRarity.Epic)]
        [TestCase(CardRarity.Epic, CardRarity.Legendary)]
        [TestCase(CardRarity.Legendary, CardRarity.Legendary)]
        [TestCase(CardRarity.Void, CardRarity.Void)]
        public void Next_Tier_StepsUpAndStopsAtLegendaryOrVoid(CardRarity rarity, CardRarity expected)
        {
            var actual = ArenaRarity.Next(rarity);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
