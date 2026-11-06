using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    public sealed class LightWaveRing : MonoBehaviour
    {

        const int Segments = 128;

        const int Rows = 4;
        static readonly float[] RowAlpha = { 1f, 1f, .42f, 0f };

        const float ShadowRefreshSeconds = .25f;

        [Min(0f), Tooltip("Height of the ring above the ground, cells.")]
        public float height = .08f;

        [Min(.01f), Tooltip("Width of the bright band at the leading edge, cells; never more than a quarter of the thickness.")]
        public float edgeWidth = .2f;

        [Range(0f, 1f), Tooltip("Illustrated art: how far the leading edge is lifted toward white.")]
        public float edgeWhiten = .55f;

        [Tooltip("Leave dark the parts of the ring a wall hides from the centre (a light wave stops at walls).")]
        public bool shadowWalls;

        [Min(0f), Tooltip("Farthest distance from the centre the wall shadows are cast to; 0 follows the radius and casts again as it grows.")]
        public float shadowReach;

        [Min(0f), Tooltip("Illustrated art: glints a second, per cell of radius, flaring along the front while it moves; 0 for none.")]
        public float sparkleRate = 5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static float[] cos, sin;
        static int[] triangles;
        static Vector2[] uvs;
        static Material plainMaterial, glowMaterial;

        MeshRenderer meshRenderer;
        Mesh mesh;
        MaterialPropertyBlock block;
        Vector3[] vertices;
        Color[] colours;
        readonly float[] offsets = new float[Rows];
        float[] clearDistance;
        Vector3 shadowCentre;
        float shadowProbe = -1f, shadowCastAt;

        public static LightWaveRing Create(Transform parent, string name = "LightWaveRing")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<LightWaveRing>();
        }

        public bool Visible => meshRenderer && meshRenderer.enabled && isActiveAndEnabled;

        public Vector3 Centre { get; private set; }

        public float Radius { get; private set; }

        public float Thickness { get; private set; }

        public void Draw(Vector3 centre, float radius, float thickness, Color colour, float intensity)
        {
            bool art = V5Vfx.On;
            var material = Look(art);
            if (!material)
            {
                Hide();
                return;
            }

            EnsureParts();
            centre.y = 0f;
            radius = Mathf.Max(0f, radius);
            bool advanced = Visible && radius > Radius + 1e-4f && (centre - Centre).sqrMagnitude < 1e-6f;
            Centre = centre;
            Radius = radius;
            Thickness = Mathf.Max(.01f, thickness);
            if (shadowWalls) CastShadows(Mathf.Max(radius, shadowReach));
            Shape(colour, art);
            if (meshRenderer.sharedMaterial != material) meshRenderer.sharedMaterial = material;

            float k = art ? Mathf.Max(0f, intensity) : Mathf.Clamp01(intensity);
            block.SetVector(BaseColorId, new Vector4(k, k, k, 1f));
            meshRenderer.SetPropertyBlock(block);
            meshRenderer.enabled = true;
            if (art && advanced && sparkleRate > 0f) Sparkle(colour);
        }

        public void Hide()
        {
            if (meshRenderer) meshRenderer.enabled = false;
        }

        public bool LitAt(Vector3 direction, float distance)
        {
            if (!shadowWalls || clearDistance == null) return true;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return true;
            float angle = Mathf.Atan2(direction.z, direction.x);
            int s = Mathf.RoundToInt(Mathf.Repeat(angle, Mathf.PI * 2f) / (Mathf.PI * 2f) * Segments) % Segments;
            return distance <= clearDistance[s];
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }

        void EnsureParts()
        {
            if (meshRenderer) return;
            BuildTables();
            vertices = new Vector3[Segments * Rows];
            colours = new Color[Segments * Rows];
            mesh = new Mesh { name = "LightWaveRing" };
            mesh.MarkDynamic();
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.sortingOrder = -1;                                                                             
            meshRenderer.enabled = false;
            block = new MaterialPropertyBlock();
        }

        void Shape(Color colour, bool art)
        {
            float edge = Mathf.Min(edgeWidth, Thickness * .25f);
            offsets[0] = 0f;
            offsets[1] = edge;
            offsets[2] = edge + (Thickness - edge) * .35f;
            offsets[3] = Thickness;
            float alpha = Mathf.Clamp01(colour.a);
            Color trail = ToVertex(colour);
            Color front = ToVertex(art ? Color.Lerp(colour, Color.white, edgeWhiten) : colour);
            var toLocal = transform.worldToLocalMatrix;
            for (int s = 0; s < Segments; s++)
            {
                float clear = shadowWalls && clearDistance != null ? clearDistance[s] : float.MaxValue;
                for (int r = 0; r < Rows; r++)
                {
                    int i = s * Rows + r;
                    float at = Mathf.Max(0f, Radius - offsets[r]);
                    vertices[i] = toLocal.MultiplyPoint3x4(new Vector3(Centre.x + cos[s] * at, height, Centre.z + sin[s] * at));
                    var c = r < 2 ? front : trail;
                    c.a = at <= clear ? RowAlpha[r] * alpha : 0f;
                    colours[i] = c;
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.RecalculateBounds();
        }

        static Color ToVertex(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        void CastShadows(float probe)
        {
            clearDistance ??= new float[Segments];
            bool stale = shadowProbe < 0f || probe > shadowProbe + .01f || (Centre - shadowCentre).sqrMagnitude > 1e-4f ||
                         Time.unscaledTime - shadowCastAt > ShadowRefreshSeconds;
            if (!stale) return;
            shadowProbe = probe;
            shadowCentre = Centre;
            shadowCastAt = Time.unscaledTime;
            Vector3 from = Query.AtCastHeight(Centre);
            for (int s = 0; s < Segments; s++)
                clearDistance[s] = probe > 0f && Physics.Raycast(from, new Vector3(cos[s], 0f, sin[s]), out var hit, probe, GameManager.WallMask,
                    QueryTriggerInteraction.Ignore)
                    ? hit.distance
                    : float.MaxValue;
        }

        void Sparkle(Color colour)
        {
            var fx = V5Vfx.I;
            if (!fx) return;
            float expected = sparkleRate * Radius * Time.deltaTime * V5Vfx.Quality;
            int count = Mathf.Min(8, (int)expected + (Random.value < expected % 1f ? 1 : 0));
            for (int i = 0; i < count; i++)
            {
                int s = Random.Range(0, Segments);
                if (shadowWalls && clearDistance != null && Radius > clearDistance[s]) continue;
                var outward = new Vector3(cos[s], 0f, sin[s]);
                fx.WaveGlint(Centre + outward * Radius, outward, colour);
            }
        }

        static Material Look(bool art)
        {
            var made = art ? glowMaterial : plainMaterial;
            if (made) return made;
            var shader = Resources.Load<Shader>("V5/V5Particle");
            if (!shader) return null;
            made = new Material(shader) { name = art ? "LightWaveRing Glow" : "LightWaveRing Plain" };
            made.SetFloat("_Intensity", 1f);
            made.SetFloat("_Glow", 0f);
            made.SetFloat("_Bias", 0f);
            made.SetFloat("_LumaAlpha", 0f);
            made.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            made.SetFloat("_DstBlend", (float)(art ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            made.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            if (art) glowMaterial = made;
            else plainMaterial = made;
            return made;
        }

        static void BuildTables()
        {
            if (cos != null) return;
            cos = new float[Segments];
            sin = new float[Segments];
            uvs = new Vector2[Segments * Rows];
            triangles = new int[Segments * (Rows - 1) * 6];
            int t = 0;
            for (int s = 0; s < Segments; s++)
            {
                float a = s * Mathf.PI * 2f / Segments;
                cos[s] = Mathf.Cos(a);
                sin[s] = Mathf.Sin(a);
                for (int r = 0; r < Rows; r++) uvs[s * Rows + r] = new Vector2(r / (Rows - 1f), s / (float)Segments);
                int next = (s + 1) % Segments;
                for (int r = 0; r < Rows - 1; r++)
                {
                    int here = s * Rows + r, there = next * Rows + r;
                    triangles[t++] = here;
                    triangles[t++] = there;
                    triangles[t++] = here + 1;
                    triangles[t++] = there;
                    triangles[t++] = there + 1;
                    triangles[t++] = here + 1;
                }
            }
        }
    }
}
