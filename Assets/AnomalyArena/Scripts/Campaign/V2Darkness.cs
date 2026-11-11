using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The night's darkness: one screen-wide image under the HUD whose alpha is computed on the CPU from the ground point under each
    /// texel (192 x 108, refreshed 30 times a second, bilinear-scaled to the screen). Light circles cut it away. A lamp's light and the
    /// base's are flat discs: evenly lit up to their radius (where the campaign draws a ring on the ground), with only a dim band past it,
    /// so what looks lit is lit. Nothing is painted over lit ground: there the scene shows as its own lights light it, so no film lies
    /// over it. The player's glow and the glints of nodes and shadow eyes fade softly. Regions crowded with shadows are a
    /// little darker. It needs no shader, so it behaves the same in the editor and on the web. The void trial lies off
    /// the map, where no night falls: while it runs the darkness is not drawn (since 2026-11-06, when the trial opened in the campaign).
    /// </summary>
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
        readonly bool[] flat = new bool[MaxLights];
        // The step from lit to unlit at a flat disc's radius is this wide, in cells, so that the edge shows no stairs.
        const float EdgeSoft = .7f;
        // A flat disc's dim band past its radius, in cells, and how lit that band is at the radius (it fades to nothing at its end).
        const float Penumbra = 2f, PenumbraLight = .22f;
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
            // The void trial (WorldMap.TrialActive) is off the map, where no night falls.
            if (!canvas) canvas = GetComponent<Canvas>();
            bool show = !(WorldMap.Active && WorldMap.Active.TrialActive);
            if (canvas && canvas.enabled != show) canvas.enabled = show;
        }

        public void BeginFrame(float darkness, float violetPulse)
        {
            level = Mathf.Clamp01(darkness); pulse = Mathf.Clamp01(violetPulse);
            lightCount = 0;
        }

        /// <summary>A light for this frame. flatDisc: evenly lit up to radius with a dim band past it (a lamp, the base); otherwise it
        /// fades softly from the centre to radius (a glow, a glint).</summary>
        public void AddLight(Vector3 at, float radius, float intensity, bool flatDisc = false)
        {
            if (lightCount >= MaxLights || radius <= 0f || intensity <= 0f) return;
            flat[lightCount] = flatDisc;
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
            for (int i = 0; i < lightCount; i++) Splat(cam, lights[i], flat[i]);
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

        /// <summary>The ground point (y = 0) under each texel: corner rays interpolated across the screen, exact for both the orthographic
        /// and the perspective camera views.</summary>
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

        /// <summary>Adds one light circle to the lit buffer, visiting only the texels its ground bounds cover on screen.</summary>
        void Splat(Camera cam, Vector4 light, bool flatDisc)
        {
            float lx = light.x, lz = light.y, radius = light.z, intensity = light.w;
            float reach = flatDisc ? radius + Penumbra : radius;
            int x0 = W, x1 = -1, y0 = H, y1 = -1;
            for (int c = 0; c < 4; c++)
            {
                Vector3 corner = new(lx + (c % 2 == 0 ? -reach : reach), 0f, lz + (c < 2 ? -reach : reach));
                Vector3 vp = cam.WorldToViewportPoint(corner);
                if (vp.z < 0f)
                {
                    // A corner behind the camera's plane. A small glow there is off the screen; a lamp's or the base's wide disc may
                    // still reach onto it, so every texel is tested for it.
                    if (!flatDisc) return;
                    x0 = 0; x1 = W - 1; y0 = 0; y1 = H - 1;
                    break;
                }
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
                    float f;
                    if (flatDisc)
                    {
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        if (d >= reach) continue;
                        // 1 inside the ring and 0 outside it, over a soft step at the ring.
                        float inside = Mathf.Clamp01((radius - d) / EdgeSoft + .5f);
                        float outer = intensity * PenumbraLight * (d > radius ? 1f - (d - radius) / Penumbra : 1f);
                        f = Mathf.Lerp(outer, intensity, inside);
                    }
                    else
                    {
                        float q = (dx * dx + dz * dz) * inv;
                        if (q >= 1f) continue;
                        f = (1f - q) * (1f - q * .35f) * intensity;
                    }
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
