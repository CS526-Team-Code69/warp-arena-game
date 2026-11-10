using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// A luminous ring on the ground that a caller drives frame by frame with a centre, the radius of its leading edge, a thickness (the
    /// soft trail behind that edge), a colour and a brightness: a light wave sweeping outward. The leading edge lies exactly at the radius
    /// given and nothing is drawn beyond it, so a ring drawn from a gameplay front shows that front. With shadowWalls the parts of the ring
    /// a wall hides from the centre stay dark. Whitebox draws it plain (blended, never brighter than its colour, so no bloom); the
    /// illustrated art (V5Vfx.On) adds the glow: drawn additively, bright enough to bloom, a whiter leading edge and glints along the front
    /// while it moves. Mesh and materials are made at runtime, so scenes and prefabs gain nothing.
    /// General on purpose: the Spire's light wave is its first user (SpireWavePresentation); light
    /// sources and sentries of later versions can drive their own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightWaveRing : MonoBehaviour
    {
        // Points round the ring: at a radius of 10 cells the edge stays within a hundredth of a cell of a true circle.
        const int Segments = 128;
        // Rows across the ring from the leading edge inward: the edge, the end of the bright band, the middle and the end of the trail.
        const int Rows = 4;
        static readonly float[] RowAlpha = { 1f, 1f, .42f, 0f };
        // How often the wall shadows are cast again while the ring is out, for walls raised in the meantime; seconds.
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

        /// <summary>A new ring under parent (kept in world space whatever the parent does), hidden until its first Draw.</summary>
        public static LightWaveRing Create(Transform parent, string name = "LightWaveRing")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<LightWaveRing>();
        }

        /// <summary>The ring was drawn this frame or a frame before and has not been hidden since.</summary>
        public bool Visible => meshRenderer && meshRenderer.enabled && isActiveAndEnabled;

        /// <summary>The centre, on the ground, of the last Draw.</summary>
        public Vector3 Centre { get; private set; }

        /// <summary>The radius of the leading edge in the last Draw.</summary>
        public float Radius { get; private set; }

        /// <summary>The thickness (trail length) of the last Draw.</summary>
        public float Thickness { get; private set; }

        /// <summary>Draws the ring for this frame; call it again each frame the ring changes. colour.a fades the whole ring; intensity is
        /// its brightness, which only the illustrated art takes above 1 (that is what blooms).</summary>
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
            // A raw multiplier, not a colour: SetColor would decode it from sRGB as well and brighten far more than asked.
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

        /// <summary>Whether the ring is lit at this distance from its centre along this flat direction: false in a wall's shadow (with
        /// shadowWalls), true everywhere otherwise.</summary>
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
            meshRenderer.sortingOrder = -1; // with the other ground effects, under everything that stands on the ground
            meshRenderer.enabled = false;
            block = new MaterialPropertyBlock();
        }

        /// <summary>Places every point for this frame: rows at fixed distances behind the leading edge (clamped at the centre while the ring
        /// is smaller than its thickness), the edge rows lifted toward white in the illustrated art, points in a wall's shadow left clear.</summary>
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

        /// <summary>Mesh colours reach the shader untouched, while every authored colour here is sRGB: in linear space they are converted
        /// first, so the ring matches the particles and lines drawn in the same colour.</summary>
        static Color ToVertex(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        /// <summary>The distance to the first wall along each point's direction, cast at the height every wall check uses (Query.CastHeight);
        /// cast again when the centre moves, the reach grows, or every ShadowRefreshSeconds.</summary>
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

        /// <summary>Illustrated art: a few glints on the front where it is lit; they stay behind as it moves on.</summary>
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

        /// <summary>The V5 effect shader (unlit, vertex colour, blend per material) loaded from Resources, so builds keep it: blended for
        /// whitebox, additive for the illustrated art.</summary>
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
