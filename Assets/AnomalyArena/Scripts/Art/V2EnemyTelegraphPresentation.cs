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
        void LateUpdate()
        {
            if (!enemy || !illustration || !art) return;
            Sprite sprite = enemy.LineTelegraph ? art.telegraphCharge : enemy.CleaveTelegraph ? art.telegraphCleave : art.telegraphRing;
            bool show = enemy.TelegraphActive && sprite;
            illustration.enabled = show;
            if (whitebox) whitebox.enabled = !show;
            if (!show) return;
            if (illustration.sprite != sprite)
            {
                illustration.sprite = sprite;

                illustration.transform.localScale = new Vector3(1f / Mathf.Max(.01f, sprite.bounds.size.x), 1f / Mathf.Max(.01f, sprite.bounds.size.y), 1f);
            }
            illustration.color = Art.Enabled ? enemy.IsBoss && enemy.BossPhase == 3 ? new Color(1f, .44f, .22f, .75f)
                : enemy.IsBoss && enemy.BossPhase == 2 ? new Color(.83f, .57f, 1f, .75f) : new Color(1f, .72f, .36f, .75f) : whiteboxColor;
        }
    }
}
