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
