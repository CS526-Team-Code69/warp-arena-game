using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The crosshair. While the player can aim and fire (a wave's intro and its fight, the void trial, the final battle, the story's
    /// afternoons and nights) the mouse pointer is a crosshair, so the player sees where the weapon points: the shots go from the hero
    /// toward it. Every menu, page, pause, base screen and story moment keeps the system's arrow. It is the system's own pointer given a
    /// picture (Cursor.SetCursor), so it moves with the mouse at once, without the frame of delay a picture drawn by the game would have.
    /// The picture is made here: an orange dot in the middle, a cream ring round it and four cream ticks outside the ring, every line
    /// edged in near black so it reads on sand, at night and on the violet void alike.
    /// </summary>
    public sealed class AimCursor : MonoBehaviour
    {
        /// <summary>The picture's side in pixels: browsers show pointer pictures of this size on every system.</summary>
        const int Size = 32;
        /// <summary>In pixels from the middle: the ring's radius, the lines' half width, where the ticks start and end, the dot's half
        /// size and the dark edge's width.</summary>
        const float RingRadius = 7f, LineHalf = 1f, TickFrom = 11f, TickTo = 14.5f, DotHalf = 1f, EdgeWidth = 1f;

        static AimCursor instance;
        Texture2D crosshair;
        bool shown;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstance() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance || Application.isBatchMode) return;
            var host = new GameObject("AimCursor");
            DontDestroyOnLoad(host);
            host.AddComponent<AimCursor>();
        }

        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            crosshair = Draw();
        }

        void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            if (shown) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (crosshair) Destroy(crosshair);
        }

        /// <summary>The player can aim now: the run is on and its phase is a fight or its intro, the void trial, the final battle, or
        /// the story's afternoon or night (whose phase is Night).</summary>
        public static bool Aiming(GameManager game) =>
            game && game.State == GameState.Playing && (game.IsCombatActive || game.RunPhase == ArenaUiState.WaveIntro);

        void LateUpdate()
        {
            bool want = crosshair && Aiming(GameManager.Instance);
            if (want == shown) return;
            shown = want;
            if (want) Cursor.SetCursor(crosshair, new Vector2(Size * .5f, Size * .5f), CursorMode.Auto);
            else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        /// <summary>The crosshair's picture, each pixel the mean of 4 x 4 samples so the edges are smooth.</summary>
        static Texture2D Draw()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "AimCursor", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            Color cream = new Color32(250, 240, 222, 255), orange = new Color32(255, 146, 58, 255), edge = new Color(.06f, .05f, .05f, .85f);
            const int samples = 4;
            const float middle = Size * .5f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var sum = Color.clear; // premultiplied by alpha
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    var p = new Vector2(x + (sx + .5f) / samples - middle, y + (sy + .5f) / samples - middle);
                    float dot = Box(p, Vector2.zero, new Vector2(DotHalf, DotHalf));
                    float tickMiddle = (TickFrom + TickTo) * .5f, tickHalf = (TickTo - TickFrom) * .5f;
                    float ticks = Mathf.Min(
                        Mathf.Min(Box(p, new Vector2(tickMiddle, 0f), new Vector2(tickHalf, LineHalf)), Box(p, new Vector2(-tickMiddle, 0f), new Vector2(tickHalf, LineHalf))),
                        Mathf.Min(Box(p, new Vector2(0f, tickMiddle), new Vector2(LineHalf, tickHalf)), Box(p, new Vector2(0f, -tickMiddle), new Vector2(LineHalf, tickHalf))));
                    float lines = Mathf.Min(Mathf.Abs(p.magnitude - RingRadius) - LineHalf, ticks);
                    var c = dot <= 0f ? orange : lines <= 0f ? cream : Mathf.Min(dot, lines) <= EdgeWidth ? edge : Color.clear;
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
                sum /= samples * samples;
                pixels[y * Size + x] = sum.a > 0f ? (Color32)new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : new Color32(0, 0, 0, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Signed distance from p to an upright box of the given middle and half size: negative inside.</summary>
        static float Box(Vector2 p, Vector2 middle, Vector2 half)
        {
            var q = new Vector2(Mathf.Abs(p.x - middle.x) - half.x, Mathf.Abs(p.y - middle.y) - half.y);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        }
    }
}
