using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AnomalyArena
{

    [DefaultExecutionOrder(-40)]
    public sealed partial class V5Vfx : MonoBehaviour
    {
        public static V5Vfx I { get; private set; }

        public static bool On => I && Art.Enabled;

        Texture teleCone;

        public Texture TeleCone => teleCone ? teleCone : teleCone = Tex("tele-cone");

        public static float Quality { get; private set; } = 1f;

        sealed class Layer
        {
            public ParticleSystem ps;
            public ParticleSystem.EmitParams p;

            public void Emit(Vector3 at, Vector3 velocity, float size, float life, Color color, float rotation = 0f)
            {
                p.position = at;
                p.velocity = velocity;
                p.startSize = size;
                p.startLifetime = life;
                p.startColor = color;
                p.rotation = rotation;
                ps.Emit(p, 1);
            }

            public void EmitSized(Vector3 at, Vector3 velocity, Vector2 size, float life, Color color, float rotation = 0f)
            {
                p.position = at;
                p.velocity = velocity;
                p.startSize3D = new Vector3(size.x, size.y, 1f);
                p.startLifetime = life;
                p.startColor = color;
                p.rotation = rotation;
                ps.Emit(p, 1);
            }
        }

        struct LayerShape
        {
            public int tilesX, tilesY;
            public bool flipbook;
            public float cycles, drag, gravity;
            public AnimationCurve size;
            public Gradient fade;
            public ParticleSystemRenderMode mode;
            public float stretch;
            public bool sized3D, randomRotation;
            public int max;
            public int order;
        }

        Shader particleShader;
        Layer spark, flash, star, ember, ringLayer, softRing, groundGlow, decal, frostDecal, explosion, smoke, smokeLow, cloud, fireball, flame, smallFlame, muzzle, slash, elec, bolt, glint, bubble, twirl, rune, puff, energy, debris;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (_, _) => Make();
            Make();
        }

        static void Make()
        {
            if (I || !GameManager.Instance) return;
            I = new GameObject("V5 Vfx").AddComponent<V5Vfx>();
        }

        void Awake()
        {
            I = this;
            gameObject.AddComponent<V5VfxLate>();
            particleShader = Resources.Load<Shader>("V5/V5Particle");
            BuildLayers();
            BuildBodyLayers();
        }

        void Start() => ApplyLook();

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Update()
        {
            AdaptQuality();
            if (!lookApplied || lookIllustrated != Art.Enabled) ApplyLook();
            TickLook();
            DressTrialPlatform();
            TickTrial();
            TickMirrors();
            HideMeteorWhenDone();
            TickFlight();
            TickOrbs();
            TickWeaponAndEnemies();
            TickFreeze();
        }

        public static Texture2D Tex(string name) => Resources.Load<Texture2D>("V5/Fx/" + name);

        Material FireMat(string texture, float intensity, float glow, float bias = 1.2f, float lumaAlpha = 2.4f)
        {
            var m = Mat(texture, false, intensity, glow, bias);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_LumaAlpha", lumaAlpha);
            return m;
        }

        Material Mat(string texture, bool additive, float intensity = 1f, float glow = 0f, float bias = 1.2f)
        {
            var m = new Material(particleShader);
            m.SetTexture("_BaseMap", Tex(texture));
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Glow", glow);
            m.SetFloat("_Bias", bias);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            return m;
        }

        static Gradient Fade(float inShare, float holdUntil, float end = 0f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(inShare > 0f ? 0f : 1f, 0f), new GradientAlphaKey(1f, Mathf.Max(inShare, .001f)), new GradientAlphaKey(1f, Mathf.Max(holdUntil, inShare + .001f)), new GradientAlphaKey(end, 1f) });
            return g;
        }

        static AnimationCurve Curve(float start, float end, float inTangentEase = 0f)
        {
            var c = AnimationCurve.Linear(0f, start, 1f, end);
            if (inTangentEase > 0f)
            {
                c = new AnimationCurve(new Keyframe(0f, start, (end - start) * inTangentEase, (end - start) * inTangentEase), new Keyframe(1f, end, 0f, 0f));
            }
            return c;
        }

        Layer Make(string name, Material material, LayerShape s)
        {
            var go = new GameObject("V5 " + name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;                                              
            main.maxParticles = s.max > 0 ? s.max : 256;
            main.startSpeed = 0f;
            main.gravityModifier = s.gravity;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSize3D = s.sized3D;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            if (s.tilesX * s.tilesY > 1)
            {
                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = s.tilesX;
                sheet.numTilesY = s.tilesY;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                if (s.flipbook)
                {
                    sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                    sheet.cycleCount = Mathf.Max(1, Mathf.RoundToInt(s.cycles));
                }
                else
                {

                    sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                    sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .999f);
                }
            }
            if (s.size != null)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, s.size);
            }
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(s.fade ?? Fade(0f, .25f));
            if (s.drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 1000f;
                limit.drag = s.drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = true;
            }
            if (s.randomRotation)
            {
                var spin = ps.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = s.mode;
            if (s.mode == ParticleSystemRenderMode.Stretch)
            {
                r.velocityScale = s.stretch;
                r.lengthScale = 1f;
            }
            r.sortMode = ParticleSystemSortMode.None;
            r.sortingOrder = s.order;
            r.minParticleSize = 0f;
            r.maxParticleSize = 6f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            ps.Play(true);
            allLayers.Add(ps);
            return new Layer { ps = ps };
        }

        void BuildLayers()
        {
            const ParticleSystemRenderMode Flat = ParticleSystemRenderMode.HorizontalBillboard;
            const ParticleSystemRenderMode Face = ParticleSystemRenderMode.Billboard;

            decal = Make("Scorch", Mat("scorch", false, .9f, 0f, 0f), new LayerShape { tilesX = 2, tilesY = 2, mode = Flat, fade = Fade(.03f, .55f), order = -2, max = 96 });
            frostDecal = Make("Frost", Mat("frost", false, 1f, 0f, 0f), new LayerShape { mode = Flat, fade = Fade(.08f, .6f), order = -2, max = 64 });
            groundGlow = Make("GroundGlow", Mat("glow", true, 1.4f, 0f, 0f), new LayerShape { mode = Flat, fade = Fade(.05f, .2f), order = -1, max = 128 });
            ringLayer = Make("Ring", Mat("ring", true, 2.2f, 0f, 0f), new LayerShape { mode = Flat, size = Curve(.62f, 1f, 2.4f), fade = Fade(0f, .35f), order = -1, max = 128 });
            softRing = Make("SoftRing", Mat("shock", true, 1.3f, 0f, 0f), new LayerShape { mode = Flat, size = Curve(.45f, 1.06f, 2.4f), fade = Fade(0f, .15f), order = -1, max = 64 });
            rune = Make("Runes", Mat("runes", true, 1.6f, 0f, 0f), new LayerShape { tilesX = 2, tilesY = 1, mode = Flat, fade = Fade(.1f, .7f), order = -1, max = 16 });
            twirl = Make("Twirl", Mat("twirl", true, 1.8f, 0f, 0f), new LayerShape { tilesX = 2, tilesY = 2, mode = Flat, size = Curve(.9f, 1.05f), fade = Fade(.1f, .5f), order = -1, max = 96 });

            smokeLow = Make("SmokeLow", Mat("puffs", false, .55f), new LayerShape { tilesX = 2, tilesY = 2, mode = Face, size = Curve(.7f, 1.5f), fade = Fade(.1f, .35f), drag = 1.2f, order = 0, max = 192 });
            smoke = Make("Smoke", Mat("smoke", false, .6f), new LayerShape { tilesX = 8, tilesY = 8, flipbook = true, mode = Face, size = Curve(.75f, 1.4f), fade = Fade(.08f, .45f), drag = .8f, order = 1, max = 128 });
            cloud = Make("Cloud", Mat("cloud", false, 1f), new LayerShape { tilesX = 8, tilesY = 8, flipbook = true, mode = Face, size = Curve(.6f, 1.25f), fade = Fade(.22f, .6f), drag = .6f, order = 1, max = 160 });
            explosion = Make("Explosion", Mat("explosion", false, 1.05f, 2.6f), new LayerShape { tilesX = 5, tilesY = 5, flipbook = true, mode = Face, fade = Fade(0f, .45f), order = 2, max = 48 });
            fireball = Make("Fireball", FireMat("fireball", .98f, .5f), new LayerShape { tilesX = 7, tilesY = 7, flipbook = true, mode = Face, size = Curve(.55f, 1.3f), fade = Fade(.03f, .75f), drag = 1.6f, order = 3, max = 320 });
            flame = Make("Flame", FireMat("flame", .9f, .45f, 1.2f, 2.1f), new LayerShape { tilesX = 8, tilesY = 8, flipbook = true, mode = Face, fade = Fade(.1f, .72f), order = 3, max = 256 });
            smallFlame = Make("SmallFlame", FireMat("smallflame", .98f, .45f, 1.6f, 2.2f), new LayerShape { tilesX = 11, tilesY = 11, flipbook = true, mode = Face, fade = Fade(.08f, .75f), order = 3, max = 256 });
            energy = Make("Energy", Mat("energy", true, 1.8f, 1f, 1.4f), new LayerShape { tilesX = 6, tilesY = 6, flipbook = true, mode = Face, size = Curve(.8f, 1.15f), fade = Fade(0f, .55f), order = 3, max = 96 });
            debris = Make("Debris", Mat("debris", false, .55f, 0f, 1f), new LayerShape { tilesX = 2, tilesY = 2, mode = Face, gravity = 1.1f, drag = .6f, randomRotation = true, fade = Fade(0f, .75f), order = 2, max = 256 });
            puff = Make("Puff", Mat("glow", false, 1f), new LayerShape { mode = Face, size = Curve(.5f, 1.3f), fade = Fade(.05f, .3f), drag = 1.5f, order = 2, max = 128 });

            flash = Make("Flash", Mat("orb", true, 2f, 0f, 2f), new LayerShape { mode = Face, size = Curve(.7f, 1.2f), fade = Fade(0f, .1f), order = 5, max = 128 });
            star = Make("Star", Mat("stars", true, 3f, 0f, 2f), new LayerShape { tilesX = 4, tilesY = 2, mode = Face, size = Curve(.6f, 1.15f), fade = Fade(0f, .25f), order = 5, max = 96 });
            muzzle = Make("Muzzle", Mat("muzzle", true, 3f, 0f, 1.6f), new LayerShape { tilesX = 2, tilesY = 2, mode = Face, size = Curve(.85f, 1.1f), fade = Fade(0f, .4f), order = 5, max = 64 });
            slash = Make("Slash", Mat("slash", true, 2.6f, 0f, 1.6f), new LayerShape { tilesX = 2, tilesY = 2, mode = Flat, size = Curve(.8f, 1.15f), fade = Fade(0f, .3f), order = 4, max = 64 });
            elec = Make("Elec", Mat("elec", true, 3.2f, 0f, 1.8f), new LayerShape { tilesX = 2, tilesY = 2, mode = Face, fade = Fade(0f, .4f), order = 5, max = 192 });
            bolt = Make("Bolt", Mat("bolt", true, 3.4f, 0f, 1f), new LayerShape { tilesX = 1, tilesY = 8, mode = Face, sized3D = true, fade = Fade(0f, .35f), order = 5, max = 128 });
            spark = Make("Spark", Mat("trace", true, 3.6f, 0f, 1.4f), new LayerShape { mode = ParticleSystemRenderMode.Stretch, stretch = .055f, gravity = .35f, drag = 2.2f, fade = Fade(0f, .4f), order = 6, max = 512 });
            ember = Make("Ember", Mat("orb", true, 2.4f, 0f, 1f), new LayerShape { mode = Face, size = Curve(1f, .5f), gravity = -.05f, drag = 1.4f, fade = Fade(.05f, .55f), order = 6, max = 512 });
            glint = Make("Glint", Mat("stars", true, 2.4f, 0f, 1.4f), new LayerShape { tilesX = 4, tilesY = 2, mode = Face, size = Curve(.3f, 1f), fade = Fade(.2f, .4f), order = 6, max = 192 });
            bubble = Make("Bubble", Mat("bubble", false, 1.2f, 0f, 1.4f), new LayerShape { mode = Face, size = Curve(.5f, 1.1f), fade = Fade(.1f, .75f), order = 4, max = 128 });
        }

        static float R(float a, float b) => Random.Range(a, b);
        static int Count(float n) => Mathf.Max(1, Mathf.RoundToInt(n * Quality));
        static Vector3 FlatDir(Vector3 d, Vector3 fallback)
        {
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : fallback;
        }
        static Vector3 OnGround(Vector3 p, float height = .07f) => new(p.x, height, p.z);
        static Color Bright(Color c, float toWhite) => Color.Lerp(c, Color.white, toWhite);
        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static float ScreenRoll(Vector3 direction)
        {
            var gm = GameManager.Instance;
            if (!gm || !gm.cam) return 0f;
            var local = gm.cam.transform.InverseTransformDirection(direction);
            return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        }

        public void Pop(Vector3 at, Color c, float size)
        {
            flash.Emit(at, Vector3.zero, size * .85f, .16f, WithAlpha(Bright(c, .25f), Mathf.Min(1f, c.a)));
        }

        public void Burst(Vector3 at, Vector3 dir, Color c, int count, float speed, float spreadDeg, float size)
        {
            Vector3 forward = dir.sqrMagnitude > 0f ? dir.normalized : Vector3.forward;
            int n = Count(Mathf.Min(count, 10));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(-spreadDeg, spreadDeg) * .5f, 0f) * forward;
                spark.Emit(at, (d * R(.6f, 1.3f) + Vector3.up * R(-.1f, .25f)) * speed, Mathf.Max(.05f, size * .55f), R(.12f, .28f), Bright(c, .3f));
            }
        }

        public void Ring(Vector3 at, float radius, Color c, float life)
        {
            float size = Mathf.Max(.2f, radius * 2f);
            float alpha = Mathf.Min(1f, c.a) * .9f;
            ringLayer.Emit(OnGround(at, .085f), Vector3.zero, size, Mathf.Max(.14f, life), WithAlpha(Bright(c, .15f), alpha), R(0f, 360f));
            if (radius > .8f) softRing.Emit(OnGround(at, .08f), Vector3.zero, size, Mathf.Max(.14f, life) * .8f, WithAlpha(c, .16f * alpha));
        }

        public void Hit(Vector3 at, Vector3 dir, Color c, float radius, bool heavy, bool crit)
        {
            Vector3 d = FlatDir(dir, Vector3.forward);
            int n = Count(heavy ? 7 : 3);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = Quaternion.Euler(0f, R(-42f, 42f), 0f) * d * R(7f, heavy ? 17f : 13f) + Vector3.up * R(-.5f, 2f);
                spark.Emit(at, v, R(.06f, heavy ? .13f : .1f), R(.09f, .22f), Bright(c, .35f));
            }
            flash.Emit(at, Vector3.zero, Mathf.Max(.5f, radius * (heavy ? 1.7f : 1f)), heavy ? .08f : .06f, WithAlpha(Bright(c, .3f), .85f));
            if (heavy)
            {
                star.Emit(at, Vector3.zero, Mathf.Max(1f, radius * 3f), .12f, Bright(c, .5f), R(0f, 90f));
                smokeLow.Emit(at, d * R(.5f, 1.5f) + Vector3.up * .4f, Mathf.Max(.7f, radius * 1.7f), R(.3f, .5f), new Color(.5f, .47f, .44f, .42f), R(0f, 360f));
            }
            if (crit)
            {
                star.Emit(at, Vector3.zero, Mathf.Max(1.6f, radius * 4.4f), .2f, new Color(1f, .82f, .4f), R(0f, 90f));
                groundGlow.Emit(OnGround(at), Vector3.zero, radius * 3f, .16f, new Color(1f, .7f, .3f, .55f));
            }
        }

        public void Muzzle(Vector3 at, Vector3 dir, Color c, float size)
        {
            Vector3 d = FlatDir(dir, Vector3.forward);
            float roll = ScreenRoll(d) - 90f;                     
            muzzle.Emit(at + d * size * .28f, d * 1.5f, size * 1.25f, .055f, Bright(c, .35f), -roll);
            flash.Emit(at, Vector3.zero, size * .7f, .05f, c);
            groundGlow.Emit(OnGround(at), Vector3.zero, size * 2.2f, .07f, WithAlpha(c, .4f));
            CaseEject(at - d * .4f, d);
            int n = Count(2);
            for (int i = 0; i < n; i++)
                spark.Emit(at, (Quaternion.Euler(0f, R(-16f, 16f), 0f) * d * R(9f, 18f)) + Vector3.up * R(0f, 1f), R(.04f, .07f), R(.05f, .11f), Bright(c, .4f));
        }

        public void Explosion(Vector3 at, float radius, Color? tint = null, bool scorch = true)
        {
            Color warm = tint ?? new Color(1f, .62f, .24f);
            Vector3 ground = OnGround(at);
            Vector3 air = new(at.x, Mathf.Max(.8f, radius * .35f), at.z);
            if (tint.HasValue)
            {

                flash.Emit(air, Vector3.zero, radius * 1.9f, .09f, WithAlpha(Bright(warm, .3f), .85f));
                groundGlow.Emit(ground, Vector3.zero, radius * 3f, .34f, WithAlpha(warm, .55f));
                energy.Emit(air, Vector3.up * .4f, radius * 2.7f, R(.5f, .62f), warm, R(0f, 360f));
                int n = Count(Mathf.Clamp(radius * 3f, 5f, 14f));
                for (int i = 0; i < n; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                    cloud.Emit(air + d * radius * .2f, d * radius * R(.8f, 1.8f), radius * R(.5f, .8f), R(.35f, .6f), WithAlpha(warm, .3f), R(0f, 360f));
                    spark.Emit(air, (d + Vector3.up * R(.05f, .7f)) * radius * R(5f, 9f), R(.07f, .12f), R(.16f, .4f), Bright(warm, .3f));
                }
                ringLayer.Emit(OnGround(at, .09f), Vector3.zero, radius * 2f, .26f, WithAlpha(Bright(warm, .3f), .85f), R(0f, 360f));
                return;
            }
            flash.Emit(air, Vector3.zero, radius * 1.9f, .09f, WithAlpha(Bright(warm, .4f), .85f));
            groundGlow.Emit(ground, Vector3.zero, radius * 3f, .32f, WithAlpha(warm, .55f));
            explosion.Emit(air, Vector3.up * .5f, radius * 3.1f, R(.7f, .82f), Color.white, R(0f, 360f));
            int puffs = Count(Mathf.Clamp(radius * 1.4f, 2f, 5f));
            for (int i = 0; i < puffs; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius * .8f;
                smoke.Emit(air + new Vector3(o.x, 0f, o.y), new Vector3(o.x, 1.4f, o.y) * .6f, radius * R(.9f, 1.3f), R(.7f, 1.2f), new Color(.34f, .31f, .29f, .3f), R(0f, 360f));
            }
            int chunks = Count(Mathf.Clamp(radius * 2.4f, 4f, 12f));
            for (int i = 0; i < chunks; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                debris.Emit(air, (d * R(.6f, 1.2f) + Vector3.up * R(.6f, 1.3f)) * radius * R(2.5f, 4.5f), R(.18f, .4f), R(.45f, .85f), new Color(.5f, .46f, .42f), R(0f, 360f));
            }
            int sparks = Count(Mathf.Clamp(radius * 7f, 8f, 30f));
            for (int i = 0; i < sparks; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                spark.Emit(air, (d * R(.7f, 1.3f) + Vector3.up * R(.05f, .7f)) * radius * R(5f, 10f), R(.07f, .13f), R(.18f, .48f), Bright(warm, .35f));
            }
            int embers = Count(Mathf.Clamp(radius * 3f, 3f, 12f));
            for (int i = 0; i < embers; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius;
                ember.Emit(air + new Vector3(o.x, 0f, o.y), new Vector3(o.x, 2.5f, o.y) * .8f, R(.08f, .2f), R(.5f, 1.1f), warm);
            }
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, radius * 2f, .26f, WithAlpha(Bright(warm, .3f), .85f), R(0f, 360f));
            if (scorch) Scorch(at, radius);
        }

        public void Scorch(Vector3 at, float radius, float life = 5f)
        {
            decal.Emit(OnGround(at, .045f), Vector3.zero, radius * 2.4f, life, new Color(.04f, .035f, .03f, .5f), R(0f, 360f));
        }

        public void Cone(Vector3 origin, Vector3 dir, float radius, float arcDeg, ElementKind element)
        {
            Vector3 d = FlatDir(dir, Vector3.forward);
            Vector3 at = new(origin.x, Query.CastHeight, origin.z);
            float half = Mathf.Clamp(arcDeg, 10f, 170f) * .5f;
            switch (element)
            {
                case ElementKind.Fire:
                {
                    int n = Count(6);
                    for (int i = 0; i < n; i++)
                    {

                        Vector3 v = Quaternion.Euler(0f, R(-half, half) * .7f, 0f) * d;
                        fireball.Emit(at + v * R(.4f, .9f), v * radius * R(2.6f, 3.6f) + Vector3.up * R(0f, .4f), radius * R(.5f, .8f), R(.3f, .46f), new Color(1f, R(.74f, .98f), R(.55f, .88f), 1f), R(0f, 360f));
                    }
                    ember.Emit(at + d * radius * R(.3f, .8f), d * radius * 1.5f + Random.insideUnitSphere * 2f, R(.06f, .12f), R(.3f, .6f), new Color(1f, .6f, .2f));
                    groundGlow.Emit(OnGround(at + d * radius * .5f), Vector3.zero, radius * 1.3f, .14f, new Color(1f, .5f, .15f, .45f));
                    break;
                }
                case ElementKind.Frost:
                {
                    int n = Count(4);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 v = Quaternion.Euler(0f, R(-half, half) * .85f, 0f) * d;
                        cloud.Emit(at + v * R(.4f, 1f), v * radius * R(1.3f, 2.2f), radius * R(.42f, .66f), R(.3f, .5f), new Color(.72f, .9f, 1f, .5f), R(0f, 360f));
                        glint.Emit(at + v * radius * R(.2f, .95f), v * R(1f, 3f), R(.2f, .42f), R(.14f, .3f), new Color(.8f, .95f, 1f));
                    }
                    break;
                }
                case ElementKind.Poison:
                {
                    int n = Count(4);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 v = Quaternion.Euler(0f, R(-half, half) * .85f, 0f) * d;
                        cloud.Emit(at + v * R(.4f, 1f), v * radius * R(1.1f, 1.9f), radius * R(.5f, .75f), R(.45f, .7f), new Color(.5f, .95f, .25f, .5f), R(0f, 360f));
                    }
                    break;
                }
                default:
                {

                    Color blast = element == ElementKind.None ? new Color(1f, .85f, .55f) : ElementTint(element);
                    int n = Count(9);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 v = Quaternion.Euler(0f, R(-half, half), 0f) * d;
                        spark.Emit(at + v * .5f, v * radius * R(4f, 9f), R(.07f, .12f), R(.08f, .17f), blast);
                    }
                    float roll = ScreenRoll(d) - 90f;
                    muzzle.Emit(at + d * radius * .22f, d * 2f, radius * .62f, .07f, blast, -roll);
                    flash.Emit(at + d * radius * .12f, Vector3.zero, radius * .5f, .06f, blast);
                    if (element == ElementKind.Lightning)
                        Bolt(at, at + Quaternion.Euler(0f, R(-half, half), 0f) * d * radius * R(.6f, 1f), blast, .9f, .08f);
                    for (int i = 0; i < Count(3); i++)
                    {
                        Vector3 v = Quaternion.Euler(0f, R(-half, half) * .8f, 0f) * d;
                        smokeLow.Emit(at + v * radius * R(.2f, .55f), v * radius * R(.6f, 1.2f), radius * R(.4f, .6f), R(.3f, .5f), new Color(.6f, .58f, .54f, .34f), R(0f, 360f));
                    }
                    groundGlow.Emit(OnGround(at + d * radius * .4f), Vector3.zero, radius * 1.25f, .09f, new Color(1f, .75f, .4f, .4f));
                    break;
                }
            }
        }

        public void Bolt(Vector3 from, Vector3 to, Color c, float width, float life)
        {
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < .05f) return;
            float roll = ScreenRoll(line.normalized);
            bolt.EmitSized((from + to) * .5f, Vector3.zero, new Vector2(length, Mathf.Max(.25f, width)), Mathf.Max(.05f, life), Bright(c, .25f), -roll);
            flash.Emit(to, Vector3.zero, Mathf.Max(.5f, width * 1.6f), .06f, c);
        }

        Volume volume, slipVolume;
        bool lookApplied, lookIllustrated;
        UniversalAdditionalCameraData cameraData;
        bool bloomDropped;

        void ApplyLook()
        {
            var gm = GameManager.Instance;
            if (!gm || !gm.cam) return;
            lookApplied = true;
            lookIllustrated = Art.Enabled;

            var look = gm.player ? gm.player.GetComponent<V2ModelPresentation>() : null;
            if (look)
            {
                look.idleEmission = Art.Enabled ? new Color(.02f, .05f, .09f) : Color.black;
                look.baseTint = Art.Enabled ? new Color(.62f, .82f, 1f) : Color.white;
            }
            cameraData = gm.cam.GetUniversalAdditionalCameraData();
            if (cameraData)
            {
                cameraData.renderPostProcessing = Art.Enabled;
                cameraData.antialiasing = Art.Enabled ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            }
            if (!volume)
            {
                var profile = Resources.Load<VolumeProfile>("V5/V5Look");
                if (profile)
                {
                    var go = new GameObject("V5 Look");
                    go.transform.SetParent(transform, false);
                    volume = go.AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.priority = 50f;
                    volume.sharedProfile = profile;
                }
            }
            if (volume) volume.enabled = Art.Enabled;
        }

        void TickLook()
        {
            var gm = GameManager.Instance;
            bool slip = gm && gm.skills && gm.skills.TimeSlipActive && Art.Enabled;
            if (slip && !slipVolume)
            {
                var go = new GameObject("V5 Slip");
                go.transform.SetParent(transform, false);
                slipVolume = go.AddComponent<Volume>();
                slipVolume.isGlobal = true;
                slipVolume.priority = 60f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var grade = profile.Add<ColorAdjustments>(true);
                grade.saturation.Override(-55f);
                grade.colorFilter.Override(new Color(.8f, .92f, 1.08f));
                slipVolume.sharedProfile = profile;
                slipVolume.weight = 0f;
            }
            if (slipVolume) slipVolume.weight = Mathf.MoveTowards(slipVolume.weight, slip ? 1f : 0f, Time.unscaledDeltaTime * 3.5f);
        }

        float fpsClock;
        int fpsFrames, slowSpells;

        void AdaptQuality()
        {

            var game = GameManager.Instance;
            bool playing = game && game.RunPhase is ArenaUiState.WaveActive or ArenaUiState.VoidTrial or ArenaUiState.FinalBoss;
            if (Time.timeScale <= 0f || !playing || !Application.isFocused || Time.unscaledDeltaTime > .5f)
            {
                fpsClock = 0f;
                fpsFrames = 0;
                return;
            }
            fpsClock += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsClock < 2f) return;
            float fps = fpsFrames / fpsClock;
            fpsClock = 0f;
            fpsFrames = 0;
            if (fps >= 34f) { slowSpells = 0; return; }
            if (++slowSpells < 2) return;
            slowSpells = 0;
            if (!bloomDropped && volume)
            {
                bloomDropped = true;

                if (volume.profile.TryGet<Bloom>(out var bloom)) bloom.active = false;
            }
            else Quality = Mathf.Max(.35f, Quality * .75f);
        }
    }

    [DefaultExecutionOrder(3000)]
    public sealed class V5VfxLate : MonoBehaviour
    {
        void LateUpdate()
        {
            var gm = GameManager.Instance;
            var world = WorldMap.Active;
            if (!gm || !gm.hud || !gm.hud.v2Hud || !Art.Enabled || !world || !world.TrialActive) return;
            var image = gm.hud.v2Hud.damageFlash;
            if (!image) return;
            var c = image.color;
            c.a *= .4f;
            image.color = c;
        }
    }
}
