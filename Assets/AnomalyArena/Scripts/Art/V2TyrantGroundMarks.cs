using UnityEngine;

namespace AnomalyArena
{

    public sealed class V2TyrantGroundMarks : MonoBehaviour
    {
        [Tooltip("Rims of the charge's landing circle, the Ground Pound's rectangle and the meteor's impact circle.")]
        public Color rimColor = new(1f, .55f, .2f, .95f);
        [Tooltip("Fill of the meteor's impact circle (it closes from the rim to the centre as the meteor nears) and the shockwave's band.")]
        public Color fillColor = new(1f, .3f, .1f, .5f);
        [Tooltip("The tail crossing the Tail Sweep's fan, and the fan's rim while it does.")]
        public Color tailColor = new(1f, .85f, .4f, .95f);
        [Tooltip("Ring of the Astral Guard's reach while it channels.")]
        public Color astralColor = new(.6f, .75f, 1f, .9f);
        [Tooltip("The meteor in the air.")]
        public Color meteorColor = new(1f, .7f, .35f, 1f);
        [Min(.01f), Tooltip("Width of the rims, cells.")] public float lineWidth = .14f;
        [Min(.05f), Tooltip("Diameter of the meteor in the air, cells.")] public float meteorSize = 1.4f;
        const int Segments = 72;

        Enemy enemy;
        Material material;
        int sortingLayer, sortingOrder;
        LineRenderer landing, tail, fanRim, pound, poundBand, impactRim, impactFill, meteor, astral;

        void LateUpdate()
        {
            if (!enemy) enemy = GetComponent<Enemy>();
            bool on = enemy && enemy.IsAlive && enemy.definition && enemy.definition.kind == EnemyKind.Tyrant;
            if (on && !material) FindMaterial();
            if (!material) on = false;
            DrawLanding(on && enemy.RushLandingShown);
            DrawSweep(on && enemy.TailSweeping);
            DrawPound(on && enemy.PoundShown);
            DrawMeteor(on && enemy.MeteorFlying);
            DrawAstral(on && enemy.AstralChanneling);
        }

        void OnDisable()
        {
            foreach (var line in new[] { landing, tail, fanRim, pound, poundBand, impactRim, impactFill, meteor, astral })
                if (line)
                    line.enabled = false;
        }

        void FindMaterial()
        {
            var telegraph = GetComponentInChildren<V2EnemyTelegraphPresentation>(true);
            SpriteRenderer source = telegraph && telegraph.illustration ? telegraph.illustration : GetComponentInChildren<SpriteRenderer>(true);
            if (!source) return;
            material = source.sharedMaterial;
            sortingLayer = source.sortingLayerID;
            sortingOrder = source.sortingOrder;
        }

        void DrawLanding(bool show)
        {
            if (!Show(ref landing, "TyrantLandingRim", Segments, true, show)) return;
            Circle(landing, enemy.RushLanding, enemy.RushSlamReach, .055f);
            Style(landing, lineWidth, rimColor);
        }

        void DrawSweep(bool show)
        {
            bool rim = Show(ref fanRim, "TyrantSweepRim", Segments, false, show);
            if (!Show(ref tail, "TyrantTail", 2, false, show) || !rim) return;
            Vector3 centre = Query.Flat(enemy.Position), facing = Query.Flat(enemy.TelegraphFacing).normalized;
            float reach = enemy.TailReach, half = enemy.SweepArc * .5f;
            for (int i = 0; i < Segments; i++)
            {
                float angle = Mathf.Lerp(-half, half, i / (Segments - 1f));
                fanRim.SetPosition(i, centre + Quaternion.Euler(0f, angle, 0f) * facing * reach + Vector3.up * .06f);
            }

            Flatten(fanRim);
            Style(fanRim, lineWidth, tailColor);
            tail.SetPosition(0, centre + Vector3.up * .07f);
            tail.SetPosition(1, centre + enemy.TailDirection * reach + Vector3.up * .07f);
            Flatten(tail);
            Style(tail, Mathf.Max(lineWidth, enemy.TailHalfWidth * 2f), tailColor);
        }

        void DrawPound(bool show)
        {
            bool outline = Show(ref pound, "TyrantPoundRect", 4, true, show);
            bool band = Show(ref poundBand, "TyrantPoundWave", 2, false, show && enemy.PoundStep >= 0);
            if (!outline) return;
            Vector3 origin = enemy.PoundOrigin, ahead = enemy.PoundFacing, side = Vector3.Cross(Vector3.up, ahead) * (enemy.PoundWidth * .5f);
            Vector3 lift = Vector3.up * .06f, far = ahead * enemy.PoundLength;
            pound.SetPosition(0, origin - side + lift);
            pound.SetPosition(1, origin + side + lift);
            pound.SetPosition(2, origin + side + far + lift);
            pound.SetPosition(3, origin - side + far + lift);
            Flatten(pound);
            Style(pound, lineWidth, rimColor);
            if (!band) return;

            float step = enemy.PoundLength / enemy.PoundSteps, middle = step * (enemy.PoundStep + .5f);
            poundBand.SetPosition(0, origin - side + ahead * middle + lift);
            poundBand.SetPosition(1, origin + side + ahead * middle + lift);
            Flatten(poundBand);
            Style(poundBand, Mathf.Max(lineWidth, step), fillColor);
        }

        void DrawMeteor(bool show)
        {
            bool rim = Show(ref impactRim, "TyrantMeteorRim", Segments, true, show);
            bool fill = Show(ref impactFill, "TyrantMeteorFill", Segments, true, show);
            bool body = Show(ref meteor, "TyrantMeteor", Segments, true, show);
            if (!rim || !fill || !body) return;
            float reach = enemy.MeteorReach, filled = Mathf.Max(.02f, reach * enemy.MeteorFill01);
            Circle(impactRim, enemy.MeteorTarget, reach, .055f);
            Style(impactRim, lineWidth, rimColor);

            Circle(impactFill, enemy.MeteorTarget, reach - filled * .5f, .05f);
            Style(impactFill, filled, fillColor);
            DrawMeteorBody(enemy.MeteorPosition);
        }

        void DrawMeteorBody(Vector3 at)
        {
            var cam = GameManager.Instance ? GameManager.Instance.cam : null;
            if (!cam)
            {
                meteor.enabled = false;
                return;
            }

            float r = meteorSize * .25f;
            Vector3 right = cam.transform.right * r, up = cam.transform.up * r;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                meteor.SetPosition(i, at + right * Mathf.Cos(a) + up * Mathf.Sin(a));
            }

            meteor.alignment = LineAlignment.View;
            Style(meteor, meteorSize * .5f, meteorColor);
        }

        void DrawAstral(bool show)
        {
            if (!Show(ref astral, "TyrantAstralRing", Segments, true, show)) return;
            Circle(astral, enemy.Position, enemy.AstralReach, .055f);
            Style(astral, lineWidth, astralColor);
        }

        bool Show(ref LineRenderer line, string name, int points, bool loop, bool show)
        {
            if (!show)
            {
                if (line) line.enabled = false;
                return false;
            }

            if (!line) line = Make(name, points, loop);
            line.enabled = true;
            return true;
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

        static void Style(LineRenderer line, float width, Color colour)
        {
            line.widthMultiplier = width;
            line.startColor = line.endColor = colour;
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
