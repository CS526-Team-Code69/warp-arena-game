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
    [Category("V3Smoke")]
    public class TyrantRedesignTest
    {
        const string ArenaScene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test]
        public void TyrantMoveChoice_ByDistance_CloseSweepsMiddleChargesFarThrows_PhaseTwoMovesOnlyInPhaseTwo()
        {
            var b = new BossTuning();
            float close = Enemy.TyrantCloseReach(1, b) - .1f, middle = Enemy.TyrantMiddleReach(1, b) - .1f, far = Enemy.TyrantMiddleReach(1, b) + 1f;
            for (int step = 0; step < 20; step++)
            {
                float roll = step / 20f;
                Assert.That(Enemy.TyrantMoveChoice(1, close, roll, true, true, b), Is.EqualTo(Enemy.TailSweepMove), "phase 1 close: the sweep only");
                Assert.That(Enemy.TyrantMoveChoice(1, middle, roll, true, true, b), Is.EqualTo(Enemy.RampageChargeMove), "phase 1 middle: the charge only");
                Assert.That(Enemy.TyrantMoveChoice(1, far, roll, true, true, b), Is.EqualTo(Enemy.StarThrowMove), "phase 1 far: the meteor only");
                Assert.That(Enemy.TyrantMoveChoice(2, close, roll, true, true, b), Is.EqualTo(Enemy.TailSweepMove).Or.EqualTo(Enemy.GroundPoundMove));
                Assert.That(Enemy.TyrantMoveChoice(2, Enemy.TyrantMiddleReach(2, b) - .1f, roll, false, true, b), Is.EqualTo(Enemy.RampageChargeMove),
                    "no scepter while it is not ready");
                Assert.That(Enemy.TyrantMoveChoice(2, Enemy.TyrantMiddleReach(2, b) + 1f, roll, true, false, b), Is.EqualTo(Enemy.StarThrowMove),
                    "no Astral Guard without minions to devour");
            }

            Assert.That(Enemy.TyrantMoveChoice(2, close, b.poundShare - .01f, true, true, b), Is.EqualTo(Enemy.GroundPoundMove), "phase 2 close: some pounds");
            Assert.That(Enemy.TyrantMoveChoice(2, close, b.poundShare + .01f, true, true, b), Is.EqualTo(Enemy.TailSweepMove));
            Assert.That(Enemy.TyrantMoveChoice(2, middle, b.scepterShare - .01f, true, true, b), Is.EqualTo(Enemy.RoyalScepterMove), "phase 2 middle: some scepters");
            Assert.That(Enemy.TyrantMoveChoice(2, Enemy.TyrantMiddleReach(2, b) + 1f, b.astralShare - .01f, true, true, b),
                Is.EqualTo(Enemy.AstralGuardMove), "phase 2 far: some Astral Guards");
            Assert.That(Enemy.TyrantCloseReach(2, b), Is.GreaterThan(Enemy.TyrantCloseReach(1, b)), "the bigger phase-2 sweep widens the close band");
            Assert.That(Enemy.TyrantMiddleReach(2, b), Is.GreaterThan(Enemy.TyrantMiddleReach(1, b)), "and the longer phase-2 charge the middle band");
        }

        [Test]
        public void TyrantTuning_PhaseTwoMovesAreMuchBiggerAndComeMuchMoreOften_KnockbackTiersFollowTheDocument()
        {
            var b = new BossTuning();
            Assert.That(Enemy.TyrantPhaseCount, Is.EqualTo(2), "two phases");
            Assert.That(b.sweepRangePhase2, Is.GreaterThan(b.sweepRange * 1.3f), "the sweep grows a lot");
            Assert.That(b.rushMaxRunPhase2, Is.GreaterThan(b.rushMaxRun * 1.3f), "so does the charge");
            Assert.That(b.rushSlamRadiusPhase2, Is.GreaterThan(b.rushSlamRadius * 1.3f));
            Assert.That(b.meteorRadiusPhase2, Is.GreaterThan(b.meteorRadius * 1.3f), "and the meteor");

            float walk = b.tyrantMoveGap, shortWalk = b.tyrantMoveGap * b.tyrantEnragedGapShare, quick = b.tyrantEnragedWindupShare;
            float sweep = (b.sweepWindup + b.sweepSeconds + walk) / (b.sweepWindup * quick + b.sweepSeconds + shortWalk);
            float meteor = (b.meteorWindup + walk) / (b.meteorWindup * quick + shortWalk);

            float motion = (10f - b.rushLeapDistance) / (b.rushSpeed * .5f) + b.rushLeapFlight;
            float charge = (b.rushWindup + motion + b.rushRecovery + walk) / ((b.rushWindup + b.rushRecovery) * quick + motion + shortWalk);
            Assert.That(sweep, Is.GreaterThanOrEqualTo(1.5f), "enraged, its Tail Sweeps come much more often");
            Assert.That(meteor, Is.GreaterThanOrEqualTo(1.5f), "so do its Star Throws");
            Assert.That(charge, Is.GreaterThanOrEqualTo(1.4f), "and its Rampage Charges, whose run and leap keep their own time");
            Assert.That(b.tyrantEnragedDamage, Is.GreaterThan(1f), "and hit harder");

            Assert.That(b.sweepKnock, Is.EqualTo(KnockTier.Heavy), "document 5.3: the Tail Sweep is heavy");
            Assert.That(b.rushSlamKnock, Is.EqualTo(KnockTier.Fly), "document 5.3: the charge's landing slam is fly");
            Assert.That(b.sweepArc, Is.EqualTo(150f), "the fan matches the saved sector art");
        }

        [Test]
        public void EncounterBalance_TyrantBudget_CountsOneShieldRefill_ItsShieldsAndHpStillLastNinetySeconds()
        {
            var balance = ScriptableObject.CreateInstance<EncounterBalance>();
            var tyrant = ScriptableObject.CreateInstance<EnemyDefinition>();
            try
            {
                tyrant.kind = EnemyKind.Tyrant;
                tyrant.startShieldShare = .1f;
                balance.tyrantPhaseShieldShare = .15f;
                Assert.That(balance.ShieldBudgetShare(tyrant, ElementKind.None), Is.EqualTo(.1f + .15f).Within(1e-5f),
                    "its opening shield and one refill (two phases), not two");
                int wave = balance.BudgetWave(tyrant, 15);
                float hp = balance.MaxHp(tyrant, wave, false);
                float shields = hp * (tyrant.startShieldShare + balance.tyrantPhaseShieldShare);
                Assert.That((hp + shields / balance.ShieldDirectFactor) / balance.DpsAt(wave), Is.EqualTo(balance.bossSeconds).Within(1f),
                    "HP and the shields it really gets add up to its boss budget");
            }
            finally
            {
                Object.DestroyImmediate(balance);
                Object.DestroyImmediate(tyrant);
            }
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task Immunities_AlwaysFrostAndLightning_NoWaveAnnouncesThem_ShownDuringItsFight()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var waves = game.waves;
            Assert.That(waves.TyrantImmunities, Is.EqualTo(new[] { ElementKind.Frost, ElementKind.Lightning }), "fixed, in a fixed order");

            var body = targets[0];
            body.Configure(waves.tyrant.definition, 1f, false);
            body.enabled = false;
            var board = ElementStatus.For(body);
            Assert.That(board.ElementFactor(ElementKind.Frost), Is.Zero, "a Tyrant ignores Frost from its own Configure on");
            Assert.That(board.ElementFactor(ElementKind.Lightning), Is.Zero, "and Lightning");
            Assert.That(board.ElementFactor(ElementKind.Fire), Is.EqualTo(1f), "other elements still work");

            var waveField = typeof(WaveManager).GetField("<WaveIndex>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var phaseField = typeof(WaveManager).GetField("<Phase>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            object wave = waveField.GetValue(waves), phase = phaseField.GetValue(waves);
            try
            {
                waveField.SetValue(waves, 10);
                var snapshot = game.UiSnapshot;
                Assert.That(snapshot.wave, Is.EqualTo(11));
                Assert.That(snapshot.bossImmunities, Is.Empty, "wave 11 no longer announces the Tyrant's immunities");
                Assert.That(snapshot.bossImmunityIndices.All(i => i < 0), Is.True, "and shows no immunity icons before its fight");

                phaseField.SetValue(waves, WavePhase.FinalBoss);
                snapshot = game.UiSnapshot;
                Assert.That(snapshot.bossImmunities, Is.Not.Empty, "its fight shows them");
                Assert.That(snapshot.bossImmunityIndices.Take(2), Is.EqualTo(new[] { (int)ElementKind.Frost - 1, (int)ElementKind.Lightning - 1 }),
                    "the Frost and Lightning icons");
            }
            finally
            {
                waveField.SetValue(waves, wave);
                phaseField.SetValue(waves, phase);
            }
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task MoveChoice_ByDistanceAlone_AtMiddleDistanceOffScreen_ItStillCharges()
        {
            var (game, targets, tyrant) = await TyrantSandbox(5f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            var player = game.player;

            var b = game.waves.bosses;
            Vector3 at = Vector3.zero;
            bool found = false;
            for (float distance = Enemy.TyrantMiddleReach(1, b) - .2f; distance > Enemy.TyrantCloseReach(1, b) + .5f && !found; distance -= .5f)
            for (int a = 0; a < 360 && !found; a += 15)
            {
                at = Flat(player.Position) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * distance;
                found = world.IsWalkable(at, tyrant.Radius) && !(SkillWorldAPI.IsOnScreen?.Invoke(at) ?? true);
            }

            Assume.That(found, Is.True, "open ground off screen at middle distance");
            Place(tyrant, at);
            Assume.That(SkillWorldAPI.IsOnScreen(tyrant.Position), Is.False, "it stands off screen");
            tyrant.ForceNextBossMove(-1);
            SetPrivate(tyrant, "cooldown", 0f);
            await Until(() => tyrant.WindingUp, 3f, "its own choice of move");
            Assert.That(tyrant.BossAttackIndex, Is.EqualTo(Enemy.RampageChargeMove), "middle distance means the charge, on screen or not");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task TailSweep_FanWarning_ThenCrossesLeftToRight_HitsYouOnceByTheHeavyTier_SparesItsMinions()
        {
            var (game, targets, tyrant) = await TyrantSandbox(5f);
            var player = game.player;
            var b = game.waves.bosses;
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            Vector3 toPlayer = Flat(player.Position - tyrant.Position).normalized;
            var minion = targets[0];
            V3CombatSmokeTest.Teleport(minion, tyrant.Position + Quaternion.Euler(0f, -40f, 0f) * toPlayer * 4f);
            Physics.SyncTransforms();
            float minionHp = minion.Hp;
            var hits = new List<DamageInfo>();
            void Count(DamageInfo hit) { if (hit.dealer?.Owner == tyrant) hits.Add(hit); }
            player.AttackReceived += Count;
            try
            {
                Force(tyrant, Enemy.TailSweepMove);
                await Until(() => tyrant.WindingUp && tyrant.CleaveTelegraph, 3f, "the sweep wind-up");
                Assert.That(tyrant.TelegraphActive, Is.True, "the fan shows on the ground");
                Assert.That(tyrant.TelegraphRange, Is.EqualTo(b.sweepRange).Within(.01f), "as far as the tail reaches");
                Assert.That(hits, Is.Empty, "nothing lands during the wind-up");

                await Until(() => tyrant.TailSweeping, 3f, "the tail starts to sweep");
                var angles = new List<float>();
                float start = Time.time, deadline = Time.realtimeSinceStartup + 3f;
                while (tyrant.TailSweeping)
                {
                    if (Time.realtimeSinceStartup > deadline) Assert.Fail("The sweep never ended.");
                    angles.Add(tyrant.TailAngle);
                    await Awaitable.FixedUpdateAsync();
                }

                float seconds = Time.time - start;
                Assert.That(angles.Count, Is.GreaterThan(2), "the tail crosses the fan over several steps");
                Assert.That(angles[0], Is.LessThan(0f), "from its left edge");
                for (int i = 1; i < angles.Count; i++) Assert.That(angles[i], Is.GreaterThanOrEqualTo(angles[i - 1]), "always turning to the right");
                Assert.That(tyrant.TailAngle, Is.EqualTo(b.sweepArc * .5f).Within(.01f), "to its right edge");
                Assert.That(seconds, Is.GreaterThanOrEqualTo(b.sweepSeconds * .7f), "it takes a moment, it is not instant");
                await Awaitable.FixedUpdateAsync();

                Assert.That(hits, Has.Count.EqualTo(1), "you are hit once as the tail passes");
                Assert.That(hits[0].knockDistance, Is.EqualTo(KnockbackRules.Active.heavy), "and thrown by the heavy tier (document 5.3)");
                Assert.That(minion.Hp, Is.EqualTo(minionHp), "its own minion in the fan is spared");
            }
            finally { player.AttackReceived -= Count; }
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task TailSweep_BreaksOnlyTheWallPiecesItTouches_CollisionAndPathsFollow_ANewFightStartsWhole()
        {
            var (game, targets, tyrant) = await TyrantSandbox(10f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            var player = game.player;
            var b = game.waves.bosses;
            Assume.That(FindWallToSweep(world, tyrant.Radius, b, out var bossAt, out var playerAt, out var inWall, out var farPart), Is.True,
                "the map has a wall with open ground in front of it");
            Place(tyrant, bossAt);
            Place(player, playerAt);
            Assume.That(world.IsWall(inWall), Is.True, "the point is inside the wall");
            Assume.That(Physics.CheckSphere(Query.AtCastHeight(inWall), .2f, GameManager.WallMask, QueryTriggerInteraction.Ignore), Is.True,
                "and the wall's collider is there");
            var blocked = (bool[])typeof(MapGrid).GetField("blocked", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world.Grid);
            var blockedBefore = (bool[])blocked.Clone();

            Force(tyrant, Enemy.TailSweepMove);
            await Until(() => tyrant.TailSweeping, 4f, "the sweep");
            await Until(() => !tyrant.TailSweeping, 3f, "the tail crosses the fan");
            await Awaitable.FixedUpdateAsync();

            Assert.That(world.IsWall(inWall), Is.False, "the wall piece the tail touched is gone");
            Assert.That(Physics.CheckSphere(Query.AtCastHeight(inWall), .2f, GameManager.WallMask, QueryTriggerInteraction.Ignore), Is.False,
                "and so is its collider: bodies and shots pass");
            int opened = Enumerable.Range(0, blocked.Length).Count(i => blockedBefore[i] && !blocked[i]);
            Assert.That(opened, Is.GreaterThan(0), "enemy paths were rebuilt through the broken wall");
            if (farPart.HasValue)
            {
                Assert.That(world.IsWall(farPart.Value), Is.True, "the part of the same wall out of the tail's reach still stands");
                Assert.That(Physics.CheckSphere(Query.AtCastHeight(farPart.Value), .2f, GameManager.WallMask, QueryTriggerInteraction.Ignore),
                    Is.True, "with a collider");
            }

            await Seconds(1f);
            Assert.That(world.IsWall(inWall), Is.False, "it stays broken for the rest of the fight");

            tyrant.Configure(tyrant.definition, 1f, false);
            tyrant.enabled = false;
            Physics.SyncTransforms();
            Assert.That(world.IsWall(inWall), Is.True, "a new Tyrant fight starts on the whole map");
            Assert.That(Physics.CheckSphere(Query.AtCastHeight(inWall), .2f, GameManager.WallMask, QueryTriggerInteraction.Ignore), Is.True);
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RampageCharge_LineToTheLandingCircle_RunsThenLeapsAndSlams_YouAndItsMinionAreThrownByTheFlyTier()
        {
            var (game, targets, tyrant) = await TyrantSandbox(10f);
            var player = game.player;
            var b = game.waves.bosses;
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();

            Assume.That(await OnChargeLane(game, tyrant, 10f, 1.5f), Is.True, "a lane wider than its body");
            var minion = targets[0];
            V3CombatSmokeTest.Teleport(minion, tyrant.Position + Vector3.right * 12f);                                             
            Physics.SyncTransforms();
            float playerPush = -1f, minionPush = -1f;
            void PlayerHit(DamageInfo hit) { if (hit.dealer?.Owner == tyrant) playerPush = hit.knockDistance; }
            void MinionHit(DamageInfo hit) { if (hit.dealer?.Owner == tyrant) minionPush = hit.knockDistance; }
            player.AttackReceived += PlayerHit;
            minion.AttackReceived += MinionHit;
            try
            {
                Vector3 from = Flat(tyrant.Position), aim = Flat(player.Position);
                Force(tyrant, Enemy.RampageChargeMove);
                await Until(() => tyrant.WindingUp && tyrant.LineTelegraph, 3f, "the charge wind-up");
                Assert.That(tyrant.TelegraphActive, Is.True, "the line shows on the ground");
                Assert.That(tyrant.RushLandingShown, Is.True, "with its landing circle");
                Vector3 landing = tyrant.RushLanding;
                Assert.That(tyrant.TelegraphRange, Is.EqualTo(Flat(landing - from).magnitude).Within(.1f), "the line reaches the landing point");
                Assert.That(Vector3.Distance(Vector3.Project(landing - from, aim - from) + from, landing), Is.LessThan(.1f),
                    "on the line toward where you stood");
                Assert.That(Flat(landing - from).magnitude, Is.LessThanOrEqualTo(Flat(aim - from).magnitude + .1f), "never past you");
                Assert.That(tyrant.RushSlamReach, Is.EqualTo(b.rushSlamRadius).Within(.01f), "the phase-1 slam");

                Vector3 ahead = (aim - from).normalized, side = Vector3.Cross(Vector3.up, ahead);
                Place(player, landing + side * 3f);
                V3CombatSmokeTest.Teleport(minion, landing - side * 3f);
                Physics.SyncTransforms();
                float minionHp = minion.Hp;

                await Until(() => tyrant.Charging, 3f, "the run");
                await Until(() => tyrant.RushLeaping, 4f, "the leap at the end of the run");
                Assert.That(tyrant.RushLandingShown, Is.True, "the circle stays while it flies");
                await Until(() => !tyrant.RushLeaping, 3f, "the landing");
                await Awaitable.FixedUpdateAsync();

                Assert.That(Flat(tyrant.Position - landing).magnitude, Is.LessThan(.5f), "it lands on the circle");
                Assert.That(tyrant.GetComponent<Rigidbody>().isKinematic, Is.False, "back on the ground");
                Assert.That(tyrant.GetComponent<CapsuleCollider>().enabled, Is.True);
                Assert.That(playerPush, Is.EqualTo(KnockbackRules.Active.fly), "the slam throws you by the fly tier (document 5.3)");
                Assert.That(minionPush, Is.EqualTo(KnockbackRules.Active.fly), "and its own minion too (doc 13.2)");
                Assert.That(minion.Hp, Is.LessThan(minionHp), "which is hurt");
            }
            finally
            {
                player.AttackReceived -= PlayerHit;
                minion.AttackReceived -= MinionHit;
            }
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RampageCharge_IntoABulwark_StunsIt_StunnedItTakesMoreDamageNeitherWalksNorActs_ThenActsAgain()
        {
            var (game, targets, tyrant) = await TyrantSandbox(10f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            Assume.That(await OnChargeLane(game, tyrant, 10f, 0f), Is.True, "a lane as wide as its body");
            Vector3 toPlayer = Flat(game.player.Position - tyrant.Position).normalized;

            WorldMap.Active.CreateWall(tyrant.Position + toPlayer * 5f, 3f, 30f, 0f, tyrant);
            Physics.SyncTransforms();

            Force(tyrant, Enemy.RampageChargeMove);
            await Until(() => tyrant.WindingUp && tyrant.LineTelegraph, 3f, "the charge wind-up");
            Assert.That(tyrant.TelegraphRange, Is.EqualTo(4.5f).Within(.4f), "the line ends at the bulwark's face");
            Assert.That(tyrant.RushLandingShown, Is.False, "no landing circle: it will crash, not leap");
            await Until(() => tyrant.BossStunned, 5f, "the run hits the bulwark");
            Assert.That(tyrant.RushLeaping, Is.False, "it does not leap");

            var board = ElementStatus.For(tyrant);
            board.SetShield(0f);
            float hp = tyrant.Hp;
            tyrant.ReceiveDamage(new DamageInfo { amount = 10f, isAttack = true, ignoreShield = true, kind = HitKind.Blunt });
            Assert.That(hp - tyrant.Hp, Is.EqualTo(10f * b.tyrantStunDamageTaken).Within(.01f), "stunned, it takes more damage");

            int crosses = game.waves.preparedMarkers.Count(m => m && m.gameObject.activeSelf);
            Vector3 at = Flat(tyrant.Position);
            Force(tyrant, Enemy.RoyalScepterMove);
            float until = Time.time + Mathf.Min(1f, b.tyrantWallStun * .5f);
            while (Time.time < until)
            {
                Assert.That(tyrant.WindingUp, Is.False, "no move starts while stunned, not even a summon");
                Assert.That(Flat(tyrant.Position - at).magnitude, Is.LessThan(.05f), "and it does not walk");
                await Awaitable.FixedUpdateAsync();
            }

            Assert.That(game.waves.preparedMarkers.Count(m => m && m.gameObject.activeSelf), Is.EqualTo(crosses), "no summons either");
            await Until(() => !tyrant.BossStunned, b.tyrantWallStun + 1f, "the stun wears off");
            await Until(() => tyrant.WindingUp, 3f, "it acts again");
            Assert.That(tyrant.BossAttackIndex, Is.EqualTo(Enemy.RoyalScepterMove), "the move it was told to make waited for the stun");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RampageCharge_ABulwarkBesideTheCentreLineInItsBodysWay_StunsItAsWell()
        {
            var (game, targets, tyrant) = await TyrantSandbox(10f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            Assume.That(await OnChargeLane(game, tyrant, 10f, 0f), Is.True, "a lane as wide as its body");
            Vector3 toPlayer = Flat(game.player.Position - tyrant.Position).normalized, side = Vector3.Cross(Vector3.up, toPlayer);

            const float Beside = 1.5f;
            Assume.That(tyrant.Radius - b.rushGraze, Is.GreaterThan(Beside + .3f), "its body reaches well past the bulwark's end");
            WorldMap.Active.CreateWall(tyrant.Position + toPlayer * 5f + side * (Beside + 1.5f), 3f, 30f, 0f, tyrant);
            Physics.SyncTransforms();
            Assume.That(Query.WallBetween(Query.AtCastHeight(tyrant.Position), Query.AtCastHeight(game.player.Position), out _), Is.False,
                "the centre line itself is clear");

            Force(tyrant, Enemy.RampageChargeMove);
            await Until(() => tyrant.WindingUp && tyrant.LineTelegraph, 3f, "the charge wind-up");
            Assert.That(tyrant.RushLandingShown, Is.False, "the line is as wide as its body and ends at the bulwark: no landing circle");
            Assert.That(tyrant.TelegraphRange, Is.LessThan(5.5f), "it stops before the bulwark's far side");
            await Until(() => tyrant.BossStunned, 5f, "the run hits the bulwark with its side");
            Assert.That(tyrant.RushLeaping, Is.False, "it does not leap over it");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RampageCharge_AtAPlayerInAGravityRing_LandsWhereItCanWalk()
        {
            var (game, targets, tyrant) = await TyrantSandbox(14f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            var player = game.player;
            Assume.That(FindRingApproach(world, tyrant.Radius, game.waves.bosses, out var spot, out var from), Is.True,
                "a gravity ring with open ground along it");

            var body = player.GetComponent<Rigidbody>();
            var constraints = body.constraints;
            try
            {
                body.constraints = RigidbodyConstraints.FreezeAll;
                Place(player, spot);
                Place(tyrant, from);
                await Awaitable.FixedUpdateAsync();
                Force(tyrant, Enemy.RampageChargeMove);
                await Until(() => tyrant.WindingUp && tyrant.LineTelegraph, 3f, "the charge wind-up");
                Assert.That(tyrant.RushLandingShown, Is.True, "it plans a leap, not a crash");
                Assert.That(world.IsWalkable(tyrant.RushLanding, tyrant.Radius), Is.True,
                    "onto ground it can walk on: not the gravity ring it is never pulled out of, no hole, no wall");
                await Until(() => tyrant.RushLeaping, 4f, "the leap");
                await Until(() => !tyrant.RushLeaping, 3f, "the landing");
                await Awaitable.FixedUpdateAsync();
                Assert.That(world.IsWalkable(tyrant.Position, tyrant.Radius), Is.True, "and it stands there");
            }
            finally { body.constraints = constraints; }
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RampageCharge_IntoAHole_TheLineEndsAtItsEdge_ItCrashesThereAndNeverLeaps()
        {
            var (game, targets, tyrant) = await TyrantSandbox(14f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            Assume.That(FindHoleAcross(world, tyrant.Radius, out var hole, out var from, out var spot), Is.True,
                "a black hole with open ground on two sides");
            Place(game.player, spot);
            Place(tyrant, from);
            await Awaitable.FixedUpdateAsync();
            float edge = Flat(hole.Centre - tyrant.Position).magnitude - hole.KillRadius;

            Force(tyrant, Enemy.RampageChargeMove);
            await Until(() => tyrant.WindingUp && tyrant.LineTelegraph, 3f, "the charge wind-up");
            Assert.That(tyrant.RushLandingShown, Is.False, "no landing circle: it will crash at the hole");
            Assert.That(tyrant.TelegraphRange, Is.EqualTo(edge).Within(.6f), "the line ends at the hole's edge");
            await Until(() => tyrant.BossStunned, 5f, "the run stops at the edge");
            Assert.That(tyrant.RushLeaping, Is.False, "it never leaps over the hole");
            Assert.That(tyrant.IsAlive, Is.True, "nor falls in");
            Assert.That(Flat(tyrant.Position - hole.Centre).magnitude - tyrant.Radius, Is.GreaterThan(hole.KillRadius - .3f),
                "its body stopped at the edge");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task GroundPound_RectangleWarning_ThenAShockwaveThatAdvancesBandByBand_HitsYouWhenItGetsThere()
        {
            var (game, targets, tyrant) = await TyrantSandbox(3f);
            var player = game.player;
            var b = game.waves.bosses;
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            Vector3 lane = Flat(tyrant.Position - player.Position).normalized;

            Place(player, tyrant.Position + lane * 7f);
            var minion = targets[0];
            var hits = new List<(DamageInfo hit, int band, float at)>();
            void Count(DamageInfo hit) { if (hit.dealer?.Owner == tyrant) hits.Add((hit, tyrant.PoundStep, Time.time)); }
            player.AttackReceived += Count;
            try
            {
                Force(tyrant, Enemy.GroundPoundMove);
                await Until(() => tyrant.WindingUp && tyrant.PoundShown, 3f, "the pound wind-up");
                Assert.That(tyrant.PoundWidth, Is.EqualTo(b.poundWidth).Within(.01f), "a rectangle as wide as the pound");
                Assert.That(tyrant.PoundLength, Is.GreaterThan(0f).And.LessThanOrEqualTo(b.poundLength + .01f), "and as long, or cut by a wall");
                Assert.That(Vector3.Angle(tyrant.PoundFacing, lane), Is.LessThan(5f), "toward you");
                Assume.That(tyrant.PoundLength, Is.GreaterThan(6f), "room for the shockwave to travel");
                Assert.That(tyrant.PoundStep, Is.EqualTo(-1), "no shockwave during the wind-up");

                Vector3 origin = tyrant.PoundOrigin, facing = tyrant.PoundFacing;
                Place(player, origin + facing * (tyrant.PoundLength * .85f));
                V3CombatSmokeTest.Teleport(minion, origin + facing * 1f);
                Physics.SyncTransforms();
                float minionHp = minion.Hp;
                int farBand = Mathf.FloorToInt(.85f * tyrant.PoundSteps);

                await Until(() => tyrant.PoundStep >= 0, 3f, "the slam");
                float struck = Time.time;
                int last = tyrant.PoundStep;
                var bands = new List<int> { last };
                float deadline = Time.realtimeSinceStartup + 4f;
                while (tyrant.PoundStep >= 0)
                {
                    if (Time.realtimeSinceStartup > deadline) Assert.Fail("The shockwave never ended.");
                    if (tyrant.PoundStep != last) bands.Add(last = tyrant.PoundStep);
                    await Awaitable.FixedUpdateAsync();
                }

                Assert.That(bands.Count, Is.GreaterThan(3), "the shockwave crosses the rectangle band by band");
                for (int i = 1; i < bands.Count; i++) Assert.That(bands[i], Is.GreaterThan(bands[i - 1]), "always forward");
                Assert.That(hits, Has.Count.EqualTo(1), "it reaches you once");
                Assert.That(hits[0].band, Is.GreaterThanOrEqualTo(farBand - 1), "only when it gets to your band, not at the slam");
                Assert.That(hits[0].at - struck, Is.GreaterThanOrEqualTo((farBand - 2) * b.poundStepSeconds), "a while after the slam");
                Assert.That(hits[0].hit.knockDistance, Is.EqualTo(KnockbackRules.Active.Distance(b.poundKnock)), "pushed by its tier");
                Assert.That(minion.Hp, Is.EqualTo(minionHp), "its own minion in the rectangle is spared");
            }
            finally { player.AttackReceived -= Count; }
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RoyalScepter_CallsACrowdOfWaveFourteensKinds_RedCrossesFirst_NoExperience()
        {
            var (game, targets, tyrant) = await TyrantSandbox(10f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var waves = game.waves;
            int expected = tyrant.ScepterCrowd();
            int full = waves.PopulationTargetForWave(waves.WaveCount - 1);
            Assert.That(expected, Is.InRange(1, Mathf.CeilToInt(full * waves.bosses.scepterCrowdShare)), "up to a share of wave 14's crowd");
            int living = waves.LivingCount;

            Force(tyrant, Enemy.RoyalScepterMove);
            await Until(() => tyrant.WindingUp && tyrant.BossAttackIndex == Enemy.RoyalScepterMove, 3f, "the scepter is raised");
            await Until(() => !tyrant.WindingUp, waves.bosses.scepterWindup + 2f, "the call");
            var crosses = waves.preparedMarkers.Where(m => m && m.gameObject.activeSelf && m.bossSummon).ToArray();
            Assert.That(crosses, Is.Not.Empty, "red crosses first");
            Assert.That(crosses.Length, Is.LessThanOrEqualTo(expected), "no more than the crowd");
            Assert.That(waves.LivingCount, Is.EqualTo(living), "nothing appears at once");
            Assert.That(crosses.All(c => waves.waveSet.AllowsKind(waves.WaveCount, c.definition.kind)), Is.True, "only kinds wave 14 lists");
            Assert.That(crosses.Any(c => EnemyRoles.IsSpecial(c.definition.kind)), Is.True, "its specials too, not only the basic crowd");
            Assert.That(crosses.Count(c => c.elite), Is.LessThanOrEqualTo(waves.maxConcurrentElites), "elites stay under their cap");
            Assert.That(tyrant.ScepterCrowd(), Is.LessThanOrEqualTo(full - crosses.Length), "its crowd on the way counts against the next call");

            var tick = typeof(WaveManager).GetMethod("TickPreparedMarkers", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 5 && crosses.Any(c => c.gameObject.activeSelf); i++) tick.Invoke(waves, new object[] { 1.6f });
            Assert.That(crosses.All(c => !c.gameObject.activeSelf), Is.True, "each cross becomes its minion");
            var summons = waves.Alive.Where(e => e && e.IsAlive && e != tyrant).ToArray();
            Assert.That(summons, Is.Not.Empty);
            Assert.That(summons.All(e => e.SuppressRewards), Is.True, "boss summons give no experience");
            Assert.That(summons.All(e => e.BossSummon), Is.True, "and are known as its summons");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RoyalScepter_ABloaterItCalls_StillBlowsUpWhenShotDead_ALabClearLightsNoFuse()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            FeedEachHoleOnce(game);
            foreach (var target in targets) target.DiscardWithoutReward();
            game.player.Invulnerable = true;
            var waves = game.waves;
            var bloater = waves.DefinitionOf(EnemyKind.Bloater);
            Assume.That(bloater, Is.Not.Null, "the roster keeps the Bloater");

            var shot = CallSummon(waves, bloater, game.player.Position + Vector3.right * 12f);
            Assume.That(shot, Is.Not.Null, "a red cross of the scepter's kind becomes a Bloater");
            Assert.That(shot.SuppressRewards && shot.BossSummon, Is.True, "one of its summons: no reward");
            int zones = Enemy.RadiationZones.Count;
            shot.ReceiveDamage(new DamageInfo { amount = shot.maxHp * 10f, isAttack = true, ignoreShield = true, kind = HitKind.Blunt });
            Assert.That(shot.IsAlive, Is.False, "shot dead");
            Assert.That(shot.BloaterWarningActive, Is.True, "its fuse burns all the same (document 10.2: it blows up when killed too)");
            await Until(() => Enemy.RadiationZones.Count > zones, 3f, "the blast and its radiation zone");

            var cleared = CallSummon(waves, bloater, game.player.Position + Vector3.left * 12f);
            Assume.That(cleared, Is.Not.Null, "a second Bloater summon");
            zones = Enemy.RadiationZones.Count;
            waves.ClearLab();
            Assert.That(cleared.IsAlive, Is.False, "the lab clears it");
            Assert.That(cleared.BloaterWarningActive, Is.False, "without lighting its fuse");
            await Seconds(1f);
            Assert.That(Enemy.RadiationZones.Count, Is.LessThanOrEqualTo(zones), "and without a new radiation zone");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task StarThrow_LandsWhereYouStood_ThenItsFragmentsStandAsWalls_TheSweepBreaksThemOrTheyCrumble()
        {
            var (game, targets, tyrant) = await TyrantSandbox(14f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var player = game.player;
            var world = WorldMap.Active;
            var b = game.waves.bosses;
            float life = b.meteorFragmentLife;
            float push = -1f;
            void Hit(DamageInfo info) { if (info.dealer?.Owner == tyrant) push = info.knockDistance; }
            player.AttackReceived += Hit;
            try
            {
                b.meteorFragmentLife = 2f;

                Assume.That(OpenGround(world, player.Position, b.meteorRadius + b.meteorFragmentSize, out var open), Is.True, "open ground");
                Place(player, open);
                Vector3 stood = Flat(player.Position);
                Force(tyrant, Enemy.StarThrowMove);
                await Until(() => tyrant.WindingUp && tyrant.BossAttackIndex == Enemy.StarThrowMove, 3f, "it kneads the meteor");
                Assert.That(tyrant.TelegraphActive, Is.False, "no line or fan: the impact circle comes with the throw");
                await Until(() => tyrant.MeteorFlying, 3f, "the throw");
                Assert.That(Flat(tyrant.MeteorTarget - stood).magnitude, Is.LessThan(.3f), "aimed where you stood");
                Assert.That(tyrant.MeteorReach, Is.EqualTo(b.meteorRadius).Within(.01f), "the phase-1 impact");
                float filled = tyrant.MeteorFill01;
                await Awaitable.FixedUpdateAsync();
                await Awaitable.FixedUpdateAsync();
                Assert.That(tyrant.MeteorFill01, Is.GreaterThan(filled), "its circle fills as it nears");
                await Until(() => !tyrant.MeteorFlying, b.meteorFlight + 1f, "the impact");
                Assert.That(push, Is.EqualTo(KnockbackRules.Active.Distance(b.meteorKnock)), "you were hit inside the circle");

                var terrain = TyrantTerrain.For(world);
                var fragments = terrain.Fragments;
                Assert.That(fragments, Is.Not.Empty, "the meteor splits into fragments");
                Assert.That(fragments.Count, Is.LessThanOrEqualTo(b.meteorFragments));
                foreach (var fragment in fragments)
                {
                    Vector3 centre = new(fragment.center.x, 0f, fragment.center.y);
                    Assert.That(world.IsWall(centre), Is.True, "each stands as a wall for paths and spawns");
                    Assert.That(Physics.CheckSphere(Query.AtCastHeight(centre), .2f, GameManager.WallMask, QueryTriggerInteraction.Ignore), Is.True,
                        "and for bodies and shots");
                    Assert.That(fragment.Contains(new Vector2(stood.x, stood.z)), Is.False, "none landed on you");
                }

                var first = fragments[0];
                Vector3 target = new(first.center.x, 0f, first.center.y), dir = Vector3.forward;
                int broken = terrain.BreakSwept(target - dir * 3f, Quaternion.Euler(0f, -10f, 0f) * dir, Quaternion.Euler(0f, 10f, 0f) * dir, 4f,
                    b.sweepTailHalfWidth, b.sweepWallPiece);
                Assert.That(broken, Is.GreaterThanOrEqualTo(1), "a tail breaks it");
                Assert.That(world.IsWall(target), Is.False, "and it is gone");

                await Until(() => terrain.Fragments.Count == 0, b.meteorFragmentLife + 2f, "the rest crumble when their time is up");
                foreach (var fragment in fragments)
                    Assert.That(world.IsWall(new Vector3(fragment.center.x, 0f, fragment.center.y)), Is.False, "no fragment wall is left");
            }
            finally
            {
                b.meteorFragmentLife = life;
                player.AttackReceived -= Hit;
            }
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task StarThrow_FragmentsKeepTheirTimeWhileAVoidTrialFreezesTheBattlefield()
        {
            var (game, targets, tyrant) = await TyrantSandbox(14f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            var player = game.player;
            var b = game.waves.bosses;

            Vector3 piece = Vector3.zero;
            bool found = false;
            foreach (var point in GroundNear(world, player.Position + Vector3.right * 6f, b.meteorFragmentSize))
            {
                if (!world.IsWalkable(point, b.meteorFragmentSize) || Flat(point - player.Position).magnitude < 3f ||
                    Flat(point - tyrant.Position).magnitude < tyrant.Radius + 2f) continue;
                piece = point;
                found = true;
                break;
            }

            Assume.That(found, Is.True, "open ground for a fragment");
            var terrain = TyrantTerrain.For(world);
            Assume.That(terrain.AddFragments(new[] { piece }, b.meteorFragmentSize, .5f), Is.EqualTo(1), "a fragment stands");
            Assume.That(world.EnterLabTrial(player), Is.True, "the void trial opens (the enemy lab's way in)");

            await Seconds(1.5f);
            Assert.That(world.TrialActive, Is.True, "still in the trial");
            Assert.That(terrain.Fragments, Has.Count.EqualTo(1), "its time stood still with the frozen battlefield (document 12.5)");
            Assert.That(world.IsWall(piece), Is.True, "and it still stands as a wall");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task AstralGuard_ChannelsThenDevoursTheMinionsInReach_ForHpAndShield_WithoutAKill()
        {
            var (game, targets, tyrant) = await TyrantSandbox(14f);
            var b = game.waves.bosses;
            V3CombatSmokeTest.Teleport(targets[0], tyrant.Position + Vector3.right * (tyrant.Radius + 1.5f));
            V3CombatSmokeTest.Teleport(targets[1], tyrant.Position + Vector3.left * (tyrant.Radius + 2f));
            V3CombatSmokeTest.Teleport(targets[2], tyrant.Position + Vector3.right * (b.astralRadius + 5f));
            Physics.SyncTransforms();
            var board = ElementStatus.For(tyrant);
            tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * .3f, source = DamageSource.Wall, ignoreShield = true });
            board.SetShield(0f);
            await Awaitable.FixedUpdateAsync();
            Assume.That(tyrant.BossPhase, Is.EqualTo(1), "still phase 1, so no refill muddles the numbers");
            float hp = tyrant.Hp;
            int kills = game.waves.Kills;

            Force(tyrant, Enemy.AstralGuardMove);
            await Until(() => tyrant.AstralChanneling, 3f, "the channel");
            Assert.That(tyrant.AstralReach, Is.EqualTo(b.astralRadius).Within(.01f), "its ring shows how far it reaches");
            Assert.That(targets[0].IsAlive && targets[1].IsAlive, Is.True, "nothing is eaten while it channels");
            await Until(() => !tyrant.WindingUp, b.astralChannel + 2f, "the channel ends");

            Assert.That(targets[0].gameObject.activeSelf || targets[1].gameObject.activeSelf, Is.False, "the two minions in reach are devoured");
            Assert.That(game.waves.Alive.Contains(targets[0]) || game.waves.Alive.Contains(targets[1]), Is.False, "and leave the wave's list");
            Assert.That(targets[0].CorpseActive || targets[1].CorpseActive, Is.False, "without leaving a corpse");
            Assert.That(targets[2].IsAlive, Is.True, "the one out of reach is not");
            Assert.That(game.waves.Kills, Is.EqualTo(kills), "a devoured minion is no kill");
            Assert.That(tyrant.Hp, Is.EqualTo(Mathf.Min(tyrant.maxHp, hp + tyrant.maxHp * b.astralHealPerMinion * 2f)).Within(.5f), "HP back for each");
            Assert.That(board.Shield, Is.EqualTo(Mathf.Min(tyrant.maxHp * b.astralShieldCap, tyrant.maxHp * b.astralShieldPerMinion * 2f)).Within(.5f),
                "and shield, never past its cap");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task PhaseTwo_BelowItsThreshold_RefillsTheShieldOnce_EnragedItsSweepGrowsHitsHarderAndComesMuchSooner()
        {
            var (game, targets, tyrant) = await TyrantSandbox(5f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            var balance = game.waves.waveSet.encounterBalance;
            var board = ElementStatus.For(tyrant);
            Assert.That(board.Shield, Is.EqualTo(tyrant.maxHp * tyrant.definition.startShieldShare).Within(1f), "it starts with a shield");
            board.SetShield(0f);
            tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * (1f - b.tyrantPhase2At) + 1f, source = DamageSource.Wall, ignoreShield = true });
            await Until(() => tyrant.BossPhase == 2, 2f, "phase 2");
            Assert.That(board.Shield, Is.EqualTo(tyrant.maxHp * balance.tyrantPhaseShieldShare).Within(1f), "entering phase 2 refills its shield");

            board.SetShield(0f);
            tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.Hp - tyrant.maxHp * .1f, source = DamageSource.Wall, ignoreShield = true });
            for (int i = 0; i < 5; i++) await Awaitable.FixedUpdateAsync();
            Assert.That(tyrant.BossPhase, Is.EqualTo(2), "two phases only");
            Assert.That(board.Shield, Is.Zero, "and one refill");

            var hits = new List<DamageInfo>();
            void Count(DamageInfo hit) { if (hit.dealer?.Owner == tyrant) hits.Add(hit); }
            game.player.AttackReceived += Count;
            try
            {
                Place(game.player, tyrant.Position + Flat(game.player.Position - tyrant.Position).normalized * 5f);
                Force(tyrant, Enemy.TailSweepMove);
                await Until(() => tyrant.WindingUp && tyrant.CleaveTelegraph, 3f, "the enraged sweep");
                float started = Time.time;
                Assert.That(tyrant.TelegraphRange, Is.EqualTo(b.sweepRangePhase2).Within(.01f), "its fan is much bigger");
                Assert.That(tyrant.WindupSecondsRemaining, Is.EqualTo(b.sweepWindup * b.tyrantEnragedWindupShare).Within(.1f),
                    "its wind-up is shorter");
                await Until(() => tyrant.TailSweeping, 3f, "the tail");
                await Until(() => !tyrant.TailSweeping, 3f, "the tail crosses the fan");
                Assert.That(tyrant.CooldownRemaining, Is.EqualTo(b.tyrantMoveGap * b.tyrantEnragedGapShare).Within(.1f),
                    "and so is its walk to the next move");
                Assert.That(hits, Has.Count.EqualTo(1), "the tail hits you");
                Assert.That(hits[0].amount, Is.EqualTo(tyrant.attackDamage * b.sweepDamage * b.tyrantEnragedDamage).Within(.01f), "harder");

                tyrant.ForceNextBossMove(Enemy.TailSweepMove);
                await Until(() => tyrant.WindingUp, 4f, "the next move");
                float cycle = Time.time - started;
                float enraged = b.sweepWindup * b.tyrantEnragedWindupShare + b.sweepSeconds + b.tyrantMoveGap * b.tyrantEnragedGapShare;
                Assert.That(cycle, Is.EqualTo(enraged).Within(.15f), "the enraged wind-up, the sweep and the enraged walk");
                Assert.That(cycle * 1.5f, Is.LessThanOrEqualTo(b.sweepWindup + b.sweepSeconds + b.tyrantMoveGap),
                    "at least one and a half times as often as in phase 1");
            }
            finally { game.player.AttackReceived -= Count; }
        }

        static Vector3 Flat(Vector3 v) => Query.Flat(v);

        static void SetPrivate(Enemy enemy, string field, float value) =>
            typeof(Enemy).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, value);

        static void Force(Enemy tyrant, int move)
        {
            tyrant.ForceNextBossMove(move);
            SetPrivate(tyrant, "cooldown", 0f);
        }

        static void Place(Combatant body, Vector3 at)
        {
            body.ClearWaveState();
            V3CombatSmokeTest.Teleport(body, new Vector3(at.x, 0f, at.z));
            Physics.SyncTransforms();
        }

        static Enemy CallSummon(WaveManager waves, EnemyDefinition data, Vector3 at)
        {
            var marker = waves.BorrowMarker();
            if (!marker) return null;
            var before = waves.Alive.ToArray();
            marker.Begin(at, data, false, ElementKind.None, true);
            var tick = typeof(WaveManager).GetMethod("TickPreparedMarkers", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 5 && marker.gameObject.activeSelf; i++) tick.Invoke(waves, new object[] { 1.6f });
            return waves.Alive.FirstOrDefault(e => e && e.IsAlive && e.definition == data && !before.Contains(e));
        }

        static async Task<bool> OnChargeLane(GameManager game, Enemy tyrant, float length, float margin)
        {
            if (!OpenLane(WorldMap.Active, tyrant.Position, tyrant.Radius + margin, length + 1f, out var start, out var ahead)) return false;
            Place(tyrant, start);
            Place(game.player, start + ahead * length);
            await Awaitable.FixedUpdateAsync();                   
            return true;
        }

        static bool OpenLane(WorldMap world, Vector3 near, float body, float length, out Vector3 start, out Vector3 direction)
        {
            var sides = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            foreach (var point in GroundNear(world, near, body))
            foreach (var side in sides)
            {
                if (!LaneClear(world, point, side, body, length)) continue;
                start = point;
                direction = side;
                return true;
            }

            start = near;
            direction = Vector3.forward;
            return false;
        }

        static bool LaneClear(WorldMap world, Vector3 from, Vector3 direction, float body, float length)
        {
            for (float s = 0f; s <= length; s += .5f)
            {
                Vector3 point = from + direction * s;
                if (!world.IsWalkable(point, body) || Quicksand.IsQuicksand(point)) return false;
            }

            return !Physics.SphereCast(Query.AtCastHeight(from), body, direction, out _, length, GameManager.WallMask, QueryTriggerInteraction.Ignore);
        }

        static IEnumerable<Vector3> GroundNear(WorldMap world, Vector3 near, float margin)
        {
            var bounds = world.Bounds;
            var points = new List<Vector3>();
            for (float x = bounds.min.x + margin; x <= bounds.max.x - margin; x += 2f)
            for (float z = bounds.min.z + margin; z <= bounds.max.z - margin; z += 2f)
                points.Add(new Vector3(x, 0f, z));
            return points.OrderBy(p => Flat(p - near).sqrMagnitude);
        }

        static bool FindRingApproach(WorldMap world, float body, BossTuning b, out Vector3 spot, out Vector3 from)
        {
            const float Away = 9f;
            spot = from = Vector3.zero;
            foreach (var hole in world.holes)
            {
                if (!hole || hole.Phase != HolePhase.Active) continue;
                Vector3 centre = Flat(hole.Centre);
                float ring = (hole.KillRadius + hole.GravityRadius) * .5f;
                for (int a = 0; a < 360; a += 15)
                foreach (float turn in new[] { 90f, -90f })
                {
                    Vector3 outward = Quaternion.Euler(0f, a, 0f) * Vector3.forward, along = Quaternion.Euler(0f, turn, 0f) * outward;
                    Vector3 you = centre + outward * ring, it = you + along * Away, end = you + along * b.rushLeapDistance;

                    if (!hole.InGravity(end) || Flat(end - centre).magnitude < hole.KillRadius + body) continue;
                    if (!world.Contains(you, 1f) || world.IsWall(you, 1f) || world.IsHole(you) || !world.IsWalkable(it, body)) continue;
                    if (!PathClear(world, it, end, body, hole)) continue;
                    spot = you;
                    from = it;
                    return true;
                }
            }

            return false;
        }

        static bool FindHoleAcross(WorldMap world, float body, out BlackHole hole, out Vector3 from, out Vector3 spot)
        {
            hole = null;
            from = spot = Vector3.zero;
            foreach (var candidate in world.holes)
            {
                if (!candidate || candidate.Phase != HolePhase.Active) continue;
                Vector3 centre = Flat(candidate.Centre);
                for (int a = 0; a < 360; a += 15)
                {
                    Vector3 outward = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    Vector3 it = centre + outward * (candidate.GravityRadius + body + 1f), you = centre - outward * (candidate.GravityRadius + 1.5f);
                    if (!world.IsWalkable(it, body) || !world.IsWalkable(you, 1f)) continue;
                    if (!PathClear(world, it, centre + outward * candidate.KillRadius, body, candidate)) continue;
                    hole = candidate;
                    from = it;
                    spot = you;
                    return true;
                }
            }

            return false;
        }

        static bool PathClear(WorldMap world, Vector3 from, Vector3 to, float body, BlackHole except)
        {
            Vector3 delta = Flat(to - from);
            float length = delta.magnitude;
            if (length < .01f) return true;
            Vector3 direction = delta / length;
            for (float s = 0f; s <= length; s += .5f)
            {
                Vector3 point = from + direction * s;
                if (!world.Contains(point, body) || world.IsWall(point, body)) return false;
            }

            if (Physics.SphereCast(Query.AtCastHeight(from), body, direction, out _, length, GameManager.WallMask, QueryTriggerInteraction.Ignore))
                return false;
            foreach (var other in world.holes)
                if (other && other != except && (other.Phase is HolePhase.Active or HolePhase.Collapsing) &&
                    SegmentDistance(from, to, other.Centre) < other.GravityRadius + body)
                    return false;
            return true;
        }

        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = Flat(b - a), ap = Flat(point - a);
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            return Flat(ap - ab * t).magnitude;
        }

        static bool FindWallToSweep(WorldMap world, float body, BossTuning b, out Vector3 bossAt, out Vector3 playerAt, out Vector3 inWall,
            out Vector3? farPart)
        {
            bossAt = playerAt = inWall = Vector3.zero;
            farPart = null;
            bool found = false;
            float wanted = b.sweepRange + b.sweepWallPiece + b.sweepTailHalfWidth + 1f;
            var sides = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (var rect in world.layout.walls)
            foreach (var n in sides)
            foreach (float end in new[] { 1f, -1f, 0f })
            foreach (float turn in new[] { 50f, -50f })
            {
                Vector3 centre = new(rect.center.x, 0f, rect.center.y), along = new(Mathf.Abs(n.z), 0f, Mathf.Abs(n.x));
                float depth = Mathf.Abs(n.x) > 0f ? rect.width * .5f : rect.height * .5f;
                float length = Mathf.Abs(n.x) > 0f ? rect.height : rect.width;
                Vector3 face = centre + n * depth + along * (end * Mathf.Max(0f, length * .5f - 1f));
                Vector3 boss = face + n * (body + 1.5f), player = boss + Quaternion.Euler(0f, turn, 0f) * -n * 4f;
                if (!world.IsWalkable(boss, body) || !world.IsWalkable(player, 1f) || Quicksand.IsQuicksand(player)) continue;

                Vector3 other = centre + n * (depth - .3f) - along * (end * (length * .5f - .3f));
                bool far = end != 0f && Flat(other - boss).magnitude > wanted;
                if (found && !far) continue;
                found = true;
                bossAt = boss;
                playerAt = player;
                inWall = face - n * .3f;
                farPart = far ? other : null;
                if (far) return true;
            }

            return found;
        }

        static bool OpenGround(WorldMap world, Vector3 near, float radius, out Vector3 spot)
        {
            spot = near;
            float best = float.MaxValue;
            var bounds = world.Bounds;
            for (float x = bounds.min.x + radius; x <= bounds.max.x - radius; x += 2f)
            for (float z = bounds.min.z + radius; z <= bounds.max.z - radius; z += 2f)
            {
                var point = new Vector3(x, 0f, z);
                float d = Flat(point - near).sqrMagnitude;
                if (d >= best || !world.IsWalkable(point, radius)) continue;
                best = d;
                spot = point;
            }

            return best < float.MaxValue;
        }

        static async Task<(GameManager game, Enemy[] targets, Enemy tyrant)> TyrantSandbox(float ahead)
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            FeedEachHoleOnce(game);
            game.player.Invulnerable = true;
            game.player.enabled = true;                                           
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * ahead);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");

            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            return (game, targets, tyrant);
        }

        static void FeedEachHoleOnce(GameManager game)
        {
            var world = WorldMap.Active;
            world.SkipHoleIntro();                                    
            foreach (var hole in world.holes)
                if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0)
                    hole.Feed(1, game.waves.WaveIndex + 1);
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.NextFrameAsync();
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
