using System;
using UnityEngine;

namespace AnomalyArena
{
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
        [Tooltip("Combine markers within each 45-degree sector; at most eight directions.")] public float refreshInterval = .1f;
        public float horizontalInset = 92f, topInset = 148f, bottomInset = 170f;
        public Color arrowColor = new(1f, .61f, .37f);
        int[] counts;
        Vector2[] sums;
        float refresh;
        void Awake() { counts = new int[8]; sums = new Vector2[8]; }
        void OnDisable() => HideAll();
        void HideAll()
        {
            if (indicators != null) foreach (var indicator in indicators) if (indicator?.root) indicator.root.gameObject.SetActive(false);
        }
        void Update()
        {
            var gm = GameManager.Instance;
            if (!ArenaText.Ready || !gm || !gm.IsCombatActive || !waves || !cameraSource || !canvasRect) { HideAll(); return; }
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0f) return;
            refresh = Mathf.Max(.02f, refreshInterval);
            Array.Clear(counts, 0, counts.Length); Array.Clear(sums, 0, sums.Length);
            foreach (var enemy in waves.Alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.gameObject.activeInHierarchy) continue;
                Vector3 viewport = cameraSource.WorldToViewportPoint(enemy.Position);
                if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f) continue;
                Vector2 direction = new((viewport.x - .5f) * Screen.width, (viewport.y - .5f) * Screen.height);
                if (viewport.z <= 0f) direction = -direction;
                if (direction.sqrMagnitude < .0001f) direction = Vector2.up;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                int bin = (Mathf.RoundToInt(angle / 45f) + 8) % 8;
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
                float reachX = Mathf.Max(30f, centre.x - horizontalInset);
                float reachY = Mathf.Max(30f, direction.y > 0f ? centre.y - topInset : centre.y - bottomInset);
                float distance = Mathf.Min(reachX / Mathf.Max(.0001f, Mathf.Abs(direction.x)), reachY / Mathf.Max(.0001f, Mathf.Abs(direction.y)));
                Vector2 screen = centre + direction * distance;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
                view.root.position = canvasRect.TransformPoint(local);
                if (view.arrow)
                {
                    view.arrow.text = "▲"; view.arrow.color = arrowColor;
                    view.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                }
                if (view.count) view.count.text = ArenaText.Pair("×" + counts[i],"×" + counts[i]);
            }
        }
    }
}
