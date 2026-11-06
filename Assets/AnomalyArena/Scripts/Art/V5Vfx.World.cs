using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed partial class V5Vfx
    {

        public void SkillCast(SkillId id, Vector3 point, float radius, Vector3 from)
        {
            switch (id)
            {
                case SkillId.Contagion: PoisonBurst(point, Mathf.Max(1f, radius)); break;
                case SkillId.FrostNova: FrostNova(point, Mathf.Max(1f, radius)); break;
                case SkillId.Radiance: HealGlow(point); break;
                case SkillId.Devour: Swallow(point, 1.6f); break;
                case SkillId.Blink: Blink(from, point); break;
                case SkillId.Shockwave: Shockwave(point, Mathf.Max(1f, radius)); break;
                case SkillId.Overcharge:
                    flash.Emit(new Vector3(point.x, Query.CastHeight, point.z), Vector3.zero, 2.4f, .14f, new Color(1f, .72f, .3f));
                    for (int i = 0; i < Count(5); i++) elec.Emit(new Vector3(point.x, Query.CastHeight, point.z) + Random.insideUnitSphere * .8f, Vector3.zero, R(1.4f, 2.2f), R(.08f, .14f), new Color(1f, .78f, .35f), R(0f, 360f));
                    break;
                case SkillId.TimeSlip:
                    ringLayer.Emit(OnGround(point, .09f), Vector3.zero, Mathf.Max(6f, radius * 2f), .5f, new Color(.62f, .82f, 1f, .85f));
                    softRing.Emit(OnGround(point, .085f), Vector3.zero, Mathf.Max(6f, radius * 2f), .45f, new Color(.5f, .75f, 1f, .3f));
                    break;
                case SkillId.Decoy:
                    flash.Emit(new Vector3(point.x, Query.CastHeight, point.z), Vector3.zero, 2.2f, .16f, new Color(.45f, .85f, 1f));
                    ringLayer.Emit(OnGround(point, .09f), Vector3.zero, 2.6f, .28f, new Color(.5f, .88f, 1f, .85f));
                    break;
            }
        }

        public void WakeSample(Vector3 at, float radius)
        {
            if (Chance(2.2f))
            {
                float size = R(1.1f, 2f) * Mathf.Clamp(radius / .75f, .7f, 1.5f);
                flame.Emit(new Vector3(at.x + R(-radius, radius) * .5f, size * .3f, at.z + R(-radius, radius) * .5f), Vector3.up * R(.1f, .3f), size, R(.5f, .8f), new Color(1f, R(.62f, 1f), R(.45f, .9f)));
            }
            if (Chance(.5f)) groundGlow.Emit(OnGround(at), Vector3.zero, radius * 3.2f, .3f, new Color(1f, .45f, .12f, .3f));
            if (Chance(.35f)) ember.Emit(new Vector3(at.x, .5f, at.z), new Vector3(R(-.4f, .4f), R(1.5f, 3f), R(-.4f, .4f)), R(.05f, .09f), R(.4f, .8f), FireTint);
        }

        public void GravityWellTick(Vector3 centre, float radius)
        {
            if (Chance(26f))
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 d = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 at = centre + d * radius * R(.65f, 1f) + Vector3.up * .3f;
                Vector3 inward = -d * radius * R(1.1f, 1.8f) + new Vector3(-d.z, 0f, d.x) * radius * .7f;
                spark.Emit(at, inward, R(.05f, .09f), R(.3f, .55f), new Color(.65f, .48f, 1f));
            }
            if (Chance(10f)) twirl.Emit(OnGround(centre, .09f), Vector3.zero, radius * R(1.2f, 2f), R(.3f, .5f), new Color(.6f, .38f, 1f, .75f), R(0f, 360f));
            if (Chance(5f)) ringLayer.Emit(OnGround(centre, .085f), Vector3.zero, radius * 2f, .45f, new Color(.62f, .42f, 1f, .32f));
            if (Chance(6f)) groundGlow.Emit(OnGround(centre), Vector3.zero, radius * 1.5f, .35f, new Color(.32f, .12f, .7f, .4f));
        }

        public void StormTick(Vector3 centre, float radius)
        {
            if (Chance(5f)) ringLayer.Emit(OnGround(centre, .085f), Vector3.zero, radius * 2f, .5f, new Color(.6f, .78f, 1f, .3f));
            if (Chance(8f))
            {
                Vector2 o = Random.insideUnitCircle * radius * .8f;
                cloud.Emit(new Vector3(centre.x + o.x, 3f, centre.z + o.y), Vector3.zero, radius * R(.6f, 1f), R(.7f, 1.1f), new Color(.2f, .23f, .32f, .3f), R(0f, 360f));
            }
        }

        public void SingularityTick(Vector3 at, float progress)
        {
            VoidSmoke(at + Vector3.up * .7f, .8f + progress);
            if (Chance(16f + 20f * progress))
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 d = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                spark.Emit(at + d * R(3f, 6f) + Vector3.up * .7f, -d * R(9f, 16f), R(.05f, .1f), R(.2f, .34f), VoidTint);
            }
            if (Chance(5f + 8f * progress)) twirl.Emit(OnGround(at, .1f), Vector3.zero, R(3f, 5f) * Mathf.Lerp(1f, .6f, progress), R(.25f, .4f), WithAlpha(VoidTint, .8f), R(0f, 360f));
        }

        public void VoidExplosion(Vector3 at, float radius)
        {
            Vector3 air = new(at.x, Query.CastHeight, at.z);
            flash.Emit(air, Vector3.zero, radius * 1.8f, .18f, VoidTint);
            star.Emit(air, Vector3.zero, radius * 1.6f, .2f, new Color(.85f, .65f, 1f), R(0f, 90f));
            energy.Emit(air, Vector3.zero, radius * 2.3f, .6f, new Color(.72f, .42f, 1f), R(0f, 360f));
            ringLayer.Emit(OnGround(at, .09f), Vector3.zero, radius * 2f, .42f, new Color(.78f, .5f, 1f, 1f));
            softRing.Emit(OnGround(at, .085f), Vector3.zero, radius * 2.05f, .4f, new Color(.5f, .25f, 1f, .45f));
            groundGlow.Emit(OnGround(at), Vector3.zero, radius * 2.4f, .4f, new Color(.5f, .2f, 1f, .6f));
            int n = Count(Mathf.Clamp(radius * 3f, 10f, 26f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
                smoke.Emit(air + d * radius * .2f, d * radius * R(.8f, 1.8f), radius * R(.25f, .45f), R(.5f, 1f), new Color(.08f, .03f, .16f, .75f), R(0f, 360f));
                spark.Emit(air, (d + Vector3.up * R(0f, .3f)) * radius * R(2.5f, 5f), R(.07f, .12f), R(.2f, .45f), VoidTint);
            }
            Scorch(at, radius * .7f, 5f);
        }

        public void GroundSlam(Vector3 at, float radius, Color c, bool ring = true)
        {
            Vector3 ground = OnGround(at, .4f);
            if (ring) ringLayer.Emit(OnGround(at, .09f), Vector3.zero, radius * 2f, .3f, WithAlpha(Bright(c, .25f), .95f));
            softRing.Emit(OnGround(at, .085f), Vector3.zero, radius * 2.05f, .32f, new Color(.72f, .64f, .52f, .3f));
            flash.Emit(ground, Vector3.zero, radius * .9f, .08f, new Color(1f, .9f, .75f));
            decal.Emit(OnGround(at, .045f), Vector3.zero, radius * 1.7f, 3.5f, new Color(.08f, .07f, .06f, .6f), R(0f, 360f));
            int n = Count(Mathf.Clamp(radius * 5f, 10f, 28f));
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 360f / n + R(-8f, 8f), 0f) * Vector3.forward;
                smokeLow.Emit(ground + d * radius * .25f, d * radius * R(1.5f, 2.8f) + Vector3.up * R(0f, .6f), radius * R(.3f, .55f), R(.35f, .7f), new Color(.6f, .55f, .47f, .42f), R(0f, 360f));
                if (i % 2 == 0) spark.Emit(ground, (d + Vector3.up * R(.3f, 1f)) * radius * R(2f, 4.5f), R(.09f, .16f), R(.25f, .5f), new Color(.82f, .7f, .55f));
            }
        }

        public void TailSweepTick(Vector3 centre, Vector3 tailDirection, float reach)
        {
            Vector3 d = FlatDir(tailDirection, Vector3.forward);
            float angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Vector3 tip = centre + d * reach;
            if (Chance(55f)) slash.Emit(OnGround(centre + d * reach * R(.55f, .9f), .3f), Vector3.zero, reach * R(.7f, 1.05f), R(.1f, .17f), new Color(1f, .65f, .28f, .85f), angle);
            if (Chance(80f)) spark.Emit(new Vector3(tip.x, .6f, tip.z), (d * R(2f, 6f) + new Vector3(-d.z, 0f, d.x) * R(-9f, 9f) + Vector3.up * R(0f, 3f)), R(.06f, .11f), R(.14f, .3f), new Color(1f, .7f, .35f));
            if (Chance(30f))
            {
                Vector3 at = centre + d * reach * R(.3f, 1f);
                smokeLow.Emit(new Vector3(at.x, .4f, at.z), Vector3.up * .3f, R(1f, 1.9f), R(.25f, .45f), new Color(.5f, .45f, .4f, .34f), R(0f, 360f));
            }
        }

        public void MeteorTick(Vector3 at, Vector3 motion, float size)
        {
            Vector3 back = motion.sqrMagnitude > .001f ? -motion.normalized : Vector3.up;
            if (meteorMaterial) meteorMaterial.SetVector("_Back", back);
            int n = Count(3);
            for (int i = 0; i < n; i++)
            {
                if (Chance(90f)) fireball.Emit(at + back * R(0f, size) + Random.insideUnitSphere * size * .3f, back * R(1f, 4f), size * R(.9f, 1.5f), R(.22f, .4f), new Color(1f, .75f, .5f, .95f), R(0f, 360f));
            }
            if (Chance(30f)) smoke.Emit(at + back * size, back * R(1f, 3f), size * R(1f, 1.7f), R(.5f, .9f), new Color(.22f, .19f, .17f, .55f), R(0f, 360f));
            if (Chance(30f)) ember.Emit(at, back * R(2f, 6f) + Random.insideUnitSphere * 2f, R(.06f, .12f), R(.3f, .6f), FireTint);
        }

        GameObject meteorRock;
        int meteorFrame;
        Material tailMaterial;

        public Material TailMaterial => tailMaterial ? tailMaterial : tailMaterial = Mat("tracer", true, 3f, 0f, 0f);

        Material meteorMaterial;

        public void MeteorBody(Vector3 at, float size)
        {
            if (!meteorRock) meteorRock = MakeRock();
            meteorRock.SetActive(true);
            meteorRock.transform.position = at;
            meteorRock.transform.localScale = Vector3.one * size;
            meteorRock.transform.Rotate(120f * Time.deltaTime, 70f * Time.deltaTime, 0f, Space.Self);
            meteorFrame = Time.frameCount;
        }

        void HideMeteorWhenDone()
        {
            if (meteorRock && meteorRock.activeSelf && Time.frameCount > meteorFrame + 1) meteorRock.SetActive(false);
        }

        GameObject MakeRock()
        {
            var faces = new List<(Vector3 a, Vector3 b, Vector3 c)>();
            Vector3[] o = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int[,] start = { { 0, 4, 3 }, { 0, 3, 5 }, { 0, 5, 2 }, { 0, 2, 4 }, { 1, 3, 4 }, { 1, 5, 3 }, { 1, 2, 5 }, { 1, 4, 2 } };
            for (int i = 0; i < 8; i++) faces.Add((o[start[i, 0]], o[start[i, 1]], o[start[i, 2]]));
            for (int level = 0; level < 4; level++)
            {
                var next = new List<(Vector3 a, Vector3 b, Vector3 c)>();
                foreach (var (a, b, c) in faces)
                {
                    Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                    next.Add((a, ab, ca)); next.Add((ab, b, bc)); next.Add((ca, bc, c)); next.Add((ab, bc, ca));
                }
                faces = next;
            }
            var index = new Dictionary<Vector3, int>();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int Corner(Vector3 v)
            {
                if (index.TryGetValue(v, out int i)) return i;

                float lumps = Mathf.PerlinNoise(v.x * 1.7f + 11f, v.y * 1.7f + v.z * 1.1f + 5f) * .55f + Mathf.PerlinNoise(v.z * 4.1f + 3f, v.x * 3.3f + v.y * 3.7f + 9f) * .2f;
                vertices.Add(v * (.66f + lumps));
                index[v] = vertices.Count - 1;
                return vertices.Count - 1;
            }
            foreach (var (a, b, c) in faces)
            {
                triangles.Add(Corner(a)); triangles.Add(Corner(b)); triangles.Add(Corner(c));
            }
            var mesh = new Mesh { name = "V5Rock" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            var go = new GameObject("V5 Meteor");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var shader = Resources.Load<Shader>("V5/V5Meteor");
            var stone = Resources.Load<Material>("V5/Terrain/V5WallMeteor");
            if (shader)
            {
                meteorMaterial = new Material(shader);
                meteorMaterial.SetTexture("_BaseMap", stone ? stone.GetTexture("_BaseMap") : Tex("noise"));
                meteorMaterial.SetTexture("_NoiseMap", Tex("noise"));
                r.sharedMaterial = meteorMaterial;
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public void AbsorbMote(Vector3 at, Vector3 velocity, float life)
        {
            ember.Emit(at, velocity, R(.09f, .18f), life, new Color(.78f, .52f, 1f));
            if (Random.value < .3f) spark.Emit(at, velocity, R(.05f, .09f), life, VoidTint);
        }

        public void HoleTick(Vector3 centre, float gravityRadius, float killRadius, bool active)
        {
            if (!active) return;
            float span = gravityRadius - killRadius;
            if (Chance(Mathf.Clamp(gravityRadius * 3f, 7f, 18f)))
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 d = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 at = centre + d * (killRadius + span * R(.4f, 1f)) + Vector3.up * .25f;
                Vector3 v = -d * span * R(1f, 1.8f) + new Vector3(-d.z, 0f, d.x) * span * R(.8f, 1.4f);
                ember.Emit(at, v, R(.05f, .1f), R(.35f, .7f), new Color(.75f, .5f, 1f));
            }
        }

        public void PortalTick(Vector3 centre, float radius)
        {
            if (Chance(12f))
            {
                Vector2 o = Random.insideUnitCircle.normalized * radius * R(.7f, 1.2f);
                ember.Emit(new Vector3(centre.x + o.x, .3f, centre.z + o.y), new Vector3(-o.x, 2.6f, -o.y) * .7f, R(.06f, .13f), R(.5f, .95f), new Color(.6f, .85f, 1f));
            }
            if (Chance(3.5f)) ringLayer.Emit(OnGround(centre, .1f), Vector3.zero, radius * 2.5f, .7f, new Color(.55f, .8f, 1f, .4f));
            if (Chance(5f)) groundGlow.Emit(OnGround(centre), Vector3.zero, radius * 4f, .45f, new Color(.32f, .55f, 1f, .4f));
        }

        readonly Dictionary<Transform, GameObject> mirrors = new();
        Material hologram;

        void TickMirrors()
        {
            var world = WorldMap.Active;
            if (!world) return;
            TickMirror(world.decoy);
            TickMirror(world.trialDecoy);
        }

        void TickMirror(Transform decoy)
        {
            if (!decoy) return;
            bool on = decoy.gameObject.activeInHierarchy && Art.Enabled;
            mirrors.TryGetValue(decoy, out var mirror);
            if (!on)
            {
                if (mirror) mirror.SetActive(false);
                return;
            }
            foreach (var r in decoy.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
            if (!mirror) mirror = mirrors[decoy] = MakeMirror();
            if (!mirror) return;
            mirror.SetActive(true);
            var gm = GameManager.Instance;
            mirror.transform.position = new Vector3(decoy.position.x, gm && gm.player ? gm.player.transform.position.y : 0f, decoy.position.z);
            if (gm && gm.player) mirror.transform.rotation = Quaternion.LookRotation(FlatDir(gm.player.AimDirection, Vector3.forward));
            BuffMotes(decoy.position, .8f, new Color(.5f, .85f, 1f), 9f);
            if (Chance(2.6f)) ringLayer.Emit(OnGround(decoy.position, .09f), Vector3.zero, 2.2f, .5f, new Color(.5f, .85f, 1f, .55f));
        }

        GameObject MakeMirror()
        {
            var gm = GameManager.Instance;
            var look = gm && gm.player ? gm.player.GetComponent<V2ModelPresentation>() : null;
            if (!look || !look.modelRoot) return null;
            var copy = Instantiate(look.modelRoot.gameObject, transform);
            copy.name = "V5 Mirror";
            copy.transform.localScale = look.modelRoot.lossyScale;
            if (!hologram) hologram = new Material(Resources.Load<Shader>("V5/V5Hologram"));
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = hologram;
                r.sharedMaterials = materials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var animator = copy.GetComponentInChildren<Animator>();
            if (animator)
            {
                animator.applyRootMotion = false;
                animator.speed = 1f;
                animator.Play("Base Layer.Idle", 0, 0f);
            }
            return copy;
        }

        GameObject trialBackdrop;
        Material nebula;
        bool platformDressed;
        Material platformMaterial, platformSaved;

        void DressTrialPlatform()
        {
            var world = WorldMap.Active;
            if (platformDressed == Art.Enabled || !world || !world.trialPlatform) return;
            var body = world.trialPlatform.GetComponent<Renderer>();
            if (!body) return;

            if (platformDressed)
            {
                body.sharedMaterial = platformSaved;
                platformDressed = false;
                return;
            }
            var shader = Resources.Load<Shader>("V5/V5Platform");
            if (!shader) return;
            if (!platformMaterial)
            {
                platformMaterial = new Material(shader);
                platformMaterial.SetTexture("_NoiseMap", Tex("noise"));
            }
            platformSaved = body.sharedMaterial;
            body.sharedMaterial = platformMaterial;
            platformDressed = true;
        }

        void TickTrial()
        {
            var world = WorldMap.Active;
            bool trial = world && world.TrialActive && world.trialPlatform && Art.Enabled;
            if (trial && !trialBackdrop)
            {
                trialBackdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
                trialBackdrop.name = "V5 Trial Backdrop";
                Destroy(trialBackdrop.GetComponent<Collider>());
                trialBackdrop.transform.SetParent(transform, false);
                trialBackdrop.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                trialBackdrop.transform.localScale = new Vector3(1600f, 1600f, 1f);
                nebula = new Material(Resources.Load<Shader>("V5/V5Nebula"));
                nebula.SetTexture("_NoiseMap", Tex("noise"));
                var r = trialBackdrop.GetComponent<MeshRenderer>();
                r.sharedMaterial = nebula;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            if (trialBackdrop)
            {
                trialBackdrop.SetActive(trial);
                if (trial)
                {
                    Vector3 centre = world.trialPlatform.position;
                    trialBackdrop.transform.position = new Vector3(centre.x, centre.y - 26f, centre.z);
                    nebula.SetVector("_Centre", new Vector4(centre.x, 0f, centre.z, 0f));

                    if (Chance(9f))
                    {
                        Vector2 o = Random.insideUnitCircle * 26f;
                        ember.Emit(new Vector3(centre.x + o.x, centre.y - 4f, centre.z + o.y), Vector3.up * R(.8f, 2f), R(.06f, .16f), R(1.4f, 2.6f), new Color(.62f, .42f, 1f, .75f));
                    }
                }
            }
        }

        readonly List<(ExperienceOrb orb, Color colour)> orbs = new();
        readonly HashSet<ExperienceOrb> dressedOrbs = new();

        public void Orb(ExperienceOrb orb, Color colour)
        {
            if (dressedOrbs.Add(orb)) orbs.Add((orb, colour));
            else for (int i = 0; i < orbs.Count; i++) if (orbs[i].orb == orb) orbs[i] = (orb, colour);
            if (orb.visual) orb.visual.sharedMaterial = OrbBody;
        }

        Material orbBody;
        Material OrbBody
        {
            get
            {
                if (orbBody) return orbBody;
                orbBody = Mat("glow", true, 2.6f, 0f, 0f);
                orbBody.SetTexture("_BaseMap", Texture2D.whiteTexture);
                return orbBody;
            }
        }

        void TickOrbs()
        {
            foreach (var (orb, colour) in orbs)
            {
                if (!orb || !orb.gameObject.activeInHierarchy) continue;
                Vector3 at = orb.transform.position;
                if (Chance(4f)) ember.Emit(at + Random.insideUnitSphere * .25f, Vector3.up * R(.3f, .9f), R(.04f, .08f), R(.3f, .6f), colour);
                if (Chance(5f)) groundGlow.Emit(OnGround(at), Vector3.zero, 1.5f, .3f, WithAlpha(colour, .3f));
            }
        }

        public void PillarTick(Vector3 centre, float radius)
        {
            if (Chance(26f))
            {
                Vector2 o = Random.insideUnitCircle * radius;
                ember.Emit(new Vector3(centre.x + o.x, .3f, centre.z + o.y), Vector3.up * R(4f, 9f), R(.07f, .15f), R(.4f, .8f), new Color(.85f, .6f, 1f));
            }
        }

        static Mesh flatQuad;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor"), MapId = Shader.PropertyToID("_BaseMap");
        MaterialPropertyBlock discBlock;

        public MeshRenderer MakeFlatDisc(string name, string texture, Transform parent)
        {
            if (!flatQuad)
            {
                flatQuad = new Mesh { name = "V5FlatQuad" };
                flatQuad.vertices = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f), new Vector3(.5f, 0f, -.5f) };
                flatQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                flatQuad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                flatQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                flatQuad.RecalculateBounds();
            }
            var go = new GameObject("V5 " + name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = flatQuad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = QuadAdditive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = -1;
            discBlock ??= new MaterialPropertyBlock();
            discBlock.Clear();
            discBlock.SetTexture(MapId, Tex(texture));
            r.SetPropertyBlock(discBlock);
            return r;
        }

        public static void PlaceDisc(MeshRenderer disc, Vector3 at, float radius, float height, float angle, Color color)
        {
            var t = disc.transform;
            t.SetPositionAndRotation(new Vector3(at.x, height, at.z), Quaternion.Euler(0f, angle, 0f));
            var p = t.parent ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(radius * 2f / Mathf.Max(.001f, Mathf.Abs(p.x)), 1f, radius * 2f / Mathf.Max(.001f, Mathf.Abs(p.z)));
            if (I)
            {
                I.discBlock ??= new MaterialPropertyBlock();
                disc.GetPropertyBlock(I.discBlock);
                I.discBlock.SetColor(ColorId, color);
                disc.SetPropertyBlock(I.discBlock);
            }
        }

        public void OrbitTrail(Vector3 at, Vector3 tangent, bool open, ElementKind element)
        {
            Color c = element != ElementKind.None ? ElementTint(element) : open ? new Color(1f, .62f, .3f) : new Color(.55f, .88f, 1f);
            if (Chance(34f)) spark.Emit(at + Vector3.up * .15f, FlatDir(tangent, Vector3.forward) * R(-1f, -4f) + Random.insideUnitSphere * .5f, R(.05f, .1f), R(.1f, .22f), c);
        }

        public void DroneGlow(Vector3 at)
        {
            if (Chance(8f)) ember.Emit(at, Vector3.down * R(.5f, 1.2f), R(.05f, .09f), R(.15f, .3f), new Color(.5f, .9f, 1f));
        }

        public void CoolingSmoke(Vector3 at)
        {
            smokeLow.Emit(at, Vector3.up * R(.7f, 1.1f), R(.6f, .9f), R(.7f, 1.1f), new Color(.62f, .62f, .66f, .42f), R(0f, 360f));
            if (Random.value < .5f) ember.Emit(at, new Vector3(R(-1f, 1f), R(1f, 2.4f), R(-1f, 1f)), R(.04f, .07f), R(.2f, .4f), FireTint);
        }

        public void TractorMotes(Vector3 from, Vector3 to)
        {
            if (Chance(24f)) ember.Emit(Vector3.Lerp(from, to, Random.value), (from - to).normalized * R(3f, 7f), R(.05f, .1f), R(.14f, .26f), new Color(.55f, .95f, 1f));
        }

        void TickFlight()
        {
            var gm = GameManager.Instance;
            var player = gm ? gm.player : null;
            if (!player || !Art.Enabled || player.State != CharacterState.Dashing) return;
            Vector3 at = Query.AtCastHeight(player.Position);
            Vector3 back = -FlatDir(player.AimDirection, Vector3.forward);
            if (Chance(70f)) fireball.Emit(at + back * .8f, back * R(4f, 9f) + Random.insideUnitSphere, R(1f, 1.7f), R(.2f, .36f), new Color(1f, .78f, .5f, .95f), R(0f, 360f));
            if (Chance(30f)) smokeLow.Emit(at + back * 1.4f, back * R(1f, 3f), R(.9f, 1.5f), R(.4f, .7f), new Color(.5f, .47f, .45f, .4f), R(0f, 360f));
        }

        public void ShotMote(Vector3 at, Color c)
        {
            if (Chance(14f)) ember.Emit(at, Random.insideUnitSphere * .4f, R(.06f, .12f), R(.16f, .3f), c);
        }

        public void RocketTrail(Vector3 at, Vector3 back)
        {
            if (Chance(70f)) fireball.Emit(at, back * R(2f, 5f) + Random.insideUnitSphere * 1.2f, R(.8f, 1.3f), R(.18f, .32f), new Color(1f, .75f, .5f, .95f), R(0f, 360f));
            if (Chance(34f)) smokeLow.Emit(at + back * .4f, back * R(1f, 2.4f), R(.6f, 1f), R(.35f, .6f), new Color(.55f, .52f, .5f, .4f), R(0f, 360f));
        }
    }
}
