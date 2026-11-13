using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>Coordinates saved geometry, gravity, portals and the saved trial platform.</summary>
    [DefaultExecutionOrder(200)]
    public sealed partial class WorldMap : MonoBehaviour
    {
        public static WorldMap Active { get; private set; }
        public MapLayout layout;
        public Biome biome;
        public Biome[] biomes;
        public Transform[] biomeWallGroups;
        public Renderer floorRenderer;
        public BlackHole[] holes;
        [Header("黑洞调整")]
        [Tooltip("只给固定地图洞增加的引力圈宽度；临时技能洞保留原配置。")] public float mapGravityExtraCells = 1f;
        public float mapHolePullSpeed = 6f;
        [SerializeField] int feedbackPatchRevision;
        public int FeedbackPatchRevision => feedbackPatchRevision;
        public void MarkFeedbackPatchApplied() => feedbackPatchRevision = 1;
        public BlackHole temporaryHole;
        [Tooltip("Saved trial-only skill objects preserve the frozen battlefield versions.")]
        public BlackHole trialTemporaryHole;
        [Tooltip("Saved extra slots support two simultaneous blink holes plus Pocket Void.")]
        public BlackHole[] extraTemporaryHoles = Array.Empty<BlackHole>();
        public BlackHole[] trialExtraTemporaryHoles = Array.Empty<BlackHole>();
        public TemporaryWall[] temporaryWalls;
        public Transform trialPlatform;
        public Enemy hollow;
        [Tooltip("Seconds after entering the void trial before the Nash Prophet (Hollow) may start its first move.")]
        public float trialOpeningGrace = 3f;
        [Tooltip("Cells from the platform centre where the player is set down on entering the trial; outside the Nash Prophet's close " +
                 "range (BossTuning.nashNearRange), so its first move is a far one.")]
        public float trialEntryDistance = 8f;
        [Min(0), Tooltip("Hole slots kept for the Nash Prophet's prophecy landings (copies of the trial temporary hole made at start), " +
                         "so its holes never take the player's own Pocket Void or blink slots.")]
        public int bossHoleSlots = 2;
        BlackHole[] bossHoles = Array.Empty<BlackHole>();
        public Transform decoy;
        public Transform trialDecoy;
        int TemporaryHoleCount => 1 + (TrialActive ? (trialExtraTemporaryHoles?.Length ?? 0) + bossHoles.Length : extraTemporaryHoles?.Length ?? 0);
        // The Prophet's slots come last in TemporaryHoleAt; the player's own holes never use them.
        int PlayerHoleCount => TemporaryHoleCount - (TrialActive ? bossHoles.Length : 0);
        BlackHole TemporaryHoleAt(int index)
        {
            if (index == 0) return TrialActive ? trialTemporaryHole : temporaryHole;
            if (!TrialActive) return extraTemporaryHoles[index-1];
            int extra = trialExtraTemporaryHoles?.Length ?? 0;
            return index - 1 < extra ? trialExtraTemporaryHoles[index-1] : bossHoles[index-1-extra];
        }
        bool IsTemporaryHole(Vector3 point, Combatant actor = null)
        {
            for(int i=0;i<TemporaryHoleCount;i++){var hole=TemporaryHoleAt(i);if(hole&&hole.gameObject.activeSelf&&hole.Contains(point,actor))return true;}
            return false;
        }
        Transform ActiveDecoy => TrialActive ? trialDecoy : decoy;
        public bool DecoyActive => ActiveDecoy && ActiveDecoy.gameObject.activeSelf;
        /// <summary>Where enemies go: while the player cannot be targeted (Void Walk) the place the void state began,
        /// else a standing Mirror Image, else the player.</summary>
        public Vector3 EnemyTarget => PlayerHidden ? GM.skills.VoidWalkStart : DecoyActive ? ActiveDecoy.position : GM.player.Position;
        public bool PlayerHidden => GM && GM.skills && GM.skills.PlayerUntargetable;
        public float trialRadius = 12f;
        public bool TrialActive { get; private set; }
        public bool TrialWon { get; private set; }
        public MapGrid Grid { get; private set; }
        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(layout ? layout.width : 100, 20, layout ? layout.height : 70));
        public event Action VoidTrialEntered;
        public event Action VoidTrialWon;
        public event Action<BlackHole> HoleFed;
        // Document 12 teaching moment, played as a short cutscene the first time a black hole is in view during a wave: the world
        // freezes, the view glides onto the hole, three reward-less fodder spiral in and the counter reads 3 / 40, then play resumes.
        enum HoleIntroStage { Idle, Approach, Pull, Hold, Done }
        sealed class IntroFodder { public Enemy body; public float angle, startRadius, radius, fedAt; public bool fed, gone; public Vector3 scale; }
        HoleIntroStage holeIntroStage;
        float holeIntroClock;
        readonly List<IntroFodder> holeIntroFodder = new();
        BlackHole holeIntroHole;
        CameraRig holeIntroRig;
        public bool HoleIntroPlaying => holeIntroStage is HoleIntroStage.Approach or HoleIntroStage.Pull or HoleIntroStage.Hold;
        public bool CanFeed
        {
            get
            {
                if (TrialWon || TrialActive) return false;
                foreach (var hole in holes) if (hole.Phase == HolePhase.Portal || hole.Phase == HolePhase.Collapsing) return false;
                return true;
            }
        }
        /// <summary>A portal is open somewhere on the map (the build phase may enter the trial through it).</summary>
        public bool HasOpenPortal
        {
            get { foreach (var hole in holes) if (hole && hole.Phase == HolePhase.Portal) return true; return false; }
        }
        Vector3 returnPoint;
        BlackHole enteredHole;
        bool labTrial;
        int lastCell = -1;
        float gridTimer;
        float decoyLife;
        bool decoyBlast;
        Combatant decoyOwner;
        float trialDecoyLife;
        bool trialDecoyBlast;
        Combatant trialDecoyOwner;
        readonly List<TemporaryWall> trialWalls = new List<TemporaryWall>();
        readonly List<GameObject> frozen = new List<GameObject>();
        readonly List<Combatant> characters = new List<Combatant>();
        static GameManager GM => GameManager.Instance;

        /// <summary>The open mode's map pick for the next scene load (Biome.Dunes or Biome.Ruins; null picks at random as before). The map
        /// is laid here in Awake, so a pick other than the map already laid reloads the scene (GameManager, ArenaUiCommand.ChooseMap); the
        /// next Awake takes the pick and clears it.</summary>
        public static string NextMapFamily { get; set; }

        /// <summary>The laid map's place in biomes, or -1.</summary>
        public int BiomeIndex => biomes != null && biome ? Array.IndexOf(biomes, biome) : -1;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMapPick() => NextMapFamily = null;

        void Awake()
        {
            Active = this;
            if (biomes != null && biomes.Length > 0)
            {
                // Only the Dunes and the Ruins are in rotation (system doc 12.2); the open mode may ask for one of them.
                int chosen = Biome.PickForRun(biomes, NextMapFamily);
                NextMapFamily = null;
                biome = biomes[chosen];
                layout = biome.layout;
                for (int i = 0; i < biomeWallGroups.Length; i++) biomeWallGroups[i].gameObject.SetActive(i == chosen);
                if (chosen < biomeWallGroups.Length) LayoutWalls.Fill(biomeWallGroups[chosen], layout); // walls the layout gained after the scene was saved
                biome.ApplyLighting(); // daytime on every map for now; night comes later
                if (floorRenderer && biome.floor)
                {
                    var surface = floorRenderer.GetComponent<V2SurfaceArt>();
                    if (surface) surface.SetWhiteboxMaterial(biome.floor);
                    else floorRenderer.sharedMaterial = biome.floor;
                    var block = new MaterialPropertyBlock();
                    floorRenderer.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", biome.floor.color);
                    block.SetColor("_Color", biome.floor.color);
                    floorRenderer.SetPropertyBlock(block);
                }
            }
            if (trialTemporaryHole && bossHoleSlots > 0)
            {
                bossHoles = new BlackHole[bossHoleSlots];
                for (int i = 0; i < bossHoleSlots; i++)
                {
                    bossHoles[i] = Instantiate(trialTemporaryHole, trialTemporaryHole.transform.parent);
                    bossHoles[i].name = trialTemporaryHole.name + " (boss " + (i + 1) + ")";
                    bossHoles[i].gameObject.SetActive(false);
                }
            }
            Grid = new MapGrid(layout ? layout.width : 100, layout ? layout.height : 70);
            PlaceHolesAtAnchors();
            Grid.Rebuild(this);
            Quicksand.Build(this);
            // The trial platform is a cylinder flattened into a disc. Its default CapsuleCollider, scaled that way, turns into a sphere as
            // wide as the platform: a body set down on the disc starts inside it and is pushed out over the edge, where it falls. The disc
            // stands on a box of the same size instead; the edge rule in FixedUpdate still decides who falls off.
            if (trialPlatform && trialPlatform.TryGetComponent(out CapsuleCollider capsule))
            {
                var box = trialPlatform.gameObject.AddComponent<BoxCollider>();
                box.center = capsule.center;
                box.size = new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f);
                Destroy(capsule);
            }
            SkillWorldAPI.CreateWall = CreateWall;
            SkillWorldAPI.CreateTemporaryHole = CreateTemporaryHole;
            SkillWorldAPI.CreateVoidHole = CreateVoidHole;
            SkillWorldAPI.FeedNearestHole = FeedNearestHole;
            SkillWorldAPI.ClosestHole = ClosestHole;
            SkillWorldAPI.IsHole = IsHole;
            SkillWorldAPI.GetPull = GetPull;
            SkillWorldAPI.ValidPoint = ValidPoint;
            SkillWorldAPI.IsElite = target => target is Enemy enemy && (enemy.Elite || enemy.IsBoss);
            SkillWorldAPI.Swallow = (victim, owner) => victim.OnEnterFallZone(Vector3.zero);
            SkillWorldAPI.CreateDecoy = CreateDecoy;
            SkillWorldAPI.SkillObjectStanding = SkillObjectStanding;
        }

        /// <summary>Both map holes start fresh on two different anchors of the layout, picked at random (Awake; RestoreLayout after a V2
        /// campaign map).</summary>
        void PlaceHolesAtAnchors()
        {
            // Null-safe since 2026-11-06: RestoreLayout (WorldMap.Campaign.cs) also calls this, possibly while the scene is going away.
            // A map hole pulls a little faster than the player walks (BlackHole.PullOverWalk), in the open mode as in the story.
            float pull = BlackHole.MapPull(PlayerWalkSpeed());
            foreach (var hole in holes) if (hole) { hole.ResetHole(); hole.pullSpeed = pull; }
            if (layout && layout.holeAnchors != null && layout.holeAnchors.Length >= 2 && holes.Length >= 2 && holes[0] && holes[1])
            {
                int first = UnityEngine.Random.Range(0, layout.holeAnchors.Length);
                int second = (first + 1 + UnityEngine.Random.Range(0, layout.holeAnchors.Length - 1)) % layout.holeAnchors.Length;
                holes[0].transform.position = new Vector3(layout.holeAnchors[first].x, 0, layout.holeAnchors[first].y);
                holes[1].transform.position = new Vector3(layout.holeAnchors[second].x, 0, layout.holeAnchors[second].y);
            }
        }

        /// <summary>The player's walking speed (PlayerController.moveSpeed), 6 cells a second before there is a player.</summary>
        static float PlayerWalkSpeed()
        {
            var gm = GameManager.Instance;
            var player = gm && gm.player ? gm.player : FindAnyObjectByType<PlayerController>();
            return player ? player.moveSpeed : 6f;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            ForgetCampaignMap(); // V2 day campaign: its runtime layout and hooks (WorldMap.Campaign.cs)
        }

        public bool Contains(Vector3 point, float margin = 0f) => Mathf.Abs(point.x) <= Bounds.extents.x - margin && Mathf.Abs(point.z) <= Bounds.extents.z - margin;
        public Vector3 ClampPoint(Vector3 point, float radius = 0.5f) => new Vector3(Mathf.Clamp(point.x, Bounds.min.x + radius, Bounds.max.x - radius), point.y, Mathf.Clamp(point.z, Bounds.min.z + radius, Bounds.max.z - radius));
        public bool IsWall(Vector3 point, float margin = 0f)
        {
            foreach (var rect in extraWalls)
                if (point.x >= rect.xMin - margin && point.x <= rect.xMax + margin && point.z >= rect.yMin - margin && point.z <= rect.yMax + margin) return true;
            if (layout && layout.walls != null)
                foreach (var rect in layout.walls)
                    if (point.x >= rect.xMin - margin && point.x <= rect.xMax + margin && point.z >= rect.yMin - margin && point.z <= rect.yMax + margin) return true;
            foreach (var wall in temporaryWalls)
                if (wall && wall.gameObject.activeInHierarchy && wall.GetComponent<Collider>().bounds.Contains(new Vector3(point.x, wall.transform.position.y, point.z))) return true;
            return false;
        }
        public bool IsHole(Vector3 point)
        {
            foreach (var hole in holes) if (hole.Contains(point)) return true;
            return IsTemporaryHole(point);
        }
        public Vector3 GetPull(Vector3 point) => GetPull(point,null);
        public Vector3 GetPull(Vector3 point, Combatant actor)
        {
            Vector3 pull = Vector3.zero;
            foreach (var hole in holes) pull += hole.Pull(point);
            for(int i=0;i<TemporaryHoleCount;i++){var hole=TemporaryHoleAt(i);if(hole&&hole.gameObject.activeSelf)pull+=hole.Pull(point,actor);}
            return pull;
        }
        public bool IsWalkable(Vector3 point, float radius = 0.5f, bool allowGravity = false) => Contains(point, radius) && !IsWall(point, radius) && !IsHole(point) && (allowGravity || GetPull(point).sqrMagnitude < 0.01f);
        /// <param name="followIntoGravity">Enemy.FollowingIntoGravity (document 9.4): a fast kind while what the enemies chase stands in a
        /// gravity ring. Then a path to a target inside a ring may cross into it; otherwise it ends at the nearest ground outside.</param>
        public Vector3 GetChaseDirection(Vector3 position, Vector3 target, bool followIntoGravity)
        {
            if (TrialActive) return Query.Flat(target - position).normalized;
            Vector3 pull = GetPull(position);
            if (pull.sqrMagnitude > 0.01f && GetPull(target).sqrMagnitude < 0.01f)
                return -pull.normalized; // A displaced enemy chooses the shortest escape from the zone.
            bool allow = followIntoGravity && GetPull(target).sqrMagnitude > 0.01f;
            return Grid.Direction(position, target, allow);
        }
        public void HoleChanged()
        {
            Grid.Rebuild(this);
            if (extraWalls.Count > 0) Grid.IntegrateRaid(extraWalls[0]);
            lastCell = -1;
        }

        /// <summary>V2 day campaign: the base and the ship pad stand as walls (paths, spawns, knockback and line of sight treat them so);
        /// the first rect is the base, which raiders reach through their own flow field (MapGrid.IntegrateRaid).</summary>
        readonly List<Rect> extraWalls = new();
        public void SetExtraWalls(IList<Rect> rects)
        {
            extraWalls.Clear();
            if (rects != null) extraWalls.AddRange(rects);
            HoleChanged();
        }
        public Vector3 GetRaidDirection(Vector3 position, Vector3 spot) => Grid.RaidDirection(position, spot);

        void Update()
        {
            if (!GM || !GM.player) return;
            TickHoleIntro();
            if (CampaignMapApplied) TickCampaignWorld(); // V2 day campaign: the side quest and a confirmed entry (WorldMap.Campaign.cs)
            // The build phase turns its holes with the panel open as well (GameManager.BuildTimeFlows); E still needs the panel away.
            if (GM.IsCombatActive || GM.CanPlayerMove || GM.BuildTimeFlows)
            {
                float dt = GM.WorldDeltaTime;
                // The original battlefield remains frozen while trial-only effects can still run.
                if (!TrialActive) foreach (var hole in holes) hole.Tick(dt);
                for(int i=0;i<TemporaryHoleCount;i++){var hole=TemporaryHoleAt(i);if(hole&&hole.gameObject.activeSelf)hole.Tick(dt);}
                int index = Grid.Index(GM.player.Position);
                gridTimer -= dt;
                if (!TrialActive && (lastCell != index || gridTimer <= 0f))
                {
                    Grid.Integrate(EnemyTarget);
                    lastCell = index;
                    gridTimer = 0.2f;
                }
                if (ArenaKeys.Pressed(GameAction.Interact)) TryEnterPortal(GM.player);
                if (TrialActive) TickDecoy(ref trialDecoyLife, trialDecoy, trialDecoyBlast, trialDecoyOwner, dt);
                else TickDecoy(ref decoyLife, decoy, decoyBlast, decoyOwner, dt);
            }
        }

        const float HoleIntroApproach = .9f, HoleIntroSpiral = 2.4f, HoleIntroStagger = .45f, HoleIntroSink = .6f, HoleIntroHold = 1.6f, HoleIntroZoom = .62f;

        /// <summary>Cutscene driver on unscaled time: trigger on first sight, glide in, spiral the fodder into the hole, sink each one
        /// as it is counted, hold on the counter, then hand play back. Once per run.</summary>
        void TickHoleIntro()
        {
            if (holeIntroStage == HoleIntroStage.Done) return;
            if (holeIntroStage == HoleIntroStage.Idle)
            {
                // V2 day campaign (since 2026-11-06): the campaign says when its hole may play this (by day or in the afternoon, while it
                // is out), and the hole's count may already have begun. The classic run keeps its own rule: mid-wave, the first sight.
                if (CampaignMapApplied) { if (TrialActive || campaignHoleIntroAllowed == null || !campaignHoleIntroAllowed()) return; }
                else if (TrialActive || !GM.IsCombatActive || GM.waves.Phase != WavePhase.WaveActive) return;
                BlackHole seen = null;
                foreach (var hole in holes)
                    if (hole && hole.Phase == HolePhase.Active && (hole.Feeds == 0 || CampaignMapApplied) && (SkillWorldAPI.IsOnScreen?.Invoke(hole.Centre) ?? false)) { seen = hole; break; }
                if (!seen) return;
                holeIntroHole = seen;
                Vector3 toPlayer = Query.Flat(GM.player.Position - seen.Centre);
                toPlayer = toPlayer.sqrMagnitude > .01f ? toPlayer.normalized : Vector3.forward;
                float startRadius = seen.GravityRadius - .35f;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, 70f + 120f * i, 0f) * toPlayer;
                    Vector3 at = seen.Centre + dir * startRadius;
                    if (!Contains(at, .5f) || IsWall(at, .5f)) continue;
                    var body = CampaignMapApplied ? SpawnCampaignIntroFodder(at) : GM.waves.SpawnIntroFodder(at);
                    if (!body) continue;
                    body.enabled = false; // No chase and no escape: the cutscene moves the body itself.
                    holeIntroFodder.Add(new IntroFodder
                    {
                        body = body, angle = Mathf.Atan2(dir.x, dir.z), startRadius = startRadius, radius = startRadius,
                        scale = body.bodyRenderer ? body.bodyRenderer.transform.localScale : Vector3.one,
                    });
                }
                if (holeIntroFodder.Count == 0) { holeIntroStage = HoleIntroStage.Done; HoleIntroFinished?.Invoke(); return; }
                holeIntroStage = HoleIntroStage.Approach;
                holeIntroClock = 0f;
                GM.BeginCutscene();
                holeIntroRig = GM.cam ? GM.cam.GetComponent<CameraRig>() : null;
                holeIntroRig?.Focus(seen.Centre, HoleIntroZoom, HoleIntroApproach);
                return;
            }
            float dt = Time.unscaledDeltaTime;
            holeIntroClock += dt;
            if (holeIntroStage == HoleIntroStage.Approach)
            {
                if (holeIntroClock >= HoleIntroApproach) { holeIntroStage = HoleIntroStage.Pull; holeIntroClock = 0f; }
                return;
            }
            if (holeIntroStage == HoleIntroStage.Pull)
            {
                bool allGone = true;
                for (int i = 0; i < holeIntroFodder.Count; i++)
                {
                    var f = holeIntroFodder[i];
                    if (!f.body || f.gone) continue;
                    allGone = false;
                    if (f.fed)
                    {
                        // Counted: keep turning while sinking and shrinking, then end the fall so nothing lingers in the hole.
                        float k = Mathf.Clamp01((holeIntroClock - f.fedAt) / HoleIntroSink);
                        f.angle += dt * 5f;
                        MoveBody(f.body, holeIntroHole.Centre + Orbit(f.angle) * (f.radius * (1f - k)) + Vector3.down * (1.2f * k));
                        if (f.body.bodyRenderer) f.body.bodyRenderer.transform.localScale = f.scale * (1f - k);
                        if (k >= 1f) { f.gone = true; f.body.FinishFallNow(); }
                        continue;
                    }
                    float t = Mathf.Clamp01((holeIntroClock - i * HoleIntroStagger) / HoleIntroSpiral);
                    if (t <= 0f) continue;
                    float eased = t * t;
                    f.radius = Mathf.Lerp(f.startRadius, 0f, eased);
                    f.angle += dt * (1.6f + 3.5f * eased);
                    MoveBody(f.body, holeIntroHole.Centre + Orbit(f.angle) * f.radius);
                    if (f.radius <= holeIntroHole.KillRadius * .55f) FeedIntroFodder(f);
                }
                if (allGone) { holeIntroStage = HoleIntroStage.Hold; holeIntroClock = 0f; }
                return;
            }
            if (holeIntroStage == HoleIntroStage.Hold && holeIntroClock >= HoleIntroHold) FinishHoleIntro();
        }

        static Vector3 Orbit(float angle) => new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

        /// <summary>Ends the cutscene at once, counting what is left (the enemy lab and tests). No player input calls it: the cutscene
        /// cannot be skipped.</summary>
        public void SkipHoleIntro()
        {
            if (!HoleIntroPlaying) return;
            foreach (var f in holeIntroFodder)
            {
                if (!f.body || f.gone) continue;
                if (!f.fed) { MoveBody(f.body, holeIntroHole.Centre); FeedIntroFodder(f); }
                f.gone = true; f.body.FinishFallNow();
            }
            FinishHoleIntro();
        }

        void FeedIntroFodder(IntroFodder f)
        {
            f.fed = true; f.fedAt = holeIntroClock;
            holeIntroHole.Feed(1, GM.waves.WaveIndex + 1);
            HoleFed?.Invoke(holeIntroHole);
            f.body.OnEnterFallZone(Vector3.zero);
        }

        void FinishHoleIntro()
        {
            holeIntroStage = HoleIntroStage.Done;
            foreach (var f in holeIntroFodder) if (f.body) f.body.enabled = true; // A fall still running completes under normal time.
            holeIntroRig?.ClearFocus(.8f);
            GM.EndCutscene();
            HoleIntroFinished?.Invoke();
        }

        static void MoveBody(Combatant body, Vector3 pos)
        {
            var rb = body.GetComponent<Rigidbody>();
            if (rb) rb.position = pos;
            body.transform.position = pos;
        }

        static void TickDecoy(ref float remaining, Transform visual, bool endBlast, Combatant owner, float dt)
        {
            if (!visual || !visual.gameObject.activeSelf) return;
            remaining -= dt;
            if (remaining > 0f) return;
            if (endBlast) foreach (var victim in Query.Characters(visual.position, 3f)) if (victim.Team == Team.Enemy) victim.ReceiveDamage(DamageInfo.Attack(8f, owner, victim.Position - visual.position, 5f, HitKind.Blast));
            visual.gameObject.SetActive(false);
            // Mirror Image level 3 listens for the natural end of the decoy; wave cleanup deactivates it without this call.
            SkillWorldAPI.DecoyEnded?.Invoke(visual.position);
        }

        void FixedUpdate()
        {
            // The build phase pulls and swallows with the panel open as well (GameManager.BuildTimeFlows), exactly as while walking.
            if (!GM || (!GM.IsCombatActive && !GM.CanPlayerMove && !GM.BuildTimeFlows) || !GM.player) return;
            characters.Clear();
            characters.Add(GM.player);
            foreach (var enemy in GM.waves.Alive) if (enemy && enemy.gameObject.activeInHierarchy) characters.Add(enemy);
            foreach (var combatant in characters)
            {
                if (!combatant.IsAlive || combatant.State == CharacterState.Hooked) continue;
                if (SkillWorldAPI.HoleProtection != null && SkillWorldAPI.HoleProtection(combatant)) continue;
                if (TrialActive)
                {
                    TickTrialBody(combatant); // the void, fragments, holes, the Prophet's pull and the portals: WorldMap.Trial.cs
                    continue;
                }
                // Launch Yourself Tame: no hole drags or swallows the flying missile.
                if (MissileHoleProof(combatant)) continue;
                bool swallowed = false;
                foreach (var hole in holes)
                {
                    if (!hole.Contains(combatant.Position)) continue;
                    if (combatant is Enemy boss && boss.IsBoss) continue;
                    if (combatant.Team == Team.Enemy) { hole.Feed(1, GM.waves.WaveIndex + 1); HoleFed?.Invoke(hole); }
                    combatant.OnEnterFallZone(Vector3.zero);
                    swallowed = true;
                    break;
                }
                // Pocket Void swallows everything except bosses (document 8.4).
                if (!swallowed && IsTemporaryHole(combatant.Position,combatant) && !(combatant is Enemy { IsBoss: true })) { SwallowIntoVoid(combatant); swallowed = true; }
                // Bosses ignore black-hole pull (document 12.3: the Nash Prophet and the Tyrant are never pulled or swallowed).
                if (!swallowed && !(combatant is Enemy { IsBoss: true })) ApplyHolePull(combatant);
            }
            TickCorpses(GM.WorldFixedDeltaTime);
            // Existing projectile prefabs remain editable; entering a circular black-hole core loses a shot.
            if (AttackPool.Instance)
                foreach (var projectile in AttackPool.Instance.actors)
                    if (projectile && projectile.gameObject.activeSelf && projectile.AffectedByHoles && IsHole(projectile.transform.position)) projectile.SwallowedByHole();
        }

        /// <summary>Launch Yourself Tame (Q25): the player flying the tamed missile is out of reach of every black hole (map,
        /// void-trial and their own Pocket Void); the void off the trial platform and map gaps still take them.</summary>
        static bool MissileHoleProof(Combatant combatant) => combatant == GM.player && GM.runWeapons && GM.runWeapons.MissileHoleProof;

        /// <summary>Chain Blade (Q18): a drag from 'from' to 'at' would take the body into a hole's core, into a pull ring it
        /// is outside of, or nearer the core of a ring it is already in. Each hole is asked on its own, so overlapping pulls cannot cancel out.</summary>
        public bool DragIntoHole(Vector3 from, Vector3 at, Combatant actor)
        {
            foreach (var hole in holes) if (DragInto(hole, from, at, actor)) return true;
            for (int i = 0; i < TemporaryHoleCount; i++) { var hole = TemporaryHoleAt(i); if (hole && hole.gameObject.activeSelf && DragInto(hole, from, at, actor)) return true; }
            return false;
        }
        static bool DragInto(BlackHole hole, Vector3 from, Vector3 at, Combatant actor) => hole.Contains(at, actor) || hole.Pull(at, actor).sqrMagnitude > .01f
            && (hole.Pull(from, actor).sqrMagnitude <= .01f || Query.Flat(at - hole.Centre).sqrMagnitude < Query.Flat(from - hole.Centre).sqrMagnitude);

        /// <summary>A temporary (Pocket Void) hole: swallowed enemies still pay their experience straight to the player (document 8.4).</summary>
        void SwallowIntoVoid(Combatant combatant)
        {
            if (combatant is Enemy enemy && enemy.IsAlive && !enemy.SuppressRewards && !enemy.IsTrialSummon && GM.progression && GM.waves)
                GM.progression.GainXp(GM.waves.ExperienceFor(enemy));
            combatant.OnEnterFallZone(Vector3.zero);
        }

        static BossTuning BossNumbers => GM && GM.waves && GM.waves.bosses != null ? GM.waves.bosses : BossTuning.Defaults;

        /// <summary>Void trial: a boss hit pushes the player at most to holeKnockMargin from any open hole. The platform edge does not stop
        /// it in any phase (the doc dropped "its attacks never push you off"), so the repulse pulse throws a player caught
        /// in the outer part of its circle into the void and only pushes one close to the Prophet (BossTuning). Off the trial, unchanged.</summary>
        public float LimitBossKnockback(Vector3 from, Vector3 dir, float distance)
        {
            if (!TrialActive || !trialPlatform || distance <= 0f) return distance;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f) return distance;
            dir.Normalize();
            var numbers = BossNumbers;
            for (int i = 0; i < TemporaryHoleCount; i++)
            {
                var hole = TemporaryHoleAt(i);
                if (!hole || !hole.gameObject.activeSelf || hole.Phase is not (HolePhase.Active or HolePhase.Collapsing)) continue;
                Vector3 q = Query.Flat(from - hole.Centre);
                float r = hole.KillRadius + numbers.holeKnockMargin;
                float hb = Vector3.Dot(q, dir), hc = q.sqrMagnitude - r * r;
                if (hc <= 0f) { if (hb < 0f) distance = 0f; continue; } // already within the margin: never closer
                float disc = hb * hb - hc;
                if (hb >= 0f || disc < 0f) continue; // moving away or passing clear
                distance = Mathf.Min(distance, Mathf.Max(0f, -hb - Mathf.Sqrt(disc)));
            }
            return Mathf.Max(0f, distance);
        }

        /// <summary>Opens a prophecy landing hole in one of the Prophet's own slots (no gravity ring); the oldest is reused when all are open.</summary>
        public void CreateBossHole(Vector3 at, float diameter, float life, Combatant owner)
        {
            if (!TrialActive) return;
            BlackHole slot = null;
            foreach (var hole in bossHoles)
            {
                if (!hole) continue;
                if (!hole.gameObject.activeSelf) { slot = hole; break; }
                if (!slot || hole.TemporarySecondsLeft < slot.TemporarySecondsLeft) slot = hole;
            }
            if (!slot) { CreateVoidHole(at, diameter, 0f, life, owner, true); return; }
            slot.MakeTemporary(new Vector3(at.x, trialTemporaryHole.transform.position.y, at.z), diameter, life, owner, true, 0f);
            HoleChanged();
        }
        void HideBossHoles() { foreach (var hole in bossHoles) if (hole) hole.gameObject.SetActive(false); }

        static void ApplyPull(Combatant combatant, Vector3 pull)
        {
            if (combatant.State == CharacterState.Dashing) return;
            var body = combatant.GetComponent<Rigidbody>();
            if (body && !body.isKinematic) body.linearVelocity += pull * (combatant.Team == Team.Player ? 1f : GM.WorldTimeScale);
        }
        // Design document 4.4: the pull is a plain velocity (8 cells/s on the fixed holes), so walking (6) loses 2 cells/s and only a
        // sprint (12) gets out at 4 cells/s; there is no extra rule holding a walking player in place.
        void ApplyHolePull(Combatant combatant)
        {
            // Spin-up Tame: braced against a wall at full spin, the player is not dragged by a black hole (walking into its core still swallows them).
            if (combatant == GM.player && GM.runWeapons && GM.runWeapons.Anchored) return;
            ApplyPull(combatant,GetPull(combatant.Position,combatant));
        }
        void TickCorpses(float dt)
        {
            if(GM.waves.preparedEnemies!=null)foreach(var enemy in GM.waves.preparedEnemies)
            {
                if(!enemy||!enemy.CorpseActive||enemy.CorpseCarried||!enemy.gameObject.activeSelf)continue;
                enemy.TickCorpse(dt,GetPull(enemy.Position));
                if(!enemy.CorpseActive)continue;
                if(TrialActive&&Query.Flat(enemy.Position-trialPlatform.position).magnitude>trialRadius){enemy.DespawnCorpse(enemy.SpawnVersion);continue;}
                if(!TrialActive)foreach(var hole in holes)
                {
                    if(!hole.Contains(enemy.Position))continue;
                    FeedCorpse(hole,enemy,enemy.SpawnVersion);break;
                }
                if(enemy.CorpseActive&&IsTemporaryHole(enemy.Position))enemy.DespawnCorpse(enemy.SpawnVersion);
            }
        }
        void FeedCorpse(BlackHole hole,Enemy source,int version)
        {
            if(!source||!source.TryConsumeCorpseFeed(version))return;
            if(!hole.Temporary&&CanFeed){hole.Feed(1,GM.waves.WaveIndex+1);HoleFed?.Invoke(hole);}
        }

        /// <summary>E on an open portal (document 12.5): the player must stand on it in the normal state, during a wave or
        /// while walking the build phase with the panel put away (Tab). No phase enters a portal from a distance. In the void trial E takes
        /// the trial portal the player stands on by the same rule (WorldMap.Trial.cs).</summary>
        public bool TryEnterPortal(PlayerController player)
        {
            if (TrialActive) return TryTakeTrialPortal(player);
            foreach (var hole in holes)
            {
                if (!CanEnterPortal(player, hole)) continue;
                // V2 day campaign (since 2026-11-06): the campaign asks first; a yes enters on a later frame (TickCampaignWorld).
                if (CampaignMapApplied && campaignConfirmPortal != null) { AskCampaignPortal(hole); return true; }
                enteredHole = hole;
                BeginTrial(player, hole.Centre, false);
                return true;
            }
            return false;
        }
        /// <summary>Whether E takes the player through this hole right now; the portal label offers E only then. The normal state also
        /// means free to act (document 4.5): not stunned, frozen or caught in a paralysis twitch, so no stun is carried onto the platform.</summary>
        public bool CanEnterPortal(PlayerController player, BlackHole hole)
        {
            if (!player || !hole || hole.Phase != HolePhase.Portal || TrialActive || TrialWon) return false;
            if (player.State != CharacterState.Normal || player.ActionsBlocked) return false;
            // V2 day campaign (since 2026-11-06): by day and in the afternoon, as the campaign says (CampaignPortalOpenNow).
            if (CampaignMapApplied) { if (!CampaignPortalOpenNow) return false; }
            else if (!GM.BuildWalking && !GM.BuildCrateWalking && (!GM.IsCombatActive || GM.waves.Phase != WavePhase.WaveActive)) return false;
            return Mathf.Abs(player.Position.x - hole.Centre.x) <= 0.5f && Mathf.Abs(player.Position.z - hole.Centre.z) <= 0.5f;
        }
        /// <summary>Enemy lab: the void trial against the Hollow at once, without feeding a portal open. A win still gives its reward but
        /// leaves the black holes and the trial itself as they were, so the player returns to where they stood and can fight it again.</summary>
        public bool EnterLabTrial(PlayerController player)
        {
            if (!player || TrialActive || TrialWon || !hollow || !trialPlatform || !GM.IsCombatActive || GM.waves.Phase != WavePhase.WaveActive) return false;
            enteredHole = null;
            BeginTrial(player, player.Position, true);
            return true;
        }
        void BeginTrial(PlayerController player, Vector3 returnTo, bool lab)
        {
            labTrial = lab;
            returnPoint = returnTo;
            frozen.Clear();
            foreach (var enemy in GM.waves.Alive) if (enemy && enemy.gameObject.activeSelf) Freeze(enemy.gameObject);
            // Also freeze delayed death effects (Bloater is already removed from Alive before becoming a corpse).
            foreach(var enemy in GM.waves.preparedEnemies)if(enemy&&enemy.gameObject.activeSelf)Freeze(enemy.gameObject);
            if (AttackPool.Instance) foreach (var projectile in AttackPool.Instance.actors) if (projectile && projectile.gameObject.activeSelf) Freeze(projectile.gameObject);
            foreach (var bolt in GM.waves.preparedBolts) if (bolt.gameObject.activeSelf) Freeze(bolt.gameObject);
            GM.waves.SuspendForTrial();
            foreach (var wall in temporaryWalls) if (wall && wall.gameObject.activeSelf) wall.BattlefieldFrozen = true;
            if (decoy && decoy.gameObject.activeSelf) Freeze(decoy.gameObject);
            TrialActive = true;
            trialRadius = BossNumbers.prophetPlatformRadii.x;
            trialPlatform.gameObject.SetActive(true);
            trialPlatform.localScale = new Vector3(trialRadius * 2f, trialPlatform.localScale.y, trialRadius * 2f);
            MovePlayer(player, trialPlatform.position + Vector3.back * trialEntryDistance);
            // Document 12.5: entering clears every status and empties the shield.
            var status = ElementStatus.For(player);
            if (status) { status.ClearStatuses(); status.SetShield(0f); }
            hollow.gameObject.SetActive(true);
            hollow.transform.position = trialPlatform.position;
            hollow.Configure(hollow.definition, GM.waves.HpMultiplier, false);
            hollow.HoldAttacks(trialOpeningGrace);
            hollow.IsTrialSummon = true;
            hollow.SuppressRewards = true;
            hollow.BeginProphetFight();
            if (!hollow.GetComponent<V2ProphetTrialMarks>()) hollow.gameObject.AddComponent<V2ProphetTrialMarks>();
            BuildTrialTerrain(); // the fragments round the platform and the portals to them (WorldMap.Trial.cs)
            GM.waves.TrackBoss(hollow);
            // Since 2026-11-10 the banner does not explain the portals: each shows its own E once the player stands on it.
            if (GM.hud) GM.hud.Banner(ArenaText.Pair("纳什先知", "THE NASH PROPHET"),
                ArenaText.Pair("只要它的召唤物还活着，它就不会受伤。", "As long as its summons are alive, it can't be hurt."));
            VoidTrialEntered?.Invoke();
        }
        /// <summary>Enemy lab ("player cannot die"): a fall sets the body back on safe ground instead of ending the run. In the trial
        /// it lands well inside the edge on the side it went over; on the map, just outside the gravity ring of the hole it fell into.</summary>
        public void SetBackOnSafeGround(Combatant body)
        {
            Vector3 at = body.Position;
            if (TrialActive)
            {
                Vector3 outward = Query.Flat(at - trialPlatform.position);
                if (outward.sqrMagnitude < .01f) outward = Vector3.back;
                at = trialPlatform.position + outward.normalized * Mathf.Max(0f, trialRadius - 4f);
            }
            else
            {
                foreach (var hole in holes)
                {
                    if (!hole || hole.Phase == HolePhase.Gone || Query.Flat(at - hole.Centre).magnitude > hole.GravityRadius) continue;
                    Vector3 away = Query.Flat(at - hole.Centre);
                    if (away.sqrMagnitude < .01f) away = Vector3.back;
                    // The side it came from first, then around the ring until a spot is free of walls.
                    for (int step = 0; step < 8; step++)
                    {
                        Vector3 spot = ClampPoint(hole.Centre + Quaternion.Euler(0f, 45f * step, 0f) * away.normalized * (hole.GravityRadius + 1.5f), body.Radius);
                        if (!IsWall(spot, body.Radius)) { at = spot; break; }
                    }
                }
                at = ClampPoint(at, body.Radius);
            }
            at.y = 0f;
            body.CancelDisplacement();
            body.GetComponent<Rigidbody>().position = at;
            body.transform.position = at;
        }
        void Freeze(GameObject go) { frozen.Add(go); go.SetActive(false); }
        public bool IsBattlefieldFrozen(GameObject go) => TrialActive && frozen.Contains(go);
        static void MovePlayer(PlayerController player, Vector3 point)
        {
            player.SetState(CharacterState.Normal);
            var body = player.GetComponent<Rigidbody>();
            point.y = 0f;
            body.linearVelocity = Vector3.zero;
            body.position = point;
            player.transform.position = point;
        }

        public void CompleteTrial()
        {
            if (!TrialActive) return;
            foreach(var enemy in GM.waves.preparedEnemies)if(enemy&&enemy.CorpseActive&&enemy.IsTrialSummon)enemy.DespawnCorpse(enemy.SpawnVersion);
            GM.waves.ClearTrialSummons();
            if (hollow) hollow.ClearBossMarks();
            HideBossHoles();
            HideTrialTerrain();
            foreach (var wall in trialWalls) if (wall) wall.gameObject.SetActive(false);
            trialWalls.Clear();
            foreach (var wall in temporaryWalls) if (wall) wall.BattlefieldFrozen = false;
            if (trialDecoy) trialDecoy.gameObject.SetActive(false);
            if (trialTemporaryHole) trialTemporaryHole.gameObject.SetActive(false);
            if(trialExtraTemporaryHoles!=null)foreach(var hole in trialExtraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            TrialActive = false;
            TrialWon = !labTrial;
            // Boss shots still flying over the platform end with it; otherwise they would hold pool slots the Spitters need.
            if (GM.waves.preparedBolts != null)
                foreach (var bolt in GM.waves.preparedBolts)
                    if (bolt && bolt.gameObject.activeSelf && !frozen.Contains(bolt.gameObject)) bolt.gameObject.SetActive(false);
            foreach (var go in frozen) if (go) go.SetActive(true);
            frozen.Clear();
            GM.waves.ResumeAfterTrial();
            MovePlayer(GM.player, returnPoint);
            GM.player.Heal(GM.player.maxHp);
            // Document 12.4: winning the trial removes every black hole, but only once the player is back and has watched the void power
            // being absorbed: GameManager calls RemoveHolesAfterTrial when the return freeze ends.
            hollow.gameObject.SetActive(false);
            trialPlatform.gameObject.SetActive(false);
            VoidTrialWon?.Invoke();
        }
        public void OnWaveEnded(int oneBasedWave)
        {
            ClearSavedDeathEffects();
            // V2 day campaign (since 2026-11-06): the hidden gate stays open until the player goes through, however many days pass.
            if (!CampaignMapApplied) foreach (var hole in holes) hole.WaveEnded(oneBasedWave);
            foreach (var wall in temporaryWalls) if (wall) { wall.BattlefieldFrozen = false; wall.gameObject.SetActive(false); }
            if (temporaryHole) temporaryHole.gameObject.SetActive(false);
            if (trialTemporaryHole) trialTemporaryHole.gameObject.SetActive(false);
            if(extraTemporaryHoles!=null)foreach(var hole in extraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            if(trialExtraTemporaryHoles!=null)foreach(var hole in trialExtraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            HideBossHoles();
            if (decoy) decoy.gameObject.SetActive(false);
            if (trialDecoy) trialDecoy.gameObject.SetActive(false);
            HoleChanged();
        }
        /// <summary>After a won trial: every black hole, the portal included, turns back into ordinary ground (document 12.4).</summary>
        public void RemoveHolesAfterTrial()
        {
            if (!TrialWon) return;
            foreach (var hole in holes) if (hole && hole.Phase != HolePhase.Gone) hole.Close();
            CloseCampaignHoleAfterTrial(); // V2 day campaign: its hole closes also while it is put away for the night
        }
        public void ClosePortals() { foreach (var hole in holes) if (hole.Phase == HolePhase.Portal || hole.Phase == HolePhase.Collapsing) hole.Close(); }
        public void ClearTemporaryObjects()
        {
            ClearSavedDeathEffects();
            if(temporaryHole)temporaryHole.gameObject.SetActive(false);
            if(trialTemporaryHole)trialTemporaryHole.gameObject.SetActive(false);
            if(extraTemporaryHoles!=null)foreach(var hole in extraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            if(trialExtraTemporaryHoles!=null)foreach(var hole in trialExtraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            HideBossHoles();
            foreach(var wall in temporaryWalls)if(wall){wall.BattlefieldFrozen=false;wall.gameObject.SetActive(false);}
            if(decoy)decoy.gameObject.SetActive(false);if(trialDecoy)trialDecoy.gameObject.SetActive(false);
            if(Grid!=null)HoleChanged();
        }
        void ClearSavedDeathEffects()
        {
            if(!GM||!GM.waves||GM.waves.preparedEnemies==null)return;
            foreach(var enemy in GM.waves.preparedEnemies)
            {
                if(!enemy)continue;
                if(enemy.CorpseActive)enemy.DespawnCorpse(enemy.SpawnVersion);
                // A dead Bloater delay or a swallowed falling body is not a surviving enemy.
                // Stop these saved death effects at settlement without another kill/XP callback.
                else if(enemy.State is CharacterState.Dead or CharacterState.Falling)enemy.DiscardWithoutReward();
            }
        }

        public void CreateWall(Vector3 at, float width, float life, float endKnockback, Combatant owner)
        {
            foreach (var wall in temporaryWalls) if (!wall.gameObject.activeSelf) { wall.Deploy(at + Vector3.up, width, life, endKnockback, owner); if (TrialActive) trialWalls.Add(wall); return; }
        }
        public void CreateDecoy(Vector3 at, float life, bool explodeAtEnd, Combatant owner)
        {
            if (!ActiveDecoy) return;
            ActiveDecoy.position = at;
            if (TrialActive) { trialDecoyLife = life; trialDecoyBlast = explodeAtEnd; trialDecoyOwner = owner; }
            else { decoyLife = life; decoyBlast = explodeAtEnd; decoyOwner = owner; }
            ActiveDecoy.gameObject.SetActive(true);
        }
        /// <summary>Whether owner's Bulwark wall still stands, its mirror image is still out or its Pocket Void hole is still open where the
        /// player is now: on the battlefield, or in the void trial while it runs (battlefield ones held for the trial do not count).
        /// SkillLoadout asks before a cast: a skill cannot be cast again while its own effect lasts.</summary>
        public bool SkillObjectStanding(SkillId skill, Combatant owner) => owner && skill switch
        {
            SkillId.Bulwark => WallStanding(owner),
            SkillId.Decoy => DecoyActive && (TrialActive ? trialDecoyOwner : decoyOwner) == owner,
            SkillId.PocketVoid => VoidHoleOpen(owner),
            _ => false
        };
        bool WallStanding(Combatant owner)
        {
            if (temporaryWalls != null)
                foreach (var wall in temporaryWalls)
                    if (wall && wall.gameObject.activeSelf && !wall.BattlefieldFrozen && wall.Owner == owner) return true;
            return false;
        }
        /// <summary>Only a boss breaks a Bulwark wall (document 0262): every standing one touched says yes goes at once, whole, as when its
        /// time is up, with a puff of rubble. Battlefield walls held for the trial do not count. Returns how many broke.</summary>
        public int BreakBulwarks(Func<TemporaryWall, bool> touched)
        {
            int broken = 0;
            if (temporaryWalls != null)
                foreach (var wall in temporaryWalls)
                {
                    if (!wall || !wall.gameObject.activeInHierarchy || wall.BattlefieldFrozen || !touched(wall)) continue;
                    Vector3 at = wall.transform.position;
                    TyrantTerrain.Rubble(new Vector2(at.x, at.z));
                    wall.Fall();
                    broken++;
                }
            if (broken > 0) HoleChanged();
            return broken;
        }
        /// <summary>The Nash Prophet's repulse pulse (document 7.4: only a boss breaks a Bulwark): every Bulwark wall any part of which lies within radius of centre,
        /// seen from above, breaks.</summary>
        public int BreakBulwarksWithin(Vector3 centre, float radius) => BreakBulwarks(wall =>
        {
            Vector3 c = Query.Flat(centre);
            if (!wall.TryGetComponent(out Collider body) || !body.enabled) return Query.Flat(wall.transform.position - c).magnitude <= radius;
            Vector3 near = body.ClosestPoint(new Vector3(c.x, wall.transform.position.y, c.z));
            return Query.Flat(near - c).magnitude <= radius;
        });
        bool VoidHoleOpen(Combatant owner)
        {
            for (int i = 0; i < PlayerHoleCount; i++) { var hole = TemporaryHoleAt(i); if (hole && hole.gameObject.activeSelf && hole.Owner == owner) return true; }
            return false;
        }
        public void CreateTemporaryHole(Vector3 at, float width, float life, Combatant owner) => CreateVoidHole(at,width,1f,life,owner,false);
        public void CreateVoidHole(Vector3 at,float diameter,float gravityWidth,float life,Combatant owner,bool protectOwner)
        {
            BlackHole slot=null;
            for(int i=0;i<PlayerHoleCount;i++){var hole=TemporaryHoleAt(i);if(!hole)continue;if(!hole.gameObject.activeSelf){slot=hole;break;}if(!slot||hole.TemporarySecondsLeft<slot.TemporarySecondsLeft)slot=hole;}
            if(slot){slot.MakeTemporary(at,diameter,life,owner,protectOwner,gravityWidth);HoleChanged();}
        }
        public void FeedNearestHole(Vector3 at, float reach, int count)
        {
            BlackHole best = null;
            float distance = reach;
            foreach (var hole in holes) { float d = Query.Flat(hole.Centre - at).magnitude; if (hole.Phase == HolePhase.Active && d < distance) { best = hole; distance = d; } }
            if (best) best.Feed(count, GM.waves.WaveIndex + 1);
        }
        public Vector3? ClosestHole(Vector3 at, float reach)
        {
            BlackHole best = null;
            float distance = reach;
            foreach (var hole in holes) { float d = Query.Flat(hole.Centre - at).magnitude; if (hole.Phase == HolePhase.Active && d < distance) { best = hole; distance = d; } }
            return best ? best.Centre : (Vector3?)null;
        }
        public Vector3? ValidPoint(Vector3 from, Vector3 desired)
        {
            if (TrialActive)
            {
                // A point on a fragment stays where it is; anything else in the trial is drawn back onto the platform.
                if (!OnTrialGround(desired, .5f))
                    desired = trialPlatform.position + Vector3.ClampMagnitude(Query.Flat(desired-trialPlatform.position),Mathf.Max(0f,trialRadius-.5f));
            }
            else desired = ClampPoint(desired);
            Vector3 delta = Query.Flat(desired - from);
            for (float distance = delta.magnitude; distance >= 0f; distance -= 0.5f)
            {
                Vector3 at = from + delta.normalized * distance;
                bool inside = TrialActive ? OnTrialGround(at, .5f) : Contains(at,.5f);
                if (inside && !IsWall(at, 0.5f) && !IsHole(at) && !IsTemporaryHole(at)) return at;
            }
            return null;
        }
    }
}
