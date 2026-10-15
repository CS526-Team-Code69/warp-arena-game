using UnityEngine;

namespace AnomalyArena
{

    public sealed class V2BlackHolePresentation : MonoBehaviour
    {
        public BlackHole hole;
        public SpriteRenderer core, swirl, gravity, portal;
        [Header("V4 high-visibility layers")]
        [Tooltip("Pulsing dashed ring on the pull border: where the danger starts.")] public SpriteRenderer warningRing;
        [Tooltip("Chevrons spiralling inward across the pull field.")] public SpriteRenderer inwardStreaks;
        [Tooltip("Purple halo around the kill core.")] public SpriteRenderer coreGlow;
        [Tooltip("Bright rim on the kill edge.")] public SpriteRenderer rimGlow;
        public float streakDegreesPerSecond = -75f;
        [Min(0f)] public float pulsePerSecond = 1.6f;
        public Renderer[] obsoleteBoxRenderers;
        public float swirlDegreesPerSecond = 32f;
        public float groundHeight = .035f;
        float rotation;

        void OnEnable() { HideBoxes(); rotation = 0f; }
        void HideBoxes() { if (obsoleteBoxRenderers != null) foreach (var box in obsoleteBoxRenderers) if (box) box.enabled = false; }
        void LateUpdate()
        {
            if (!hole) return;
            HideBoxes();
            var gm = GameManager.Instance;
            float dt = gm ? gm.WorldDeltaTime : Time.deltaTime;
            rotation = Mathf.Repeat(rotation + dt * swirlDegreesPerSecond, 360f);
            bool active = hole.Phase is HolePhase.Active or HolePhase.Collapsing;
            bool illustrated = Art.Enabled;
            float pulse = .5f + .5f * Mathf.Sin(Time.time * pulsePerSecond * Mathf.PI * 2f);
            bool pulling = hole.Phase == HolePhase.Active;
            Layer(gravity, pulling, hole.GravityRadius, groundHeight,
                illustrated ? new Color(.74f, .4f, 1f, .38f + .1f * pulse) : new Color(.62f, .4f, .95f, .3f), 0f);
            Layer(warningRing, pulling, hole.GravityRadius, groundHeight + .001f, new Color(.95f, .55f, 1f, .55f + .4f * pulse), rotation * .4f);
            Layer(inwardStreaks, pulling, hole.GravityRadius * .97f, groundHeight + .0015f, new Color(.85f, .55f, 1f, .7f), rotation * streakDegreesPerSecond / Mathf.Max(1f, swirlDegreesPerSecond));
            Layer(coreGlow, active, hole.KillRadius * 1.5f, groundHeight + .0018f, new Color(1f, 1f, 1f, .9f + .1f * pulse), 0f);
            Layer(core, active, hole.KillRadius, groundHeight + .002f, new Color(.018f, .01f, .03f, 1f), 0f);
            Layer(swirl, active && illustrated, hole.KillRadius, groundHeight + .004f, new Color(.78f, .5f, 1f, .95f), rotation);
            Layer(rimGlow, active, hole.KillRadius * 1.2f, groundHeight + .005f, new Color(.95f, .62f, 1f, .7f + .3f * pulse), -rotation);
            Layer(portal, hole.Phase == HolePhase.Portal, .65f, groundHeight + .006f,
                illustrated ? new Color(.7f, .9f, 1f, 1f) : Color.cyan, -rotation);
        }
        void Layer(SpriteRenderer renderer, bool show, float radius, float y, Color tint, float angle)
        {
            if (!renderer) return;
            renderer.enabled = show && renderer.sprite && radius > .001f;
            if (!renderer.enabled) return;
            renderer.transform.position = new Vector3(hole.Centre.x, y, hole.Centre.z);
            renderer.transform.rotation = Quaternion.Euler(90f, angle, 0f);
            var p = renderer.transform.parent ? renderer.transform.parent.lossyScale : Vector3.one;
            renderer.transform.localScale = new Vector3(radius * 2f / Mathf.Max(.001f, renderer.sprite.bounds.size.x * Mathf.Abs(p.x)), radius * 2f / Mathf.Max(.001f, renderer.sprite.bounds.size.y * Mathf.Abs(p.z)), 1f);
            renderer.color = tint;
        }
    }
}
