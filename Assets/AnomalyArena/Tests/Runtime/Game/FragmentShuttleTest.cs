using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// A Brute chasing the player inside the ring of the Tyrant's meteor fragments. From some sides its way points straight across a
    /// fragment's margin with a hair of sideways in it: it slid a step to one side, its way then leaned to the other, it slid back, and so
    /// it shuttled in place for as long as the fragments stood. Each slide was a step, so the rule that sets a refused walk down on open
    /// ground (Enemy.Unstick.cs) never counted it. A walk that gets nowhere is set down as a refused one is (Enemy.NoProgress.cs,
    /// WalkProgressWatch). The first test walks the shuttle with the game's own walking rules and checks the watch catches it; the
    /// second lets a real Brute walk at the ring from many sides (with the physics a Brute mostly ends up refused outright instead,
    /// which the old rule already caught) and checks it reaches the player without standing in one place for long.
    /// </summary>
    [TestFixture]
    public class FragmentShuttleTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        // The sides the walking rules alone (WorldMap.WalkStep on the chase field) shuttle from, with the ring turned 0 degrees and the
        // player standing this far off its middle: 22.5 degrees apart, as the fragments' probe numbered them.
        static readonly int[] Sides = { 3, 6, 7, 8, 9 };
        static readonly Vector3 PlayerOffset = new(-.4f, 0f, 1.1f);
        // From those sides the way ends just outside the north fragment's margin, aimed at the cell whose centre lies 1.5 west of the
        // ring's middle, straight across the margin. A slide along it is 0.047 a physics step (half a Brute's walk); a body that stops
        // within 0.012 of the line has too little sideways for a slide and is refused, one that does not straddles the line for good.
        // These starts lie on the margin's edge a whole number of slides plus this much east of the line.
        static readonly float[] EdgeOffsets = { .018f, .024f, .030f };
        WaveSet originalWaves;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            var game = GameManager.Instance;
            if (game && originalWaves) game.waves.waveSet = originalWaves;
            originalWaves = null;
        }

        [Test, LoadScene(Scene), Timeout(60000)]
        public async Task WalkAtTheFragmentRing_Shuttles_AndTheProgressWatchSetsItDown()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && WorldMap.Active && WorldMap.Active.Grid != null, 12f, "the map");
            var map = WorldMap.Active;
            Assume.That(FindOpenGround(map, GameManager.Instance.player.Position, out Vector3 centre), Is.True, "the map has open ground 10 cells round");
            var points = Enumerable.Range(0, 4).Select(i => centre + Quaternion.Euler(0f, 90f * i, 0f) * Vector3.forward * 2.625f).ToList();
            var terrain = TyrantTerrain.For(map);
            try
            {
                Assume.That(terrain.AddFragments(points, 1.6f, 600f), Is.EqualTo(4), "the four fragments stand");
                Vector3 target = centre + PlayerOffset;
                map.Grid.Integrate(target);
                const float dt = 1f / 30f, radius = 1f, speed = 4.7f;
                int shuttles = 0;
                string unarrived = "";
                foreach (int side in Sides)
                {
                    Vector3 start = centre + Quaternion.Euler(0f, side * 22.5f + 3f, 0f) * Vector3.forward * 8f;
                    // Without the watch: the longest the old rule's count (a walk refused outright) ran, and whether the body arrived.
                    var (arrived, longestRefused, _) = Walk(map, start, target, radius, speed, dt, null);
                    if (!arrived && longestRefused < Enemy.WalkRefusedSeconds) shuttles++;
                    // With the watch, setting the body down on open ground as the game does.
                    var (arrivedNow, _, setDowns) = Walk(map, start, target, radius, speed, dt, new WalkProgressWatch());
                    if (!arrivedNow) unarrived += $"\n  from side {side} ({start}), set down {setDowns} times";
                }
                Assume.That(shuttles, Is.GreaterThan(0), "from some side the walk gets nowhere and is never refused outright for long");
                Assert.That(unarrived, Is.Empty, "with the watch every walk reaches the player:" + unarrived);
            }
            finally
            {
                terrain.ResetTerrain();
            }
        }

        static (bool arrived, float longestRefused, int setDowns) Walk(WorldMap map, Vector3 at, Vector3 target, float radius, float speed, float dt, WalkProgressWatch watch)
        {
            float refused = 0f, longestRefused = 0f;
            int setDowns = 0;
            for (float t = 0f; t < 12f; t += dt)
            {
                if (Query.Flat(target - at).magnitude - radius - .5f <= 1f) return (true, longestRefused, setDowns);
                Vector3 wanted = map.GetChaseDirection(at, target, false, radius) * speed;
                Vector3 taken = map.WalkStep(at, wanted, dt, radius, false);
                refused = Query.Flat(taken).sqrMagnitude < 1e-6f ? refused + dt : 0f;
                longestRefused = Mathf.Max(longestRefused, refused);
                bool setDown = watch != null && watch.Note(at, wanted, taken, dt, t, Enemy.WalkRefusedSeconds);
                at += taken * dt;
                if (setDown && map.OpenGroundOnPath(at, radius, false, false, out var ground))
                {
                    at = Query.Flat(ground);
                    setDowns++;
                }
            }
            return (false, longestRefused, setDowns);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(240000)]
        public async Task BruteAtTheFragmentRing_NeverStandsShuttlingInPlace()
        {
            var game = await StartOpenRun();
            var map = WorldMap.Active;
            Assume.That(FindOpenGround(map, game.player.Position, out Vector3 centre), Is.True, "the map has open ground 10 cells round");
            var player = game.player;
            player.Invulnerable = true;
            Place(player, centre + PlayerOffset);

            var points = Enumerable.Range(0, 4).Select(i => centre + Quaternion.Euler(0f, 90f * i, 0f) * Vector3.forward * 2.625f).ToList();
            int standing = TyrantTerrain.For(map).AddFragments(points, 1.6f, 600f);
            Assume.That(standing, Is.EqualTo(4), "the four fragments stand round the player");

            string failures = "";
            var starts = EdgeOffsets.Select(off => ($"the margin's edge {off:0.000} east of the line", centre + new Vector3(-1.5f + off + 4f * .047f, 0f, 4.46f)))
                .Concat(Sides.Select(side => ($"side {side}", centre + Quaternion.Euler(0f, side * 22.5f + 3f, 0f) * Vector3.forward * 8f)))
                .ToList();
            int walked = 0;
            Enemy brute = null;
            foreach (var (name, start) in starts)
            {
                // One Brute walks from every start in turn (the lab keeps only a few of each kind): it finishes a blow it began first.
                if (!brute || !brute.IsAlive) brute = game.waves.SpawnLab(EnemyKind.Brute, start, false, ElementKind.None);
                else
                {
                    for (float wait = 0f; brute.WindingUp && wait < 5f; wait += Time.deltaTime) await Hold(game, player, centre, brute);
                    Place(brute, start);
                }
                if (!brute) { Debug.Log($"[FragmentShuttle] {name}: no Brute could be set down at {start}"); continue; }
                walked++;
                int blows = brute.AttackSequence, setDowns = brute.SetDownCount;
                await Hold(game, player, centre, brute);
                Vector3 anchor = brute.Position, stoodAt = anchor;
                float clock = 0f, anchorAt = 0f, longestStill = 0f;
                bool reached = false;
                while (clock < 14f && brute.IsAlive)
                {
                    if (brute.WindingUp || brute.AttackSequence > blows) { reached = true; break; }
                    if (Query.Flat(brute.Position - anchor).magnitude > .5f) { anchor = brute.Position; anchorAt = clock; }
                    else if (clock - anchorAt > longestStill) { longestStill = clock - anchorAt; stoodAt = brute.Position; }
                    clock += game.WorldDeltaTime;
                    await Hold(game, player, centre, brute);
                }
                Debug.Log($"[FragmentShuttle] {name}: from {start}, reached the player {reached} after {clock:0.0} s, stood still at most {longestStill:0.0} s at {stoodAt}, set down {brute.SetDownCount - setDowns} times");
                if (longestStill >= 4f)
                    failures += $"\n  from {name} ({start}) the Brute stood {longestStill:0.0} s at {stoodAt} (set down {brute.SetDownCount - setDowns} times, reached the player {reached})";
            }
            if (brute) brute.DiscardWithoutReward();
            Time.timeScale = 1f;
            Assume.That(walked, Is.GreaterThanOrEqualTo(6), "most starts had room for a Brute");
            Assert.That(failures, Is.Empty, "a Brute at the fragment ring never stands in one place for 4 s or more:" + failures);
        }

        /// <summary>One frame with the player held where it stands, unhurt, and every other monster gone.</summary>
        static async Task Hold(GameManager game, PlayerController player, Vector3 centre, Enemy brute)
        {
            Place(player, centre + PlayerOffset);
            player.Heal(player.maxHp);
            foreach (var other in game.waves.Alive.ToArray())
                if (other && other != brute && other.IsAlive) other.DiscardWithoutReward();
            await Awaitable.NextFrameAsync();
        }

        static void Place(Combatant body, Vector3 at)
        {
            at.y = body.transform.position.y;
            if (body.TryGetComponent(out Rigidbody rb))
            {
                rb.position = at;
                if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            }
            body.transform.position = at;
        }

        /// <summary>A point on a 2-cell lattice with ground a wide body can stand on 10 cells round it, away from the player.</summary>
        static bool FindOpenGround(WorldMap map, Vector3 player, out Vector3 centre)
        {
            for (float x = -40f; x <= 40f; x += 2f)
            for (float z = -30f; z <= 30f; z += 2f)
            {
                var c = new Vector3(x, 0f, z);
                if (Query.Flat(player - c).magnitude < 14f) continue;
                bool clear = true;
                for (float dx = -10f; dx <= 10f && clear; dx += .5f)
                for (float dz = -10f; dz <= 10f && clear; dz += .5f)
                    if (dx * dx + dz * dz <= 100f) clear = map.IsWalkable(c + new Vector3(dx, 0f, dz), 1.2f);
                if (!clear) continue;
                centre = c;
                return true;
            }
            centre = Vector3.zero;
            return false;
        }

        /// <summary>The open mode's first wave, made long so it does not end meanwhile, with every black hole fed once (no first-sight
        /// scene).</summary>
        async Task<GameManager> StartOpenRun()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map");
            var game = GameManager.Instance;
            originalWaves = game.waves.waveSet;
            var waves = Object.Instantiate(originalWaves);
            foreach (var wave in waves.waves) wave.duration = 600f;
            game.waves.waveSet = waves;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            await UntilWaveActive(game);
            foreach (var hole in WorldMap.Active.holes)
                if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0) hole.Feed(1, game.waves.WaveIndex + 1);
            await Awaitable.NextFrameAsync();
            await UntilWaveActive(game);
            return game;
        }

        static async Task UntilWaveActive(GameManager game)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) WorldMap.Active.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
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
