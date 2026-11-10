using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Uses the boss's real phase; no independent phase timer or HP threshold. With the art off (2026-11-10) the phase is a row of
    /// the whitebox table's small tips over the boss's look (the elite's tip, "elite.tip"), one per phase reached: white in phase 1, amber in
    /// phase 2, red in phase 3, turned to read across the screen.</summary>
    public sealed class V2BossPresentation : MonoBehaviour
    {
        public Enemy enemy;
        public V2ArtSet art;
        public SpriteRenderer phaseMark;
        public float markHeight = .7f;
        public float headOffset = .65f;
        [Header("Whitebox phase tips (2026-11-10)")]
        [Tooltip("Gap between the top of the whitebox look and the bottom of the tips, cells.")]
        public float whiteboxTipGap = .4f;
        [Tooltip("Distance between two tips, as a share of their own (scaled) width.")]
        public float whiteboxTipSpacing = 1.6f;
        const int Phases = 3;
        static readonly string[] PhaseColours = { "white", "amber", "damaged" }; // keys of the whitebox table's colours
        Transform tipRow;
        WhiteboxLook[] tips;
        WhiteboxCharacter look;
        int paintedPhase = -1;

        void LateUpdate()
        {
            if (!enemy) return;
            int phase = Mathf.Clamp(enemy.BossPhase - 1, 0, Phases - 1);
            ShowWhiteboxTips(enemy.IsAlive && !Art.Enabled, phase);
            if (!phaseMark) return;
            Sprite sprite = art && art.bossPhaseMarks != null && phase < art.bossPhaseMarks.Length ? art.bossPhaseMarks[phase] : null;
            bool show = enemy.IsAlive && Art.Enabled && sprite;
            phaseMark.enabled = show;
            if (!show) return;
            if (phaseMark.sprite != sprite) phaseMark.sprite = sprite;
            var camera = GameManager.Instance ? GameManager.Instance.cam : null;
            if (!camera) return;
            // Over the top of the 3D model as this camera sees it (the 2.5D camera, 2026-11-06); whitebox keeps the head point.
            Vector3 head = enemy.TryModelTop(camera, out var top) ? top : enemy.HeadPoint(camera);
            phaseMark.transform.position = head + camera.transform.up * headOffset;
            phaseMark.transform.rotation = camera.transform.rotation;
            phaseMark.transform.localScale = Vector3.one * (markHeight / Mathf.Max(.01f, sprite.bounds.size.y));
        }

        /// <summary>phase + 1 tips over the whitebox look, larger on a larger boss; built once and kept.</summary>
        void ShowWhiteboxTips(bool show, int phase)
        {
            if (!show)
            {
                if (tipRow && tipRow.gameObject.activeSelf) tipRow.gameObject.SetActive(false);
                return;
            }
            if (!tipRow)
            {
                tipRow = new GameObject("Whitebox phase tips").transform;
                tipRow.SetParent(enemy.transform, false);
                tips = new WhiteboxLook[Phases];
                for (int i = 0; i < Phases; i++) tips[i] = Whitebox.Build("elite.tip", tipRow, "Tip " + i).GetComponent<WhiteboxLook>();
                paintedPhase = -1;
            }
            if (!tipRow.gameObject.activeSelf) tipRow.gameObject.SetActive(true);
            if (!look) look = enemy.GetComponent<WhiteboxCharacter>();
            var part = tips[0].Part(0);
            float scale = Mathf.Max(1f, enemy.radius);
            float top = look ? look.TopY : enemy.Position.y + enemy.radius * 2f;
            // The tips' row faces the camera's way across the screen, so they read side by side whatever the camera's turn.
            var camera = GameManager.Instance ? GameManager.Instance.cam : null;
            Vector3 across = camera ? Query.Flat(camera.transform.right) : Vector3.right;
            if (across.sqrMagnitude < 1e-4f) across = Vector3.right;
            Whitebox.PlaceWorld(tipRow, new Vector3(enemy.Position.x, top + whiteboxTipGap - (part.position.y - part.size.y * .5f) * scale, enemy.Position.z),
                Quaternion.LookRotation(Vector3.Cross(across.normalized, Vector3.up), Vector3.up), scale);
            float step = part.size.x * whiteboxTipSpacing;
            for (int i = 0; i < Phases; i++)
            {
                bool on = i <= phase;
                if (tips[i].gameObject.activeSelf != on) tips[i].gameObject.SetActive(on);
                tips[i].transform.localPosition = new Vector3((i - phase * .5f) * step, 0f, 0f);
            }
            if (paintedPhase == phase) return;
            paintedPhase = phase;
            var colour = Whitebox.Colour(PhaseColours[phase]);
            for (int i = 0; i < Phases; i++) tips[i].SetColour(0, colour);
        }
    }
}
