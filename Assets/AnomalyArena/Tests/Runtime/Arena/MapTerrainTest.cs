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
    public class MapTerrainTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const string Dunes = "DustFlats", Ruins = "WreckField", Rift = "RiftZone";
        UnityEngine.Random.State savedRandom;

        [SetUp]
        public void SetUp()
        {
            savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(7219);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Random.state = savedRandom;

        [Test]
        public void PickForRun_MapsOutOfRotation_AreNeverPickedUnlessNoMapIsIn()
        {
            var biomes = new[] { MakeBiome(true), MakeBiome(false), MakeBiome(true) };
            try
            {
                var picks = Enumerable.Range(0, 200).Select(_ => Biome.PickForRun(biomes)).ToArray();
                Assert.That(picks.Distinct().OrderBy(i => i), Is.EqualTo(new[] { 0, 2 }), "both maps in rotation start runs, the other never does");

                foreach (var biome in biomes) biome.inRotation = false;
                Assert.That(Biome.PickForRun(biomes), Is.InRange(0, biomes.Length - 1), "with no map in rotation any map may start");
            }
            finally
            {
                foreach (var biome in biomes) Object.DestroyImmediate(biome);
            }
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task SceneMapList_RiftZonesAreOutOfRotation_RunsStartOnTheDunesOrTheRuins()
        {
            var world = await LoadedMap();

            Assert.That(world.biomes.Where(b => Family(b) == Rift), Is.Not.Empty.And.All.Matches<Biome>(b => !b.inRotation),
                "the Rift Zone maps stay in the scene but leave the rotation");
            Assert.That(world.biomes.Where(b => Family(b) != Rift), Has.All.Matches<Biome>(b => b.inRotation), "every Dunes and Ruins map is in rotation");
            Assert.That(world.biome.inRotation, Is.True, "this run started on a map in rotation");
            var families = Enumerable.Range(0, 300).Select(_ => Family(world.biomes[Biome.PickForRun(world.biomes)])).Distinct();
            Assert.That(families, Is.EquivalentTo(new[] { Dunes, Ruins }), "runs start on both the Dunes and the Ruins");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task MapMixTilts_DunesRaiseBloatersAndSpiresRuinsRaiseRammers_InTheWeightsTheWavesRoll()
        {
            var world = await LoadedMap();
            var waves = GameManager.Instance.waves;
            const int wave = 14;                                                            
            var untilted = ScriptableObject.CreateInstance<Biome>();                      
            var runMap = world.biome;
            var failures = new List<string>();
            try
            {
                world.biome = untilted;
                var plain = new[] { Shares(waves, wave, 1), Shares(waves, wave, 2) };
                foreach (var biome in world.biomes)
                {
                    var raised = Family(biome) == Dunes ? new[] { EnemyKind.Bloater, EnemyKind.Spire }
                        : Family(biome) == Ruins ? new[] { EnemyKind.Rammer } : Array.Empty<EnemyKind>();
                    world.biome = biome;                                                        
                    for (int group = 1; group <= 2; group++)
                    {
                        var shares = Shares(waves, wave, group);
                        foreach (var kind in raised)
                            if (!(shares[kind] > plain[group - 1][kind] + 1e-4f))
                                failures.Add($"{biome.name} {(group == 1 ? "special" : "elite")} {kind}: share {shares[kind]:0.000}, untilted {plain[group - 1][kind]:0.000}, tilt {Tilt(biome, kind)}");
                    }
                }
            }
            finally
            {
                world.biome = runMap;
                Object.DestroyImmediate(untilted);
            }

            Assert.That(failures, Is.Empty, "doc 12.1-12.2: the Dunes bring more Bloaters and Spires, the Ruins more Rammers");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task RuinsLayouts_MoreWallsWithLAndBracketShapes_AllClearOfHoleGravityAndTheStart()
        {
            var world = await LoadedMap();
            float gravity = world.holes.Max(h => h.GravityRadius);
            int dunesMost = Layouts(world, Dunes).Max(l => l.walls.Length);
            var failures = new List<string>();

            foreach (var layout in Layouts(world, Ruins))
            {
                var shapes = Joined(layout.walls);
                if (shapes.Count(IsL) < 2) failures.Add($"{layout.name}: {shapes.Count(IsL)} L walls");
                if (shapes.Count(IsBracket) < 2) failures.Add($"{layout.name}: {shapes.Count(IsBracket)} ] walls");
                foreach (var shape in shapes.Where(s => s.Count > 1 && !IsL(s) && !IsBracket(s)))
                    failures.Add($"{layout.name}: joined walls {string.Join(" ", shape)} are neither an L nor a ]");
                if (layout.walls.Length <= dunesMost) failures.Add($"{layout.name}: {layout.walls.Length} walls, a Dunes layout has {dunesMost}");
                var start = new Vector2(layout.playerStart.x, layout.playerStart.z);
                foreach (var wall in layout.walls)
                {
                    foreach (var anchor in layout.holeAnchors)
                        if (Distance(wall, anchor) <= gravity + 1f) failures.Add($"{layout.name}: wall {wall} reaches the hole anchor {anchor}");
                    if (Distance(wall, start) <= 8f) failures.Add($"{layout.name}: wall {wall} crowds the start");
                    if (wall.xMin < -layout.width * .5f || wall.xMax > layout.width * .5f || wall.yMin < -layout.height * .5f || wall.yMax > layout.height * .5f)
                        failures.Add($"{layout.name}: wall {wall} leaves the map");
                }
            }

            Assert.That(failures, Is.Empty, "doc 12.1-12.2: the Ruins have more walls, straight, L and ] shaped, away from the black holes");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task LayoutWallsFill_EveryRuinsLayout_EachRectGetsAWallColliderOnTheWallLayer()
        {
            var world = await LoadedMap();
            var failures = new List<string>();

            for (int i = 0; i < world.biomes.Length; i++)
            {
                var biome = world.biomes[i];
                if (Family(biome) != Ruins) continue;
                var group = world.biomeWallGroups[i];
                int saved = group.childCount;
                int added = LayoutWalls.Fill(group, biome.layout);
                if (biome != world.biome && added == 0) failures.Add($"{biome.name}: no wall added to the {saved} saved ones");
                if (LayoutWalls.Fill(group, biome.layout) != 0) failures.Add($"{biome.name}: a second fill added walls");
                if (group.childCount != biome.layout.walls.Length) failures.Add($"{biome.name}: {group.childCount} wall objects for {biome.layout.walls.Length} rects");
                bool wasActive = group.gameObject.activeSelf;
                group.gameObject.SetActive(true);
                Physics.SyncTransforms();
                try
                {
                    for (int w = 0; w < biome.layout.walls.Length && w < group.childCount; w++)
                    {
                        var rect = biome.layout.walls[w];
                        var wall = group.GetChild(w);
                        if (!wall.gameObject.activeSelf || wall.gameObject.layer != GameManager.WallLayer || !wall.GetComponent<BoxCollider>())
                            failures.Add($"{biome.name} wall {w}: not an active wall collider");
                        var footprint = new Rect(wall.position.x - wall.lossyScale.x * .5f, wall.position.z - wall.lossyScale.z * .5f, wall.lossyScale.x, wall.lossyScale.z);
                        if (Vector2.Distance(footprint.min, rect.min) > .01f || Vector2.Distance(footprint.max, rect.max) > .01f)
                            failures.Add($"{biome.name} wall {w}: footprint {footprint} for rect {rect}");

                        var inside = new Vector3(rect.center.x, 1f, rect.center.y);
                        var half = new Vector3(rect.width * .5f - .1f, .3f, rect.height * .5f - .1f);
                        var hits = Physics.OverlapBox(inside, half, Quaternion.identity, GameManager.WallMask, QueryTriggerInteraction.Ignore);
                        if (!hits.Any(hit => hit.transform.parent == group))
                            failures.Add($"{biome.name} wall {w}: no wall collider of this map inside rect {rect}");
                    }
                }
                finally
                {
                    group.gameObject.SetActive(wasActive);
                }
            }

            Assert.That(failures, Is.Empty, "collision, bounces and art follow the layout's walls");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task DunesLayouts_QuicksandPools_StayOffWallsHoleGravityAndTheStart()
        {
            var world = await LoadedMap();
            float gravity = world.holes.Max(h => h.GravityRadius);
            var failures = new List<string>();

            foreach (var layout in Layouts(world, Dunes))
            {
                if (layout.quicksand == null || layout.quicksand.Length < 6) failures.Add($"{layout.name}: {layout.quicksand?.Length ?? 0} quicksand pools");
                if (layout.quicksandSpeed <= 0f || layout.quicksandSpeed >= 1f) failures.Add($"{layout.name}: quicksand speed {layout.quicksandSpeed} does not slow");
                var pools = layout.quicksand ?? Array.Empty<MapLayout.SandPool>();
                var start = new Vector2(layout.playerStart.x, layout.playerStart.z);
                for (int p = 0; p < pools.Length; p++)
                {
                    var pool = pools[p];
                    string at = $"{layout.name} pool {pool.centre} r{pool.radius}";
                    if (layout.walls.Any(w => Distance(w, pool.centre) <= pool.radius)) failures.Add(at + " overlaps a wall");
                    if (layout.holeAnchors.Any(a => Vector2.Distance(a, pool.centre) <= pool.radius + gravity)) failures.Add(at + " reaches a black hole's gravity ring");
                    if (Vector2.Distance(pool.centre, start) <= pool.radius + 2f) failures.Add(at + " covers the start");
                    if (Mathf.Abs(pool.centre.x) + pool.radius >= layout.width * .5f || Mathf.Abs(pool.centre.y) + pool.radius >= layout.height * .5f)
                        failures.Add(at + " leaves the map");
                    for (int q = p + 1; q < pools.Length; q++)
                        if (Vector2.Distance(pool.centre, pools[q].centre) <= pool.radius + pools[q].radius) failures.Add(at + " overlaps pool " + q);
                }
            }
            foreach (var layout in Layouts(world, Ruins))
                if (layout.quicksand != null && layout.quicksand.Length > 0) failures.Add($"{layout.name}: the Ruins have quicksand");

            Assert.That(failures, Is.Empty, "doc 12.1: the Dunes have quicksand on open ground only");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task QuicksandBuild_DunesMap_DrawsColliderFreeFloorDecalsAndSlowsOnlyInsideThePools()
        {
            var world = await LoadedMap();
            var dunes = world.biomes.First(b => Family(b) == Dunes);
            var (biome, layout) = (world.biome, world.layout);
            try
            {
                world.biome = dunes;
                world.layout = dunes.layout;
                var drawn = Quicksand.Build(world);
                Assert.That(drawn, Is.Not.Null, "the Dunes layout has pools to draw");
                var decals = drawn.GetComponentsInChildren<MeshRenderer>();
                int perPool = Art.Enabled ? 1 : dunes.quicksandLayers.Count(l => l.texture);
                Assert.That(decals, Has.Length.EqualTo(dunes.layout.quicksand.Length * perPool), "one pit per pool with the illustrated art, one decal per pool and layer in whitebox");
                Assert.That(drawn.GetComponentsInChildren<Collider>(true), Is.Empty, "quicksand blocks neither bodies nor shots");
                Assert.That(decals, Has.All.Matches<MeshRenderer>(r => r.sortingOrder < 0 && r.transform.position.y > 0f && r.transform.position.y < .06f),
                    "drawn on the floor, under the ground telegraphs");

                var pool = dunes.layout.quicksand[0];
                var centre = new Vector3(pool.centre.x, 0f, pool.centre.y);
                Assert.That(Quicksand.SpeedFactor(centre), Is.EqualTo(dunes.layout.quicksandSpeed).Within(1e-4f), "walking in a pool is slowed");
                Assert.That(Quicksand.SpeedFactor(centre + Vector3.right * (pool.radius + .5f)), Is.EqualTo(1f), "just outside it walking is not");
                Assert.That(world.IsWalkable(centre, .5f), Is.True, "a pool is still walkable ground for enemies and spawns");
            }
            finally
            {
                world.biome = biome;
                world.layout = layout;
                Quicksand.Build(world);
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PlayerWalkingInQuicksand_VelocityIsTheLayoutShareOfTheNormalWalk()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var world = WorldMap.Active;
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(PlayerController).GetField("moveInput", Private).SetValue(player, Vector2.up);
            typeof(PlayerController).GetField("sprintMultiplier", Private).SetValue(player, 1f);
            var tick = typeof(PlayerController).GetMethod("TickNormal", Private);
            var body = player.GetComponent<Rigidbody>();
            float Walk()
            {
                tick.Invoke(player, new object[] { Time.fixedDeltaTime });
                return Query.Flat(body.linearVelocity).magnitude;
            }

            var original = world.layout;
            var sandy = Object.Instantiate(original);
            sandy.quicksand = Array.Empty<MapLayout.SandPool>();                                                                 
            world.layout = sandy;
            try
            {
                float plain = Walk();
                sandy.quicksand = new[] { new MapLayout.SandPool { centre = new Vector2(player.Position.x, player.Position.z), radius = 3f } };
                sandy.quicksandSpeed = .6f;
                float slowed = Walk();

                Assert.That(plain, Is.GreaterThan(1f), "the player walks forward");
                Assert.That(slowed / plain, Is.EqualTo(.6f).Within(1e-3f), "quicksand slows the player's walk to the layout's share");
            }
            finally
            {
                world.layout = original;
                body.linearVelocity = Vector3.zero;
                Object.Destroy(sandy);
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task EnemyWalkingInQuicksand_MovesAtTheLayoutShareOfItsSpeed()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var world = WorldMap.Active;
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var walker = targets[0];                        
            walker.Configure(walker.definition, 1f, false);                                                        
            V3CombatSmokeTest.Teleport(walker, game.player.Position + Vector3.forward * 14f);                                           
            walker.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            walker.enabled = true;
            Physics.SyncTransforms();

            var original = world.layout;
            var sandy = Object.Instantiate(original);
            sandy.quicksand = Array.Empty<MapLayout.SandPool>();                                                                 
            world.layout = sandy;
            try
            {
                float plain = await WalkSpeed(walker);
                sandy.quicksand = new[] { new MapLayout.SandPool { centre = new Vector2(walker.Position.x, walker.Position.z), radius = 4f } };
                sandy.quicksandSpeed = .5f;
                float slowed = await WalkSpeed(walker);

                Assert.That(plain, Is.GreaterThan(.5f), walker.definition.kind + " walks toward the player");
                Assert.That(slowed / plain, Is.EqualTo(.5f).Within(.08f), "quicksand halves its walking speed and nothing routes it around the pool");
            }
            finally
            {
                world.layout = original;
                Object.Destroy(sandy);
            }
        }

        static async Task<float> WalkSpeed(Combatant body)
        {
            for (int i = 0; i < 3; i++) await Awaitable.FixedUpdateAsync();
            Vector3 from = body.Position;
            const int steps = 10;
            for (int i = 0; i < steps; i++) await Awaitable.FixedUpdateAsync();
            return Query.Flat(body.Position - from).magnitude / (steps * Time.fixedDeltaTime);
        }

        static string Family(Biome biome) => biome.name.Split('_')[0];

        static int Tilt(Biome biome, EnemyKind kind) => biome.mixTilt != null && (int)kind < biome.mixTilt.Length ? biome.mixTilt[(int)kind] : 0;

        static Dictionary<EnemyKind, float> Shares(WaveManager waves, int wave, int group)
        {
            var weights = waves.GroupWeights(wave, group);
            float total = weights.Sum();
            var shares = new Dictionary<EnemyKind, float>();
            for (int i = 0; i < weights.Length; i++)
                if (waves.roster[i]) shares[waves.roster[i].kind] = total > 0f ? weights[i] / total : 0f;
            return shares;
        }

        static IEnumerable<MapLayout> Layouts(WorldMap world, string family) => world.biomes.Where(b => Family(b) == family).Select(b => b.layout);

        static float Distance(Rect rect, Vector2 point) =>
            Vector2.Distance(point, new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax)));

        static bool Touch(Rect a, Rect b) => a.xMin <= b.xMax && b.xMin <= a.xMax && a.yMin <= b.yMax && b.yMin <= a.yMax;

        static bool Across(Rect a, Rect b) => a.width > a.height != b.width > b.height;

        static List<List<Rect>> Joined(Rect[] walls)
        {
            var shapes = new List<List<Rect>>();
            var left = new List<Rect>(walls);
            while (left.Count > 0)
            {
                var shape = new List<Rect> { left[0] };
                left.RemoveAt(0);
                for (int i = 0; i < shape.Count; i++)
                for (int j = left.Count - 1; j >= 0; j--)
                    if (Touch(shape[i], left[j])) { shape.Add(left[j]); left.RemoveAt(j); }
                shapes.Add(shape);
            }
            return shapes;
        }

        static bool IsL(List<Rect> shape) => shape.Count == 2 && Across(shape[0], shape[1]);

        static bool IsBracket(List<Rect> shape)
        {
            if (shape.Count != 3) return false;
            for (int spine = 0; spine < 3; spine++)
            {
                var arms = shape.Where((_, i) => i != spine).ToArray();
                if (arms.All(a => Touch(a, shape[spine]) && Across(a, shape[spine])) && !Touch(arms[0], arms[1])) return true;
            }
            return false;
        }

        static Biome MakeBiome(bool inRotation)
        {
            var biome = ScriptableObject.CreateInstance<Biome>();
            biome.inRotation = inRotation;
            return biome;
        }

        static async Task<WorldMap> LoadedMap()
        {
            await Awaitable.NextFrameAsync();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!WorldMap.Active || WorldMap.Active.Grid == null || !GameManager.Instance)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for the world map.");
                await Awaitable.NextFrameAsync();
            }
            var world = WorldMap.Active;
            Assume.That(world.biomes != null && world.biomeWallGroups != null && world.biomes.Length == world.biomeWallGroups.Length, Is.True,
                "the scene keeps one wall group per map");
            return world;
        }
    }
}
