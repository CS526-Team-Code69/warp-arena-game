using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// Dunes quicksand (system doc 12.1): round pools from the map layout that slow everyone walking through them, the player and every enemy but
    /// the bosses, to the layout's quicksandSpeed. Pools never block movement, paths or shots and enemies do not route around them; the void
    /// trial has none. Gameplay reads the layout directly; this object only draws the pools, built once when the map is chosen.
    /// </summary>
    public sealed class Quicksand : MonoBehaviour
    {
        // Above the floor top (y = 0) and its detail patches, below every effect drawn on the ground; one small step per decal layer.
        const float DecalHeight = .02f, LayerStep = .004f;
        // Floor detail patches draw at -4, the black-hole rings from -3 up (no pool touches a hole) and effects at 0 or above, so the pools sit
        // on the floor art and under every telegraph.
        const int SortingOrder = -3;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor"), TextureId = Shader.PropertyToID("_BaseMap");
        static Mesh quad;
        static Quicksand drawn;
        readonly List<(Transform decal, float spin)> spinning = new();
        Biome biome;
        MapLayout.SandPool[] pools;
        bool illustrated;

        /// <summary>Whether the point is in one of the current map's quicksand pools; never during the void trial.</summary>
        public static bool IsQuicksand(Vector3 point)
        {
            var world = WorldMap.Active;
            var layout = world ? world.layout : null;
            if (!layout || layout.quicksand == null || world.TrialActive) return false;
            foreach (var pool in layout.quicksand) if (pool.Contains(point)) return true;
            return false;
        }

        /// <summary>Walking speed share at a point: the layout's quicksandSpeed inside a pool, 1 elsewhere.</summary>
        public static float SpeedFactor(Vector3 point) => IsQuicksand(point) ? Mathf.Clamp(WorldMap.Active.layout.quicksandSpeed, .05f, 1f) : 1f;

        /// <summary>Draws the pools of the world's current layout, replacing earlier decals: sunken pits with the illustrated art, the biome's
        /// quicksand decals in whitebox. Returns the decal root, or null when the layout has no pools.</summary>
        public static Quicksand Build(WorldMap world)
        {
            if (drawn) Destroy(drawn.gameObject);
            drawn = null;
            var pools = world && world.layout ? world.layout.quicksand : null;
            if (pools == null || pools.Length == 0) return null;
            drawn = new GameObject("Quicksand").AddComponent<Quicksand>();
            drawn.biome = world.CampaignMapApplied && !HasLook(world.biome) ? BorrowedLook(world) : world.biome;
            drawn.pools = pools;
            drawn.Draw();
            return drawn;
        }

        static bool HasLook(Biome look) => look && look.quicksandMaterial && look.quicksandLayers != null && look.quicksandLayers.Length > 0;

        /// <summary>V2 day campaign (since 2026-11-06): its map has pools on every biome, and a biome without a quicksand look of its own (the
        /// Ruins) draws them in whitebox with the first of the world's biomes that has one (the Dunes). Only the campaign's map asks; a
        /// classic map keeps its own biome's look. The map's own biome when none has a look.</summary>
        static Biome BorrowedLook(WorldMap world)
        {
            if (world.biomes == null) return world.biome;
            foreach (var other in world.biomes)
                if (HasLook(other)) return other;
            return world.biome;
        }

        void Draw()
        {
            illustrated = Art.Enabled;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var old = transform.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            spinning.Clear();
            if (!quad) quad = BuildQuad();
            if (illustrated && V5TerrainPits.Draw(transform, pools, quad, DecalHeight, SortingOrder)) return;
            var look = biome;
            if (!look || !look.quicksandMaterial || look.quicksandLayers == null) return;
            var block = new MaterialPropertyBlock();
            for (int p = 0; p < pools.Length; p++)
            for (int l = 0; l < look.quicksandLayers.Length; l++)
            {
                var layer = look.quicksandLayers[l];
                if (!layer.texture) continue;
                var decal = new GameObject($"Pool{p}_{l}").transform;
                decal.SetParent(transform, false);
                // A fixed turn per pool and layer keeps neighbouring pools from looking stamped.
                decal.SetPositionAndRotation(new Vector3(pools[p].centre.x, DecalHeight + l * LayerStep, pools[p].centre.y), Quaternion.Euler(0f, (p * 67f + l * 41f) % 360f, 0f));
                float size = pools[p].radius * 2f * Mathf.Max(.1f, layer.size);
                decal.localScale = new Vector3(size, 1f, size);
                decal.gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = decal.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = look.quicksandMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = SortingOrder;
                block.Clear();
                block.SetColor(ColorId, layer.tint);
                block.SetTexture(TextureId, layer.texture);
                renderer.SetPropertyBlock(block);
                if (layer.spin != 0f) spinning.Add((decal, layer.spin));
            }
        }

        void Update()
        {
            if (illustrated != Art.Enabled) Draw();
            float dt = Time.deltaTime;
            if (illustrated) V5TerrainPits.Tick(dt);
            foreach (var (decal, spin) in spinning) if (decal) decal.Rotate(0f, spin * dt, 0f, Space.World);
        }

        void OnDestroy()
        {
            if (drawn == this) drawn = null;
        }

        /// <summary>A unit quad lying flat on the ground (XZ plane), facing up.</summary>
        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "QuicksandQuad" };
            mesh.vertices = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f), new Vector3(.5f, 0f, -.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
