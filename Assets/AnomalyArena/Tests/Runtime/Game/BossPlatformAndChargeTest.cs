using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                                                              
                                                                                                                                          
                                                                                                                                         
                                                                                                                                            
                                                                                                                                         
                                                                                
    [TestFixture]
    [Category("V3Smoke")]
    public class BossPlatformAndChargeTest
    {
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";
        const string ArenaScene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

                                              

        [Test]
        public void ProphetTuning_ThePulsePushesHarderEachPhase_TheOwnersNumbers_NoShrinkOrEdgeWarningIsLeft()
        {
            var b = new BossTuning();
            Assert.That(Enemy.NashPulsePush(1, b), Is.EqualTo(4.8f), "owner 2026-10-26: phase 1 pushes 4.8 cells");
            Assert.That(Enemy.NashPulsePush(2, b), Is.EqualTo(5.2f), "phase 2 pushes 5.2 (owner 2026-10-27)");
            Assert.That(Enemy.NashPulsePush(3, b), Is.EqualTo(7f), "phase 3 pushes 7");
            const BindingFlags members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.That(typeof(BossTuning).GetField("nashEdgeWarning", members), Is.Null, "no new-edge warning is left to tune");
            Assert.That(typeof(Enemy).GetProperty("EdgeHoldLeft", members), Is.Null, "nor a hold before a shrink");
            Assert.That(typeof(Enemy).GetMethod("TickNashPlatform", members), Is.Null, "nor the shrink itself");
        }

                                                     

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task ProphetPhaseThree_FightsOnThePhaseOnePlatform_FragmentsAndPortalsStay_APillarStandsOnAPlayerPastTheOldEdge()
        {
            var (game, world, hollow) = await NashProphetTest.StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            Vector3 centre = Flat(world.trialPlatform.position);
            float start = world.trialRadius;
            Assume.That(start, Is.EqualTo(b.prophetPlatformRadii.x).Within(.01f), "the phase-1 platform");
            var fragments = Enumerable.Range(0, world.TrialFragmentCount).Select(world.TrialFragmentCentre).ToArray();
            var portals = Enumerable.Range(0, world.TrialPortalCount).Select(world.TrialPortalCentre).ToArray();
            Assume.That(portals, Is.Not.Empty, "the scene has its trial portals");

            hollow.HoldAttacks(60f);                                               
            await NashProphetTest.EnterPhase(hollow, 2, b);
            await NashProphetTest.EnterPhase(hollow, 3, b);
                                                                      
            float since = Time.time;
            while (Time.time - since < 2.5f) await Awaitable.NextFrameAsync();

            Assert.That(world.trialRadius, Is.EqualTo(start).Within(.001f), "phase 3 is fought on the phase-1 platform");
            Assert.That(world.trialPlatform.localScale.x, Is.EqualTo(start * 2f).Within(.01f), "the disc keeps its size");
            Assert.That(world.OnTrialGround(centre + Vector3.back * (start - .1f)), Is.True, "there is ground right out to its rim");
            Assert.That(world.TrialFragmentCount, Is.EqualTo(fragments.Length), "every fragment still floats round it");
            for (int i = 0; i < fragments.Length; i++)
                Assert.That(Vector3.Distance(world.TrialFragmentCentre(i), fragments[i]), Is.LessThan(.001f), $"fragment {i} where it was");
            Assert.That(world.TrialPortalCount, Is.EqualTo(portals.Length), "every portal is still on it");
            for (int i = 0; i < portals.Length; i++)
                Assert.That(Vector3.Distance(world.TrialPortalCentre(i), portals[i]), Is.LessThan(.001f), $"portal {i} where it was");

                                                                                                                                         
            Vector3 spot = centre + Vector3.back * (b.prophetPlatformRadii.z + 2f);
            Assume.That(Flat(spot - centre).magnitude, Is.LessThan(start - 1f), "still well on the platform");
            NashProphetTest.Place(player, spot);
            NashProphetTest.Force(hollow, Enemy.VoidPillarMove);
            await Until(() => hollow.VoidPillars.Count > 0, b.nashPillarWindup + 2f, "the pillar circles");
            hollow.HoldAttacks(60f);
            Assert.That(Flat(hollow.VoidPillars[0].centre - spot).magnitude, Is.LessThan(.1f),
                "the first pillar stands on the player, not cut back to an edge the platform no longer shrinks to");
        }

                                             

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RampageCharge_KnocksTheMinionsInItsWayAside_EachToItsOwnSideUnhurt_RunsItsWholeLine_LeavesOneBesideItsLaneAlone()
        {
            var (game, targets, tyrant) = await TyrantRedesignTest.TyrantSandbox(10f);
            var b = game.waves.bosses;
            var world = WorldMap.Active;
            Assume.That(await TyrantRedesignTest.OnChargeLane(game, tyrant, 10f, 1.5f), Is.True, "a lane wider than its body");
            Vector3 from = Flat(tyrant.Position), ahead = Flat(game.player.Position - tyrant.Position).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, ahead);
            float Along(Combatant body) => Vector3.Dot(Flat(body.Position) - from, ahead);
            float Across(Combatant body) => Vector3.Dot(Flat(body.Position) - from, side);

                                                                                                                                         
                                                                                                                                   
                                                                                                              
            var grunt = game.waves.DefinitionOf(EnemyKind.Grunt);
            Assume.That(grunt, Is.Not.Null, "the roster has the Grunt");
            foreach (var minion in targets)
            {
                minion.Configure(grunt, 1f, false);                                       
                minion.moveSpeed = 0f;
                minion.enabled = true;
            }

            var right = targets[0];
            var left = targets[1];
            var beside = targets[2];
            TyrantRedesignTest.Place(right, from + ahead * 4f + side * .6f);
            TyrantRedesignTest.Place(left, from + ahead * 5.5f - side * .6f);
            TyrantRedesignTest.Place(beside, from + ahead * 6.5f + side * (tyrant.Radius + beside.Radius + .4f));
            foreach (var minion in targets)
                Assume.That(world.IsWalkable(minion.Position, minion.Radius), Is.True, "each stands on open ground");

            var hp = targets.Select(t => t.Hp).ToArray();
            Vector3 besideAt = Flat(beside.Position);
            float rightAlong = Along(right), leftAlong = Along(left);
            var hits = new List<DamageInfo>();
            void Count(DamageInfo hit) { if (hit.dealer?.Owner == tyrant && tyrant.Charging) hits.Add(hit); }
            foreach (var minion in targets) minion.AttackReceived += Count;
            try
            {
                TyrantRedesignTest.Force(tyrant, Enemy.RampageChargeMove);
                await Until(() => tyrant.WindingUp && tyrant.LineTelegraph, 3f, "the charge wind-up");
                Vector3 landing = tyrant.RushLanding;
                float run = Flat(landing - from).magnitude - b.rushLeapDistance;
                Assume.That(run, Is.GreaterThan(rightAlong + 1f).And.GreaterThan(leftAlong + 1f).And.GreaterThan(Along(beside)),
                    "its run carries its centre past all three");

                float rightAbreast = float.NaN, leftAbreast = float.NaN, drift = 0f, inward = 0f;
                float rightWas = Across(right), leftWas = Across(left);
                bool besideMoved = false, leapt = false;
                float[] hpAtLeap = null, acrossAtLeap = null, alongAtLeap = null;
                Vector3 ranTo = await TyrantRedesignTest.RunAndLand(tyrant, () =>
                {
                                                                                            
                    inward = Mathf.Max(inward, Mathf.Max(rightWas - Across(right), Across(left) - leftWas));
                    rightWas = Across(right);
                    leftWas = Across(left);
                    if (tyrant.RushLeaping && !leapt)
                    {
                        leapt = true;
                        hpAtLeap = targets.Select(t => t.Hp).ToArray();
                        acrossAtLeap = new[] { Across(right), Across(left) };
                        alongAtLeap = new[] { Along(right), Along(left) };
                    }

                    if (!tyrant.Charging) return;
                    float along = Along(tyrant);
                    drift = Mathf.Max(drift, Mathf.Abs(Across(tyrant)));
                    if (float.IsNaN(rightAbreast) && along >= Along(right)) rightAbreast = Across(right);
                    if (float.IsNaN(leftAbreast) && along >= Along(left)) leftAbreast = Across(left);
                    if (Flat(beside.Position - besideAt).magnitude > .05f) besideMoved = true;
                });

                Assert.That(leapt, Is.True, "the run ended in its leap");
                                                                                     
                Assert.That(Vector3.Dot(ranTo - from, ahead), Is.GreaterThan(run - .3f), "nothing held it back: it ran its whole planned run");
                Assert.That(drift, Is.LessThan(.1f), "straight down its line");
                Assert.That(Flat(tyrant.Position - landing).magnitude, Is.LessThan(.5f), "then leapt and landed on its circle");

                                                                                                             
                Assert.That(rightAbreast, Is.GreaterThanOrEqualTo(tyrant.Radius), "the minion right of its line was already knocked well out to the right");
                Assert.That(leftAbreast, Is.LessThanOrEqualTo(-tyrant.Radius), "the one left of it out to the left");
                Assert.That(acrossAtLeap[0], Is.GreaterThanOrEqualTo(tyrant.Radius + right.Radius), "by the end of the run each is clear of its lane");
                Assert.That(acrossAtLeap[1], Is.LessThanOrEqualTo(-tyrant.Radius - left.Radius));
                Assert.That(Mathf.Abs(alongAtLeap[0] - rightAlong), Is.LessThan(.3f), "pushed straight aside, not carried along");
                Assert.That(Mathf.Abs(alongAtLeap[1] - leftAlong), Is.LessThan(.3f));
                Assert.That(inward, Is.LessThan(.02f), "each slides straight out and never judders back toward the lane");
                Assert.That(hits, Is.Empty, "the shove is no hit");
                Assert.That(hpAtLeap, Is.EqualTo(hp), "and costs them no HP");
                Assert.That(besideMoved, Is.False, "the minion beside its lane is not touched");
            }
            finally
            {
                foreach (var minion in targets) minion.AttackReceived -= Count;
            }
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task RampageCharge_StillRunsYouDownOnce_ForItsRunDamage_PushedAsideByTheRunsTier()
        {
            var (game, targets, tyrant) = await TyrantRedesignTest.TyrantSandbox(10f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            var player = game.player;
            Assume.That(await TyrantRedesignTest.OnChargeLane(game, tyrant, 10f, 1.5f), Is.True, "a lane wider than its body");
            Vector3 from = Flat(tyrant.Position), ahead = Flat(player.Position - tyrant.Position).normalized;
            var hits = new List<(DamageInfo hit, bool running)>();
            void Count(DamageInfo hit) { if (hit.dealer?.Owner == tyrant) hits.Add((hit, tyrant.Charging)); }
            player.AttackReceived += Count;
            try
            {
                TyrantRedesignTest.Force(tyrant, Enemy.RampageChargeMove);
                await Until(() => tyrant.WindingUp && tyrant.LineTelegraph, 3f, "the charge wind-up");
                                                                                          
                TyrantRedesignTest.Place(player, from + ahead * 4f);
                await TyrantRedesignTest.RunAndLand(tyrant);

                var runHits = hits.Where(h => h.running).Select(h => h.hit).ToList();
                Assert.That(runHits, Has.Count.EqualTo(1), "the run runs you down once");
                Assert.That(runHits[0].amount, Is.EqualTo(tyrant.attackDamage * b.rushRunDamage).Within(.01f), "for its run damage");
                Assert.That(runHits[0].knockDistance, Is.EqualTo(KnockbackRules.Active.Distance(b.rushRunKnock)), "pushed by the run's tier");
                Assert.That(Mathf.Abs(Vector3.Dot(Flat(runHits[0].knockDir).normalized, ahead)), Is.LessThan(.05f), "straight out to the side of its lane");
            }
            finally
            {
                player.AttackReceived -= Count;
            }
        }

                              

        static Vector3 Flat(Vector3 v) => Query.Flat(v);

        static async Task Until(System.Func<bool> completed, float timeout, string operation)
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
