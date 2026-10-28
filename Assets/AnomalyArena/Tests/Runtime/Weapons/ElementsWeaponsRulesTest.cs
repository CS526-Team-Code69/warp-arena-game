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
    public class ElementsWeaponsRulesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

                                                               

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task WeaponHit_WeakOrStrong_AddsTheSameStacksAndAnOwnElementItsFactor()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var weak = Bare(targets[0]);
            var strong = Bare(targets[1]);
            var own = Bare(targets[2]);
            var rules = weak.Rules;

            targets[0].ReceiveDamage(Hit(game, 2f, ElementKind.Poison));
            targets[1].ReceiveDamage(Hit(game, 40f, ElementKind.Poison));
            var builtIn = Hit(game, 2f, ElementKind.Poison);
            builtIn.builtInElement = true;
            targets[2].ReceiveDamage(builtIn);

            Assert.That(weak.Poison, Is.EqualTo(rules.stacksPerHit).Within(.001f), "a light hit adds one hit's stacks");
            Assert.That(strong.Poison, Is.EqualTo(rules.stacksPerHit).Within(.001f), "a hit twenty times harder adds no more");
            Assert.That(own.Poison, Is.EqualTo(rules.stacksPerHit * rules.builtInElementStackFactor).Within(.001f),
                "a weapon's own element adds its factor");

            targets[0].ReceiveDamage(Hit(game, 2f, ElementKind.Poison));
            targets[0].ReceiveDamage(Hit(game, 2f, ElementKind.Poison));
            Assert.That(weak.Poison, Is.EqualTo(3f * rules.stacksPerHit).Within(.001f), "every hit counts, so a fast weapon stacks faster");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ContinuousHits_OneSourceOnOneTarget_CountForStacksOncePerInterval()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var board = Bare(targets[0]);
            var beside = Bare(targets[1]);
            float interval = board.Rules.continuousStackInterval;
            Assume.That(interval, Is.GreaterThan(.2f), "an interval long enough to see");
            object spray = new object(), field = new object();
            int counted = 0, countedBeside = 0;
            board.StatusApplied += element => counted += element == ElementKind.Poison ? 1 : 0;
            beside.StatusApplied += element => countedBeside += element == ElementKind.Poison ? 1 : 0;
            float hp = targets[0].Hp;

            for (int tick = 0; tick < 3; tick++) targets[0].ReceiveDamage(Continuous(game, 1f, spray));

            Assert.That(hp - targets[0].Hp, Is.EqualTo(3f).Within(.01f), "every tick still deals its damage");
            Assert.That(counted, Is.EqualTo(1), "but only the first adds stacks");
            targets[0].ReceiveDamage(Continuous(game, 1f, field));
            targets[1].ReceiveDamage(Continuous(game, 1f, spray));
            Assert.That(counted, Is.EqualTo(2), "another source counts on its own");
            Assert.That(countedBeside, Is.EqualTo(1), "and on another target the same source counts too");

            await Seconds(interval + .05f);
            targets[0].ReceiveDamage(Continuous(game, 1f, spray));
            Assert.That(counted, Is.EqualTo(3), "once the interval has passed, the source counts again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FlameStream_HeldOnOneEnemy_HitsEveryTickButStacksAboutOncePerInterval()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];
            var board = Bare(enemy);
            var stream = Equip(game, WeaponForm.Flamethrower, 0);
            Assume.That(game.runWeapons.CycleSeconds(stream), Is.LessThanOrEqualTo(board.Rules.continuousStackInterval * .5f),
                "the stream ticks at least twice per interval");
            int hits = 0, counted = 0;
            enemy.AttackReceived += _ => hits++;
            board.StatusApplied += element => counted += element == ElementKind.Fire ? 1 : 0;

            game.runWeapons.HandleInput(true, true, false);
            float until = Time.time + 2f;
            while (Time.time < until) { await Awaitable.NextFrameAsync(); game.runWeapons.HandleInput(false, true, false); }
            game.runWeapons.HandleInput(false, false, true);

            Assume.That(hits, Is.GreaterThanOrEqualTo(6), "the stream kept hitting");
            Assert.That(counted, Is.InRange(hits / 3, hits / 2 + 1), "only about one tick in two adds Burning");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FirePatch_EnemyStandingInIt_TakesEveryTickButStacksAboutOncePerInterval()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];
            var board = Bare(enemy);
            var wildfire = Equip(game, WeaponForm.Flamethrower, 1);
            int hits = 0, counted = 0;
            enemy.AttackReceived += _ => hits++;
            board.StatusApplied += element => counted += element == ElementKind.Fire ? 1 : 0;

            var patch = AttackPool.Instance.Acquire();
            patch.Configure(AttackMode.Field, game.player, wildfire, enemy.Position, Vector3.forward, 1f, 0f, 0f, 0f, 1.5f, 2.2f);
            Assume.That(patch.tickInterval, Is.LessThanOrEqualTo(board.Rules.continuousStackInterval * .5f),
                "the patch ticks at least twice per interval");
            await Until(() => !patch.gameObject.activeSelf, 4f, "the patch burns out");

            Assume.That(hits, Is.GreaterThanOrEqualTo(6), "the patch kept burning the enemy");
            Assert.That(counted, Is.InRange(hits / 3, hits / 2 + 1), "only about one tick in two adds Burning");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BlizzardSpray_HeldOnOneEnemy_ItsHitStacksAndItsOwnFrostDoseEachComeAboutOncePerInterval()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];
            var board = Bare(enemy);
            board.ControlThresholdFactor = 1000f;                                                   
            var blizzard = Equip(game, WeaponForm.CryoCannon, 1);
            Assume.That(game.runWeapons.CycleSeconds(blizzard), Is.LessThanOrEqualTo(board.Rules.continuousStackInterval * .5f),
                "the spray ticks at least twice per interval");
            int hits = 0, frost = 0;
            enemy.AttackReceived += _ => hits++;
            board.StatusApplied += element => frost += element == ElementKind.Frost ? 1 : 0;

            game.runWeapons.HandleInput(true, true, false);
            float until = Time.time + 2f;
            while (Time.time < until) { await Awaitable.NextFrameAsync(); game.runWeapons.HandleInput(false, true, false); }
            game.runWeapons.HandleInput(false, false, true);

            Assume.That(hits, Is.GreaterThanOrEqualTo(6), "the spray kept hitting");
                                                                                            
            Assert.That(frost, Is.InRange(2 * (hits / 3), 2 * (hits / 2 + 1)), "both come about once per interval, not on every tick");
        }

                                           

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightHit_WielderHurtALittleOrALot_HealsTheSameShareOfTheDamage()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var rules = ElementStatus.For(player).Rules;
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            Bare(targets[0]);
            Bare(targets[1]);
            const float damage = 40f;
            float share = Mathf.Floor(damage * rules.lightShare);
            Assume.That(share, Is.GreaterThanOrEqualTo(1f), "the hit heals at least one point");

            LeaveHp(game, player.maxHp * .6f);
            float before = player.Hp;
            targets[0].ReceiveDamage(Hit(game, damage, ElementKind.Light));
            float healedHigh = player.Hp - before;

            LeaveHp(game, player.maxHp * .15f);
            before = player.Hp;
            targets[1].ReceiveDamage(Hit(game, damage, ElementKind.Light));
            float healedLow = player.Hp - before;

            Assert.That(healedHigh, Is.EqualTo(share).Within(.01f), "a flat share of the damage dealt");
            Assert.That(healedLow, Is.EqualTo(healedHigh).Within(.01f), "and no more when the wielder is low");
        }

                                                  

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LanceRecoil_OpenGround_PushesYouTheWholeRecoilWithoutKnockback_TameKeepsTheWholePush()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            Vector3 origin = player.Position;
            player.DebugSetAim(Vector3.back);                                                                                 
            player.enabled = true;                                                        
            var rail = Equip(game, WeaponForm.Railgun, 0);

            var (untamed, untamedKnocked) = await LanceShot(game, rail);

            Assert.That(untamedKnocked, Is.False, "the recoil is a push, never a knockback");
            Assert.That(game.runWeapons.LanceRecoil(rail), Is.EqualTo(tuning.lanceRecoil), "untamed: the whole recoil");
            Assert.That(untamed, Is.EqualTo(tuning.lanceRecoil).Within(.6f), "it still pushes you that far");

            await SetBack(player, origin);
            var tamed = Equip(game, WeaponForm.Railgun, 0, UpgradeAxis.Tame);
            var (pushed, tamedKnocked) = await LanceShot(game, tamed);

            Assert.That(tamedKnocked, Is.False);
                                                                                                                            
            Assert.That(game.runWeapons.LanceRecoil(tamed), Is.EqualTo(tuning.lanceRecoil));
            Assert.That(pushed, Is.EqualTo(untamed).Within(.6f), "Tame keeps the whole push on open ground");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LanceRecoil_WallRightBehindYou_StopsYouWithoutHurtingOrKnockingYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var player = game.player;
            Vector3 origin = player.Position;
            player.DebugSetAim(Vector3.back);
                                                                                                 
            WorldMap.Active.CreateWall(origin + Vector3.forward * 1.8f, 4f, 60f, 0f, player);
            await SetBack(player, origin);                                
            player.enabled = true;
            var rail = Equip(game, WeaponForm.Railgun, 0);
            float hp = player.Hp;

            var (distance, knocked) = await LanceShot(game, rail);

            Assert.That(knocked, Is.False, "the recoil never knocks you back");
            Assert.That(distance, Is.LessThan(game.runWeapons.LanceRecoil(rail) * .5f), "the wall stops the push");
            Assert.That(player.Hp, Is.EqualTo(hp), "and running into it costs no HP");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FocusRecoil_FullCharge_PushesYouWithoutKnockback_TameKeepsThePushAndGuards()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            Vector3 origin = player.Position;
            player.DebugSetAim(Vector3.back);
            player.enabled = true;
            player.ClearWaveState();                                                                     
            var focus = Equip(game, WeaponForm.ScatterCannon, 1);

            var (untamed, untamedKnocked, untamedGuard) = await FocusShot(game, focus);

            Assert.That(untamedKnocked, Is.False, "the recoil is a push, never a knockback");
            Assert.That(untamed, Is.EqualTo(game.runWeapons.FocusRecoil(focus, 1f)).Within(.8f), "a full charge still pushes you the whole recoil");
            Assert.That(untamedGuard, Is.False, "and gives no guard");

            await SetBack(player, origin);
            player.ClearWaveState();
            var tamed = Equip(game, WeaponForm.ScatterCannon, 1, UpgradeAxis.Tame);
            var (pushed, tamedKnocked, tamedGuard) = await FocusShot(game, tamed);

            Assert.That(tamedKnocked, Is.False);
                                                                                                                 
            Assert.That(game.runWeapons.FocusRecoil(tamed, 1f), Is.EqualTo(tuning.focusRecoilMax).Within(.001f));
            Assert.That(pushed, Is.EqualTo(untamed).Within(.8f), "Tame keeps the whole push");
            Assert.That(tamedGuard, Is.True, "and still guards you against attacks for a moment");
        }

                                                              

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LanceSlug_ShieldedEnemy_ShieldTakesTheWholeHitFirstThenTheHp()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var enemy = targets[0];
            var board = Bare(enemy);
            const float shield = 20f;
            board.SetShield(shield);
            var rail = Equip(game, WeaponForm.Railgun, 0);
            float slug = game.runWeapons.HitDamage(rail);
            Assume.That(slug, Is.GreaterThan(shield), "the slug outweighs the Shield");

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => board.Shield < shield || enemy.Hp < enemy.maxHp, 3f, "the slug hits");

            Assert.That(board.Shield, Is.Zero, "the slug goes into the Shield first and empties it");
            Assert.That(enemy.maxHp - enemy.Hp, Is.EqualTo(slug - shield).Within(.01f), "uncut: the HP takes what the Shield did not");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LanceCrossBlast_ShieldedEnemy_FollowsTheSlugRule_PoisonStillPassesTheShield()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var enemy = targets[0];
            var board = Bare(enemy);
            const float shield = 10f;
            board.SetShield(shield);
            var rail = Equip(game, WeaponForm.Railgun, 0, UpgradeAxis.Twist);
            float blast = game.runWeapons.HitDamage(rail) * game.runWeapons.upgrades.lanceCrossShare;
            Assume.That(blast, Is.GreaterThan(shield), "the blast outweighs the Shield");
                                                                                              
            var slug = AttackPool.Instance.Acquire();
            slug.Configure(AttackMode.Bolt, game.player, rail, game.player.Position, Vector3.forward, 0f, 0f, 0f, 0f, 0f, 1f);

            game.runWeapons.LanceCross(slug, enemy.Position);
            slug.Release();
            await Until(() => board.Shield < shield || enemy.Hp < enemy.maxHp, 2f, "the crossing blast goes off");

            Assert.That(board.Shield, Is.Zero, "the blast goes into the Shield first and empties it");
            Assert.That(enemy.maxHp - enemy.Hp, Is.EqualTo(blast - shield).Within(.01f), "uncut, like the slug");

            board.SetShield(shield);
            float hp = enemy.Hp;
            enemy.ReceiveDamage(new DamageInfo { amount = 3f, source = DamageSource.Poison });
            Assert.That(board.Shield, Is.EqualTo(shield), "Poison still passes the Shield by");
            Assert.That(hp - enemy.Hp, Is.EqualTo(3f).Within(.01f), "straight into the HP");
        }

                                                             

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task WeaponCards_BothLanguages_SayWhatTheWeaponsNowDo()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var tuning = GameManager.Instance.runWeapons.upgrades;
            var texts = new Dictionary<string, string[]>();
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                foreach (var code in new[] { "zh-Hans", "en" })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    texts[code] = new[]
                    {
                        WeaponGuide.How(WeaponForm.Flamethrower, 1),
                        WeaponGuide.Backfire(WeaponForm.CryoCannon, 1),
                        WeaponGuide.Backfire(WeaponForm.Knife, 1),
                        WeaponGuide.How(WeaponForm.ShieldRing, 0),
                        WeaponGuide.How(WeaponForm.Railgun, 0),
                        ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Railgun, 0, UpgradeAxis.Twist),
                        WeaponGuide.Backfire(WeaponForm.Railgun, 0),
                        WeaponGuide.Backfire(WeaponForm.ScatterCannon, 1),
                        ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Railgun, 0, UpgradeAxis.Tame),
                        ArenaTerms.ExpandedUpgradeDescription(WeaponForm.ScatterCannon, 1, UpgradeAxis.Tame),
                        WeaponGuide.Backfire(WeaponForm.ScatterCannon, 0),
                        WeaponGuide.How(WeaponForm.Flamethrower, 0),
                        WeaponGuide.How(WeaponForm.CryoCannon, 0),
                        WeaponGuide.Backfire(WeaponForm.CryoCannon, 0),
                    };
                }
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                Object.Destroy(locale.gameObject);
            }

            string[] zh = texts["zh-Hans"], en = texts["en"];
            Assert.That(zh[0], Does.Contain("where it lands").And.Not.Contain("at your feet"), "Wildfire: the burning ground is where it lands");
            Assert.That(en[0], Does.Contain("where it lands"));
                                                                                                              
            Assert.That(zh[0], Does.Not.Contain("at most"), "Wildfire: no patch cap on the card");
            Assert.That(en[0], Does.Not.Contain("at most"));
            Assert.That(zh[1], Does.Contain("snow").And.Contain("Frost").And.Not.Contain("move slower"), "Blizzard: snow at your feet chills you; spraying no longer slows you");
            Assert.That(en[1], Does.Contain("snow").And.Contain("Frost").And.Not.Contain("move slower"));
            Assert.That(zh[2], Does.Contain("drags you").And.Not.Contain("short wait"), "Chain Blade (owner 2026-10-27): the two-way pull is its backfire; the wait is only a cooldown now");
            Assert.That(en[2], Does.Contain("drags you"));
            Assert.That(zh[3], Does.Contain("force field").And.Contain("Deflect"), "Deflect (owner 2026-10-27): a slowing field that deflects shots");
            Assert.That(en[3], Does.Contain("force field").And.Contain("deflected"));
            Assert.That(zh[3], Does.Contain("轻伤"), "Deflect (P22): the card states its light hit");
            Assert.That(en[3], Does.Contain("light"));
            for (int i = 4; i <= 5; i++)
            {
                Assert.That(zh[i], Does.Contain("Shield's damage cut"), "the Lance and its crossing blast skip only the Shield's cut");
                Assert.That(en[i], Does.Contain("Shield's damage cut"));
            }
            for (int i = 6; i <= 7; i++)
            {
                Assert.That(zh[i], Does.Contain("does not hurt"), "the Lance and Focus recoil never hurts against a wall");
                Assert.That(en[i], Does.Contain("does not hurt"));
            }
            string lanceRecoil = tuning.lanceRecoil.ToString("0.##");
            Assert.That(zh[8], Does.Contain($"没墙时照样后退 {lanceRecoil}").And.Not.Contain("只推"), "Lance Tame (P17): the whole push on open ground");
            Assert.That(en[8], Does.Contain($"still pushes you back {lanceRecoil}").And.Not.Contain("only"));
            Assert.That(zh[9], Does.Contain("照样把你推开").And.Contain("免受攻击").And.Not.Contain("%").And.Not.Contain("wall"), "Focus Tame (P18): the push stays, the guard is added");
            Assert.That(en[9], Does.Contain("as far").And.Contain("immune").And.Not.Contain("%").And.Not.Contain("wall"));
                                                                                                                                         
            Assert.That(zh[10], Does.Contain("shells").And.Not.Contain("jam"), "Scatter Blast: hot shells, no jam");
            Assert.That(en[10], Does.Contain("shells").And.Not.Contain("jam"));
            Assert.That(zh[11], Does.Not.Contain("built-in"), "Stream: no built-in note");
            Assert.That(en[11], Does.Not.Contain("built-in"));
            Assert.That(zh[12], Does.Contain("one per enemy").And.Not.Contain("built-in"), "Shatter: one shard per enemy, no built-in note");
            Assert.That(en[12], Does.Contain("one per enemy").And.Not.Contain("built-in"));
            Assert.That(zh[13], Does.Contain("slip").And.Not.Contain("Frost"), "Shatter: the ice makes you slip and no longer chills you");
            Assert.That(en[13], Does.Contain("slip").And.Not.Contain("Frost"));
        }

                                                                                                          

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task RollArmory_BeforeTheFirstSkill_RingIsAlwaysDeflectAndDroneIsAFairCoin()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var game = GameManager.Instance;
            var weapons = game.runWeapons;
            Assume.That(game.progression.EnergyUnlocked, Is.False, "no skill yet, so no energy bar");
            Assume.That(weapons.DrainOfferable, Is.False);
            float upgradeChance = weapons.armoryUpgradeChance;
            var savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(2610);
            try
            {
                var rings = DrawAuto(weapons, WeaponForm.ShieldRing, 0);
                Assert.That(rings.newOnes, Is.Not.Empty, "the Energy Ring still comes up as a new weapon");
                Assert.That(rings.newOnes, Has.All.Matches<RunWeapon>(w => w.variant == 0), "but always as the Deflect: the Drain costs Energy");
                Assert.That(rings.versions, Is.Not.Empty, "and a held Deflect still comes up as a stronger version");
                Assert.That(rings.versions, Has.All.Matches<RunWeapon>(w => w.variant == 0), "that never turns into the Drain");
                var drones = DrawAuto(weapons, WeaponForm.SentryDrone, 1);
                Assert.That(drones.newOnes, Has.Some.Matches<RunWeapon>(w => w.variant == 0).And.Some.Matches<RunWeapon>(w => w.variant == 1),
                    "the Gunner costs no Energy now (Q103), so the drone is a fair coin from the start");
                Assert.That(drones.versions, Has.Some.Matches<RunWeapon>(w => w.variant == 0), "also as a stronger version");

                game.progression.UnlockEnergy();                                    
                var after = DrawAuto(weapons, WeaponForm.ShieldRing, 0);
                Assert.That(weapons.DrainOfferable, Is.True);
                Assert.That(after.newOnes, Has.Some.Matches<RunWeapon>(w => w.variant == 1), "with the energy bar the Drain is offered");
                Assert.That(after.versions, Has.Some.Matches<RunWeapon>(w => w.variant == 1), "also as a stronger version");
            }
            finally { weapons.armoryUpgradeChance = upgradeChance; UnityEngine.Random.state = savedRandom; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Gunner_NoEnergyBarYet_FiresAndNeverCostsEnergy()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            Assume.That(game.progression.EnergyUnlocked, Is.False, "the sandbox run has learned no skill");
            var gunner = EquipAuto(game, WeaponForm.SentryDrone, 0);

            await Until(() => ActiveBolts(gunner), 2f, "the drone fires without an energy bar (owner 2026-10-27, Q86)");
            Assert.That(game.progression.EnergyUnlocked, Is.False, "and does not bring the bar itself");

            game.progression.UnlockEnergy();                                    
            float energy = game.progression.Energy;
            int shots = gunner.droneShots;
            await Until(() => gunner.droneShots >= shots + 2 || game.runWeapons.DroneDown(gunner), 2f, "the drone keeps firing");
            Assert.That(game.progression.Energy, Is.EqualTo(energy).Within(.01f), "its shots cost no Energy");
        }

                              

                                                                                                                  
        static RunWeapon Equip(GameManager game, WeaponForm form, int variant, UpgradeAxis axes = UpgradeAxis.None)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, axes = axes, rounds = game.runWeapons.ClipSize(form, variant) };
            weapon.element = form switch { WeaponForm.Flamethrower => ElementKind.Fire, WeaponForm.CryoCannon => ElementKind.Frost, WeaponForm.ArcCoil => ElementKind.Lightning, WeaponForm.SporeLauncher => ElementKind.Poison, _ => ElementKind.None };
            weapon.builtInElement = weapon.element != ElementKind.None;
            if (weapon.builtInElement) weapon.axes |= UpgradeAxis.Infusion;
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        static RunWeapon EquipAuto(GameManager game, WeaponForm form, int variant)
        {
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true };
            game.runWeapons.Auto[0] = weapon;
            return weapon;
        }

                                                                                               
        static DamageInfo Hit(GameManager game, float amount, ElementKind element)
        {
            var info = DamageInfo.Attack(amount, game.player);
            info.element = element;
            return info;
        }

                                                                                                            
        static DamageInfo Continuous(GameManager game, float amount, object source)
        {
            var info = Hit(game, amount, ElementKind.Poison);
            info.continuousSource = source;
            return info;
        }

                                                                                      
        static ElementStatus Bare(Combatant body)
        {
            var board = ElementStatus.For(body);
            board.SetShield(0f);
            board.ClearStatuses();
            return board;
        }

                                                                                                           
        static void LeaveHp(GameManager game, float hp)
        {
            float taken = Mathf.Max(.01f, game.progression.Stats.DamageTakenMultiplier);
            game.player.ReceiveDamage(new DamageInfo { amount = (game.player.Hp - hp) / taken, source = DamageSource.Poison, ignoreShield = true });
        }

                                                                    
        static async Task SetBack(Combatant body, Vector3 at)
        {
            body.CancelDisplacement();
            V3CombatSmokeTest.Teleport(body, at);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
        }

                                                                                                                                               
                                                                                                            
        static async Task<(float distance, bool knocked)> LanceShot(GameManager game, RunWeapon rail)
        {
            var player = game.player;
            Vector3 from = player.Position;
            bool knocked = false;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => { knocked |= player.State == CharacterState.Knocked; return !rail.charging && rail.cooldown > 0f; }, 3f, "the Lance fires");
            float until = Time.time + 1.2f;
            while (Time.time < until) { knocked |= player.State == CharacterState.Knocked; await Awaitable.NextFrameAsync(); }
            return (Vector3.Dot(Query.Flat(player.Position - from), Vector3.forward), knocked);
        }

                                                                                                                                           
                                                                                                        
        static async Task<(float distance, bool knocked, bool guarded)> FocusShot(GameManager game, RunWeapon focus)
        {
            var player = game.player;
            Vector3 from = player.Position;
            game.runWeapons.HandleInput(true, true, false);
            focus.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);
            bool guarded = player.Protected, knocked = false;
            float until = Time.time + 1.2f;
            while (Time.time < until) { knocked |= player.State == CharacterState.Knocked; await Awaitable.NextFrameAsync(); }
            return (Vector3.Dot(Query.Flat(player.Position - from), Vector3.forward), knocked, guarded);
        }

                                                                                                                                              
                                                                                                                                                       
        static (List<RunWeapon> newOnes, List<RunWeapon> versions) DrawAuto(RunWeapons weapons, WeaponForm form, int heldVariant)
        {
            var newOnes = new List<RunWeapon>();
            var versions = new List<RunWeapon>();
            weapons.Hand = new[] { new RunWeapon { form = WeaponForm.Rifle, revealed = true }, new RunWeapon { form = WeaponForm.Knife, variant = 1, revealed = true } };
            weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
            weapons.armoryUpgradeChance = 0f;
            for (int draw = 0; draw < 200; draw++)
            {
                weapons.RollArmory(3);
                newOnes.AddRange(weapons.Offers.Where(w => w != null && w.form == form && w.upgradeOf == null));
            }
            var held = new RunWeapon { form = form, variant = heldVariant, revealed = true };
            weapons.Auto = new[] { held, new RunWeapon { form = form == WeaponForm.OrbitBlades ? WeaponForm.ShieldRing : WeaponForm.OrbitBlades, revealed = true } };
            weapons.armoryUpgradeChance = 1f;
            for (int draw = 0; draw < 200; draw++)
            {
                weapons.RollArmory(3);
                versions.AddRange(weapons.Offers.Where(w => w != null && w.upgradeOf == held));
            }
            return (newOnes, versions);
        }

        static bool ActiveBolts(RunWeapon w) =>
            AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == w && a.mode == AttackMode.Bolt);

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
