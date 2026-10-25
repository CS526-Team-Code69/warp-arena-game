using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{

    public sealed class Quicksand : MonoBehaviour
    {

        const float DecalHeight = .02f, LayerStep = .004f;

        const int SortingOrder = -3;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor"), TextureId = Shader.PropertyToID("_BaseMap");
        static Mesh quad;
        static Quicksand drawn;
        readonly List<(Transform decal, float spin)> spinning = new();

        public static bool IsQuicksand(Vector3 point)
        {
            var world = WorldMap.Active;
            var layout = world ? world.layout : null;
            if (!layout || layout.quicksand == null || world.TrialActive) return false;
            foreach (var pool in layout.quicksand) if (pool.Contains(point)) return true;
            return false;
        }

        public static float SpeedFactor(Vector3 point) => IsQuicksand(point) ? Mathf.Clamp(WorldMap.Active.layout.quicksandSpeed, .05f, 1f) : 1f;

        public static Quicksand Build(WorldMap world)
        {
            if (drawn) Destroy(drawn.gameObject);
            drawn = null;
            var pools = world && world.layout ? world.layout.quicksand : null;
            if (pools == null || pools.Length == 0) return null;
            drawn = new GameObject("Quicksand").AddComponent<Quicksand>();
            var look = world.biome;
            if (!look || !look.quicksandMaterial || look.quicksandLayers == null) return drawn;
            if (!quad) quad = BuildQuad();
            var block = new MaterialPropertyBlock();
            for (int p = 0; p < pools.Length; p++)
            for (int l = 0; l < look.quicksandLayers.Length; l++)
            {
                var layer = look.quicksandLayers[l];
                if (!layer.texture) continue;
                var decal = new GameObject($"Pool{p}_{l}").transform;
                decal.SetParent(drawn.transform, false);

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
                if (layer.spin != 0f) drawn.spinning.Add((decal, layer.spin));
            }
            return drawn;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var (decal, spin) in spinning) if (decal) decal.Rotate(0f, spin * dt, 0f, Space.World);
        }

        void OnDestroy()
        {
            if (drawn == this) drawn = null;
        }

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
