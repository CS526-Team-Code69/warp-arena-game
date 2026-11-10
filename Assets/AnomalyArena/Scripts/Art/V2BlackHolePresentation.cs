using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Saved radial layers. The rule component owns phase and all radii, in both visual modes.
    /// With the art off (2026-11-10) the core is the whitebox table's hole ("hole.core": a flat black disc in a purple rim) at the kill
    /// radius, with the table's small cubes ("hole.box") circling over it; the pull field keeps its flat warning layers, which show where the
    /// danger starts. The open door is the table's standing light violet ring ("gate"; cyan, the colour of the player, until 2026-11-10), turned to the camera.</summary>
    public sealed class V2BlackHolePresentation : MonoBehaviour
    {
        /// <summary>How far an attack on the hole has got, 0..1, or below 0 when nothing is going on (the V2 campaign's timed damage check
        /// sets it; null shows no progress). The whitebox hole's cubes light up one by one with it.</summary>
        public static System.Func<BlackHole, float> AttackProgress;
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
        // The whitebox pieces, made on first use.
        const int WhiteboxCubes = 8; // the table's ring of small cubes over the hole
        Transform whiteboxCore, cubeRing, gate;
        WhiteboxLook[] cubes;
        int litCubes = -1;
        static float tableRadius, ringRadius;
        const float LitShare = .65f; // how far a lit cube goes from its own colour toward white

        /// <summary>The V5 art: one flat disc whose shader draws the hole from the real radii. Phase and radii still come from the rule component.</summary>
        void DrawV5()
        {
            var fx = V5Vfx.I;
            foreach (var layer in new[] { gravity, warningRing, inwardStreaks, coreGlow, core, swirl, rimGlow, portal }) if (layer) layer.enabled = false;
            if (!disc) MakeDisc();
            if (!disc) return;
            bool portalOpen = hole.Phase == HolePhase.Portal;
            bool active = hole.Phase is HolePhase.Active or HolePhase.Collapsing;
            // The hole giving way to the door: the gathered void bursts out once.
            if (portalOpen && !hole.Temporary && lastPhase == HolePhase.Collapsing) fx.VoidExplosion(hole.Centre, 5f);
            lastPhase = hole.Phase;
            disc.enabled = portalOpen || active && hole.KillRadius > .001f;
            if (!disc.enabled) return;
            bool pulling = hole.Phase == HolePhase.Active;
            // Collapsing, the pull is gone: the disc is only the shrinking core and its glow.
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
            if (V5Vfx.On) { ShowWhitebox(false, false, false); DrawV5(); return; }
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
            Layer(coreGlow, active && illustrated, hole.KillRadius * 1.5f, groundHeight + .0018f, new Color(1f, 1f, 1f, .9f + .1f * pulse), 0f);
            Layer(core, active && illustrated, hole.KillRadius, groundHeight + .002f, new Color(.018f, .01f, .03f, 1f), 0f);
            Layer(swirl, active && illustrated, hole.KillRadius, groundHeight + .004f, new Color(.78f, .5f, 1f, .95f), rotation);
            Layer(rimGlow, active && illustrated, hole.KillRadius * 1.2f, groundHeight + .005f, new Color(.95f, .62f, 1f, .7f + .3f * pulse), -rotation);
            Layer(portal, hole.Phase == HolePhase.Portal && illustrated, .65f, groundHeight + .006f, new Color(.7f, .9f, 1f, 1f), -rotation);
            ShowWhitebox(!illustrated, active && hole.KillRadius > .001f, hole.Phase == HolePhase.Portal);
        }

        /// <summary>The whitebox core at the kill radius (the table draws it for the 7-cell hole and is scaled to the real one), its cubes
        /// circling with the swirl, lit by AttackProgress; and the door while the portal is open.</summary>
        void ShowWhitebox(bool whitebox, bool core, bool door)
        {
            core &= whitebox;
            door &= whitebox;
            if (core && !whiteboxCore)
            {
                whiteboxCore = Whitebox.Build("hole.core", transform, "Whitebox hole");
                cubeRing = new GameObject("Whitebox hole cubes").transform;
                cubeRing.SetParent(transform, false);
                cubes = new WhiteboxLook[WhiteboxCubes];
                for (int i = 0; i < WhiteboxCubes; i++) cubes[i] = Whitebox.Build("hole.box", cubeRing, "Cube " + i).GetComponent<WhiteboxLook>();
                litCubes = -1;
            }
            if (door && !gate) gate = Whitebox.Build("gate", transform, "Whitebox door");
            if (whiteboxCore && whiteboxCore.gameObject.activeSelf != core) { whiteboxCore.gameObject.SetActive(core); cubeRing.gameObject.SetActive(core); }
            if (gate && gate.gameObject.activeSelf != door) gate.gameObject.SetActive(door);
            if (core)
            {
                if (tableRadius <= 0f) MeasureTable();
                float scale = hole.KillRadius / tableRadius;
                var ground = new Vector3(hole.Centre.x, 0f, hole.Centre.z);
                Whitebox.PlaceWorld(whiteboxCore, ground, Quaternion.identity, scale);
                Whitebox.PlaceWorld(cubeRing, ground, Quaternion.Euler(0f, rotation, 0f), scale);
                for (int i = 0; i < WhiteboxCubes; i++)
                {
                    float angle = i * Mathf.PI * 2f / WhiteboxCubes;
                    cubes[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius);
                }
                float progress = AttackProgress != null ? AttackProgress(hole) : -1f;
                int lit = progress < 0f ? 0 : Mathf.Clamp(Mathf.FloorToInt(progress * WhiteboxCubes + 1e-4f), 0, WhiteboxCubes);
                if (lit != litCubes)
                {
                    litCubes = lit;
                    var bright = Color.Lerp(cubes[0].TableColour(0), Whitebox.Colour(Whitebox.MarkColour), LitShare);
                    for (int i = 0; i < WhiteboxCubes; i++)
                        if (i < lit) cubes[i].SetColour(0, bright);
                        else cubes[i].ResetColours();
                }
            }
            if (door)
            {
                var gm = GameManager.Instance;
                var camera = gm && gm.cam ? gm.cam : Camera.main;
                Vector3 facing = camera ? Query.Flat(camera.transform.forward) : Vector3.forward;
                if (facing.sqrMagnitude < 1e-4f) facing = Vector3.forward;
                Whitebox.PlaceWorld(gate, new Vector3(hole.Centre.x, 0f, hole.Centre.z), Quaternion.LookRotation(facing.normalized, Vector3.up));
            }
        }

        /// <summary>The table's hole: the core disc's radius (the 7-cell hole it is drawn for) and how far out its cubes ride (its
        /// outermost cube in the side view).</summary>
        static void MeasureTable()
        {
            var core = Whitebox.PartsOf("hole.core");
            tableRadius = core.Count > 0 ? core[0].size.x * .5f : 3.5f;
            var parts = Whitebox.PartsOf("hole");
            ringRadius = 0f;
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].prim == Whitebox.Prim.Box) ringRadius = Mathf.Max(ringRadius, Mathf.Abs(parts[i].position.z));
            if (ringRadius <= 0f) ringRadius = tableRadius;
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
