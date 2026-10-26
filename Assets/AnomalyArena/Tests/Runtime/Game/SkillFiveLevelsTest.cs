using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class SkillFiveLevelsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const float TargetHp = 100f;
        UnityEngine.Random.State savedRandom;

        static readonly Dictionary<(SkillId, string), (float first, float last)> OldLevelOneAndThree = new()
        {
            [(SkillId.Blink, "range")] = (6f, 8f),
            [(SkillId.Bulwark, "radius")] = (4f, 6f),
            [(SkillId.Bulwark, "strength")] = (.15f, .4f),
            [(SkillId.Bulwark, "contactKnockback")] = (4f, 8f),
            [(SkillId.Contagion, "duration")] = (8f, 12f),
            [(SkillId.Decoy, "duration")] = (3f, 5f),
            [(SkillId.Devour, "strength")] = (.3f, .5f),
            [(SkillId.FlameWake, "duration")] = (4f, 6f),
            [(SkillId.FlameWake, "radius")] = (.75f, 1.5f),
            [(SkillId.FrostNova, "radius")] = (4f, 5.5f),
            [(SkillId.GravityWell, "duration")] = (3f, 4.5f),
            [(SkillId.Overcharge, "duration")] = (4f, 6f),
            [(SkillId.Radiance, "duration")] = (5f, 7f),
            [(SkillId.Radiance, "statusResistance")] = (.3f, .7f),
            [(SkillId.Shockwave, "radius")] = (4f, 5f),
            [(SkillId.StormCall, "radius")] = (5f, 6f),
            [(SkillId.StormCall, "strength")] = (3f, 6f),
            [(SkillId.TimeSlip, "duration")] = (2.5f, 3.5f),
        };

        [SetUp]
        public void SetUp() => savedRandom = UnityEngine.Random.state;

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Random.state = savedRandom;
            Time.timeScale = 1f;
        }

        [TestCase(4, CardRarity.Epic)]
        [TestCase(5, CardRarity.Legendary)]
        public void ForSkill_LevelsFourAndFive_ArePurpleAndOrange(int level, CardRarity expected)
        {
            var actual = ArenaRarity.ForSkill(null, level);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(4)]
        [TestCase(5)]
        public void ForSkill_VoidSkillAtLevelsFourAndFive_StaysBlack(int level)
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
        public void ScaledDamage_LevelFive_GrowsAsTheOldLevelThreeDidAndNoFurther()
        {
            var sut = ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                float levelOne = sut.ScaledDamage(10f, 1);

                float levelFive = sut.ScaledDamage(10f, SkillDefinition.MaxLevel) / levelOne;
                float levelSix = sut.ScaledDamage(10f, SkillDefinition.MaxLevel + 1) / levelOne;

                Assert.That(levelFive, Is.EqualTo(1.35f * 1.35f).Within(.001f), "level 5 hits as hard as level 3 did (1.35 per level over three)");
                Assert.That(levelSix, Is.EqualTo(levelFive).Within(.0001f), "nothing grows past level 5");
            }
            finally { Object.DestroyImmediate(sut); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task SavedSkills_PerLevelValues_LevelOneAsBeforeLevelFiveAsTheOldLevelThreeEvenlyBetween()
        {
            var game = await ReadyScene();
            var problems = new List<string>();

            foreach (var definition in game.skills.Catalog.skills.Where(d => d))
            {
                foreach (var (name, values) in PerLevelValues(definition))
                {
                    string at = $"{definition.id}.{name}";
                    if (values == null || values.Length != SkillDefinition.MaxLevel) { problems.Add($"{at}: {values?.Length ?? 0} entries"); continue; }
                    if (!OldLevelOneAndThree.TryGetValue((definition.id, name), out var old)) old = (values[0], values[0]);
                    if (!Near(values[0], old.first) || !Near(values[^1], old.last))
                        problems.Add($"{at}: {values[0]} to {values[^1]}, expected {old.first} to {old.last}");
                    for (int level = 2; level < SkillDefinition.MaxLevel; level++)
                    {
                        float value = values[level - 1], even = Mathf.Lerp(old.first, old.last, (level - 1) / (float)(SkillDefinition.MaxLevel - 1));
                        bool fits = (definition.id, name) switch
                        {

                            (SkillId.FlameWake, "radius") => Near(value, old.first),

                            (SkillId.StormCall, "strength") => Near(value, Mathf.Round(value)) && Mathf.Abs(value - even) <= .5f + 1e-4f
                                && value >= values[level - 2],
                            _ => Near(value, even)
                        };
                        if (!fits) problems.Add($"{at} level {level}: {value}, evenly {even}");
                    }
                }
                if (!Near(Mathf.Pow(definition.levelDamageGrowth, SkillDefinition.MaxLevel - 1), 1.35f * 1.35f))
                    problems.Add($"{definition.id}: damage growth {definition.levelDamageGrowth} per level");
            }

            Assert.That(problems, Is.Empty);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MaxLevelEffects_LevelFourHasNoneOfThem_LevelFiveHasThem()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            skills.ResetRun();
            var devour = skills.Catalog.Find(SkillId.Devour);
            var overcharge = skills.Catalog.Find(SkillId.Overcharge);
            Assume.That(skills.Learn(SkillId.Blink, -1, 4) && skills.Learn(SkillId.Overcharge, -1, 4), Is.True, "Blink and Overcharge at level 4");
            int blink = SlotOf(skills, SkillId.Blink), overchargeSlot = SlotOf(skills, SkillId.Overcharge);
            Assume.That(skills.TryCast(overchargeSlot), Is.True, "Overcharge at level 4 is cast");
            int blinkChargesAtFour = skills.BlinkChargesMax;
            float weaponDamageAtFour = skills.SkillDamageMultiplier;
            var slotColourAtFour = game.UiSnapshot.skillSlots[blink].rarity;

            skills.ClearWaveEffects();
            skills.Learn(SkillId.Blink);
            skills.Learn(SkillId.Overcharge);
            skills.Slots[overchargeSlot].CooldownLeft = 0f;
            Assume.That(skills.TryCast(overchargeSlot), Is.True, "Overcharge at level 5 is cast");

            Assert.That(blinkChargesAtFour, Is.EqualTo(1), "Blink at level 4: one charge");
            Assert.That(skills.BlinkChargesMax, Is.EqualTo(2), "Blink at level 5: two charges");
            Assert.That(devour.Cooldown(4), Is.EqualTo(devour.cooldown), "Devour at level 4: the normal cooldown");
            Assert.That(devour.Cooldown(5), Is.EqualTo(devour.advancedCooldown), "Devour at level 5: the shorter cooldown");
            Assert.That(weaponDamageAtFour, Is.EqualTo(1f), "Overcharge at level 4 adds no weapon damage");
            Assert.That(skills.SkillDamageMultiplier, Is.EqualTo(overcharge.overchargeDamageMultiplier), "Overcharge at level 5 adds weapon damage");
            Assert.That(slotColourAtFour, Is.EqualTo(CardRarity.Epic), "a level-4 skill slot is purple");
            Assert.That(game.UiSnapshot.skillSlots[blink].rarity, Is.EqualTo(CardRarity.Legendary), "a level-5 skill slot is orange");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task SkillTexts_BothLanguages_MaxLevelClauseOnlyAtFiveAndBulwarkAndSingularityFollowTheNewRules()
        {
            var game = await ReadyScene();
            var catalog = game.skills.Catalog;
            var ordinary = catalog.skills.Where(d => d && !d.greaterVoid).ToArray();
            var bulwark = catalog.Find(SkillId.Bulwark);
            var singularity = catalog.Find(SkillId.Singularity);
            var texts = new Dictionary<string, (List<SkillId> clauseAtFour, List<SkillId> noClauseAtFive, string bulwark, string singularity)>();
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                foreach (var (code, clause) in new[] { ("zh-Hans", "at max level"), ("en", "at max level") })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    texts[code] = (ordinary.Where(d => d.Description(4).Contains(clause)).Select(d => d.id).ToList(),
                        ordinary.Where(d => !d.Description(SkillDefinition.MaxLevel).Contains(clause)).Select(d => d.id).ToList(),
                        bulwark.Description(1), singularity.Description(1));
                }
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                Object.Destroy(locale.gameObject);
            }

            Assert.That(texts["zh-Hans"].clauseAtFour, Is.Empty, "Chinese: no max-level clause at level 4");
            Assert.That(texts["zh-Hans"].noClauseAtFive, Is.Empty, "Chinese: every ordinary skill names its max-level effect at level 5");
            Assert.That(texts["en"].clauseAtFour, Is.Empty, "English: no max-level clause at level 4");
            Assert.That(texts["en"].noClauseAtFive, Is.Empty, "English: every ordinary skill names its max-level effect at level 5");
            Assert.That(texts["zh-Hans"].bulwark, Does.Contain("Tyrant King").And.Not.Contain("unbreakable"), "Chinese Bulwark: only the Tyrant King's charge breaks it");
            Assert.That(texts["en"].bulwark, Does.Contain("Tyrant King").And.Not.Contain("unbreakable"), "English Bulwark: only the Tyrant King's charge breaks it");
            Assert.That(texts["zh-Hans"].singularity, Does.Contain("heavily slowed").And.Not.Contain("hold still"), "Chinese Singularity: slowed, not held");
            Assert.That(texts["en"].singularity, Does.Contain("heavily slowed").And.Not.Contain("hold still"), "English Singularity: slowed, not held");
        }

        [Test]
        public void SkillRarity_EveryWaveRollAndPity_IsTheStatCardRoll()
        {
            var sut = ScriptableObject.CreateInstance<ProgressionRules>();
            try
            {
                var mismatches = new List<string>();

                for (int wave = 0; wave <= 16; wave++)
                foreach (float roll in new[] { 0f, 24.99f, 25f, 99.99f })
                foreach (bool pity in new[] { false, true })
                    if (sut.SkillRarity(wave, roll, pity) != sut.StatRarity(wave, roll, pity))
                        mismatches.Add($"wave {wave}, roll {roll}, pity {pity}");

                Assert.That(mismatches, Is.Empty);
            }
            finally { Object.DestroyImmediate(sut); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollCard_EachWaveBand_NewSkillCardsShowOnlyItsTwoColoursMostlyTheLower()
        {
            var skills = (await ReadyScene()).skills;
            UnityEngine.Random.InitState(1026);
            var problems = new List<string>();

            foreach (int wave in new[] { 1, 5, 10, 13 })
            {
                skills.ResetRun();
                ProgressionRules.RarityBand(wave, out var low, out var high);
                int lows = 0, highs = 0;
                for (int i = 0; i < 200; i++)
                {
                    var card = skills.RollCard(wave);

                    if (card.Payload is SkillId.Devour) continue;
                    if (card.Rarity == low) lows++;
                    else if (card.Rarity == high) highs++;
                    else problems.Add($"wave {wave}: {card.Rarity}");
                }
                if (highs == 0 || lows <= highs) problems.Add($"wave {wave}: {lows} {low} against {highs} {high}");
            }

            Assert.That(problems, Is.Empty);
        }

        [TestCase(0f, CardRarity.Epic, 4), TestCase(100f, CardRarity.Legendary, 5), LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollCard_WaveTwelve_NewSkillCardIsPurpleLevelFourOrOrangeLevelFive(float highTierPercent, CardRarity colour, int level)
        {
            var game = await ReadyScene();
            var skills = game.skills;
            skills.ResetRun();
            UnityEngine.Random.InitState(1026);
            var original = game.progression.Rules;
            var rules = Object.Instantiate(original);
            try
            {

                rules.highTierPercent = highTierPercent;
                game.progression.Rules = rules;
                DraftCard card = null;
                for (int i = 0; i < 50 && card == null; i++)
                {
                    var drawn = skills.RollCard(12);
                    if (drawn.Payload is not SkillId.Devour) card = drawn;
                }
                Assume.That(card, Is.Not.Null, "a card other than the always black Devour comes up");

                bool applied = card.Apply();

                Assert.That(card.Rarity, Is.EqualTo(colour), "the card's colour is the level it gives");
                Assert.That(card.Title, Does.Contain(level.ToString()), "the title names the level");
                Assert.That(applied, Is.True, "the card applies");
                Assert.That(skills.Slots.First(s => !s.Empty).Level, Is.EqualTo(level), "the new skill starts at that level");
            }
            finally { game.progression.Rules = original; Object.Destroy(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollCard_FourSkillCardsBelowTheHighTier_NextIsTheHighTier()
        {
            var game = await ReadyScene();
            var skills = game.skills;
            skills.ResetRun();
            var original = game.progression.Rules;
            var rules = Object.Instantiate(original);
            try
            {

                rules.highTierPercent = 0f;
                rules.highTierPity = 4;
                game.progression.Rules = rules;

                var actual = Enumerable.Range(0, 6).Select(_ => skills.RollCard(3).Rarity).ToArray();

                Assert.That(actual, Is.EqualTo(new[] { CardRarity.Rare, CardRarity.Rare, CardRarity.Rare, CardRarity.Rare, CardRarity.Exceptional, CardRarity.Rare }));
            }
            finally { game.progression.Rules = original; Object.Destroy(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollCard_UpgradeCarriedAboveTheBand_CountsAsAHighCardForThePity()
        {
            var game = await ReadyScene();
            var skills = game.skills;
            skills.ResetRun();
            var originalRules = game.progression.Rules;
            var originalCatalog = skills.Catalog;
            var rules = Object.Instantiate(originalRules);
            var frostOnly = ScriptableObject.CreateInstance<SkillCatalog>();
            var flameOnly = ScriptableObject.CreateInstance<SkillCatalog>();
            try
            {

                rules.highTierPercent = 0f;
                rules.highTierPity = 2;
                game.progression.Rules = rules;
                frostOnly.skills = new[] { originalCatalog.Find(SkillId.FrostNova) };
                flameOnly.skills = new[] { originalCatalog.Find(SkillId.FlameWake) };
                skills.Catalog = flameOnly;
                Assume.That(skills.Learn(SkillId.FlameWake, -1, 4), Is.True, "Flame Wake at level 4");

                skills.Catalog = frostOnly;
                var first = skills.RollCard(3, SkillId.FlameWake);
                skills.Catalog = flameOnly;
                var upgrade = skills.RollCard(3);
                skills.Catalog = frostOnly;

                var actual = Enumerable.Range(0, 3).Select(_ => skills.RollCard(3, SkillId.FlameWake).Rarity).ToArray();

                Assume.That(first.Rarity, Is.EqualTo(CardRarity.Rare), "a green Frost Nova card: one card below blue");
                Assume.That(upgrade.Rarity, Is.EqualTo(CardRarity.Legendary), "the level-4 Flame Wake's upgrade card gives level 5, orange");
                Assert.That(actual, Is.EqualTo(new[] { CardRarity.Rare, CardRarity.Rare, CardRarity.Exceptional }));
            }
            finally
            {
                game.progression.Rules = originalRules;
                skills.Catalog = originalCatalog;
                Object.Destroy(rules);
                Object.Destroy(frostOnly);
                Object.Destroy(flameOnly);
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task GravityWell_LevelFourThenFive_OnlyLevelFiveBlastsAndItCountsTheDamageCardOnce()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var target = LoneTarget(game, targets);
            var skills = game.skills;
            skills.ResetRun();
            var definition = skills.Catalog.Find(SkillId.GravityWell);
            Assume.That(skills.Learn(SkillId.GravityWell, -1, 4), Is.True, "Gravity Well at level 4");
            game.player.DebugSetAimPoint(target.Position);
            Assume.That(skills.TryCast(0), Is.True, "Gravity Well at level 4 is cast");
            await UntilGravityWellEnds(skills);
            float hpAfterLevelFour = target.Hp;
            skills.Learn(SkillId.GravityWell);
            skills.Slots[0].CooldownLeft = 0f;

            Assume.That(skills.TryCast(0), Is.True, "Gravity Well at level 5 is cast");
            await UntilGravityWellEnds(skills);

            float blast = Mathf.Round(definition.ScaledDamage(definition.endDamage, SkillDefinition.MaxLevel, 1.5f));
            Assert.That(hpAfterLevelFour, Is.EqualTo(TargetHp), "level 4 ends without a blast");
            Assert.That(target.Hp, Is.EqualTo(TargetHp - blast).Within(.01f), "the level-5 blast counts the Damage card once");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SingularityBlast_DamageCard_CountsOnce()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var target = LoneTarget(game, targets);
            var skills = game.skills;
            skills.ResetRun();
            var definition = skills.Catalog.Find(SkillId.Singularity);
            Assume.That(skills.Learn(SkillId.Singularity), Is.True, "Singularity is learned");
            await Until(() => SkillWorldAPI.IsOnScreen == null || SkillWorldAPI.IsOnScreen(target.Position), 4f, "the camera shows the target");
            Assume.That(skills.TryCast(0), Is.True, "Singularity is cast");
            int absorbed = 0;

            await Until(() =>
            {
                if (skills.SingularityActive) absorbed = skills.SingularityAbsorbedCount;
                return !skills.SingularityActive;
            }, 6f, "the singularity blasts");

            float blast = Mathf.Round(definition.ScaledDamage(definition.singularityBaseDamage + absorbed * definition.singularityDamagePerEnemy, 1, 1.5f));
            Assume.That(absorbed, Is.EqualTo(1), "it drew the target in");
            Assert.That(target.Hp, Is.EqualTo(TargetHp - blast).Within(.01f));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Singularity_WhileItGathers_SlowsThePlayerWhoMayStillFireAndCast()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            skills.ResetRun();
            var definition = skills.Catalog.Find(SkillId.Singularity);
            Assume.That(skills.Learn(SkillId.Singularity) && skills.Learn(SkillId.Overcharge), Is.True, "Singularity and Overcharge are learned");
            game.runWeapons.ClearWaveEffects();
            var knife = new RunWeapon { form = WeaponForm.Knife, variant = 0, revealed = true };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = knife;
            float before = skills.PlayerMoveMultiplier;
            Assume.That(skills.TryCast(0), Is.True, "Singularity is cast");

            float gathering = skills.PlayerMoveMultiplier;
            bool rooted = game.player.ActionsBlocked || game.player.MovementLocked;
            bool cast = skills.TryCast(1);
            game.runWeapons.HandleInput(true, true, false);
            bool fired = knife.inFlight;
            bool stillGathering = skills.SingularityActive;
            await Until(() => !skills.SingularityActive, 6f, "the singularity blasts");

            Assert.That(stillGathering, Is.True, "all of it happened while the singularity gathered");
            Assert.That(gathering, Is.EqualTo(before * definition.singularityPlayerMoveMultiplier).Within(1e-4f), "the player moves heavily slowed");
            Assert.That(rooted, Is.False, "the player is not rooted");
            Assert.That(cast, Is.True, "another skill casts meanwhile");
            Assert.That(fired, Is.True, "the hand weapon fires meanwhile");
            Assert.That(skills.PlayerMoveMultiplier, Is.EqualTo(before).Within(1e-4f), "full speed again after the blast");
        }

        static IEnumerable<(string, float[])> PerLevelValues(SkillDefinition definition)
        {
            yield return ("duration", definition.duration);
            yield return ("radius", definition.radius);
            yield return ("range", definition.range);
            yield return ("strength", definition.strength);
            yield return ("contactKnockback", definition.contactKnockback);
            yield return ("statusResistance", definition.statusResistance);
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) <= 1e-3f;

        static int SlotOf(SkillLoadout skills, SkillId id) => Array.FindIndex(skills.Slots, s => !s.Empty && s.Id == id);

        static Enemy LoneTarget(GameManager game, Enemy[] targets)
        {
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var board = ElementStatus.For(targets[0]);
            board.SetShield(0f);
            board.ClearStatuses();
            game.runWeapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
            game.progression.Stats.damageBonus = .5f;
            return targets[0];
        }

        static async Task UntilGravityWellEnds(SkillLoadout skills)
        {
            var ground = new List<SkillGroundVisual>();
            await Until(() =>
            {
                skills.CopyGroundVisuals(ground);
                return ground.All(visual => visual.Id != SkillId.GravityWell);
            }, 8f, "the gravity well ends");
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "saved scene and localization initialize");
            var game = GameManager.Instance;
            Assume.That(game.player && game.progression && game.skills, Is.True, "V2 scene references exist");
            return game;
        }

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
