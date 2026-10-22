using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
                                                                                                     
    [DefaultExecutionOrder(200)]
    public sealed class WorldMap : MonoBehaviour
    {
        public static WorldMap Active { get; private set; }
        public MapLayout layout;
        public Biome biome;
        public Biome[] biomes;
        public Transform[] biomeWallGroups;
        public Renderer floorRenderer;
        public BlackHole[] holes;
        [Header("Add black-hole suction audio.")]
        [Tooltip("Additional attraction width for permanent map holes; temporary skill holes retain their settings.")] public float mapGravityExtraCells = 1f;
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
        public Transform decoy;
        public Transform trialDecoy;
        int TemporaryHoleCount => 1 + (TrialActive ? trialExtraTemporaryHoles?.Length ?? 0 : extraTemporaryHoles?.Length ?? 0);
        BlackHole TemporaryHoleAt(int index) => index == 0 ? TrialActive ? trialTemporaryHole : temporaryHole : TrialActive ? trialExtraTemporaryHoles[index-1] : extraTemporaryHoles[index-1];
        bool IsTemporaryHole(Vector3 point, Combatant actor = null)
        {
            for(int i=0;i<TemporaryHoleCount;i++){var hole=TemporaryHoleAt(i);if(hole&&hole.gameObject.activeSelf&&hole.Contains(point,actor))return true;}
            return false;
        }
        Transform ActiveDecoy => TrialActive ? trialDecoy : decoy;
        public bool DecoyActive => ActiveDecoy && ActiveDecoy.gameObject.activeSelf;
        public Vector3 EnemyTarget => DecoyActive ? ActiveDecoy.position : GM.player.Position;
        public float trialRadius = 12f;
        public bool TrialActive { get; private set; }
        public bool TrialWon { get; private set; }
        public MapGrid Grid { get; private set; }
        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(layout ? layout.width : 100, 20, layout ? layout.height : 70));
        public event Action VoidTrialEntered;
        public event Action VoidTrialWon;
        public event Action<BlackHole> HoleFed;
                                                                                                                                  
                                                                                                                                      
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
                                                                                                                      
        public bool HasOpenPortal
        {
            get { foreach (var hole in holes) if (hole && hole.Phase == HolePhase.Portal) return true; return false; }
        }
        Vector3 returnPoint;
        BlackHole enteredHole;
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

        void Awake()
        {
            Active = this;
            if (biomes != null && biomes.Length > 0)
            {
                int chosen = UnityEngine.Random.Range(0, biomes.Length);
                biome = biomes[chosen];
                layout = biome.layout;
                for (int i = 0; i < biomeWallGroups.Length; i++) biomeWallGroups[i].gameObject.SetActive(i == chosen);
                RenderSettings.ambientLight = biome.ambient;
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
            Grid = new MapGrid(layout ? layout.width : 100, layout ? layout.height : 70);
            foreach (var hole in holes) hole.ResetHole();
            if (layout && layout.holeAnchors != null && layout.holeAnchors.Length >= 2)
            {
                int first = UnityEngine.Random.Range(0, layout.holeAnchors.Length);
                int second = (first + 1 + UnityEngine.Random.Range(0, layout.holeAnchors.Length - 1)) % layout.holeAnchors.Length;
                holes[0].transform.position = new Vector3(layout.holeAnchors[first].x, 0, layout.holeAnchors[first].y);
                holes[1].transform.position = new Vector3(layout.holeAnchors[second].x, 0, layout.holeAnchors[second].y);
            }
            Grid.Rebuild(this);
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
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        public bool Contains(Vector3 point, float margin = 0f) => Mathf.Abs(point.x) <= Bounds.extents.x - margin && Mathf.Abs(point.z) <= Bounds.extents.z - margin;
        public Vector3 ClampPoint(Vector3 point, float radius = 0.5f) => new Vector3(Mathf.Clamp(point.x, Bounds.min.x + radius, Bounds.max.x - radius), point.y, Mathf.Clamp(point.z, Bounds.min.z + radius, Bounds.max.z - radius));
        public bool IsWall(Vector3 point, float margin = 0f)
        {
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
        public Vector3 GetChaseDirection(Vector3 position, Vector3 target, float speed)
        {
            if (TrialActive) return Query.Flat(target - position).normalized;
            Vector3 pull = GetPull(position);
            if (pull.sqrMagnitude > 0.01f && GetPull(target).sqrMagnitude < 0.01f)
                return -pull.normalized;                                                                
            bool allow = GetPull(target).sqrMagnitude > 0.01f && speed >= 4.5f;
            return Grid.Direction(position, target, allow);
        }
        public void HoleChanged()
        {
            Grid.Rebuild(this);
            lastCell = -1;
        }

        void Update()
        {
            if (!GM || !GM.player) return;
            TickHoleIntro();
            if (GM.IsCombatActive || GM.CanPlayerMove)
            {
                float dt = GM.WorldDeltaTime;
                                                                                                  
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
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) TryEnterPortal(GM.player);
                if (TrialActive) TickDecoy(ref trialDecoyLife, trialDecoy, trialDecoyBlast, trialDecoyOwner, dt);
                else TickDecoy(ref decoyLife, decoy, decoyBlast, decoyOwner, dt);
            }
        }

        const float HoleIntroApproach = .9f, HoleIntroSpiral = 2.4f, HoleIntroStagger = .45f, HoleIntroSink = .6f, HoleIntroHold = 1.6f, HoleIntroZoom = .62f;

                                                                                                                                       
                                                                                               
        void TickHoleIntro()
        {
            if (holeIntroStage == HoleIntroStage.Done) return;
            if (holeIntroStage == HoleIntroStage.Idle)
            {
                if (TrialActive || !GM.IsCombatActive || GM.waves.Phase != WavePhase.WaveActive) return;
                BlackHole seen = null;
                foreach (var hole in holes)
                    if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0 && (SkillWorldAPI.IsOnScreen?.Invoke(hole.Centre) ?? false)) { seen = hole; break; }
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
                    var body = GM.waves.SpawnIntroFodder(at);
                    if (!body) continue;
                    body.enabled = false;                                                               
                    holeIntroFodder.Add(new IntroFodder
                    {
                        body = body, angle = Mathf.Atan2(dir.x, dir.z), startRadius = startRadius, radius = startRadius,
                        scale = body.bodyRenderer ? body.bodyRenderer.transform.localScale : Vector3.one,
                    });
                }
                if (holeIntroFodder.Count == 0) { holeIntroStage = HoleIntroStage.Done; return; }
                holeIntroStage = HoleIntroStage.Approach;
                holeIntroClock = 0f;
                GM.BeginCutscene();
                holeIntroRig = GM.cam ? GM.cam.GetComponent<CameraRig>() : null;
                holeIntroRig?.Focus(seen.Centre, HoleIntroZoom, HoleIntroApproach);
                GM.hud?.Banner(ArenaText.Pair("BLACK HOLE","BLACK HOLE"), ArenaText.Pair("Enemies swallowed alive are counted. Feed it 40 to open the portal.","Enemies swallowed alive are counted. Feed it 40 to open the portal."));
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
            foreach (var f in holeIntroFodder) if (f.body) f.body.enabled = true;                                                     
            holeIntroRig?.ClearFocus(.8f);
            GM.EndCutscene();
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
                                                                                                                            
            SkillWorldAPI.DecoyEnded?.Invoke(visual.position);
        }

        void FixedUpdate()
        {
            if (!GM || (!GM.IsCombatActive && !GM.CanPlayerMove) || !GM.player) return;
            characters.Clear();
            characters.Add(GM.player);
            foreach (var enemy in GM.waves.Alive) if (enemy && enemy.gameObject.activeInHierarchy) characters.Add(enemy);
            foreach (var combatant in characters)
            {
                if (!combatant.IsAlive || combatant.State == CharacterState.Hooked) continue;
                if (SkillWorldAPI.HoleProtection != null && SkillWorldAPI.HoleProtection(combatant)) continue;
                if (TrialActive)
                {
                    float distance = Query.Flat(combatant.Position - trialPlatform.position).magnitude;
                    if (distance > trialRadius)
                    {
                        combatant.OnEnterFallZone(Vector3.zero);
                        if (combatant.Team == Team.Enemy && combatant != hollow && hollow && hollow.IsAlive)
                            hollow.ReceiveDamage(DamageInfo.Attack(hollow.maxHp * 0.05f, GM.player));
                    }
                    else if (IsTemporaryHole(combatant.Position,combatant) && combatant != hollow) SwallowIntoVoid(combatant);
                    else if (distance > trialRadius - 2f) ApplyPull(combatant, Query.Flat(combatant.Position - trialPlatform.position).normalized * 3f);
                    else ApplyHolePull(combatant);
                    if (hollow && hollow.IsAlive && hollow.Inhaling && combatant != hollow)
                    {
                        ApplyPull(combatant, Query.Flat(hollow.Position - combatant.Position).normalized * 4f);
                        if (Query.Flat(hollow.Position - combatant.Position).magnitude <= hollow.Radius + combatant.Radius)
                            combatant.ReceiveDamage(DamageInfo.Attack(10f, hollow, combatant.Position - hollow.Position, 8f));
                    }
                    continue;
                }
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
                                                                                
                if (!swallowed && IsTemporaryHole(combatant.Position,combatant) && !(combatant is Enemy { IsBoss: true })) { SwallowIntoVoid(combatant); swallowed = true; }
                if (!swallowed) ApplyHolePull(combatant);
            }
            TickCorpses(GM.WorldFixedDeltaTime);
                                                                                                             
            if (AttackPool.Instance)
                foreach (var projectile in AttackPool.Instance.actors)
                    if (projectile && projectile.gameObject.activeSelf && projectile.AffectedByHoles && IsHole(projectile.transform.position)) projectile.SwallowedByHole();
        }

                                                                                                                                                  
        void SwallowIntoVoid(Combatant combatant)
        {
            if (combatant is Enemy enemy && enemy.IsAlive && !enemy.SuppressRewards && !enemy.IsTrialSummon && GM.progression && GM.waves)
                GM.progression.GainXp(GM.waves.ExperienceFor(enemy));
            combatant.OnEnterFallZone(Vector3.zero);
        }

        static void ApplyPull(Combatant combatant, Vector3 pull)
        {
            if (combatant.State == CharacterState.Dashing) return;
            var body = combatant.GetComponent<Rigidbody>();
            if (body && !body.isKinematic) body.linearVelocity += pull * (combatant.Team == Team.Player ? 1f : GM.WorldTimeScale);
        }
        void ApplyHolePull(Combatant combatant)
        {
            ApplyPull(combatant,GetPull(combatant.Position,combatant));
            if(combatant is not PlayerController player||player.stamina.Sprinting||player.State!=CharacterState.Normal)return;
            var body=player.GetComponent<Rigidbody>();if(!body||body.isKinematic)return;
            if(!TrialActive)foreach(var hole in holes)ConstrainWalkingInGravity(player,body,hole);
            for(int i=0;i<TemporaryHoleCount;i++){var hole=TemporaryHoleAt(i);if(hole&&hole.gameObject.activeSelf)ConstrainWalkingInGravity(player,body,hole);}
        }
        static void ConstrainWalkingInGravity(PlayerController player,Rigidbody body,BlackHole hole)
        {
            if(!hole||hole.Pull(player.Position,player).sqrMagnitude<.001f)return;
            Vector3 offset=Query.Flat(player.Position-hole.Centre);float radius=offset.magnitude;
            if(radius<.001f)return;
            float dt=Time.fixedDeltaTime;Vector3 next=offset+Query.Flat(body.linearVelocity)*dt;
                                                                                                                              
            if(next.sqrMagnitude>radius*radius)
            {
                Vector3 velocity=(next.normalized*radius-offset)/Mathf.Max(.001f,dt);
                body.linearVelocity=new Vector3(velocity.x,body.linearVelocity.y,velocity.z);
            }
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

                                                                                                                                                                         
        public bool TryEnterPortal(PlayerController player, bool fromBuild = false)
        {
            if (!player || TrialActive || TrialWon) return false;
            bool build = fromBuild && GM.RunPhase is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready;
                                                                                                               
            bool walking = !fromBuild && GM.BuildWalking && player.State == CharacterState.Normal;
            if (!build && !walking && (!GM.IsCombatActive || GM.waves.Phase != WavePhase.WaveActive || player.State != CharacterState.Normal)) return false;
            foreach (var hole in holes)
            {
                if (hole.Phase != HolePhase.Portal) continue;
                if (!build && (Mathf.Abs(player.Position.x - hole.Centre.x) > 0.5f || Mathf.Abs(player.Position.z - hole.Centre.z) > 0.5f)) continue;
                enteredHole = hole;
                returnPoint = hole.Centre;
                frozen.Clear();
                foreach (var enemy in GM.waves.Alive) if (enemy && enemy.gameObject.activeSelf) Freeze(enemy.gameObject);
                                                                                                                  
            foreach(var enemy in GM.waves.preparedEnemies)if(enemy&&enemy.gameObject.activeSelf)Freeze(enemy.gameObject);
                if (AttackPool.Instance) foreach (var projectile in AttackPool.Instance.actors) if (projectile && projectile.gameObject.activeSelf) Freeze(projectile.gameObject);
                foreach (var bolt in GM.waves.preparedBolts) if (bolt.gameObject.activeSelf) Freeze(bolt.gameObject);
                GM.waves.SuspendForTrial();
                foreach (var wall in temporaryWalls) if (wall && wall.gameObject.activeSelf) wall.BattlefieldFrozen = true;
                if (decoy && decoy.gameObject.activeSelf) Freeze(decoy.gameObject);
                TrialActive = true;
                trialRadius = 12f;
                trialPlatform.gameObject.SetActive(true);
                MovePlayer(player, trialPlatform.position + Vector3.back * 6f);
                ElementStatus.For(player)?.ClearStatuses();
                hollow.gameObject.SetActive(true);
                hollow.transform.position = trialPlatform.position;
                hollow.Configure(hollow.definition, GM.waves.HpMultiplier, false);
                hollow.IsTrialSummon = true;
                hollow.SuppressRewards = true;
                GM.waves.TrackBoss(hollow);
                VoidTrialEntered?.Invoke();
                return true;
            }
            return false;
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
            foreach (var wall in trialWalls) if (wall) wall.gameObject.SetActive(false);
            trialWalls.Clear();
            foreach (var wall in temporaryWalls) if (wall) wall.BattlefieldFrozen = false;
            if (trialDecoy) trialDecoy.gameObject.SetActive(false);
            if (trialTemporaryHole) trialTemporaryHole.gameObject.SetActive(false);
            if(trialExtraTemporaryHoles!=null)foreach(var hole in trialExtraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            TrialActive = false;
            TrialWon = true;
            foreach (var go in frozen) if (go) go.SetActive(true);
            frozen.Clear();
            GM.waves.ResumeAfterTrial();
            MovePlayer(GM.player, returnPoint);
            GM.player.Heal(GM.player.maxHp);
                                                                                                                         
            foreach (var hole in holes) if (hole && hole.Phase != HolePhase.Gone) hole.Close();
            hollow.gameObject.SetActive(false);
            trialPlatform.gameObject.SetActive(false);
            VoidTrialWon?.Invoke();
        }
        public void OnWaveEnded(int oneBasedWave)
        {
            ClearSavedDeathEffects();
            foreach (var hole in holes) hole.WaveEnded(oneBasedWave);
            foreach (var wall in temporaryWalls) if (wall) { wall.BattlefieldFrozen = false; wall.gameObject.SetActive(false); }
            if (temporaryHole) temporaryHole.gameObject.SetActive(false);
            if (trialTemporaryHole) trialTemporaryHole.gameObject.SetActive(false);
            if(extraTemporaryHoles!=null)foreach(var hole in extraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            if(trialExtraTemporaryHoles!=null)foreach(var hole in trialExtraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            if (decoy) decoy.gameObject.SetActive(false);
            if (trialDecoy) trialDecoy.gameObject.SetActive(false);
            HoleChanged();
        }
        public void ClosePortals() { foreach (var hole in holes) if (hole.Phase == HolePhase.Portal || hole.Phase == HolePhase.Collapsing) hole.Close(); }
        public void ClearTemporaryObjects()
        {
            ClearSavedDeathEffects();
            if(temporaryHole)temporaryHole.gameObject.SetActive(false);
            if(trialTemporaryHole)trialTemporaryHole.gameObject.SetActive(false);
            if(extraTemporaryHoles!=null)foreach(var hole in extraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
            if(trialExtraTemporaryHoles!=null)foreach(var hole in trialExtraTemporaryHoles)if(hole)hole.gameObject.SetActive(false);
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
        public void CreateTemporaryHole(Vector3 at, float width, float life, Combatant owner) => CreateVoidHole(at,width,1f,life,owner,false);
        public void CreateVoidHole(Vector3 at,float diameter,float gravityWidth,float life,Combatant owner,bool protectOwner)
        {
            BlackHole slot=null;
            for(int i=0;i<TemporaryHoleCount;i++){var hole=TemporaryHoleAt(i);if(!hole)continue;if(!hole.gameObject.activeSelf){slot=hole;break;}if(!slot||hole.TemporarySecondsLeft<slot.TemporarySecondsLeft)slot=hole;}
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
                desired = trialPlatform.position + Vector3.ClampMagnitude(Query.Flat(desired-trialPlatform.position),Mathf.Max(0f,trialRadius-.5f));
            else desired = ClampPoint(desired);
            Vector3 delta = Query.Flat(desired - from);
            for (float distance = delta.magnitude; distance >= 0f; distance -= 0.5f)
            {
                Vector3 at = from + delta.normalized * distance;
                bool inside = TrialActive ? Query.Flat(at-trialPlatform.position).magnitude <= trialRadius-.5f : Contains(at,.5f);
                if (inside && !IsWall(at, 0.5f) && !IsHole(at) && !IsTemporaryHole(at)) return at;
            }
            return null;
        }
    }
}
