using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>The illustrated look of the quicksand pools: one sunken, slowly turning pit per pool, exactly the pool's size (Quicksand decides when; gameplay reads the
    /// layout, never these).</summary>
    public static class V5TerrainPits
    {
        const string MaterialPath = "V5/Terrain/V5QuicksandPit";
        // One full turn of the spiral takes this many seconds.
        const float SecondsPerTurn = 26f;
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static Material material;
        static float clock;

        /// <summary>Draws the pools under parent. False if the pit's material is missing, so the caller can fall back on its decals.</summary>
        public static bool Draw(Transform parent, MapLayout.SandPool[] pools, Mesh quad, float height, int sortingOrder)
        {
            if (!material)
            {
                var source = Resources.Load<Material>(MaterialPath);
                if (!source) return false;
                material = new Material(source) { hideFlags = HideFlags.DontSave };
            }
            for (int p = 0; p < pools.Length; p++)
            {
                var pit = new GameObject($"Pit{p}").transform;
                pit.SetParent(parent, false);
                pit.SetPositionAndRotation(new Vector3(pools[p].centre.x, height, pools[p].centre.y), Quaternion.identity);
                float size = pools[p].radius * 2f;
                pit.localScale = new Vector3(size, 1f, size);
                pit.gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = pit.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = sortingOrder;
            }
            return true;
        }

        /// <summary>Turns the spiral; it stands still while the game's time does.</summary>
        public static void Tick(float deltaTime)
        {
            if (!material) return;
            clock = (clock + deltaTime * Mathf.PI * 2f / SecondsPerTurn) % (Mathf.PI * 2f);
            material.SetFloat(ClockId, clock);
        }
    }
}
