using UnityEngine;

namespace AnomalyArena
{
    public sealed class V2BossPresentation : MonoBehaviour
    {
        public Enemy enemy;
        public V2ArtSet art;
        public SpriteRenderer phaseMark;
        public float markHeight = .7f;
        public float headOffset = .65f;
        void LateUpdate()
        {
            if (!enemy || !phaseMark || !art) return;
            int phase = Mathf.Clamp(enemy.BossPhase - 1, 0, 2);
            Sprite sprite = art.bossPhaseMarks != null && phase < art.bossPhaseMarks.Length ? art.bossPhaseMarks[phase] : null;
            bool show = enemy.IsAlive && Art.Enabled && sprite;
            phaseMark.enabled = show;
            if (!show) return;
            if (phaseMark.sprite != sprite) phaseMark.sprite = sprite;
            var camera = GameManager.Instance ? GameManager.Instance.cam : null;
            if (!camera) return;
            phaseMark.transform.position = enemy.HeadPoint(camera) + camera.transform.up * headOffset;
            phaseMark.transform.rotation = camera.transform.rotation;
            phaseMark.transform.localScale = Vector3.one * (markHeight / Mathf.Max(.01f, sprite.bounds.size.y));
        }
    }
}
