using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
                                                                                                                                           
                                                                                                                                                                                           
                                                                                                                                         
                                                                                                
    [TestFixture]
    public class CardsArmoryWeaponsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const string SceneEn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

                                                  

        [TestCase(UpgradeAxis.Tame, CardRarity.Rare)]
        [TestCase(UpgradeAxis.Twist, CardRarity.Exceptional)]
        [TestCase(UpgradeAxis.Infusion, CardRarity.Epic)]
        public void ForWeaponCard_Direction_ShowsTheDirectionsColour(UpgradeAxis axis, CardRarity expected)
        {
            var actual = ArenaRarity.ForWeaponCard(axis);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void DraftCard_TameCardOfAWeaponWithThreeUpgrades_IsGreenNotTheWeaponsColour()
        {
            var weapon = new RunWeapon { form = WeaponForm.Rifle, element = ElementKind.Fire, axes = UpgradeAxis.Infusion | UpgradeAxis.Twist | UpgradeAxis.Power };
            var card = new DraftCard(DraftCardKind.Weapon, "Tame", "", _ => true, payload: new WeaponUpgradeOffer(weapon, UpgradeAxis.Tame, ElementKind.Fire));

            var actual = card.Rarity;

            Assert.That(actual, Is.EqualTo(CardRarity.Rare));
        }

        [TestCase(CardRarity.Common, 1)]
        [TestCase(CardRarity.Rare, 2)]
        [TestCase(CardRarity.Exceptional, 3)]
        [TestCase(CardRarity.Legendary, 3)]
        [TestCase(CardRarity.Void, 1)]
        public void SkillLevel_CardColour_GivesLevelOneToThree(CardRarity rarity, int expected)
        {
            var actual = ArenaRarity.SkillLevel(rarity);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(1, 10f, CardRarity.Rare)]
        [TestCase(2, 30f, CardRarity.Common)]
        [TestCase(3, 5f, CardRarity.Exceptional)]
        [TestCase(8, 25f, CardRarity.Rare)]
        [TestCase(8, 50f, CardRarity.Common)]
        [TestCase(0, 10f, CardRarity.Rare)]
        public void SkillRarity_WaveBandRows_RollWhiteGreenOrBlue(int wave, float roll, CardRarity expected)
        {
            var rules = ScriptableObject.CreateInstance<ProgressionRules>();
            try
            {
                rules.skillRarityBands = new[] { new SkillRarityBand(1, 20f, 0f), new SkillRarityBand(3, 30f, 10f) };

                var actual = rules.SkillRarity(wave, roll);

                Assert.That(actual, Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [TestCase(1, UpgradeAxis.Tame, 50f)]
        [TestCase(4, UpgradeAxis.Twist, 10f)]
        [TestCase(12, UpgradeAxis.Infusion, 70f)]
        [TestCase(12, UpgradeAxis.Power, 0f)]
        public void DirectionWeight_WaveBandRows_ReadTheRowOfTheWave(int wave, UpgradeAxis axis, float expected)
        {
            var rules = ScriptableObject.CreateInstance<ProgressionRules>();
            try
            {
                rules.weaponDirectionBands = new[] { new WeaponDirectionBand(1, 50f, 30f, 20f), new WeaponDirectionBand(4, 60f, 10f, 30f), new WeaponDirectionBand(10, 10f, 20f, 70f) };

                var actual = rules.DirectionWeight(wave, axis);

                Assert.That(actual, Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [TestCase(1, 0f)]
        [TestCase(3, 0f)]
        [TestCase(4, .2f)]
        [TestCase(9, .4f)]
        [TestCase(15, .4f)]
        public void MkBonusChance_WaveBand_ReadsTheRowOfItsBand(int wave, float expected)
        {
            var roster = ScriptableObject.CreateInstance<WeaponRoster>();
            try
            {
                roster.mkWaveSpacing = 3;
                roster.mkBonusChance = new[] { 0f, .2f, .4f };

                var actual = roster.MkBonusChance(wave);

                Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
            }
            finally { Object.DestroyImmediate(roster); }
        }

                                  

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task Learn_CardLevel_NewSkillStartsThereAndAnOwnedOneRisesAtLeastOneLevel()
        {
            var skills = (await ReadyScene()).skills;
            skills.ResetRun();

            bool learned = skills.Learn(SkillId.FrostNova, -1, 3);
            skills.Learn(SkillId.Blink);
            skills.Learn(SkillId.Blink, -1, 2);
            int afterGreen = Level(skills, SkillId.Blink);
            skills.Learn(SkillId.Blink, -1, 2);
            bool aboveThree = skills.Learn(SkillId.Blink, -1, 3);
            skills.Learn(SkillId.PocketVoid, -1, 3);

            Assert.That(learned && Level(skills, SkillId.FrostNova) == 3, Is.True, "a blue card puts a new skill straight at level 3");
            Assert.That(afterGreen, Is.EqualTo(2), "a green card raises a level-1 skill to 2");
            Assert.That(Level(skills, SkillId.Blink), Is.EqualTo(3), "a green card on a level-2 skill still adds one level");
            Assert.That(aboveThree, Is.False, "nothing goes above level 3");
            Assert.That(Level(skills, SkillId.PocketVoid), Is.EqualTo(1), "greater void skills have no levels");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollCard_AllBlueBand_NewSkillCardIsBlueAndLearnsAtLevelThree()
        {
            var game = await ReadyScene();
            var skills = game.skills;
            skills.ResetRun();
            var original = game.progression.Rules;
            var rules = Object.Instantiate(original);
            try
            {
                rules.skillRarityBands = new[] { new SkillRarityBand(1, 0f, 100f) };
                game.progression.Rules = rules;
                var card = skills.RollCard(1);
                var definition = skills.Catalog.Find((SkillId)card.Payload);

                bool applied = card.Apply();

                Assert.That(card.Rarity, Is.EqualTo(ArenaRarity.ForSkill(definition, 3)), "the card shows the level it gives");
                Assert.That(card.Title, Does.Contain("3"), "the title names the level");
                Assert.That(applied, Is.True, "the card applies");
                Assert.That(Level(skills, definition.id), Is.EqualTo(3), "the new skill starts at level 3");
            }
            finally { game.progression.Rules = original; Object.Destroy(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollCard_AllBlueBandOnAnOwnedLevelOneSkill_UpgradeCardJumpsToLevelThree()
        {
            var game = await ReadyScene();
            var skills = game.skills;
            skills.ResetRun();
            skills.Learn(SkillId.FlameWake);
            var original = game.progression.Rules;
            var rules = Object.Instantiate(original);
            try
            {
                rules.skillRarityBands = new[] { new SkillRarityBand(1, 0f, 100f) };
                game.progression.Rules = rules;
                DraftCard upgrade = null;
                for (int i = 0; i < 100 && upgrade == null; i++)
                {
                    var card = skills.RollCard(1);
                    if (card.Payload is SkillId id && id == SkillId.FlameWake) upgrade = card;
                }
                Assume.That(upgrade, Is.Not.Null, "an upgrade card of the owned skill comes up");

                upgrade.Apply();

                Assert.That(upgrade.Rarity, Is.EqualTo(CardRarity.Exceptional), "the card is blue: the level it gives");
                Assert.That(Level(skills, SkillId.FlameWake), Is.EqualTo(3), "level 1 straight to 3");
            }
            finally { game.progression.Rules = original; Object.Destroy(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task HudHelp_DraftAndReplaceSkill_SayTheCardGivesTheLevel()
        {
            await ReadyScene();
            var hud = Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            var screens = hud ? hud.screens : null;
            var draft = screens?.FirstOrDefault(s => s && s.state == ArenaUiState.Draft);
            var replace = screens?.FirstOrDefault(s => s && s.state == ArenaUiState.ReplaceSkill);
            var render = typeof(V2Hud).GetMethod("RenderScreen", BindingFlags.Instance | BindingFlags.NonPublic);
            Assume.That(draft && draft.body && replace && replace.body && render != null, Is.True, "the saved scene has both screens");
            var texts = new Dictionary<string, (string draft, string replace)>();
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                foreach (var code in new[] { "zh-Hans", "en" })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    render.Invoke(hud, new object[] { draft, new ArenaUiSnapshot { state = ArenaUiState.Draft } });
                    render.Invoke(hud, new object[] { replace, new ArenaUiSnapshot { state = ArenaUiState.ReplaceSkill } });
                    texts[code] = (draft.body.text, replace.body.text);
                }
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                Object.Destroy(locale.gameObject);
            }

                                                                                                                                    
            Assert.That(texts["zh-Hans"].draft, Does.Contain("its colour shows the level it gives").And.Not.Contain("one level for a skill"), "Chinese draft help");
            Assert.That(texts["zh-Hans"].replace, Does.Contain("the level this card gives").And.Not.Contain("starts at level 1"), "Chinese replace-skill help");
            Assert.That(texts["en"].draft, Does.Contain("its colour shows the level it gives").And.Not.Contain("one level for a skill"), "English draft help");
            Assert.That(texts["en"].replace, Does.Contain("the level this card gives").And.Not.Contain("starts at level 1"), "English replace-skill help");
        }

                                   

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollUpgradeCard_WaveBandWeights_PickTheDirectionOfThatWave()
        {
            var game = await ReadyScene();
            var weapons = game.runWeapons;
            var original = game.progression.Rules;
            var rules = Object.Instantiate(original);
            try
            {
                                                                                                                  
                rules.weaponDirectionBands = new[] { new WeaponDirectionBand(1, 100f, 0f, 0f), new WeaponDirectionBand(5, 0f, 0f, 100f) };
                game.progression.Rules = rules;
                weapons.Hand = new[] { new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true }, null };
                weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
                var early = new List<UpgradeAxis>();
                var late = new List<UpgradeAxis>();

                for (int i = 0; i < 20; i++)
                {
                    early.Add(((WeaponUpgradeOffer)weapons.RollUpgradeCard(null, 2).Payload).Axis);
                    late.Add(((WeaponUpgradeOffer)weapons.RollUpgradeCard(null, 7).Payload).Axis);
                }

                Assert.That(early, Has.All.EqualTo(UpgradeAxis.Tame), "an early wave draws its weighted direction");
                Assert.That(late, Has.All.EqualTo(UpgradeAxis.Infusion), "a later wave draws its own");
            }
            finally { game.progression.Rules = original; Object.Destroy(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollUpgradeCard_OnlyOpenDirectionWeighsNothing_IsStillOffered()
        {
            var game = await ReadyScene();
            var weapons = game.runWeapons;
            var original = game.progression.Rules;
            var rules = Object.Instantiate(original);
            try
            {
                rules.weaponDirectionBands = new[] { new WeaponDirectionBand(1, 0f, 100f, 100f) };
                game.progression.Rules = rules;
                                                                                                                                
                weapons.Hand = new[] { new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, axes = UpgradeAxis.Infusion | UpgradeAxis.Twist }, null };
                weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];

                var card = weapons.RollUpgradeCard(null, 3);

                Assert.That(((WeaponUpgradeOffer)card.Payload).Axis, Is.EqualTo(UpgradeAxis.Tame));
            }
            finally { game.progression.Rules = original; Object.Destroy(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollUpgradeCard_VoidInfusion_CardIsPurpleAndTheSlotTurnsBlackOnceTaken()
        {
            var game = await ReadyScene();
            var weapons = game.runWeapons;
            var original = game.progression.Rules;
            var rules = Object.Instantiate(original);
            try
            {
                                                                                                                                 
                rules.weaponDirectionBands = new[] { new WeaponDirectionBand(1, 0f, 0f, 100f) };
                game.progression.Rules = rules;
                var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true };
                weapons.Hand = new[] { rifle, null };
                weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
                DraftCard voidCard = null;
                for (int i = 0; i < 300 && voidCard == null; i++)
                {
                    var card = weapons.RollUpgradeCard(null, 1);
                    if (card.Payload is WeaponUpgradeOffer offer && offer.Axis == UpgradeAxis.Infusion && offer.Element == ElementKind.Void) voidCard = card;
                }
                Assume.That(voidCard, Is.Not.Null, "a Void infusion card comes up");

                bool applied = voidCard.Apply();

                Assert.That(voidCard.Rarity, Is.EqualTo(CardRarity.Epic), "every Infusion card is purple, a Void one included");
                Assert.That(applied && rifle.element == ElementKind.Void, Is.True, "taking it infuses Void");
                Assert.That(ArenaRarity.ForWeapon(rifle), Is.EqualTo(CardRarity.Void), "the slot of a weapon carrying Void is black");
            }
            finally { game.progression.Rules = original; Object.Destroy(rules); }
        }

                             

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollArmory_StrongerVersionOfAHeldWeapon_IsAFreshWeaponWithAFreshAnomaly()
        {
            var game = await ReadyScene();
            var weapons = game.runWeapons;
            var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, axes = UpgradeAxis.Infusion | UpgradeAxis.Twist, element = ElementKind.Fire };
            HoldFullLoadout(weapons, rifle);
            var versions = new List<RunWeapon>();

            for (int draw = 0; draw < 200; draw++)
            {
                weapons.RollArmory(8);
                versions.AddRange(weapons.Offers.Where(w => w != null && w.upgradeOf == rifle));
            }

            Assert.That(versions, Is.Not.Empty, "the held rifle comes up as a stronger version");
            Assert.That(versions, Has.All.Matches<RunWeapon>(w => w.form == WeaponForm.Rifle && w.mk > rifle.mk), "same form, higher Mk");
            Assert.That(versions, Has.All.Matches<RunWeapon>(w => w.axes == UpgradeAxis.None && w.element == ElementKind.None),
                "none of the held rifle's upgrades or infusion");
            Assert.That(versions, Has.None.Matches<RunWeapon>(w => w.revealed), "its anomaly is revealed only when taken");
            Assert.That(versions.Select(w => w.variant).Distinct().Count(), Is.EqualTo(2), "the anomaly is rolled afresh: either one comes up");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollArmory_MkBonusChance_RaisesNewWeaponsAndVersionsOneTierOnlyWhenItHits()
        {
            var game = await ReadyScene();
            var weapons = game.runWeapons;
            var original = weapons.roster;
            var roster = Object.Instantiate(original);
            try
            {
                weapons.roster = roster;
                var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true };
                HoldFullLoadout(weapons, rifle);
                const int wave = 5;
                int standard = roster.ParMk(wave), version = weapons.UpgradeTargetMk(rifle, wave);

                var plain = DrawOffers(weapons, roster, wave, 0f);
                var raised = DrawOffers(weapons, roster, wave, 1f);

                Assert.That(plain.Where(w => w.upgradeOf == null).Select(w => w.mk), Is.Not.Empty.And.All.EqualTo(standard),
                    "without the bonus a new weapon comes at the wave's standard Mk");
                Assert.That(plain.Where(w => w.upgradeOf != null).Select(w => w.mk), Is.Not.Empty.And.All.EqualTo(version),
                    "and a stronger version one tier above it");
                Assert.That(raised.Where(w => w.upgradeOf == null).Select(w => w.mk), Is.Not.Empty.And.All.EqualTo(Mathf.Min(standard + 1, roster.MaxMk - 1)),
                    "a sure bonus raises a new weapon one tier, never to the top Mk");
                Assert.That(raised.Where(w => w.upgradeOf != null).Select(w => w.mk), Is.Not.Empty.And.All.EqualTo(Mathf.Min(version + 1, roster.MaxMk)),
                    "and a stronger version one tier too");
            }
            finally { weapons.roster = original; Object.Destroy(roster); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task UiSnapshot_StrongerVersionOffer_SaysItReplacesTheHeldWeapon()
        {
            var game = await ReadyScene();
            var weapons = game.runWeapons;
            var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, axes = UpgradeAxis.Twist };
            HoldFullLoadout(weapons, rifle);
            int index = -1;
            for (int draw = 0; draw < 200 && index < 0; draw++)
            {
                weapons.RollArmory(5);
                index = Array.FindIndex(weapons.Offers, w => w != null && w.upgradeOf == rifle);
            }
            Assume.That(index, Is.GreaterThanOrEqualTo(0), "the held rifle comes up as a stronger version");

            var card = game.UiSnapshot.armoryOffers[index];

            Assert.That(card.body, Does.Contain(rifle.Label), "it names the weapon it replaces");
            Assert.That(card.body, Does.Not.Contain("keep"), "it no longer promises to keep the anomaly and upgrades");
            Assert.That(card.rarity, Is.EqualTo(CardRarity.Common), "a fresh weapon has no upgrades to colour it");
        }

                              

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MeltdownTame_HasteRunsOut_WeaponsReturnToTheirOwnPace()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            var tuning = weapons.upgrades;
            float seconds = tuning.meltdownTameHasteSeconds;
            try
            {
                tuning.meltdownTameHasteSeconds = .4f;
                var gun = Equip(game, WeaponForm.Minigun, 1, UpgradeAxis.Tame);
                float cycle = weapons.CycleSeconds(gun);
                gun.heat = 3.95f;
                weapons.HandleInput(true, true, false);
                Assume.That(weapons.MeltdownHaste, Is.GreaterThan(1f), "the meltdown hastes you");
                string detail = weapons.AnomalySlotDetail(gun);

                await Until(() => weapons.MeltdownHaste == 1f, 2f, "the haste runs out");

                Assert.That(detail, Does.Contain($"+{tuning.meltdownTameHaste * 100f:0}%"), "the slot shows the bonus while it lasts");
                Assert.That(weapons.CycleSeconds(gun), Is.EqualTo(cycle).Within(.001f), "back to the gun's own pace");
                Assert.That(weapons.AnomalySlotDetail(gun), Is.Empty, "and the slot text is gone");
            }
            finally { tuning.meltdownTameHasteSeconds = seconds; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task WildfireTwist_YouOnYourOwnFence_GetBurningAtYourRateUntilTamed()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            var status = ElementStatus.For(player);
            Vector3 origin = player.Position;
            V3CombatSmokeTest.Teleport(player, origin + Vector3.forward * 4f);
            Physics.SyncTransforms();
            var wildfire = Equip(game, WeaponForm.Flamethrower, 1, UpgradeAxis.Twist);
                                                                                                                     
            AttackActor older = null;
            foreach (float z in new[] { 1f, 7f })
            {
                var patch = AttackPool.Instance.Acquire();
                patch.Configure(AttackMode.Field, player, wildfire, origin + Vector3.forward * z, Vector3.forward, 1f, 0f, 0f, 0f, 1f, 6f);
                patch.hurtOwner = true; patch.ownerDamage = 0f;
                older ??= patch;
            }
            status.ClearStatuses();

            await Until(() => status.Fire > 0f, 1f, "your own fence sets you burning");
            float untamed = status.Fire;
            wildfire.axes |= UpgradeAxis.Tame;
            status.ClearStatuses();
            await Seconds(.6f);

            Assert.That(untamed, Is.EqualTo(tuning.wildfireFenceSelfFirePerSecond * older.tickInterval).Within(.01f), "untamed: your own rate per patch tick");
            Assert.That(status.Fire, Is.Zero, "tamed: the fence leaves you alone");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LaunchYourself_Flight_SlowsDownButLastsAsLongAsAtTheOldSteadySpeed()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var weapons = game.runWeapons;
            var rocket = Equip(game, WeaponForm.RocketLauncher, 1);
            float range = weapons.roster.Get(WeaponForm.RocketLauncher).rangeB * rocket.Reach;
            float steady = 25f / weapons.StartingFormCycleFactor(WeaponForm.RocketLauncher);
            Assume.That(weapons.upgrades.rocketEndSpeedShare, Is.LessThan(.8f), "the tuned missile does slow down");
            var steps = new List<float>();
            Vector3 last = game.player.Position;

            weapons.HandleInput(true, true, false);
            float start = Time.fixedTime;
            while (game.player.State == CharacterState.Dashing)
            {
                await Awaitable.FixedUpdateAsync();
                steps.Add(Query.Flat(game.player.Position - last).magnitude);
                last = game.player.Position;
            }
            float flight = Time.fixedTime - start;
            var moving = steps.Where(s => s > .0001f).ToList();

            Assume.That(moving.Count, Is.GreaterThanOrEqualTo(4), "the flight spans several physics steps");
                                                                                                                    
            Assert.That(moving[1], Is.GreaterThan(moving[moving.Count - 2] * 1.2f), "the missile slows down over its flight");
            Assert.That(flight, Is.EqualTo(range / steady).Within(3f * Time.fixedDeltaTime), "a full flight lasts as long as at the old steady speed");
        }

                            

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task WeaponNames_Chinese_FollowTheRenames()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && ArenaText.IsChinese, 12f, "the Chinese scene and locale");

            Assert.That(ArenaTerms.WeaponForm(WeaponForm.ArcCoil), Is.EqualTo("Arc Cannon"), "Arc Cannon");
            Assert.That(ArenaTerms.WeaponAnomaly(WeaponForm.ArcCoil, 0), Is.EqualTo("Arc Jump"), "Arc Jump");
            Assert.That(ArenaTerms.WeaponAnomaly(WeaponForm.ArcCoil, 1), Is.EqualTo("Capacitor"), "Capacitor keeps its name");
            Assert.That(ArenaTerms.WeaponForm(WeaponForm.Minigun), Is.EqualTo("Gatling"), "Gatling");
            Assert.That(ArenaTerms.WeaponAnomaly(WeaponForm.Knife, 1), Is.EqualTo("Fling Hook"), "Fling Hook");
        }

        [Test, LoadScene(SceneEn), Category("Integration"), Timeout(30000)]
        public async Task WeaponNames_English_FollowTheRenames()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && !ArenaText.IsChinese, 12f, "the English scene and locale");

            Assert.That(ArenaTerms.WeaponForm(WeaponForm.ArcCoil), Is.EqualTo("Arc Cannon"), "Arc Cannon");
            Assert.That(ArenaTerms.WeaponAnomaly(WeaponForm.ArcCoil, 0), Is.EqualTo("Arc Jump"), "Arc Jump");
            Assert.That(ArenaTerms.WeaponForm(WeaponForm.Minigun), Is.EqualTo("Gatling"), "Gatling");
            Assert.That(ArenaTerms.WeaponAnomaly(WeaponForm.Knife, 1), Is.EqualTo("Fling Hook"), "Fling Hook");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ExpandedCardTexts_BothLanguages_UseOnlyTheNewWeaponNames()
        {
            await ReadyScene();
                                                                                                                                    
            var oldNames = new[] { ("zh-Hans", new[] { "minigun", "Arc Coil", "Chain", "arc coil" }), ("en", new[] { "minigun", "arc coil" }) };
            var stale = new List<string>();
            var spinUpTwist = new Dictionary<string, string>();
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                foreach (var (code, names) in oldNames)
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    for (int form = (int)WeaponForm.GravGun; form <= (int)WeaponForm.SwarmLauncher; form++)
                    for (int variant = 0; variant < 2; variant++)
                    foreach (var axis in new[] { UpgradeAxis.Twist, UpgradeAxis.Tame })
                    {
                        string text = ArenaTerms.ExpandedUpgradeDescription((WeaponForm)form, variant, axis);
                        stale.AddRange(names.Where(n => text.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                            .Select(n => $"{code} {(WeaponForm)form} {variant} {axis}: {n}"));
                    }
                    spinUpTwist[code] = ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Minigun, 0, UpgradeAxis.Twist);
                }
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                Object.Destroy(locale.gameObject);
            }

            Assert.That(stale, Is.Empty, "no expanded weapon card still uses an old name");
            Assert.That(spinUpTwist["zh-Hans"], Does.Contain("Gatling"), "the Chinese Spin-up Twist card names the Gatling");
            Assert.That(spinUpTwist["en"], Does.Contain("Gatling"), "and so does the English one");
        }

                              

        static int Level(SkillLoadout skills, SkillId id) => skills.Slots.First(s => !s.Empty && s.Id == id).Level;

                                                                                                                                   
        static void HoldFullLoadout(RunWeapons weapons, RunWeapon rifle)
        {
            weapons.Hand = new[] { rifle, new RunWeapon { form = WeaponForm.Knife, variant = 1, revealed = true } };
            weapons.Auto = new[] { new RunWeapon { form = WeaponForm.OrbitBlades, revealed = true }, new RunWeapon { form = WeaponForm.ShieldRing, revealed = true } };
        }

        static List<RunWeapon> DrawOffers(RunWeapons weapons, WeaponRoster roster, int wave, float bonusChance)
        {
            roster.mkBonusChance = new[] { bonusChance };
            var offers = new List<RunWeapon>();
            for (int draw = 0; draw < 80; draw++)
            {
                weapons.RollArmory(wave);
                offers.AddRange(weapons.Offers.Where(w => w != null));
            }
            return offers;
        }

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant, UpgradeAxis axes = UpgradeAxis.None)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, axes = axes, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "saved scene and localization initialize");
            var game = GameManager.Instance;
            Assume.That(game.player && game.runWeapons && game.progression && game.skills, Is.True, "V2 scene references exist");
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

        static async Task Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.NextFrameAsync();
        }
    }
}
