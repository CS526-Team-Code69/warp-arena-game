using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// Whitebox looks by id (2026-11-10): every kind of thing is its own set of polyhedra in its own colours, so the game reads without
    /// the illustrated art. The table is work/whitebox-1110/looks.json (made by make_sheet.py next to it); make_whitebox_cs.py turns it
    /// into Whitebox.Table.cs, so a shape or a colour is changed in the table and nowhere else. Sizes are metres, z is forward, the feet
    /// stand on y = 0.
    /// Every part is one renderer on a mesh shared by every part of its shape (Whitebox.Meshes.cs) and one of three shared materials
    /// (lit for solid parts, unlit see-through for parts with alpha below 1, and the elite shell's back faces); the colour is a property
    /// block (WhiteboxLook), so nothing is allocated per frame. Parts carry no colliders: a look is only seen, never hit.
    /// </summary>
    public static partial class Whitebox
    {
        /// <summary>The polyhedra of the table. Each fills the box of its part's size: Box a cuboid; Wedge high at the back, sloping down
        /// to the front; Pyramid a square base with its apex up; PyramidZ the same lying on its back, apex forward; Octa an octahedron;
        /// Prism6 / Prism8 a six- / eight-sided prism and Disc a 24-sided one, along whichever axis has the odd size out; Icosphere a
        /// once-divided icosahedron; Obelisk a tapering square shaft with a pyramid cap; Flare a horn twice as wide at the front as at
        /// the back; Ring a closed band round the axis of its thinnest size.</summary>
        public enum Prim { Box, Wedge, Pyramid, PyramidZ, Octa, Prism6, Prism8, Icosphere, Obelisk, Flare, Ring, Disc }

        /// <summary>One part of a look: its shape, size and centre (metres, in the look's own space), colour key and alpha.</summary>
        public readonly struct Part
        {
            public readonly Prim prim;
            public readonly Vector3 size, position;
            public readonly string colour;
            public readonly float alpha;

            public Part(Prim prim, float sx, float sy, float sz, float px, float py, float pz, string colour, float alpha)
            {
                this.prim = prim;
                size = new Vector3(sx, sy, sz);
                position = new Vector3(px, py, pz);
                this.colour = colour;
                this.alpha = alpha;
            }

            /// <summary>The box the part fills, in the look's space.</summary>
            public Bounds Bounds => new(position, size);
            public float Volume => size.x * size.y * size.z;
            /// <summary>The table's colour of this part, with its alpha.</summary>
            public Color TableColour { get { var c = Colour(colour); c.a = alpha; return c; } }
        }

        /// <summary>The colour key the table uses for small white marks (a body's front wedge, the elite's tip): a body's state colour
        /// (wind-up yellow, a hit flash, a status) leaves these parts as they are.</summary>
        public const string MarkColour = "white";

        static readonly Part[] Fallback = { new(Prim.Box, 1f, 1f, 1f, 0f, .5f, 0f, MarkColour, 1f) };
        static readonly HashSet<string> warned = new();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static Material opaque, seeThrough, shell;

        /// <summary>Builds the look under parent, feet on the parent's y = 0 and facing +z, and returns its root. The parts carry no colliders.
        /// The root holds a WhiteboxLook, through which the parts are recoloured; an id the table does not have builds a plain white cube
        /// and warns once.</summary>
        public static Transform Build(string lookId, Transform parent, string name = null)
        {
            if (lookId == null || !TableLooks.TryGetValue(lookId, out var parts))
            {
                if (warned.Add(lookId ?? "")) Debug.LogWarning($"[Whitebox] the table has no look \"{lookId}\"; a plain cube stands in");
                parts = Fallback;
            }
            var root = new GameObject(name ?? "Whitebox " + lookId).transform;
            if (parent) root.gameObject.layer = parent.gameObject.layer;
            root.SetParent(parent, false);
            root.gameObject.AddComponent<WhiteboxLook>().Setup(lookId, parts);
            return root;
        }

        /// <summary>Whether the table has a look (or a named piece of one) with this id.</summary>
        public static bool Has(string lookId) => lookId != null && TableLooks.ContainsKey(lookId);

        /// <summary>Every look id of the table and its named pieces.</summary>
        public static IEnumerable<string> LookIds => TableLooks.Keys;

        /// <summary>The parts of a look, in the table's order; empty for an id the table does not have.</summary>
        public static IReadOnlyList<Part> PartsOf(string lookId) =>
            lookId != null && TableLooks.TryGetValue(lookId, out var parts) ? parts : System.Array.Empty<Part>();

        /// <summary>A colour of the table by key (alpha 1); white for a key it does not have.</summary>
        public static Color Colour(string key) => key != null && TableColours.TryGetValue(key, out var c) ? c : Color.white;

        /// <summary>The box round every part of a look, in its own space (zero size for an unknown id). Walls that should match a look
        /// can be made from it, or from each part's Bounds.</summary>
        public static Bounds LocalBounds(string lookId)
        {
            var parts = PartsOf(lookId);
            if (parts.Count == 0) return default;
            var bounds = parts[0].Bounds;
            for (int i = 1; i < parts.Count; i++) bounds.Encapsulate(parts[i].Bounds);
            return bounds;
        }

        /// <summary>The index of the part with the largest volume: a character's body.</summary>
        public static int MainPart(IReadOnlyList<Part> parts)
        {
            int best = 0;
            for (int i = 1; i < parts.Count; i++) if (parts[i].Volume > parts[best].Volume) best = i;
            return best;
        }

        /// <summary>How far forward (look space z) the part reaches at the height y, from its shape: a pyramid narrows toward its apex,
        /// an octahedron toward both tips, a wedge slopes back. Used to set a front mark against the body.</summary>
        public static float FrontAt(in Part part, float y)
        {
            float t = Mathf.Clamp01((y - (part.position.y - part.size.y * .5f)) / Mathf.Max(1e-4f, part.size.y));
            float front = part.prim switch
            {
                Prim.Pyramid => .5f * (1f - t),
                Prim.Octa => .5f * (1f - Mathf.Abs(2f * t - 1f)),
                Prim.Icosphere => .5f * Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * t - 1f) * (2f * t - 1f))),
                Prim.Obelisk => t <= .8f ? .5f - (.5f - ObeliskShaftTop) * t / .8f : ObeliskShaftTop * (1f - (t - .8f) / .2f),
                Prim.Wedge => .5f - t,
                Prim.PyramidZ => .5f - Mathf.Abs(2f * t - 1f),
                _ => .5f
            };
            return part.position.z + part.size.z * front;
        }

        /// <summary>Puts a look (or any child) at a world position and turn with a world size, whatever its parent's scale: the auto
        /// weapons and the hole's pieces are placed from world positions each frame.</summary>
        public static void PlaceWorld(Transform look, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            look.SetPositionAndRotation(position, rotation);
            var parent = look.parent ? look.parent.lossyScale : Vector3.one;
            look.localScale = new Vector3(scale / Mathf.Max(1e-4f, Mathf.Abs(parent.x)), scale / Mathf.Max(1e-4f, Mathf.Abs(parent.y)),
                scale / Mathf.Max(1e-4f, Mathf.Abs(parent.z)));
        }

        // ───── Materials ─────

        /// <summary>Solid parts: a copy of the game's lit material (the one the old whitebox boxes used), so the build has its shader
        /// variant; URP Lit when there is no game manager yet.</summary>
        internal static Material Opaque
        {
            get
            {
                if (opaque) return opaque;
                var gm = GameManager.Instance;
                Material made = gm && gm.litMaterial ? new Material(gm.litMaterial) : null;
                if (!made)
                {
                    var lit = Shader.Find("Universal Render Pipeline/Lit");
                    made = lit ? new Material(lit) : SeeThroughMaterial(CullMode.Back, false);
                }
                made.name = "Whitebox (lit)";
                made.hideFlags = HideFlags.DontSave;
                if (made.HasProperty(BaseColorId)) made.SetColor(BaseColorId, Color.white);
                return opaque = made;
            }
        }

        /// <summary>Parts with alpha below 1 (the Shade, the decoy, a cloaked body): URP Unlit in its transparent variant, which the build
        /// keeps because the black hole and portal materials use it.</summary>
        internal static Material SeeThrough => seeThrough ? seeThrough : seeThrough = SeeThroughMaterial(CullMode.Back, true);

        /// <summary>The elite shell: the same, white, showing only back faces, so the body hides its middle and a rim shows round it.</summary>
        internal static Material Shell => shell ? shell : shell = SeeThroughMaterial(CullMode.Front, true);

        static Material SeeThroughMaterial(CullMode cull, bool transparent)
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var gm = GameManager.Instance;
            var m = unlit ? new Material(unlit) : gm && gm.litMaterial ? new Material(gm.litMaterial) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            m.name = cull == CullMode.Front ? "Whitebox (shell)" : "Whitebox (see-through)";
            m.hideFlags = HideFlags.DontSave;
            if (unlit && transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                // Set by the material inspector, not by the shader: a material made in code keeps One / Zero (opaque) without these.
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)cull);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, Color.white);
            return m;
        }
    }
}
