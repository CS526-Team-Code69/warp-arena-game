using UnityEngine;

namespace AnomalyArena
{

    public sealed class V2EnemyTelegraphPresentation : MonoBehaviour
    {
        public Enemy enemy;
        public V2ArtSet art;
        public SpriteRenderer illustration;
        public Renderer whitebox;
        public Color whiteboxColor = new Color(1f, .25f, .2f, .7f);
        [Tooltip("Spire warning circle: line width in cells; the ring artwork grows too heavy at the Spire's size, so it uses a plain line.")]
        public float spireLineWidth = .1f;
        [Tooltip("Spire warning circle colour while winding up.")]
        public Color spireLineColor = new Color(1f, .62f, .3f, .55f);
        const int SpireSegments = 96;
        LineRenderer spireLine;

        void LateUpdate()
        {
            if (!enemy || !illustration || !art) return;
            if (enemy.definition && enemy.definition.kind == EnemyKind.Spire)
            {
                illustration.enabled = false;
                if (whitebox) whitebox.enabled = false;
                DrawSpireCircle();
                return;
            }
            if (spireLine) spireLine.enabled = false;
            Sprite sprite = enemy.LineTelegraph ? art.telegraphCharge : enemy.CleaveTelegraph ? art.telegraphCleave : art.telegraphRing;

            if (V5Vfx.On)
            {
                var drawn = V5Vfx.I.Sprite(enemy.LineTelegraph ? "tele-charge" : enemy.CleaveTelegraph ? "tele-cleave" : "tele-ring", sprite ? sprite.pixelsPerUnit : 100f);
                if (drawn) sprite = drawn;
            }
            bool show = enemy.TelegraphActive && sprite;
            illustration.enabled = show;
            if (whitebox) whitebox.enabled = !show;
            if (!show) return;
            if (illustration.sprite != sprite)
            {
                illustration.sprite = sprite;

                illustration.transform.localScale = new Vector3(1f / Mathf.Max(.01f, sprite.bounds.size.x), 1f / Mathf.Max(.01f, sprite.bounds.size.y), 1f);
            }
            Color telegraphColor = Art.Enabled ? enemy.IsBoss && enemy.BossPhase == 3 ? new Color(1f, .44f, .22f, .75f)
                : enemy.IsBoss && enemy.BossPhase == 2 ? new Color(.83f, .57f, 1f, .75f) : new Color(1f, .72f, .36f, .75f) : whiteboxColor;
            illustration.color = WithWindupAlpha(telegraphColor);
        }

        void DrawSpireCircle()
        {
            if (!enemy.TelegraphActive) { if (spireLine) spireLine.enabled = false; return; }
            if (!spireLine)
            {
                var line = new GameObject("SpireWarningCircle");
                line.transform.SetParent(transform, false);
                spireLine = line.AddComponent<LineRenderer>();
                spireLine.useWorldSpace = true;
                spireLine.loop = true;
                spireLine.positionCount = SpireSegments;
                spireLine.sharedMaterial = illustration.sharedMaterial;
                spireLine.sortingLayerID = illustration.sortingLayerID;
                spireLine.sortingOrder = illustration.sortingOrder;
                spireLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                spireLine.receiveShadows = false;
                spireLine.numCapVertices = 0;
            }
            float radius = Mathf.Max(.1f, enemy.definition.pulseRadius);
            Vector3 centre = new Vector3(enemy.Position.x, .06f, enemy.Position.z);
            for (int i = 0; i < SpireSegments; i++)
            {
                float a = i * Mathf.PI * 2f / SpireSegments;
                spireLine.SetPosition(i, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            Color c = WithWindupAlpha(Art.Enabled ? spireLineColor : whiteboxColor);
            spireLine.startColor = spireLine.endColor = c;
            spireLine.widthMultiplier = spireLineWidth;
            spireLine.enabled = true;
        }

        Color WithWindupAlpha(Color color)
        {
            float remaining = Mathf.Clamp01(enemy.WindupSecondsRemaining / Mathf.Max(.01f, enemy.windupTime));
            float progress = 1f - remaining;
            float alpha = Mathf.Lerp(.4f, 1f, progress);
            if (enemy.WindupSecondsRemaining <= .2f)
                alpha = Mathf.Sin((.2f - enemy.WindupSecondsRemaining) * Mathf.PI * 20f) >= 0f ? 1f : .4f;
            color.a = alpha;
            return color;
        }
    }
}
