using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Marks of the Nash Prophet's 2026-10-25 moves in the void trial, made at runtime so no scene objects are needed: its void
    /// pillars through V2VoidPillarFx (each one's circle, clock, strike and standing column); and, as flat line renderers like
    /// V2BossGroundMarks, the gravity pull as a tether whose dashes run from the player into the Prophet and, while a summon keeps it
    /// invulnerable, a ward ring round it with a line to every summon the player can see (a cloaked Shade stays hidden). WorldMap adds
    /// it to the Prophet when a trial starts.</summary>
    public sealed class V2ProphetTrialMarks : MonoBehaviour
    {
        [Tooltip("Dashes of the gravity pull's tether between the player and the Prophet.")]
        public Color pullColor = new(.7f, .45f, 1f, .85f);
        [Min(1), Tooltip("Dashes along the pull tether.")] public int pullDashes = 8;
        [Min(0f), Tooltip("Cells per second the dashes run toward the Prophet.")] public float pullDashSpeed = 6f;
        [Tooltip("Ward ring round the Prophet and its lines to the summons that keep it invulnerable.")]
        public Color wardColor = new(.55f, .95f, 1f, .8f);
        [Min(0f), Tooltip("Cells between the Prophet's body and its ward ring.")] public float wardGap = .6f;
        [Min(0f), Tooltip("Seconds the ward ring flashes wider after it turns a hit away.")] public float wardFlashSeconds = .2f;
        [Min(1f), Tooltip("Width of the ward ring while it flashes, as a multiple of the line width.")] public float wardFlashWidth = 3f;
        [Min(.01f), Tooltip("Width of the tether dashes and the ward lines, cells.")] public float lineWidth = .14f;
        const int Segments = 72;
        // Height of the ward and the tether above the ground: over the pillars' ground marks (V2VoidPillarFx), so nothing flickers
        // where they cross.
        const float LineHeight = .065f;

        Enemy enemy;
        Material material;
        int sortingLayer, sortingOrder, seenBlocks;
        float flashLeft;
        LineRenderer wardRing;
        V2VoidPillarFx pillarFx;
        readonly List<LineRenderer> dashes = new(), wardLines = new();

        /// <summary>The view this Prophet's void pillars are drawn with; null until its first pillars in a trial.</summary>
        public V2VoidPillarFx Pillars => pillarFx;

        void LateUpdate()
        {
            if (!enemy) enemy = GetComponent<Enemy>();
            var world = WorldMap.Active;
            bool show = enemy && enemy.IsAlive && enemy.definition && enemy.definition.kind == EnemyKind.Hollow && world && world.TrialActive;
            // Before the lines' material is looked for: a pillar kills, so it is drawn whether or not the lines can be.
            DrawPillars(show);
            if (show && !material) FindMaterial();
            if (!material) show = false;
            DrawPull(show);
            DrawWard(show);
        }

        void OnDisable()
        {
            foreach (var list in new[] { dashes, wardLines })
                foreach (var line in list) if (line) line.enabled = false;
            if (wardRing) wardRing.enabled = false;
            if (pillarFx) pillarFx.Clear();
        }

        void OnDestroy()
        {
            if (pillarFx) Destroy(pillarFx.gameObject);
        }

        /// <summary>The sprite material the enemy's own warning uses: it takes vertex colours (as V2BossGroundMarks does).</summary>
        void FindMaterial()
        {
            var telegraph = GetComponent<V2EnemyTelegraphPresentation>();
            SpriteRenderer source = telegraph != null && telegraph.illustration != null ? telegraph.illustration : GetComponentInChildren<SpriteRenderer>(true);
            if (source == null) return;
            material = source.sharedMaterial;
            sortingLayer = source.sortingLayerID;
            sortingOrder = source.sortingOrder - 1; // under the attack warnings
        }

        /// <summary>The trial's pillars through their own view. It stands at the scene's root, not under the Prophet: the body turns
        /// every frame, and whatever recolours or hides the body's renderers would reach the view's too. With the trial over or the
        /// Prophet down the pillars go at once.</summary>
        void DrawPillars(bool show)
        {
            if (!show)
            {
                if (pillarFx) pillarFx.Clear();
                return;
            }

            if (!pillarFx)
            {
                if (enemy.VoidPillars.Count == 0) return;
                pillarFx = V2VoidPillarFx.Create(null, "ProphetVoidPillars");
            }

            pillarFx.Draw(enemy.VoidPillars);
        }

        void DrawPull(bool show)
        {
            var player = GameManager.Instance ? GameManager.Instance.player : null;
            Vector3 from = player ? Query.Flat(player.Position) : Vector3.zero, along = show ? Query.Flat(enemy.Position) - from : Vector3.zero;
            float length = along.magnitude - (show ? enemy.Radius : 0f);
            bool on = show && enemy.GravityPulling && player && player.IsAlive && length > 0f;
            // Lines are only made once the material is known (show), so none is left without it.
            if (on) while (dashes.Count < pullDashes) dashes.Add(Make("GravityPullDash (" + (dashes.Count + 1) + ")", 2, false));
            Vector3 direction = on ? along.normalized : Vector3.zero;
            float spacing = on ? length / pullDashes : 0f;
            float shift = on ? Mathf.Repeat(Time.time * pullDashSpeed, spacing) : 0f;
            for (int i = 0; i < dashes.Count; i++)
            {
                var dash = dashes[i];
                // Each dash covers half of its slot and the slots run toward the Prophet, so the tether reads as drawn in.
                float start = i * spacing + shift;
                dash.enabled = on && i < pullDashes && start < length;
                if (!dash.enabled) continue;
                float end = Mathf.Min(length, start + spacing * .5f);
                dash.SetPosition(0, from + direction * start + Vector3.up * LineHeight);
                dash.SetPosition(1, from + direction * end + Vector3.up * LineHeight);
                dash.widthMultiplier = lineWidth;
                dash.startColor = dash.endColor = pullColor;
                Flatten(dash);
            }
        }

        void DrawWard(bool show)
        {
            bool on = show && enemy.ProphetWarded;
            if (on && !wardRing) wardRing = Make("ProphetWardRing", Segments, true);
            if (enemy && enemy.WardBlockSequence != seenBlocks)
            {
                seenBlocks = enemy.WardBlockSequence;
                flashLeft = wardFlashSeconds;
            }

            flashLeft = Mathf.Max(0f, flashLeft - Time.deltaTime);
            if (wardRing) wardRing.enabled = on;
            int lines = 0;
            var waves = on && GameManager.Instance ? GameManager.Instance.waves : null;
            if (on)
            {
                Vector3 centre = Query.Flat(enemy.Position);
                Circle(wardRing, centre, enemy.Radius + wardGap, LineHeight);
                wardRing.widthMultiplier = lineWidth * (flashLeft > 0f ? wardFlashWidth : 1f);
                wardRing.startColor = wardRing.endColor = wardColor;
                if (waves)
                    foreach (var summon in waves.Alive)
                    {
                        if (!summon || !summon.WardsProphet(enemy) || summon.Cloaked) continue;
                        if (lines == wardLines.Count) wardLines.Add(Make("ProphetWardLine (" + (lines + 1) + ")", 2, false));
                        var line = wardLines[lines++];
                        line.SetPosition(0, centre + Vector3.up * LineHeight);
                        line.SetPosition(1, Query.Flat(summon.Position) + Vector3.up * LineHeight);
                        line.widthMultiplier = lineWidth;
                        line.startColor = line.endColor = wardColor;
                        Flatten(line);
                        line.enabled = true;
                    }
            }

            for (int i = lines; i < wardLines.Count; i++) wardLines[i].enabled = false;
        }

        /// <summary>A line that lies flat on the ground (the tether's dashes, the ward's ring and lines).</summary>
        LineRenderer Make(string name, int points, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points;
            line.sharedMaterial = material;
            line.sortingLayerID = sortingLayer;
            line.sortingOrder = sortingOrder;
            line.alignment = LineAlignment.TransformZ; // flat on the ground, not turned toward the camera
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 0;
            line.enabled = false;
            return line;
        }

        static void Circle(LineRenderer line, Vector3 centre, float radius, float height)
        {
            radius = Mathf.Max(.01f, radius);
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                line.SetPosition(i, new Vector3(centre.x + Mathf.Cos(a) * radius, height, centre.z + Mathf.Sin(a) * radius));
            }

            Flatten(line);
        }

        /// <summary>The body turns every frame; keep each ribbon facing straight up.</summary>
        static void Flatten(LineRenderer line) => line.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
    }
}
