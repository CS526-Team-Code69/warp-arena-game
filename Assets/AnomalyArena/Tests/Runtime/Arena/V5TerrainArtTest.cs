using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class V5TerrainArtTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [Test]
        public void Build_AnyRectAndStone_MeshEdgesAreTheRectAndEverySpotHasStone()
        {
            var rects = new[]
            {
                new Rect(-65f, -33f, 4f, 2f), new Rect(13f, 8f, 2f, 5f), new Rect(-30f, 40f, 14f, 2f), new Rect(50f, -26f, 2f, 12f),
                new Rect(-56f, -33f, 4f, 3f), new Rect(22f, -8f, 6f, 4f), new Rect(3.2f, 7.7f, 1.667f, 2f), new Rect(-4.8f, 11f, 1.6f, 1.6f),
            };
            foreach (V5WallStyle style in System.Enum.GetValues(typeof(V5WallStyle)))
            foreach (var rect in rects)
            foreach (int axis in new[] { 0, 1 })
            {
                var shape = V5TerrainWalls.Build(style, rect, axis);
                try
                {
                    string what = $"{style} {rect} axis {axis}";
                    Assert.That(shape.mesh.bounds.size.x, Is.EqualTo(rect.width).Within(.001f), what + ": width");
                    Assert.That(shape.mesh.bounds.size.z, Is.EqualTo(rect.height).Within(.001f), what + ": depth");
                    Assert.That(shape.mesh.bounds.center.x, Is.EqualTo(0f).Within(.001f), what + ": centred across");
                    Assert.That(shape.mesh.bounds.center.z, Is.EqualTo(0f).Within(.001f), what + ": centred along");
                    Assert.That(shape.mesh.bounds.min.y, Is.GreaterThanOrEqualTo(-.001f), what + ": nothing below the ground");
                    Assert.That(shape.mesh.bounds.max.y, Is.LessThanOrEqualTo(2.4f), what + ": no taller than the old wall collider by much, so it hides next to nothing behind it");
                    Assert.That(shape.heights, Has.All.GreaterThan(0f), what + ": a spot of the footprint without stone");
                }
                finally
                {
                    Object.DestroyImmediate(shape.mesh);
                }
            }
        }

        [Test]
        public void Build_ThePieceOfAWall_KeepsTheStonesOfTheWhole()
        {
            var whole = V5TerrainWalls.Build(V5WallStyle.Ruins, new Rect(-30f, 40f, 14f, 2f), 0);
            var piece = V5TerrainWalls.Build(V5WallStyle.Ruins, new Rect(-30f + 6f, 40f, 2f, 2f), 0);
            try
            {
                int same = 0, spots = 0;
                for (float x = -.875f; x < 1f; x += .25f)
                for (float z = -.875f; z < 1f; z += .25f)
                {
                    spots++;

                    if (Mathf.Approximately(piece.HeightAt(x, z), whole.HeightAt(x, z))) same++;
                }
                Assert.That(same, Is.GreaterThanOrEqualTo(spots * 3 / 4), "a cut piece shows the same stones in the same places; only the slivers at its new ends may differ");
            }
            finally
            {
                Object.DestroyImmediate(whole.mesh);
                Object.DestroyImmediate(piece.mesh);
            }
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task TyrantCut_LongWall_EveryPieceDrawsExactlyItsCollider()
        {
            var world = await LoadedMap();
            var longest = world.layout.walls.OrderByDescending(r => Mathf.Max(r.width, r.height)).First();
            var terrain = TyrantTerrain.For(world);
            try
            {
                Vector3 mid = new(longest.center.x, 0f, longest.center.y);
                Vector3 across = longest.width >= longest.height ? Vector3.forward : Vector3.right;
                int broken = terrain.BreakAlong(mid - across * 5f, mid + across * 5f, 1f, 2f);
                Assert.That(broken, Is.GreaterThan(0), "the run broke part of the wall");
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();

                int checkedWalls = 0;
                foreach (var art in V5TerrainWallArt.Standing)
                {
                    if (!art || art.Shape == null || !art.TryGetComponent(out BoxCollider box)) continue;
                    checkedWalls++;
                    Bounds solid = box.bounds, drawn = art.Shape.mesh.bounds;
                    Assert.That(drawn.size.x, Is.EqualTo(solid.size.x).Within(.001f), art.name + " width");
                    Assert.That(drawn.size.z, Is.EqualTo(solid.size.z).Within(.001f), art.name + " depth");
                    var child = art.Stone;
                    Assert.That(child.lossyScale.x, Is.EqualTo(1f).Within(.001f), art.name + " stone is not stretched");
                    Assert.That(child.lossyScale.z, Is.EqualTo(1f).Within(.001f), art.name + " stone is not stretched");
                    Assert.That(child.position.y, Is.EqualTo(0f).Within(.001f), art.name + " stone stands on the ground");
                    Assert.That(child.position.x, Is.EqualTo(solid.center.x).Within(.001f), art.name + " stone sits on its collider");
                    Assert.That(child.position.z, Is.EqualTo(solid.center.z).Within(.001f), art.name + " stone sits on its collider");
                }
                Assert.That(checkedWalls, Is.EqualTo(world.layout.walls.Length), "one drawn wall per standing rect");
            }
            finally
            {
                terrain.ResetTerrain();
            }
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task Whitebox_TheStoneAndTheOldArtStayHidden_TheWallsOwnBoxShows()
        {
            var world = await LoadedMap();
            bool saved = Art.Enabled;
            try
            {
                Art.Enabled = false;
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                foreach (var look in Object.FindObjectsByType<V2TerrainPresentation>(FindObjectsSortMode.None))
                {
                    Assert.That(look.whitebox.enabled, Is.True, look.name + " shows its whitebox");
                    if (look.illustratedRoot) Assert.That(look.illustratedRoot.gameObject.activeSelf, Is.False, look.name + " hides the saved art");
                    if (look.TryGetComponent(out V5TerrainWallArt art)) Assert.That(art.Shape, Is.Null, look.name + " hides the stone");
                }
                Assert.That(world.floorRenderer.sharedMaterial.shader.name, Does.Not.Contain("V5"), "the whitebox floor is not the illustrated ground");
            }
            finally
            {
                Art.Enabled = saved;
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task<WorldMap> LoadedMap()
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!WorldMap.Active || WorldMap.Active.Grid == null || !GameManager.Instance)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for the world map.");
                await Awaitable.NextFrameAsync();
            }
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            return WorldMap.Active;
        }
    }
}
