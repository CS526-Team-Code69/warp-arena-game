using System;
using UnityEngine;

namespace AnomalyArena
{

    public class V2WorldLabels : MonoBehaviour
    {
        [Serializable]
        public struct EnemyLabel
        {
            public RectTransform root;
            public UnityEngine.UI.Image hp, shield, affix;
            public UnityEngine.UI.Text name;
            public UnityEngine.UI.Image[] statusIcons;
            public UnityEngine.UI.Text[] statusCounts;
        }

        public V2ArtSet art;
        public EnemyLabel[] enemyLabels;
        public UnityEngine.UI.Text[] holeLabels;
        public UnityEngine.UI.Text[] edgeLabels;
        [Header("Offscreen black hole marker")]
        public Color holeEdgeColor = new(.78f, .36f, 1f);
        public Color holeEdgeGlow = new(1f, .84f, 1f);
        [Min(0f)] public float holeEdgePulsePerSecond = 1.8f;
        [Min(1f)] public float holeEdgeScale = 1.35f;
        Enemy[] bindings;
        float refresh;
        RectTransform canvasRect;

        void Awake()
        {
            bindings = new Enemy[enemyLabels.Length];
            canvasRect = GetComponentInParent<Canvas>().transform as RectTransform;
        }

        void Update()
        {
            if (!ArenaText.Ready) return;
            var gm = GameManager.Instance;
            if (!gm || !gm.cam || !gm.waves || !canvasRect) return;
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0f) { refresh = .1f; RefreshEnemyData(gm); }
            for (int i = 0; i < bindings.Length; i++)
            {
                var enemy = bindings[i]; var view = enemyLabels[i];
                bool show = enemy && enemy.IsAlive && enemy.gameObject.activeInHierarchy && !enemy.Cloaked;
                if (show) show = PlaceWorld(view.root, enemy.HeadPoint(gm.cam), gm.cam);
                if (view.root.gameObject.activeSelf != show) view.root.gameObject.SetActive(show);
            }
            int edges = 0;
            var world = WorldMap.Active;
            if (world && world.holes != null)
            {
                for (int i = 0; i < Mathf.Min(holeLabels.Length, world.holes.Length); i++)
                {
                    var hole = world.holes[i]; var label = holeLabels[i];
                    bool show = hole && hole.Phase != HolePhase.Gone && !world.TrialActive;
                    if (!show) { label.gameObject.SetActive(false); continue; }
                    bool canEnter = hole.Phase == HolePhase.Portal && gm.player && gm.player.State == CharacterState.Normal
                        && gm.waves.Phase == WavePhase.WaveActive && Mathf.Abs(gm.player.Position.x - hole.Centre.x) <= .5f
                        && Mathf.Abs(gm.player.Position.z - hole.Centre.z) <= .5f;
                    string text = hole.Phase == HolePhase.Portal ? ArenaText.Text(canEnter ? "ui.hole.enter" : "ui.hole.portal") + "\n" + ArenaText.Text("ui.hole.remaining", hole.WavesLeft)
                        : world.CanFeed && hole.Phase == HolePhase.Active ? $"{hole.Feeds} / {hole.feedsToOpen}" : "";
                    if (label.text != text) label.text = text;
                    bool visible = PlaceWorld(label.rectTransform, hole.Centre + gm.cam.transform.up * (hole.size * .5f + 1f), gm.cam);
                    label.gameObject.SetActive(visible && text.Length > 0);
                    if (Offscreen(hole.Centre, gm.cam) && edges < edgeLabels.Length)
                    {
                        float distance = gm.player ? Query.Flat(hole.Centre - gm.player.Position).magnitude : 0f;
                        if (hole.Phase == HolePhase.Portal)
                            PlaceEdge(edgeLabels[edges++], hole.Centre, gm.cam, ArenaText.Text("ui.hole.offscreen", hole.WavesLeft), true);
                        else
                            PlaceHoleEdge(edgeLabels[edges++], hole.Centre, gm.cam, ArenaText.Pair("BLACK HOLE","BLACK HOLE") + $"  {distance:0}m");
                    }
                }
            }

