using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The rules of the Warden's guard duty one by one (Enemy.Guard.cs): the draw of its ward, by given rolls; Wardens spreading over
    /// the monsters there are and fanning out where they share one; whom a Warden never guards; and the void trial, where the
    /// Nash Prophet's Wardens stand in front of it on the platform. WardenGuardTest holds what the player sees of it.
    /// </summary>
    [TestFixture]
    public class WardenGuardRulesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";
        const float ClearGround = 14f;

        [TearDown]
        public void TearDown()
        {
            Enemy.GuardRoll = null;
            Time.timeScale = 1f;
            WorldMap.NextMapFamily = null;
        }

        // ───── The draw (no scene) ─────

        [Test]
        public void Draw_ASpireCountsTwice_EveryOtherMonsterOnce()
        {
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                Assert.That(Enemy.GuardWeight(kind), Is.EqualTo(kind == EnemyKind.Spire ? Enemy.GuardSpireWeight : 1f), $"{kind}'s weight in the draw");
            Assert.That(Enemy.GuardSpireWeight, Is.EqualTo(2f), "a Spire counts as two monsters");

            var weights = new[] { Enemy.GuardWeight(EnemyKind.Grunt), Enemy.GuardWeight(EnemyKind.Spire), Enemy.GuardWeight(EnemyKind.Stomper) };
            var drawn = new int[weights.Length];
            const int Rolls = 4000;
            for (int i = 0; i < Rolls; i++) drawn[Enemy.DrawGuard(weights, (i + .5f) / Rolls)]++;
            Assert.That(drawn, Is.EqualTo(new[] { Rolls / 4, Rolls / 2, Rolls / 4 }), "of even rolls a Spire beside two other monsters takes half, each of the others a quarter");
        }

        [Test]
        public void Draw_WithNobodyToDraw_GivesNone_AndTheLastRollStaysInside()
        {
            Assert.That(Enemy.DrawGuard(new float[0], .5f), Is.EqualTo(-1));
            Assert.That(Enemy.DrawGuard(new[] { 0f, 0f }, .5f), Is.EqualTo(-1));
            Assert.That(Enemy.DrawGuard(new[] { 1f, 2f, 0f }, 1f), Is.EqualTo(1), "a roll of 1 draws the last monster with a weight");
            Assert.That(Enemy.DrawGuard(new[] { 1f, 2f }, 0f), Is.EqualTo(0));
        }

        // ───── The draw in the game ─────

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task Warden_BesideASpireAndOneOtherMonster_DrawsTheSpireOnTwoRollsInThree()
        {
            var (game, map) = await Arena();
            Vector3 centre = OpenGround(map);
            var spire = Hold(Spawn(game, EnemyKind.Spire, centre + Vector3.forward * 9f));
            var other = Hold(Spawn(game, EnemyKind.Brute, centre + Vector3.back * 7f));
            Place(game.player, centre + Vector3.right * 13f);
            var warden = Spawn(game, EnemyKind.Warden, centre);
            var data = game.waves.DefinitionOf(EnemyKind.Warden);

            const int Rolls = 300;
            int toSpire = 0, toOther = 0;
            for (int i = 0; i < Rolls; i++)
            {
                float roll = (i + .5f) / Rolls;
                int asked = 0;
                Enemy.GuardRoll = () => asked++ == 0 ? roll : .75f; // the ward first, then the shoulder
                warden.Configure(data, 1f, false);
                V3CombatSmokeTest.Teleport(warden, centre);
                Physics.SyncTransforms();
                await Awaitable.FixedUpdateAsync();
                await Awaitable.FixedUpdateAsync();
                if (warden.Ward == spire) toSpire++;
                else if (warden.Ward == other) toOther++;
                else Assert.Fail($"roll {roll:0.000}: the Warden guards {(warden.Ward ? warden.Ward.definition.kind.ToString() : "nobody")}");
            }
            Assert.That((toSpire, toOther), Is.EqualTo((Rolls * 2 / 3, Rolls / 3)), "of even rolls two in three draw the Spire");
        }

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task TwoWardens_DoNotShareAWard_WhileAnotherMonsterIsFree()
        {
            var (game, map) = await Arena();
            Vector3 centre = OpenGround(map);
            var spire = Hold(Spawn(game, EnemyKind.Spire, centre + Vector3.forward * 8f));
            var other = Hold(Spawn(game, EnemyKind.Brute, centre + Vector3.back * 7f));
            Place(game.player, centre + Vector3.right * 13f);
            var first = Spawn(game, EnemyKind.Warden, centre + Vector3.left * 2f);
            var second = Spawn(game, EnemyKind.Warden, centre + Vector3.left * 5f);
            var data = game.waves.DefinitionOf(EnemyKind.Warden);
            for (int round = 0; round < 24; round++)
            {
                first.Configure(data, 1f, false);
                second.Configure(data, 1f, false);
                V3CombatSmokeTest.Teleport(first, centre + Vector3.left * 2f);
                V3CombatSmokeTest.Teleport(second, centre + Vector3.left * 5f);
                Physics.SyncTransforms();
                await Awaitable.FixedUpdateAsync();
                await Awaitable.FixedUpdateAsync();
                Assert.That(first.Ward && second.Ward, Is.True, $"round {round}: both Wardens have a ward");
                Assert.That(first.Ward, Is.Not.SameAs(second.Ward), $"round {round}: with two monsters and two Wardens each guards its own");
            }
            Assert.That(new[] { spire, other }, Has.Member(first.Ward));
        }

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task ThreeWardens_ByOneSpire_FanOutOnThePlayersSide()
        {
            var (game, map) = await Arena();
            Vector3 centre = OpenGround(map);
            var spire = Hold(Spawn(game, EnemyKind.Spire, centre));
            Place(game.player, centre + Vector3.back * 13f);
            var wardens = new[]
            {
                Spawn(game, EnemyKind.Warden, centre + new Vector3(-3f, 0f, 5f)),
                Spawn(game, EnemyKind.Warden, centre + new Vector3(0f, 0f, 6f)),
                Spawn(game, EnemyKind.Warden, centre + new Vector3(3f, 0f, 5f)),
            };
            Time.timeScale = 2f;
            float deadline = Time.time + 16f, held = -1f;
            while (Time.time < deadline)
            {
                await Awaitable.FixedUpdateAsync();
                if (!wardens.All(w => w.AtGuardPost)) held = -1f;
                else if (held < 0f) held = Time.time;
                else if (Time.time - held >= 1f) break;
            }
            string where = string.Join("; ", wardens.Select(w =>
                $"{P(w.Position)} {Vector3.Angle(Flat(w.Position - spire.Position), Flat(game.player.Position - spire.Position)):0} deg, {Flat(w.Position - spire.Position).magnitude:0.0} from the Spire, at post {w.AtGuardPost}"));
            Assert.That(held >= 0f && Time.time - held >= 1f, Is.True, "the three Wardens come to rest: " + where);
            foreach (var warden in wardens)
            {
                Assert.That(warden.Ward, Is.SameAs(spire), "the Spire is the only monster to guard");
                Assert.That(Vector3.Angle(Flat(warden.Position - spire.Position), Flat(game.player.Position - spire.Position)), Is.LessThanOrEqualTo(Enemy.GuardRearLimit),
                    "each stands on the player's side of the Spire: " + where);
                Assert.That(Flat(warden.Position - spire.Position).magnitude, Is.LessThanOrEqualTo(Enemy.GuardLeash), "inside its shield reach of the Spire: " + where);
            }
            for (int a = 0; a < wardens.Length; a++)
            for (int b = a + 1; b < wardens.Length; b++)
                Assert.That(Flat(wardens[a].Position - wardens[b].Position).magnitude, Is.GreaterThan(1.5f), "each has a post of its own: " + where);
        }

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task Warden_NeverGuardsAWardenOrAShade_AndWaitsByThePlayerInstead()
        {
            var (game, map) = await Arena();
            Vector3 centre = OpenGround(map);
            Place(game.player, centre + Vector3.back * 12f);
            var shade = Hold(Spawn(game, EnemyKind.Shade, centre + Vector3.right * 4f));
            var fellow = Hold(Spawn(game, EnemyKind.Warden, centre + Vector3.left * 4f));
            var warden = Spawn(game, EnemyKind.Warden, centre + Vector3.forward * 6f);
            Time.timeScale = 2f;
            float deadline = Time.time + 16f, held = -1f, away = 0f;
            while (Time.time < deadline)
            {
                await Awaitable.FixedUpdateAsync();
                Assert.That(warden.Ward, Is.Null, $"the Warden guards neither the {shade.definition.kind} nor the other {fellow.definition.kind}");
                away = Flat(warden.Position - game.player.Position).magnitude;
                bool waits = warden.AtGuardPost && away >= Enemy.GuardFreeNear - Enemy.GuardStandoffSlack && away <= Enemy.GuardFreeFar;
                if (!waits) held = -1f;
                else if (held < 0f) held = Time.time;
                else if (Time.time - held >= 1f) break;
            }
            Assert.That(held >= 0f && Time.time - held >= 1f, Is.True, $"with nobody to guard it waits by the player; it is {away:0.0} cells away at {P(warden.Position)}");
        }

        // ───── The void trial ─────

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(180000)]
        public async Task VoidTrial_TheProphetsWardens_StandInFrontOfItOnThePlatform()
        {
            var (game, world, hollow) = await NashProphetTest.StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            player.Invulnerable = true;
            Vector3 middle = Flat(world.trialPlatform.position);
            NashProphetTest.Place(player, middle + Vector3.back * (b.nashNearRange - 1.5f));
            NashProphetTest.Force(hollow, Enemy.VoidSummonMove);
            await NashProphetTest.Until(() => Summons(game, hollow).Length == b.nashSummonCount, b.nashSummonWindup + b.nashSummonWarning + 6f, "the Wardens appear");
            hollow.HoldAttacks(120f);
            var wardens = Summons(game, hollow);
            Assume.That(wardens.All(w => w.definition.kind == EnemyKind.Warden), Is.True, "phase 1 summons Wardens");

            // The player far from the Prophet: the Wardens fan out in front of it.
            NashProphetTest.Place(player, middle + Vector3.back * 10f);
            string wrong = await AllStand(game, world, hollow, wardens, 14f);
            Assert.That(wrong, Is.Null, "with the player far out the Prophet's Wardens stand in front of it. " + wrong);
            // The player close to the Prophet: still on the platform, off its body, on the player's side.
            NashProphetTest.Place(player, middle + Vector3.right * 5f);
            wrong = await AllStand(game, world, hollow, wardens, 14f);
            Assert.That(wrong, Is.Null, "with the player close to the Prophet its Wardens still stand on the player's side. " + wrong);
        }

        static async Task<string> AllStand(GameManager game, WorldMap world, Enemy hollow, Enemy[] wardens, float seconds)
        {
            float deadline = Time.time + seconds, held = -1f;
            string last = "";
            while (Time.time < deadline)
            {
                await Awaitable.FixedUpdateAsync();
                bool good = true;
                var parts = new List<string>();
                foreach (var warden in wardens)
                {
                    if (!warden.IsAlive) return "a Warden fell: " + warden.DeathCause;
                    Vector3 at = warden.Position, by = hollow.Position;
                    float bearing = Vector3.Angle(Flat(at - by), Flat(game.player.Position - by)), gap = Flat(at - by).magnitude;
                    bool ok = warden.Ward == hollow && world.OnTrialGround(at, 0f) && bearing <= Enemy.GuardRearLimit && gap >= hollow.Radius + warden.Radius - .05f && warden.AtGuardPost;
                    good &= ok;
                    parts.Add($"{P(at)} {bearing:0} deg off the line to the player, {gap:0.0} from the Prophet, on the platform {world.OnTrialGround(at, 0f)}, at its post {warden.AtGuardPost}");
                }
                last = $"After {seconds:0} s (the Prophet {P(hollow.Position)}, the player {P(game.player.Position)}): " + string.Join("; ", parts);
                if (!good) held = -1f;
                else if (held < 0f) held = Time.time;
                else if (Time.time - held >= 1f) return null;
            }
            return last;
        }

        static Enemy[] Summons(GameManager game, Enemy hollow) =>
            game.waves.Alive.Where(e => e && e != hollow && e.IsAlive && e.IsTrialSummon).ToArray();

        // ───── The ground and the bodies (as WardenGuardTest) ─────

        static async Task<(GameManager game, WorldMap map)> Arena()
        {
            WorldMap.NextMapFamily = Biome.Dunes;
            await SceneManagerHelper.LoadSceneAsync(Scene);
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            game.player.Invulnerable = true;
            return (game, WorldMap.Active);
        }

        static Vector3 OpenGround(WorldMap map)
        {
            Bounds b = map.Bounds;
            for (float z = 0f; z <= b.extents.z - ClearGround; z += 2f)
            for (float x = 0f; x <= b.extents.x - ClearGround; x += 2f)
            for (int sign = 0; sign < 4; sign++)
            {
                var centre = new Vector3((sign & 1) == 0 ? x : -x, 0f, (sign & 2) == 0 ? z : -z);
                if (!map.IsWalkable(centre, ClearGround) || map.ClosestHole(centre, ClearGround + 14f).HasValue) continue;
                bool clear = true;
                for (float dz = -ClearGround; dz <= ClearGround && clear; dz += 2f)
                for (float dx = -ClearGround; dx <= ClearGround && clear; dx += 2f)
                {
                    Vector3 p = centre + new Vector3(dx, 0f, dz);
                    clear = map.IsWalkable(p, .5f) && !Quicksand.IsQuicksand(p);
                }
                if (clear) return centre;
            }
            Assert.Fail($"{map.biome.name}: no square of open ground {ClearGround * 2f} cells across for the check.");
            return Vector3.zero;
        }

        static Enemy Spawn(GameManager game, EnemyKind kind, Vector3 at)
        {
            var enemy = game.waves.SpawnLab(kind, at, false, ElementKind.None);
            Assert.That(enemy, Is.Not.Null, $"the pool gives a {kind}");
            Physics.SyncTransforms();
            return enemy;
        }

        static Enemy Hold(Enemy enemy)
        {
            enemy.enabled = false;
            var body = enemy.GetComponent<Rigidbody>();
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            body.constraints = RigidbodyConstraints.FreezeAll;
            return enemy;
        }

        static void Place(Combatant body, Vector3 at)
        {
            V3CombatSmokeTest.Teleport(body, at);
            Physics.SyncTransforms();
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
        static string P(Vector3 p) => $"({p.x:0.0}, {p.z:0.0})";
    }
}
