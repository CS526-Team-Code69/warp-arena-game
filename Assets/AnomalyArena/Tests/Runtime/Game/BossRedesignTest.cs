using System;
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
    public class BossRedesignTest
    {
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";
        const string ArenaScene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

                                             

        [Test]
        public void ProphetMoveFor_CloseOnlyPulsesOrInhales_FarOnlyFires()
        {
            var b = new BossTuning();
            for (int phase = 1; phase <= 3; phase++)
            for (int step = 0; step < 20; step++)
            {
                float roll = step / 20f;
                int close = Enemy.ProphetMoveFor(phase, true, roll, b), far = Enemy.ProphetMoveFor(phase, false, roll, b);
                Assert.That(close, phase == 2 ? Is.EqualTo(Enemy.PulseMove).Or.EqualTo(Enemy.InhaleMove) : Is.EqualTo(Enemy.PulseMove),
                    $"phase {phase} close, roll {roll}: no shots point-blank");
                Assert.That(far, Is.Not.EqualTo(Enemy.PulseMove), $"phase {phase} far, roll {roll}: no pulse that only shoos the player");
                if (phase == 1) Assert.That(far, Is.EqualTo(Enemy.DoubleShotMove).Or.EqualTo(Enemy.RingMove));
                if (phase < 3) Assert.That(far, Is.Not.EqualTo(Enemy.ProphecyMove), "prophecy landings are phase 3 only");
                if (phase == 3) Assert.That(far, Is.Not.EqualTo(Enemy.InhaleMove), "phase 3 drops the inhale");
            }

            Assert.That(Enemy.ProphetMoveFor(1, false, .59f, b), Is.EqualTo(Enemy.DoubleShotMove), "phase 1: 60% predictive shots");
            Assert.That(Enemy.ProphetMoveFor(1, false, .61f, b), Is.EqualTo(Enemy.RingMove));
            Assert.That(Enemy.ProphetMoveFor(2, false, .9f, b), Is.EqualTo(Enemy.InhaleMove), "phase 2 far: 30% inhales");
            Assert.That(Enemy.ProphetMoveFor(2, true, .7f, b), Is.EqualTo(Enemy.InhaleMove), "phase 2 close: 40% inhales");
            Assert.That(Enemy.ProphetMoveFor(3, false, .9f, b), Is.EqualTo(Enemy.ProphecyMove), "phase 3 far: 35% prophecy landings");
        }

        [Test]
        public void TyrantMoveFor_BandsMatchEachMovesReach_FarOrOffScreenLeaps()
        {
            var b = new BossTuning();
            for (int step = 0; step < 20; step++)
            {
                float roll = step / 20f;
                Assert.That(Enemy.TyrantMoveFor(25f, true, roll, b), Is.EqualTo(Enemy.LeapMove), "beyond 20 cells it only leaps");
                Assert.That(Enemy.TyrantMoveFor(5f, false, roll, b), Is.EqualTo(Enemy.LeapMove), "off screen it only leaps");
                Assert.That(Enemy.TyrantMoveFor(6.4f, true, roll, b),
                    Is.EqualTo(Enemy.CleaveMove).Or.EqualTo(Enemy.SlamMove).Or.EqualTo(Enemy.ChargeMove));
                Assert.That(Enemy.TyrantMoveFor(8f, true, roll, b), Is.EqualTo(Enemy.SlamMove).Or.EqualTo(Enemy.ChargeMove),
                    "6.5-8.5 cells: never a cleave that cannot reach, never a leap");
                Assert.That(Enemy.TyrantMoveFor(12f, true, roll, b), Is.EqualTo(Enemy.ChargeMove).Or.EqualTo(Enemy.LeapMove),
                    "beyond the slam's 8.5 cells: never a slam that cannot reach");
            }

            Assert.That(Enemy.TyrantMoveFor(5f, true, .5f, b), Is.EqualTo(Enemy.CleaveMove), "close: 55% cleaves");
            Assert.That(Enemy.TyrantMoveFor(5f, true, .7f, b), Is.EqualTo(Enemy.SlamMove), "close: 30% slams");
            Assert.That(Enemy.TyrantMoveFor(5f, true, .9f, b), Is.EqualTo(Enemy.ChargeMove), "close: 15% charges");
            Assert.That(Enemy.TyrantMoveFor(12f, true, .6f, b), Is.EqualTo(Enemy.ChargeMove), "far: 70% charges");
            Assert.That(Enemy.TyrantMoveFor(12f, true, .8f, b), Is.EqualTo(Enemy.LeapMove), "far: 30% leaps");
        }

                                                     

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task TrialEntry_PlayerSetDownEightCellsOut_FirstMoveIsAPredictiveShotThatNeverPushes()
        {
            var (game, world, hollow) = await StartTrial();
            var player = game.player;
            Assert.That(Flat(player.Position - world.trialPlatform.position).magnitude, Is.EqualTo(world.trialEntryDistance).Within(.3f),
                "set down outside the pulse range");
            Assert.That(world.trialEntryDistance, Is.EqualTo(8f));
            Assert.That(world.trialRadius, Is.EqualTo(game.waves.bosses.prophetPlatformRadii.x), "phase 1 platform");

            await Until(() => hollow.WindingUp, world.trialOpeningGrace + 2f, "the first wind-up");
            Assert.That(hollow.BossAttackIndex, Is.EqualTo(Enemy.DoubleShotMove), "the first move is a predictive shot");
            Assert.That(hollow.TelegraphActive, Is.False, "shots draw no ground warning; the shot itself is the warning");
            await Until(() => !hollow.WindingUp, 2f, "the shot fires");
            var shots = game.waves.preparedBolts.Where(b => b && b.gameObject.activeSelf && b.IsBossShot).ToArray();
            Assert.That(shots, Has.Length.EqualTo(2), "phase 1 fires two");
            Vector3 toPlayer = Flat(player.Position - hollow.Position).normalized;
            foreach (var shot in shots)
                Assert.That(Vector3.Angle(shot.Direction, toPlayer), Is.LessThan(5f), "a player standing still is where both shots go");
            Assert.That(shots.All(s => !s.IsPoisonSpike), Is.True, "the Prophet's shots carry no Spitter poison");

            float hp = player.Hp;
            bool knocked = false;
            float until = Time.time + 1.5f;
            while (Time.time < until)
            {
                knocked |= player.State == CharacterState.Knocked;
                await Awaitable.FixedUpdateAsync();
            }

            Assert.That(player.Hp, Is.LessThan(hp), "the shot hits a player who did not turn");
            Assert.That(knocked, Is.False, "shots never push");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task TrialKnockback_FromTheProphet_StopsShortOfTheEdgeAndOfHoles_OwnRecoilDoesNot()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;                                                    
            var player = game.player;
            Assume.That(player.knockbackScale, Is.EqualTo(1f), "the distances below assume an unscaled player push");
            var b = game.waves.bosses;
            Vector3 centre = Flat(world.trialPlatform.position);
            float r = world.trialRadius;

            Assert.That(world.LimitBossKnockback(centre + Vector3.right * (r - 4f), Vector3.right, 8f),
                Is.EqualTo(4f - b.edgeKnockMargin).Within(.01f), "outward: stops at the margin");
            Assert.That(world.LimitBossKnockback(centre + Vector3.right * (r - 4f), Vector3.left, 3f), Is.EqualTo(3f).Within(.01f),
                "inward: unchanged");

            Place(player, centre + Vector3.right * (r - 3.5f));
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, hollow, Vector3.right, 1.4f)), Is.True);
            await Settle(player);
            float stopped = Flat(player.Position - centre).magnitude;
            Assert.That(stopped, Is.EqualTo(r - b.edgeKnockMargin).Within(.25f), "a boss hit stops the player at the margin from the edge");
            Assert.That(stopped, Is.LessThanOrEqualTo(r - b.edgeKnockMargin + .05f), "and never past it");
            Assert.That(player.State, Is.Not.EqualTo(CharacterState.Falling));

            Place(player, centre + Vector3.right * (r - 3.5f));
            player.Knockback(Vector3.right, 1.4f, KnockKind.Hit);
            await Settle(player);
            Assert.That(Flat(player.Position - centre).magnitude, Is.GreaterThan(r - b.edgeKnockMargin + .1f),
                "the player's own recoil is not cut short (backfire can still drop them)");

            Vector3 hole = centre + Vector3.back * 9.5f;
            world.CreateBossHole(hole, 3f, 10f, hollow);
            Assert.That(world.IsHole(hole), Is.True, "the Prophet's hole opens in its own slot");
            Place(player, centre + Vector3.back * 6f);
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, hollow, Vector3.back, 8f)), Is.True);
            await Settle(player);
            Assert.That(Flat(player.Position - hole).magnitude, Is.GreaterThanOrEqualTo(1.5f + b.holeKnockMargin - .2f),
                "a boss hit stops the player a cell short of a hole");
            Assert.That(player.State, Is.Not.EqualTo(CharacterState.Falling));
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task SummonFall_OffTheEdgeOrIntoAHole_CostsAFixedShareAndStagger_EvenWhileStunned()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;
            var b = game.waves.bosses;
            Vector3 centre = Flat(world.trialPlatform.position);
            float hp = hollow.Hp, share = hollow.maxHp * b.summonFallHpShare;

            game.waves.SpawnExtrasAt(EnemyKind.Grunt, new[] { centre + Vector3.right * (world.trialRadius + 1f) }, true);
            await Until(() => hollow.Hp < hp, 2f, "a summon off the edge costs the Prophet HP");
            Assert.That(hollow.Hp, Is.EqualTo(hp - share).Within(.01f), "a fixed 4% (not a player hit, no crit)");
            Assert.That(hollow.StaggerRatio, Is.EqualTo(b.summonFallStagger / b.prophetStaggerLimit).Within(.001f), "and some stagger");

            hollow.AddStagger(b.prophetStaggerLimit);
            Assert.That(hollow.BossStunned, Is.True, "a full stagger stuns it");
            hp = hollow.Hp;
            Vector3 hole = centre + Vector3.forward * 6f;
            world.CreateBossHole(hole, 3f, 10f, hollow);
            game.waves.SpawnExtrasAt(EnemyKind.Grunt, new[] { hole }, true);
            await Until(() => hollow.Hp < hp, 2f, "a summon in a hole counts as a fall");
            Assert.That(hollow.Hp, Is.EqualTo(hp - share).Within(.01f), "still exactly 4% while stunned (no stun bonus on a fixed share)");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task ProphetPulse_DrawsItsCircle_HitsOnlyThePlayer()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            Vector3 centre = Flat(world.trialPlatform.position);
            var summon = SpawnStillSummon(game, hollow, centre + Vector3.left * 3.5f);
            var player = game.player;
            Place(player, centre + Vector3.back * 4f);
            float summonHp = summon.Hp, playerHp = player.Hp;
            Vector3 summonAt = summon.Position;

            hollow.ForceNextBossMove(Enemy.PulseMove);
            SetPrivate(hollow, "cooldown", 0f);
            await Until(() => hollow.WindingUp, 2f, "the pulse wind-up");
            Assert.That(hollow.BossAttackIndex, Is.EqualTo(Enemy.PulseMove));
            Assert.That(hollow.CircleTelegraph && hollow.TelegraphActive, Is.True, "a boss circle move draws its ground circle");
            Assert.That(hollow.TelegraphRange, Is.EqualTo(b.pulseReach).Within(.01f), "the circle is as big as the pulse");
            await Until(() => !hollow.WindingUp, 2f, "the pulse fires");
            await Awaitable.FixedUpdateAsync();

            Assert.That(player.Hp, Is.LessThan(playerHp), "the pulse hits the player inside it");
            Assert.That(player.State, Is.EqualTo(CharacterState.Knocked), "and pushes them out");
            Assert.That(summon.Hp, Is.EqualTo(summonHp), "its own summon is not hit");
            Assert.That(Flat(summon.Position - summonAt).magnitude, Is.LessThan(.1f), "nor pushed");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task ProphetInhale_HitsThePlayerOnce_SparesItsSummons_ThenPulses()
        {
            var (game, world, hollow) = await StartTrial();
            Vector3 centre = Flat(world.trialPlatform.position);
            var player = game.player;
            var summon = SpawnStillSummon(game, hollow, centre + Vector3.left * (hollow.Radius + .55f));
            Place(player, centre + Vector3.back * (hollow.Radius + player.Radius + .05f));
            int hits = 0;
            void Count(DamageInfo hit) { if (hit.dealer?.Owner == hollow) hits++; }
            player.AttackReceived += Count;
            try
            {
                hollow.ForceNextBossMove(Enemy.InhaleMove);
                SetPrivate(hollow, "cooldown", 0f);
                await Until(() => hollow.Inhaling, 3f, "the inhale starts");
                float summonHp = summon.Hp;
                await Until(() => !hollow.Inhaling, 4f, "the inhale ends");
                Assert.That(hits, Is.EqualTo(1), "touching it during one inhale hurts once");
                Assert.That(summon.Hp, Is.EqualTo(summonHp), "a summon pulled into it is not hurt");
                Assert.That(hollow.WindingUp && hollow.BossAttackIndex == Enemy.PulseMove, Is.True, "an inhale always ends in a pulse");
                Assert.That(hollow.TelegraphActive, Is.True, "that pulse draws its circle too");
            }
            finally { player.AttackReceived -= Count; }
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task EquilibriumRing_PhaseOne_EvenlySpacedShotsWithOneAimedAtThePlayer()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            hollow.ForceNextBossMove(Enemy.RingMove);
            SetPrivate(hollow, "cooldown", 0f);
            await Until(() => hollow.WindingUp, 2f, "the ring wind-up");
            Assert.That(hollow.BossAttackIndex, Is.EqualTo(Enemy.RingMove));
            await Until(() => !hollow.WindingUp, 2f, "the ring fires");
            var shots = game.waves.preparedBolts.Where(s => s && s.gameObject.activeSelf && s.IsBossShot).ToArray();
            Assert.That(shots, Has.Length.EqualTo(b.ringShots), "one ring in phase 1");
            var angles = shots.Select(s => Mathf.Repeat(Mathf.Atan2(s.Direction.x, s.Direction.z) * Mathf.Rad2Deg, 360f)).OrderBy(a => a).ToArray();
            float spacing = 360f / b.ringShots;
            for (int i = 0; i < angles.Length; i++)
            {
                float gap = Mathf.Repeat(angles[(i + 1) % angles.Length] - angles[i], 360f);
                Assert.That(gap, Is.EqualTo(spacing).Within(1f), "evenly spaced");
            }

            Vector3 toPlayer = Flat(game.player.Position - hollow.Position).normalized;
            Assert.That(shots.Min(s => Vector3.Angle(s.Direction, toPlayer)), Is.LessThan(2f), "the ring is aimed so the player must step into a gap");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task EquilibriumRing_PhaseTwo_SecondRingClosesTheGapsOfTheFirstEvenIfThePlayerSteppedIntoOne()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            game.player.Invulnerable = true;
            try
            {
                hollow.ReceiveDamage(new DamageInfo { amount = hollow.maxHp * .5f, source = DamageSource.Wall });
                await Until(() => hollow.BossPhase == 2, 2f, "phase 2");
                hollow.ForceNextBossMove(Enemy.RingMove);
                SetPrivate(hollow, "cooldown", 0f);
                await Until(() => hollow.WindingUp && hollow.BossAttackIndex == Enemy.RingMove, 3f, "the ring wind-up");
                await Until(() => game.waves.preparedBolts.Count(s => s && s.gameObject.activeSelf && s.IsBossShot) >= b.ringShots, 3f, "the first ring");
                var first = game.waves.preparedBolts.Where(s => s && s.gameObject.activeSelf && s.IsBossShot).ToArray();
                float spacing = 360f / b.ringShots;
                float Angle(EnemyBolt s) => Mathf.Repeat(Mathf.Atan2(s.Direction.x, s.Direction.z) * Mathf.Rad2Deg, 360f);
                float firstAngle = Angle(first[0]);

                                                                                                                                    
                                                                                                                                  
                Vector3 gap = Quaternion.Euler(0f, firstAngle + spacing * .5f, 0f) * Vector3.forward, centre = Flat(world.trialPlatform.position);
                float reach = 6f;
                while (reach > 2f && Flat(hollow.Position + gap * reach - centre).magnitude > world.trialRadius - 2f) reach -= .5f;
                Place(game.player, Flat(hollow.Position) + gap * reach);
                await Until(() => game.waves.preparedBolts.Count(s => s && s.gameObject.activeSelf && s.IsBossShot && !first.Contains(s)) >= b.ringShots,
                    b.ringInterval + 2f, "the second ring");
                var second = game.waves.preparedBolts.Where(s => s && s.gameObject.activeSelf && s.IsBossShot && !first.Contains(s)).ToArray();
                float expectedTurn = spacing / Mathf.Max(1, b.phase2Rings);
                foreach (var shot in second)
                {
                    float offset = Mathf.Repeat(Angle(shot) - firstAngle, spacing);
                    Assert.That(Mathf.Min(Mathf.Abs(offset - expectedTurn), spacing - Mathf.Abs(offset - expectedTurn)), Is.LessThan(1f),
                        "every second-ring shot sits between two first-ring shots, wherever the player went");
                }
            }
            finally { game.player.Invulnerable = false; }
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task ProphecyLanding_PhaseThree_TwoCirclesFillThenTurnIntoHolesThatClose()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            Vector3 centre = Flat(world.trialPlatform.position);
            hollow.ReceiveDamage(new DamageInfo { amount = hollow.maxHp * .75f, source = DamageSource.Wall });
            await Until(() => hollow.BossPhase == 3, 2f, "phase 3");
            Place(game.player, centre + Vector3.back * 6f);
            hollow.ForceNextBossMove(Enemy.ProphecyMove);
            SetPrivate(hollow, "cooldown", 0f);
            await Until(() => hollow.LandingZones.Count == 2, 3f, "two landing circles");
            var zones = hollow.LandingZones.ToArray();
            foreach (var zone in zones)
            {
                                                                                                                     
                Assert.That(Flat(zone.centre - centre).magnitude, Is.LessThanOrEqualTo(Mathf.Min(world.trialRadius, hollow.PlatformGoal) - b.prophecyEdgeMargin + .05f),
                    "never within the margin of the edge the platform shrinks to");
                Assert.That(Flat(zone.centre - hollow.Position).magnitude, Is.GreaterThanOrEqualTo(hollow.Radius + zone.radius),
                    "never on its own body");
                Assert.That(zone.radius * 2f, Is.EqualTo(b.prophecyDiameter).Within(.01f), "full size from the start");
                Assert.That(world.IsHole(zone.centre), Is.False, "not a hole while it fills");
            }
                                                                                                                             
            hollow.HoldAttacks(b.prophecyFill + b.prophecyHoleLife + 3f);

            await Until(() => hollow.LandingZones.Count == 0, b.prophecyFill + 1f, "the circles fill");
            Assert.That(zones.All(z => world.IsHole(z.centre)), Is.True, "a filled circle becomes a hole");
            await Until(() => zones.All(z => !world.IsHole(z.centre)), b.prophecyHoleLife + 2f, "the holes close");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task ProphetSummon_ShowsRedCrossesFirst_NeverMoreThanTheCap()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            Vector3 centre = Flat(world.trialPlatform.position);
            for (int i = 0; i < 6; i++)
                SpawnStillSummon(game, hollow, centre + Quaternion.Euler(0f, 60f * i, 0f) * Vector3.forward * 6.5f);
            Assume.That(TrialSummons(game, hollow), Is.EqualTo(6));
            int markers = ActiveMarkers(game);

            SetPrivate(hollow, "summonTimer", 0f);
            await Until(() => ActiveMarkers(game) > markers, 1f, "red crosses on the summon points");
            Assert.That(ActiveMarkers(game) - markers, Is.EqualTo(b.prophetSummonCap - 6), "only up to the cap of living summons");
            Assert.That(TrialSummons(game, hollow), Is.EqualTo(6), "nothing appears while the crosses show");
            await Until(() => TrialSummons(game, hollow) > 6, b.prophetSummonWarning + 1f, "the summons appear");
            Assert.That(TrialSummons(game, hollow), Is.EqualTo(b.prophetSummonCap));
            Assert.That(ActiveMarkers(game), Is.EqualTo(markers), "the crosses are gone");
            Assert.That(game.waves.Alive.Where(e => e && e != hollow && e.IsTrialSummon).All(e => e.SuppressRewards), Is.True,
                "summons give no experience");
        }

                                             

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task TyrantCharge_LineEndsAtTheWall_WallHitClearsTheShieldThenCostsThreePercent()
        {
            var (game, targets, tyrant) = await TyrantSandbox(10f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            Vector3 toPlayer = Flat(game.player.Position - tyrant.Position).normalized;
                                                                                                                    
            WorldMap.Active.CreateWall(tyrant.Position + toPlayer * 5f, 3f, 30f, 0f, tyrant);
            Physics.SyncTransforms();
            var board = ElementStatus.For(tyrant);
            Assume.That(board.Shield, Is.GreaterThan(0f), "it starts with its opening shield");
            float maxHp = tyrant.maxHp;

            tyrant.ForceNextBossMove(Enemy.ChargeMove);
            SetPrivate(tyrant, "cooldown", 0f);
            await Until(() => tyrant.WindingUp && tyrant.LineTelegraph, 3f, "the charge wind-up");
            Assert.That(tyrant.TelegraphRange, Is.EqualTo(4.5f).Within(.4f), "the line ends at the bulwark's face, not 20 cells on");
            await Until(() => tyrant.BossStunned, 5f, "the charge hits the bulwark");
            Assert.That(board.Shield, Is.Zero, "the wall hit clears the shield");
            Assert.That(tyrant.Hp, Is.EqualTo(maxHp - maxHp * b.wallHitHpShare).Within(1f),
                "then costs 3% of max HP in full (the shield was cleared first, the stun came after)");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task TyrantCleave_SparesItsSummons_PhaseTwoBackSlashTurnsOnAPlayerWhoGotRound()
        {
            var (game, targets, tyrant) = await TyrantSandbox(4.5f);
            var player = game.player;
            Vector3 toPlayer = Flat(player.Position - tyrant.Position).normalized;
            var summon = targets[1];
            targets[0].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            V3CombatSmokeTest.Teleport(summon, tyrant.Position + Quaternion.Euler(0f, 40f, 0f) * toPlayer * 4f);
            Physics.SyncTransforms();
            float summonHp = summon.Hp, playerHp = player.Hp;

            tyrant.ForceNextBossMove(Enemy.CleaveMove);
            SetPrivate(tyrant, "cooldown", 0f);
            await Until(() => tyrant.WindingUp && tyrant.CleaveTelegraph, 3f, "the cleave wind-up");
            Assert.That(tyrant.TelegraphActive, Is.True, "the sector shows on the ground");
            await Until(() => !tyrant.WindingUp, 3f, "the cleave lands");
            await Awaitable.FixedUpdateAsync();
            Assert.That(player.Hp, Is.LessThan(playerHp), "the player in the sector is hit");
            Assert.That(summon.Hp, Is.EqualTo(summonHp), "its own summon in the sector is not");

                                                       
            tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * .4f, source = DamageSource.Wall, ignoreShield = true });
            await Until(() => tyrant.BossPhase == 2 && tyrant.WindingUp && tyrant.BossAttackIndex == Enemy.RoyalShieldMove, 3f,
                "the royal shield cast");
            var balance = game.waves.waveSet.encounterBalance;
            Assert.That(ElementStatus.For(tyrant).Shield, Is.EqualTo(tyrant.maxHp * balance.tyrantPhaseShieldShare).Within(1f),
                "the royal shield refills to its share");
            await Until(() => !tyrant.WindingUp, 3f, "the cast ends");

            Place(player, tyrant.Position + toPlayer * 4.5f);
            tyrant.ForceNextBossMove(Enemy.CleaveMove);
            SetPrivate(tyrant, "cooldown", 0f);
            await Until(() => tyrant.WindingUp && tyrant.BossAttackIndex == Enemy.CleaveMove, 3f, "the second cleave wind-up");
            Vector3 side = Vector3.Cross(Vector3.up, tyrant.TelegraphFacing).normalized;
            Place(player, tyrant.Position + side * 4f);                                             
            await Until(() => tyrant.WindingUp && tyrant.BossAttackIndex == Enemy.BackSlashMove, 3f, "the back slash");
            Assert.That(tyrant.CleaveTelegraph, Is.True, "the back slash draws the same sector");
            Assert.That(Vector3.Angle(tyrant.TelegraphFacing, Flat(player.Position - tyrant.Position)), Is.LessThan(10f),
                "turned toward the player");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task TyrantLeap_LandsOnTheLockedPoint_KeepsItsCircleInFlight_SparesSummons()
        {
            var (game, targets, tyrant) = await TyrantSandbox(14f);
            var player = game.player;
            var summon = targets[0];
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var b = game.waves.bosses;
            Vector3 aim = Flat(player.Position);
            Vector3 from = Flat(tyrant.Position);

            tyrant.ForceNextBossMove(Enemy.LeapMove);
            SetPrivate(tyrant, "cooldown", 0f);
            await Until(() => tyrant.WindingUp && tyrant.BossAttackIndex == Enemy.LeapMove, 3f, "the leap wind-up");
            Vector3 target = tyrant.LeapTarget;
            Assert.That(tyrant.CircleTelegraph && tyrant.TelegraphActive, Is.True, "a landing circle");
            Assert.That(tyrant.TelegraphRange, Is.EqualTo(b.leapRadius).Within(.01f));
            Assert.That(Flat(tyrant.telegraph.position - target).magnitude, Is.LessThan(.1f), "drawn where it will land");
            Assert.That(Flat(target - aim).magnitude, Is.LessThan(3f), "aimed where the player stood");
            Assert.That(Vector3.Distance(Vector3.Project(target - from, aim - from) + from, target), Is.LessThan(.1f),
                "on the line toward the player (moved back only to clear walls)");

            Vector3 away = Vector3.Cross(Vector3.up, (aim - from).normalized);
            Place(player, target + away * 3f);                                          
            V3CombatSmokeTest.Teleport(summon, target - away * 3.2f);
            Physics.SyncTransforms();
            float summonHp = summon.Hp, playerHp = player.Hp;
            await Until(() => tyrant.Leaping, 2f, "it takes off");
            Assert.That(tyrant.LeapTarget, Is.EqualTo(target), "the landing point does not follow the player");
            Assert.That(tyrant.TelegraphActive, Is.True, "the circle stays while it flies");
            await Until(() => !tyrant.Leaping, 2f, "it lands");
            await Awaitable.FixedUpdateAsync();
            Assert.That(Flat(tyrant.Position - target).magnitude, Is.LessThan(.5f), "on the landing point");
            Assert.That(tyrant.GetComponent<Rigidbody>().isKinematic, Is.False, "back on the ground");
            Assert.That(tyrant.GetComponent<CapsuleCollider>().enabled, Is.True);
            Assert.That(player.Hp, Is.LessThan(playerHp), "the landing hits a player still inside the circle");
            Assert.That(summon.Hp, Is.EqualTo(summonHp), "and spares its own summon");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task Bosses_TakeNoHitStop_AndIgnoreBlackHolePull()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * 12f);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");
            tyrant.enabled = false;
            Physics.SyncTransforms();
            var grunt = targets[0];
            grunt.ReceiveDamage(DamageInfo.Attack(3f, game.player, Vector3.forward, 0f, HitKind.Slam));
            tyrant.ReceiveDamage(DamageInfo.Attack(3f, game.player, Vector3.forward, 0f, HitKind.Slam));
            Assume.That(grunt.HitPaused, Is.True, "a heavy hit holds an ordinary enemy");
            Assert.That(tyrant.HitPaused, Is.False, "a boss only jolts, so its clocks keep running");

            var world = WorldMap.Active;
            var hole = world.holes.FirstOrDefault(h => h && h.Phase == HolePhase.Active);
            Assume.That(hole, Is.Not.Null, "the map has an active black hole");
            float ring = hole.KillRadius + hole.gravityCells * .5f;
            Vector3 bossSpot = Vector3.zero, gruntSpot = Vector3.zero;
            bool found = false;
            for (int a = 0; a < 360 && !found; a += 15)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                bossSpot = Flat(hole.Centre + dir * ring);
                                                                                                                
                gruntSpot = Flat(hole.Centre + Quaternion.Euler(0f, 90f, 0f) * dir * ring);
                found = world.Contains(bossSpot, 2.5f) && !world.IsWall(bossSpot, 2.5f) && world.Contains(gruntSpot, 1f) &&
                        !world.IsWall(gruntSpot, 1f);
            }

            Assume.That(found, Is.True, "room for both bodies in the pull ring");
            V3CombatSmokeTest.Teleport(tyrant, bossSpot);
            V3CombatSmokeTest.Teleport(grunt, gruntSpot);
            grunt.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            Physics.SyncTransforms();
                                                                                                                          
            for (int i = 0; i < 5; i++) await Awaitable.FixedUpdateAsync();
            Vector3 bossSettled = tyrant.Position, gruntSettled = grunt.Position;
            for (int i = 0; i < 20; i++) await Awaitable.FixedUpdateAsync();
            Assume.That(Flat(grunt.Position - gruntSettled).magnitude, Is.GreaterThan(.2f), "the ring pulls an ordinary enemy");
            Assert.That(Flat(tyrant.Position - bossSettled).magnitude, Is.LessThan(.05f),
                $"the boss is not pulled (set at {bossSpot}, after 5 steps {bossSettled}, after 25 {tyrant.Position}, hole {hole.Centre})");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task TyrantSummons_ShowRedCrossesFirst_AndGiveNoExperience()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * 15f);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");
            tyrant.enabled = false;
            Physics.SyncTransforms();
            int living = game.waves.LivingCount;

            game.waves.SpawnTyrantSummons(6, tyrant.Position);
            var crosses = game.waves.preparedMarkers.Where(m => m && m.gameObject.activeSelf && m.bossSummon).ToArray();
            Assert.That(crosses, Is.Not.Empty, "boss summons show a red cross first");
            Assert.That(game.waves.LivingCount, Is.EqualTo(living), "nothing appears at once");

            typeof(WaveManager).GetMethod("TickPreparedMarkers", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game.waves, new object[] { 1.1f });
            Assert.That(crosses.All(m => !m.gameObject.activeSelf), Is.True, "each cross becomes its summon");
            var summons = game.waves.Alive.Where(e => e && e.IsAlive && e != tyrant && !targets.Contains(e)).ToArray();
            Assert.That(summons, Has.Length.EqualTo(crosses.Length));
            Assert.That(summons.All(e => e.SuppressRewards), Is.True, "boss summons give no experience");
        }

                              

        static Vector3 Flat(Vector3 v) => Query.Flat(v);

        static void SetPrivate(Enemy enemy, string field, float value) =>
            typeof(Enemy).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, value);

                                                                                                             
        static void Place(Combatant body, Vector3 at)
        {
            body.ClearWaveState();
            V3CombatSmokeTest.Teleport(body, new Vector3(at.x, 0f, at.z));
            Physics.SyncTransforms();
        }

        static async Task Settle(Combatant body)
        {
            await Awaitable.FixedUpdateAsync();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (body.State == CharacterState.Knocked && Time.realtimeSinceStartup < deadline) await Awaitable.FixedUpdateAsync();
        }

        static Enemy SpawnStillSummon(GameManager game, Enemy hollow, Vector3 at)
        {
            var before = game.waves.Alive.ToArray();
            game.waves.SpawnExtrasAt(EnemyKind.Grunt, new[] { at }, true);
            var summon = game.waves.Alive.FirstOrDefault(e => e && e != hollow && e.IsAlive && !before.Contains(e));
            Assume.That(summon, Is.Not.Null, "the pool has a free Grunt");
            summon.enabled = false;                                
            return summon;
        }

        static int TrialSummons(GameManager game, Enemy hollow) =>
            game.waves.Alive.Count(e => e && e != hollow && e.IsAlive && e.IsTrialSummon);

        static int ActiveMarkers(GameManager game) => game.waves.preparedMarkers.Count(m => m && m.gameObject.activeSelf);

        static async Task<(GameManager game, Enemy[] targets, Enemy tyrant)> TyrantSandbox(float ahead)
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.player.Invulnerable = true;
            game.player.enabled = true;                                           
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * ahead);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");
                                                                                                            
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            return (game, targets, tyrant);
        }

        static async Task<(GameManager game, WorldMap world, Enemy hollow)> StartTrial()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active && Object.FindAnyObjectByType<EnemyLab>(), 12f,
                "scene, localization, map and lab");
            var game = GameManager.Instance;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The lab never reached combat; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Loadout) game.ExecuteUiCommand(ArenaUiCommand.ChooseLoadout, 0);
                await Awaitable.NextFrameAsync();
            }

            var lab = Object.FindAnyObjectByType<EnemyLab>();
            var world = WorldMap.Active;
            lab.selected = EnemyKind.Hollow;
            Assume.That(lab.Spawn(), Is.SameAs(world.hollow), "the lab enters the void trial");
            Assume.That(world.TrialActive, Is.True);
            await Awaitable.FixedUpdateAsync();
            return (game, world, world.hollow);
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
