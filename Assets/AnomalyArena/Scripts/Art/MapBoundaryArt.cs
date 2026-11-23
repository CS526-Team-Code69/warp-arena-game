using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// Draws the map's boundary. The view follows the player past the map's sides (CameraRig.KeepInFrame), so near a side it shows
    /// ground beyond the map; this marks where the map ends, in two parts that say the same thing in both looks.
    /// A low wall stands along all four sides just outside the map, its inner face on the map's edge, where the scene's unseen walls
    /// already stop every body. It is made as the map's own walls are: a box of the map's wall colour in the whitebox look, the
    /// map's stone in the illustrated one (V5TerrainWallArt, so it gets its shadow and its rubble on the ground like any wall). It is
    /// kept low, WhiteboxHeight and StoneHeightShare of a map wall, so that on the camera's side it hides nothing that stands inside
    /// the map next to it.
    /// Past the wall the ground goes dark: it takes the colour of the void the camera clears to, the one the trial's platform floats
    /// in. In the whitebox look the dark begins at the wall, a flat field; in the illustrated one the ground sinks into it over
    /// FadeCells. The dark is one flat mesh laid just over the floor, out to the floor's own edge.
    /// Nothing here has a collider and the game reads none of it: walking, the path grid, lines of sight and physics are as they
    /// were. The wall is cut into pieces of PieceLength; only those within ShowRange of where the camera looks are switched on,
    /// and a piece's stone is built the first time it is. It is built again when the map changes size (the story's larger map,
    /// and the classic one coming back), follows the art switch, and is not drawn while the void trial runs, whose platform lies
    /// off the map with nothing but the void round it.
    /// V2BiomeArt attaches it to the floor it dresses.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(420)]
    public sealed class MapBoundaryArt : MonoBehaviour
    {
        public const string RootName = "Map Boundary";
        /// <summary>Cells the wall is wide, outward from the map's edge.</summary>
        public const float WallWidth = 2f;
        /// <summary>Height of the whitebox wall in cells, and the share of a stone wall's own height the stone one keeps.</summary>
        public const float WhiteboxHeight = 1f, StoneHeightShare = .55f;
        /// <summary>Cells past the wall over which the ground sinks into the dark in the illustrated look, and how dark it already
        /// is at the wall's foot there (0 to 1).</summary>
        public const float FadeCells = 6f, FadeStart = .5f;

        // The dark lies ShadeLift over the floor: under the warning circles and other marks the game draws on the ground (at .03).
        const float PieceLength = 20f, ShowRange = 42f, HideRange = 50f, ShadeLift = .012f;
        const int BuildsPerFrame = 2;
        // The void the camera clears to in the arena scenes, for a camera that clears to something else.
        static readonly Color DefaultVoid = new(.025f, .035f, .07f, 1f);
        static Mesh cube;

        sealed class Piece
        {
            public GameObject body;
            public MeshRenderer box;
            public Rect rect;
            public V5TerrainWallArt stone;
            public bool dressed;
        }

        WorldMap world;
        Renderer floor;
        Transform rim;
        MeshRenderer shade;
        Mesh shadeMesh;
        Material shadeMaterial;
        readonly List<Piece> pieces = new();
        readonly Color32[] shadeColours = new Color32[16];

        Vector3 builtSize;
        Bounds builtFloor;
        Biome builtBiome;
        bool built, builtStone, builtArt, hidden;
        V5WallStyle builtStyle;

        /// <summary>The boundary of the map in play, or null.</summary>
        public static MapBoundaryArt Current { get; private set; }

        /// <summary>The map's rectangle the boundary was last built round.</summary>
        public Bounds Inner => new(Vector3.zero, builtSize);
        /// <summary>The wall's pieces: their rectangles on the ground (x, z), all outside the map.</summary>
        public IEnumerable<Rect> PieceRects { get { foreach (var piece in pieces) yield return piece.rect; } }
        public int PieceCount => pieces.Count;
        /// <summary>Pieces switched on now (those near where the camera looks).</summary>
        public int ShownPieces { get { int n = 0; foreach (var piece in pieces) if (piece.body && piece.body.activeInHierarchy) n++; return n; } }
        /// <summary>The dark past the wall is drawn now.</summary>
        public bool ShadeShown => shade && shade.enabled && shade.gameObject.activeInHierarchy;
        /// <summary>The wall is the map's stone now (the illustrated look on a map built from stone); otherwise boxes.</summary>
        public bool StoneShown => builtStone && Art.Enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            Current = null;
            cube = null;
        }

        /// <summary>Gives the map its boundary, once; a second call returns the one it has.</summary>
        public static MapBoundaryArt Attach(WorldMap world, Renderer floor)
        {
            if (!world || !floor) return null;
            if (Current && Current.world == world) return Current;
            var root = new GameObject(RootName);
            root.transform.SetParent(floor.transform.parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // The pieces are laid out in cells whatever scale the floor's parent has.
            Vector3 parentScale = root.transform.parent ? root.transform.parent.lossyScale : Vector3.one;
            if (parentScale.x != 0f && parentScale.y != 0f && parentScale.z != 0f)
                root.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
            var art = root.AddComponent<MapBoundaryArt>();
            art.world = world;
            art.floor = floor;
            Current = art;
            return art;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (shadeMesh) Destroy(shadeMesh);
            if (shadeMaterial) Destroy(shadeMaterial);
        }

        void LateUpdate()
        {
            if (!world || !floor) return;
            // The void trial's platform lies off the map with the void all round it: the map's boundary is not drawn meanwhile.
            bool trial = world.TrialActive;
            if (hidden != trial && rim)
            {
                hidden = trial;
                rim.gameObject.SetActive(!trial);
                if (shade) shade.gameObject.SetActive(!trial);
            }

            if (trial) return;
            if (Stale()) Build();
            if (builtArt != Art.Enabled)
            {
                builtArt = Art.Enabled;
                PaintShade();
                foreach (var piece in pieces) piece.dressed = false;
            }

            ShowNearPieces();
        }

        bool Stale()
        {
            if (!built) return true;
            Bounds ground = floor.bounds;
            bool stone = StoneOf(world, out var style);
            return world.Bounds.size != builtSize || world.biome != builtBiome || ground.size != builtFloor.size || ground.center != builtFloor.center
                   || stone != builtStone || stone && style != builtStyle;
        }

        /// <summary>The stone the map's own walls are built from; false for a map that keeps saved wall art (it gets boxes).</summary>
        static bool StoneOf(WorldMap world, out V5WallStyle style)
        {
            style = V5WallStyle.Ruins;
            int index = world.BiomeIndex;
            return index >= 0 && world.biomeWallGroups != null && index < world.biomeWallGroups.Length
                   && V5TerrainWalls.StyleOfGroup(world.biomeWallGroups[index], out style);
        }

        void Build()
        {
            foreach (var piece in pieces)
                if (piece.body) Destroy(piece.body);
            pieces.Clear();
            if (!rim)
            {
                rim = new GameObject("Rim").transform;
                rim.SetParent(transform, false);
            }

            built = true;
            Bounds map = world.Bounds;
            builtSize = map.size;
            builtFloor = floor.bounds;
            builtBiome = world.biome;
            builtStone = StoneOf(world, out builtStyle);
            builtArt = Art.Enabled;
            float hx = map.extents.x, hz = map.extents.z, w = WallWidth;
            // North and south run the whole width with the corners; east and west fill in between them.
            AddSide(new Rect(-hx - w, hz, hx * 2f + w * 2f, w), true);
            AddSide(new Rect(-hx - w, -hz - w, hx * 2f + w * 2f, w), true);
            AddSide(new Rect(hx, -hz, w, hz * 2f), false);
            AddSide(new Rect(-hx - w, -hz, w, hz * 2f), false);
            BuildShade(map, builtFloor);
            PaintShade();
        }

        void AddSide(Rect side, bool alongX)
        {
            float length = alongX ? side.width : side.height;
            int count = Mathf.Max(1, Mathf.CeilToInt(length / PieceLength - .001f));
            float step = length / count;
            if (!cube) cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Material wall = world.biome ? world.biome.wall : null;
            for (int i = 0; i < count; i++)
            {
                Rect rect = alongX ? new Rect(side.xMin + step * i, side.yMin, step, side.height) : new Rect(side.xMin, side.yMin + step * i, side.width, step);
                // A box with no collider: only something to look at.
                var body = new GameObject("Rim_" + pieces.Count.ToString("00"), typeof(MeshFilter), typeof(MeshRenderer));
                body.transform.SetParent(rim, false);
                body.transform.position = new Vector3(rect.center.x, WhiteboxHeight * .5f, rect.center.y);
                body.transform.localScale = new Vector3(rect.width, WhiteboxHeight, rect.height);
                body.GetComponent<MeshFilter>().sharedMesh = cube;
                var box = body.GetComponent<MeshRenderer>();
                box.sharedMaterial = wall;
                box.shadowCastingMode = ShadowCastingMode.Off;
                box.receiveShadows = false;
                box.reflectionProbeUsage = ReflectionProbeUsage.Off;
                body.SetActive(false);
                pieces.Add(new Piece { body = body, box = box, rect = rect });
            }
        }

        /// <summary>Switches on the pieces near where the camera looks and off those far from it, and gives a piece that came on,
        /// or whose look is out of date, its look: at most BuildsPerFrame a frame, since a stone is built the first time.</summary>
        void ShowNearPieces()
        {
            Vector3 focus = Focus();
            int dressedNow = 0;
            foreach (var piece in pieces)
            {
                if (!piece.body) continue;
                float away = Away(piece.rect, focus);
                bool on = piece.body.activeSelf;
                if (on && away > HideRange)
                {
                    piece.body.SetActive(false);
                    continue;
                }

                if (on ? piece.dressed : away > ShowRange) continue;
                if (dressedNow >= BuildsPerFrame) continue; // the next frame takes it
                if (!on) piece.body.SetActive(true);
                Dress(piece);
                dressedNow++;
            }
        }

        /// <summary>The piece's look for the art switch as it stands: its box, or the map's stone in the illustrated look.</summary>
        void Dress(Piece piece)
        {
            piece.dressed = true;
            bool stone = builtStone && Art.Enabled;
            piece.box.enabled = !stone;
            if (!builtStone) return;
            if (!piece.stone) piece.stone = V5TerrainWallArt.Attach(piece.body, builtStyle);
            piece.stone.Refresh(); // builds the stone now, or puts it away in the whitebox look
            if (!stone || !piece.stone.Stone) return;
            // The stone stands on the ground at its own size, whatever the box measures; here it keeps a share of its height.
            Vector3 scale = piece.body.transform.lossyScale;
            piece.stone.Stone.localScale = new Vector3(1f / scale.x, StoneHeightShare / scale.y, 1f / scale.z);
        }

        /// <summary>Where the camera looks on the ground; the map's middle without a camera.</summary>
        static Vector3 Focus()
        {
            var game = GameManager.Instance;
            Camera cam = game && game.cam ? game.cam : Camera.main;
            if (!cam) return Vector3.zero;
            var t = cam.transform;
            if (t.forward.y > -.01f) return new Vector3(t.position.x, 0f, t.position.z);
            return t.position + t.forward * (-t.position.y / t.forward.y);
        }

        static float Away(Rect rect, Vector3 point)
        {
            float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax), dz = Mathf.Max(rect.yMin - point.z, 0f, point.z - rect.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ───── The dark past the wall ─────

        /// <summary>Four rectangles one inside the other, joined by flat bands: the map's edge, the wall's outer face, the end of
        /// the fade and the floor's edge. PaintShade sets how dark each is.</summary>
        void BuildShade(Bounds map, Bounds ground)
        {
            if (!shade)
            {
                var shader = Resources.Load<Shader>("V5/V5Particle");
                var holder = new GameObject("Dark", typeof(MeshFilter), typeof(MeshRenderer));
                holder.transform.SetParent(transform, false);
                shade = holder.GetComponent<MeshRenderer>();
                shade.shadowCastingMode = ShadowCastingMode.Off;
                shade.receiveShadows = false;
                shade.lightProbeUsage = LightProbeUsage.Off;
                shade.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (shader)
                {
                    shadeMaterial = new Material(shader) { name = "Map boundary dark", hideFlags = HideFlags.DontSave };
                    // Under every effect drawn on the ground: a warning circle that reaches past the map's edge still shows on it.
                    shadeMaterial.renderQueue = (int)RenderQueue.Transparent - 100;
                    shade.sharedMaterial = shadeMaterial;
                }
                else shade.enabled = false;
                shadeMesh = new Mesh { name = "Map boundary dark", hideFlags = HideFlags.DontSave };
                holder.GetComponent<MeshFilter>().sharedMesh = shadeMesh;
            }

            float y = ground.max.y + ShadeLift;
            // The floor's edge, a hair inside it, and never inside the rings it closes.
            float left = ground.min.x + .05f, right = ground.max.x - .05f, back = ground.min.z + .05f, ahead = ground.max.z - .05f;
            var points = new Vector3[16];
            for (int ring = 0; ring < 4; ring++)
            {
                float grow = ring == 0 ? 0f : ring == 1 ? WallWidth : WallWidth + FadeCells;
                float x0 = -map.extents.x - grow, x1 = map.extents.x + grow, z0 = -map.extents.z - grow, z1 = map.extents.z + grow;
                if (ring == 3) { x0 = left; x1 = right; z0 = back; z1 = ahead; }
                x0 = Mathf.Max(x0, Mathf.Min(left, -map.extents.x)); x1 = Mathf.Min(x1, Mathf.Max(right, map.extents.x));
                z0 = Mathf.Max(z0, Mathf.Min(back, -map.extents.z)); z1 = Mathf.Min(z1, Mathf.Max(ahead, map.extents.z));
                points[ring * 4] = new Vector3(x0, y, z0);
                points[ring * 4 + 1] = new Vector3(x1, y, z0);
                points[ring * 4 + 2] = new Vector3(x1, y, z1);
                points[ring * 4 + 3] = new Vector3(x0, y, z1);
            }

            var triangles = new int[3 * 4 * 6];
            int t = 0;
            for (int ring = 0; ring < 3; ring++)
            for (int side = 0; side < 4; side++)
            {
                int a = ring * 4 + side, b = ring * 4 + (side + 1) % 4, c = a + 4, d = b + 4;
                // Facing up: inner corner, outer corner, next outer corner; then inner corner, next outer corner, next inner corner.
                triangles[t++] = a; triangles[t++] = d; triangles[t++] = c;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = d;
            }

            shadeMesh.Clear();
            shadeMesh.vertices = points;
            shadeMesh.uv = new Vector2[16];
            shadeMesh.triangles = triangles;
            shadeMesh.RecalculateBounds();
        }

        /// <summary>How dark each ring is: all of it at once in the whitebox look, deepening over FadeCells in the illustrated one.</summary>
        void PaintShade()
        {
            if (!shadeMesh) return;
            var game = GameManager.Instance;
            Camera cam = game && game.cam ? game.cam : Camera.main;
            Color dark = cam && cam.clearFlags == CameraClearFlags.SolidColor ? cam.backgroundColor : DefaultVoid;
            float atWall = Art.Enabled ? FadeStart : 1f;
            for (int ring = 0; ring < 4; ring++)
            {
                dark.a = ring < 2 ? atWall : 1f;
                for (int corner = 0; corner < 4; corner++) shadeColours[ring * 4 + corner] = dark;
            }

            shadeMesh.colors32 = shadeColours;
        }
    }
}
