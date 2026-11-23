using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// What leads the player to the campaign's black hole besides the afternoon's ore trail (since 2026-11-06): small glowing purple shards
    /// of dark matter on the ground, and the hole's hum. The shards lie along the line from the base to the hole, a few far out and more
    /// the nearer the hole, and thickest in a field round it. The hum is silent rules.holeHumReach cells from the hole and grows to
    /// rules.holeHumVolume at its gravity ring. Both are there exactly when the hole is: by day and in the afternoon, never at night or
    /// on Day 0, and both go for good once the hole is gone (the hidden boss beaten; after the won trial the hole and its shards stay
    /// through the frozen moment of the return). The hum is also silent in the void trial and from its win on, in the base and in the
    /// story scenes, and holds where it is while the game is paused, like the other sounds. The shards are decoration
    /// only: one static mesh with one material (a single draw call), no collider, never on walls, quicksand or inside the hole's pull.
    /// They are built with the campaign's map (SetupCampaignMap) and destroyed with it (RestoreClassicMap); a classic run never has them.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // Where the shards lie, in cells (presentation, not balance numbers): LineShards along the line from the base to the hole, from
        // LineStart cells out to the hole's gravity ring and spread LineSpread to either side (at most LineHalfWidth); FieldShards round
        // the hole, from FieldRingGap outside its gravity ring to FieldReach cells from its centre. A spot on a wall, on quicksand or in
        // the hole's pull is tried again elsewhere, up to ShardTries times, then left out.
        const int LineShards = 110, FieldShards = 140, ShardTries = 8;
        const float LineStart = 22f, LineSpread = 2.4f, LineHalfWidth = 6f, FieldReach = 34f, FieldRingGap = 1.2f, ShardClearance = .35f;
        // Colours above 1 reach the bloom of the illustrated art, so there the shards glow; the whitebox has no bloom, and above 1 its
        // purple would only clip toward pink, so there they stay at 1. The glow breathes a little on the game's clock (Time.time), so it
        // holds still while the game is paused or frozen, as the hole's own pulse does (V2BlackHolePresentation).
        // 1.2 keeps a faint bloom without washing the purple out toward pink (1.7 read as pale pink petals in the 2.5D view, 2026-11-06).
        const float ShardGlow = 1.2f, ShardBreath = .12f, ShardBreathSpeed = 1.7f;
        // The hum's level may change by its full volume in HumFadeSeconds (real time); far out it plays at HumFarPitch, at the ring at 1.
        const float HumFadeSeconds = .6f, HumFarPitch = .85f;
        // Dark matter's purple (the V2 palette): the shaded and the lit faces of a shard. The faces are shaded from one fixed direction.
        static readonly Color ShardDeep = new(.34f, .08f, .66f), ShardBright = new(.66f, .32f, 1f);
        static readonly Vector3 ShardLight = new Vector3(-.35f, 1f, -.45f).normalized;

        // Sealed void ore on the same line: a few pieces of the void alloy's ore (larger than the shards, glowing violet), from
        // PathOreStart cells out to PathOreEndGap cells short of the hole's centre, about PathOreGap cells apart and up to PathOreSide
        // cells to either side. They cannot be mined until the Nash Prophet is beaten; standing within PathOreNear cells of one says what
        // opens them, at most once every SealedOreRepeat seconds. The line's spots come from the map's seed. Away from the line each
        // afternoon lays rules.voidOreNodes more sealed pieces at random across the map, like the day's ore (PlaceSealedMapOre, from
        // AfternoonVoidOre). Once the Prophet is beaten every sealed piece goes, and each afternoon's void alloy lies across the map
        // instead (V2Campaign.Void.cs).
        const float PathOreStart = 26f, PathOreGap = 12f, PathOreEndGap = 12f, PathOreSide = 1.6f, PathOreClearance = 1.1f, PathOreNear = 3.2f,
            SealedOreRepeat = 25f;
        const int PathOreMin = 3, PathOreMax = 7;

        Transform holeGuide, pathOre;
        readonly List<Vector3> sealedOre = new();
        // Today's sealed void ore across the map, and the object that holds its pieces (outside the hole's guide).
        readonly List<Vector3> sealedMapOre = new();
        Transform mapOre;
        float sealedOreSaidAt = -999f;
        GameObject guideShards;
        Mesh guideMesh;
        Material guideMaterial;
        AudioSource holeHum;
        float humLevel, guideGlow = 1f;
        int shardCount;
        bool humPaused;

        /// <summary>For automated checks: whether the shards are out now, how many lie on the map (0 once the hole is gone), and the hum's
        /// volume now (0 while silent or paused).</summary>
        public bool HoleShardsShown => guideShards && guideShards.activeSelf;
        public int HoleShardCount => holeGuide ? shardCount : 0;
        public float HoleHumLevel => holeHum && holeHum.isPlaying ? holeHum.volume : 0f;
        /// <summary>For automated checks: where the sealed void ore lies (empty once the hole is gone), and whether it shows now.</summary>
        public IReadOnlyList<Vector3> SealedOre => sealedOre;
        public bool SealedOreShown => pathOre && pathOre.gameObject.activeSelf;
        /// <summary>For automated checks: where today's sealed void ore across the map lies (empty once the Prophet is beaten).</summary>
        public IReadOnlyList<Vector3> SealedMapOre => sealedMapOre;

        /// <summary>SetupCampaignMap, once the campaign's map and its black hole are in place: the shards (hidden until the hole shows)
        /// and the hum's source (silent).</summary>
        void BuildHoleGuide(WorldMap map)
        {
            ClearHoleGuide();
            var hole = map ? map.CampaignHole : null;
            if (!hole) return;
            holeGuide = new GameObject("V2 Hole Guide").transform;
            guideMesh = BuildShardMesh(map, hole);
            if (guideMesh)
            {
                guideShards = new GameObject("Shards");
                guideShards.transform.SetParent(holeGuide, false);
                guideShards.AddComponent<MeshFilter>().sharedMesh = guideMesh;
                var r = guideShards.AddComponent<MeshRenderer>();
                guideGlow = Art.Enabled ? ShardGlow : 1f;
                guideMaterial = ShardMaterial(guideGlow);
                if (guideMaterial) r.sharedMaterial = guideMaterial;
                else if (gm && gm.litMaterial) r.sharedMaterial = gm.Mat(ShardDeep); // no effects shader: plain purple, no glow
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                guideShards.SetActive(false); // TickHoleGuide shows them with the hole (Day 0 keeps it put away)
            }
            BuildSealedOre(map, hole);
            holeHum = holeGuide.gameObject.AddComponent<AudioSource>();
            holeHum.playOnAwake = false; holeHum.loop = true; holeHum.spatialBlend = 0f; holeHum.dopplerLevel = 0f; holeHum.priority = 100;
            holeHum.volume = 0f;
            humLevel = 0f; humPaused = false;
        }

        /// <summary>RestoreClassicMap, a fresh BuildHoleGuide, or the hole gone for good: the shards with their mesh and material, and the
        /// hum, which stops at once.</summary>
        void ClearHoleGuide()
        {
            if (holeHum) holeHum.Stop();
            if (holeGuide) Destroy(holeGuide.gameObject);
            if (guideMesh) Destroy(guideMesh);
            if (guideMaterial) Destroy(guideMaterial);
            holeGuide = null; pathOre = null; guideShards = null; guideMesh = null; guideMaterial = null; holeHum = null;
            sealedOre.Clear();
            ClearSealedMapOre();
            humLevel = 0f; humPaused = false; shardCount = 0;
        }

        /// <summary>Every frame while the campaign's map is in place (TickHoleQuest, right after it puts the hole away or brings it out, and
        /// in the void trial): the shards show exactly when the hole does, and the hum follows the player's distance to it.</summary>
        void TickHoleGuide(WorldMap map)
        {
            if (!holeGuide) return;
            var hole = map.CampaignHole;
            // The hidden boss beaten: the hole closes as the frozen moment of the return ends (RemoveHolesAfterTrial), and its shards and
            // its hum go with it for good. Until then the shards stay with the hole; the hum is silent from the win on (TickHoleHum).
            if (!hole || hole.Phase == HolePhase.Gone) { ClearHoleGuide(); return; }
            // The Prophet beaten: the seal is broken and the sealed pieces go; the afternoon's void alloy takes their spots from then on.
            if (prophetBeaten && (sealedOre.Count > 0 || sealedMapOre.Count > 0))
            {
                if (pathOre) Destroy(pathOre.gameObject);
                pathOre = null;
                sealedOre.Clear();
                ClearSealedMapOre();
            }
            bool shown = !map.CampaignHoleHidden;
            if (guideShards && guideShards.activeSelf != shown) guideShards.SetActive(shown);
            if (pathOre && pathOre.gameObject.activeSelf != shown) pathOre.gameObject.SetActive(shown);
            TickSealedOreHint(shown);
            if (shown && guideMaterial) guideMaterial.SetFloat("_Intensity", guideGlow * (1f + ShardBreath * Mathf.Sin(Time.time * ShardBreathSpeed)));
            TickHoleHum(map, hole, shown);
        }

        /// <summary>The hum, by day and in the afternoon while the hole is out and the player walks the map (not in the void trial or once
        /// it is won, not in the base or a story scene): silent rules.holeHumReach cells out, rules.holeHumVolume at the gravity ring, on a
        /// square curve so it swells as the player comes near, and a little higher in pitch there. The clip is the Nash Prophet's gravity
        /// pull loop, built from raw samples by V5WeaponAudio, so on the web it loops without the gap an imported clip gets. One looping
        /// source, never a second copy; it stops while silent, and the pause menu holds it where it is, as it does the other sounds.</summary>
        void TickHoleHum(WorldMap map, BlackHole hole, bool shown)
        {
            if (!holeHum) return;
            bool paused = gm && gm.RunPhase == ArenaUiState.Paused;
            if (paused != humPaused)
            {
                humPaused = paused;
                if (paused) holeHum.Pause(); else holeHum.UnPause();
            }
            if (paused) return;
            var p = gm ? gm.player : null;
            float target = 0f, near = 0f;
            if (shown && p && !map.TrialActive && !map.TrialWon && Stage is (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk))
            {
                float ring = hole.GravityRadius;
                near = Mathf.InverseLerp(Mathf.Max(ring + 1f, rules.holeHumReach), ring, Query.Flat(p.Position - hole.Centre).magnitude);
                var audio = V3WorldAudio.Instance;
                target = rules.holeHumVolume * near * near * (audio ? audio.masterGain : 1f);
            }
            humLevel = Mathf.MoveTowards(humLevel, target, Time.unscaledDeltaTime * Mathf.Max(.05f, rules.holeHumVolume) / HumFadeSeconds);
            if (humLevel <= .001f)
            {
                if (holeHum.isPlaying) holeHum.Stop();
                return;
            }
            if (!holeHum.clip) holeHum.clip = V5WeaponAudio.Instance ? V5WeaponAudio.Instance.ClipOf(V5Sfx.LoopGravity) : null;
            if (!holeHum.clip) return; // no such clip in this build: the hole stays silent
            holeHum.volume = humLevel;
            if (target > 0f) holeHum.pitch = Mathf.Lerp(HumFarPitch, 1f, near);
            if (!holeHum.isPlaying) holeHum.Play();
        }

        /// <summary>Every shard in one mesh. A generator of its own, seeded from the map's seed, leaves the gameplay's random numbers
        /// untouched and lays the same shards on the same map.</summary>
        Mesh BuildShardMesh(WorldMap map, BlackHole hole)
        {
            Vector3 centre = Query.Flat(hole.Centre), toHole = centre - BaseCentre;
            float distance = toHole.magnitude, ring = hole.GravityRadius + FieldRingGap;
            if (distance < LineStart + ring) return null;
            Vector3 dir = toHole / distance, side = new(-dir.z, 0f, dir.x);
            var rng = new System.Random(MapSeed * 31 + 7);
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            for (int i = 0; i < LineShards; i++)
                for (int attempt = 0; attempt < ShardTries; attempt++)
                {
                    // The square root of an even draw thins them out toward the base: their density rises steadily toward the hole.
                    float along = Mathf.Lerp(LineStart, distance - ring, Mathf.Sqrt(Unit(rng)));
                    Vector3 at = BaseCentre + dir * along + side * Mathf.Clamp(Gaussian(rng) * LineSpread, -LineHalfWidth, LineHalfWidth);
                    if (!ShardSpotOk(map, at)) continue;
                    AddShard(vertices, colours, triangles, rng, at, along / distance);
                    break;
                }
            for (int i = 0; i < FieldShards; i++)
                for (int attempt = 0; attempt < ShardTries; attempt++)
                {
                    // A power above 1 crowds them toward the gravity ring.
                    float r = ring + (FieldReach - ring) * Mathf.Pow(Unit(rng), 1.8f), a = Unit(rng) * Mathf.PI * 2f;
                    Vector3 at = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!ShardSpotOk(map, at)) continue;
                    AddShard(vertices, colours, triangles, rng, at, 1f - .3f * (r - ring) / Mathf.Max(1f, FieldReach - ring));
                    break;
                }
            if (vertices.Count == 0) return null;
            shardCount = vertices.Count / 24; // eight faces of three corners each
            var mesh = new Mesh { name = "Black hole shards" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[vertices.Count])); // the shader reads a texture; every vertex takes its white corner
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Ground a shard may lie on: past the open ground round the base, walkable (no wall, terrain, hole or pull) with a little
        /// room, and no quicksand.</summary>
        static bool ShardSpotOk(WorldMap map, Vector3 at) =>
            Query.Flat(at - BaseCentre).magnitude >= BaseClearRadius && map.IsWalkable(at, ShardClearance) && Quicksand.SpeedFactor(at) > .99f;

        /// <summary>One shard: a thin crystal (a stretched double pyramid of four sides) lying on the ground at a slant, partly sunk into it,
        /// its faces shaded from ShardLight so it reads as cut glass without any light of the scene. near (0 at the base, 1 at the hole)
        /// makes it larger and brighter.</summary>
        static void AddShard(List<Vector3> vertices, List<Color> colours, List<int> triangles, System.Random rng, Vector3 at, float near)
        {
            near = Mathf.Clamp01(near);
            // Small fragments: about a third of a cell long, a little longer near the hole.
            float length = Mathf.Lerp(.16f, .36f, Unit(rng)) * (1f + .35f * near), width = length * Mathf.Lerp(.18f, .3f, Unit(rng));
            var turn = Quaternion.Euler(0f, Unit(rng) * 360f, 0f) * Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f, 32f, Unit(rng)));
            Vector3 axis = turn * Vector3.right, up = turn * Vector3.up, across = turn * Vector3.forward;
            // The floor's top is y = 0 with its decals a hair above (Quicksand.DecalHeight .02): the shard sits just over them, its lower
            // edge and one tip sunk out of sight.
            var middle = new Vector3(at.x, .02f + width * .35f, at.z);
            Vector3 tipA = middle + axis * (length * .5f), tipB = middle - axis * (length * .5f);
            float roll = Unit(rng) * Mathf.PI * .5f;
            var rim = new Vector3[4];
            for (int j = 0; j < 4; j++)
            {
                float a = roll + j * Mathf.PI * .5f;
                rim[j] = middle + (up * Mathf.Cos(a) + across * Mathf.Sin(a)) * (width * (j % 2 == 0 ? 1f : .6f));
            }
            Color tint = Color.Lerp(ShardDeep, ShardBright, Unit(rng) * .3f);
            float brightness = Mathf.Lerp(.7f, 1f, near);
            for (int j = 0; j < 4; j++)
            {
                Face(vertices, colours, triangles, tipA, rim[j], rim[(j + 1) % 4], tint, brightness);
                Face(vertices, colours, triangles, tipB, rim[(j + 1) % 4], rim[j], tint, brightness);
            }
        }

        static void Face(List<Vector3> vertices, List<Color> colours, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Color tint, float brightness)
        {
            // Both sides are drawn (the effects shader culls nothing), so a face is lit by whichever side looks at the light.
            float lit = Mathf.Abs(Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, ShardLight));
            Color colour = Color.Lerp(tint * .8f, ShardBright, lit * .6f) * brightness;
            colour.a = .92f;
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colours.Add(colour); colours.Add(colour); colours.Add(colour);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
        }

        /// <summary>The effects shader (Resources/V5/V5Particle: unlit, vertex colours, always in the build), alpha blended, at the given
        /// brightness; null when the shader is missing.</summary>
        static Material ShardMaterial(float glow)
        {
            var shader = Resources.Load<Shader>("V5/V5Particle");
            if (!shader) return null;
            var material = new Material(shader) { name = "Black hole shards" };
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Intensity", glow);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            return material;
        }

        /// <summary>The spots of the void ore on the line from the base to the hole: evenly spaced along it, a little to either side, on
        /// open ground (walkable with room, no quicksand, past the open ground round the base) and apart from each other. A generator of
        /// its own, seeded from the map's seed, gives the same spots on the same map, with the hole there or gone.</summary>
        List<Vector3> PathOreSpots(WorldMap map, Vector3 holeCentre)
        {
            var spots = new List<Vector3>();
            if (!map) return spots;
            Vector3 toHole = Query.Flat(holeCentre) - BaseCentre;
            float distance = toHole.magnitude, end = distance - PathOreEndGap;
            if (end - PathOreStart < PathOreGap * .5f) return spots;
            Vector3 dir = toHole / distance, side = new(-dir.z, 0f, dir.x);
            int count = Mathf.Clamp(Mathf.RoundToInt((end - PathOreStart) / PathOreGap) + 1, PathOreMin, PathOreMax);
            var rng = new System.Random(MapSeed * 53 + 11);
            for (int i = 0; i < count; i++)
            {
                float along = Mathf.Lerp(PathOreStart, end, count == 1 ? .5f : i / (float)(count - 1));
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float shift = attempt == 0 ? 0f : (Unit(rng) - .5f) * PathOreGap * .5f;
                    Vector3 at = BaseCentre + dir * (along + shift) + side * ((Unit(rng) - .5f) * 2f * PathOreSide);
                    if (!PathOreSpotOk(map, at)) continue;
                    bool apart = true;
                    foreach (var other in spots)
                        if (Query.Flat(other - at).sqrMagnitude < PathOreGap * PathOreGap * .25f) { apart = false; break; }
                    if (!apart) continue;
                    spots.Add(at);
                    break;
                }
            }
            return spots;
        }

        static bool PathOreSpotOk(WorldMap map, Vector3 at) =>
            Query.Flat(at - BaseCentre).magnitude >= BaseClearRadius && map.IsWalkable(at, PathOreClearance) && Quicksand.SpeedFactor(at) > .99f;

        /// <summary>BuildHoleGuide: the sealed void ore on the hole's line while the Nash Prophet stands, hidden until the hole shows (with
        /// the shards). Each piece is the void alloy's ore model (or the whitebox's void alloy) with no collider and nothing to mine.</summary>
        void BuildSealedOre(WorldMap map, BlackHole hole)
        {
            sealedOre.Clear();
            if (prophetBeaten || !hole) return;
            sealedOre.AddRange(PathOreSpots(map, hole.Centre));
            if (sealedOre.Count == 0) return;
            pathOre = new GameObject("Sealed void ore").transform;
            pathOre.SetParent(holeGuide, false);
            var rng = new System.Random(MapSeed * 59 + 3);
            foreach (var at in sealedOre) BuildSealedPiece(pathOre, at, rng);
            pathOre.gameObject.SetActive(false);
        }

        /// <summary>One piece of sealed void ore: the void alloy's ore model (or the whitebox's void alloy) at a point, turned at random,
        /// with no collider and nothing to mine.</summary>
        void BuildSealedPiece(Transform parent, Vector3 at, System.Random rng)
        {
            var prefab = art && Art.Enabled ? art.oreNodePrefab : null;
            var root = new GameObject("SealedVoidOre").transform;
            root.SetParent(parent, false);
            root.position = at;
            root.rotation = Quaternion.Euler(0f, Unit(rng) * 360f, 0f);
            if (prefab)
            {
                var model = Instantiate(prefab, root);
                StripColliders(model);
                FitFootprint(model.transform, 1.8f);
                PaintVoidAlloy(model);
            }
            else Whitebox.Build("voidalloy", root);
        }

        /// <summary>AfternoonVoidOre while the Nash Prophet stands, from Day 1: yesterday's sealed pieces across the map go and
        /// rules.voidOreNodes new ones lie at random on open ground, as the day's ore does (TryNodePoint), so void alloy is seen all over
        /// the map before it can be mined.</summary>
        void PlaceSealedMapOre()
        {
            ClearSealedMapOre();
            if (prophetBeaten || !rules || rules.voidOreNodes <= 0) return;
            for (int i = 0; i < rules.voidOreNodes; i++)
                if (TryNodePoint(out var at)) sealedMapOre.Add(at);
            if (sealedMapOre.Count == 0) return;
            mapOre = new GameObject("Sealed void ore (map)").transform;
            var rng = new System.Random(MapSeed * 61 + Day * 7 + 5);
            foreach (var at in sealedMapOre) BuildSealedPiece(mapOre, at, rng);
        }

        /// <summary>The sealed pieces across the map go (a new afternoon's, the Prophet beaten, the guide cleared).</summary>
        void ClearSealedMapOre()
        {
            if (mapOre) Destroy(mapOre.gameObject);
            mapOre = null;
            sealedMapOre.Clear();
        }

        /// <summary>By day and in the afternoon, standing by a piece of sealed void ore says what it is and what opens it, at most once every
        /// SealedOreRepeat seconds: a piece on the hole's line (while the line shows) also says to follow the pieces to the hole, a piece
        /// elsewhere on the map only that it cannot be mined yet. Nothing once the Prophet is beaten (void alloy is mined from then on).</summary>
        void TickSealedOreHint(bool lineShown)
        {
            if (prophetBeaten || sealedOre.Count == 0 && sealedMapOre.Count == 0 || !ui || !gm || !gm.player) return;
            if (Stage is not (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk)) return;
            if (Time.unscaledTime < sealedOreSaidAt + SealedOreRepeat) return;
            Vector3 pp = gm.player.Position;
            if (lineShown)
                foreach (var at in sealedOre)
                {
                    if (Query.Flat(pp - at).sqrMagnitude > PathOreNear * PathOreNear) continue;
                    sealedOreSaidAt = Time.unscaledTime;
                    ui.Flash(ArenaText.Pair("虚空封住的矿石：只有虚空之力才能开采。顺着它们走就能找到黑洞，虚空之力在黑洞的另一边。",
                        "Ore sealed by the void: only the void's power can mine it. Follow it to the black hole; that power lies beyond."), 5f);
                    return;
                }
            foreach (var at in sealedMapOre)
            {
                if (Query.Flat(pp - at).sqrMagnitude > PathOreNear * PathOreNear) continue;
                sealedOreSaidAt = Time.unscaledTime;
                ui.Flash(ArenaText.Pair("虚空合金矿：现在还没法开采，要先获得虚空之力。虚空之力在黑洞的另一边。",
                    "Void alloy ore: you cannot mine it yet. You need the void's power first, and it lies beyond the black hole."), 5f);
                return;
            }
        }

        static float Unit(System.Random rng) => (float)rng.NextDouble();

        /// <summary>A standard normal number (Box-Muller).</summary>
        static float Gaussian(System.Random rng) => Mathf.Sqrt(-2f * Mathf.Log(1f - Unit(rng))) * Mathf.Cos(2f * Mathf.PI * Unit(rng));
    }
}
