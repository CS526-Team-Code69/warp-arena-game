using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed class V2ProphetTrialMarks : MonoBehaviour
    {
        [Tooltip("Rim of a void pillar's warning circle, at full size from the start.")]
        public Color pillarRimColor = new(.75f, .45f, 1f, .95f);
        [Tooltip("Fill of a warning circle; it closes from the rim to the centre as the pillar nears.")]
        public Color pillarFillColor = new(.35f, .1f, .5f, .55f);
        [Tooltip("Disc of a standing pillar: a player whose centre is inside it dies.")]
        public Color pillarColor = new(.9f, .6f, 1f, .7f);
        [Tooltip("Beam of a standing pillar at its foot; it fades out toward the top.")]
        public Color pillarBeamColor = new(.95f, .85f, 1f, .6f);
        [Min(0f), Tooltip("Height of a standing pillar's beam, cells.")] public float pillarBeamHeight = 7f;
        [Tooltip("Dashes of the gravity pull's tether between the player and the Prophet.")]
        public Color pullColor = new(.7f, .45f, 1f, .85f);
        [Min(1), Tooltip("Dashes along the pull tether.")] public int pullDashes = 8;
        [Min(0f), Tooltip("Cells per second the dashes run toward the Prophet.")] public float pullDashSpeed = 6f;
        [Tooltip("Ward ring round the Prophet and its lines to the summons that keep it invulnerable.")]
        public Color wardColor = new(.55f, .95f, 1f, .8f);
        [Min(0f), Tooltip("Cells between the Prophet's body and its ward ring.")] public float wardGap = .6f;
        [Min(0f), Tooltip("Seconds the ward ring flashes wider after it turns a hit away.")] public float wardFlashSeconds = .2f;
        [Min(1f), Tooltip("Width of the ward ring while it flashes, as a multiple of the line width.")] public float wardFlashWidth = 3f;
        [Min(.01f), Tooltip("Width of rims, tether dashes and ward lines, cells.")] public float lineWidth = .14f;
        const int Segments = 72;

        const float FillHeight = .05f, RimHeight = .055f, LineHeight = .065f;

        Enemy enemy;
        Material material;
        int sortingLayer, sortingOrder, seenBlocks;
        float flashLeft;
        LineRenderer wardRing;
        readonly List<LineRenderer> rims = new(), fills = new(), beams = new(), dashes = new(), wardLines = new();

        void LateUpdate()
        {
            if (!enemy) enemy = GetComponent<Enemy>();
            var world = WorldMap.Active;
            bool show = enemy && enemy.IsAlive && enemy.definition && enemy.definition.kind == EnemyKind.Hollow && world && world.TrialActive;
            if (show && !material) FindMaterial();
            if (!material) show = false;
            DrawPillars(show);
            DrawPull(show);
            DrawWard(show);
        }

        void OnDisable()
        {
            foreach (var list in new[] { rims, fills, beams, dashes, wardLines })
                foreach (var line in list) if (line) line.enabled = false;
            if (wardRing) wardRing.enabled = false;
        }

        void FindMaterial()
        {
            var telegraph = GetComponent<V2EnemyTelegraphPresentation>();
            SpriteRenderer source = telegraph != null && telegraph.illustration != null ? telegraph.illustration : GetComponentInChildren<SpriteRenderer>(true);
            if (source == null) return;
            material = source.sharedMaterial;
            sortingLayer = source.sortingLayerID;
            sortingOrder = source.sortingOrder - 1;                             
        }

        void DrawPillars(bool show)
        {
            var pillars = show ? enemy.VoidPillars : null;
            int count = pillars?.Count ?? 0;
            while (rims.Count < count)
            {
                int number = rims.Count + 1;
                rims.Add(Make("VoidPillarRim (" + number + ")", Segments, true, true));
                fills.Add(Make("VoidPillarFill (" + number + ")", Segments, true, true));
                beams.Add(Make("VoidPillarBeam (" + number + ")", 2, false, false));
            }

            for (int i = 0; i < rims.Count; i++)
            {
                bool on = i < count;
                bool standing = on && pillars[i].Standing;
                rims[i].enabled = fills[i].enabled = on;
                beams[i].enabled = standing;
                if (!on) continue;
                var pillar = pillars[i];
                Circle(rims[i], pillar.centre, pillar.radius, RimHeight);
                rims[i].widthMultiplier = lineWidth;
                rims[i].startColor = rims[i].endColor = pillarRimColor;

                float band = Mathf.Max(.02f, pillar.radius * pillar.Warning01);
                Circle(fills[i], pillar.centre, pillar.radius - band * .5f, FillHeight);
                fills[i].widthMultiplier = band;
                fills[i].startColor = fills[i].endColor = standing ? pillarColor : pillarFillColor;
                if (V5Vfx.On)
                {

                    fills[i].sharedMaterial = V5Vfx.I.TrailMaterial;
                    beams[i].sharedMaterial = V5Vfx.I.TrailMaterial;
                    fills[i].startColor = fills[i].endColor = standing ? new Color(.42f, .16f, .85f, .55f) : new Color(.3f, .08f, .55f, .4f);
                    if (standing)
                    {
                        V5Vfx.I.PillarTick(pillar.centre, pillar.radius);
                        beams[i].startColor = new Color(.6f, .34f, 1f, .5f);
                        beams[i].endColor = new Color(.6f, .34f, 1f, 0f);
                    }
                }
                if (!standing) continue;
                var beam = beams[i];
                beam.SetPosition(0, new Vector3(pillar.centre.x, FillHeight, pillar.centre.z));
                beam.SetPosition(1, new Vector3(pillar.centre.x, pillarBeamHeight, pillar.centre.z));
                beam.widthMultiplier = pillar.radius * 2f;                         
                beam.startColor = pillarBeamColor;
                beam.endColor = new Color(pillarBeamColor.r, pillarBeamColor.g, pillarBeamColor.b, 0f);
            }
        }

        void DrawPull(bool show)
        {
            var player = GameManager.Instance ? GameManager.Instance.player : null;
            Vector3 from = player ? Query.Flat(player.Position) : Vector3.zero, along = show ? Query.Flat(enemy.Position) - from : Vector3.zero;
            float length = along.magnitude - (show ? enemy.Radius : 0f);
            bool on = show && enemy.GravityPulling && player && player.IsAlive && length > 0f;

            if (on) while (dashes.Count < pullDashes) dashes.Add(Make("GravityPullDash (" + (dashes.Count + 1) + ")", 2, false, true));
            Vector3 direction = on ? along.normalized : Vector3.zero;
            float spacing = on ? length / pullDashes : 0f;
            float shift = on ? Mathf.Repeat(Time.time * pullDashSpeed, spacing) : 0f;
            for (int i = 0; i < dashes.Count; i++)
            {
                var dash = dashes[i];

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
            if (on && !wardRing) wardRing = Make("ProphetWardRing", Segments, true, true);
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
                        if (lines == wardLines.Count) wardLines.Add(Make("ProphetWardLine (" + (lines + 1) + ")", 2, false, true));
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

        LineRenderer Make(string name, int points, bool loop, bool flat)
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
            line.alignment = flat ? LineAlignment.TransformZ : LineAlignment.View;
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

        static void Flatten(LineRenderer line) => line.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
    }
}