            if (gm.waves.preparedMarkers != null)
                foreach (var marker in gm.waves.preparedMarkers)
                {
                    if (!marker || !marker.gameObject.activeInHierarchy || !marker.definition || marker.definition.kind != EnemyKind.Shade
                        || edges >= edgeLabels.Length || !Offscreen(marker.transform.position, gm.cam)) continue;
                    PlaceEdge(edgeLabels[edges++], marker.transform.position, gm.cam, ArenaText.Text("ui.shade.incoming"), true);
                }
            for (int i = edges; i < edgeLabels.Length; i++) edgeLabels[i].gameObject.SetActive(false);
        }

        static bool Offscreen(Vector3 world, Camera camera)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f;
        }

        void RefreshEnemyData(GameManager gm)
        {
            int index = 0;
            foreach (var enemy in gm.waves.Alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.gameObject.activeInHierarchy || index >= bindings.Length) continue;
                bindings[index] = enemy;
                var view = enemyLabels[index++]; var status = ElementStatus.For(enemy);
                view.hp.fillAmount = enemy.maxHp > 0f ? Mathf.Clamp01(enemy.Hp / enemy.maxHp) : 0f;
                view.shield.fillAmount = status && enemy.maxHp > 0f ? Mathf.Clamp01(status.Shield / enemy.maxHp) : 0f;
                view.shield.gameObject.SetActive(status && status.Shield > 0f);
                string name = enemy.IsBoss ? enemy.definition.kind == EnemyKind.Hollow ? ArenaText.Pair("THE HOLLOW","THE HOLLOW") : ArenaText.Pair("THE TYRANT","THE TYRANT") : enemy.Elite ? ArenaText.Text("ui.elite") : "";
                if (view.name.text != name) view.name.text = name;
                V2Hud.SetElementIcon(view.affix, art, (int)enemy.Affix - 1);
                for (int j = 0; j < 4; j++)
                {
                    float count = status ? j switch { 0 => status.Fire, 1 => status.Poison, 2 => status.Frost, _ => status.Lightning } : 0f;
                    bool on = count >= 1f || (j == 2 && status && status.Frozen) || (j == 3 && status && status.Paralysed);
                    V2Hud.SetElementIcon(view.statusIcons[j], art, on ? j : -1);
                    view.statusCounts[j].gameObject.SetActive(on);
                    string value = j == 2 && status && status.Frozen ? ArenaText.Text("ui.freeze.short") : j == 3 && status && status.Paralysed ? ArenaText.Text("ui.paralysis.short") : Mathf.FloorToInt(count).ToString();
                    if (on && view.statusCounts[j].text != value) view.statusCounts[j].text = value;
                }
            }
            for (int i = index; i < bindings.Length; i++) bindings[i] = null;
        }

        bool PlaceWorld(RectTransform label, Vector3 world, Camera cam)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            label.position = canvasRect.TransformPoint(local);
            return true;
        }

        void PlaceHoleEdge(UnityEngine.UI.Text label, Vector3 world, Camera cam, string value)
        {
            PlaceEdge(label, world, cam, value, false);
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * holeEdgePulsePerSecond * Mathf.PI * 2f);
            label.color = Color.Lerp(holeEdgeColor, holeEdgeGlow, pulse);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.localScale = Vector3.one * holeEdgeScale * (1f + .08f * pulse);
        }

        void PlaceEdge(UnityEngine.UI.Text label, Vector3 world, Camera cam, string value, bool bright)
        {
            label.fontStyle = FontStyle.Normal;
            label.rectTransform.localScale = Vector3.one;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) { screen.x = Screen.width - screen.x; screen.y = Screen.height - screen.y; }
            screen.x = Mathf.Clamp(screen.x, 110f, Screen.width - 110f);
            screen.y = Mathf.Clamp(screen.y, 170f, Screen.height - 140f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            label.rectTransform.position = canvasRect.TransformPoint(local);
            string arrow = Mathf.Abs(screen.x - Screen.width * .5f) > Mathf.Abs(screen.y - Screen.height * .5f)
                ? screen.x < Screen.width * .5f ? "◀ " : "▶ " : screen.y < Screen.height * .5f ? "▼ " : "▲ ";
            if (label.text != arrow + value) label.text = arrow + value;
            label.color = bright ? V2Hud.ElementColors[6] : new Color(.63f, .67f, .76f);
            label.gameObject.SetActive(true);
        }
    }
}
