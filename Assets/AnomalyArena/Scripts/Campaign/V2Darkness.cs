using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{

    public sealed class V2Darkness : MonoBehaviour
    {
        const int W = 192, H = 108, MaxLights = 96;
        static readonly Color32 Night = new(4, 7, 16, 255);
        static readonly Color32 Violet = new(70, 20, 110, 255);

        GameManager gm;
        RawImage image;
        Texture2D texture;
        readonly Color32[] pixels = new Color32[W * H];
        readonly float[] gx = new float[W * H], gz = new float[W * H], lit = new float[W * H];
        readonly Vector4[] lights = new Vector4[MaxLights];
        int lightCount;
        float level, pulse, nextUpdate;
        int cols = 1, rows = 1;
        float[] regionExtra = { 0f };
        Bounds mapBounds;

        public static V2Darkness Create(GameManager gm)
        {
            var go = new GameObject("V2NightDarkness", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var d = go.AddComponent<V2Darkness>();
            d.gm = gm;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            int hudOrder = gm && gm.hud && gm.hud.v2Hud && gm.hud.v2Hud.localizedCanvas ? gm.hud.v2Hud.localizedCanvas.sortingOrder : 0;
            canvas.sortingOrder = hudOrder - 1;
            d.image = new GameObject("Dark", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            d.image.rectTransform.SetParent(go.transform, false);
            d.image.rectTransform.anchorMin = Vector2.zero; d.image.rectTransform.anchorMax = Vector2.one;
            d.image.rectTransform.offsetMin = d.image.rectTransform.offsetMax = Vector2.zero;
            d.image.raycastTarget = false;
            d.texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "NightDarkness", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            d.image.texture = d.texture;
            return d;
        }

        public void SetActive(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }

        Canvas canvas;

        void LateUpdate()
        {

            if (!canvas) canvas = GetComponent<Canvas>();
            bool show = !(WorldMap.Active && WorldMap.Active.TrialActive);
            if (canvas && canvas.enabled != show) canvas.enabled = show;
        }

        public void BeginFrame(float darkness, float violetPulse)
        {
            level = Mathf.Clamp01(darkness); pulse = Mathf.Clamp01(violetPulse);
            lightCount = 0;
        }

        public void AddLight(Vector3 at, float radius, float intensity)
        {
            if (lightCount >= MaxLights || radius <= 0f || intensity <= 0f) return;
            lights[lightCount++] = new Vector4(at.x, at.z, radius, Mathf.Clamp01(intensity));
        }

        public void SetRegions(int columns, int rowCount, float[] extra, Bounds bounds)
        {
            cols = Mathf.Max(1, columns); rows = Mathf.Max(1, rowCount); regionExtra = extra; mapBounds = bounds;
        }

        public void EndFrame()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 1f / 30f;
            var cam = gm ? gm.cam : null;
            if (!cam) return;
            GroundPoints(cam);
            System.Array.Clear(lit, 0, lit.Length);
            for (int i = 0; i < lightCount; i++) Splat(cam, lights[i]);
            for (int i = 0; i < pixels.Length; i++)
            {
                float extra = RegionExtra(gx[i], gz[i]);
                float a = Mathf.Clamp01((level + extra) * (1f - lit[i]));
                a = Mathf.Max(a, pulse * .6f);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Min(.97f, a) * 255f);
                pixels[i] = pulse > 0f ? Color32.Lerp(Night, Violet, pulse / .35f) : Night;
                pixels[i].a = alpha;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        void GroundPoints(Camera cam)
        {
            Ray r00 = cam.ViewportPointToRay(new Vector3(0f, 0f, 0f)), r10 = cam.ViewportPointToRay(new Vector3(1f, 0f, 0f));
            Ray r01 = cam.ViewportPointToRay(new Vector3(0f, 1f, 0f)), r11 = cam.ViewportPointToRay(new Vector3(1f, 1f, 0f));
            for (int y = 0; y < H; y++)
            {
                float v = (y + .5f) / H;
                Vector3 oL = Vector3.Lerp(r00.origin, r01.origin, v), oR = Vector3.Lerp(r10.origin, r11.origin, v);
                Vector3 dL = Vector3.Lerp(r00.direction, r01.direction, v), dR = Vector3.Lerp(r10.direction, r11.direction, v);
                for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W;
                    Vector3 o = Vector3.Lerp(oL, oR, u), d = Vector3.Lerp(dL, dR, u);
                    float t = Mathf.Abs(d.y) > 1e-4f ? -o.y / d.y : 0f;
                    int i = y * W + x;
                    gx[i] = o.x + d.x * t; gz[i] = o.z + d.z * t;
                }
            }
        }

        void Splat(Camera cam, Vector4 light)
        {
            float lx = light.x, lz = light.y, radius = light.z, intensity = light.w;
            int x0 = W, x1 = -1, y0 = H, y1 = -1;
            for (int c = 0; c < 4; c++)
            {
                Vector3 corner = new(lx + (c % 2 == 0 ? -radius : radius), 0f, lz + (c < 2 ? -radius : radius));
                Vector3 vp = cam.WorldToViewportPoint(corner);
                if (vp.z < 0f) return;
                x0 = Mathf.Min(x0, Mathf.FloorToInt(vp.x * W)); x1 = Mathf.Max(x1, Mathf.CeilToInt(vp.x * W));
                y0 = Mathf.Min(y0, Mathf.FloorToInt(vp.y * H)); y1 = Mathf.Max(y1, Mathf.CeilToInt(vp.y * H));
            }
            x0 = Mathf.Max(0, x0 - 1); y0 = Mathf.Max(0, y0 - 1); x1 = Mathf.Min(W - 1, x1 + 1); y1 = Mathf.Min(H - 1, y1 + 1);
            float inv = 1f / (radius * radius);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * W + x;
                    float dx = gx[i] - lx, dz = gz[i] - lz;
                    float q = (dx * dx + dz * dz) * inv;
                    if (q >= 1f) continue;
                    float f = (1f - q) * (1f - q * .35f) * intensity;
                    if (f > lit[i]) lit[i] = f;
                }
        }

        float RegionExtra(float x, float z)
        {
            if (regionExtra == null || regionExtra.Length < cols * rows || mapBounds.size.x <= 0f) return 0f;
            float fx = Mathf.Clamp((x - mapBounds.min.x) / mapBounds.size.x * cols - .5f, 0f, cols - 1);
            float fz = Mathf.Clamp((z - mapBounds.min.z) / mapBounds.size.z * rows - .5f, 0f, rows - 1);
            int ix = Mathf.Min(cols - 2, Mathf.FloorToInt(fx)), iz = Mathf.Min(rows - 2, Mathf.FloorToInt(fz));
            if (cols < 2 || rows < 2) return regionExtra[0];
            ix = Mathf.Max(0, ix); iz = Mathf.Max(0, iz);
            float tx = fx - ix, tz = fz - iz;
            float a = Mathf.Lerp(regionExtra[iz * cols + ix], regionExtra[iz * cols + ix + 1], tx);
            float b = Mathf.Lerp(regionExtra[(iz + 1) * cols + ix], regionExtra[(iz + 1) * cols + ix + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }
    }
}
