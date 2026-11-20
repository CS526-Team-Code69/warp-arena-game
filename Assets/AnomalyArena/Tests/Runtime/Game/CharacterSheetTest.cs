using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>The character screen's pure parts, no scene: which bonus lines CharacterBonuses.From gives for the player's stats at the
    /// run's start and now and the run's other effects, in which order, and how each line is worded in both languages; and what a taken
    /// upgrade card keeps of itself (TakenCard), so the open mode's list can name it and count the same card once.</summary>
    [TestFixture]
    public class CharacterSheetTest
    {
        static RunStats Start() => new();

        static CharacterBonusExtras Extras(bool energy = false, bool skills = false)
        {
            var extras = CharacterBonusExtras.None;
            extras.energyShown = energy;
            extras.skillsShown = skills;
            return extras;
        }

        static List<CharacterBonusKind> Kinds(List<CharacterBonus> lines) => lines.Select(line => line.Kind).ToList();

        // ───── Which lines ─────

        [Test]
        public void From_StatsAtTheirStart_GivesNoLine()
        {
            var start = Start();

            var actual = CharacterBonuses.From(start, start.Copy(), Extras(true, true));

            Assert.That(actual, Is.Empty, "nothing differs from the start, so there is nothing to list");
        }

        [Test]
        public void From_TwoDamageCards_ShowOneLineWithTheirProduct()
        {
            var start = Start();
            var now = start.Copy();
            now.Add(StatId.Damage, .12f);
            now.Add(StatId.Damage, .12f);

            var actual = CharacterBonuses.From(start, now, Extras());

            Assert.That(Kinds(actual), Is.EqualTo(new[] { CharacterBonusKind.Damage }));
            Assert.That(actual[0].Value(true), Is.EqualTo("+25%"), "damage cards multiply: 1.12 × 1.12 is +25 %, not +24 %");
        }

        [TestCase(StatId.MaxHp, 15f, CharacterBonusKind.MaxHp, "+15")]
        [TestCase(StatId.AttackSpeed, .25f, CharacterBonusKind.AttackSpeed, "+25%")]
        [TestCase(StatId.MoveSpeed, .08f, CharacterBonusKind.MoveSpeed, "+8%")]
        [TestCase(StatId.Protection, 25f, CharacterBonusKind.DamageTaken, "-20%")]
        [TestCase(StatId.CritChance, .1f, CharacterBonusKind.CritChance, "+10%")]
        [TestCase(StatId.ElementPenetration, .2f, CharacterBonusKind.Penetration, "+20%")]
        public void From_OneStatRaised_ShowsItsLineAsTheGameCountsIt(StatId stat, float amount, CharacterBonusKind kind, string value)
        {
            var start = Start();
            var now = start.Copy();
            now.Add(stat, amount);

            var actual = CharacterBonuses.From(start, now, Extras());

            Assert.That(Kinds(actual), Is.EqualTo(new[] { kind }));
            Assert.That(actual[0].Value(true), Is.EqualTo(value));
            Assert.That(actual[0].Value(false), Is.EqualTo(value), "a number reads the same in both languages");
        }

        [Test]
        public void From_CritMultiplierRaised_ShowsTheMultiplierItself()
        {
            var start = Start();
            var now = start.Copy();
            now.AddBonus(StatBonus.CritMultiplier, .15f);

            var actual = CharacterBonuses.From(start, now, Extras());

            Assert.That(Kinds(actual), Is.EqualTo(new[] { CharacterBonusKind.CritDamage }));
            Assert.That(actual[0].Value(false), Is.EqualTo("×2.15"));
        }

        [Test]
        public void From_EnergyLines_ShowOnlyOnceTheEnergyIsUnlocked()
        {
            var start = Start();
            var now = start.Copy();
            now.Add(StatId.MaxEnergy, 25f);
            var extras = Extras();
            extras.energyRegeneration = 4f;

            var hidden = CharacterBonuses.From(start, now, extras);
            extras.energyShown = true;
            var shown = CharacterBonuses.From(start, now, extras);

            Assert.That(hidden, Is.Empty, "before the energy is unlocked nothing about it is listed");
            Assert.That(Kinds(shown), Is.EqualTo(new[] { CharacterBonusKind.MaxEnergy, CharacterBonusKind.EnergyRegeneration }));
            Assert.That(shown[0].Value(true), Is.EqualTo("+25"));
            Assert.That(shown[1].Value(true), Is.EqualTo("每秒 +4"));
            Assert.That(shown[1].Value(false), Is.EqualTo("+4 a second"));
        }

        [Test]
        public void From_SkillFactors_ShowOnlyOnceASkillIsLearned()
        {
            var start = Start();
            var extras = Extras();
            extras.skillCooldown = .7f;
            extras.skillCost = .7f;

            var hidden = CharacterBonuses.From(start, start.Copy(), extras);
            extras.skillsShown = true;
            var shown = CharacterBonuses.From(start, start.Copy(), extras);

            Assert.That(hidden, Is.Empty);
            Assert.That(Kinds(shown), Is.EqualTo(new[] { CharacterBonusKind.SkillCooldown, CharacterBonusKind.SkillCost }));
            Assert.That(shown[0].Value(true), Is.EqualTo("-30%"), "a shorter cooldown is a reduction");
        }

        [Test]
        public void From_SeveralChanges_KeepTheScreensOrder()
        {
            var start = Start();
            var now = start.Copy();
            now.Add(StatId.Protection, 25f);
            now.Add(StatId.Damage, .12f);
            now.Add(StatId.MaxHp, 15f);
            var extras = Extras(skills: true);
            extras.weaponDamage = 1.06f;
            extras.bossPenetration = .15f;
            extras.statusFloor = 50f;

            var actual = CharacterBonuses.From(start, now, extras);

            Assert.That(Kinds(actual), Is.EqualTo(new[]
            {
                CharacterBonusKind.MaxHp, CharacterBonusKind.Damage, CharacterBonusKind.DamageTaken, CharacterBonusKind.WeaponDamage,
                CharacterBonusKind.BossPenetration, CharacterBonusKind.StatusFloor,
            }));
        }

        [Test]
        public void From_AChangeTooSmallToShow_GivesNoLine()
        {
            var start = Start();
            var now = start.Copy();
            now.attackSpeedBonus = .0001f;

            var actual = CharacterBonuses.From(start, now, Extras());

            Assert.That(actual, Is.Empty, "+0.01 % would read as +0 %");
        }

        // ───── Words ─────

        [TestCase(CharacterBonusKind.Damage, "伤害", "Damage")]
        [TestCase(CharacterBonusKind.DamageTaken, "受到的伤害", "Damage taken")]
        [TestCase(CharacterBonusKind.SkillCooldown, "技能冷却", "Skill cooldowns")]
        [TestCase(CharacterBonusKind.MaxHp, "最大生命", "Max HP")]
        public void Name_EachLanguage_IsThePlainName(CharacterBonusKind kind, string chinese, string english)
        {
            var sut = new CharacterBonus(kind, 10f);

            Assert.That(sut.Name(true), Is.EqualTo(chinese));
            Assert.That(sut.Name(false), Is.EqualTo(english));
        }

        [TestCase(CharacterBonusKind.Regeneration, 1f, 5f, "每 5 秒 +1", "+1 every 5 s")]
        [TestCase(CharacterBonusKind.Sprint, .2f, 0f, "+0.2 秒", "+0.2 s")]
        [TestCase(CharacterBonusKind.PickupRange, 1.5f, 0f, "+1.5 格", "+1.5 cells")]
        [TestCase(CharacterBonusKind.StatusFloor, 50f, 0f, "每秒至少 50", "at least 50 a second")]
        public void Value_UnitsOfTheirOwn_AreWrittenInEachLanguage(CharacterBonusKind kind, float amount, float interval, string chinese, string english)
        {
            var sut = new CharacterBonus(kind, amount, interval);

            Assert.That(sut.Value(true), Is.EqualTo(chinese));
            Assert.That(sut.Value(false), Is.EqualTo(english));
        }

        [TestCase(25.44f, "+25%")]
        [TestCase(4.5f, "+4.5%")]
        [TestCase(-20f, "-20%")]
        [TestCase(-.24f, "-0.2%")]
        public void SignedPercent_WholeFromTenOn_OneDecimalBelow(float value, string expected)
        {
            var actual = CharacterBonus.SignedPercent(value);

            Assert.That(actual, Is.EqualTo(expected));
        }

        // ───── Cards taken ─────

        [Test]
        public void TakenCardOf_EachKind_KeepsWhatNamesIt()
        {
            var weapon = new RunWeapon { form = WeaponForm.Rifle };
            var stat = new DraftCard(DraftCardKind.Stat, "", "", _ => true, payload: new StatCardOffer(StatId.Damage, CardRarity.Rare, .18f), rarity: CardRarity.Rare);
            var upgrade = new DraftCard(DraftCardKind.Weapon, "", "", _ => true, payload: new WeaponUpgradeOffer(weapon, UpgradeAxis.Infusion, ElementKind.Fire));
            var skill = new DraftCard(DraftCardKind.Skill, "", "", _ => true, payload: SkillId.Blink, rarity: CardRarity.Exceptional);

            var takenStat = TakenCard.Of(stat);
            var takenUpgrade = TakenCard.Of(upgrade);
            var takenSkill = TakenCard.Of(skill);

            Assert.That((takenStat.kind, takenStat.id, takenStat.rarity), Is.EqualTo((DraftCardKind.Stat, (int)StatId.Damage, CardRarity.Rare)));
            Assert.That((takenUpgrade.kind, takenUpgrade.id, takenUpgrade.axis, takenUpgrade.element),
                Is.EqualTo((DraftCardKind.Weapon, (int)WeaponForm.Rifle, UpgradeAxis.Infusion, ElementKind.Fire)));
            Assert.That(takenUpgrade.rarity, Is.EqualTo(CardRarity.Epic), "a weapon card's colour is its direction's: Infusion is purple");
            Assert.That((takenSkill.kind, takenSkill.id, takenSkill.rarity), Is.EqualTo((DraftCardKind.Skill, (int)SkillId.Blink, CardRarity.Exceptional)));
        }

        // ───── A weapon's card, to read again ─────

        [Test]
        public void WeaponLines_AnAnomalyNotRevealedYet_SaysNothing()
        {
            var hidden = new RunWeapon { form = WeaponForm.Rifle, variant = 1, revealed = false };

            CharacterSheet.WeaponLines(hidden, out string how, out string trait);

            Assert.That(how, Is.Empty, "how it attacks stays hidden with its anomaly");
            Assert.That(trait, Is.Empty, "and so does its trait");
        }

        [Test]
        public void WeaponLines_ARevealedAnomaly_GivesTheWeaponCardsAttackAndTrait()
        {
            var shown = new RunWeapon { form = WeaponForm.Rifle, variant = 1, revealed = true };

            CharacterSheet.WeaponLines(shown, out string how, out string trait);

            Assert.That(how, Is.EqualTo(WeaponGuide.How(WeaponForm.Rifle, 1)));
            Assert.That(trait, Is.EqualTo(WeaponGuide.Backfire(WeaponForm.Rifle, 1)));
        }

        [Test]
        public void WeaponLines_ThePistol_HasItsAttackAndNoTrait()
        {
            var pistol = new RunWeapon { form = WeaponForm.Pistol, revealed = true };

            CharacterSheet.WeaponLines(pistol, out string how, out string trait);

            Assert.That(how, Is.EqualTo(WeaponGuide.How(WeaponForm.Pistol, 0)));
            Assert.That(trait, Is.Empty, "a plain weapon has no trait, as its weapon card shows none");
        }

        [Test]
        public void TakenCardSame_SameCardAndRarity_IsTheSame_AnotherRarityIsNot()
        {
            var rare = TakenCard.Of(new DraftCard(DraftCardKind.Stat, "", "", _ => true, payload: new StatCardOffer(StatId.Damage, CardRarity.Rare, .18f), rarity: CardRarity.Rare));
            var rareAgain = TakenCard.Of(new DraftCard(DraftCardKind.Stat, "", "", _ => true, payload: new StatCardOffer(StatId.Damage, CardRarity.Rare, .18f), rarity: CardRarity.Rare));
            var common = TakenCard.Of(new DraftCard(DraftCardKind.Stat, "", "", _ => true, payload: new StatCardOffer(StatId.Damage, CardRarity.Common, .12f)));

            Assert.That(rare.Same(rareAgain), Is.True, "the screen counts the two as one line, ×2");
            Assert.That(rare.Same(common), Is.False);
        }
    }
}
