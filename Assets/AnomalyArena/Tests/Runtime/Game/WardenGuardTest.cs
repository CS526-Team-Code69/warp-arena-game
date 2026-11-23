using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The Warden stands guard where the player can see and reach it. It used to stand five cells behind its nearest ally as the
    /// player saw it; behind a Spire, which never moves, it stayed out of reach for as long as the Spire lived while the wave waited
    /// on it. It guards one monster drawn at random, a Spire twice as likely as another, and stands beside it on the player's side
    /// inside its own shield reach; it walks round when the player does, takes another monster when its ward dies and comes up to
    /// the player when none is left. Every check here reads only where the Warden stands and where it walks, so the same checks
    /// run on the rule before.
    /// </summary>
    [TestFixture]
    public class WardenGuardTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        // The Warden's shield cast reaches this far (Enemy.TickSpecial), and past this many degrees off the line from its ward to the
        // player it stands behind its ward as the player sees it.
        const float ShieldReach = 5f, BehindPast = 110f;
        const float ClearGround = 14f;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            WorldMap.NextMapFamily = null;
        }

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task Warden_GuardingASpire_StandsOnThePlayersSideInsideItsShieldReach_AndComesRoundWhenThePlayerDoes()
        {
            var (game, map) = await Arena();
            Vector3 centre = OpenGround(map);
            var spire = Hold(Spawn(game, EnemyKind.Spire, centre)); // a ward that stands; its laser is not what is checked
            Place(game.player, centre + Vector3.back * 13f);
            // Born where the rule before kept it: five cells behind the Spire as the player sees it.
            var warden = Spawn(game, EnemyKind.Warden, centre + Vector3.forward * 5f);
            Time.timeScale = 2f;

            string wrong = await StandsGuard(game, warden, spire, 10f);
            Assert.That(wrong, Is.Null, $"{map.biome.name}: a Warden born behind a Spire comes round to the player's side of it. {wrong}");
            await Until(() => ElementStatus.For(spire).Shield > 0f, 8f, "its shield reaches the Spire it stands by");

            // The player walks round to the other side: the Warden is behind the Spire again and does not stay there.
            Place(game.player, centre + Vector3.forward * 13f);
            float began = Time.time;
            wrong = await StandsGuard(game, warden, spire, 10f);
            Assert.That(wrong, Is.Null, $"{map.biome.name}: with the player on the Spire's other side the Warden walks round to that side. {wrong}");
            Debug.Log($"[WardenGuard] {map.biome.name}: the Warden came round the Spire in {Time.time - began - 1f:0.0} s; it stands {P(warden.Position)}, the Spire {P(spire.Position)}, the player {P(game.player.Position)}");
        }

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task Warden_BetweenASpireAndANearerMonster_GoesToTheSpireMoreOftenThanToTheOther()
        {
            var (game, map) = await Arena();
            Vector3 centre = OpenGround(map);
            // The Spire stands farther from the Warden than the other monster: the rule before always took the nearer one.
            var spire = Hold(Spawn(game, EnemyKind.Spire, centre + Vector3.forward * 10f));
            var other = Hold(Spawn(game, EnemyKind.Brute, centre + Vector3.back * 7f));
            Place(game.player, centre + Vector3.right * 13f);
            var warden = Spawn(game, EnemyKind.Warden, centre);
            var body = warden.GetComponent<Rigidbody>();
            var data = game.waves.DefinitionOf(EnemyKind.Warden);

            const int Births = 240;
            int toSpire = 0, toOther = 0, undecided = 0;
            for (int i = 0; i < Births; i++)
            {
                // A new birth on the same spot: the Warden makes its choice afresh and its first steps show it.
                warden.Configure(data, 1f, false);
                V3CombatSmokeTest.Teleport(warden, centre);
                body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                await Awaitable.FixedUpdateAsync();
                await Awaitable.FixedUpdateAsync();
                float north = body.linearVelocity.z;
                if (north > .3f) toSpire++;
                else if (north < -.3f) toOther++;
                else undecided++;
            }

            string counts = $"{map.biome.name}: of {Births} births the Warden set off toward the Spire {toSpire} times, toward the {other.definition.kind} {toOther} times, neither way {undecided} times";
            Debug.Log("[WardenGuard] " + counts);
            Assert.That(undecided, Is.LessThan(Births / 20), counts);
            Assert.That(toSpire, Is.GreaterThan(toOther), "the Spire is taken more often than the other monster. " + counts);
            // A Spire counts twice in the draw: two births in three (160 of 240; the bounds are four standard deviations wide).
            Assert.That(toSpire, Is.InRange(Births * 2 / 3 - 30, Births * 2 / 3 + 30), "about two births in three go to the Spire. " + counts);
        }

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task Warden_WhoseWardDies_TakesAnotherMonster_AndComesUpToThePlayerWhenNoneIsLeft()
        {
            var (game, map) = await Arena();
            Vector3 centre = OpenGround(map);
            Place(game.player, centre + Vector3.back * 12f);
            var first = Hold(Spawn(game, EnemyKind.Brute, centre + Vector3.forward * 2f));
            var warden = Spawn(game, EnemyKind.Warden, centre + Vector3.forward * 8f);
            Time.timeScale = 2f;

            string wrong = await StandsGuard(game, warden, first, 10f);
            Assert.That(wrong, Is.Null, $"{map.biome.name}: with one monster to guard the Warden stands by it on the player's side. {wrong}");

            // Another monster arrives, a Spire even: the Warden keeps the ward it has.
            var spire = Hold(Spawn(game, EnemyKind.Spire, centre + Vector3.right * 9f));
            await Seconds(3f);
            Assert.That(Flat(warden.Position - first.Position).magnitude, Is.LessThan(ShieldReach),
                $"{map.biome.name}: the Warden keeps its ward while it lives; it stands {P(warden.Position)}, its ward {P(first.Position)}, the Spire {P(spire.Position)}");

            first.DiscardWithoutReward();
            wrong = await StandsGuard(game, warden, spire, 14f);
            Assert.That(wrong, Is.Null, $"{map.biome.name}: its ward gone, the Warden takes the monster that is left and stands by it. {wrong}");

            spire.DiscardWithoutReward();
            float deadline = Time.time + 16f, held = -1f, away = 0f;
            while (Time.time < deadline)
            {
                await Awaitable.FixedUpdateAsync();
                away = Flat(warden.Position - game.player.Position).magnitude;
                bool waits = away >= 3f && away <= 7.5f && Flat(warden.GetComponent<Rigidbody>().linearVelocity).magnitude < .1f;
                if (!waits) held = -1f;
                else if (held < 0f) held = Time.time;
                else if (Time.time - held >= 1f) break;
            }
            Assert.That(held >= 0f && Time.time - held >= 1f, Is.True,
                $"{map.biome.name}: with nobody left to guard the Warden comes up to the player and waits 3 to 7.5 cells away; it is {away:0.0} cells away at {P(warden.Position)}, the player {P(game.player.Position)}");
        }

        // ───── What "stands guard" means here ─────

        /// <summary>Waits until the Warden has stood for a second by its ward on the player's side: no more than BehindPast degrees off
        /// the line from the ward to the player, inside its shield reach of the ward, and no farther from the player than a cell past
        /// the ward. Null once it has; otherwise where it stood when the time ran out.</summary>
        static async Task<string> StandsGuard(GameManager game, Enemy warden, Enemy ward, float seconds)
        {
            var body = warden.GetComponent<Rigidbody>();
            float deadline = Time.time + seconds, held = -1f;
            string last = "";
            while (Time.time < deadline)
            {
                await Awaitable.FixedUpdateAsync();
                Vector3 at = warden.Position, by = ward.Position, player = game.player.Position;
                float bearing = Vector3.Angle(Flat(at - by), Flat(player - by)), gap = Flat(at - by).magnitude;
                float mine = Flat(at - player).magnitude, wards = Flat(by - player).magnitude, speed = Flat(body.linearVelocity).magnitude;
                last = $"After {seconds:0} s it stands {P(at)}, {bearing:0} degrees off the line from its ward {P(by)} to the player {P(player)} (behind it past {BehindPast:0}), "
                       + $"{gap:0.0} cells from its ward (its shield reaches {ShieldReach:0}), {mine:0.0} cells from the player (its ward {wards:0.0}), moving {speed:0.0} cells a second.";
                bool good = bearing <= BehindPast && gap < ShieldReach && mine <= wards + 1f && speed < .1f;
                if (!good) held = -1f;
                else if (held < 0f) held = Time.time;
                else if (Time.time - held >= 1f) return null;
            }
            return last;
        }

        // ───── The ground and the bodies ─────

        /// <summary>The open mode on a Dunes map (the widest open ground), its wave stopped and the field empty.</summary>
        static async Task<(GameManager game, WorldMap map)> Arena()
        {
            WorldMap.NextMapFamily = Biome.Dunes;
            await SceneManagerHelper.LoadSceneAsync(Scene);
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            game.player.Invulnerable = true;
            return (game, WorldMap.Active);
        }

        /// <summary>The middle of a square of ground ClearGround cells to every side with no wall, hole, pull or quicksand in it.</summary>
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

        /// <summary>The monster stands where it is and does nothing; it is alive, so a Warden may guard it.</summary>
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

        static async Task Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.NextFrameAsync();
        }

        static async Task Until(Func<bool> completed, float seconds, string operation)
        {
            float deadline = Time.time + seconds;
            while (!completed())
            {
                if (Time.time > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
