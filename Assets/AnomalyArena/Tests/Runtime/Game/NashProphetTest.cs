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
    public class NashProphetTest
    {
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

                                                        

        [Test]
        public void NashMoveFor_ByPhaseAndDistance_PicksOnlyThatBandsMoves()
        {
            var b = new BossTuning();
            for (int phase = 1; phase <= 3; phase++)
            {
                                                                                                      
                var close = new[] { Enemy.PulseMove, Enemy.VoidSummonMove, Enemy.RingMove };
                var far = phase == 1 ? new[] { Enemy.RingMove }
                    : new[] { Enemy.RingMove, Enemy.PredictiveVolleyMove, Enemy.VoidPillarMove, Enemy.GravityPullMove };
                Assert.That(Moves(phase, true, true, false, b), Is.EquivalentTo(close), $"phase {phase}, close: pulse, summon and ring, never the pillars");
                Assert.That(Moves(phase, false, true, false, b), Is.EquivalentTo(far), $"phase {phase}, far: the ring; the volley, the pillars and the pull from phase 2");
                Assert.That(Moves(phase, true, false, false, b), Has.No.Member(Enemy.VoidSummonMove),
                    $"phase {phase}: no summon while one lives or its rest runs");
            }
        }

        [Test]
        public void NashMoveFor_WhilePillarsStand_TheGravityPullComesFarMoreOften()
        {
            var b = new BossTuning();
            int Pulls(bool pillars) => Enumerable.Range(0, 1000)
                .Count(i => Enemy.NashMoveFor(2, false, true, pillars, i / 1000f, b) == Enemy.GravityPullMove);
            Assert.That(Pulls(true), Is.GreaterThan(Pulls(false) * 1.5f), "pillars up: the pull comes far more often, to drag the player in");
        }

        [Test]
        public void NashMoveFor_TheWeightsAsDecided_CloseRingAsLikelyAsThePulse_FarFourWays()
        {
            var b = new BossTuning();
            int Count(int phase, bool near, bool canSummon, bool pillars, int move) =>
                Enumerable.Range(0, 1000).Count(i => Enemy.NashMoveFor(phase, near, canSummon, pillars, i / 1000f, b) == move);
                                                                                                                        
            Assert.That(Count(1, true, true, false, Enemy.RingMove), Is.EqualTo(Count(1, true, true, false, Enemy.PulseMove)).Within(2), "close: the ring as likely as the pulse");
            Assert.That(Count(1, true, false, false, Enemy.RingMove), Is.EqualTo(500).Within(2), "no summon ready: ring or pulse, half and half");
                                                                                                   
            Assert.That(Count(2, false, true, false, Enemy.VoidPillarMove), Is.EqualTo(1000 / 3.8f).Within(3), "far: the pillars weigh 1 of 3.8");
            Assert.That(Count(3, false, true, true, Enemy.GravityPullMove), Is.EqualTo(1000 * 2f / 5f).Within(3), "pillars up: the pull weighs 2 of 5");
            b.nashNearRingWeight = 0f;
            Assert.That(Count(2, true, true, false, Enemy.RingMove), Is.Zero, "a close ring weight of 0 keeps the ring a far move only");
        }

        [Test]
        public void Tuning_PhaseThreeIsHarder_ThreePhasesByHpShare()
        {
            var b = new BossTuning();
            Assert.That(b.nashPhase2HpShare, Is.GreaterThan(b.nashPhase3HpShare).And.LessThan(1f), "three phases by HP share");
                                                                                                                                            
            float platform = b.prophetPlatformRadii.x;
            float spared1 = platform - Enemy.NashPulsePush(1, b), spared2 = platform - Enemy.NashPulsePush(2, b);
            float spared3 = platform - Enemy.NashPulsePush(3, b);
            Assert.That(spared2, Is.LessThan(spared1), "phase 2: the repulse pushes harder, so it spares less");
            Assert.That(spared3, Is.LessThan(spared2), "phase 3 harder still");
            Assert.That(spared3, Is.LessThan(spared1 - 1.5f), "phase 3: the repulse spares only a player close to it");
            Assert.That(b.nashPhase3PullSpeed, Is.GreaterThan(b.nashPullSpeed), "and the pull draws harder");
            Assert.That(b.nashPhase2Rings, Is.GreaterThan(1), "several rings in a row from phase 2");
            Assert.That(b.nashPhase3Rings, Is.GreaterThan(b.nashPhase2Rings), "more in phase 3");
            Assert.That(b.nashPhase3VolleyShots, Is.GreaterThan(b.nashVolleyShots), "more volley shots in phase 3");
            Assert.That(b.nashPhase3PillarRadius, Is.GreaterThan(b.nashPillarRadius), "bigger pillars in phase 3");
            Assert.That(b.nashDamageFactor, Is.GreaterThan(0f), "a fixed damage factor of its own");
        }

        [Test]
        public void VolleyLeadPoint_AMovingTarget_IsMetWhereItWillBe()
        {
            Vector3 from = Vector3.zero, target = new(0f, 0f, 10f), velocity = new(3f, 0f, 0f);
            const float speed = 12f;
            Vector3 meet = Enemy.VolleyLeadPoint(from, target, velocity, speed);
            Assert.That(Vector3.Distance(meet, target + velocity * (meet.magnitude / speed)), Is.LessThan(.1f), "shot and target arrive together");
            Assert.That(Enemy.VolleyLeadPoint(from, target, Vector3.zero, speed), Is.EqualTo(target), "a target standing still is aimed at directly");
        }

                                                  

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(90000)]
        public async Task TrialTerrain_EachPortalLeadsToItsOwnFragmentInAnotherDirection_AndBack()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;                                                 
            var b = game.waves.bosses;
            var player = game.player;
            Vector3 centre = Flat(world.trialPlatform.position);
            Assert.That(world.TrialFragmentCount, Is.EqualTo(b.nashFragmentCount).And.GreaterThan(1), "broken fragments float round the platform");
            Assert.That(world.TrialPortalCount, Is.EqualTo(world.TrialFragmentCount), "one black-hole portal on the platform for each");
            Assert.That(b.nashFragmentRadius / player.Radius, Is.InRange(2.5f, 4f), "a fragment is about three player bodies across");
            var built = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Count(t => t.name.StartsWith("TrialFragment ("));
            Assert.That(built, Is.EqualTo(world.TrialFragmentCount), "built at runtime, one disc each");
            for (int i = 0; i < world.TrialFragmentCount; i++)
            {
                Assert.That(Flat(world.TrialFragmentCentre(i) - centre).magnitude - b.nashFragmentRadius, Is.GreaterThan(world.trialRadius),
                    $"void lies between the platform and fragment {i}");
                Assert.That(Flat(world.TrialPortalCentre(i) - centre).magnitude + b.nashPortalTrigger, Is.LessThan(world.trialRadius),
                    $"portal {i} stands on the platform");
            }

            var reached = new HashSet<int>();
            using var keyboard = new TestKeyboard();
            for (int i = 0; i < world.TrialPortalCount; i++)
            {
                int fragment = world.TrialPortalFragment(i);
                Vector3 portal = world.TrialPortalCentre(i), middle = world.TrialFragmentCentre(fragment);
                Place(player, portal);
                await keyboard.TapUntil(KeyCode.E, () => Flat(player.Position - middle).magnitude <= b.nashFragmentRadius,
                    $"E on portal {i} takes the player to fragment {fragment}");
                Assert.That(Vector3.Angle(portal - centre, middle - centre), Is.GreaterThan(90f), "the player comes out in another direction");
                reached.Add(fragment);
                for (int step = 0; step < 10; step++) await Awaitable.FixedUpdateAsync();
                Assert.That(Flat(player.Position - middle).magnitude, Is.LessThanOrEqualTo(b.nashFragmentRadius), "set down clear of the way back, they stay");
                Assert.That(player.State, Is.Not.EqualTo(CharacterState.Falling), "a fragment is ground");

                Place(player, middle);
                await keyboard.TapUntil(KeyCode.E, () => Flat(player.Position - portal).magnitude < .1f, $"E on fragment {fragment}'s portal brings the player back");
                for (int step = 0; step < 10; step++) await Awaitable.FixedUpdateAsync();
                Assert.That(Flat(player.Position - portal).magnitude, Is.LessThan(.1f), "out of the portal they took, which does not send them straight back");
            }

            Assert.That(reached, Has.Count.EqualTo(world.TrialFragmentCount), "every portal has its own fragment");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task TrialPortal_StandingOnItDoesNothing_EOnItTakesThePlayerThereAndBack_EOffEveryPortalDoesNothing()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;                                                 
            var b = game.waves.bosses;
            var player = game.player;
            using var keyboard = new TestKeyboard();
            Vector3 portal = world.TrialPortalCentre(0), middle = world.TrialFragmentCentre(world.TrialPortalFragment(0));

                                                                           
            Vector3 entry = Flat(player.Position);
            Assume.That(world.CanTakeTrialPortal(player, out _), Is.False, "set down off every portal");
            await keyboard.Tap(KeyCode.E);
            Assert.That(Flat(player.Position - entry).magnitude, Is.LessThan(.1f), "off every portal, E takes the player nowhere");
            Place(player, portal + Flat(portal - world.trialPlatform.position).normalized * (b.nashPortalTrigger + .4f));
            Vector3 beside = Flat(player.Position);
            await keyboard.Tap(KeyCode.E);
            Assert.That(Flat(player.Position - beside).magnitude, Is.LessThan(.1f), "beside a portal, E takes the player nowhere either");

                                                                                                   
            Place(player, portal);
            await StandFor(1f);
            Assert.That(Flat(player.Position - portal).magnitude, Is.LessThan(.1f), "standing on a portal takes the player nowhere by itself");
            await keyboard.Tap(KeyCode.E);
            Assert.That(Flat(player.Position - middle).magnitude, Is.LessThanOrEqualTo(b.nashFragmentRadius), "E on it takes them to its fragment");

                                        
            Place(player, middle);
            await StandFor(1f);
            Assert.That(Flat(player.Position - middle).magnitude, Is.LessThan(.1f), "standing on the fragment's portal takes the player nowhere by itself");
            Assert.That(PressE(), Is.True, "E on the fragment's portal");
            Assert.That(Flat(player.Position - portal).magnitude, Is.LessThan(.1f), "brings them back out of the portal they took");

                                                                                                                                              
            Assert.That(PressE(), Is.False, "E straight after a jump does nothing");
            await StandFor(b.nashPortalRearm + .05f);
            await keyboard.Tap(KeyCode.E);
            Assert.That(Flat(player.Position - middle).magnitude, Is.LessThanOrEqualTo(b.nashFragmentRadius),
                "once the portals are awake again, E takes the portal the player came out of");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task TrialPortal_KnockedStunnedOrFrozenOnIt_EDoesNothing_FreeToActAgainItsLabelOffersEAndETakesIt()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;
            var b = game.waves.bosses;
            var player = game.player;
            using var keyboard = new TestKeyboard();
            Vector3 portal = world.TrialPortalCentre(0), middle = world.TrialFragmentCentre(world.TrialPortalFragment(0));
            var labels = Object.FindAnyObjectByType<V2WorldLabels>();
            Assume.That(labels && labels.holeLabels.Length > 0 && labels.holeLabels[0], Is.True, "the scene has its saved portal labels");
            var label = labels.holeLabels[0];
            string offer = ArenaText.Pair("E · TRAVEL","E · TRAVEL");

                                                                                 
            Vector3 across = Flat(portal - world.trialPlatform.position).normalized;
            Place(player, portal - across * .15f);
            player.Knockback(across, .3f, KnockKind.Hit);
            Assume.That(player.State, Is.EqualTo(CharacterState.Knocked), "the player slides from a knockback");
            Assume.That(Flat(player.Position - portal).magnitude, Is.LessThan(b.nashPortalTrigger), "on the portal while sliding");
            Assert.That(world.CanTakeTrialPortal(player, out _), Is.False, "knocked, the portal does not offer E");
            await keyboard.Tap(KeyCode.E);
            Assert.That(Flat(player.Position - middle).magnitude, Is.GreaterThan(b.nashFragmentRadius), "knocked, E does not take the portal");
            await Settle(player);

                             
            Place(player, portal);
            player.Stun(.6f);
            Assume.That(player.State == CharacterState.Normal && player.Stunned, Is.True, "in the normal state, only stunned");
            await keyboard.Tap(KeyCode.E);
            Assert.That(Flat(player.Position - portal).magnitude, Is.LessThan(.1f), "stunned, E does not take the portal");
            Assert.That(label.gameObject.activeSelf, Is.False, "and its label offers no E");

            await Until(() => !player.Stunned, 3f, "the stun wears off");

                            
            var status = ElementStatus.For(player);
            Place(player, portal);
            status.Add(ElementKind.Frost, 999f);
            Assume.That(status.Frozen, Is.True, "enough Frost freezes the player");
            await keyboard.Tap(KeyCode.E);
            Assert.That(Flat(player.Position - portal).magnitude, Is.LessThan(.1f), "frozen, E does not take the portal");
            Assert.That(label.gameObject.activeSelf, Is.False, "nor does its label offer E");
            status.ClearStatuses();

            await Until(() => label.gameObject.activeSelf, 1f, "free to act on the portal, its label shows");
            Assert.That(label.text, Is.EqualTo(offer), "and offers E");
            await keyboard.Tap(KeyCode.E);
            Assert.That(Flat(player.Position - middle).magnitude, Is.LessThanOrEqualTo(b.nashFragmentRadius), "free to act again, E takes the portal");
            Assert.That(label.gameObject.activeSelf, Is.False, "off the portals, the label is gone");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(120000)]
        public async Task PortalJump_AKeyStillHeldFromTheWayIn_LeavesThePlayerStandingAMoment_OffNeitherTheFragmentNorThePlatform()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;                                                                 
            var player = await Mortal(game);
            var b = game.waves.bosses;
            Assert.That(b.nashPortalArrivalHold, Is.GreaterThan(Reaction), "the player stands still longer than a quick reaction takes");
            Assert.That(b.nashPortalArrivalHold, Is.GreaterThanOrEqualTo(b.nashPortalRearm), "the portals are awake again before the player can walk");
            Vector3 centre = Flat(world.trialPlatform.position);
            try
            {
                foreach (bool sprint in new[] { false, true })
                    for (int i = 0; i < world.TrialPortalCount; i++)
                    {
                        string how = (sprint ? "sprinting" : "walking") + " through portal " + i;
                        Vector3 portal = world.TrialPortalCentre(i), middle = world.TrialFragmentCentre(world.TrialPortalFragment(i));
                                                                                                         
                        Vector3 key = (middle - centre).normalized;
                        Place(player, portal - key * 1.2f);
                        Vector3 landed = await GoThrough(player, key, sprint, () => Flat(player.Position - middle).magnitude <= b.nashFragmentRadius, how);
                        Assert.That(await ReactAndLetGo(player, landed), Is.LessThan(.15f), how + ": the held key does not walk the player on at once");
                        Assert.That(player.IsAlive && Flat(player.Position - middle).magnitude <= b.nashFragmentRadius, Is.True,
                            how + ": still on the fragment a second later");

                                                                                                                               
                        landed = await GoThrough(player, -key, sprint, () => Flat(player.Position - portal).magnitude < .1f, how + ", and back");
                        Assert.That(await ReactAndLetGo(player, landed), Is.LessThan(.15f), how + ", and back: the held key does not walk the player on at once");
                        Assert.That(player.IsAlive && Flat(player.Position - centre).magnitude <= world.trialRadius, Is.True,
                            how + ", and back: still on the platform a second later");
                    }
            }
            finally
            {
                HoldKeys(player, null);
            }
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(90000)]
        public async Task PortalJump_KeepingTheKeyTowardThePlatformDown_EAsThePlayerCrossesTheFragmentsPortalTakesThemBack_NeverPastIt()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;
            var player = await Mortal(game);
            var b = game.waves.bosses;
            Vector3 centre = Flat(world.trialPlatform.position);
            try
            {
                foreach (bool sprint in new[] { false, true })
                    for (int i = 0; i < world.TrialPortalCount; i++)
                    {
                        string how = (sprint ? "sprinting" : "walking") + " through portal " + i;
                        Vector3 portal = world.TrialPortalCentre(i), middle = world.TrialFragmentCentre(world.TrialPortalFragment(i));
                                                                                                                                             
                                                                                                                                  
                        Vector3 key = (centre - middle).normalized;
                        Place(player, portal - key * 1.2f);
                        await GoThrough(player, key, sprint, () => Flat(player.Position - middle).magnitude <= b.nashFragmentRadius, how);
                        await GoThrough(player, key, sprint, () => !player.IsAlive || Flat(player.Position - portal).magnitude < .1f,
                            how + ": E on the fragment's portal as they cross it");
                        HoldKeys(player, null);
                        Assert.That(player.IsAlive, Is.True, how + ": the fragment's own portal takes them back before they reach its far rim");
                    }
            }
            finally
            {
                HoldKeys(player, null);
            }
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task PortalJump_DuringTheGravityPull_ThePullWaitsWhileThePlayerStandsStill_ThenDrawsThemOn()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            Vector3 centre = Flat(world.trialPlatform.position);
            Place(player, centre + Vector3.back * 9f);
            Force(hollow, Enemy.GravityPullMove);
            await Until(() => hollow.GravityPulling, b.nashPullWindup + 2f, "the pull");
            Vector3 middle = world.TrialFragmentCentre(world.TrialPortalFragment(0));
            Place(player, world.TrialPortalCentre(0));
            Vector3 landed = await GoThrough(player, null, false, () => Flat(player.Position - middle).magnitude <= b.nashFragmentRadius,
                "through portal 0 while the pull runs");
            float jumped = Time.time;
            while (Time.time - jumped < b.nashPortalArrivalHold - .1f) await Awaitable.FixedUpdateAsync();
            Assume.That(hollow.GravityPulling, Is.True, "the pull is still on");
            Assert.That(Flat(player.Position - landed).magnitude, Is.LessThan(.15f), "while the player stands still after the jump the pull waits");
            float from = Flat(player.Position - centre).magnitude;
            await Until(() => Flat(player.Position - centre).magnitude < from - .3f, 1.5f, "then it draws them toward the middle again");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task TrialTerrain_TheVoidBetweenThePlatformAndAFragment_KillsAtOnce()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;
            var player = await Mortal(game);
            var b = game.waves.bosses;
            Vector3 centre = Flat(world.trialPlatform.position), fragment = world.TrialFragmentCentre(0);
            Vector3 outward = (fragment - centre).normalized;
            float gap = (world.trialRadius + Flat(fragment - centre).magnitude - b.nashFragmentRadius) * .5f;
            Assume.That(world.OnTrialGround(centre + outward * gap), Is.False, "the middle of the gap is void");

            Place(player, centre + outward * gap);
                                                                                                                                  
            for (int step = 0; step < 2 && player.IsAlive; step++) await Awaitable.FixedUpdateAsync();
            Assert.That(player.IsAlive, Is.False, "a player over the void falls at once");
            Assert.That(player.DeathCause, Is.EqualTo(DeathCause.FellIntoGap));
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task SummonOverTheVoid_SimplyFalls_AndCostsTheProphetNothing()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;
            float hp = hollow.Hp;
            Vector3 centre = Flat(world.trialPlatform.position);
                                                                                    
            Vector3 at = centre + Quaternion.Euler(0f, game.waves.bosses.nashPortalAngle, 0f) * Vector3.forward * (world.trialRadius + 1f);
            Assume.That(world.OnTrialGround(at), Is.False);
            var summon = SpawnSummon(game, hollow, EnemyKind.Grunt, at);
            await Until(() => !summon.IsAlive, 2f, "a summon over the void falls");
            for (int i = 0; i < 5; i++) await Awaitable.FixedUpdateAsync();
            Assert.That(hollow.Hp, Is.EqualTo(hp), "a fallen summon costs the Prophet nothing any more");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task PhaseTwo_ThePlatformKeepsItsSize_NoNewEdgeShows()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            hollow.HoldAttacks(60f);                                    
            float start = world.trialRadius;
            Assume.That(start, Is.EqualTo(b.prophetPlatformRadii.x).Within(.01f), "the phase-1 platform");
            ToPhase(hollow, 2, b);
            await Until(() => hollow.BossPhase == 2, 2f, "phase 2");
                                                                                                                                          
            float since = Time.time;
            while (Time.time - since < 3f)
            {
                Assert.That(world.trialRadius, Is.EqualTo(start).Within(.001f), "the platform keeps its size");
                Assert.That(world.trialPlatform.localScale.x, Is.EqualTo(start * 2f).Within(.01f), "and so does the disc");
                Assert.That(hollow.GetComponentsInChildren<LineRenderer>().Any(l => l.enabled && l.name.Contains("Edge")), Is.False,
                    "no new edge is drawn");
                await Awaitable.NextFrameAsync();
            }
        }

                                                  

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task TrialEntry_NoMoveDuringTheOpening_ThenAFarMoveForAPlayerSetDownFar()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            float start = Time.time;
            Assert.That(Flat(game.player.Position - world.trialPlatform.position).magnitude, Is.GreaterThan(b.nashNearRange),
                "the player is set down in the far band");
            await Until(() => hollow.WindingUp, world.trialOpeningGrace + 2f, "the first wind-up");
            Assert.That(Time.time - start, Is.GreaterThanOrEqualTo(world.trialOpeningGrace - .3f), "no move during the opening");
            Assert.That(hollow.BossAttackIndex, Is.EqualTo(Enemy.RingMove), "phase 1, far: only the ring (the pull comes in phase 2)");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(90000)]
        public async Task RepulsePulse_DrawsItsCircle_HitsAndPushesOnlyThePlayer_EachPhaseByItsOwnPush()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            Assume.That(player.knockbackScale, Is.EqualTo(1f), "the distances below assume an unscaled player push");
            Vector3 centre = Flat(world.trialPlatform.position);
            var summon = SpawnSummon(game, hollow, EnemyKind.Grunt, centre + Vector3.left * 3.5f);
            summon.enabled = false;                                
            var hits = new List<DamageInfo>();
            void Record(DamageInfo hit)
            {
                if (hit.dealer?.Owner == hollow) hits.Add(hit);
            }

            player.AttackReceived += Record;
            try
            {
                Place(player, centre + Vector3.back * 4f);
                float summonHp = summon.Hp;
                Vector3 summonAt = summon.Position;
                Force(hollow, Enemy.PulseMove);
                await Until(() => hollow.WindingUp && hollow.BossAttackIndex == Enemy.PulseMove, 2f, "the pulse wind-up");
                Assert.That(hollow.CircleTelegraph && hollow.TelegraphActive, Is.True, "a ground circle shows its reach");
                Assert.That(hollow.TelegraphRange, Is.EqualTo(b.nashPulseReach).Within(.01f), "as big as the pulse");
                await Until(() => hits.Count == 1, 3f, "the pulse hits the player");
                hollow.HoldAttacks(60f);
                Assert.That(hits[0].knockDistance, Is.EqualTo(Enemy.NashPulsePush(1, b)).Within(.01f), "and pushes them away by its phase-1 push");
                Assert.That(hits[0].amount, Is.EqualTo(hollow.definition.damage * b.nashDamageFactor * b.nashPulseDamage).Within(.01f),
                    "its damage comes from its own fixed factor, not the wave's");
                await Awaitable.FixedUpdateAsync();
                Assert.That(player.State, Is.EqualTo(CharacterState.Knocked), "the player slides away");
                Assert.That(summon.Hp, Is.EqualTo(summonHp), "its own summon is not hit");
                Assert.That(Flat(summon.Position - summonAt).magnitude, Is.LessThan(.1f), "nor pushed");
                Assert.That(Flat(hollow.Position - centre).magnitude, Is.LessThan(.05f), "it stands still in the middle");

                summon.DiscardWithoutReward();                                             
                await Settle(player);

                                                                                                                                            
                                                       
                Vector3 spot = centre + Vector3.back * (hollow.Radius + player.Radius + .2f);
                for (int phase = 2; phase <= 3; phase++)
                {
                    float slid = await PulseSlideFrom(spot, phase, game, world, hollow, hits), push = Enemy.NashPulsePush(phase, b);
                    Assert.That(hits[hits.Count - 1].knockDistance, Is.EqualTo(push).Within(.01f), $"phase {phase} pushes by its own push");
                    Assert.That(slid, Is.EqualTo(push).Within(Mathf.Max(.4f, push * .08f)), $"phase {phase}: the player slides about that far");
                }
            }
            finally
            {
                player.AttackReceived -= Record;
            }
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task RepulsePulse_BreaksEveryBulwarkInItsCircle_ABulwarkOutsideItStands()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            Vector3 centre = Flat(world.trialPlatform.position);
            Place(player, centre + Vector3.back * (b.nashPulseReach + 3f));                             
                                                                                                                                                         
            var inside = Bulwark(world, centre + Vector3.left * (b.nashPulseReach - .5f), 2f, player);
            var outside = Bulwark(world, centre + Vector3.right * (b.nashPulseReach + 2.5f), 2f, player);
            Assume.That(inside && outside && inside != outside, Is.True, "two bulwarks stand, one at the rim of its circle, one past it");
            await Awaitable.FixedUpdateAsync();
            Assume.That(inside.gameObject.activeSelf && outside.gameObject.activeSelf, Is.True, "both still up before the pulse");

            Force(hollow, Enemy.PulseMove);
            await Until(() => hollow.WindingUp && hollow.BossAttackIndex == Enemy.PulseMove, 2f, "the pulse wind-up");
            Assert.That(inside.gameObject.activeSelf, Is.True, "the wind-up breaks nothing");
            await Until(() => !inside.gameObject.activeSelf, b.nashPulseWindup + 2f, "the pulse breaks the bulwark in its circle");
            hollow.HoldAttacks(60f);
            Assert.That(outside.gameObject.activeSelf, Is.True, "a bulwark past its circle stands");
            Assert.That(world.SkillObjectStanding(SkillId.Bulwark, player), Is.True, "the one left still counts as the player's wall");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task RepulsePulse_PhaseOne_AHitInTheCloseRangeOnlyPushes_OnTheRimOfItsCircleItThrowsThePlayerOff()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            await Mortal(game);
                                                                                                                       
            var (safeEnd, rim) = await PulseBands(1, b.nashNearRange, game, world, hollow);
            Assert.That(rim - safeEnd, Is.InRange(.5f, 2f), "phase 1: only a thin ring at the rim of its circle throws the player off");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task RepulsePulse_PhaseTwo_PushesHarder_ABandByItIsSafe_FromInsideTheCloseRangeOutItsCircleThrowsThePlayerOff()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = await Mortal(game);
            float touching = hollow.Radius + player.Radius, twoBodies = 4f * player.Radius;
            var (safeEnd, rim) = await PulseBands(2, touching + twoBodies, game, world, hollow);
            Assert.That(safeEnd - touching, Is.GreaterThan(twoBodies), "phase 2: a band more than two player bodies wide by its body is safe");
            Assert.That(safeEnd, Is.GreaterThan(b.nashNearRange - .3f), "it still spares about the whole close range (owner 2026-10-27)");
            Assert.That(rim - safeEnd, Is.InRange(1f, 2.2f), "only the outer part of its circle throws the player off");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task RepulsePulse_PhaseThree_ABandTwoPlayerBodiesWideByItIsSafe_FartherOutItsCircleThrowsThePlayerOff()
        {
            var (game, world, hollow) = await StartTrial();
            var player = await Mortal(game);
            float touching = hollow.Radius + player.Radius, twoBodies = 4f * player.Radius;
                                                                                                                        
            var (safeEnd, rim) = await PulseBands(3, touching + twoBodies, game, world, hollow);
            Assert.That(safeEnd - touching, Is.GreaterThanOrEqualTo(twoBodies), "phase 3: the safe band is at least two player bodies wide");
            Assert.That(rim - safeEnd, Is.GreaterThan(safeEnd - touching), "but most of its circle throws the player off");
            Assert.That(world.trialRadius, Is.GreaterThan(rim + 1f), "on the platform that keeps its size the player can still step out of its circle");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(90000)]
        public async Task VoidSummon_WardensThenShades_KeepItInvulnerableUntilTheyDie_AndGiveNoExperience()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            Place(player, Flat(world.trialPlatform.position) + Vector3.back * (b.nashNearRange - 1.5f));
            Force(hollow, Enemy.VoidSummonMove);
            await Until(() => hollow.WindingUp && hollow.BossAttackIndex == Enemy.VoidSummonMove, 2f, "the summon wind-up");
            await Until(() => Crosses(game, EnemyKind.Warden) == b.nashSummonCount, b.nashSummonWindup + 2f, "a red cross on each summon point");
            Assert.That(Summons(game, hollow), Is.Empty, "nothing appears while the crosses show");
            await Until(() => Summons(game, hollow).Length == b.nashSummonCount, b.nashSummonWarning + 2f, "the Wardens appear");
            hollow.HoldAttacks(60f);
            var wardens = Summons(game, hollow);
            Assert.That(wardens.All(e => e.definition.kind == EnemyKind.Warden), Is.True, "phases 1-2 summon Wardens");
            Assert.That(wardens.All(e => e.SuppressRewards && e.IsTrialSummon), Is.True, "summons give no experience");
            Assert.That(Crosses(game, EnemyKind.Warden), Is.Zero, "the crosses are gone");

            Assert.That(hollow.ProphetWarded, Is.True, "while they live it is invulnerable");
            float hp = hollow.Hp;
            int blocks = hollow.WardBlockSequence;
            Assert.That(hollow.ReceiveDamage(DamageInfo.Attack(25f, player, Vector3.forward)), Is.False, "a hit is turned away");
            Assert.That(hollow.ReceiveDamage(new DamageInfo { amount = 25f, source = DamageSource.Poison }), Is.False, "so is a poison tick");
            Assert.That(hollow.Hp, Is.EqualTo(hp), "no HP lost");
            Assert.That(hollow.WardBlockSequence, Is.EqualTo(blocks + 2), "the ward flashes for each");
            var labels = Object.FindAnyObjectByType<V2WorldLabels>();
            if (labels)
                await Until(() => labels.enemyLabels.Any(l => l.root && l.root.gameObject.activeInHierarchy && l.name &&
                                                            l.name.text.Contains(ArenaText.Pair("INVULNERABLE","INVULNERABLE"))), 2f,
                    "its label says it is invulnerable");

            foreach (var warden in wardens) warden.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall, ignoreShield = true });
            Assert.That(hollow.ProphetWarded, Is.False, "with every summon dead the ward is gone");
            Assert.That(hollow.ReceiveDamage(DamageInfo.Attack(25f, player, Vector3.forward)), Is.True);
            Assert.That(hollow.Hp, Is.LessThan(hp), "and it takes damage again");

            ToPhase(hollow, 3, b);
            await Until(() => hollow.BossPhase == 3, 2f, "phase 3");
            Force(hollow, Enemy.VoidSummonMove);
            await Until(() => Summons(game, hollow).Length == b.nashPhase3SummonCount, b.nashSummonWindup + b.nashSummonWarning + 3f,
                "the phase-3 summons appear");
            Assert.That(Summons(game, hollow).All(e => e.definition.kind == EnemyKind.Shade), Is.True, "phase 3 summons Shades");
            Assert.That(hollow.ProphetWarded, Is.True, "and they ward it too");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task GravityPull_DrawsOnlyThePlayerTowardTheMiddle_HarderInPhaseThree_StillWalkable()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            Assert.That(b.nashPhase3PullSpeed, Is.LessThan(player.moveSpeed), "even the phase-3 pull is slower than walking, so the player can walk out");
            Vector3 centre = Flat(world.trialPlatform.position);
            var summon = SpawnSummon(game, hollow, EnemyKind.Grunt, centre + Vector3.right * 8f);
            summon.enabled = false;
            Vector3 summonAt = summon.Position;

            float drawn = await PullDrift(hollow, player, centre, b);
            Assert.That(drawn, Is.EqualTo(b.nashPullSpeed).Within(b.nashPullSpeed * .35f), "it draws the player in at its speed");
            Assert.That(Flat(summon.Position - summonAt).magnitude, Is.LessThan(.1f), "its summons are left where they stand");

            summon.DiscardWithoutReward();                                             
            ToPhase(hollow, 3, b);
            await Until(() => hollow.BossPhase == 3, 2f, "phase 3");
            float drawn3 = await PullDrift(hollow, player, centre, b);
            Assert.That(drawn3, Is.EqualTo(b.nashPhase3PullSpeed).Within(b.nashPhase3PullSpeed * .35f), "phase 3 at its own speed");
            Assert.That(drawn3, Is.GreaterThan(drawn), "harder than before");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task VoidPillars_PhaseTwo_WarnFirstThenKillAPlayerStandingInOne()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = await Mortal(game);
            Vector3 centre = Flat(world.trialPlatform.position);
            ToPhase(hollow, 2, b);
            await Until(() => hollow.BossPhase == 2, 2f, "phase 2");
            Place(player, centre + Vector3.back * 5f);
            Force(hollow, Enemy.VoidPillarMove);
            await Until(() => hollow.VoidPillars.Count > 0, b.nashPillarWindup + 2f, "the pillar circles");
            hollow.HoldAttacks(60f);
            var pillars = hollow.VoidPillars.ToArray();
            Assert.That(pillars, Has.Length.EqualTo(b.nashPillarCount), "phase 2 casts its count");
            Assert.That(Flat(pillars[0].centre - player.Position).magnitude, Is.LessThan(.1f), "the first stands on the player");
            foreach (var pillar in pillars)
            {
                Assert.That(pillar.radius, Is.EqualTo(b.nashPillarRadius).Within(.01f), "phase-2 size");
                Assert.That(Flat(pillar.centre - hollow.Position).magnitude, Is.GreaterThanOrEqualTo(hollow.Radius + pillar.radius - .01f),
                    "never on its own body");
                Assert.That(world.OnTrialGround(pillar.centre), Is.True, "over the platform");
                Assert.That(pillar.Standing, Is.False, "a circle warns first");
            }

            while (!hollow.VoidPillars[0].Standing)
            {
                Assert.That(player.IsAlive, Is.True, "nothing kills while the circles fill");
                await Awaitable.FixedUpdateAsync();
            }

            await Until(() => !player.IsAlive, 1f, "the standing pillar kills the player inside it");
            Assert.That(player.LastDamagedBy, Is.SameAs(hollow), "the Prophet's pillar did it");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task VoidPillars_SpareAPlayerWhoStepsOut_AndGoAwayAfterTheirLife()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = await Mortal(game);
            Vector3 centre = Flat(world.trialPlatform.position);
            ToPhase(hollow, 2, b);
            await Until(() => hollow.BossPhase == 2, 2f, "phase 2");
            Place(player, centre + Vector3.back * 5f);
            Force(hollow, Enemy.VoidPillarMove);
            await Until(() => hollow.VoidPillars.Count > 0, b.nashPillarWindup + 2f, "the pillar circles");
            hollow.HoldAttacks(60f);
            Place(player, centre + new Vector3(-3f, 0f, -6.5f));                                                     
            Assume.That(hollow.VoidPillars.All(p => Flat(p.centre - player.Position).magnitude > p.radius), Is.True, "outside every pillar");
            await Until(() => hollow.VoidPillars.Count > 0 && hollow.VoidPillars.All(p => p.Standing), b.nashPillarWarning + 1f, "the pillars stand");
            for (int i = 0; i < 10; i++) await Awaitable.FixedUpdateAsync();
            Assert.That(player.IsAlive, Is.True, "a pillar kills only who stands in it");
            await Until(() => hollow.VoidPillars.Count == 0, b.nashPillarLife + 1f, "the pillars go away after their life");
            Assert.That(player.IsAlive, Is.True);
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task EquilibriumRing_OneRingThenSeveral_EvenlySpaced_ReachingTheFragments()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            int pool = game.waves.preparedBolts.Length;
            Assert.That(b.nashRingShots * b.nashPhase2Rings, Is.LessThanOrEqualTo(pool), "phase-2 rings fit the saved shots");
            Assert.That(b.nashPhase3RingShots * b.nashPhase3Rings, Is.LessThanOrEqualTo(pool), "phase-3 rings fit the saved shots");
            Assert.That(b.nashPhase3VolleyShots + b.nashPhase3RingShots * (b.nashPhase3Rings - 1), Is.LessThanOrEqualTo(pool),
                "a volley soon after rings still finds free shots");
            Vector3 centre = Flat(world.trialPlatform.position);

            Force(hollow, Enemy.RingMove);
            await Until(() => hollow.WindingUp && hollow.BossAttackIndex == Enemy.RingMove, 2f, "the ring wind-up");
            await Until(() => !hollow.WindingUp, 3f, "the ring fires");
            hollow.HoldAttacks(60f);
            var shots = BossShots(game);
            Assert.That(shots, Has.Length.EqualTo(b.nashRingShots), "one ring in phase 1");
            var angles = shots.Select(s => Mathf.Repeat(Mathf.Atan2(s.Direction.x, s.Direction.z) * Mathf.Rad2Deg, 360f)).OrderBy(a => a).ToArray();
            for (int i = 0; i < angles.Length; i++)
                Assert.That(Mathf.Repeat(angles[(i + 1) % angles.Length] - angles[i], 360f), Is.EqualTo(360f / b.nashRingShots).Within(1f), "evenly spaced");
            Vector3 toPlayer = Flat(game.player.Position - hollow.Position).normalized;
            Assert.That(shots.Min(s => Vector3.Angle(s.Direction, toPlayer)), Is.LessThan(2f), "one shot aimed at the player");
                                                                                                                        
            float past = Mathf.Max(Flat(world.TrialFragmentCentre(0) - centre).magnitude - b.nashFragmentRadius, world.trialRadius + 2.5f);
            await Until(() => BossShots(game).Any(s => Flat(s.transform.position - centre).magnitude > past), 5f,
                "the ring flies on past the platform to the fragments");

            await Until(() => BossShots(game).Length == 0, 8f, "the first ring is gone");
            ToPhase(hollow, 2, b);
            await Until(() => hollow.BossPhase == 2, 2f, "phase 2");
            Force(hollow, Enemy.RingMove);
            int launched = await CountLaunches(game, b.nashRingWindup + b.nashRingInterval * b.nashPhase2Rings + .6f);
            Assert.That(launched, Is.EqualTo(b.nashRingShots * b.nashPhase2Rings), "phase 2 fires its rings in a row");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task PredictiveVolley_PhaseTwo_FiresItsShotsAtThePlayer_AndEachTurnsAfterThem()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            Vector3 centre = Flat(world.trialPlatform.position);
            ToPhase(hollow, 2, b);
            await Until(() => hollow.BossPhase == 2, 2f, "phase 2");
            Place(player, centre + Vector3.back * 9f);
            Force(hollow, Enemy.PredictiveVolleyMove);
            int launched = await CountLaunches(game, b.nashVolleyWindup + b.nashVolleyInterval * b.nashVolleyShots + .6f);
            Assert.That(launched, Is.EqualTo(b.nashVolleyShots), "a phase-2 volley fires its count");
            await Until(() => BossShots(game).Length == 0, 4f, "that volley is over");

            Place(player, centre + Vector3.back * 9f);
            Force(hollow, Enemy.PredictiveVolleyMove);
            await Until(() => BossShots(game).Length > 0, b.nashVolleyWindup + 2f, "the first shot of the next volley");
            hollow.HoldAttacks(60f);
            var shot = BossShots(game)[0];
            Assert.That(Vector3.Angle(shot.Direction, Flat(player.Position - shot.transform.position)), Is.LessThan(3f),
                "aimed at where a player standing still will be: on them");

            Place(player, player.Position + Vector3.right * 2.5f);               
            float before = Vector3.Angle(shot.Direction, Flat(player.Position - shot.transform.position));
            for (int i = 0; i < 8; i++) await Awaitable.FixedUpdateAsync();
            Assume.That(shot.gameObject.activeSelf, Is.True, "the shot is still in flight");
            float after = Vector3.Angle(shot.Direction, Flat(player.Position - shot.transform.position));
            Assert.That(after, Is.LessThan(before - 2f), "it turns after the player");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task BossPush_IsNoLongerStoppedAtThePlatformEdge_ButStillStopsShortOfAnOpenHole()
        {
            var (game, world, hollow) = await StartTrial();
            hollow.enabled = false;               
            var player = await Mortal(game);
            Assume.That(player.knockbackScale, Is.EqualTo(1f), "the distances below assume an unscaled player push");
            var b = game.waves.bosses;
            Vector3 centre = Flat(world.trialPlatform.position);
            float r = world.trialRadius;

                                                                                                               
            Vector3 hole = centre + Vector3.back * 9.5f;
            world.CreateVoidHole(hole, 3f, 0f, 10f, player, false);
            Assume.That(world.IsHole(hole), Is.True, "the hole is open");
            Place(player, centre + Vector3.back * 6f);
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, hollow, Vector3.back, 8f)), Is.True);
            await Settle(player);
            Assert.That(Flat(player.Position - hole).magnitude, Is.GreaterThanOrEqualTo(1.5f + b.holeKnockMargin - .2f),
                "a boss hit stops the player a margin short of an open hole");
            Assert.That(player.IsAlive, Is.True);

                                                                                                                                
            Vector3 outward = Quaternion.Euler(0f, b.nashPortalAngle, 0f) * Vector3.forward;
            Vector3 start = centre + outward * (r - 3.5f);
            Assume.That(world.OnTrialGround(centre + outward * (r + 1.5f)), Is.False, "void beyond the edge on that line");
            Assert.That(world.LimitBossKnockback(start, outward, 6f), Is.EqualTo(6f).Within(.01f), "the edge does not cut a boss push short");
            Place(player, start);
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, hollow, outward, 6f)), Is.True);
            await Until(() => !player.IsAlive, 2f, "a boss push past the edge throws the player into the void");
            Assert.That(player.DeathCause, Is.EqualTo(DeathCause.FellIntoGap));
        }

                              

                                                                                                                                                        
        static TemporaryWall Bulwark(WorldMap world, Vector3 at, float width, Combatant user)
        {
            world.CreateWall(at, width, 60f, 0f, user);
            Physics.SyncTransforms();
            return world.temporaryWalls.FirstOrDefault(w => w && w.gameObject.activeSelf && Flat(w.transform.position - at).magnitude < .1f);
        }

        static Vector3 Flat(Vector3 v) => Query.Flat(v);

        static int[] Moves(int phase, bool near, bool canSummon, bool pillars, BossTuning b) =>
            Enumerable.Range(0, 200).Select(i => Enemy.NashMoveFor(phase, near, canSummon, pillars, i / 200f, b)).Distinct().ToArray();

                                                                                                                                    
        internal static void Force(Enemy hollow, int move)
        {
            hollow.ForceNextBossMove(move);
            typeof(Enemy).GetField("cooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hollow, 0f);
        }

                                                                                                                            
        internal static void ToPhase(Enemy hollow, int phase, BossTuning b)
        {
            float share = phase >= 3 ? b.nashPhase3HpShare : b.nashPhase2HpShare;
            hollow.ReceiveDamage(new DamageInfo { amount = hollow.Hp - hollow.maxHp * share + 1f, source = DamageSource.Wall, ignoreShield = true });
        }

                                                                                                                                                    
        const float Reaction = .35f;

                                                                                                                           
        static void HoldKeys(PlayerController player, Vector3? key, bool sprint = false)
        {
            player.debugMoveHeld = key.HasValue ? new Vector2(key.Value.x, key.Value.z) : (Vector2?)null;
            player.debugSprintHeld = key.HasValue ? sprint : (bool?)null;
        }

                                                                                                                                             
                                                                                                                                              
                                                                                 
        static async Task<Vector3> GoThrough(PlayerController player, Vector3? key, bool sprint, Func<bool> through, string operation)
        {
            HoldKeys(player, key, sprint);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!through())
            {
                if (!player.IsAlive) Assert.Fail("The player fell before this: " + operation);
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                PressE();
                await Awaitable.FixedUpdateAsync();
            }

            return Flat(player.Position);
        }

                                                                                                                                               
                                                                                                                                              
        static bool PressE()
        {
            var game = GameManager.Instance;
            var world = WorldMap.Active;
            return world && game && game.player && (game.IsCombatActive || game.CanPlayerMove) && world.TryEnterPortal(game.player);
        }

                                                                                                                                           
                                                              
        static async Task<float> ReactAndLetGo(PlayerController player, Vector3 landed)
        {
            float jumped = Time.time;
            while (Time.time - jumped < Reaction) await Awaitable.FixedUpdateAsync();
            float moved = Flat(player.Position - landed).magnitude;
            HoldKeys(player, null);
            while (Time.time - jumped < 1f) await Awaitable.FixedUpdateAsync();
            return moved;
        }

                                                                                                                                   
        internal static async Task EnterPhase(Enemy hollow, int phase, BossTuning b)
        {
            ToPhase(hollow, phase, b);
            await Until(() => hollow.BossPhase == phase, 2f, $"phase {phase}");
        }

                                                                                                                                               
                                                                                                                                            
                                                   
        static async Task<float> PulseSlideFrom(Vector3 spot, int phase, GameManager game, WorldMap world, Enemy hollow, List<DamageInfo> hits)
        {
            var b = game.waves.bosses;
            var player = game.player;
            Place(player, spot);
            player.Heal(player.maxHp);
            await EnterPhase(hollow, phase, b);
            int before = hits.Count;
            Force(hollow, Enemy.PulseMove);
            await Until(() => hits.Count > before, b.nashPulseWindup + 2f, $"the phase-{phase} pulse hits");
            hollow.HoldAttacks(60f);
            await Settle(player);
            Assert.That(player.IsAlive && world.OnTrialGround(player.Position), Is.True, $"phase {phase}: thrown from by its body, the player stays on the platform");
            return Flat(player.Position - spot).magnitude;
        }

                                                                                                                                                 
                                                                                                                                               
                                                                                                                                              
                                                                                                                                      
        internal static async Task<(float safeEnd, float rim)> PulseBands(int phase, float safeAt, GameManager game, WorldMap world, Enemy hollow)
        {
            var b = game.waves.bosses;
            var player = game.player;
            Assume.That(player.knockbackScale, Is.EqualTo(1f), "the distances below assume an unscaled player push");
            Vector3 centre = Flat(world.trialPlatform.position);
            hollow.HoldAttacks(60f);                                
            Place(player, centre + Vector3.back * safeAt);
            if (phase > 1) await EnterPhase(hollow, phase, b);
            float edge = world.trialRadius, rim = b.nashPulseReach + player.Radius, touching = hollow.Radius + player.Radius;

            player.Heal(player.maxHp);
            float hp = player.Hp;
            Force(hollow, Enemy.PulseMove);
            await Until(() => player.Hp < hp, b.nashPulseWindup + 2f, $"the phase-{phase} pulse hits the player {safeAt:0.##} cells out");
            hollow.HoldAttacks(60f);
            await Settle(player);
            Assert.That(player.IsAlive && world.OnTrialGround(player.Position), Is.True,
                $"phase {phase}: hit {safeAt:0.##} cells out, the player only slides and stays on the platform");
            float slid = Flat(player.Position - centre).magnitude - safeAt, safeEnd = edge - slid;
            Debug.Log($"[pulse-bands] phase {phase}: platform {edge:0.##}, reach {b.nashPulseReach:0.##} (hits a player within {rim:0.##}), " +
                      $"push {Enemy.NashPulsePush(phase, b):0.##} (slid {slid:0.###}); safe {touching:0.##}-{safeEnd:0.##} ({safeEnd - touching:0.##} wide), " +
                      $"throws off {safeEnd:0.##}-{Mathf.Min(rim, edge):0.##} ({Mathf.Min(rim, edge) - safeEnd:0.##} wide)");

            float deadlyAt = safeEnd + .3f;
            Assume.That(deadlyAt, Is.LessThan(rim - .1f), "a little past the safe band is still inside its circle");
            Place(player, centre + Vector3.back * deadlyAt);
            player.Heal(player.maxHp);
            Force(hollow, Enemy.PulseMove);
                                                                                                              
            await Until(() => !player.IsAlive, b.nashPulseWindup + 3f, $"phase {phase}: hit {deadlyAt:0.##} cells out, the pulse throws the player off");
            Assert.That(player.DeathCause, Is.EqualTo(DeathCause.FellIntoGap), "into the void");
            Assert.That(player.LastDamagedBy, Is.SameAs(hollow), "thrown by its pulse");
            return (safeEnd, rim);
        }

                                                                                                             
        internal static void Place(Combatant body, Vector3 at)
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

                                                                                           
        static async Task StandFor(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.FixedUpdateAsync();
        }

                                                                                                                             
        internal static async Task<PlayerController> Mortal(GameManager game)
        {
            Object.FindAnyObjectByType<EnemyLab>().invulnerable = false;
            game.player.Invulnerable = false;
            await Awaitable.NextFrameAsync();
            Assume.That(game.player.Invulnerable, Is.False, "the lab lets the player die");
            return game.player;
        }

                                                                                                                             
        static Enemy SpawnSummon(GameManager game, Enemy hollow, EnemyKind kind, Vector3 at)
        {
            var before = game.waves.Alive.ToArray();
            game.waves.SpawnExtrasAt(kind, new[] { at }, true);
            var summon = game.waves.Alive.FirstOrDefault(e => e && e != hollow && e.IsAlive && !before.Contains(e));
            Assume.That(summon, Is.Not.Null, "the pool has a free body");
            return summon;
        }

        static Enemy[] Summons(GameManager game, Enemy hollow) =>
            game.waves.Alive.Where(e => e && e != hollow && e.IsAlive && e.IsTrialSummon).ToArray();

        static int Crosses(GameManager game, EnemyKind kind) => game.waves.preparedMarkers.Count(m =>
            m && m.gameObject.activeSelf && m.bossSummon && m.definition && m.definition.kind == kind);

        static EnemyBolt[] BossShots(GameManager game) => game.waves.preparedBolts.Where(s => s && s.gameObject.activeSelf && s.IsBossShot).ToArray();

                                                                                                                                       
        static async Task<int> CountLaunches(GameManager game, float seconds)
        {
            var flying = new HashSet<EnemyBolt>(BossShots(game));
            int launched = 0;
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                await Awaitable.FixedUpdateAsync();
                var now = BossShots(game);
                launched += now.Count(s => !flying.Contains(s));
                flying = new HashSet<EnemyBolt>(now);
            }

            return launched;
        }

                                                                                                                                                  
        static async Task<float> PullDrift(Enemy hollow, PlayerController player, Vector3 centre, BossTuning b)
        {
            Place(player, centre + Vector3.back * 9f);
            Force(hollow, Enemy.GravityPullMove);
            await Until(() => hollow.GravityPulling, b.nashPullWindup + 2f, "the pull");
            float from = Flat(player.Position - centre).magnitude, start = Time.time;
            while (Time.time - start < 1f) await Awaitable.FixedUpdateAsync();
            float drift = (from - Flat(player.Position - centre).magnitude) / (Time.time - start);
            await Until(() => !hollow.GravityPulling, b.nashPullSeconds + 1f, "the pull ends");
            hollow.HoldAttacks(60f);
            return drift;
        }

        internal static async Task<(GameManager game, WorldMap world, Enemy hollow)> StartTrial()
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

        internal static async Task Until(Func<bool> completed, float timeout, string operation)
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
