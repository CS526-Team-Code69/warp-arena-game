using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// A character's whitebox body (2026-11-10): the player, every enemy kind and the two bosses show their own look from the whitebox
    /// table (Whitebox.Build) instead of the saved capsule-box. V2ModelPresentation adds it to every character.
    /// Size: the table draws each kind at its prefab's size, so the look keeps the colliders as they are; it follows the saved body
    /// object's scale (a mini Splitter's 0.6, a Bloater's swell, the hit squash, the player's flattened corpse, the shrink into a gap)
    /// from the feet, so a scaled body stays on the ground (the saved box kept its centre and a mini Splitter floated 0.4 m up).
    /// Colour: each part shows its table colour. The body object's colour stays the one gameplay code writes (Combatant.RefreshBodyTint,
    /// the hit flash, Enemy's wind-up yellow, the Shade's cloak, V2EnemyThreatFeedback's warning flash); whenever it differs from the
    /// kind's own colour, the look's parts take it (the small white marks excepted), so every one of those keeps working unchanged. A
    /// see-through body colour (the cloak) keeps the parts' own colours and takes its alpha. Element states then tint the parts:
    /// frozen icy blue, burning a flickering orange, poisoned green, paralysed a blinking yellow (StatusTint).
    /// Marks: a small white wedge on the front for kinds whose look shows no front (FaceMark); an elite wears a white shell round its
    /// look and a small white tip over it (the table's "elite").
    /// The saved box, facing nose and other saved whitebox renderers are hidden in whitebox; the illustrated art never sees any of this.
    /// </summary>
    [DefaultExecutionOrder(1250)] // after the hit flash (Combatant), the model (1100) and the Bloater's warning flash (1200) wrote this frame's body colour
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class WhiteboxCharacter : MonoBehaviour
    {
        /// <summary>The element states a body can show, read from its ElementStatus (a plain copy, so the tint is a pure function).</summary>
        public struct StatusView
        {
            public bool frozen, paralysed, twitching;
            public float chill01, fire, poison;

            public bool Any => frozen || paralysed || twitching || chill01 > .05f || fire > .3f || poison > .5f;

            public static StatusView Of(ElementStatus status) => status
                ? new StatusView
                {
                    frozen = status.Frozen, paralysed = status.Paralysed, twitching = status.Twitching,
                    chill01 = status.Frost > .05f ? status.Chill01 : 0f, fire = status.Fire, poison = status.Poison
                }
                : default;
        }

        // How strongly each state pulls a part toward its colour (0 none, 1 all the way). Fire flickers between its two values, a
        // paralysis blinks between its two at ParalysisBlinksPerSecond.
        const float FrozenShare = .65f, ChillShare = .35f, FireLow = .3f, FireHigh = .65f, PoisonShare = .45f;
        const float ParalysedLow = .15f, ParalysedHigh = .6f, TwitchShare = .75f, ParalysisBlinksPerSecond = 4f;
        const float ProphetOrbitDegreesPerSecond = 40f; // the Nash Prophet's three small cubes circle it
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Combatant body;
        Enemy enemy;
        CapsuleCollider capsule;
        MaterialPropertyBlock read;
        Renderer[] saved;
        bool savedHidden;
        Transform root;
        WhiteboxLook look, shell, tip, face;
        Transform shellRoot;
        string builtId;
        int main;
        float orbit;
        int seed;

        /// <summary>The look shown now (null before the first whitebox frame).</summary>
        public WhiteboxLook Look => look;
        /// <summary>The elite's white shell (null until an elite first shows it; off while the body is not a living elite).</summary>
        public WhiteboxLook EliteShell => shell;
        /// <summary>The elite's white tip over the look.</summary>
        public WhiteboxLook EliteTip => tip;
        /// <summary>The white front wedge added to a kind whose look shows no front (null for the others).</summary>
        public WhiteboxLook FaceMark => face;
        /// <summary>Whether the whitebox look is showing this frame.</summary>
        public bool Shown { get; private set; }
        /// <summary>The colour of the body part (the largest) now.</summary>
        public Color BodyColour => look ? look.ShownColour(main) : default;
        /// <summary>The top of the look in the world, for marks over the head; the body's head point while no look shows.</summary>
        public float TopY => Shown && root ? root.TransformPoint(new Vector3(0f, look.LocalBounds.max.y, 0f)).y : body ? body.Position.y + body.radius * 2f : 0f;

        public static string LookIdFor(EnemyKind kind) => kind switch
        {
            EnemyKind.Grunt => "grunt",
            EnemyKind.Brute => "brute",
            EnemyKind.Skitter => "skitter",
            EnemyKind.Spitter => "spitter",
            EnemyKind.Rammer => "rammer",
            EnemyKind.Bloater => "bloater",
            EnemyKind.Warden => "warden",
            EnemyKind.Splitter => "splitter",
            EnemyKind.Spire => "spire",
            EnemyKind.Stomper => "stomper",
            EnemyKind.Shade => "shade",
            EnemyKind.Hollow => "prophet",
            EnemyKind.Tyrant => "tyrant",
            EnemyKind.Chanter => "chanter",
            _ => "grunt"
        };

        /// <summary>The look of a character: its kind's, or the player's.</summary>
        public static string LookIdFor(Combatant character) =>
            character is Enemy { definition: { } data } ? LookIdFor(data.kind) : "player";

        /// <summary>The top of the shown whitebox look as cam sees it (2026-11-10): over the body's middle, at the highest reach along the
        /// camera's up of any of its parts (and of the elite's tip), so a label or a mark drawn there sits just over the look in any view,
        /// as Combatant.TryModelTop does over a model. Each part is taken as its own box, so a stepped look (the Tyrant) is not raised by
        /// its wide base. False while no look is shown.</summary>
        public bool TryTop(Camera cam, out Vector3 point)
        {
            point = default;
            if (!Shown || !look || !cam) return false;
            Vector3 up = cam.transform.up;
            float highest = float.NegativeInfinity;
            for (int i = 0; i < look.PartCount; i++) highest = Mathf.Max(highest, Reach(look.PartRenderer(i), up));
            if (tip && tip.gameObject.activeInHierarchy)
                for (int i = 0; i < tip.PartCount; i++) highest = Mathf.Max(highest, Reach(tip.PartRenderer(i), up));
            Vector3 middle = look.PartRenderer(main).bounds.center;
            point = middle + up * (highest - Vector3.Dot(up, middle));
            return true;
        }

        /// <summary>How far along up the renderer's box reaches (its highest corner).</summary>
        static float Reach(Renderer part, Vector3 up)
        {
            if (!part) return float.NegativeInfinity;
            var box = part.bounds;
            var half = box.extents;
            return Vector3.Dot(up, box.center) + Mathf.Abs(up.x) * half.x + Mathf.Abs(up.y) * half.y + Mathf.Abs(up.z) * half.z;
        }

        /// <summary>The kinds whose table look shows no front of its own (a symmetric body with no barrel, plate, slope or white wedge):
        /// they get the table's small white front wedge ("face.mark"). The player, Grunt, Brute and Stomper carry it in the table; the
        /// Skitter and Rammer are wedges themselves; the Spitter's barrel and the Warden's plate show their front.</summary>
        public static bool AddsFaceMark(string lookId) => lookId is "splitter" or "chanter" or "bloater" or "spire" or "shade" or "prophet" or "tyrant";

        /// <summary>A part colour with the body's element states on it (pure: the same inputs give the same colour). time drives the
        /// burning flicker and the paralysis blink; seed keeps bodies from flickering in step.</summary>
        public static Color StatusTint(Color colour, StatusView view, float time, int seed = 0)
        {
            float a = colour.a;
            if (view.frozen) colour = Color.Lerp(colour, Whitebox.Colour("ice"), FrozenShare);
            else if (view.chill01 > .05f) colour = Color.Lerp(colour, Whitebox.Colour("ice"), ChillShare * Mathf.Clamp01(view.chill01));
            if (view.fire > .3f)
                colour = Color.Lerp(colour, Whitebox.Colour("fire"), Mathf.Lerp(FireLow, FireHigh, Mathf.PerlinNoise(time * 9f, (seed & 1023) * .013f)));
            if (view.poison > .5f) colour = Color.Lerp(colour, Whitebox.Colour("poison"), PoisonShare);
            if (view.twitching) colour = Color.Lerp(colour, Whitebox.Colour("shock"), TwitchShare);
            else if (view.paralysed)
                colour = Color.Lerp(colour, Whitebox.Colour("shock"), Mathf.Repeat(time * ParalysisBlinksPerSecond, 1f) < .5f ? ParalysedHigh : ParalysedLow);
            colour.a = a;
            return colour;
        }

        // A dead enemy lies as a low dark slab (2026-11-10): its standing look read as alive and hid what it dropped.
        const float CorpseHeight = .2f, CorpseShade = .4f;
        bool Corpse => enemy && enemy.CorpseActive;

        void Awake()
        {
            body = GetComponent<Combatant>();
            enemy = body as Enemy;
            capsule = GetComponent<CapsuleCollider>();
            read = new MaterialPropertyBlock();
            seed = Random.Range(0, 1024);
            // The decoy's whitebox stand-in follows the player's aim; the player carries it so no scene object is needed.
            if (body is PlayerController && !GetComponent<WhiteboxDecoys>()) gameObject.AddComponent<WhiteboxDecoys>();
        }

        void OnDisable() => Shown = false;

        void LateUpdate()
        {
            if (!body) return;
            bool whitebox = !Art.Enabled;
            HideSaved(whitebox);
            var bodyRenderer = body.bodyRenderer;
            bool show = whitebox && bodyRenderer && bodyRenderer.enabled;
            if (show) EnsureLook();
            if (root && root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            Shown = show && root;
            if (!Shown) return;
            Place(bodyRenderer.transform);
            Paint(bodyRenderer);
            ShowElite(enemy && enemy.ShowsEliteOutline);
            if (builtId == "prophet") Orbit();
        }

        /// <summary>The saved whitebox renderers (the body box, the facing nose, the illustration's whitebox parts) are hidden while the
        /// look shows. forceRenderingOff, not enabled: other code switches enabled with the art mode.</summary>
        void HideSaved(bool hide)
        {
            if (saved == null)
            {
                var list = new System.Collections.Generic.List<Renderer>();
                if (body.bodyRenderer) list.Add(body.bodyRenderer);
                foreach (var part in GetComponentsInChildren<Transform>(true))
                    if (part.name == "Facing" && part.TryGetComponent(out Renderer nose) && !list.Contains(nose)) list.Add(nose);
                foreach (var drawn in GetComponents<V2CharacterIllustration>())
                    if (drawn.whiteboxRenderers != null)
                        foreach (var part in drawn.whiteboxRenderers)
                            if (part && !list.Contains(part)) list.Add(part);
                saved = list.ToArray();
                savedHidden = !hide;
            }
            if (savedHidden == hide) return;
            savedHidden = hide;
            foreach (var part in saved) if (part) part.forceRenderingOff = hide;
        }

        void EnsureLook()
        {
            string id = LookIdFor(body);
            if (root && builtId == id) return;
            if (root) Destroy(root.gameObject);
            shell = tip = face = null;
            shellRoot = null;
            builtId = id;
            root = Whitebox.Build(id, transform, "Whitebox body");
            look = root.GetComponent<WhiteboxLook>();
            main = look.MainPart;
            orbit = 0f;
            if (AddsFaceMark(id)) BuildFaceMark();
        }

        /// <summary>The table's front wedge on a look without a front: at 78 % of the body's height (as the table sets it on the Grunt),
        /// just in front of the body's surface there, larger on a big body.</summary>
        void BuildFaceMark()
        {
            var marks = Whitebox.PartsOf("face.mark");
            var gruntParts = Whitebox.PartsOf("grunt");
            if (marks.Count == 0 || gruntParts.Count == 0) return;
            var mark = marks[0];
            // Where the table puts it on the Grunt: its height as a share of the body's, and how far its centre is from the body's front.
            var gruntBody = gruntParts[Whitebox.MainPart(gruntParts)];
            var onGrunt = gruntBody;
            foreach (var part in gruntParts)
                if (part.prim == Whitebox.Prim.Wedge && part.colour == Whitebox.MarkColour) { onGrunt = part; break; }
            float gruntTop = gruntBody.position.y + gruntBody.size.y * .5f;
            float height = onGrunt.position.y / Mathf.Max(.01f, gruntTop);                       // 0.78
            float gap = Mathf.Max(0f, onGrunt.position.z - Whitebox.FrontAt(gruntBody, onGrunt.position.y)); // 0.12
            var bodyPart = look.Part(main);
            float top = bodyPart.position.y + bodyPart.size.y * .5f;
            float y = top * height;
            float scale = Mathf.Max(1f, BaseRadius);
            float z = Whitebox.FrontAt(bodyPart, y) + gap * scale;
            var t = Whitebox.Build("face.mark", root, "Face mark");
            t.localScale = Vector3.one * scale;
            t.localPosition = new Vector3(0f, y - mark.position.y * scale, z);
            face = t.GetComponent<WhiteboxLook>();
        }

        /// <summary>The collider's radius as the prefab has it: the capsule is four radii tall on every character prefab, and its height
        /// never changes (Enemy.Configure scales only the radius, for a mini Splitter).</summary>
        float BaseRadius => capsule ? capsule.height * .25f : body.radius;

        /// <summary>Follows the saved body object's scale from the feet, and the hit jolt (Combatant.VisualOffset) like the model does.</summary>
        void Place(Transform bodyTransform)
        {
            // Every character prefab's body object has the scale of half the capsule's height (a capsule mesh two units tall).
            float baseScale = capsule ? Mathf.Max(.01f, capsule.height * .5f) : 1f;
            Vector3 scale = bodyTransform.localScale / baseScale;
            if (Corpse) scale.y *= CorpseHeight;
            root.localScale = scale;
            float feet = capsule ? capsule.center.y - capsule.height * .5f : 0f;
            Vector3 jolt = transform.InverseTransformVector(body.VisualOffset);
            root.localPosition = new Vector3(jolt.x, feet + jolt.y, jolt.z);
            root.localRotation = Quaternion.identity;
        }

        void Paint(Renderer bodyRenderer)
        {
            bodyRenderer.GetPropertyBlock(read);
            bool tinted = read.HasColor(BaseColorId);
            Color tint = tinted ? read.GetColor(BaseColorId) : default;
            // The kind's own colour on the body object means "no state": the parts keep the table's colours.
            if (tinted && enemy && enemy.definition && Same(tint, enemy.definition.colour)) tinted = false;
            bool flash = tinted && tint.a >= .999f && Same(tint, Color.white);
            var view = body.IsAlive && !flash ? StatusView.Of(ElementStatus.For(body)) : default;
            bool states = view.Any;
            bool corpse = Corpse;
            float time = Time.time;
            for (int i = 0; i < look.PartCount; i++)
            {
                Color c = look.TableColour(i);
                if (!look.IsMark(i))
                {
                    if (tinted) c = tint.a < .999f ? new Color(c.r, c.g, c.b, Mathf.Min(c.a, tint.a)) : new Color(tint.r, tint.g, tint.b, c.a);
                    if (states) c = StatusTint(c, view, time, seed);
                    if (corpse) c = new Color(c.r * CorpseShade, c.g * CorpseShade, c.b * CorpseShade, c.a);
                }
                look.SetColour(i, c);
            }
        }

        static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < .004f;

        /// <summary>The table's elite: a white shell (its first part's colour and alpha) the size of the elite's shell over its body,
        /// scaled about the body's middle, and the white tip the same height over the look's top as over the elite's body.</summary>
        void ShowElite(bool on)
        {
            if (on && !shell) BuildElite();
            if (shellRoot && shellRoot.gameObject.activeSelf != on) shellRoot.gameObject.SetActive(on);
            if (tip && tip.gameObject.activeSelf != on) tip.gameObject.SetActive(on);
        }

        void BuildElite()
        {
            var elite = Whitebox.PartsOf("elite");
            var tips = Whitebox.PartsOf("elite.tip");
            if (elite.Count < 2 || tips.Count == 0) return;
            Whitebox.Part outline = elite[0], inside = elite[1];
            float grow = outline.size.y / Mathf.Max(.01f, inside.size.y);             // 1.15
            float lift = tips[0].position.y - (inside.position.y + inside.size.y * .5f); // the tip's centre over the body's top
            var bodyPart = look.Part(main);
            shellRoot = Whitebox.Build(builtId, root, "Elite shell");
            shellRoot.localScale = Vector3.one * grow;
            shellRoot.localPosition = bodyPart.position * (1f - grow);
            shell = shellRoot.GetComponent<WhiteboxLook>();
            shell.UseShell(outline.TableColour);
            var tipRoot = Whitebox.Build("elite.tip", root, "Elite tip");
            tipRoot.localPosition = new Vector3(0f, look.LocalBounds.max.y + lift - tips[0].position.y, 0f);
            tip = tipRoot.GetComponent<WhiteboxLook>();
        }

        /// <summary>The Nash Prophet's small white cubes circle its body.</summary>
        void Orbit()
        {
            var gm = GameManager.Instance;
            orbit = Mathf.Repeat(orbit + (gm ? gm.WorldDeltaTime : Time.deltaTime) * ProphetOrbitDegreesPerSecond, 360f);
            var turn = Quaternion.Euler(0f, orbit, 0f);
            for (int i = 0; i < look.PartCount; i++)
                if (look.IsMark(i)) look.PartRenderer(i).transform.localPosition = turn * look.Part(i).position;
        }
    }
}
