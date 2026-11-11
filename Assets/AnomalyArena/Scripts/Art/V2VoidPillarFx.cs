using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// The Nash Prophet's void pillars on screen, wherever they fall: in the void trial (V2ProphetTrialMarks) and round the player when
    /// it answers through the campaign's black hole (V2Campaign.Hole.cs). The caller hands over the pillars each frame; this only reads
    /// them (Enemy.VoidPillar: a circle warns, then the pillar stands and kills a player whose centre is inside it).
    ///
    /// Everything shown follows the pillar's own clock, so the picture can neither run ahead of the rule nor lag behind it:
    /// - The warning. The circle lies on the ground from the first frame with its outer edge on the deadly radius. A second ring grows
    ///   from the middle and meets that edge the instant the pillar comes down, so the gap left between the two is the time left; the
    ///   edge beats faster as it runs out, and a thread of light stands over the circle and swells just before the end.
    /// - The strike. In the frame the pillar turns deadly the edge flares white and the column is there at full width (its core wider
    ///   for a moment, then settling); once per pillar a burst and a short camera shake mark the instant.
    /// - Standing. A low veil stands exactly on the edge, hoops climbing it; inside it a column streams upward round a white-hot beam
    ///   that rises well above. Shortly before its time is up it flickers, but it keeps its full width for as long as it kills.
    /// - The end. A pillar is harmless from the frame it leaves the list: its edge and its clock go at once, and what is left of the
    ///   column thins to a thread and fades instead of popping off.
    /// Nothing lasting is drawn outside the deadly radius (the strike's flash and shock pass over it and are gone): pillars fall in
    /// groups with gaps to step through, and the gaps have to stay readable. For the same reason the veil is low and everything
    /// fades with height: what stands behind a pillar stays visible.
    ///
    /// Two looks, the same shapes on the ground. Illustrated (V5Vfx.On): light, built like the Prophet's shots, a small white-hot
    /// middle inside a wide violet glow. The lit parts are drawn additively and bright enough to bloom; noise streams up the veil and
    /// the column; a dark disc under the circle and a dark shaft behind the beam keep the outline on pale ground; the V5 layer adds
    /// the particles (V5Vfx.Pillars.cs). Whitebox: plain. The whitebox table's colours, drawn blended and never brighter than
    /// themselves, a plain column for the core, no particles; the table's small cubes ("hole.box") sit round the circle and light up
    /// one by one as the warning runs, then climb the standing pillar.
    ///
    /// Cost: one view per pillar, kept and shown again for later pillars; seven small meshes and five materials shared by every view
    /// (the V5 effect shader, loaded from Resources so builds keep it); colours go through one property block; nothing is allocated
    /// per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V2VoidPillarFx : MonoBehaviour
    {
        [Header("Shape")]
        [Min(1f), Tooltip("Height of a standing pillar's beam, cells. It fades out toward its top; the veil on the circle's edge is lower.")]
        public float columnHeight = 9f;
        [Range(.1f, 1f), Tooltip("Width of the streaming column inside the veil as a share of the deadly circle's. The veil always stands on the circle's edge.")]
        public float coreShare = .4f;
        [Range(0f, .3f), Tooltip("Width of the thread of light over a warning circle, as a share of the circle's. With 0 nothing stands over the circle until the last moment.")]
        public float threadShare = .045f;
        [Min(0f), Tooltip("Cells per second the streaks, the hoops and the whitebox cubes climb a standing pillar.")]
        public float riseSpeed = 6f;

        [Header("Timing")]
        [Min(0f), Tooltip("Beats per second of the warning circle's edge as the warning starts.")]
        public float beatFrom = 2f;
        [Min(0f), Tooltip("Beats per second of the edge as the warning ends; it quickens in between, so the time running out is felt.")]
        public float beatTo = 8f;
        [Min(.01f), Tooltip("Seconds the strike's flare takes to settle into the standing pillar.")]
        public float strikeSettle = .3f;
        [Min(0f), Tooltip("Seconds before its end in which a standing pillar flickers. It kills at full width until the end.")]
        public float endFlicker = .4f;
        [Min(.01f), Tooltip("Seconds a pillar that is over takes to thin out. It is harmless by then, and its circle's edge goes at once.")]
        public float fadeSeconds = .35f;

        [Header("Strike accent")]
        [Min(0f), Tooltip("Camera shake of a strike right beside the player; it falls to nothing at shakeReach.")]
        public float strikeShake = .14f;
        [Min(0f), Tooltip("How far the view closes in for an instant on a strike right beside the player.")]
        public float strikePunch = .006f;
        [Min(1f), Tooltip("Cells from the player beyond which a strike does not move the camera.")]
        public float shakeReach = 30f;

        [Header("Illustrated look")]
        [Min(0f), Tooltip("Brightness of the lit parts in the illustrated art; above 1 more of them reach the bloom.")]
        public float artBrightness = 1f;
        [Tooltip("The circle's edge while it warns.")]
        public Color artEdge = new(.72f, .42f, 1f);
        [Tooltip("What the edge and the beam turn to at the strike.")]
        public Color artHot = new(1f, .92f, 1f);
        [Tooltip("The ring growing from the middle, the floor of a standing pillar and the light at its foot.")]
        public Color artFill = new(.55f, .25f, 1f);
        [Tooltip("The veil on the circle's edge, its hoops, the streaming column and the glow round the beam.")]
        public Color artVeil = new(.62f, .36f, 1f);
        [Tooltip("The beam of a standing pillar and the thread over a warning circle.")]
        public Color artCore = new(.93f, .82f, 1f);
        [Tooltip("The disc under the circle and the shaft behind the beam: dark, so the pillar keeps its outline on pale ground.")]
        public Color artDark = new(.035f, .012f, .08f);

        // ───── Shapes shared by every view ─────

        // Points round a circle and round a column. At a radius of 3 cells the 72-sided edge stays within 3 mm of the true circle.
        const int Segments = 72, Sides = 40;
        // Heights above the ground: the dark disc lowest, then the light at the foot, the growing ring and the edge, all under the
        // trial's ward and tether lines (V2ProphetTrialMarks, .065), so nothing flickers where they cross.
        const float SinkHeight = .04f, HaloHeight = .044f, FillHeight = .05f, EdgeHeight = .056f;
        // Rows of the ground shapes, as shares of the radius from the outer edge inward, with the alpha of each. The outer row of
        // every one is at 1: scaled by a radius, the shape ends exactly there.
        static readonly float[] SinkRows = { 1f, .93f, .6f, 0f }, SinkAlpha = { 1f, .92f, .64f, .5f };
        static readonly float[] FillRows = { 1f, .95f, .86f, .5f, 0f }, FillAlpha = { 1f, 1f, .42f, .2f, .12f };
        static readonly float[] EdgeRows = { 1f, .955f, .9f, .74f }, EdgeAlpha = { 1f, 1f, .3f, 0f };
        static readonly float[] HoopRows = { 1f, .965f, .9f }, HoopAlpha = { 1f, 1f, 0f };
        // Rows up a column and up a beam, as shares of the height: bright at the foot and gone at the top, so nothing ends in a hard
        // line and what stands behind the upper part stays visible. The beam's first row is clear: a flat sheet would otherwise
        // meet the ground in a straight cut.
        static readonly float[] ShellRows = { 0f, .05f, .18f, .36f, .6f, 1f }, ShellAlpha = { 1f, .93f, .7f, .44f, .2f, 0f };
        static readonly float[] BeamRows = { 0f, .03f, .12f, .3f, .6f, 1f }, BeamAlpha = { 0f, 1f, .95f, .72f, .36f, 0f };
        // Heights as shares of the column's: the veil is a low wall, the streaming column and the dark shaft stop short of the
        // beam's top, and the hoops and cubes turn back half way up.
        const float VeilHeight = .4f, CoreHeight = .85f, ShaftHeight = .55f, ClimbTop = .45f;
        // Widths as multiples of the column's radius: the dark shaft behind it, the glow round the beam and the beam itself (the
        // soft line's picture fills about half its sheet, so the beam's white reads at about half the column's width).
        const float ShaftWidth = 1.5f, AuraWidth = 2.6f, BeamWidth = 1.1f;
        const int Hoops = 3;
        // The streaming noise: tiles round, cells of height one tile is stretched over (long, so it reads as streaks) and turns a
        // second, for the veil and for the column, which streams faster; and the shader's base and hot-part brightness that turn
        // the noise into dim wisps with bright streaks.
        const float VeilTiles = 5f, VeilStretch = 22f, VeilTurn = .07f, CoreTiles = 3f, CoreStretch = 14f, CoreTurn = -.16f, CoreRise = 1.8f;
        const float StreakBase = .3f, StreakGlow = 4.5f;
        // The column breathes by this share of its width, this often, and stands this much wider at the strike.
        const float CorePulse = .06f, CorePulseRate = 31f, CoreOvershoot = .55f;
        // The warning: seconds the circle's first flash lasts, the power that keeps the thread thin until the last tenth, and how
        // much of the column's width it swells to before the strike.
        const float PopSeconds = .18f, SwellPower = 12f, SwellShare = .6f;
        // A pillar first seen standing later than this after it came down gets no strike burst: the instant is over.
        const float FreshStrike = .15f;
        const float FlickerRate = 42f, MinRadius = .05f;
        // Whitebox cubes: how many, how far inside the edge they ride, the height they rest at while the circle warns, degrees a
        // second they circle a standing pillar, seconds they take to leap up at the strike, and how far a lit one goes toward white.
        const int Cubes = 6;
        const float CubeInset = .32f, CubeRest = .22f, CubeSpin = 110f, CubeLeap = .18f, CubeLit = .7f;
        const string CubeLook = "hole.box";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        static readonly Vector4 NoTiling = new(1f, 1f, 0f, 0f);
        static Mesh sinkMesh, fillMesh, edgeMesh, hoopMesh, shellMesh, beamMesh, quadMesh;
        // Blended; added; added with the streaming noise; added with the soft line across a beam; added with the soft round glow.
        static Material plain, light, streaks, soft, halo;
        static MaterialPropertyBlock block;
        // The whitebox table's colours for the same parts, read once.
        static Color boxDark, boxEdge, boxHot, boxFill, boxVeil, boxCore, boxHoop;
        static float cubeCentre;

        /// <summary>One pillar's shapes. A view follows a pillar of the list, thins out once that pillar is gone, then waits hidden.</summary>
        sealed class View
        {
            public Transform root;
            // On the ground: the dark disc, the light at the foot, the ring growing from the middle, the edge. Standing: the dark
            // shaft, the veil on the edge, the streaming column, the glow round the beam and the beam; the hoops.
            public MeshRenderer sink, halo, fill, edge, shaft, veil, core, aura, beam;
            public MeshRenderer[] hoops;
            // Every part above, to hide them in one go.
            public MeshRenderer[] parts;
            public readonly float[] hoopShown = new float[Hoops];
            public Transform[] cubes;
            public WhiteboxLook[] cubeLooks;
            public int litCubes;
            public bool shown, bound, matched, dressed, art;
            // The pillar as last seen.
            public Vector3 centre;
            public float radius, age, warning, life;
            public bool standing, sawWarning, struck;
            // 0 while the pillar lasts; 0 to 1 while the view thins out after it.
            public float gone;
            public float seed;
        }

        /// <summary>One view's frame in numbers, worked out once and used to place and to paint its parts.</summary>
        struct Moment
        {
            public float radius, height;
            // The pillar is down (or, thinning out, was); it is still in the list.
            public bool standing, lasts;
            // 0 to 1 over the warning (1 once standing); 1 at the strike falling to 0 as it settles.
            public float warned, flare;
            // 1 on each beat of the warning's edge and 0 between; 1 as the circle appears and 0 a moment later.
            public float beat, pop;
            // What the flicker before the end leaves of the column's light this frame; what thinning out leaves of the view; what
            // it leaves of the parts that go first (the veil, the column, the hoops).
            public float alive, keep, early;
            // Radius of the streaming column, or of the thread before the strike.
            public float core;
        }

        readonly List<View> views = new();
        int shownViews, drawnFrame = -1;
        float time;
        bool linear;
        Quaternion facing = Quaternion.identity;

        /// <summary>A new set of pillar views under parent (none: at the scene's root). It keeps itself in world space.</summary>
        public static V2VoidPillarFx Create(Transform parent, string name = "VoidPillars")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<V2VoidPillarFx>();
        }

        /// <summary>Views made so far. A view is kept and shown again for a later pillar.</summary>
        public int ViewCount => views.Count;

        /// <summary>Views that draw something now: a pillar's, or one thinning out after its pillar.</summary>
        public int ShownCount => shownViews;

        /// <summary>Draws the pillars for this frame, in the battlefield's time; call it every frame, also with none left (a pillar
        /// that is gone thins out over the next frames). A second call in the same frame draws again without moving time on.</summary>
        public void Draw(IReadOnlyList<Enemy.VoidPillar> pillars)
        {
            var game = GameManager.Instance;
            float dt = Time.frameCount == drawnFrame ? 0f : game ? game.WorldDeltaTime : Time.deltaTime;
            drawnFrame = Time.frameCount;
            Draw(pillars, dt);
        }

        /// <summary>Draws the pillars, with dt seconds gone since the last call (null or empty: none).</summary>
        public void Draw(IReadOnlyList<Enemy.VoidPillar> pillars, float dt)
        {
            int count = pillars?.Count ?? 0;
            if (count == 0 && shownViews == 0) return;
            if (!Ready())
            {
                Clear();
                return;
            }

            dt = Mathf.Max(0f, dt);
            time += dt;
            bool art = V5Vfx.On;
            linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            if (art) facing = Facing();
            KeepInWorldSpace();
            for (int i = 0; i < views.Count; i++) views[i].matched = false;
            for (int k = 0; k < count; k++)
            {
                var pillar = pillars[k];
                var view = ViewOf(pillar);
                view.matched = true;
                Follow(view, pillar, art);
                Show(view, art, dt);
            }

            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                if (view.matched || !view.shown) continue;
                if (view.bound) Release(view, art);
                view.gone += dt / Mathf.Max(.01f, fadeSeconds);
                if (view.gone >= 1f) Hide(view);
                else Show(view, art, dt);
            }
        }

        /// <summary>Everything goes at once (the fight is over, or its place has been left).</summary>
        public void Clear()
        {
            for (int i = 0; i < views.Count; i++)
                if (views[i].shown) Hide(views[i]);
        }

        void OnDisable() => Clear();

        // ───── Which view shows which pillar ─────

        /// <summary>The view already following this pillar, or a free one bound to it. The list gives a pillar no name and shifts
        /// when an older one ends, so a pillar is known by where it stands; one younger than the view's (a new pillar on the same
        /// spot) takes a view of its own.</summary>
        View ViewOf(in Enemy.VoidPillar pillar)
        {
            float radius = Mathf.Max(MinRadius, pillar.radius);
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                if (view.shown && view.bound && !view.matched && view.centre == pillar.centre && Mathf.Approximately(view.radius, radius) &&
                    pillar.age >= view.age - 1e-4f)
                    return view;
            }

            View free = null;
            for (int i = 0; i < views.Count && free == null; i++)
                if (!views[i].shown) free = views[i];
            free ??= Make();
            free.shown = free.bound = true;
            shownViews++;
            free.gone = 0f;
            free.centre = pillar.centre;
            free.radius = radius;
            free.age = pillar.age;
            free.standing = free.sawWarning = free.struck = false;
            free.litCubes = -1;
            // A phase of its own from where it stands, so pillars side by side do not move as one.
            free.seed = Mathf.Repeat(pillar.centre.x * .618f + pillar.centre.z * .414f, 1f);
            free.root.localPosition = new Vector3(pillar.centre.x, 0f, pillar.centre.z);
            return free;
        }

        /// <summary>Takes the pillar's state for this frame; the frame it is first seen standing is the strike.</summary>
        void Follow(View view, in Enemy.VoidPillar pillar, bool art)
        {
            view.age = pillar.age;
            view.warning = Mathf.Max(0f, pillar.warning);
            view.life = Mathf.Max(0f, pillar.life);
            view.standing = pillar.Standing;
            if (!view.standing)
            {
                view.sawWarning = true;
                return;
            }

            if (view.struck) return;
            view.struck = true;
            if (view.sawWarning || pillar.age - pillar.warning <= FreshStrike) Strike(view, art);
        }

        /// <summary>The view's pillar has left the list: it thins out from here.</summary>
        void Release(View view, bool art)
        {
            view.bound = false;
            view.gone = 0f;
            if (art && view.struck) V5Vfx.I.PillarFade(view.centre, view.radius, Mathf.Max(1f, columnHeight));
        }

        /// <summary>The view draws nothing and waits for a later pillar. Its renderers are switched off rather than its objects, which
        /// is safe at any moment (also while this object is being switched off or destroyed with its parent).</summary>
        void Hide(View view)
        {
            view.shown = view.bound = false;
            shownViews--;
            foreach (var part in view.parts)
                if (part && part.enabled) part.enabled = false;
            if (view.cubeLooks == null) return;
            foreach (var cube in view.cubeLooks)
                if (cube) cube.Visible = false;
        }

        /// <summary>The instant a pillar turns deadly, once per pillar: the burst (the V5 layer's in the illustrated art, the saved
        /// effect pool's plain flash and shards in whitebox; the Prophet's own ring already runs out to the radius) and a camera
        /// accent that weakens with the distance to the player. Several pillars striking together shake no more than one.</summary>
        void Strike(View view, bool art)
        {
            Vector3 foot = new(view.centre.x, 0f, view.centre.z);
            if (art) V5Vfx.I.PillarStrike(foot, view.radius, view.life);
            else
            {
                Fx.Pop(foot + Vector3.up * .6f, boxHot, view.radius * 2.2f);
                Fx.Burst(foot + Vector3.up * .4f, Vector3.zero, boxEdge, 8, view.radius * 2.4f, 360f, .16f);
            }

            var game = GameManager.Instance;
            if (!game || !game.player) return;
            float near = 1f - Mathf.Clamp01(Query.Flat(game.player.Position - foot).magnitude / Mathf.Max(1f, shakeReach));
            if (near <= 0f) return;
            if (strikeShake > 0f) game.Shake(strikeShake * near);
            if (strikePunch > 0f) game.Punch(strikePunch * near);
        }

        // ───── One view, one frame ─────

        void Show(View view, bool art, float dt)
        {
            if (!view.dressed || view.art != art) Dress(view, art);
            var m = MomentOf(view);
            Place(view, m, art);
            if (art) PaintArt(view, m);
            else PaintWhitebox(view, m);
            ShowCubes(view, m, !art);

            // What the V5 layer adds while the pillar lasts: sparks gathering into a warning circle, motes and streaks climbing a
            // standing one. Their rates are per second of real time, so nothing is emitted while time stands still.
            if (!art || !m.lasts || dt <= 0f) return;
            if (m.standing)
            {
                V5Vfx.I.PillarTick(view.centre, m.radius);
                V5Vfx.I.PillarClimb(view.centre, m.radius, m.height);
            }
            else V5Vfx.I.PillarGather(view.centre, m.radius, m.warned);
        }

        Moment MomentOf(View view)
        {
            var m = new Moment { radius = view.radius, height = Mathf.Max(1f, columnHeight), standing = view.standing, lasts = view.gone <= 0f };
            m.warned = m.standing || view.warning <= 0f ? 1f : Mathf.Clamp01(view.age / view.warning);
            float stood = m.standing ? view.age - view.warning : 0f;
            m.flare = m.standing ? Sq(1f - Mathf.Clamp01(stood / Mathf.Max(.01f, strikeSettle))) : 0f;
            // The edge's beats so far: their rate climbs evenly from beatFrom to beatTo over the warning, so the count is its
            // integral and needs no memory.
            float t = Mathf.Min(view.age, view.warning);
            float beats = beatFrom * t + (beatTo - beatFrom) * t * t / (2f * Mathf.Max(.01f, view.warning));
            m.beat = .5f + .5f * Mathf.Cos(Mathf.PI * 2f * beats);
            m.pop = m.standing ? 0f : Sq(1f - Mathf.Clamp01(view.age / PopSeconds));
            float left = view.warning + view.life - view.age;
            float dim = m.standing && endFlicker > 0f && left < endFlicker
                ? (1f - Mathf.Max(0f, left) / endFlicker) * (.5f - .5f * Mathf.Cos(left * FlickerRate))
                : 0f;
            m.alive = 1f - .5f * dim;
            m.keep = 1f - view.gone;
            m.early = Mathf.Clamp01(1f - 2.2f * view.gone);
            // Until the strike only a thread, swelling in the last moments; then the column, wider for an instant and breathing.
            // A pillar that is over keeps no width: the column closes to nothing.
            float core = m.standing
                ? m.radius * coreShare * (1f + CoreOvershoot * m.flare) * (1f + CorePulse * Mathf.Sin((time + view.seed) * CorePulseRate))
                : m.radius * Mathf.Lerp(threadShare * (.5f + .5f * m.warned), coreShare * SwellShare, Mathf.Pow(m.warned, SwellPower));
            m.core = Mathf.Min(m.radius, core) * m.keep * m.keep;
            return m;
        }

        /// <summary>Where and how large every part is this frame; the same in both looks, except that only the illustrated art has
        /// the beam and its glow. The edge and the dark disc end on the deadly radius, the veil stands on it, and the ring from the
        /// middle has grown as far as the warning has run.</summary>
        void Place(View view, in Moment m, bool art)
        {
            float radius = m.radius, height = m.height;
            Flat(view.sink, radius);
            Flat(view.edge, radius);
            Flat(view.fill, Mathf.Max(.001f, radius * m.warned));
            Flat(view.halo, radius * 2f * (m.standing ? .55f : .5f + .2f * m.warned));
            Upright(view.veil, radius, height * VeilHeight);
            Upright(view.core, m.core, height * CoreHeight);
            Upright(view.shaft, Mathf.Min(radius, m.core * ShaftWidth), height * ShaftHeight);
            if (art)
            {
                // The beam and its glow are flat sheets through the pillar's axis; they turn about it to face the camera.
                Upright(view.aura, m.core * AuraWidth, height);
                Upright(view.beam, m.core * BeamWidth, height);
                view.aura.transform.localRotation = view.beam.transform.localRotation = facing;
            }

            float climb = height * ClimbTop;
            for (int i = 0; i < Hoops; i++)
            {
                // A hoop shows from the ground up and fades toward the height where it turns back.
                float phase = Mathf.Repeat(time * riseSpeed / climb + (float)i / Hoops + view.seed, 1f);
                var hoop = view.hoops[i].transform;
                hoop.localPosition = new Vector3(0f, phase * climb, 0f);
                hoop.localScale = new Vector3(radius, 1f, radius);
                view.hoopShown[i] = Mathf.Clamp01(phase / .1f) * Mathf.Pow(1f - phase, 1.3f);
            }
        }

        void PaintArt(View view, in Moment m)
        {
            float k = artBrightness, warned = m.warned, flare = m.flare;
            bool standing = m.standing;
            Paint(view.sink, artDark, 1f, (standing ? .8f : Mathf.Lerp(.34f, .62f, warned)) * m.keep);
            // The edge heats from violet toward white as the warning runs, flares white at the strike and stays hot while it kills.
            // Edge and ring belong to a pillar that can still kill, so they go with it.
            Color edge = Color.Lerp(artEdge, artHot, standing ? .3f + .7f * flare : .35f * warned * warned);
            float glare = standing ? (1.6f + 1.5f * flare) * m.alive : (1f + .7f * warned) * (.6f + .4f * m.beat) + 1.2f * m.pop;
            Paint(view.edge, edge, glare * k, m.lasts ? 1f : 0f);
            Paint(view.fill, artFill, (standing ? .5f + .8f * flare : .7f + .5f * warned) * k, m.lasts ? standing ? .22f + .6f * flare : 1f : 0f);
            Paint(view.halo, artFill, k, standing ? (.9f + .1f * flare) * m.alive * m.keep * m.keep : (.1f + .25f * warned) * m.keep);
            // The beam, white-hot inside a wide violet glow; before the strike the two are the thread over the circle.
            Paint(view.aura, artVeil, (standing ? 1.1f : .5f) * k, (standing ? (.5f + .4f * flare) * m.alive : .25f + .3f * warned) * m.keep);
            Paint(view.beam, Color.Lerp(artCore, artHot, flare), (standing ? (2f + 1.2f * flare) * m.alive : .5f + 1.2f * warned * warned) * k, m.keep);
            // From the strike on: the dark shaft, the veil and the column with their noise streaming up, the hoops.
            float turn = view.seed + time * VeilTurn, rise = Mathf.Repeat(time * riseSpeed / VeilStretch, 1f);
            Paint(view.veil, artVeil, (1f + .8f * flare) * k, standing ? (.6f + .4f * flare) * m.alive * m.early : 0f,
                new Vector4(VeilTiles, m.height * VeilHeight / VeilStretch, turn, -rise));
            turn = view.seed * 3f + time * CoreTurn;
            rise = Mathf.Repeat(time * riseSpeed * CoreRise / CoreStretch, 1f);
            Paint(view.core, Color.Lerp(artVeil, artCore, .45f), (1.15f + .6f * flare) * k, standing ? .85f * m.alive * m.early : 0f,
                new Vector4(CoreTiles, m.height * CoreHeight / CoreStretch, turn, -rise));
            Paint(view.shaft, artDark, 1f, standing ? .4f * Mathf.Clamp01(1f - 2f * view.gone) : 0f);
            for (int i = 0; i < Hoops; i++)
                Paint(view.hoops[i], artVeil, 1.5f * m.alive * k, standing ? view.hoopShown[i] * m.early : 0f);
        }

        void PaintWhitebox(View view, in Moment m)
        {
            float warned = m.warned, flare = m.flare;
            bool standing = m.standing;
            Paint(view.sink, boxDark, 1f, (standing ? .6f : Mathf.Lerp(.3f, .5f, warned)) * m.keep);
            // The edge blinks toward white with the beat, harder as the warning runs, and is white at the strike.
            float white = standing ? .2f + .8f * flare : Mathf.Max(m.pop, m.beat * (.25f + .65f * warned));
            Paint(view.edge, Color.Lerp(boxEdge, boxHot, white), 1f, m.lasts ? 1f : 0f);
            Paint(view.fill, boxFill, 1f, m.lasts ? standing ? .45f : .6f : 0f);
            // A plain column for the core (the thread before the strike) inside a plain veil; both flash white at the strike.
            Paint(view.core, Color.Lerp(boxCore, boxHot, flare), 1f, (standing ? .85f * m.alive : .35f + .5f * warned) * m.keep);
            Paint(view.veil, Color.Lerp(boxVeil, boxHot, flare), 1f, standing ? (.3f + .3f * flare) * m.alive * m.early : 0f);
            for (int i = 0; i < Hoops; i++)
                Paint(view.hoops[i], boxHoop, 1f, standing ? .85f * m.alive * view.hoopShown[i] * m.early : 0f);
            // Light only: the foot's glow, the beam with its glow, and the dark shaft that sets them off.
            Paint(view.halo, boxFill, 1f, 0f);
            Paint(view.aura, boxFill, 1f, 0f);
            Paint(view.beam, boxFill, 1f, 0f);
            Paint(view.shaft, boxDark, 1f, 0f);
        }

        /// <summary>Whitebox: the table's small cubes round the circle. While it warns they rest on the ground just inside the edge
        /// and light up one by one, all of them lit as the pillar comes down; then they leap up and climb it in a spiral, shrinking
        /// away where they turn back. Made the first time a view is shown in whitebox.</summary>
        void ShowCubes(View view, in Moment m, bool whitebox)
        {
            if (!whitebox)
            {
                if (view.cubeLooks != null)
                    foreach (var cube in view.cubeLooks) cube.Visible = false;
                return;
            }

            if (view.cubes == null)
            {
                view.cubes = new Transform[Cubes];
                view.cubeLooks = new WhiteboxLook[Cubes];
                for (int i = 0; i < Cubes; i++)
                {
                    view.cubes[i] = Whitebox.Build(CubeLook, view.root, "Cube " + (i + 1));
                    view.cubeLooks[i] = view.cubes[i].GetComponent<WhiteboxLook>();
                }

                view.litCubes = -1;
            }

            int lit = m.standing ? Cubes : Mathf.Clamp(Mathf.FloorToInt(m.warned * Cubes + 1e-4f), 0, Cubes);
            if (lit != view.litCubes)
            {
                view.litCubes = lit;
                for (int i = 0; i < Cubes; i++)
                {
                    var look = view.cubeLooks[i];
                    if (i < lit) look.SetColour(0, Color.Lerp(look.TableColour(0), boxHot, CubeLit));
                    else look.ResetColours();
                }
            }

            float ring = Mathf.Max(m.radius * .5f, m.radius - CubeInset), climb = m.height * ClimbTop;
            float leap = m.standing ? Mathf.Clamp01((view.age - view.warning) / CubeLeap) : 0f;
            for (int i = 0; i < Cubes; i++)
            {
                float turn = view.seed * 360f + i * 360f / Cubes, lift = CubeRest, size = m.keep;
                if (m.standing)
                {
                    float phase = Mathf.Repeat(time * riseSpeed / climb + (float)i / Cubes + view.seed, 1f);
                    turn += time * CubeSpin;
                    lift += phase * climb * leap;
                    size *= Mathf.Clamp01(phase / .08f + (1f - leap)) * Mathf.Clamp01((1f - phase) / .25f);
                }

                bool on = size > .02f;
                view.cubeLooks[i].Visible = on;
                if (!on) continue;
                float a = turn * Mathf.Deg2Rad;
                var cube = view.cubes[i];
                // The table's piece carries its cube above its own origin; the cube's middle goes where it is wanted at any size.
                cube.localPosition = new Vector3(Mathf.Cos(a) * ring, lift - cubeCentre * size, Mathf.Sin(a) * ring);
                cube.localRotation = Quaternion.Euler(0f, -turn, 0f);
                cube.localScale = new Vector3(size, size, size);
            }
        }

        // ───── Placing and painting one part ─────

        static void Flat(MeshRenderer part, float radius) => part.transform.localScale = new Vector3(radius, 1f, radius);

        static void Upright(MeshRenderer part, float radius, float height) => part.transform.localScale = new Vector3(radius, height, radius);

        void Paint(MeshRenderer part, Color colour, float brightness, float alpha) => Paint(part, colour, brightness, alpha, NoTiling);

        /// <summary>Colour, brightness and alpha of one part for this frame; a part with nothing to show is not drawn at all. A raw
        /// vector, not a colour: the authored colours are sRGB and are converted here, the brightness must not be converted with them.</summary>
        void Paint(MeshRenderer part, Color colour, float brightness, float alpha, Vector4 tiling)
        {
            bool on = alpha > .004f && brightness > 0f && part.transform.localScale.x > .002f;
            if (part.enabled != on) part.enabled = on;
            if (!on) return;
            if (linear) colour = colour.linear;
            block.SetVector(BaseColorId, new Vector4(colour.r * brightness, colour.g * brightness, colour.b * brightness, Mathf.Clamp01(alpha)));
            block.SetVector(BaseMapStId, tiling);
            part.SetPropertyBlock(block);
        }

        static float Sq(float x) => x * x;

        /// <summary>The turn that faces an upright sheet to the game's camera, about the vertical only.</summary>
        static Quaternion Facing()
        {
            var game = GameManager.Instance;
            var view = game && game.cam ? game.cam : Camera.main;
            if (!view) return Quaternion.identity;
            Vector3 forward = Query.Flat(view.transform.forward);
            return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
        }

        /// <summary>The views are placed in world coordinates, whatever this object's parent does.</summary>
        void KeepInWorldSpace()
        {
            var t = transform;
            if (t.position != Vector3.zero || t.rotation != Quaternion.identity) t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var parent = t.parent;
            Vector3 scale = Vector3.one;
            if (parent)
            {
                Vector3 p = parent.lossyScale;
                scale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(p.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(p.y)), 1f / Mathf.Max(1e-4f, Mathf.Abs(p.z)));
            }

            if (t.localScale != scale) t.localScale = scale;
        }

        // ───── Making views, shapes and materials ─────

        View Make()
        {
            var view = new View { root = new GameObject("VoidPillar (" + (views.Count + 1) + ")").transform };
            view.root.SetParent(transform, false);
            view.sink = Part(view.root, "Sink", sinkMesh, SinkHeight);
            view.halo = Part(view.root, "Halo", quadMesh, HaloHeight);
            view.fill = Part(view.root, "Clock", fillMesh, FillHeight);
            view.edge = Part(view.root, "Edge", edgeMesh, EdgeHeight);
            view.shaft = Part(view.root, "Shaft", shellMesh, 0f);
            view.veil = Part(view.root, "Veil", shellMesh, 0f);
            view.core = Part(view.root, "Core", shellMesh, 0f);
            view.aura = Part(view.root, "Aura", beamMesh, 0f);
            view.beam = Part(view.root, "Beam", beamMesh, 0f);
            view.hoops = new MeshRenderer[Hoops];
            view.parts = new MeshRenderer[9 + Hoops];
            view.parts[0] = view.sink;
            view.parts[1] = view.halo;
            view.parts[2] = view.fill;
            view.parts[3] = view.edge;
            view.parts[4] = view.shaft;
            view.parts[5] = view.veil;
            view.parts[6] = view.core;
            view.parts[7] = view.aura;
            view.parts[8] = view.beam;
            for (int i = 0; i < Hoops; i++) view.parts[9 + i] = view.hoops[i] = Part(view.root, "Hoop " + (i + 1), hoopMesh, 0f);
            views.Add(view);
            return view;
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var part = go.AddComponent<MeshRenderer>();
            part.shadowCastingMode = ShadowCastingMode.Off;
            part.receiveShadows = false;
            part.lightProbeUsage = LightProbeUsage.Off;
            part.reflectionProbeUsage = ReflectionProbeUsage.Off;
            part.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            part.enabled = false;
            return part;
        }

        /// <summary>The look's materials and drawing order on the view's parts. The dark disc and the dark shaft are plain in both
        /// looks, since darkness cannot be added.
        /// Illustrated: the lit parts are added to the picture, so their order among themselves and among the V5 layer's bright
        /// particles does not matter; what matters is that none shares an order with a blended layer, or the two would swap from
        /// frame to frame. The ground parts go over the V5 layer's dark ground marks (order -2) with its ground lights (-1), under
        /// everything that stands on the ground; the column's light goes with the layer's brightest particles (5), over its smoke
        /// and fire; the dark disc lies under all ground marks and the dark shaft under all light.
        /// Whitebox: every part is blended, so each has its own order: disc, growing ring, edge, then veil, core and hoops.</summary>
        static void Dress(View view, bool art)
        {
            view.dressed = true;
            view.art = art;
            var lit = art ? light : plain;
            view.sink.sharedMaterial = view.shaft.sharedMaterial = plain;
            view.fill.sharedMaterial = view.edge.sharedMaterial = lit;
            view.veil.sharedMaterial = view.core.sharedMaterial = art ? streaks : plain;
            view.aura.sharedMaterial = view.beam.sharedMaterial = soft;
            view.halo.sharedMaterial = halo;
            view.sink.sortingOrder = -3;
            view.fill.sortingOrder = art ? -1 : -2;
            view.halo.sortingOrder = view.edge.sortingOrder = -1;
            view.shaft.sortingOrder = 1;
            view.veil.sortingOrder = art ? 5 : 2;
            view.core.sortingOrder = art ? 5 : 3;
            view.aura.sortingOrder = view.beam.sortingOrder = 5;
            for (int i = 0; i < Hoops; i++)
            {
                view.hoops[i].sharedMaterial = lit;
                view.hoops[i].sortingOrder = art ? 5 : 4;
            }
        }

        /// <summary>The shared shapes and materials, made on first use and again if a scene change took them. False without the V5
        /// effect shader (it is loaded from Resources, so builds keep it): nothing can be drawn then.</summary>
        static bool Ready()
        {
            if (plain && light && streaks && soft && halo && sinkMesh && fillMesh && edgeMesh && hoopMesh && shellMesh && beamMesh && quadMesh) return true;
            var shader = Resources.Load<Shader>("V5/V5Particle");
            if (!shader) return false;
            block ??= new MaterialPropertyBlock();
            if (!plain) plain = Look(shader, "VoidPillar Plain", false, null, 1f, 0f);
            if (!light) light = Look(shader, "VoidPillar Light", true, null, 1f, 0f);
            if (!streaks) streaks = Look(shader, "VoidPillar Streaks", true, V5Vfx.Tex("noise"), StreakBase, StreakGlow);
            if (!soft) soft = Look(shader, "VoidPillar Beam", true, V5Vfx.Tex("softline"), 1f, 0f);
            if (!halo) halo = Look(shader, "VoidPillar Halo", true, V5Vfx.Tex("glow"), 1.2f, 0f);
            if (!sinkMesh) sinkMesh = Rings("VoidPillar Sink", SinkRows, SinkAlpha);
            if (!fillMesh) fillMesh = Rings("VoidPillar Clock", FillRows, FillAlpha);
            if (!edgeMesh) edgeMesh = Rings("VoidPillar Edge", EdgeRows, EdgeAlpha);
            if (!hoopMesh) hoopMesh = Rings("VoidPillar Hoop", HoopRows, HoopAlpha);
            if (!shellMesh) shellMesh = Shell("VoidPillar Column", ShellRows, ShellAlpha);
            if (!beamMesh) beamMesh = Sheet("VoidPillar Beam", BeamRows, BeamAlpha);
            if (!quadMesh) quadMesh = Quad("VoidPillar Halo");
            // Whitebox: the table's void colours. A colour changed in the table changes here with it.
            boxDark = Whitebox.Colour("black");
            boxEdge = Whitebox.Colour("core");
            boxHot = Whitebox.Colour(Whitebox.MarkColour);
            boxFill = Whitebox.Colour("dark");
            boxVeil = Whitebox.Colour("dark");
            boxCore = Whitebox.Colour("prophet");
            boxHoop = Whitebox.Colour("gate");
            var cube = Whitebox.PartsOf(CubeLook);
            cubeCentre = cube.Count > 0 ? cube[0].position.y : .5f;
            return true;
        }

        /// <summary>A material of the V5 effect shader (unlit, vertex colour times texture): added to what is behind it, or blended
        /// over it. Without a texture it draws the mesh's own colours.</summary>
        static Material Look(Shader shader, string name, bool additive, Texture texture, float intensity, float glow)
        {
            var made = new Material(shader) { name = name };
            if (texture) made.SetTexture("_BaseMap", texture);
            made.SetFloat("_Intensity", intensity);
            made.SetFloat("_Glow", glow);
            made.SetFloat("_Bias", 0f);
            made.SetFloat("_LumaAlpha", 0f);
            made.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            made.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            made.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            return made;
        }

        /// <summary>Rings in the ground plane round the origin: one row of points at each radius, white with the row's alpha. A
        /// last radius of 0 closes the shape into a disc.</summary>
        static Mesh Rings(string name, float[] radii, float[] alphas)
        {
            int rows = radii.Length;
            var vertices = new Vector3[(Segments + 1) * rows];
            var colours = new Color32[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for (int s = 0; s <= Segments; s++)
            {
                float a = s * Mathf.PI * 2f / Segments, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                for (int r = 0; r < rows; r++)
                {
                    int i = s * rows + r;
                    vertices[i] = new Vector3(cos * radii[r], 0f, sin * radii[r]);
                    colours[i] = White(alphas[r]);
                    uv[i] = new Vector2((float)s / Segments, radii[r]);
                }
            }

            return Strips(name, vertices, colours, uv, Segments, rows);
        }

        /// <summary>An open column of radius 1 and height 1 standing on the origin: one row of points at each height, white with
        /// the row's alpha. Its texture runs once round it and once up it.</summary>
        static Mesh Shell(string name, float[] heights, float[] alphas)
        {
            int rows = heights.Length;
            var vertices = new Vector3[(Sides + 1) * rows];
            var colours = new Color32[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for (int s = 0; s <= Sides; s++)
            {
                float a = s * Mathf.PI * 2f / Sides, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                for (int r = 0; r < rows; r++)
                {
                    int i = s * rows + r;
                    vertices[i] = new Vector3(cos, heights[r], sin);
                    colours[i] = White(alphas[r]);
                    uv[i] = new Vector2((float)s / Sides, heights[r]);
                }
            }

            return Strips(name, vertices, colours, uv, Sides, rows);
        }

        /// <summary>An upright sheet of height 1 through the origin, from -1 to 1 across: one row of points at each height, white
        /// with the row's alpha. The soft line's picture lies across it (the picture's profile runs along its v).</summary>
        static Mesh Sheet(string name, float[] heights, float[] alphas)
        {
            int rows = heights.Length;
            var vertices = new Vector3[2 * rows];
            var colours = new Color32[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for (int s = 0; s < 2; s++)
            for (int r = 0; r < rows; r++)
            {
                int i = s * rows + r;
                vertices[i] = new Vector3(s * 2f - 1f, heights[r], 0f);
                colours[i] = White(alphas[r]);
                uv[i] = new Vector2(.5f, s);
            }

            return Strips(name, vertices, colours, uv, 1, rows);
        }

        static Color32 White(float alpha) => new(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));

        /// <summary>Joins columns of points (each rows long) into a mesh; both faces are drawn, so the winding does not matter.</summary>
        static Mesh Strips(string name, Vector3[] vertices, Color32[] colours, Vector2[] uv, int columns, int rows)
        {
            var triangles = new int[columns * (rows - 1) * 6];
            int t = 0;
            for (int s = 0; s < columns; s++)
            for (int r = 0; r < rows - 1; r++)
            {
                int here = s * rows + r, there = (s + 1) * rows + r;
                triangles[t++] = here;
                triangles[t++] = there;
                triangles[t++] = here + 1;
                triangles[t++] = there;
                triangles[t++] = there + 1;
                triangles[t++] = here + 1;
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A flat square one cell wide on the ground, for the picture of the light at the pillar's foot.</summary>
        static Mesh Quad(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f), new Vector3(.5f, 0f, -.5f) });
            mesh.SetColors(new[] { White(1f), White(1f), White(1f), White(1f) });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ───── For automated checks ─────

        /// <summary>What one view draws now, measured from its renderers (so a wrong shape or a wrong size shows).</summary>
        public readonly struct Reading
        {
            /// <summary>The circle's edge, the ring growing from the middle and the veil of the column are drawn.</summary>
            public readonly bool EdgeShown, ClockShown, ColumnShown;
            /// <summary>The view's pillar is over and the view is thinning out.</summary>
            public readonly bool ThinningOut;
            /// <summary>Outer radius of the circle's edge, of the ring growing from the middle and of the column's veil, and the
            /// radius of the column inside the veil (of the thread before the strike), in cells; 0 for a part that is not drawn.</summary>
            public readonly float EdgeRadius, ClockRadius, ColumnRadius, CoreRadius;

            public Reading(bool edgeShown, bool clockShown, bool columnShown, bool thinningOut, float edgeRadius, float clockRadius, float columnRadius, float coreRadius)
            {
                EdgeShown = edgeShown;
                ClockShown = clockShown;
                ColumnShown = columnShown;
                ThinningOut = thinningOut;
                EdgeRadius = edgeRadius;
                ClockRadius = clockRadius;
                ColumnRadius = columnRadius;
                CoreRadius = coreRadius;
            }
        }

        /// <summary>For automated checks: what the view at centre (a pillar's, or one thinning out there) draws now.</summary>
        public bool TryRead(Vector3 centre, out Reading reading)
        {
            centre = Query.Flat(centre);
            // A pillar's own view first; one thinning out on the same spot only when no pillar stands there.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < views.Count; i++)
                {
                    var view = views[i];
                    if (!view.shown || view.bound != (pass == 0) || Query.Flat(view.centre) != centre) continue;
                    // The column inside the veil is the streaming (or plain) column where that shows, else the beam, which is a
                    // sheet turned to the camera: its width is its scale.
                    float core = view.core.enabled ? Radius(view.core) : view.beam.enabled ? view.beam.transform.lossyScale.x / BeamWidth : 0f;
                    reading = new Reading(view.edge.enabled, view.fill.enabled, view.veil.enabled, !view.bound, Radius(view.edge), Radius(view.fill),
                        Radius(view.veil), core);
                    return true;
                }

            reading = default;
            return false;
        }

        /// <summary>Half the width of what a round part draws, from its renderer's own bounds.</summary>
        static float Radius(MeshRenderer part) => part.enabled ? part.bounds.extents.x : 0f;
    }
}
