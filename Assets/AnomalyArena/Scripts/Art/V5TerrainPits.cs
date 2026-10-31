using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{

    public static class V5TerrainPits
    {
        const string MaterialPath = "V5/Terrain/V5QuicksandPit";

        const float SecondsPerTurn = 26f;
        static readonly int ClockId = Shader.PropertyToID("_Clock");
        static Material material;
        static float clock;

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

        public static void Tick(float deltaTime)
        {
            if (!material) return;
            clock = (clock + deltaTime * Mathf.PI * 2f / SecondsPerTurn) % (Mathf.PI * 2f);
            material.SetFloat(ClockId, clock);
        }
    }
}
