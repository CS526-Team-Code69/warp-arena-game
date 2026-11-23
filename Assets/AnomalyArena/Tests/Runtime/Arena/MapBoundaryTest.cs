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
    /// The map's boundary (MapBoundaryArt): the view follows the player past the map's sides, and what it shows there used to be
    /// unmarked floor ending at a wall nobody could see. A low wall rings the map just outside its edge and the ground past it is
    /// dark, on the open mode's maps and on the story's larger one, in the whitebox look (boxes) and in the illustrated one (the
    /// map's stone, kept low). It is only drawn: nothing of it has a collider, and it is put away while the void trial runs.
    /// </summary>
    [TestFixture]
    public class MapBoundaryTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        bool savedArt;

        [SetUp]
        public void SetUp() => savedArt = Art.Enabled;

        [TearDown]
        public void TearDown()
        {
            Art.Enabled = savedArt;
            Time.timeScale = 1f;
            WorldMap.NextMapFamily = null;
        }

        [TestCase(Biome.Dunes), TestCase(Biome.Ruins)]
        [BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task OpenMode_ALowWallRingsTheMapOutsideItsEdge_InBothLooks_AndBlocksNothing(string family)
        {
            WorldMap.NextMapFamily = family;
            await SceneManagerHelper.LoadSceneAsync(Scene);
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            game.player.Invulnerable = true;
            var world = WorldMap.Active;
            var boundary = await Boundary(world);
            string map = world.biome.name;
            RingsTheMap(boundary, world, map);
            Assert.That(boundary.GetComponentsInChildren<Collider>(true), Is.Empty, map + ": nothing of the boundary has a collider, so bodies, shots and sight lines meet what they met before");

            // At a corner the pieces of two sides are in view.
            Bounds b = world.Bounds;
            V3CombatSmokeTest.Teleport(game.player, new Vector3(b.extents.x - 1.2f, 0f, b.extents.z - 1.2f));
            Physics.SyncTransforms();
            await Until(() => Shown(boundary).Count >= 2 && Shown(boundary).All(p => Dressed(p, Art.Enabled && boundary.StoneShown)), 20f, map + ": the wall's pieces by the corner are drawn");
            Assert.That(boundary.ShadeShown, Is.True, map + ": the dark past the wall is drawn");
            Assert.That(boundary.ShownPieces, Is.LessThan(boundary.PieceCount), map + ": only the pieces near the view are switched on");

            Art.Enabled = false;
            await Until(() => Shown(boundary).All(p => Dressed(p, false)), 10f, map + ": the whitebox look gives every piece in view its box");
            foreach (var piece in Shown(boundary))
            {
                var box = piece.GetComponent<MeshRenderer>();
                Assert.That(box.sharedMaterial, Is.SameAs(world.biome.wall), $"{map} {piece.name}: the box wears the map's own wall colour");
                Assert.That(box.bounds.max.y, Is.EqualTo(MapBoundaryArt.WhiteboxHeight).Within(.01f), $"{map} {piece.name}: the box is low");
                Assert.That(box.bounds.min.y, Is.EqualTo(0f).Within(.01f), $"{map} {piece.name}: the box stands on the ground");
            }

            Art.Enabled = true;
            await Until(() => Shown(boundary).All(p => Dressed(p, true)), 10f, map + ": the illustrated look gives every piece in view the map's stone");
            Assert.That(boundary.StoneShown, Is.True, map + " is built from stone");
            foreach (var piece in Shown(boundary))
            {
                var stone = piece.transform.Find("V5 Stone").GetComponent<MeshRenderer>();
                Bounds drawn = stone.bounds;
                Assert.That(piece.GetComponent<MeshRenderer>().enabled, Is.False, $"{map} {piece.name}: the box is put away under the stone");
                Assert.That(drawn.max.y, Is.InRange(.3f, 1.3f), $"{map} {piece.name}: the stone is kept low, so it hides nothing inside the map next to it");
                Assert.That(drawn.min.y, Is.EqualTo(0f).Within(.02f), $"{map} {piece.name}: the stone stands on the ground");
                Vector3 size = piece.transform.lossyScale;
                Assert.That(drawn.size.x, Is.EqualTo(size.x).Within(.02f), $"{map} {piece.name}: the stone fills the piece's width");
                Assert.That(drawn.size.z, Is.EqualTo(size.z).Within(.02f), $"{map} {piece.name}: the stone fills the piece's depth");
            }
            Debug.Log($"[MapBoundary] {map}: {boundary.PieceCount} pieces round {world.Bounds.size.x} x {world.Bounds.size.z}, {boundary.ShownPieces} in view at the corner");
        }

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task Story_TheWallRingsTheStorysLargerMap()
        {
            WorldMap.NextMapFamily = Biome.Ruins;
            await SceneManagerHelper.LoadSceneAsync(Scene);
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 30f, "scene and localization");
            var game = GameManager.Instance;
            var world = WorldMap.Active;
            Vector3 classic = world.Bounds.size;
            var boundary = await Boundary(world);
            RingsTheMap(boundary, world, "the classic map before the story starts");
            Assume.That(game.TryGetComponent(out V2Campaign campaign), Is.True, "the scene has the story");
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 60f, "Day 0's walk begins");
            Assume.That(world.CampaignMapApplied && world.Bounds.size != classic, Is.True, "the story lays its larger map");
            await Until(() => boundary && boundary.Inner.size == world.Bounds.size, 5f, "the boundary is built again round the story's map");
            RingsTheMap(boundary, world, $"the story's map, seed {campaign.MapSeed}");
            Assert.That(boundary.GetComponentsInChildren<Collider>(true), Is.Empty, "nothing of the boundary has a collider");
        }

        [Test, BuildScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task VoidTrial_TheMapsBoundaryIsPutAway_AndComesBackAfterIt()
        {
            WorldMap.NextMapFamily = Biome.Dunes;
            await SceneManagerHelper.LoadSceneAsync(Scene);
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            game.player.Invulnerable = true;
            var world = WorldMap.Active;
            var boundary = await Boundary(world);
            Bounds b = world.Bounds;
            V3CombatSmokeTest.Teleport(game.player, new Vector3(-b.extents.x + 1.2f, 0f, -b.extents.z + 1.2f));
            Physics.SyncTransforms();
            await Until(() => boundary.ShownPieces >= 2 && boundary.ShadeShown, 20f, "the boundary shows at the corner");

            game.player.enabled = true;
            game.waves.enabled = true;
            Assume.That(world.EnterLabTrial(game.player), Is.True, "the trial starts");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(boundary.ShownPieces, Is.Zero, "in the void trial no piece of the map's wall is drawn");
            Assert.That(boundary.ShadeShown, Is.False, "nor the dark past it");

            world.CompleteTrial();
            await Until(() => !world.TrialActive && boundary.ShadeShown, 20f, "the trial is over and the boundary is back");
        }

        // ───── What "rings the map" means here ─────

        /// <summary>Every piece lies outside the map with a side on its edge; together they fill the frame WallWidth wide round it
        /// exactly once (no gap, no overlap).</summary>
        static void RingsTheMap(MapBoundaryArt boundary, WorldMap world, string map)
        {
            Bounds b = world.Bounds;
            float hx = b.extents.x, hz = b.extents.z, w = MapBoundaryArt.WallWidth;
            Assert.That(boundary.Inner.size, Is.EqualTo(b.size), map + ": the boundary is built round the map in play");
            var rects = boundary.PieceRects.ToList();
            Assert.That(rects.Count, Is.GreaterThanOrEqualTo(4), map + ": a piece for every side at least");
            float area = 0f;
            foreach (var rect in rects)
            {
                area += rect.width * rect.height;
                bool outside = rect.xMin >= hx - .001f || rect.xMax <= -hx + .001f || rect.yMin >= hz - .001f || rect.yMax <= -hz + .001f;
                Assert.That(outside, Is.True, $"{map}: the piece {rect} lies outside the map ({b.size.x} x {b.size.z})");
                bool onEdge = Mathf.Abs(rect.xMin - hx) < .001f || Mathf.Abs(rect.xMax + hx) < .001f || Mathf.Abs(rect.yMin - hz) < .001f || Mathf.Abs(rect.yMax + hz) < .001f;
                Assert.That(onEdge, Is.True, $"{map}: the piece {rect} has a side on the map's edge");
                Assert.That(rect.xMin >= -hx - w - .001f && rect.xMax <= hx + w + .001f && rect.yMin >= -hz - w - .001f && rect.yMax <= hz + w + .001f, Is.True,
                    $"{map}: the piece {rect} keeps within {w} cells of the edge");
            }
            for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                float ox = Mathf.Min(rects[i].xMax, rects[j].xMax) - Mathf.Max(rects[i].xMin, rects[j].xMin);
                float oz = Mathf.Min(rects[i].yMax, rects[j].yMax) - Mathf.Max(rects[i].yMin, rects[j].yMin);
                Assert.That(ox > .001f && oz > .001f, Is.False, $"{map}: the pieces {rects[i]} and {rects[j]} overlap");
            }
            float frame = (hx * 2f + w * 2f) * (hz * 2f + w * 2f) - hx * 2f * hz * 2f;
            Assert.That(area, Is.EqualTo(frame).Within(.01f), map + ": the pieces fill the whole frame round the map");
        }

        static List<GameObject> Shown(MapBoundaryArt boundary)
        {
            var list = new List<GameObject>();
            var rim = boundary.transform.Find("Rim");
            if (!rim) return list;
            foreach (Transform piece in rim)
                if (piece.gameObject.activeInHierarchy) list.Add(piece.gameObject);
            return list;
        }

        /// <summary>The piece wears the look asked for: its stone and no box, or its box and no stone.</summary>
        static bool Dressed(GameObject piece, bool stone)
        {
            var art = piece.transform.Find("V5 Stone");
            bool stoneOn = art && art.gameObject.activeSelf && art.GetComponent<MeshFilter>().sharedMesh;
            return stone ? stoneOn && !piece.GetComponent<MeshRenderer>().enabled : !stoneOn && piece.GetComponent<MeshRenderer>().enabled;
        }

        static async Task<MapBoundaryArt> Boundary(WorldMap world)
        {
            await Until(() => MapBoundaryArt.Current && MapBoundaryArt.Current.PieceCount > 0, 10f, "the map gets its boundary");
            return MapBoundaryArt.Current;
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
