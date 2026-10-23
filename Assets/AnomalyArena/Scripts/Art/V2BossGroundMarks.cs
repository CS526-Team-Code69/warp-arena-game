using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed class V2BossGroundMarks : MonoBehaviour
    {
        [Tooltip("Push-out band along the platform edge (the outer two cells that slide you off).")]
        public Color edgeBandColor = new(0f, 0f, .02f, .45f);
        [Tooltip("Dashed line showing where the platform edge is shrinking to.")]
        public Color goalLineColor = new(.85f, .6f, 1f, .85f);
        [Tooltip("Rim of a prophecy landing circle, at full size from the start.")]
        public Color landingRimColor = new(.75f, .45f, 1f, .95f);
        [Tooltip("Fill of a prophecy landing circle; it closes from the rim to the centre as the hole nears.")]
        public Color landingFillColor = new(.06f, .02f, .12f, .85f);
        [Min(.01f), Tooltip("Width of the dashed edge and landing rims, cells.")] public float lineWidth = .14f;
        const int Segments = 72, Dashes = 40;

        Enemy enemy;
        Material material;
        int sortingLayer, sortingOrder;
        LineRenderer edgeBand;
        readonly List<LineRenderer> dashes = new(), rims = new(), fills = new();

        void LateUpdate()
        {
            if (!enemy) enemy = GetComponent<Enemy>();
            var world = WorldMap.Active;
            bool trial = enemy && enemy.IsAlive && enemy.definition && enemy.definition.kind == EnemyKind.Hollow && world &&
                         world.TrialActive && world.trialPlatform;
            if (trial && !material) FindMaterial();
            if (!material) trial = false;
            DrawPlatform(trial, world);
            DrawLandingZones(trial);
        }

        void OnDisable()
        {
            if (edgeBand) edgeBand.enabled = false;
            foreach (var line in dashes) if (line) line.enabled = false;
            foreach (var line in rims) if (line) line.enabled = false;
            foreach (var line in fills) if (line) line.enabled = false;
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

        void DrawPlatform(bool show, WorldMap world)
        {
            if (!show)
            {
                if (edgeBand) edgeBand.enabled = false;
                foreach (var line in dashes) if (line) line.enabled = false;
                return;
            }

            Vector3 centre = Query.Flat(world.trialPlatform.position);
            float radius = world.trialRadius;
            if (!edgeBand) edgeBand = Make("TrialEdgeBand", Segments, true);
            Circle(edgeBand, centre, radius - 1f, .045f);
            edgeBand.widthMultiplier = 2f;
            edgeBand.startColor = edgeBand.endColor = edgeBandColor;
            edgeBand.enabled = true;

            float goal = enemy.PlatformGoal;
            bool shrinking = radius - goal > .05f;
            while (dashes.Count < Dashes) dashes.Add(Make("TrialEdgeDash", 2, false));
            for (int i = 0; i < Dashes; i++)
            {
                var dash = dashes[i];
                dash.enabled = shrinking;
                if (!shrinking) continue;

                float a0 = i * Mathf.PI * 2f / Dashes, a1 = a0 + Mathf.PI / Dashes;
                dash.SetPosition(0, centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * goal + Vector3.up * .06f);
                dash.SetPosition(1, centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * goal + Vector3.up * .06f);
                dash.widthMultiplier = lineWidth;
                dash.startColor = dash.endColor = goalLineColor;
                Flatten(dash);
            }
        }

        void DrawLandingZones(bool show)
        {
            var zones = show ? enemy.LandingZones : null;
            int count = zones?.Count ?? 0;
            while (rims.Count < count) rims.Add(Make("ProphecyRim", Segments, true));
            while (fills.Count < count) fills.Add(Make("ProphecyFill", Segments, true));
            for (int i = 0; i < rims.Count; i++)
            {
                bool on = i < count;
                rims[i].enabled = on;
                fills[i].enabled = on;
                if (!on) continue;
                var zone = zones[i];
                Circle(rims[i], zone.centre, zone.radius, .055f);
                rims[i].widthMultiplier = lineWidth;
                rims[i].startColor = rims[i].endColor = landingRimColor;

                float band = Mathf.Max(.02f, zone.radius * zone.Fill01);
                Circle(fills[i], zone.centre, zone.radius - band * .5f, .05f);
                fills[i].widthMultiplier = band;
                fills[i].startColor = fills[i].endColor = landingFillColor;
            }
        }

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
            line.alignment = LineAlignment.TransformZ;                                                    
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
