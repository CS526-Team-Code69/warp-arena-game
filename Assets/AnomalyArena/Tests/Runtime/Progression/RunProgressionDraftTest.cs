using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class RunProgressionDraftTest
    {
        ProgressionRules rules;
        readonly List<DraftCard> weaponsReplaced = new(), skillsReplaced = new();
        bool weaponFits;

        [SetUp]
        public void SetUp()
        {
            weaponsReplaced.Clear(); skillsReplaced.Clear();
            weaponFits = true;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        RunProgression CreateSystemUnderTest()
        {
            rules = ScriptableObject.CreateInstance<ProgressionRules>();
            var sut = new GameObject("Progression").AddComponent<RunProgression>();
            sut.Rules = rules;
            sut.ResetRun();
            return sut;
        }

        DraftCard StubWeaponCard(DraftCard replacing)
        {
            weaponsReplaced.Add(replacing);
            return new DraftCard(DraftCardKind.Weapon, "weapon", "", _ => true, stillFits: () => weaponFits);
        }

        DraftCard StubSkillCard(DraftCard replacing)
        {
            skillsReplaced.Add(replacing);
            return new DraftCard(DraftCardKind.Skill, "skill", "", _ => true);
        }

        RunProgression CreateAtMaxLevel(bool bankStatDraft)
        {
            var sut = CreateSystemUnderTest();
            sut.GainXp(rules.levelCosts.Sum());
            while (sut.BankedDrafts > 0) sut.ConsumeDraft();
            if (bankStatDraft) sut.GainXp(rules.xpPerStatDraft);
            return sut;
        }

        static StatId StatOf(DraftCard card) => ((StatCardOffer)card.Payload).Stat;

        [Test, CreateScene]
        public void GrantVoidLevel_BelowMaxLevel_GrowsLikeALevelUpButKeepsXpAndLevel()
        {
            var sut = CreateSystemUnderTest();
            sut.UnlockEnergy();
            sut.GainXp(rules.levelCosts[0] - 1f);
            sut.TrySpendEnergy(50f);
            float hp = sut.Stats.maxHp, energy = sut.Stats.maxEnergy, xp = sut.Xp, healed = 0f;
            int required = sut.XpRequired;
            sut.HealingRequested += amount => healed += amount;

            sut.GrantVoidLevel();

            Assert.That(sut.Level, Is.EqualTo(1), "level number");
            Assert.That(sut.Xp, Is.EqualTo(xp), "XP");
            Assert.That(sut.XpRequired, Is.EqualTo(required), "XP needed for the next level");
            Assert.That(sut.BankedDrafts, Is.EqualTo(1), "one card pick");
            Assert.That(sut.NextDraftIsStatOnly, Is.False, "a stat, weapon and skill draft below max level");
            Assert.That(sut.Stats.maxHp, Is.EqualTo(hp + rules.maxHpPerLevel).Within(.0001f), "max HP grows by a level's amount");
            Assert.That(healed, Is.GreaterThanOrEqualTo(sut.Stats.maxHp), "a full heal is requested, like a level-up (the body clamps it to max HP)");
            Assert.That(sut.Stats.maxEnergy, Is.EqualTo(energy + rules.maxEnergyPerLevel).Within(.0001f), "max energy grows by a level's amount");
            Assert.That(sut.Energy, Is.EqualTo(sut.Stats.maxEnergy), "energy refills");
            Assert.That(sut.VoidBadge, Is.True, "the level reads in void purple");
        }

        [Test, CreateScene]
        public void GrantVoidLevel_AtMaxLevel_BanksAStatOnlyDraft()
        {
            var sut = CreateAtMaxLevel(bankStatDraft: false);

            sut.GrantVoidLevel();
            var actual = sut.DrawCards(12);

            Assert.That(sut.Level, Is.EqualTo(rules.maxLevel), "the level number stays at the cap");
            Assert.That(sut.NextDraftIsStatOnly, Is.True, "the max-level rule applies to the extra pick");
            Assert.That(actual, Has.All.Matches<DraftCard>(card => card.Kind == DraftCardKind.Stat), "three stat cards");
        }

        [Test, CreateScene]
        public void VoidBadge_VoidEnergyWithoutTheTrialLevel_ReturnsFalse()
        {
            var sut = CreateAtMaxLevel(bankStatDraft: false);

            sut.CompleteVoidTrial();

            Assert.That(sut.VoidBadge, Is.False);
        }

        [Test, CreateScene]
        public void Reroll_ThreeKindDraft_HandsTheOldWeaponAndSkillCardsToTheirFactories()
        {
            var sut = CreateSystemUnderTest();
            sut.GainXp(rules.levelCosts[0]);
            sut.BeginWave();
            var before = sut.DrawCards(1, StubWeaponCard, StubSkillCard).ToArray();

            sut.Reroll();

            Assert.That(weaponsReplaced.Last(), Is.SameAs(before[1]), "the weapon factory is told which card it replaces");
            Assert.That(skillsReplaced.Last(), Is.SameAs(before[2]), "the skill factory is told which card it replaces");
            Assert.That(sut.CurrentCards.Intersect(before), Is.Empty, "all three cards are replaced");
        }

        [Test, CreateScene]
        public void Reroll_ThreeKindDraft_NeverRepeatsTheStatCard()
        {
            var sut = CreateSystemUnderTest();
            sut.GainXp(rules.levelCosts[0]);
            sut.DrawCards(1, StubWeaponCard, StubSkillCard);
            var repeated = new List<StatId>();

            for (int i = 0; i < 30; i++)
            {
                sut.BeginWave();
                StatId before = StatOf(sut.CurrentCards[0]);
                sut.Reroll();
                if (StatOf(sut.CurrentCards[0]) == before) repeated.Add(before);
            }

            Assert.That(repeated, Is.Empty);
        }

        [Test, CreateScene]
        public void Reroll_StatOnlyDraft_ShowsThreeStatsTheOldDraftDidNotShow()
        {
            var sut = CreateAtMaxLevel(bankStatDraft: true);
            sut.DrawCards(12);
            var overlaps = new List<string>();

            for (int i = 0; i < 20; i++)
            {
                sut.BeginWave();
                var before = sut.CurrentCards.Select(StatOf).ToArray();
                sut.Reroll();
                var after = sut.CurrentCards.Select(StatOf).ToArray();
                if (before.Intersect(after).Any() || after.Distinct().Count() != 3)
                    overlaps.Add(string.Join(",", before) + " -> " + string.Join(",", after));
            }

            Assert.That(overlaps, Is.Empty);
        }

        [Test, CreateScene]
        public void Reroll_SpentDuringTheWave_IsRefusedInItsBuildPhase()
        {
            var sut = CreateSystemUnderTest();
            sut.GainXp(rules.levelCosts[0] + rules.levelCosts[1]);
            sut.BeginWave();
            sut.DrawCards(1, StubWeaponCard, StubSkillCard);
            sut.Reroll();
            sut.BeginBuildPhase(1, StubWeaponCard, StubSkillCard);
            sut.DrawCards(1);

            var actual = sut.Reroll();

            Assert.That(actual, Is.False);
        }

        [Test, CreateScene]
        public void DrawCards_DraftPutOffIntoTheBuildPhase_ComesBackWithTheSameCards()
        {
            var sut = CreateSystemUnderTest();
            sut.GainXp(rules.levelCosts[0]);
            var shown = sut.DrawCards(1, StubWeaponCard, StubSkillCard).ToArray();
            sut.BeginBuildPhase(1, StubWeaponCard, StubSkillCard);

            var actual = sut.DrawCards(1);

            Assert.That(actual, Is.EqualTo(shown));
        }

        [Test, CreateScene]
        public void DrawCards_DraftPutOffIntoTheBuildPhase_CountsItsStatCardOnceTowardThePity()
        {
            var sut = CreateSystemUnderTest();
            rules.highTierPercent = 0f;
            sut.GainXp(rules.levelCosts[0]);
            sut.DrawCards(9, StubWeaponCard, StubSkillCard);
            sut.BeginBuildPhase(9, StubWeaponCard, StubSkillCard);
            sut.DrawCards(9);
            sut.RollStatCard(); sut.RollStatCard();

            var actual = sut.RollStatCard();

            Assert.That(actual.Rarity, Is.EqualTo(CardRarity.Exceptional));
        }

        [Test, CreateScene]
        public void DrawCards_PutOffDraftWithAnOutgrownCard_DrawsOnlyThatCardAgain()
        {
            var sut = CreateSystemUnderTest();
            sut.GainXp(rules.levelCosts[0]);
            var shown = sut.DrawCards(1, StubWeaponCard, StubSkillCard).ToArray();
            weaponFits = false;

            var actual = sut.DrawCards(1);

            Assert.That(actual[1], Is.Not.SameAs(shown[1]), "the weapon card whose weapon changed is drawn anew");
            Assert.That(weaponsReplaced.Last(), Is.SameAs(shown[1]), "its factory is told which card it replaces");
            Assert.That(new[] { actual[0], actual[2] }, Is.EqualTo(new[] { shown[0], shown[2] }), "the other cards stay");
        }
    }
}
