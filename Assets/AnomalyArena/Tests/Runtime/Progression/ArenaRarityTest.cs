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
            var weapon = new RunWeapon { element = ElementKind.Void, axes = UpgradeAxis.Infusion | UpgradeAxis.Power };

            var actual = ArenaRarity.ForWeapon(weapon, UpgradeAxis.Infusion, ElementKind.Frost);

            Assert.That(actual, Is.EqualTo(CardRarity.Rare));
        }

        [Test]
        public void ForWeapon_FifthUpgradePreview_ReachesLegendary()
        {
            var weapon = new RunWeapon { axes = UpgradeAxis.Infusion | UpgradeAxis.Power | UpgradeAxis.Tempo | UpgradeAxis.Reach };

            var actual = ArenaRarity.ForWeapon(weapon, UpgradeAxis.Tame);

            Assert.That(actual, Is.EqualTo(CardRarity.Legendary));
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
    }
}
