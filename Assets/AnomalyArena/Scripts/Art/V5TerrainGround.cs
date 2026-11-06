using System;
using UnityEngine;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class V5TerrainGround : MonoBehaviour
    {

        const int TexelsPerCell = 4;
        const float ShadowSoftness = .4f, ContactReach = .8f, RubbleReach = 1.5f;
        static readonly int MaskId = Shader.PropertyToID("_ShadowMask"), RectId = Shader.PropertyToID("_ShadowRect");
        static bool dirty;

        Renderer floor;
        Texture2D mask;
        byte[] pixels;
        float[] heights;
        int texelsX, texelsZ;
        float originX, originZ;
        MaterialPropertyBlock block;

        public static void Attach(WorldMap world, Renderer floor)
        {
            if (!world || !world.layout || !floor) return;
            if (!floor.TryGetComponent(out V5TerrainGround ground)) ground = floor.gameObject.AddComponent<V5TerrainGround>();
            ground.Setup(world.layout.width, world.layout.height, floor);
        }

        public static void MarkDirty() => dirty = true;

        void Setup(float width, float depth, Renderer target)
        {
            floor = target;
            int x = Mathf.Max(4, Mathf.RoundToInt(width * TexelsPerCell)), z = Mathf.Max(4, Mathf.RoundToInt(depth * TexelsPerCell));
            if (!mask || x != texelsX || z != texelsZ)
            {
                if (mask) Destroy(mask);
                texelsX = x;
                texelsZ = z;
                mask = new Texture2D(x, z, TextureFormat.RGBA32, false, true)
                {
                    name = "V5 wall shadows",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                pixels = new byte[x * z * 4];
                heights = new float[x * z];
            }
            originX = -width * .5f;
            originZ = -depth * .5f;
            block ??= new MaterialPropertyBlock();
            floor.GetPropertyBlock(block);
            block.SetTexture(MaskId, mask);
            block.SetVector(RectId, new Vector4(1f / width, 1f / depth, .5f, .5f));
            floor.SetPropertyBlock(block);
            dirty = true;
        }

        void OnDestroy()
        {
            if (mask) Destroy(mask);
        }

        void LateUpdate()
        {
            if (!dirty || !mask) return;
            dirty = false;
            Draw();
        }

        void Draw()
        {
            Array.Clear(heights, 0, heights.Length);
            for (int i = 0; i < pixels.Length; i += 4) pixels[i] = pixels[i + 1] = 0;                         
            foreach (var art in V5TerrainWallArt.Standing)
                if (art && art.Shape != null) Stamp(art);
            Vector3 toSun = V5TerrainWalls.ToSun;
            foreach (var art in V5TerrainWallArt.Standing)
                if (art && art.Shape != null) Shade(art, toSun);
            mask.LoadRawTextureData(pixels);
            mask.Apply(false, false);
        }

        void Stamp(V5TerrainWallArt art)
        {
            var shape = art.Shape;
            Frame(art, 0f, out int x0, out int x1, out int z0, out int z1, out Vector3 at, out Vector3 right, out Vector3 ahead);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = originX + (x + .5f) / TexelsPerCell - at.x, dz = originZ + (z + .5f) / TexelsPerCell - at.z;
                float height = shape.HeightAt(dx * right.x + dz * right.z, dx * ahead.x + dz * ahead.z);
                int index = x + z * texelsX;
                if (height > heights[index]) heights[index] = height;
            }
        }

        void Shade(V5TerrainWallArt art, Vector3 toSun)
        {
            var shape = art.Shape;
            float flat = new Vector2(toSun.x, toSun.z).magnitude;
            float sunX = flat > 1e-3f ? toSun.x / flat : 0f, sunZ = flat > 1e-3f ? toSun.z / flat : 0f, climb = flat > 1e-3f ? toSun.y / flat : 10f;
            float reach = Mathf.Max(shape.tallest / climb, RubbleReach);
            int steps = Mathf.CeilToInt(shape.tallest / climb * TexelsPerCell) + 1;
            float halfW = shape.width * .5f, halfD = shape.depth * .5f, step = 1f / TexelsPerCell;
            Frame(art, reach + step, out int x0, out int x1, out int z0, out int z1, out Vector3 at, out Vector3 right, out Vector3 ahead);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float wx = originX + (x + .5f) / TexelsPerCell - at.x, wz = originZ + (z + .5f) / TexelsPerCell - at.z;
                float along = Mathf.Max(Mathf.Abs(wx * right.x + wz * right.z) - halfW, 0f), across = Mathf.Max(Mathf.Abs(wx * ahead.x + wz * ahead.z) - halfD, 0f);
                float gap = Mathf.Sqrt(along * along + across * across);

                float cast = 0f;
                for (int s = 1; s <= steps; s++)
                {
                    int qx = x + Mathf.RoundToInt(s * sunX), qz = z + Mathf.RoundToInt(s * sunZ);
                    if (qx < 0 || qz < 0 || qx >= texelsX || qz >= texelsZ) break;
                    float height = heights[qx + qz * texelsX];
                    if (height <= 0f) continue;
                    cast = Mathf.Max(cast, (height - s * step * climb) / ShadowSoftness + .5f);
                }

                int index = (x + z * texelsX) * 4;
                Raise(index, Mathf.Clamp01(cast));
                float contact = Mathf.Clamp01(1f - gap / ContactReach);
                Raise(index + 1, contact * contact);
                Raise(index + 2, Mathf.Clamp01(1f - gap / RubbleReach));
            }
        }

        void Raise(int index, float value)
        {
            byte packed = (byte)(value * 255f);
            if (packed > pixels[index]) pixels[index] = packed;
        }

        void Frame(V5TerrainWallArt art, float margin, out int x0, out int x1, out int z0, out int z1, out Vector3 at, out Vector3 right, out Vector3 ahead)
        {
            var t = art.transform;
            at = t.position;
            right = t.right;
            ahead = t.forward;
            float halfW = art.Shape.width * .5f, halfD = art.Shape.depth * .5f;
            float extentX = Mathf.Abs(right.x) * halfW + Mathf.Abs(ahead.x) * halfD + margin, extentZ = Mathf.Abs(right.z) * halfW + Mathf.Abs(ahead.z) * halfD + margin;
            x0 = Mathf.Max(0, Mathf.FloorToInt((at.x - extentX - originX) * TexelsPerCell));
            x1 = Mathf.Min(texelsX - 1, Mathf.CeilToInt((at.x + extentX - originX) * TexelsPerCell));
            z0 = Mathf.Max(0, Mathf.FloorToInt((at.z - extentZ - originZ) * TexelsPerCell));
            z1 = Mathf.Min(texelsZ - 1, Mathf.CeilToInt((at.z + extentZ - originZ) * TexelsPerCell));
        }
    }
}
