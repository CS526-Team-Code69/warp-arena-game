using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class CombatRulesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test]
        public void ScaleFor_BiggerBodiesAndElites_SlideShorterWithinTheLimits()
        {
            var sut = new KnockbackRules { referenceRadius = .5f, sizeExponent = 1f, minScale = .25f, maxScale = 1.5f, eliteFactor = .5f };

            Assert.That(sut.ScaleFor(.5f, false), Is.EqualTo(1f).Within(1e-4f), "a body of the reference size slides the whole distance");
            Assert.That(sut.ScaleFor(1f, false), Is.EqualTo(.5f).Within(1e-4f), "twice as wide, half as far");
            Assert.That(sut.ScaleFor(1f, true), Is.EqualTo(.25f).Within(1e-4f), "an elite slides half as far as its kind");
            Assert.That(sut.ScaleFor(.1f, false), Is.EqualTo(1.5f).Within(1e-4f), "a tiny body stops at the longest slide");
            Assert.That(sut.ScaleFor(4f, false), Is.EqualTo(.25f).Within(1e-4f), "a huge ordinary body stops at the shortest slide");
        }

        [Test]
        public void AttackTier_SpecialKinds_FollowTheDocumentTable()
        {
            var sut = new KnockbackRules();

            Assert.That(KnockbackRules.AttackTier(EnemyKind.Rammer), Is.EqualTo(KnockTier.Light), "the Rammer's charge pushes a little");
            Assert.That(KnockbackRules.AttackTier(EnemyKind.Bloater), Is.EqualTo(KnockTier.Medium), "the Bloater's blast");
            Assert.That(KnockbackRules.AttackTier(EnemyKind.Stomper), Is.EqualTo(KnockTier.Heavy), "the Stomper's slam is heavy, not fly");
            Assert.That(KnockbackRules.AttackTier(EnemyKind.Shade), Is.EqualTo(KnockTier.None), "the Shade's strike pushes nobody");
            Assert.That(KnockbackRules.AttackTier(EnemyKind.Spire), Is.EqualTo(KnockTier.None), "the Spire's pulse pushes nobody");
            Assert.That(sut.Distance(KnockTier.None), Is.Zero, "no tier, no push");
            Assert.That(sut.Distance(KnockTier.Heavy), Is.LessThan(sut.Distance(KnockTier.Fly)), "the heavy tier stops short of the fly tier");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Configure_SmallAndLargeBodies_KnockbackScaleFollowsBodySize()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var rules = KnockbackRules.Active;
            var sut = targets[0];

            float grunt = ScaleAfterConfigure(sut, game, EnemyKind.Grunt);
            float brute = ScaleAfterConfigure(sut, game, EnemyKind.Brute);
            float mini = ScaleAfterConfigure(sut, game, EnemyKind.Splitter, mini: true);
            float rammer = ScaleAfterConfigure(sut, game, EnemyKind.Rammer);
            float eliteRammer = ScaleAfterConfigure(sut, game, EnemyKind.Rammer, elite: true);
            float spire = ScaleAfterConfigure(sut, game, EnemyKind.Spire);
            float hollow = ScaleAfterConfigure(sut, game, EnemyKind.Hollow);

            Assert.That(grunt, Is.EqualTo(rules.ScaleFor(game.waves.DefinitionOf(EnemyKind.Grunt).radius, false)).Within(1e-4f),
                "a Grunt follows the size curve");
            Assert.That(brute, Is.LessThan(grunt), "the wider Brute slides shorter than a Grunt");
            Assert.That(mini, Is.GreaterThan(grunt), "a mini Splitter slides farther than a Grunt");
            Assert.That(eliteRammer, Is.EqualTo(rammer * rules.eliteFactor).Within(1e-4f), "an elite slides less than its kind");
            Assert.That(spire, Is.Zero, "the Spire never moves");
            Assert.That(hollow, Is.Zero, "a boss never moves");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Knockback_BruteAndGrunt_TheBruteSlidesShorterButAHookFlingIgnoresSize()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;
            var grunt = Ready(targets[0], game, EnemyKind.Grunt, origin + Vector3.forward * 3f);
            var brute = Ready(targets[1], game, EnemyKind.Brute, origin + Vector3.forward * 8f);
            var flung = Ready(targets[2], game, EnemyKind.Brute, origin + Vector3.forward * 13f);
            const float distance = 4f;

            grunt.Knockback(Vector3.forward, distance, KnockKind.Hit);
            brute.Knockback(Vector3.forward, distance, KnockKind.Hit);
            flung.Throw(Vector3.forward, distance, new ThrowImpact { dealer = game.player });

            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();

            float full = distance * game.rules.knockDamping;
            Assume.That(Launch(grunt), Is.GreaterThan(0f), "the Grunt was launched");
            Assert.That(brute.knockbackScale, Is.LessThan(grunt.knockbackScale), "the Brute is the bigger body");
            Assert.That(Launch(brute) / Launch(grunt), Is.EqualTo(brute.knockbackScale / grunt.knockbackScale).Within(.1f),
                "so it is launched that much slower by the same knockback");
            Assert.That(Launch(flung), Is.EqualTo(full).Within(full * .2f), "a hook fling slides its whole distance, whatever the size");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterBlastTwist_BigSmallAndEliteBodies_AreAllPulledToJustInFront()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;

            var bodies = new[]
            {
                Ready(targets[0], game, EnemyKind.Brute, origin + new Vector3(0f, 0f, 7f)),
                Ready(targets[1], game, EnemyKind.Rammer, origin + new Vector3(.4f, 0f, 4.2f), elite: true),
                Ready(targets[2], game, EnemyKind.Splitter, origin + new Vector3(-.5f, 0f, 2.8f), mini: true)
            };
            Assume.That(bodies[0].knockbackScale, Is.LessThan(1f), "a plain knockback would leave the Brute short");
            Assume.That(bodies[1].knockbackScale, Is.LessThan(1f), "and the elite Rammer");
            Assume.That(bodies[2].knockbackScale, Is.GreaterThan(1f), "and carry the mini Splitter too far");
            var tuning = game.runWeapons.upgrades;
            game.runWeapons.ClearWaveEffects();

            game.runWeapons.Hand[game.runWeapons.ActiveIndex] =
                new RunWeapon { form = WeaponForm.ScatterCannon, variant = 0, revealed = true, axes = UpgradeAxis.Twist, rounds = 1 };

            var toward = bodies.Select(b => -Query.Flat(b.Position - origin).normalized).ToArray();
            var expected = bodies.Select(b => (Query.Flat(b.Position - origin).magnitude - b.radius - game.player.radius - tuning.scatterVacuumGap)
                * game.rules.knockDamping).ToArray();

            game.runWeapons.HandleInput(true, true, false);

            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();

            for (int i = 0; i < bodies.Length; i++)
                Assert.That(Speed(bodies[i], toward[i]), Is.EqualTo(expected[i]).Within(expected[i] * .2f),
                    $"{bodies[i].definition.kind} (knockback scale {bodies[i].knockbackScale:0.##}) is pulled to just in front of you");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Knockback_SpireWindingUpAndTyrant_NeitherIsKnockedNorLosesItsWindup()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.player.Invulnerable = true;                                                         
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var spire = targets[0];
            spire.Configure(game.waves.DefinitionOf(EnemyKind.Spire), 1f, false);
            Place(spire, game.player.Position + Vector3.forward * 4f);
            spire.enabled = true;
            await Until(() => spire.WindingUp, 4f, "the Spire starts its wind-up");
            Vector3 at = spire.Position;
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * 12f);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");
            tyrant.enabled = false;

            spire.Knockback(Vector3.forward, 10f, KnockKind.Hit);
            spire.Push(Vector3.forward, 10f);
            tyrant.Knockback(Vector3.forward, 10f, KnockKind.Hit);
            var spireState = spire.State;
            bool windingUp = spire.WindingUp;
            var tyrantState = tyrant.State;
            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();

            Assert.That(spireState, Is.EqualTo(CharacterState.Normal), "the Spire is not knocked");
            Assert.That(windingUp, Is.True, "so its wind-up goes on");
            Assert.That(Query.Flat(spire.Position - at).magnitude, Is.LessThan(.01f), "and it stays where it stood");
            Assert.That(tyrantState, Is.EqualTo(CharacterState.Normal), "a boss is not knocked either");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StomperSlam_PlayerAndEnemyInTheCone_ArePushedByTheHeavyTier()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;
            var inFront = targets[1];
            targets[2].DiscardWithoutReward();
            Place(inFront, origin + Vector3.forward * 2f);
            float playerPush = -1f, enemyPush = -1f;
            game.player.AttackReceived += info => playerPush = info.knockDistance;
            inFront.AttackReceived += info => enemyPush = info.knockDistance;

            Arm(targets[0], game, EnemyKind.Stomper, origin + Vector3.forward * 4f);
            await Until(() => playerPush >= 0f && enemyPush >= 0f, 6f, "the slam lands on the player and the enemy in front");

            Assert.That(playerPush, Is.EqualTo(KnockbackRules.Active.heavy), "the player is thrown by the heavy tier");
            Assert.That(enemyPush, Is.EqualTo(KnockbackRules.Active.heavy), "and so is the enemy in the cone");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task RammerCharge_HitsThePlayer_PushesByTheLightTierAndStuns()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            float push = -1f;
            game.player.AttackReceived += info => push = info.knockDistance;

            Arm(targets[0], game, EnemyKind.Rammer, game.player.Position + Vector3.forward * 8f);
            await Until(() => push >= 0f, 6f, "the charge lands");

            Assert.That(push, Is.EqualTo(KnockbackRules.Active.light), "the charge pushes the player by the light tier");
            Assert.That(game.player.Stunned, Is.True, "and stuns");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Tyrant_InTheRun_IgnoresFrostAndLightningButTakesFireAndPoison()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var sut = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * 12f);
            Assume.That(sut && sut.IsBoss, Is.True, "the scene keeps the Tyrant");
            sut.enabled = false;
            Assume.That(game.progression.Stats.elementPenetration, Is.Zero, "no Damage Penetration yet");
            var board = ElementStatus.For(sut);

            Assert.That(board.DamageFactor(ElementKind.Frost, game.player), Is.Zero, "the Tyrant takes no Frost damage");
            Assert.That(board.DamageFactor(ElementKind.Lightning, game.player), Is.Zero, "nor any Lightning damage");
            Assert.That(board.DamageFactor(ElementKind.Fire, game.player), Is.EqualTo(1f), "but all of a Fire hit");
            Assert.That(board.DamageFactor(ElementKind.Poison, game.player), Is.EqualTo(1f), "and all of a Poison hit");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReceiveDamage_HitOfAnImmuneElement_DealsNoDamageAndNoStacksWhileOtherElementsStillWork()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var sut = targets[0];
            var board = Bare(sut);
            board.SetImmunity(ElementKind.Frost, 1f);
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            Assume.That(game.progression.Stats.elementPenetration, Is.Zero, "no Damage Penetration yet");
            float hp = sut.Hp;

            sut.ReceiveDamage(Hit(game, 10f, ElementKind.Frost));
            float afterFrost = sut.Hp, frost = board.Frost;
            sut.ReceiveDamage(Hit(game, 10f, ElementKind.Fire));

            Assert.That(afterFrost, Is.EqualTo(hp), "a Frost hit deals nothing to a Frost-immune body");
            Assert.That(frost, Is.Zero, "nor leaves any Frost");
            Assert.That(sut.Hp, Is.EqualTo(hp - 10f).Within(.01f), "a Fire hit still deals its damage");
            Assert.That(board.Fire, Is.GreaterThan(0f), "and still burns");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReceiveDamage_ImmuneElementWithDamagePenetration_DealsThatShareButStillNoStacks()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var sut = targets[0];
            var board = Bare(sut);
            board.SetImmunity(ElementKind.Frost, 1f);
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            game.progression.Stats.elementPenetration = .4f;
            float hp = sut.Hp;

            sut.ReceiveDamage(Hit(game, 10f, ElementKind.Frost));

            Assert.That(hp - sut.Hp, Is.EqualTo(4f).Within(.01f), "40% of the Frost damage gets through");
            Assert.That(board.Frost, Is.Zero, "but Damage Penetration never adds a status");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReceiveDamage_HollowWithFullPenetration_TakesTheFrostDamageButNeverAnyFrost()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var sut = targets[0];
            sut.Configure(game.waves.DefinitionOf(EnemyKind.Hollow), 1f, false);
            sut.enabled = false;
            var board = Bare(sut);
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            float lightningShare = board.DamageFactor(ElementKind.Lightning, game.player);
            game.progression.Stats.elementPenetration = 1f;
            float hp = sut.Hp;

            sut.ReceiveDamage(Hit(game, 10f, ElementKind.Frost));

            Assert.That(lightningShare, Is.Zero, "without Damage Penetration the Prophet takes no Lightning damage");
            Assert.That(hp - sut.Hp, Is.EqualTo(10f).Within(.01f), "full Damage Penetration lets all the Frost damage through");
            Assert.That(board.Frost, Is.Zero, "yet the boss never gets any Frost");
            Assert.That(board.Frozen, Is.False, "and is never frozen");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Tick_BurnOnABodyThatTurnedFireImmune_DealsNoDamage()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var sut = targets[0];
            var board = Bare(sut);
            board.Add(ElementKind.Fire, 4f, game.player);
            Assume.That(board.Fire, Is.GreaterThan(0f), "the stacks landed before the immunity");
            board.SetImmunity(ElementKind.Fire, 1f);
            float hp = sut.Hp;

            board.Tick(1f);

            Assert.That(sut.Hp, Is.EqualTo(hp));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightningJump_FromAStruckEnemy_SparesALightningImmuneNeighbourButNotAnother()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 at = game.player.Position + Vector3.forward * 6f;
            var struck = targets[0];
            var immune = targets[1];
            var other = targets[2];
            Place(struck, at);
            Place(immune, at + Vector3.forward * 2f);
            Place(other, at - Vector3.forward * 2f);
            Bare(struck);
            Bare(other);
            var immuneBoard = Bare(immune);
            immuneBoard.SetImmunity(ElementKind.Lightning, 1f);
            float immuneHp = immune.Hp, otherHp = other.Hp;

            struck.ReceiveDamage(Hit(game, 10f, ElementKind.Lightning));

            Assert.That(other.Hp, Is.LessThan(otherHp), "the Lightning jumps to the enemy beside it");
            Assert.That(immune.Hp, Is.EqualTo(immuneHp), "but deals nothing to the Lightning-immune one");
            Assert.That(immuneBoard.Lightning, Is.Zero, "nor stacks on it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightningJump_ThroughALightningImmuneEnemy_StopsThere()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var struck = targets[0];
            var immune = targets[1];
            var beyond = targets[2];
            float jump = ElementStatus.For(struck).Rules.lightningJumpRadius;
            Vector3 at = game.player.Position + Vector3.forward * 5f;

            Place(struck, at);
            Place(immune, at + Vector3.forward * (jump - 1f));
            Place(beyond, immune.Position + Vector3.forward * (jump - 1f));
            Assume.That(Query.Flat(beyond.Position - struck.Position).magnitude, Is.GreaterThan(jump + 1f), "beyond is out of the struck enemy's reach");
            Bare(struck);
            Bare(beyond);
            Bare(immune).SetImmunity(ElementKind.Lightning, 1f);
            float beyondHp = beyond.Hp;

            struck.ReceiveDamage(Hit(game, 10f, ElementKind.Lightning));

            Assert.That(beyond.Hp, Is.EqualTo(beyondHp), "the immune enemy does not pass the Lightning on");
            Assert.That(ElementStatus.For(beyond).Lightning, Is.Zero, "no stacks reach past it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightningHit_OnALightningImmuneEnemy_DoesNotJumpToItsNeighbour()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[2].DiscardWithoutReward();
            var immune = targets[0];
            var neighbour = targets[1];
            Vector3 at = game.player.Position + Vector3.forward * 5f;
            Place(immune, at);
            Place(neighbour, at + Vector3.forward * 2f);
            Bare(immune).SetImmunity(ElementKind.Lightning, 1f);
            Bare(neighbour);
            float neighbourHp = neighbour.Hp;

            immune.ReceiveDamage(Hit(game, 10f, ElementKind.Lightning));

            Assert.That(neighbour.Hp, Is.EqualTo(neighbourHp), "a Lightning hit on an immune body goes nowhere");
            Assert.That(ElementStatus.For(neighbour).Lightning, Is.Zero, "and puts no stacks on the neighbour");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StormCall_BoltsOnALightningImmuneTarget_DealNoDamageAndNoStacks()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var sut = targets[0];
            Place(sut, game.player.Position + Vector3.forward * 5f);
            var board = Bare(sut);
            board.SetImmunity(ElementKind.Lightning, 1f);
            game.player.DebugSetAimPoint(sut.Position);
            Assume.That(game.skills.Learn(SkillId.StormCall), Is.True, "Storm Call is learned");
            int expected = Mathf.RoundToInt(game.skills.Catalog.Find(SkillId.StormCall).Strength(1));
            int bolts = 0;
            game.skills.VisualRequested += (id, _, _, _) => bolts += id == SkillId.StormCall ? 1 : 0;
            float hp = sut.Hp;

            Assume.That(game.skills.TryCast(0), Is.True, "Storm Call is cast");
            await Until(() => bolts >= expected, 6f, "every bolt falls");

            Assert.That(sut.Hp, Is.EqualTo(hp), "the bolts deal nothing to a Lightning-immune target");
            Assert.That(board.Lightning, Is.Zero, "and add no Lightning");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StormCallLevel3_BoltsOnALightningImmuneTarget_DoNotJumpToTheEnemyBesideIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[2].DiscardWithoutReward();
            var immune = targets[0];
            var beside = targets[1];

            var def = game.skills.Catalog.Find(SkillId.StormCall);
            float radius = def.Radius(3);
            Vector3 cursor = game.player.Position + Vector3.forward * 3f;
            Place(immune, cursor + Vector3.forward * (radius - 1f));
            Place(beside, immune.Position + Vector3.forward * (ElementStatus.For(immune).Rules.lightningJumpRadius - 1f));
            Assume.That(Query.Flat(beside.Position - cursor).magnitude - beside.radius, Is.GreaterThan(radius), "no bolt falls on the enemy beside");
            var immuneBoard = Bare(immune);
            immuneBoard.SetImmunity(ElementKind.Lightning, 1f);
            Bare(beside);
            Assume.That(game.progression.Stats.elementPenetration, Is.Zero, "no Damage Penetration");
            game.skills.ResetRun();
            for (int i = 0; i < 3; i++) Assume.That(game.skills.Learn(SkillId.StormCall), Is.True, "Storm Call is learned and raised");
            int slot = Array.FindIndex(game.skills.Slots, s => !s.Empty && s.Id == SkillId.StormCall);
            Assume.That(game.skills.Slots[slot].Level, Is.EqualTo(3), "level 3: its bolts chain");
            game.player.DebugSetAimPoint(cursor);
            int expected = Mathf.RoundToInt(def.Strength(3));
            int bolts = 0;
            game.skills.VisualRequested += (id, _, _, _) => bolts += id == SkillId.StormCall ? 1 : 0;
            float immuneHp = immune.Hp, besideHp = beside.Hp;

            Assume.That(game.skills.TryCast(slot), Is.True, "Storm Call is cast");
            await Until(() => bolts >= expected, 6f, "every bolt falls");

            Assert.That(immune.Hp, Is.EqualTo(immuneHp), "the bolts deal nothing to the Lightning-immune target");
            Assert.That(immuneBoard.Lightning, Is.Zero, "nor add any Lightning to it");
            Assert.That(beside.Hp, Is.EqualTo(besideHp), "and the Lightning does not jump on from it (owner 2026-10-25: an immune boss does not conduct)");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ShatterTwist_IceStoneCrashingIntoAFrostImmuneEnemy_HurtsItNot()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[2].DiscardWithoutReward();
            var stone = targets[0];
            var immune = targets[1];                                                                     
            Place(stone, game.player.Position + Vector3.forward * 3f);
            stone.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            var immuneBoard = Bare(immune);
            immuneBoard.SetImmunity(ElementKind.Frost, 1f);
            Freeze(stone, game);
            var tuning = game.runWeapons.upgrades;
            game.runWeapons.ClearWaveEffects();

            var shatter = new RunWeapon { form = WeaponForm.CryoCannon, variant = 0, revealed = true, axes = UpgradeAxis.Twist,
                element = ElementKind.Frost, builtInElement = true, rounds = game.runWeapons.ClipSize(WeaponForm.CryoCannon, 0) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = shatter;
            float shard = game.runWeapons.HitDamage(shatter) * tuning.shatterFrozenBonus;
            float hp = immune.Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => stone.State == CharacterState.Thrown, 1f, "a shard throws the frozen enemy");
            await Until(() => stone.State != CharacterState.Thrown, 3f, "the stone crashes into the Frost-immune enemy");
            await Seconds(.3f);

            Assume.That(stone.maxHp - stone.Hp, Is.GreaterThanOrEqualTo(shard - .01f), "the stone stopped and burst");
            Assert.That(immune.Hp, Is.EqualTo(hp), "neither the Frost weapon's crash nor its burst hurts a Frost-immune enemy");
            Assert.That(immuneBoard.Frost, Is.Zero, "nor leaves any Frost on it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PenetrationOffer_FromWave11_FollowsTheBossesFixedImmunities()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            for (int i = 0; i < 20 && game.waves.WaveIndex + 1 < Mathf.Min(game.penetrationOfferWave, game.waves.WaveCount); i++) game.waves.BeginNextWave();
            Assume.That(game.waves.WaveIndex + 1, Is.EqualTo(game.penetrationOfferWave), "the wave from which the card may show (11)");
            var progression = game.progression;
            game.skills.ResetRun();
            game.runWeapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];

            Hold(game, WeaponForm.CryoCannon, ElementKind.Frost);
            RefreshPenetrationOffer(game);
            bool frostMay = progression.MayOfferPenetration, frostMust = progression.MustOfferPenetration;
            Hold(game, WeaponForm.Flamethrower, ElementKind.Fire);
            RefreshPenetrationOffer(game);
            bool fireMay = progression.MayOfferPenetration;
            Hold(game, WeaponForm.Rifle, ElementKind.None);
            game.skills.Learn(SkillId.FrostNova);
            RefreshPenetrationOffer(game);
            bool novaMay = progression.MayOfferPenetration;
            game.skills.Learn(SkillId.StormCall);
            RefreshPenetrationOffer(game);
            bool stormMay = progression.MayOfferPenetration, stormMust = progression.MustOfferPenetration;

            Assert.That(frostMay, Is.True, "a Frost weapon carries an element both bosses ignore");
            Assert.That(frostMust, Is.True, "and when it is the only damaging element, the card is forced");
            Assert.That(fireMay, Is.False, "a Fire weapon does not: no boss ignores Fire");
            Assert.That(novaMay, Is.False, "Frost Nova only adds stacks, which the card never lets through");
            Assert.That(stormMay, Is.True, "Storm Call's bolts deal Lightning damage");
            Assert.That(stormMust, Is.True, "and Frost Nova does not keep the card from being forced");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PenetrationOffer_BeforeWave11_OpensOnlyWhileAVoidPortalIsOpen()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var world = WorldMap.Active;
            var progression = game.progression;
            Assume.That(game.waves.WaveIndex + 1, Is.LessThan(game.penetrationOfferWave), "a wave long before the Tyrant");
            game.skills.ResetRun();
            game.runWeapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
            Hold(game, WeaponForm.CryoCannon, ElementKind.Frost);

            RefreshPenetrationOffer(game);
            bool shutMay = progression.MayOfferPenetration;
            var hole = world.holes.First(h => h && h.Phase == HolePhase.Active);
            hole.Feed(hole.feedsToOpen, game.waves.WaveIndex + 1);
            hole.Tick(2f);
            Assume.That(world.HasOpenPortal, Is.True, "a void portal is open: the Nash Prophet is within reach");
            RefreshPenetrationOffer(game);
            bool openMay = progression.MayOfferPenetration, openMust = progression.MustOfferPenetration;
            world.ClosePortals();
            RefreshPenetrationOffer(game);
            bool closedMay = progression.MayOfferPenetration;

            Assert.That(shutMay, Is.False, "no boss ahead yet: the card waits");
            Assert.That(openMay, Is.True, "with a portal open, a Frost weapon may get the card in any wave");
            Assert.That(openMust, Is.True, "and when Frost is its only damaging element, the card is forced");
            Assert.That(closedMay, Is.False, "once the portal is gone the card waits for the Tyrant's wave again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task StatCard_Penetration_IsNamedDamagePenetrationAndLetsOnlyDamageThrough()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            Assume.That(ArenaText.IsChinese, Is.True, "the Chinese scene");
            var sut = new StatCardOffer(StatId.ElementPenetration, CardRarity.Common, .2f);
            var english = (string[])typeof(ArenaTerms).GetField("StatEn", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            Assert.That(sut.Title, Is.EqualTo("Damage Penetration"), "card title");
            Assert.That(sut.Description, Does.Contain("Damage Penetration +20%").And.Contain("status"), "card text: a damage share, never a status");
            Assert.That(english[(int)StatId.ElementPenetration], Is.EqualTo("Damage Penetration"), "English name");
        }

        static DamageInfo Hit(GameManager game, float amount, ElementKind element)
        {
            var info = DamageInfo.Attack(amount, game.player);
            info.element = element;
            info.baseDamage = amount;
            return info;
        }

        static ElementStatus Bare(Combatant body)
        {
            var board = ElementStatus.For(body);
            board.SetShield(0f);
            board.ClearStatuses();
            return board;
        }

        static void Freeze(Combatant body, GameManager game)
        {
            var status = ElementStatus.For(body);
            status.ClearStatuses();
            status.Add(ElementKind.Frost, status.FrostThreshold + 1f, game.player);
            Assume.That(status.Frozen, Is.True, "the body is frozen");
        }

        static float ScaleAfterConfigure(Enemy enemy, GameManager game, EnemyKind kind, bool elite = false, bool mini = false)
        {
            enemy.Configure(game.waves.DefinitionOf(kind), 1f, elite, ElementKind.None, mini);
            enemy.enabled = false;
            return enemy.knockbackScale;
        }

        static Enemy Ready(Enemy enemy, GameManager game, EnemyKind kind, Vector3 at, bool elite = false, bool mini = false)
        {
            enemy.Configure(game.waves.DefinitionOf(kind), 1f, elite, ElementKind.None, mini);
            enemy.enabled = false;
            Place(enemy, at);
            enemy.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            return enemy;
        }

        static void Arm(Enemy enemy, GameManager game, EnemyKind kind, Vector3 at)
        {
            enemy.Configure(game.waves.DefinitionOf(kind), 1f, false);
            V3CombatSmokeTest.Teleport(enemy, at);
            enemy.moveSpeed = 0f;
            enemy.enabled = true;
            Physics.SyncTransforms();
        }

        static void Place(Combatant body, Vector3 at)
        {
            body.ClearWaveState();
            V3CombatSmokeTest.Teleport(body, new Vector3(at.x, 0f, at.z));
            Physics.SyncTransforms();
        }

        static float Launch(Combatant body) => Speed(body, Vector3.forward);

        static float Speed(Combatant body, Vector3 direction) => Vector3.Dot(Query.Flat(body.GetComponent<Rigidbody>().linearVelocity), direction);

        static void Hold(GameManager game, WeaponForm form, ElementKind element) =>
            game.runWeapons.Hand = new[] { new RunWeapon { form = form, revealed = true, element = element, builtInElement = element != ElementKind.None }, null };

        static void RefreshPenetrationOffer(GameManager game) =>
            typeof(GameManager).GetMethod("UpdatePenetrationEligibility", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
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
