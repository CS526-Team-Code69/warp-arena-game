using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Eight saved screen-edge groups. It points to actual living offscreen enemies, not spawn guesses.</summary>
    public sealed class V2EnemyDirections : MonoBehaviour
    {
        [Serializable]
        public sealed class Indicator
        {
            public RectTransform root;
            public UnityEngine.UI.Text arrow, count;
        }
        public WaveManager waves;
        public Camera cameraSource;
        public RectTransform canvasRect;
        public Indicator[] indicators;
        [Tooltip("同方向45度范围合并，最多显示8个方向。")] public float refreshInterval = .1f;
        [Tooltip("Canvas units kept clear at the sides, top and bottom (the bottom one is set by V2HudLayout).")]
        public float horizontalInset = 92f, topInset = 148f, bottomInset = 200f;
        public Color arrowColor = new(1f, .61f, .37f);
        int[] counts;
        Vector2[] sums;
        float refresh;
        // The fifth pass's markers sit nearer the screen's sides than the saved ones did, and below the plates along the top.
        const float FifthSideInset = 40f;
        /// <summary>The fifth pass's top inset in the open mode; while the story runs V5BattleHud keeps the markers lower, under its goal
        /// plate.</summary>
        internal const float FifthTopInset = 190f;
        V5BattleEdge[] edges;

        void Awake()
        {
            counts = new int[8]; sums = new Vector2[8];
            if (V5HudKit.On) BuildFifthEdges();
        }

        /// <summary>The fifth pass's markers (V5BattleEdge): an orange triangle pointing off the screen beside a small plate with the count.
        /// The saved arrow and count keep their words but are not drawn.</summary>
        void BuildFifthEdges()
        {
            if (indicators == null) return;
            edges = new V5BattleEdge[indicators.Length];
            for (int i = 0; i < indicators.Length; i++)
            {
                var view = indicators[i];
                if (view == null || !view.root) continue;
                if (view.arrow) view.arrow.enabled = false;
                if (view.count) view.count.enabled = false;
                edges[i] = V5BattleEdge.Create(view.root, "V5Edge");
            }
            horizontalInset = FifthSideInset;
            topInset = FifthTopInset;
        }
        /// <summary>
        /// Whether a point of the world lies off the camera's picture, and then the way to it on the screen from the screen's middle (in
        /// pixels). An orthographic camera's picture does not depend on depth, so a point behind the camera's plane (on a tilted view,
        /// ground far toward the bottom of the screen) keeps its side; only a perspective camera shows such a point mirrored, and only
        /// there is it turned back.
        /// </summary>
        public static bool OffScreenDirection(Camera camera, Vector3 world, out Vector2 direction)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            direction = new Vector2((viewport.x - .5f) * Screen.width, (viewport.y - .5f) * Screen.height);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f) return false;
            if (viewport.z <= 0f && !camera.orthographic) direction = -direction;
            if (direction.sqrMagnitude < .0001f) direction = Vector2.up;
            return true;
        }

        /// <summary>The marker (0 to 7, 45 degrees each, 0 pointing right and turning anticlockwise) a screen direction falls to.</summary>
        public static int Bin(Vector2 direction) => (Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg / 45f) + 8) % 8;

        void OnDisable() => HideAll();
        void HideAll()
        {
            if (indicators != null) foreach (var indicator in indicators) if (indicator?.root) indicator.root.gameObject.SetActive(false);
        }
        void Update()
        {
            var gm = GameManager.Instance;
            // Also during the freeze after a Void Trial won mid-wave, which is there to see where the enemies are (document 12.5).
            bool battlefield = gm && (gm.IsCombatActive || gm.RunPhase == ArenaUiState.TrialReturn);
            if (!ArenaText.Ready || !battlefield || !waves || !cameraSource || !canvasRect) { HideAll(); return; }
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0f) return;
            refresh = Mathf.Max(.02f, refreshInterval);
            Array.Clear(counts, 0, counts.Length); Array.Clear(sums, 0, sums.Length);
            foreach (var enemy in waves.Alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.gameObject.activeInHierarchy) continue;
                if (!OffScreenDirection(cameraSource, enemy.Position, out var direction)) continue;
                int bin = Bin(direction);
                counts[bin]++; sums[bin] += direction.normalized;
            }
            if (indicators == null) return;
            for (int i = 0; i < Mathf.Min(8, indicators.Length); i++)
            {
                var view = indicators[i]; if (view?.root == null) continue;
                bool on = counts[i] > 0; view.root.gameObject.SetActive(on);
                if (!on) continue;
                var direction = sums[i].normalized;
                Vector2 centre = new(Screen.width * .5f, Screen.height * .5f);
                // Insets are canvas units, so the markers stay clear of the HUD at every resolution.
                float unit = canvasRect && canvasRect.lossyScale.x > 0f ? canvasRect.lossyScale.x : 1f;
                float reachX = Mathf.Max(30f, centre.x - horizontalInset * unit);
                float reachY = Mathf.Max(30f, direction.y > 0f ? centre.y - topInset * unit : centre.y - bottomInset * unit);
                float distance = Mathf.Min(reachX / Mathf.Max(.0001f, Mathf.Abs(direction.x)), reachY / Mathf.Max(.0001f, Mathf.Abs(direction.y)));
                Vector2 screen = centre + direction * distance;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
                view.root.position = canvasRect.TransformPoint(local);
                if (view.arrow)
                {
                    view.arrow.text = "▲"; view.arrow.color = arrowColor;
                    view.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                }
                if (view.count) view.count.text = ArenaText.Pair("敌×" + counts[i], "×" + counts[i]);
                if (edges != null && i < edges.Length && edges[i]) edges[i].Show("×" + counts[i], V5TitleKit.Ink, V5TitleKit.Orange, direction);
            }
        }
    }
}
