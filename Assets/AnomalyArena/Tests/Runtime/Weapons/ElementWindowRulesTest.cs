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
    public class ElementWindowRulesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task EnemyStacks_RenewedInsideTheWindow_HoldThenDrainWithinTheDrainTime()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var board = Manual(targets[0]);
            var rules = board.Rules;
            board.ControlThresholdFactor = 100f;                                                                    
            Assume.That(rules.elementWindow, Is.GreaterThan(.5f), "a window long enough to step through");
            Assume.That(rules.elementDrainSeconds, Is.GreaterThan(.3f), "a drain long enough to catch half-way");

            foreach (var element in new[] { ElementKind.Poison, ElementKind.Frost, ElementKind.Lightning })
            {
                board.ClearStatuses();
                board.Add(element, 2f, game.player);
                board.Tick(rules.elementWindow - .1f);
                float held = Stacks(board, element);
                board.Add(element, 1f, game.player);
                board.Tick(rules.elementWindow - .1f);
                float renewed = Stacks(board, element);
                board.Tick(.2f);                                                  
                float draining = Stacks(board, element);
                board.Tick(rules.elementDrainSeconds);
                float drained = Stacks(board, element);

                Assert.That(held, Is.EqualTo(2f).Within(.001f), $"{element}: the stacks hold for the whole window");
                Assert.That(renewed, Is.EqualTo(3f).Within(.001f), $"{element}: another stack starts the window again");
                Assert.That(draining, Is.LessThan(3f).And.GreaterThan(0f), $"{element}: once it runs out the stacks drain");
                Assert.That(drained, Is.Zero, $"{element}: and are gone within the drain time");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FrostAndLightningHits_WeakOrStrong_AddOneStackAndABuiltInElementTwo()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            foreach (var element in new[] { ElementKind.Frost, ElementKind.Lightning })
            {
                var weak = Manual(targets[0]);
                var strong = Manual(targets[1]);
                var own = Manual(targets[2]);
                foreach (var board in new[] { weak, strong, own }) board.ControlThresholdFactor = 100f;
                var rules = weak.Rules;

                targets[0].ReceiveDamage(Hit(game, 2f, element, noJump: true));
                targets[1].ReceiveDamage(Hit(game, 40f, element, noJump: true));
                var builtIn = Hit(game, 2f, element, noJump: true);
                builtIn.builtInElement = true;
                targets[2].ReceiveDamage(builtIn);

                Assert.That(Stacks(weak, element), Is.EqualTo(rules.stacksPerHit).Within(.001f), $"{element}: a light hit adds one hit's stacks");
                Assert.That(Stacks(strong, element), Is.EqualTo(rules.stacksPerHit).Within(.001f), $"{element}: a hit twenty times harder adds no more");
                Assert.That(Stacks(own, element), Is.EqualTo(rules.stacksPerHit * rules.builtInElementStackFactor).Within(.001f),
                    $"{element}: a weapon's own element adds its factor");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PoisonOnAnEnemy_StopsAtTenStacks_EachDealsItsShareASecondThroughTheShieldAndStays()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;
            const float shield = 50f;
            board.SetShield(shield);
            float hp = sut.Hp;

            board.Add(ElementKind.Poison, rules.poisonOnEnemyCap + 2f, game.player);
            float capped = board.Poison;
            board.Tick(rules.poisonOnEnemyInterval);
            board.Tick(rules.poisonOnEnemyInterval);
            float expected = 2f * rules.poisonOnEnemyInterval * rules.poisonOnEnemyCap * rules.poisonOnEnemyDamagePerStack * ElementCombat.WaveScale;

            Assert.That(rules.poisonOnEnemyCap, Is.EqualTo(10f), "the owner's cap: ten stacks");
            Assert.That(capped, Is.EqualTo(rules.poisonOnEnemyCap), "more Poison stops at the cap");

            Assert.That(hp - sut.Hp, Is.EqualTo(expected).Within(1f), "each stack deals its share a second");
            Assert.That(hp - sut.Hp, Is.GreaterThan(0f), "and the ticks land");
            Assert.That(board.Shield, Is.EqualTo(shield), "straight past the Shield into the HP");
            Assert.That(board.Poison, Is.EqualTo(rules.poisonOnEnemyCap), "and the stacks stay while the window lasts");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FireHit_OnAnEnemy_BurnsItsShareOfTheHitASecondForTheWindowAndThenStops()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;
            const float hit = 40f;

            sut.ReceiveDamage(Hit(game, hit, ElementKind.Fire));
            float burn = board.Fire, afterHit = sut.Hp;
            for (int step = 0; step < Mathf.CeilToInt(rules.elementWindow * 10f) + 5; step++) board.Tick(.1f);

            Assert.That(burn, Is.EqualTo(hit * rules.fireBurnShare).Within(.001f), "a quarter of the hit a second");
            Assert.That(afterHit - sut.Hp, Is.EqualTo(hit * rules.fireBurnShare * rules.elementWindow).Within(.01f),
                "for the whole window: three quarters of the hit in all");
            Assert.That(board.Burning, Is.False, "then the burn is over");
            Assert.That(board.Fire, Is.Zero);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FireHits_ALighterOneWhileBurning_KeepsTheHeavierBurnAndRenewsTheWindow_AHeavierOneRaisesIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;
            Assume.That(rules.elementWindow, Is.GreaterThan(1.5f), "a window long enough to split");

            sut.ReceiveDamage(Hit(game, 20f, ElementKind.Fire));
            board.Tick(rules.elementWindow - 1f);
            sut.ReceiveDamage(Hit(game, 4f, ElementKind.Fire));
            float kept = board.Fire;
            board.Tick(1.5f);                                                           
            bool stillBurning = board.Burning;
            sut.ReceiveDamage(Hit(game, 32f, ElementKind.Fire));
            float raised = board.Fire;

            Assert.That(kept, Is.EqualTo(20f * rules.fireBurnShare).Within(.001f), "a lighter hit leaves the heavier burn");
            Assert.That(stillBurning, Is.True, "but renews its window");
            Assert.That(raised, Is.EqualTo(32f * rules.fireBurnShare).Within(.001f), "a heavier hit raises the burn");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FireHits_RenewedOffTheTickRhythm_BurnTheirRateForTheWholeTime()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;
            Assume.That(rules.fireInterval, Is.GreaterThan(.4f), "a renewal 0.4 s in falls between two ticks");

            sut.ReceiveDamage(Hit(game, 40f, ElementKind.Fire));
            for (int step = 0; step < 4; step++) board.Tick(.1f);
            sut.ReceiveDamage(Hit(game, 4f, ElementKind.Fire));                                            
            float afterHits = sut.Hp;
            for (int step = 0; step < Mathf.CeilToInt(rules.elementWindow * 10f) + 5; step++) board.Tick(.1f);

            Assert.That(board.Burning, Is.False, "the burn is over");

            Assert.That(afterHits - sut.Hp, Is.EqualTo(40f * rules.fireBurnShare * (rules.elementWindow + .4f)).Within(1f),
                "the heaviest hit's rate for the whole renewed time, its last part between two ticks included");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FireHits_AHeavierOneBetweenTwoTicks_RaisesTheBurnFromThatMomentOnly()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;
            Assume.That(rules.fireInterval, Is.GreaterThan(.25f), "a hit 0.25 s in falls before the first tick");

            sut.ReceiveDamage(Hit(game, 8f, ElementKind.Fire));
            board.Tick(.25f);
            sut.ReceiveDamage(Hit(game, 40f, ElementKind.Fire));
            float afterHits = sut.Hp;
            for (int step = 0; step < Mathf.CeilToInt(rules.elementWindow * 20f) + 10; step++) board.Tick(.05f);
            float earned = 8f * rules.fireBurnShare * .25f + 40f * rules.fireBurnShare * rules.elementWindow;

            Assert.That(board.Burning, Is.False, "the burn is over");
            Assert.That(afterHits - sut.Hp, Is.EqualTo(earned).Within(1f), "the light hit's rate until the heavy hit, then the heavy one's for its window");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FlameWake_AnEnemyOnTheTrail_BurnsAtTheSkillsStrengthASecond()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var sut = targets[0];
            var board = Bare(sut);

            V3CombatSmokeTest.Teleport(sut, game.player.Position + Vector3.right * .6f);
            Physics.SyncTransforms();
            Assume.That(game.skills.Learn(SkillId.FlameWake), Is.True, "Flame Wake is learned");
            var def = game.skills.Catalog.Find(SkillId.FlameWake);

            Assume.That(game.skills.TryCast(0), Is.True, "Flame Wake is cast");
            await Until(() => board.Burning, 2f, "the trail sets the enemy burning");

            Assert.That(board.Fire, Is.EqualTo(def.Strength(1) * ElementCombat.WaveScale).Within(.001f),
                "it burns at the skill's strength a second, whatever the frame time");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Burn_OnAShieldedEnemy_IsCutByTheShieldBeforeTheShieldTakesIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;
            board.SetShield(100f);

            sut.ReceiveDamage(Hit(game, 40f, ElementKind.Fire));
            float shieldAfterHit = board.Shield, burn = board.Fire;
            board.Tick(rules.fireInterval);

            Assert.That(burn, Is.EqualTo(40f * rules.fireBurnShare).Within(.001f), "the burn follows the hit itself, not what the Shield let through");
            Assert.That(shieldAfterHit - board.Shield, Is.EqualTo(Mathf.Ceil(burn * rules.fireInterval * rules.fireShieldFactor)).Within(.001f),
                "a burn tick is cut by the Shield before the Shield takes it");
            Assert.That(sut.Hp, Is.EqualTo(sut.maxHp), "nothing reaches the HP while the Shield holds");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FireStacks_NotAWeaponHit_BurnTheirOldWorthOverTheWindow_AndAFireImmuneEnemyNeverBurns()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var sut = targets[0];
            var board = Manual(sut);
            var immune = Manual(targets[1]);
            immune.SetImmunity(ElementKind.Fire, 1f);
            var rules = board.Rules;
            float hp = sut.Hp;

            board.Add(ElementKind.Fire, 2f, game.player);
            bool burning = board.Burning;
            immune.Add(ElementKind.Fire, 2f, game.player);
            targets[1].ReceiveDamage(Hit(game, 20f, ElementKind.Fire));
            for (int step = 0; step < Mathf.CeilToInt(rules.elementWindow * 10f) + 5; step++) board.Tick(.1f);

            Assert.That(burning, Is.True, "the stacks set the enemy burning");
            Assert.That(hp - sut.Hp, Is.EqualTo(2f * rules.fireStackDamage * ElementCombat.WaveScale).Within(1f), "each stack is worth its old burn damage over the window");
            Assert.That(immune.Burning, Is.False, "a Fire-immune enemy burns from neither");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FireHit_ACrit_BurnsOnTheHitBeforeTheCrit()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;
            var stats = game.progression.Stats;
            float chance = stats.critChance;
            try
            {
                stats.critChance = 1f;                                      
                float hp = sut.Hp;
                sut.ReceiveDamage(Hit(game, 20f, ElementKind.Fire));
                Assume.That(sut.LastHitCritical, Is.True, "the hit crit");

                Assert.That(hp - sut.Hp, Is.EqualTo(20f * stats.CritMultiplier).Within(.01f), "the hit itself deals its crit");
                Assert.That(board.Fire, Is.EqualTo(20f * rules.fireBurnShare).Within(.001f), "the burn follows the hit before the crit");
            }
            finally { stats.critChance = chance; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FireAffix_AnEnemyHitsAnotherEnemy_IgnitesItLikeAFireHit_ThePlayerStillGetsStacks()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var victim = Manual(targets[1]);
            var rules = victim.Rules;
            var player = ElementStatus.For(game.player);
            player.ClearStatuses();
            const float damage = 12f;

            ElementCombat.ApplyAffix(targets[0], targets[1], ElementKind.Fire, damage);
            ElementCombat.ApplyAffix(targets[0], game.player, ElementKind.Fire, damage);

            Assert.That(victim.Fire, Is.EqualTo(damage * rules.fireBurnShare).Within(.001f), "another enemy burns at a share of the hit a second");
            Assert.That(player.Fire, Is.EqualTo(player.Rules.enemyFireStacks).Within(.001f), "the player keeps the affix's Burning stacks");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task EnemyHpBar_BurningEnemies_ShowTheFireIconWithTheBurnPerSecondRoundedAndAtLeastOne()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var labels = Object.FindAnyObjectByType<V2WorldLabels>();
            Assume.That(labels, Is.Not.Null, "the scene has its world labels");
            foreach (var target in targets) Bare(target);
            var rules = ElementStatus.For(targets[0]).Rules;
            Assume.That(2f * rules.fireBurnShare, Is.LessThan(1f), "the weak hit burns less than one a second");

            targets[0].ReceiveDamage(Hit(game, 2f, ElementKind.Fire));
            targets[1].ReceiveDamage(Hit(game, 40f, ElementKind.Fire));
            string strong = Mathf.RoundToInt(40f * rules.fireBurnShare).ToString();

            var bindings = (Enemy[])typeof(V2WorldLabels).GetField("bindings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(labels);
            string FireCount(Enemy enemy)
            {
                int i = Array.IndexOf(bindings, enemy);
                if (i < 0) return null;
                var count = labels.enemyLabels[i].statusCounts[0];
                return count && count.gameObject.activeSelf ? count.text : "";
            }
            await Until(() => FireCount(targets[0]) == "1" && FireCount(targets[1]) == strong, 2f, "the burning enemies' bars show the fire icon");

            Assert.That(FireCount(targets[0]), Is.EqualTo("1"), "a burn under one a second shows 1");
            Assert.That(FireCount(targets[1]), Is.EqualTo(strong), "a strong one its rate a second");
            Assert.That(FireCount(targets[2]), Is.Empty, "an enemy that does not burn shows no fire icon");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FrostStacks_SlowMovesAndAttacksTenPercentEach_OnAnyBody_AtMostHalf()
        {
            var (_, targets) = await V3CombatSmokeTest.StartSandbox();
            var board = Manual(targets[0]);
            var rules = board.Rules;
            Assume.That(rules.frostThreshold, Is.EqualTo(6f), "six stacks freeze an ordinary body");
            var slowed = new List<float>();

            for (int stacks = 1; stacks <= 5; stacks++)
            {
                board.ClearStatuses();
                board.Add(ElementKind.Frost, stacks);
                slowed.Add(board.SpeedMultiplier);
            }
            board.ClearStatuses();
            board.ControlThresholdFactor = 2f;                             
            board.Add(ElementKind.Frost, 4f);
            float large = board.SpeedMultiplier;
            board.Add(ElementKind.Frost, 7f);
            float largeNearlyFull = board.SpeedMultiplier;

            Assert.That(slowed, Is.EqualTo(new[] { .9f, .8f, .7f, .6f, .5f }).Within(.001f), "10% slower per stack, moves and attack timers alike");
            Assert.That(large, Is.EqualTo(1f - 4f * rules.frostSlowPerStack).Within(.001f), "a large body loses the same per stack, it only needs more to freeze");
            Assert.That(largeNearlyFull, Is.EqualTo(1f - rules.frostSlowMax).Within(.001f), "never slower than half speed");
            Assert.That(board.Frozen, Is.False, "eleven stacks do not freeze a large body");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FullFrost_FreezesClearsTheStacksAndRaisesDamage_ThenStacksBuildButNoFreezeForTheImmunity()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;

            board.Add(ElementKind.Frost, board.FrostThreshold, game.player);
            bool frozen = board.Frozen, blocked = board.BlocksActions && board.BlocksMovement;
            float cleared = board.Frost;
            float hp = sut.Hp;
            sut.ReceiveDamage(Hit(game, 10f, ElementKind.None));
            float taken = hp - sut.Hp;
            board.Add(ElementKind.Frost, 2f, game.player);
            float whileFrozen = board.Frost;
            board.Tick(rules.frostDuration + .01f);
            bool thawed = !board.Frozen, immune = board.ControlImmune;
            board.Add(ElementKind.Frost, board.FrostThreshold + 3f, game.player);
            float waiting = board.Frost;
            bool frozenWhileImmune = board.Frozen;
            board.Tick(rules.controlImmunity);
            bool refrozen = board.Frozen;

            Assert.That(frozen && blocked, Is.True, "full Frost freezes it: no moving, no acting");
            Assert.That(cleared, Is.Zero, "and clears the stacks");
            Assert.That(taken, Is.EqualTo(10f * rules.frozenDamageTaken).Within(.01f), "a frozen enemy takes more damage");
            Assert.That(whileFrozen, Is.Zero, "a frozen enemy takes no Frost");
            Assert.That(thawed && immune, Is.True, "after the freeze it is immune to control for a while");
            Assert.That(waiting, Is.EqualTo(board.FrostThreshold).Within(.001f), "its stacks still build up to full meanwhile");
            Assert.That(frozenWhileImmune, Is.False, "but do not freeze it");
            Assert.That(refrozen, Is.True, "full stacks freeze it as soon as the immunity ends");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightningStacks_RaisePlayerDamageFivePercentEach_AParalysedEnemyTakesTheMost_EnemyHitsGainNothing()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var sut = targets[0];
            var board = Manual(sut);
            var rules = board.Rules;

            board.Add(ElementKind.Lightning, 3f, game.player);
            float hp = sut.Hp;
            sut.ReceiveDamage(Hit(game, 10f, ElementKind.None));
            float byPlayer = hp - sut.Hp;
            hp = sut.Hp;
            sut.ReceiveDamage(DamageInfo.Attack(10f, targets[1]));
            float byEnemy = hp - sut.Hp;
            board.Add(ElementKind.Lightning, 3f, game.player);
            bool paralysed = board.Paralysed;
            float cleared = board.Lightning;
            hp = sut.Hp;
            sut.ReceiveDamage(Hit(game, 10f, ElementKind.None));
            float whileParalysed = hp - sut.Hp;

            Assert.That(byPlayer, Is.EqualTo(10f * (1f + 3f * rules.lightningVulnerabilityPerStack)).Within(.01f), "+5% a stack on a player hit");
            Assert.That(byEnemy, Is.EqualTo(10f).Within(.01f), "an enemy's hit gains nothing from it");
            Assert.That(paralysed, Is.True, "six stacks paralyse it");
            Assert.That(cleared, Is.Zero, "and clear the stacks");
            Assert.That(whileParalysed, Is.EqualTo(10f * (1f + rules.lightningParalysedVulnerability)).Within(.01f), "a paralysed enemy counts as fully charged");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightningStacks_OnALargeBody_CountTheSamePerStack_AndTheTwitchChanceStopsAtItsCap()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var board = Manual(targets[0]);
            var rules = board.Rules;
            rules.lightningTwitchChancePerStack = .1f;
            rules.lightningTwitchChanceMax = .5f;
            board.ControlThresholdFactor = 2f;                             

            board.Add(ElementKind.Lightning, 3f, game.player);
            float three = board.LightningDamageTaken, threeChance = board.TwitchChance;
            board.Add(ElementKind.Lightning, 6f, game.player);
            float nine = board.LightningDamageTaken, nineChance = board.TwitchChance;

            Assert.That(three, Is.EqualTo(1f + 3f * rules.lightningVulnerabilityPerStack).Within(.001f), "+5% a stack on a large body too");
            Assert.That(threeChance, Is.EqualTo(3f * rules.lightningTwitchChancePerStack).Within(.001f), "and 10% a stack to twitch");
            Assert.That(nine, Is.EqualTo(1f + rules.lightningVulnerabilityMax).Within(.001f), "the extra damage stops at its cap");
            Assert.That(nineChance, Is.EqualTo(rules.lightningTwitchChanceMax).Within(.001f), "and so does the twitch chance, short of a paralysis");
            Assert.That(board.Paralysed, Is.False, "nine stacks do not paralyse a large body");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightningStacksBelowTheThreshold_TwitchByTheirChance_EachTwitchStopsTheBody()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var board = Manual(targets[0]);
            var rules = board.Rules;

            rules.lightningTwitchChancePerStack = 1f;                                     
            rules.lightningTwitchChanceMax = 1f;
            board.Add(ElementKind.Lightning, 1f, game.player);
            board.Tick(rules.twitchInterval - .05f);
            bool beforeTheRoll = board.Twitching;
            board.Tick(.1f);
            bool twitched = board.Twitching, stopped = board.BlocksMovement && board.BlocksActions;
            board.Tick(rules.twitchLength + .05f);
            bool over = !board.Twitching;
            bool paralysed = board.Paralysed;

            rules.lightningTwitchChancePerStack = 0f;                    
            board.ClearStatuses();
            board.Add(ElementKind.Lightning, 5f, game.player);
            bool everTwitched = false;
            for (int step = 0; step < 25; step++) { board.Tick(.1f); everTwitched |= board.Twitching; }

            Assert.That(beforeTheRoll, Is.False, "the first roll comes one twitch interval after the first stack");
            Assert.That(twitched && stopped, Is.True, "a twitch stops the body: no moving, no acting");
            Assert.That(over, Is.True, "for the twitch's length only");
            Assert.That(paralysed, Is.False, "one stack is no paralysis");
            Assert.That(everTwitched, Is.False, "without the chance nothing twitches");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FullLightning_ParalysesAndClearsTheStacks_ThenNoTwitchOrParalysisForTheImmunity()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var board = Manual(targets[0]);
            var rules = board.Rules;

            board.Add(ElementKind.Lightning, board.LightningThreshold, game.player);
            bool paralysed = board.Paralysed;
            float cleared = board.Lightning;
            board.Tick(rules.lightningDuration + .01f);
            bool over = !board.Paralysed, immune = board.ControlImmune;
            rules.lightningTwitchChancePerStack = 1f;
            rules.lightningTwitchChanceMax = 1f;
            board.Add(ElementKind.Lightning, board.LightningThreshold + 2f, game.player);
            float waiting = board.Lightning;
            bool twitchedWhileImmune = false;
            for (int step = 0; step < 15; step++) { board.Tick(.1f); twitchedWhileImmune |= board.Twitching || board.Paralysed; }
            board.Tick(rules.controlImmunity - 1.5f + .01f);
            bool paralysedAgain = board.Paralysed;

            Assert.That(paralysed, Is.True, "full Lightning paralyses it");
            Assert.That(cleared, Is.Zero, "and clears the stacks");
            Assert.That(over && immune, Is.True, "after the paralysis it is immune to control for a while");
            Assert.That(waiting, Is.EqualTo(board.LightningThreshold).Within(.001f), "its stacks still build up to full meanwhile");
            Assert.That(twitchedWhileImmune, Is.False, "but neither twitch nor paralyse it");
            Assert.That(paralysedAgain, Is.True, "full stacks paralyse it as soon as the immunity ends");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Bosses_FullFrostAndLightning_NeverFreezeParalyseOrTwitch()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var prophet = targets[0];
            prophet.Configure(game.waves.DefinitionOf(EnemyKind.Hollow), 1f, false);
            prophet.enabled = false;
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * 12f);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");
            tyrant.enabled = false;

            var stripped = Manual(targets[1]);
            stripped.ImmuneToControl = true;
            var boards = new[] { Manual(prophet), Manual(tyrant), stripped };
            bool controlled = false;

            foreach (var board in boards)
            {
                board.Rules.lightningTwitchChancePerStack = 1f;
                board.Rules.lightningTwitchChanceMax = 1f;
                board.Add(ElementKind.Frost, 99f, game.player);
                board.Add(ElementKind.Lightning, 99f, game.player);
                controlled |= board.Frozen || board.Paralysed;
                for (int step = 0; step < 50; step++)
                {
                    board.Tick(.1f);
                    board.Add(ElementKind.Frost, 99f, game.player);
                    board.Add(ElementKind.Lightning, 99f, game.player);
                    controlled |= board.Frozen || board.Paralysed || board.Twitching;
                }
            }

            Assert.That(controlled, Is.False, "a boss is never frozen, paralysed or made to twitch");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Freeze_DuringARammersWindUpOrCharge_CutsTheWindUpShortAndStopsTheCharge()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            game.player.Invulnerable = true;
            var rammer = targets[0];
            Arm(rammer, game, EnemyKind.Rammer, game.player.Position + Vector3.forward * 14f);
            var board = ElementStatus.For(rammer);

            await Until(() => rammer.WindingUp, 6f, "the Rammer winds up");
            board.Add(ElementKind.Frost, board.FrostThreshold, game.player);
            Assume.That(board.Frozen, Is.True, "full Frost freezes it");
            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();
            bool windUpCut = !rammer.WindingUp;

            await Until(() => !board.Frozen, 4f, "it thaws");
            board.ClearStatuses();                                                
            await Until(() => rammer.Charging, 6f, "it winds up again and charges");
            board.Add(ElementKind.Frost, board.FrostThreshold, game.player);
            Assume.That(board.Frozen, Is.True, "full Frost freezes it mid-charge");
            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();
            Vector3 at = rammer.Position;
            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();

            Assert.That(windUpCut, Is.True, "a freeze cuts the wind-up short");
            Assert.That(rammer.Charging, Is.False, "a freeze stops the charge");
            Assert.That(Query.Flat(rammer.Position - at).magnitude, Is.LessThan(.05f), "and the Rammer stands still");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightningTwitch_DuringARammersWindUpOrCharge_CutsTheWindUpShortAndStopsTheCharge()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            game.player.Invulnerable = true;
            var rammer = targets[0];
            Arm(rammer, game, EnemyKind.Rammer, game.player.Position + Vector3.forward * 14f);
            var board = ElementStatus.For(rammer);
            var original = board.Rules;
            var rules = Object.Instantiate(original);
            try
            {

                rules.lightningTwitchChancePerStack = 1f;
                rules.lightningTwitchChanceMax = 1f;
                rules.twitchInterval = .05f;
                board.Rules = rules;

                await Until(() => rammer.WindingUp, 6f, "the Rammer winds up");
                board.Add(ElementKind.Lightning, 1f, game.player);
                await Until(() => board.Twitching, 1f, "the Lightning stack makes it twitch");
                await Awaitable.FixedUpdateAsync();
                bool windUpCut = !rammer.WindingUp;
                Assume.That(board.Paralysed, Is.False, "a twitch, not a paralysis");

                board.ClearStatuses();
                await Until(() => rammer.Charging, 6f, "it winds up again and charges");
                board.Add(ElementKind.Lightning, 1f, game.player);
                await Until(() => board.Twitching, 1f, "the Lightning stack makes it twitch");
                await Awaitable.FixedUpdateAsync();

                Assert.That(windUpCut, Is.True, "a twitch cuts the wind-up short");
                Assert.That(rammer.Charging, Is.False, "a twitch stops the charge");
            }
            finally { board.Rules = original; Object.Destroy(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task InfusionAndWildfireCards_BothLanguages_SayTheNewElementRulesWithTheLiveNumbersAndFit()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var elements = new[] { ElementKind.Fire, ElementKind.Poison, ElementKind.Frost, ElementKind.Lightning, ElementKind.Earth, ElementKind.Light, ElementKind.Void };
            var playerBoard = ElementStatus.For(game.player);
            var r = playerBoard.Rules;
            var tuning = game.runWeapons.upgrades;
            string fenceBurn = (tuning.wildfireFenceFirePerSecond * ElementCombat.WaveScale).ToString("0.##");
            var rules = game.progression.Rules;
            var infusionOnly = Object.Instantiate(rules);
            var texts = new Dictionary<string, string[]>();
            string cardBody = null, cardRule = null;
            var screen = Object.FindObjectsByType<V2UiScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(s => s.state == ArenaUiState.Draft && s.choices != null && s.choices.Length > 0);
            Assume.That(screen, Is.Not.Null, "the saved scene has the draft screen");
            var choice = screen.choices[0];
            bool screenWasActive = screen.gameObject.activeSelf, choiceWasActive = choice.gameObject.activeSelf;
            var overflow = new List<string>();
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {

                infusionOnly.weaponDirectionBands = new[] { new WeaponDirectionBand(1, 0f, 0f, 100f) };
                game.progression.Rules = infusionOnly;
                game.runWeapons.Hand = new[] { new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true }, null };
                game.runWeapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
                screen.gameObject.SetActive(true);
                choice.gameObject.SetActive(true);
                choice.FitBody();
                foreach (var code in new[] { "zh-Hans", "en" })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    texts[code] = elements.Select(ArenaTerms.ElementRule)
                        .Append(ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Flamethrower, 1, UpgradeAxis.Twist)).ToArray();
                    if (chinese)
                    {
                        var card = game.runWeapons.RollUpgradeCard(null, 1);
                        cardBody = card.Description;
                        cardRule = card.Payload is WeaponUpgradeOffer { Axis: UpgradeAxis.Infusion } offer ? ArenaTerms.ElementRule(offer.Element) : null;
                    }
                    foreach (var element in elements)
                    {
                        string body = ArenaText.Pair("Infuse ","Infuse ") + ArenaTerms.Element(element) + "\n" + ArenaTerms.ElementRule(element);
                        if (!FitsTheCard(choice, body, out string fit)) overflow.Add($"{code} {element}: {fit}");
                    }
                }
            }
            finally
            {
                game.progression.Rules = rules;
                Object.Destroy(infusionOnly);
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                Object.Destroy(locale.gameObject);
                choice.gameObject.SetActive(choiceWasActive);
                screen.gameObject.SetActive(screenWasActive);
            }

            string[] zh = texts["zh-Hans"], en = texts["en"];
            string P(float share) => (share * 100f).ToString("0.#") + "%";
            string N(float value) => value.ToString("0.##");
            Assert.That(cardRule, Is.Not.Null.And.Not.Empty, "an Infusion card came up");
            Assert.That(cardBody, Does.Contain(cardRule), "the Infusion card carries its element's rule");
            Assert.That(zh[0], Does.Contain(P(r.fireBurnShare)).And.Contain($"{N(r.elementWindow)} s").And.Contain("DPS"), "Fire: a share of the weapon's DPS a second");
            Assert.That(en[0], Does.Contain(P(r.fireBurnShare)).And.Contain($"{N(r.elementWindow)} s").And.Contain("DPS"));
            Assert.That(zh[1], Does.Contain($"up to {N(r.poisonOnEnemyCap)}").And.Contain("through Shields"), "Poison: ten stacks through the Shield");
            Assert.That(en[1], Does.Contain($"up to {N(r.poisonOnEnemyCap)}").And.Contain("through Shields"));
            Assert.That(zh[2], Does.Contain(P(r.frostSlowPerStack)).And.Contain($"{N(r.frostThreshold)}").And.Contain($"{N(r.controlImmunity)} s"),
                "Frost: the slow per stack, the threshold and the immunity");
            Assert.That(en[2], Does.Contain(P(r.frostSlowPerStack)).And.Contain($"at {N(r.frostThreshold)}").And.Contain($"for {N(r.controlImmunity)} s"));
            Assert.That(zh[3], Does.Contain(P(r.lightningVulnerabilityPerStack)).And.Contain(P(r.lightningTwitchChancePerStack)).And.Contain("twitch")
                .And.Contain($"{N(r.controlImmunity)} s"), "Lightning: damage taken and twitch chance per stack, the immunity");
            Assert.That(en[3], Does.Contain(P(r.lightningVulnerabilityPerStack)).And.Contain("twitch").And.Contain($"for {N(r.controlImmunity)} s"));
            Assert.That(zh[4], Does.Contain(P(r.earthShare)).And.Contain("Shield"), "Earth: a share as Shield");
            Assert.That(zh[5], Does.Contain(P(r.lightShare)).And.Contain("heal"), "Light: a share as healing");
            Assert.That(zh[6], Does.Contain(P(r.voidShare)).And.Contain("Energy"), "Void: a share as Energy");
            Assert.That(zh[7], Does.Contain("burns").And.Contain($"burns for {fenceBurn} per second"), "Wildfire fence: the enemy burns a fixed amount a second");
            Assert.That(en[7], Does.Contain($"burns for {fenceBurn} per second"));
            Assert.That(overflow, Is.Empty, "every Infusion card fits its body box");
        }

        static float Stacks(ElementStatus board, ElementKind element) => element switch
        {
            ElementKind.Poison => board.Poison,
            ElementKind.Frost => board.Frost,
            ElementKind.Lightning => board.Lightning,
            _ => board.Fire
        };

        static DamageInfo Hit(GameManager game, float amount, ElementKind element, bool noJump = false)
        {
            var info = DamageInfo.Attack(amount, game.player);
            info.element = element;
            info.noLightningJump = noJump;
            return info;
        }

        static ElementStatus Bare(Combatant body)
        {
            var board = ElementStatus.For(body);
            board.SetShield(0f);
            board.ClearStatuses();
            return board;
        }

        static ElementStatus Manual(Combatant body)
        {
            var board = Bare(body);
            board.enabled = false;
            board.Rules = Object.Instantiate(board.Rules);
            board.Rules.lightningTwitchChancePerStack = 0f;
            return board;
        }

        static void Arm(Enemy enemy, GameManager game, EnemyKind kind, Vector3 at)
        {
            enemy.Configure(game.waves.DefinitionOf(kind), 1f, false);
            V3CombatSmokeTest.Teleport(enemy, at);
            enemy.moveSpeed = 0f;
            enemy.enabled = true;
            Physics.SyncTransforms();
        }

        static bool FitsTheCard(V2UiChoice choice, string text, out string report)
        {
            choice.ShowBody(text);
            Canvas.ForceUpdateCanvases();
            var settings = choice.body.GetGenerationSettings(choice.body.rectTransform.rect.size);
            var generator = new TextGenerator();
            generator.PopulateWithErrors(text, settings, choice.gameObject);
            int needed = text.Count(c => !char.IsWhiteSpace(c));
            int shown = text.Take(generator.characterCountVisible).Count(c => !char.IsWhiteSpace(c));
            report = $"{shown}/{needed} chars at size {choice.body.fontSize}";
            return shown >= needed;
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
