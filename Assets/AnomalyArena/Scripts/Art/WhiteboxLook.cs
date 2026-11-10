using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// The root of a look built by Whitebox.Build: its parts and their colours. A part starts in the table's colour; SetColour changes
    /// it through a property block (only when the colour really changes) and moves the part to the see-through material while its alpha
    /// is below 1. Recolouring by colour key (SetColour(string, ...)) lets a caller change a whole role at once: the ship's engine
    /// ("control") lighting up once repaired, a base side ("damaged") turning red. Nothing here runs per frame on its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WhiteboxLook : MonoBehaviour
    {
        static MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Whitebox.Part[] parts = System.Array.Empty<Whitebox.Part>();
        MeshRenderer[] renderers = System.Array.Empty<MeshRenderer>();
        Color[] shown = System.Array.Empty<Color>(), table = System.Array.Empty<Color>();
        bool[] marks = System.Array.Empty<bool>(), hidden = System.Array.Empty<bool>();
        bool shellOnly, visible = true;

        /// <summary>The id the look was built from.</summary>
        public string LookId { get; private set; }
        public int PartCount => parts.Length;
        /// <summary>The index of the largest part: a character's body.</summary>
        public int MainPart { get; private set; }
        /// <summary>The box round every part, in the root's own space.</summary>
        public Bounds LocalBounds { get; private set; }

        internal void Setup(string lookId, Whitebox.Part[] table)
        {
            LookId = lookId;
            parts = table;
            renderers = new MeshRenderer[table.Length];
            shown = new Color[table.Length];
            this.table = new Color[table.Length];
            marks = new bool[table.Length];
            hidden = new bool[table.Length];
            MainPart = Whitebox.MainPart(table);
            for (int i = 0; i < table.Length; i++)
            {
                var part = table[i];
                this.table[i] = part.TableColour;
                marks[i] = part.colour == Whitebox.MarkColour;
                var go = new GameObject(i.ToString("00") + " " + part.prim + " " + part.colour) { layer = gameObject.layer };
                var t = go.transform;
                t.SetParent(transform, false);
                t.localPosition = part.position;
                t.localScale = Whitebox.PartScale(part);
                go.AddComponent<MeshFilter>().sharedMesh = Whitebox.MeshFor(part);
                var r = go.AddComponent<MeshRenderer>();
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderers[i] = r;
                Apply(i, this.table[i]);
                var bounds = part.Bounds;
                if (i == 0) LocalBounds = bounds;
                else { var all = LocalBounds; all.Encapsulate(bounds); LocalBounds = all; }
            }
        }

        public Whitebox.Part Part(int index) => parts[index];
        public Renderer PartRenderer(int index) => renderers[index];
        public string PartColourKey(int index) => parts[index].colour;
        /// <summary>The colour the part shows now, alpha included.</summary>
        public Color ShownColour(int index) => shown[index];
        /// <summary>The part's own colour in the table, alpha included.</summary>
        public Color TableColour(int index) => table[index];
        /// <summary>Whether the part is a small white mark (Whitebox.MarkColour) that body colours leave alone.</summary>
        public bool IsMark(int index) => marks[index];

        /// <summary>Shows or hides the parts whose table colour key is colourKey (the weapon crate's lock), for good: Visible leaves a
        /// hidden part hidden. Returns how many there were.</summary>
        public int ShowParts(string colourKey, bool show)
        {
            int count = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].colour != colourKey) continue;
                hidden[i] = !show;
                if (renderers[i]) renderers[i].enabled = show && visible;
                count++;
            }
            return count;
        }

        /// <summary>Whether the part is shown (not hidden by ShowParts, and the look visible).</summary>
        public bool PartShown(int index) => visible && !hidden[index];

        /// <summary>Shows or hides every part (the renderers, not the objects, so positions keep updating).</summary>
        public bool Visible
        {
            get => visible;
            set
            {
                if (visible == value) return;
                visible = value;
                for (int i = 0; i < renderers.Length; i++) if (renderers[i]) renderers[i].enabled = value && !hidden[i];
            }
        }

        /// <summary>Draws every part as the elite's white shell: back faces only, so a rim shows round whatever stands inside it.</summary>
        public void UseShell(Color colour)
        {
            shellOnly = true;
            for (int i = 0; i < renderers.Length; i++) Apply(i, colour);
        }

        public void SetColour(int index, Color colour)
        {
            if (index < 0 || index >= renderers.Length || shown[index] == colour && renderers[index].sharedMaterial) return;
            Apply(index, colour);
        }

        /// <summary>Recolours every part whose table colour key is colourKey; returns how many there were.</summary>
        public int SetColour(string colourKey, Color colour)
        {
            int count = 0;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].colour == colourKey) { SetColour(i, colour); count++; }
            return count;
        }

        /// <summary>Gives every part whose table colour key is colourKey its table colour back.</summary>
        public void ResetColour(string colourKey)
        {
            for (int i = 0; i < parts.Length; i++) if (parts[i].colour == colourKey) SetColour(i, table[i]);
        }

        /// <summary>Gives every part its table colour back.</summary>
        public void ResetColours()
        {
            for (int i = 0; i < parts.Length; i++) SetColour(i, table[i]);
        }

        void Apply(int index, Color colour)
        {
            var r = renderers[index];
            if (!r) return;
            shown[index] = colour;
            bool solid = !shellOnly && colour.a >= .999f;
            var material = shellOnly ? Whitebox.Shell : solid ? Whitebox.Opaque : Whitebox.SeeThrough;
            if (r.sharedMaterial != material)
            {
                r.sharedMaterial = material;
                // Small parts (eyes, marks, weapon pieces) cast no shadow: hundreds of them would only add draw calls.
                var size = parts[index].size;
                r.shadowCastingMode = solid && Mathf.Max(size.x, Mathf.Max(size.y, size.z)) >= .5f ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = solid;
            }
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(BaseColorId, colour);
            r.SetPropertyBlock(block);
        }
    }
}
