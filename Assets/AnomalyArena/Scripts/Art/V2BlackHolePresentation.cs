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
        MeshRenderer disc;
        Material discMaterial;
        HolePhase lastPhase;
        static Mesh flatQuad;

        void DrawV5()
        {
            var fx = V5Vfx.I;
            foreach (var layer in new[] { gravity, warningRing, inwardStreaks, coreGlow, core, swirl, rimGlow, portal }) if (layer) layer.enabled = false;
            if (!disc) MakeDisc();
            if (!disc) return;
            bool portalOpen = hole.Phase == HolePhase.Portal;
            bool active = hole.Phase is HolePhase.Active or HolePhase.Collapsing;

            if (portalOpen && !hole.Temporary && lastPhase == HolePhase.Collapsing) fx.VoidExplosion(hole.Centre, 5f);
            lastPhase = hole.Phase;
            disc.enabled = portalOpen || active && hole.KillRadius > .001f;
            if (!disc.enabled) return;
            bool pulling = hole.Phase == HolePhase.Active;

            float outer = portalOpen ? 1.7f : pulling ? hole.GravityRadius : hole.KillRadius * 1.6f;
            disc.transform.position = new Vector3(hole.Centre.x, groundHeight + .01f, hole.Centre.z);
            disc.transform.rotation = Quaternion.identity;
            var p = disc.transform.parent ? disc.transform.parent.lossyScale : Vector3.one;
            disc.transform.localScale = new Vector3(outer * 2f / Mathf.Max(.001f, Mathf.Abs(p.x)), 1f, outer * 2f / Mathf.Max(.001f, Mathf.Abs(p.z)));
            discMaterial.SetFloat("_KillShare", portalOpen ? 0f : Mathf.Clamp01(hole.KillRadius / Mathf.Max(.01f, outer)));
            discMaterial.SetFloat("_Portal", portalOpen ? 1f : 0f);
            discMaterial.SetFloat("_Clock", Time.time);
            if (portalOpen) fx.PortalTick(hole.Centre, outer);
            else fx.HoleTick(hole.Centre, hole.GravityRadius, hole.KillRadius, pulling);
        }

        void MakeDisc()
        {
            var shader = Resources.Load<Shader>("V5/V5Hole");
            if (!shader) return;
            if (!flatQuad)
            {
                flatQuad = new Mesh { name = "V5HoleQuad" };
                flatQuad.vertices = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f), new Vector3(.5f, 0f, -.5f) };
                flatQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                flatQuad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                flatQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                flatQuad.RecalculateBounds();
            }
            var go = new GameObject("V5 Hole Disc");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = flatQuad;
            disc = go.AddComponent<MeshRenderer>();
            discMaterial = new Material(shader);
            discMaterial.SetTexture("_NoiseMap", V5Vfx.Tex("noise"));
            disc.sharedMaterial = discMaterial;
            disc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            disc.receiveShadows = false;
            disc.sortingOrder = -3;
        }

        void OnEnable() { HideBoxes(); rotation = 0f; }
        void HideBoxes() { if (obsoleteBoxRenderers != null) foreach (var box in obsoleteBoxRenderers) if (box) box.enabled = false; }
        void LateUpdate()
        {
            if (!hole) return;
            HideBoxes();
            if (V5Vfx.On) { DrawV5(); return; }
            if (disc) disc.enabled = false;
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
