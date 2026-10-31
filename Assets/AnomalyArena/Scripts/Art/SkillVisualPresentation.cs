using UnityEngine;

namespace AnomalyArena
{

    public sealed class SkillVisualPresentation : MonoBehaviour
    {
        public SkillLoadout skills;
        public V2CharacterIllustration character;
        public SpriteRenderer[] voidWalkAfterimages;
        public SpriteRenderer singularityChargeRing;
        public LineRenderer[] singularityAbsorbLines;
        [Tooltip("Decorative charging ring radius; this is not a damage or pull area.")]
        public float chargeRingRadius = 1.35f;
        public float absorbLineWidth = .045f;

        readonly Vector3[] trail = new Vector3[16];
        int trailHead;
        float trailClock;

        void OnDisable() => HideAll();
        void HideAll()
        {
            if (voidWalkAfterimages != null) foreach (var sprite in voidWalkAfterimages) if (sprite) sprite.enabled = false;
            if (singularityChargeRing) singularityChargeRing.enabled = false;
            if (singularityAbsorbLines != null) foreach (var line in singularityAbsorbLines) if (line) line.enabled = false;
        }

        void LateUpdate()
        {
            if (!skills) { HideAll(); return; }
            var gm = GameManager.Instance;
            var camera = gm ? gm.cam : null;
            if (V5Vfx.On)
            {

                HideAll();
                if (skills.SingularityActive) V5Vfx.I.SingularityTick(skills.SingularityOrigin, skills.SingularityProgress01);
                return;
            }
            if (voidWalkAfterimages != null)
            {
                RecordTrail(gm);
                for (int i = 0; i < voidWalkAfterimages.Length; i++)
                {
                    var ghost = voidWalkAfterimages[i]; if (!ghost) continue;
                    bool show = Art.Enabled && skills.VoidWalkActive && character && character.illustration && character.illustration.sprite && camera;
                    ghost.enabled = show;
                    if (!show) continue;
                    ghost.sprite = character.illustration.sprite;
                    ghost.flipX = character.illustration.flipX;

                    var point = TrailPoint((i + 1) * 3);
                    ghost.transform.position = point + Vector3.up * character.height * .5f - camera.transform.forward * .025f;
                    ghost.transform.rotation = camera.transform.rotation;
                    float height = character.height * (1f - .05f * i);
                    Size(ghost, height * ghost.sprite.bounds.size.x / Mathf.Max(.01f, ghost.sprite.bounds.size.y), height, false);
                    ghost.color = new Color(.65f, .44f, 1f, .45f * Mathf.Max(.3f, skills.VoidWalkFlash01) / (1f + .7f * i));
                }
            }
            bool active = skills.SingularityActive;
            float progress = skills.SingularityProgress01;
            if (singularityChargeRing)
            {
                singularityChargeRing.enabled = active && singularityChargeRing.sprite;
                if (singularityChargeRing.enabled)
                {
                    singularityChargeRing.transform.position = skills.SingularityOrigin + Vector3.up * .07f;
                    singularityChargeRing.transform.rotation = Quaternion.Euler(90f, -progress * 720f, 0f);
                    float radius = chargeRingRadius * Mathf.Lerp(1.1f, .7f, progress);
                    Size(singularityChargeRing, radius * 2f, radius * 2f, true);
                    singularityChargeRing.color = new Color(.78f, .56f, 1f, Mathf.Lerp(.35f, .95f, progress));
                }
            }
            if (singularityAbsorbLines == null) return;
            var targets = skills.SingularityTargets;
            for (int i = 0; i < singularityAbsorbLines.Length; i++)
            {
                var line = singularityAbsorbLines[i]; if (!line) continue;
                var target = targets != null && i < targets.Count ? targets[i] : null;
                line.enabled = active && target && target.IsAlive;
                if (!line.enabled) continue;
                line.SetPosition(0, target.Position + Vector3.up * .18f);
                line.SetPosition(1, skills.SingularityOrigin + Vector3.up * .18f);
                line.startWidth = absorbLineWidth; line.endWidth = absorbLineWidth * .2f;
                line.startColor = new Color(.65f, .4f, 1f, Mathf.Lerp(.15f, .5f, progress));
                line.endColor = new Color(.85f, .73f, 1f, Mathf.Lerp(.4f, .95f, progress));
            }
        }

        void RecordTrail(GameManager gm)
        {
            var player = gm ? gm.player : null;
            if (!player) return;
            trailClock += Time.deltaTime;
            if (trailClock < .05f) return;
            trailClock = 0f;
            trailHead = (trailHead + 1) % trail.Length;
            trail[trailHead] = player.Position;
        }

        Vector3 TrailPoint(int samplesBack)
        {
            int index = ((trailHead - samplesBack) % trail.Length + trail.Length) % trail.Length;
            return trail[index];
        }

        static void Size(SpriteRenderer sprite, float width, float height, bool flat)
        {
            var p = sprite.transform.parent ? sprite.transform.parent.lossyScale : Vector3.one;
            float vertical = flat ? Mathf.Abs(p.z) : Mathf.Abs(p.y);
            sprite.transform.localScale = new Vector3(width / Mathf.Max(.001f, sprite.sprite.bounds.size.x * Mathf.Abs(p.x)), height / Mathf.Max(.001f, sprite.sprite.bounds.size.y * vertical), 1f);
        }
    }
}
