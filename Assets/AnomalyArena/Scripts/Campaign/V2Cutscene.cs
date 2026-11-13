using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AnomalyArena
{
    /// <summary>
    /// The story cutscenes staged in whitebox. Each shot of the storyboard (the memory M1 to M9, the failed ignition L1 to L5, the ending
    /// E1 to E3) is a small set of the whitebox's solids, built when the shot starts, moved by its own clock and filmed by a camera of its
    /// own into a picture that the campaign's CG screen shows (V2CampaignUi.CgLive).
    /// Nothing of the run gets into that picture: the set stands far below the map on a layer only this camera draws, the camera has no
    /// post-processing (as the play camera in whitebox), and the night's darkness and the HUD are screen overlays, which a camera that
    /// draws into a texture never sees. The set is lit by the scene's own sun and ambient, the light the whitebox is seen in during play,
    /// so the colours are the game's; the stage is turned about the vertical so that the sun falls on each shot from the side the shot
    /// asks for (LightFrom), which keeps the tops lightest and the two sides facing the camera apart, as in play.
    /// Effects are what the whitebox uses elsewhere: small cubes of flat colour that fly and shrink away (Emit). Figures are rigid: they
    /// slide, turn and tilt. Time is unscaled: the run stands still while a cutscene plays.
    /// The shots are data (CutsceneShot), with their staging methods in V2Cutscene.Memory.cs, V2Cutscene.Ignition.cs and
    /// V2Cutscene.Ending.cs. V2Campaign.Story.cs plays them and says their lines.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class V2Cutscene : MonoBehaviour
    {
        /// <summary>Where the sets stand: far below the map, out of the play camera's reach and out of reach of the map's lamps.</summary>
        public static readonly Vector3 StageOrigin = new(0f, -4000f, 0f);
        /// <summary>The layer of everything on the stage; only the cutscene camera draws it. The project names no layer 31: nothing else is on it.</summary>
        public const int StageLayer = 31;
        // The picture: 16:9, the shape the CG screen gives it, smoothed with four samples (the solids' edges are long straight lines).
        const int Width = 1280, Height = 720, Samples = 4;
        // The camera's range: a ground thousands of metres wide reads as a straight horizon, and the stars of the space shots lie within it.
        const float Near = .25f, Far = 3000f;
        // Cubes in the air at once; beyond this the oldest makes way for a new one.
        const int MaxCubes = 700;
        // Degrees, clockwise seen from above, from the camera's first heading to where the sun comes from, unless a shot says otherwise:
        // from the right and a little behind, as the play camera sees the map.
        const float DefaultLight = 120f;

        /// <summary>Where the camera stands, the point it looks at and its vertical field of view, in the shot's own frame.</summary>
        public readonly struct CameraPose
        {
            public readonly Vector3 position, look;
            public readonly float fov;

            public CameraPose(Vector3 position, Vector3 look, float fov)
            {
                this.position = position;
                this.look = look;
                this.fov = fov;
            }
        }

        /// <summary>Turns a rate per second into whole cubes for each step, carrying what is left over to the next.</summary>
        public sealed class Spout
        {
            float carry;

            public int Take(float perSecond, float seconds)
            {
                carry += Mathf.Max(0f, perSecond) * Mathf.Max(0f, seconds);
                int whole = (int)carry;
                carry -= whole;
                return whole;
            }
        }

        sealed class Cube
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Vector3 velocity, spinAxis;
            public Color from, to;
            public float age, life, size, gravity, spin;
            public bool live;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock block;

        Camera view;
        RenderTexture target;
        // The stage (turned toward the sun for each shot), the shot's set on it, and the cubes' pool beside the set.
        Transform stage, set, dust;
        CutsceneShot shot;
        float clock, step, shake, lightTurn;
        Func<float, CameraPose> cameraPath;
        Transform subject;
        Vector3 subjectOffset;
        readonly List<Action<float>> motions = new();
        readonly List<Mesh> madeMeshes = new();
        readonly List<Cube> cubes = new();
        // Cubes that have flown their life and wait to be thrown again; when none waits and the pool is full, the oldest goes.
        readonly Stack<Cube> spare = new();
        int nextCube;

        /// <summary>A stage for the story cutscenes, on an object of its own at the scene's root.</summary>
        public static V2Cutscene Create(string name = "Cutscene stage") => new GameObject(name).AddComponent<V2Cutscene>();

        /// <summary>The picture the camera draws (null before Begin and after End).</summary>
        public Texture Picture => target;
        /// <summary>The camera that films the sets.</summary>
        public Camera View => view;
        /// <summary>The shot on the stage now (null: none).</summary>
        public CutsceneShot Shot => shot;
        /// <summary>Seconds since the shot on the stage began.</summary>
        public float Clock => clock;
        /// <summary>Seconds the current step moves the shot on (for rates: Spout.Take).</summary>
        public float Step => step;
        /// <summary>Length of the shot on the stage.</summary>
        public float Seconds => shot != null ? shot.seconds : 1f;
        /// <summary>Cubes in the air now.</summary>
        public int LiveCubes
        {
            get
            {
                int live = 0;
                foreach (var cube in cubes) if (cube.live) live++;
                return live;
            }
        }

        /// <summary>For automated checks: the shot's main figure (where it is, in the world), and whether one was named.</summary>
        public bool TrySubjectPoint(out Vector3 point)
        {
            point = subject ? subject.TransformPoint(subjectOffset) : default;
            return subject;
        }

        // ───── The stage's life ─────

        /// <summary>Makes the camera and its picture, once for a cutscene; End lets them go.</summary>
        public void Begin()
        {
            if (!stage)
            {
                stage = new GameObject("Stage") { layer = StageLayer }.transform;
                stage.SetParent(transform, false);
                stage.position = StageOrigin;
                dust = new GameObject("Cubes") { layer = StageLayer }.transform;
                dust.SetParent(stage, false);
            }

            if (!view) MakeCamera();
            if (!target)
            {
                target = new RenderTexture(Width, Height, 24) { name = "Cutscene picture", antiAliasing = Samples };
                target.Create();
            }

            view.targetTexture = target;
        }

        /// <summary>Puts a shot on the stage: the last shot's set goes, this one is built, turned to the sun and filmed from its first frame.</summary>
        public void Show(CutsceneShot next)
        {
            Begin();
            ClearSet();
            shot = next;
            clock = step = shake = 0f;
            lightTurn = DefaultLight;
            view.backgroundColor = Color.black;
            set = new GameObject("Set " + next.id) { layer = StageLayer }.transform;
            set.SetParent(stage, false);
            next.stage?.Invoke(this);
            TurnToTheSun();
            view.enabled = true;
            Advance(0f);
        }

        /// <summary>The stage shows nothing (a shot shown as a picture, or the cutscene is over): the set goes and the camera stops.</summary>
        public void Hide()
        {
            ClearSet();
            shot = null;
            if (view) view.enabled = false;
        }

        /// <summary>The cutscene is over: everything on the stage goes and the picture is let go.</summary>
        public void End()
        {
            Hide();
            cubes.Clear();
            spare.Clear();
            nextCube = 0;
            if (stage)
            {
                stage.gameObject.SetActive(false); // gone from this frame's picture already
                Destroy(stage.gameObject);
            }

            stage = set = dust = null;
            ReleasePicture();
        }

        /// <summary>Moves the shot on by seconds: its motions, its cubes and its camera. The stage calls it each frame with unscaled time;
        /// automated checks call it directly.</summary>
        public void Advance(float seconds)
        {
            if (shot == null) return;
            step = Mathf.Max(0f, seconds);
            clock += step;
            shake = 0f;
            for (int i = 0; i < motions.Count; i++) motions[i](clock);
            MoveCubes(step);
            PlaceCamera();
        }

        void LateUpdate()
        {
            if (shot != null) Advance(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            foreach (var mesh in madeMeshes)
                if (mesh) Destroy(mesh);
            madeMeshes.Clear();
            ReleasePicture();
        }

        void ReleasePicture()
        {
            if (view) view.targetTexture = null;
            if (!target) return;
            target.Release();
            Destroy(target);
            target = null;
        }

        void ClearSet()
        {
            motions.Clear();
            cameraPath = null;
            subject = null;
            if (set)
            {
                set.gameObject.SetActive(false); // gone from this frame's picture already; Destroy waits for the frame's end
                Destroy(set.gameObject);
            }

            set = null;
            foreach (var mesh in madeMeshes)
                if (mesh) Destroy(mesh);
            madeMeshes.Clear();
            spare.Clear();
            foreach (var cube in cubes)
            {
                cube.live = false;
                if (cube.renderer) cube.renderer.enabled = false;
                spare.Push(cube);
            }
        }

        void MakeCamera()
        {
            var go = new GameObject("Cutscene camera");
            go.transform.SetParent(transform, false);
            view = go.AddComponent<Camera>();
            view.enabled = false;
            view.cullingMask = 1 << StageLayer;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.black;
            view.nearClipPlane = Near;
            view.farClipPlane = Far;
            view.allowHDR = false;
            // The whitebox's own look: no bloom or grade (the play camera has none in whitebox either), no shadows, no volume.
            var data = view.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            data.volumeLayerMask = 0;
        }

        // ───── Light and camera ─────

        /// <summary>The sun falls on this shot from turn degrees clockwise (seen from above) of the camera's first heading: 90 from the
        /// right, 180 from straight behind the camera.</summary>
        public void LightFrom(float turn) => lightTurn = turn;

        /// <summary>The flat colour behind everything: the sky, or the dark of space.</summary>
        public void Sky(Color colour) => view.backgroundColor = colour;

        /// <summary>The camera's way through the shot, as a pose for each moment (seconds from the shot's start).</summary>
        public void CameraPath(Func<float, CameraPose> path) => cameraPath = path;

        /// <summary>The camera eases from one pose to the other over the whole shot.</summary>
        public void CameraMove(Vector3 fromPosition, Vector3 fromLook, Vector3 toPosition, Vector3 toLook, float fov, float toFov = 0f)
        {
            float seconds = Seconds, endFov = toFov > 0f ? toFov : fov;
            cameraPath = t =>
            {
                float k = Smooth(t / seconds);
                return new CameraPose(Vector3.Lerp(fromPosition, toPosition, k), Vector3.Lerp(fromLook, toLook, k), Mathf.Lerp(fov, endFov, k));
            };
        }

        /// <summary>Shakes the camera by up to amount metres in this step (motions ask for it every step they want it; the largest wins).</summary>
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void PlaceCamera()
        {
            if (!view || !stage || cameraPath == null) return;
            var pose = cameraPath(clock);
            Vector3 direction = pose.look - pose.position;
            if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 position = pose.position;
            if (shake > 0f)
            {
                float a = clock * 23f;
                position += rotation * new Vector3(Mathf.PerlinNoise(a, 1.7f) - .5f, Mathf.PerlinNoise(3.1f, a) - .5f, 0f) * (2f * shake);
            }

            view.transform.SetPositionAndRotation(stage.TransformPoint(position), stage.rotation * rotation);
            view.fieldOfView = pose.fov;
        }

        /// <summary>Turns the stage about the vertical so the scene's sun comes from the side the shot asked for (LightFrom), measured
        /// from the camera's heading at the shot's first frame. Everything on the stage turns with it, the camera too, so only the
        /// light changes.</summary>
        void TurnToTheSun()
        {
            stage.rotation = Quaternion.identity;
            if (cameraPath == null) return;
            var first = cameraPath(0f);
            Vector3 heading = first.look - first.position, toSun = SunDirection();
            heading.y = 0f;
            toSun.y = 0f;
            if (heading.sqrMagnitude < 1e-6f || toSun.sqrMagnitude < 1e-6f) return;
            Vector3 wanted = Quaternion.Euler(0f, lightTurn, 0f) * heading.normalized;
            stage.rotation = Quaternion.Euler(0f, Vector3.SignedAngle(wanted, toSun.normalized, Vector3.up), 0f);
        }

        /// <summary>The way toward the scene's sun, found as the arena's lighting finds it (Biome.ApplyLighting): RenderSettings.sun, else
        /// the first directional light that is on. Straight up when there is none.</summary>
        static Vector3 SunDirection()
        {
            var sun = RenderSettings.sun;
            if (!sun || !sun.isActiveAndEnabled)
            {
                sun = null;
                foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                    if (light.type == LightType.Directional && light.enabled) { sun = light; break; }
            }

            return sun ? -sun.transform.forward : Vector3.up;
        }

        // ───── Building a set (the shot's own frame: metres, y up, the ground at y = 0) ─────

        /// <summary>The parent of the shot's set.</summary>
        public Transform SetRoot => set;

        /// <summary>The shot's main figure, for automated checks: offset is in the figure's own frame.</summary>
        public void Subject(Transform figure, Vector3 offset = default)
        {
            subject = figure;
            subjectOffset = offset;
        }

        /// <summary>An empty holder for parts that move together.</summary>
        public Transform Node(string name, Vector3 at, Vector3 turn = default, Transform parent = null)
        {
            var node = new GameObject(name) { layer = StageLayer }.transform;
            node.SetParent(parent ? parent : set, false);
            node.localPosition = at;
            node.localRotation = Quaternion.Euler(turn);
            return node;
        }

        /// <summary>One solid of the whitebox (the table's shapes: Whitebox.Prim) in one flat colour, size metres, centred at at.</summary>
        public MeshRenderer Solid(Whitebox.Prim prim, Vector3 size, Vector3 at, Color colour, Vector3 turn = default, Transform parent = null)
        {
            var part = new Whitebox.Part(prim, size.x, size.y, size.z, 0f, 0f, 0f, Whitebox.MarkColour, 1f);
            var go = new GameObject(prim.ToString()) { layer = StageLayer };
            var t = go.transform;
            t.SetParent(parent ? parent : set, false);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(turn);
            t.localScale = Whitebox.PartScale(part);
            go.AddComponent<MeshFilter>().sharedMesh = Whitebox.MeshFor(part);
            var r = go.AddComponent<MeshRenderer>();
            Dress(r);
            Paint(r, colour);
            return r;
        }

        public MeshRenderer Box(Vector3 size, Vector3 at, Color colour, Vector3 turn = default, Transform parent = null) =>
            Solid(Whitebox.Prim.Box, size, at, colour, turn, parent);

        /// <summary>The ground: one flat box far wider than the camera's range, so its far edge reads as a straight horizon.</summary>
        public MeshRenderer Ground(Color colour, float size = 5000f) => Box(new Vector3(size, 1f, size), new Vector3(0f, -.5f, 0f), colour);

        /// <summary>A look of the whitebox table (the player, the ship, the wreck, the base, the lamp, the Tyrant...), feet on at.</summary>
        public WhiteboxLook Look(string id, Vector3 at, Vector3 turn = default, float scale = 1f, Transform parent = null)
        {
            var root = Whitebox.Build(id, parent ? parent : set);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(turn);
            root.localScale = Vector3.one * scale;
            return root.GetComponent<WhiteboxLook>();
        }

        /// <summary>Felix as the player is in the whitebox: the cyan box with the white wedge on its front; yaw 0 faces +z.</summary>
        public WhiteboxLook Felix(Vector3 at, float yaw, Transform parent = null) => Look("player", at, new Vector3(0f, yaw, 0f), 1f, parent);

        /// <summary>A square bar from one point to another (an arm, a beam, a segment of a zigzag).</summary>
        public Transform Bar(Vector3 from, Vector3 to, float width, Color colour, Transform parent = null)
        {
            var bar = Box(Vector3.one, from, colour, default, parent).transform;
            Lay(bar, from, to, width);
            return bar;
        }

        /// <summary>Lays a bar of the set from one point to another, in its parent's frame.</summary>
        public static void Lay(Transform bar, Vector3 from, Vector3 to, float width)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            bar.localPosition = (from + to) * .5f;
            if (length > 1e-5f) bar.localRotation = Quaternion.LookRotation(d / length, Mathf.Abs(d.y) > .99f * length ? Vector3.forward : Vector3.up);
            bar.localScale = new Vector3(width, width, Mathf.Max(1e-4f, length));
        }

        /// <summary>A square frame of four bars standing on its corner (a diamond) and turned twist degrees further, facing along z: the
        /// tunnel of the wormhole and the jump gate.</summary>
        public Transform Frame(Vector3 at, float size, float bar, Color colour, float twist, Transform parent = null)
        {
            var frame = Node("Frame", at, new Vector3(0f, 0f, 45f + twist), parent);
            float half = size * .5f, across = size + bar;
            Box(new Vector3(across, bar, bar), new Vector3(0f, half, 0f), colour, default, frame);
            Box(new Vector3(across, bar, bar), new Vector3(0f, -half, 0f), colour, default, frame);
            Box(new Vector3(bar, across, bar), new Vector3(half, 0f, 0f), colour, default, frame);
            Box(new Vector3(bar, across, bar), new Vector3(-half, 0f, 0f), colour, default, frame);
            return frame;
        }

        /// <summary>Many solids of one shape and colour in one mesh (a city, ranks of soldiers, rocks, stars): one renderer instead of
        /// hundreds. Each placement is the solid's position, turn and size.</summary>
        public MeshRenderer Batch(Whitebox.Prim prim, List<Matrix4x4> placements, Color colour, string name, Transform parent = null)
        {
            var unit = Whitebox.MeshFor(new Whitebox.Part(prim, 1f, 1f, 1f, 0f, 0f, 0f, Whitebox.MarkColour, 1f));
            var combine = new CombineInstance[placements.Count];
            for (int i = 0; i < combine.Length; i++) combine[i] = new CombineInstance { mesh = unit, transform = placements[i] };
            var mesh = new Mesh { name = "Cutscene " + name, hideFlags = HideFlags.DontSave };
            if ((long)unit.vertexCount * combine.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(combine, true, true);
            madeMeshes.Add(mesh);
            var go = new GameObject(name) { layer = StageLayer };
            go.transform.SetParent(parent ? parent : set, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            Dress(r);
            Paint(r, colour);
            return r;
        }

        /// <summary>A point of a figure in the stage's frame, the frame cubes fly in.</summary>
        public Vector3 OnStage(Transform figure, Vector3 local) => stage.InverseTransformPoint(figure.TransformPoint(local));

        /// <summary>A direction of a figure in the stage's frame.</summary>
        public Vector3 OnStageDirection(Transform figure, Vector3 local) => stage.InverseTransformDirection(figure.TransformDirection(local));

        /// <summary>Recolours one solid (a property block over the shared whitebox material, so nothing is allocated).</summary>
        public static void Paint(Renderer r, Color colour)
        {
            if (!r) return;
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(BaseColorId, colour);
            r.SetPropertyBlock(block);
        }

        /// <summary>Recolours every solid under a holder (a frame, a figure built of solids).</summary>
        public static void PaintAll(Transform holder, Color colour)
        {
            if (!holder) return;
            foreach (var r in holder.GetComponentsInChildren<MeshRenderer>()) Paint(r, colour);
        }

        static void Dress(MeshRenderer r)
        {
            r.sharedMaterial = Whitebox.Opaque;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        // ───── Motion ─────

        /// <summary>Runs every step of the shot with its clock (seconds from the start), from the first frame on.</summary>
        public void Every(Action<float> motion)
        {
            if (motion != null) motions.Add(motion);
        }

        /// <summary>One small cube of flat colour that flies from at (the stage's frame) with velocity, falls with gravity, tumbles,
        /// turns from colour toward fadeTo and shrinks away over life seconds: the whitebox's fire, smoke, dust, exhaust and sparks.</summary>
        public void Emit(Vector3 at, Vector3 velocity, Color colour, float size, float life, float gravity = 0f, Color? fadeTo = null)
        {
            if (!dust) return;
            var cube = NextCube();
            cube.live = true;
            cube.age = 0f;
            cube.life = Mathf.Max(.05f, life);
            cube.size = size;
            cube.gravity = gravity;
            cube.velocity = velocity;
            cube.from = colour;
            cube.to = fadeTo ?? colour;
            cube.spinAxis = UnityEngine.Random.onUnitSphere;
            cube.spin = UnityEngine.Random.Range(60f, 240f);
            var t = cube.transform;
            t.localPosition = at;
            t.localRotation = UnityEngine.Random.rotation;
            t.localScale = Vector3.one * size;
            Paint(cube.renderer, colour);
            cube.renderer.enabled = true;
        }

        Cube NextCube()
        {
            while (spare.Count > 0)
            {
                var free = spare.Pop();
                if (!free.live) return free;
            }

            if (cubes.Count < MaxCubes)
            {
                var part = new Whitebox.Part(Whitebox.Prim.Box, 1f, 1f, 1f, 0f, 0f, 0f, Whitebox.MarkColour, 1f);
                var go = new GameObject("Cube") { layer = StageLayer };
                go.transform.SetParent(dust, false);
                go.AddComponent<MeshFilter>().sharedMesh = Whitebox.MeshFor(part);
                var r = go.AddComponent<MeshRenderer>();
                Dress(r);
                var made = new Cube { transform = go.transform, renderer = r };
                cubes.Add(made);
                return made;
            }

            nextCube = (nextCube + 1) % cubes.Count;
            return cubes[nextCube];
        }

        void MoveCubes(float dt)
        {
            if (dt <= 0f) return;
            foreach (var cube in cubes)
            {
                if (!cube.live) continue;
                cube.age += dt;
                if (cube.age >= cube.life)
                {
                    cube.live = false;
                    cube.renderer.enabled = false;
                    spare.Push(cube);
                    continue;
                }

                float k = cube.age / cube.life;
                cube.velocity.y -= cube.gravity * dt;
                var t = cube.transform;
                t.localPosition += cube.velocity * dt;
                t.localRotation = Quaternion.AngleAxis(cube.spin * dt, cube.spinAxis) * t.localRotation;
                // Full size for the first third of its life, then it shrinks to nothing: a cube fades by getting smaller.
                t.localScale = Vector3.one * (cube.size * Mathf.Clamp01((1f - k) * 1.5f));
                if (cube.from != cube.to) Paint(cube.renderer, Color.Lerp(cube.from, cube.to, k));
            }
        }

        // ───── Shapes of time ─────

        /// <summary>0 to 1 over 0 to 1, starting and ending gently.</summary>
        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>0 before start, 1 after start + length, a straight line between.</summary>
        public static float Ramp(float t, float start, float length) => Mathf.Clamp01((t - start) / Mathf.Max(1e-4f, length));

        /// <summary>0 to 1, fast at first and settling at the end.</summary>
        public static float EaseOut(float x)
        {
            x = 1f - Mathf.Clamp01(x);
            return 1f - x * x * x;
        }

        /// <summary>A number between a and b from a seeded random source: the same set every time the shot is built.</summary>
        static float R(System.Random random, float a, float b) => a + (float)random.NextDouble() * (b - a);

        /// <summary>A number between a and b, different every call: for cubes.</summary>
        static float J(float a, float b) => UnityEngine.Random.Range(a, b);

        static Vector3 Jitter(float x, float y, float z) => new(J(-x, x), J(-y, y), J(-z, z));

        // ───── The palette (the table's colours where it has them, Whitebox.Colour) ─────

        static Color Table(string key) => Whitebox.Colour(key);

        // Ground, rock and sky. The sand is the colour that shows as the map's ground under the game's daylight.
        static readonly Color Sand = new(.43f, .34f, .26f), SandDusk = new(.38f, .29f, .22f), Groove = new(.31f, .24f, .18f), Rock = new(.4f, .31f, .23f);
        static readonly Color Footprint = new(.29f, .22f, .16f);
        static readonly Color DawnSky = new(.64f, .7f, .78f), DuskSky = new(.05f, .06f, .11f), NightSky = new(.02f, .025f, .06f), SpaceSky = new(.01f, .01f, .02f);
        // The rooms of the ship: dark slate-blue walls and a darker floor.
        static readonly Color Slate = new(.17f, .21f, .3f), SlateFloor = new(.1f, .12f, .17f), SlateCeiling = new(.13f, .16f, .23f);
        // Effects: fire, smoke, exhaust, dust and dark matter as flat cubes.
        static readonly Color Smoke = new(.25f, .25f, .27f), ExhaustCyan = new(.6f, .95f, 1f), DullOrange = new(.72f, .4f, .2f), Violet = new(.75f, .35f, 1f);
        // The ship's parts as the base panel paints them (V2Campaign.Base.cs): a broken engine, an empty tank, and a full one lit.
        static readonly Color BrokenGrey = new(.3f, .3f, .32f), EmptyTank = new(.2f, .1f, .25f), FullTank = new(.92f, .5f, 1f);
        // The Tyrant's colour struck down, and the flash of its red.
        static readonly Color TyrantDull = new(.42f, .09f, .11f), TyrantFlash = new(1f, .42f, .4f);
    }
}
