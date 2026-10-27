using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                 
                                                                                                                                                                             
                                                                                                                                                                                                     
                                                                                                                                                         
                  
    public partial class Enemy : Combatant
    {
        [Header("Attack")] public float attackDamage = 5f;

        [Tooltip(
            "Authored walk speed at or above which this enemy follows the player into a gravity zone. Compared before the global movement multiplier, so slowing everyone down does not switch the rule off.")]
        public float gravityFollowSpeed = 4.5f;

        [Tooltip("How close to the player's body edge before winding up (almost touching)")]
        public float triggerRange = 0.3f;

        [Tooltip(
            "Hit distance when the punch lands, from the body edge; the player dodges by leaving this range during the wind-up")]
        public float attackRange = 0.8f;

        [Tooltip("Arc of the punch in degrees; facing is locked when the wind-up starts")]
        public float attackArc = 120f;

        public float windupTime = 0.5f;
        public float attackInterval = 1f;

        [Tooltip("How far the player is knocked back when hit")]
        public float attackKnockback = 5f;

        [Header("Look")] public Material normalMaterial;
        public Material windupMaterial;

        [Header("Type")]
        [Tooltip("Large enemy: drops weapons, wider HP bar, uses the large textures (no longer decided by radius)")]
        public bool large;

        [Header("v2 content")] public EnemyDefinition definition;
        public bool pooled;
        public Transform telegraph;
        public ElementKind Affix { get; private set; }
        public bool Elite { get; private set; }

                                                                                                                      
        public bool Mini { get; private set; }

        public bool IsBoss => definition && EnemyRoles.IsBoss(definition.kind);
        public bool IsBossOrElite => IsBoss || Elite;

                                                                                                                                                          
        public bool IsSpecial => Elite || definition && EnemyRoles.IsSpecial(definition.kind);

        public override bool Immovable => IsBoss || definition && definition.kind == EnemyKind.Spire;
        public bool IsTrialSummon { get; set; }
        public bool SuppressRewards { get; set; }
        public bool Cloaked => definition && definition.kind == EnemyKind.Shade && revealTimer <= 0f;

        float spawnVisible,
            revealTimer,
            supportTimer,
            summonTimer,
            chargeLeft,
            inhaleLeft,
            bossRecovery,
            laserTimer;

        public bool Inhaling => IsBoss && definition.kind == EnemyKind.Hollow && inhaleLeft > 0f;
        bool charging, rushing, dyingExplosion;
        const float TyrantChargeDistance = 20f;
        float explosionTimer;

        public bool BloaterWarningActive => definition && definition.kind == EnemyKind.Bloater &&
                                            (IsAlive && windup || dyingExplosion && explosionTimer > 0f);

                                                                      
        public const int PulseMove = 0, InhaleMove = 1, DoubleShotMove = 2, RingMove = 3, ProphecyMove = 4;
                  
        public const int CleaveMove = 0, SlamMove = 1, ChargeMove = 2, BackSlashMove = 3, LeapMove = 4, RoyalShieldMove = 5;

        int bossPhase = 1, bossAttack;
        public int BossPhase => bossPhase;
        public int BossAttackIndex => bossAttack;
        public bool TelegraphActive => (windup || leaping) && telegraph && telegraph.gameObject.activeSelf;
                                                                                                                                                   
        public bool WindupTinted => windup && specialWindup || rushing;
                                                                                       
        public bool Rushing => rushing;
        bool specialWindup;

        public bool LineTelegraph => definition && (definition.kind == EnemyKind.Rammer ||
                                                    definition.kind == EnemyKind.Tyrant && bossAttack is ChargeMove or RampageChargeMove);

        public bool CleaveTelegraph => definition && definition.kind == EnemyKind.Tyrant &&
                                       bossAttack is CleaveMove or BackSlashMove or TailSweepMove;

                                                                                                                                                     
        public bool CircleTelegraph => IsBoss && (definition.kind == EnemyKind.Hollow && bossAttack == PulseMove ||
                                                  definition.kind == EnemyKind.Tyrant && bossAttack is SlamMove or LeapMove);

        static BossTuning Boss => GM && GM.waves && GM.waves.bosses != null ? GM.waves.bosses : BossTuning.Defaults;

                                                                                                                                  
        int forcedMove = -1, followUp = -1, chargesLeft, ringsLeft, ringIndex;
        float ringTimer, volleyTimer, leapTimer;
                                                                                                     
        float ringBaseAngle;
                                                                                                                             
        bool royalCastPending;
        bool leaping, royalShield, openingMove, inhaleHit;
        Vector3 leapFrom, leapTarget;

                                                                                                        
        public bool Leaping => leaping;
        public Vector3 LeapTarget => leapTarget;

                                                                                                                                 
        public struct LandingZone
        {
            public Vector3 centre;
            public float radius, age, fill;
            public float Fill01 => fill > 0f ? Mathf.Clamp01(age / fill) : 1f;
        }

        readonly List<LandingZone> landingZones = new();
        public IReadOnlyList<LandingZone> LandingZones => landingZones;
        readonly List<WaveSpawnMarker> summonMarkers = new();
        readonly List<Vector3> summonPoints = new();
        EnemyKind summonKind;
        float summonWarning;

                                                                                                                              
        public void ForceNextBossMove(int move) => forcedMove = move;
        public float TelegraphRange { get; private set; }
        public Vector3 TelegraphFacing => facing;
        ElementStatus statusBoard;
        Vector3 savedBodyScale;
        float savedCapsuleRadius = -1f;

                                                     
                                                                                                                                            
        public bool LaserLocking => laserTimer > 0f;

                                                                                            
        public Vector3 LaserDirection { get; private set; } = Vector3.forward;

                                                                                                        
        public float LaserLength { get; private set; }

                                                                                
        public int LaserSequence { get; private set; }

                                                      
        float chantStamp = -1f;
        bool chantCached;

                                                                                  
        public static float ChantRadius => GM && GM.waves ? Mathf.Max(0f, GM.waves.chantRadius) : 6f;

        static float ChantDamageBonus => GM && GM.waves ? Mathf.Max(0f, GM.waves.chantDamageBonus) : .3f;

                                                                                                                                                       
        public bool Chanted
        {
            get
            {
                float now = Time.fixedTime;
                if (chantStamp != now)
                {
                    chantStamp = now;
                    chantCached = ScanChanters();
                }

                return chantCached;
            }
        }

                                                                                                                          
        public float OutgoingDamageFactor => Chanted ? 1f + ChantDamageBonus : 1f;

        bool ScanChanters()
        {
            if (!definition || !IsAlive || IsBoss || definition.kind == EnemyKind.Chanter || !GM || !GM.waves)
                return false;
            float limit = ChantRadius * ChantRadius;
            var others = GM.waves.Alive;
            for (int i = 0; i < others.Count; i++)
            {
                var chanter = others[i];
                if (!chanter || chanter == this || !chanter.definition ||
                    chanter.definition.kind != EnemyKind.Chanter || !chanter.IsAlive) continue;
                if (Query.Flat(chanter.Position - Position).sqrMagnitude <= limit) return true;
            }

            return false;
        }

                                                           
                                                                                                                                                            
        [Serializable]
        public struct RadiationZone
        {
            public Vector3 position;
            public float radius;
            public float timeLeft;
            public float life;
            public float Remaining01 => life > 0f ? Mathf.Clamp01(timeLeft / life) : 0f;
        }

        static readonly List<RadiationZone> radiationZones = new();
        public static IReadOnlyList<RadiationZone> RadiationZones => radiationZones;

        public static void AddRadiationZone(Vector3 at, float radius, float life)
        {
            if (radius <= 0f || life <= 0f) return;
            radiationZones.Add(new RadiationZone
                { position = Query.Flat(at), radius = radius, timeLeft = life, life = life });
        }

                                                                                                                              
        public static bool TickRadiationZones(float dt, Vector3 point)
        {
            bool inside = false;
            for (int i = radiationZones.Count - 1; i >= 0; i--)
            {
                var zone = radiationZones[i];
                zone.timeLeft -= dt;
                if (zone.timeLeft <= 0f)
                {
                    radiationZones.RemoveAt(i);
                    continue;
                }

                radiationZones[i] = zone;
                inside |= Query.Flat(point - zone.position).sqrMagnitude <= zone.radius * zone.radius;
            }

            return inside;
        }

        public static void ClearRadiationZones() => radiationZones.Clear();

                                                                                                                              
        float SpireLaserDelay => definition ? Mathf.Max(.01f, definition.laserDelay) : .6f;
        float SpireLaserLength => definition ? Mathf.Max(0f, definition.laserLength) : 12f;
        float SpireLaserHalfWidth => definition ? Mathf.Max(0f, definition.laserHalfWidth) : .6f;
        static float ChantInterval => GM && GM.waves ? Mathf.Max(.1f, GM.waves.chantInterval) : 3f;

        static float VoidIntervalFactor =>
            GM && GM.waves ? Mathf.Clamp(GM.waves.voidAffixIntervalFactor, .1f, 1f) : .7f;

        static float MiniScale => GM && GM.waves ? Mathf.Clamp(GM.waves.splitterMiniScale, .1f, 1f) : .6f;
        static float MiniDamageShare => GM && GM.waves ? Mathf.Clamp01(GM.waves.splitterMiniDamageShare) : .6f;

                                                                                                                                               
        public void Configure(EnemyDefinition data, float hpMultiplier, bool elite,
            ElementKind affix = ElementKind.None, bool mini = false)
        {
            CorpseActive = CorpseCarried = CorpseFeedConsumed = false;
            corpseSeconds = 0;
            corpseVelocity = Vector3.zero;
            float bodyScale = mini ? MiniScale : 1f;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule)
            {
                capsule.enabled = true;
                if (savedCapsuleRadius < 0f) savedCapsuleRadius = capsule.radius;
                capsule.radius = savedCapsuleRadius * bodyScale;
            }

            definition = data;
            Elite = elite;
            Mini = mini;
            Affix = data.canAffix ? affix : ElementKind.None;
            bool fodder = !elite && EnemyRoles.IsMelee(data.kind);
            var balance = GM && GM.waves && GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;
            maxHp = fodder ? data.hp : Mathf.Round(data.hp * hpMultiplier * (elite ? 3f : 1f));
            if (mini) maxHp = Mathf.Max(1f, Mathf.Round(maxHp * (balance ? balance.MiniShare(true) : .4f)));
            if (balance) maxHp = balance.MaxHp(data, GM.waves.WaveIndex + 1, elite, Affix, mini);
            moveSpeed = data.speed * (GM && GM.waves ? GM.waves.EnemySpeedFactor : 1f);
            radius = data.radius * bodyScale;
            attackDamage = data.damage * (elite ? 1.5f : 1f) * (mini ? MiniDamageShare : 1f) *
                           (GM && GM.waves ? GM.waves.EnemyDamageFactor : 1f);
            windupTime = data.windup;
            attackInterval =
                data.interval * (Affix == ElementKind.Void ? VoidIntervalFactor : 1f);                           
            attackRange = data.reach;
                                                                                                                                               
            var knock = KnockbackRules.Active;
            attackKnockback = !EnemyRoles.KnocksBackPlayer(data.kind) ? 0f
                : IsBoss ? data.knockback : knock.Distance(KnockbackRules.AttackTier(data.kind));
                                                                                                                                                
            knockbackScale = Immovable ? 0f : knock.ScaleFor(radius, elite);
            large = data.kind == EnemyKind.Stomper || IsBoss;                                                        
            ResetForSpawn();
            AttackSequence = 0;
            windup = false;
            charging = rushing = false;
            dyingExplosion = false;
            SuppressRewards = false;
            cooldown = 0.5f;
            spawnVisible = data.kind == EnemyKind.Shade ? 2f : 0f;
            revealTimer = spawnVisible;
            supportTimer = 0f;
            summonTimer = data.kind == EnemyKind.Hollow ? Boss.prophetSummonInterval : Boss.tyrantSummonInterval;
            bossPhase = 1;
            inhaleLeft = bossRecovery = 0f;
            ClearBossMarks();
            forcedMove = followUp = -1;
            chargesLeft = ringsLeft = ringIndex = 0;
            ringTimer = volleyTimer = leapTimer = ringBaseAngle = 0f;
            royalCastPending = false;
            leaping = royalShield = inhaleHit = false;
            openingMove = data.kind == EnemyKind.Hollow;
            if (EnemyRoles.IsBoss(data.kind) && !GetComponent<V2BossGroundMarks>()) gameObject.AddComponent<V2BossGroundMarks>();
            laserTimer = 0f;
            LaserLength = 0f;
            chantStamp = -1f;
            statusBoard = ElementStatus.For(this);
            if (statusBoard != null)
            {
                statusBoard.ClearStatuses();
                statusBoard.CarriesVoid = Affix == ElementKind.Void;
                statusBoard.SetShield(maxHp * (Affix == ElementKind.Earth ? 0.5f : data.startShieldShare));
                statusBoard.StatusApplied -= OnStatusApplied;
                statusBoard.StatusApplied += OnStatusApplied;
                statusBoard.ImmuneToControl = IsBoss;
                statusBoard.ControlThresholdFactor = (large ? 2f : 1f) * (elite ? 2f : 1f);
                if (data.kind == EnemyKind.Hollow)
                {
                                                                                                                                           
                    foreach (var element in ElementImmunity.Boss) statusBoard.SetImmunity(element, 1f);
                }
            }

            ConfigureTyrant(data);                                                                                                         

            if (bodyRenderer)
            {
                if (savedBodyScale == Vector3.zero) savedBodyScale = bodyRenderer.transform.localScale;
                bodyRenderer.transform.localScale = savedBodyScale * bodyScale;
                bodyRenderer.enabled = true;
                RefreshBodyTint();
            }

            if (telegraph) telegraph.gameObject.SetActive(false);
            if (rb)
            {
                rb.isKinematic = data.kind == EnemyKind.Spire;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }

        void OnStatusApplied(ElementKind element) => revealTimer = 2f;

        public override bool ReceiveDamage(DamageInfo info)
        {
            if (IsBoss)
            {
                                                                                                                          
                info.knockDistance = 0f;
            }
            else if (Immovable) info.knockDistance = 0f;

            if (TurnedAwayByWard()) return false;                                                                            
            bool accepted = base.ReceiveDamage(info);
                                                                                                                               
                                                                 
            if (accepted && IsAlive && GM && GM.runWeapons) GM.runWeapons.CheckExecuteMark(this);
            return accepted;
        }

        protected override void FixedUpdate()
        {
            if (ActionsBlocked)
            {
                CancelWindup();
                charging = false;
                laserTimer = 0f;
            }

            base.FixedUpdate();
        }

        public void SetAffix(ElementKind element)
        {
            Affix = element;
            if (statusBoard) statusBoard.CarriesVoid = element == ElementKind.Void;
            if (definition)
                attackInterval = definition.interval * (element == ElementKind.Void ? VoidIntervalFactor : 1f);
        }

        void Update()
        {
                                                                                                                                
            if (telegraph && (leaping || windup && IsBoss && definition.kind == EnemyKind.Tyrant && bossAttack == LeapMove))
                telegraph.position = leapTarget + Vector3.up * .03f;
            if (!dyingExplosion || !GM.IsCombatActive) return;
            float dt = GM.WorldDeltaTime;
            explosionTimer -= dt;
            if (bodyRenderer) bodyRenderer.transform.localScale *= 1f + dt * 0.6f;
            if (explosionTimer > 0f) return;
            Explode();
            dyingExplosion = false;
            BeginCorpse();
        }

        public bool IsLarge => large;
        public bool WindingUp => windup;
                                                                                                           
        public bool PushesPlayer => definition && EnemyRoles.KnocksBackPlayer(definition.kind);
        public bool Charging => charging;
        public float CooldownRemaining => Mathf.Max(0f, cooldown);
                                                                                                                
        public void HoldAttacks(float seconds) => cooldown = Mathf.Max(cooldown, seconds);
        public float WindupSecondsRemaining => windup ? Mathf.Max(0f, windupTimer) : 0f;

                                                                                                                    
        public int AttackSequence { get; private set; }
                                                                                                     
        public int PulseSequence { get; private set; }

        public override Team Team => Team.Enemy;
        public override Vector3 AimDirection => facing;
        public override float CorpseDamage => attackDamage;

        Vector3 facing = Vector3.back;
        bool windup;
        float windupTimer;
        float cooldown;

        protected override void TickNormal(float dt)
        {
            if (cooldown > 0f)
                cooldown -= dt * (IsBoss && bossPhase == 3 ? definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f : 1f);
            var p = GM.player;
            if (!GM.IsCombatActive || p == null || !p.IsAlive)
            {
                Stop();
                CancelWindup();
                return;
            }

                                                                                                                                        
                                                                                                                                       
            if (!IsBoss && WorldMap.Active && WorldMap.Active.PlayerHidden)
            {
                SearchForHiddenPlayer(dt);
                return;
            }

            if (definition && TickSpecial(dt, p)) return;

            if (windup)
            {
                                                                       
                Stop();
                windupTimer -= dt;
                if (windupTimer <= 0f)
                {
                    PerformAttack();
                    CancelWindup();
                    cooldown = attackInterval;
                }

                return;
            }

            Vector3 to = Query.Flat(p.Position - Position);
            if (WorldMap.Active && WorldMap.Active.DecoyActive) to = Query.Flat(WorldMap.Active.EnemyTarget - Position);
            float dist = to.magnitude;
            if (dist > 0.01f) facing = to / dist;
            float edge = dist - radius - p.Radius;

            if (edge <= triggerRange && cooldown <= 0f)
            {
                windup = true;
                windupTimer = windupTime;
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcWindup, Position);
                if (definition && definition.kind == EnemyKind.Shade) revealTimer = windupTime + 1f;
                SetLook(true);
                Stop();
                return;
            }

            Vector3 direction = WorldMap.Active
                ? WorldMap.Active.GetChaseDirection(Position, WorldMap.Active.EnemyTarget, FollowingIntoGravity)
                : facing;
            Vector3 v = edge > triggerRange ? direction * moveSpeed : Vector3.zero;
            v = KeepOffEdges(v, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        const float SearchTurnDegrees = 90f;

                                                                                                                     
        void SearchForHiddenPlayer(float dt)
        {
            if (windup) CancelWindup();
            charging = false;
            Vector3 spot = WorldMap.Active.EnemyTarget;
            if (Query.Flat(spot - Position).magnitude > radius + 1f)
            {
                Vector3 direction = WorldMap.Active.GetChaseDirection(Position, spot, FollowingIntoGravity);
                if (direction.sqrMagnitude > 0.0001f) facing = Query.Flat(direction).normalized;
                Vector3 v = KeepOffEdges(direction * moveSpeed, dt);
                rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
                return;
            }

            Stop();
            facing = Quaternion.Euler(0f, SearchTurnDegrees * dt, 0f) * facing;
        }

        void PerformAttack()
        {
            AttackSequence++;
            if (!definition || definition.kind is EnemyKind.Grunt or EnemyKind.Brute or EnemyKind.Skitter
                    or EnemyKind.Splitter or EnemyKind.Shade)
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSwing, Position);
            if (definition && PerformSpecialAttack()) return;
            Fx.Telegraph(Position, facing, radius + attackRange, attackArc, new Color(1f, 0.3f, 0.2f, 0.55f));

            var p = GM.player;
            var shield = p.HeldShield;
            if (shield != null && shield != this && InFist(shield))
            {
                                                                                                                     
                DealHit(shield, attackDamage, shield.Position - Position, 0f);
                return;
            }

            if (p.IsAlive && InFist(p))
                DealHit(p, attackDamage, p.Position - Position, attackKnockback);
        }

                     
                                                                                                                                     
                                                                                                                                          
                      
        bool DealHit(Combatant target, float damage, Vector3 direction, float knock)
        {
            var player = target as PlayerController;
            if (player && !PushesPlayer) knock = 0f;
            bool lethal = player && definition && definition.kind == EnemyKind.Shade && !player.Protected &&
                          player.State != CharacterState.Dashing;
            float beforeHp = target.Hp;
            var board = ElementStatus.For(target);
            float beforeShield = board ? board.Shield : 0f;
            var hit = DamageInfo.Attack(damage * OutgoingDamageFactor, this, direction, knock);
            hit.element = Affix;                                                                     
            bool landed = target.ReceiveDamage(hit);
            if (landed && Affix != ElementKind.None)
                ElementCombat.ApplyAffix(this, target, Affix,
                    Mathf.Max(0f, beforeHp - target.Hp) + Mathf.Max(0f, beforeShield - (board ? board.Shield : 0f)));
            if (landed && lethal && target.IsAlive) target.Kill(this);                                  
            return landed;
        }

        bool TickSpecial(float dt, PlayerController player)
        {
            revealTimer -= dt;
            if (definition.kind == EnemyKind.Shade)
            {
                RefreshBodyTint();
                if (spawnVisible > 0f)
                {
                    spawnVisible -= dt;
                    Stop();
                    return true;
                }
            }

            if (definition.kind == EnemyKind.Tyrant) return TickTyrant(dt, player);                                                 

            if (IsBoss)
            {
                if (leaping)
                {
                    TickLeap(dt);
                    return true;
                }

                if (definition.kind == EnemyKind.Hollow) return TickProphet(dt, player);                                      
                TickLandingZones(dt);
                TickSummonWarning(dt);
                TickSummonClock(dt);
                TickBossPhase();
                if (definition.kind == EnemyKind.Hollow)
                {
                    TickPlatform(dt);
                    TickVolleys(dt, player);
                }

                if (inhaleLeft > 0f)
                {
                    inhaleLeft -= dt;
                    if (inhaleLeft <= 0f)
                    {
                                                                    
                        bossAttack = PulseMove;
                        BeginWindup(Boss.inhaleFollowWindup, PulseReach, false);
                    }

                    Stop();
                    return true;
                }

                if (bossRecovery > 0f)
                {
                    bossRecovery -= dt;
                    Stop();
                    return true;
                }

                if (followUp >= 0 && !windup && !charging)
                {
                    int move = followUp;
                    followUp = -1;
                    StartBossMove(move, player);
                    return true;
                }

                if (royalCastPending && !windup && !charging)
                {
                    royalCastPending = false;
                    StartBossMove(RoyalShieldMove, player);
                    return true;
                }
            }

            if (laserTimer > 0f)
            {
                                                                                                           
                laserTimer -= dt;
                if (laserTimer <= 0f) FireLaser(player);
                return true;
            }

            if (charging)
            {
                float speed = IsBoss ? 18f * (bossPhase == 3 ? 1.25f : 1f) : definition.chargeSpeed;
                speed *= GM && GM.waves ? GM.waves.MovementSpeedScale : 1f;
                float step = speed * dt;
                bool wall = Query.WallBetween(Query.AtCastHeight(Position),
                    Query.AtCastHeight(Position + facing * (step + radius)), out _);
                bool hole = IsBoss && WorldMap.Active && WorldMap.Active.IsHole(Position + facing * (step + radius));
                if (wall || hole)
                {
                    charging = false;
                    Stop();
                    if (IsBoss)
                    {
                                                                                                          
                        chargesLeft = 0;
                        statusBoard?.SetShield(0f);
                        ReceiveDamage(new DamageInfo
                            { amount = maxHp * Boss.wallHitHpShare, source = DamageSource.Wall, kind = HitKind.Slam });
                    }
                    else
                    {
                        ReceiveDamage(new DamageInfo
                            { amount = GM.rules.wallDamage * GM.waves.HpMultiplier * 2, kind = HitKind.Slam });
                        cooldown = 1.5f;
                        Stun(1.5f);
                    }

                    return true;
                }

                rb.linearVelocity = facing * speed + Vector3.up * rb.linearVelocity.y;
                chargeLeft -= step;
                if (InFist(player))
                {
                                                                                                                                             
                    bool landed = DealHit(player, IsBoss ? attackDamage * Boss.chargeDamage : attackDamage, facing,
                        IsBoss ? Boss.chargePush : attackKnockback);
                    if (landed && !IsBoss && player.IsAlive)
                    {
                        player.Stun(definition.chargeStun);
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.PlayerStunned, player.Position);
                    }
                    charging = false;
                    if (IsBoss)
                    {
                        chargesLeft = 0;                                            
                        bossRecovery = 1.5f;
                    }
                }

                if (chargeLeft <= 0f)
                {
                    bool ranOut = charging;
                    charging = false;
                    cooldown = 1.5f;
                    if (IsBoss && ranOut && --chargesLeft > 0)
                    {
                                                                                           
                        FacePlayer(player);
                        bossAttack = ChargeMove;
                        BeginWindup(Boss.chargeReaim, ChargeLineLength(), false);
                    }
                    else if (IsBoss) bossRecovery = 1.5f;
                }

                return true;
            }

            Vector3 to = Query.Flat(player.Position - Position);
            float distance = to.magnitude;
            if (windup) return false;
            if (distance > 0.01f) facing = to / distance;
            switch (definition.kind)
            {
                case EnemyKind.Spitter:
                    MoveTowards(player.Position, distance < definition.rangedNear ? -1f : distance > definition.rangedFar ? 1f : 0f, dt);
                    if (cooldown <= 0f && !Query.WallBetween(Query.AtCastHeight(Position),
                            Query.AtCastHeight(player.Position), out _)) BeginWindup(windupTime, definition.rangedFar);
                    return true;
                case EnemyKind.Warden:
                    supportTimer -= dt;
                    if (supportTimer <= 0f)
                    {
                        bool shieldAdded = false;
                        foreach (var enemy in GM.waves.Alive)
                                                                                                                           
                            if (enemy && enemy != this && !enemy.IsBoss && Query.Flat(enemy.Position - Position).magnitude < 5f)
                            {
                                var board = ElementStatus.For(enemy);
                                if (board != null)
                                {
                                    float before = board.Shield;
                                    board.SetShield(Mathf.Max(board.Shield, enemy.maxHp * 0.3f));
                                    shieldAdded |= board.Shield > before;
                                }
                            }

                        if (shieldAdded)
                        {
                            AttackSequence++;                                                               
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShield, Position);
                        }

                        supportTimer = 4f;
                    }

                    HoldBehindAllies(player, distance, dt);
                    return true;
                case EnemyKind.Chanter:
                                                                                                            
                    supportTimer -= dt;
                    if (supportTimer <= 0f)
                    {
                        AttackSequence++;
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcChant, Position);
                        supportTimer = ChantInterval;
                    }

                    HoldBehindAllies(player, distance, dt);
                    return true;
                case EnemyKind.Spire:
                    if (cooldown <= 0f) BeginWindup(windupTime, definition.pulseRadius);
                    return true;
                case EnemyKind.Stomper:
                    if (distance <= definition.triggerDistance && cooldown <= 0f)
                    {
                        BeginWindup(windupTime, definition.pulseRadius);
                        return true;
                    }

                    break;
                case EnemyKind.Rammer:
                    if (distance <= definition.triggerDistance && cooldown <= 0f)
                    {
                        BeginWindup(windupTime, definition.chargeDistance);
                        return true;
                    }

                    break;
                case EnemyKind.Bloater:
                                                                                                                                     
                                                                                                       
                    if (distance <= radius + player.Radius + definition.reach)
                    {
                        rushing = false;
                        BeginWindup(windupTime, definition.pulseRadius);
                        return true;
                    }

                    if (!rushing && cooldown <= 0f && distance <= definition.triggerDistance)
                    {
                        rushing = true;
                        chargeLeft = definition.chargeDistance;
                        V2Audio.Instance?.PlayWorld(ArenaSoundCue.BloaterWarning, Position);
                        RefreshBodyTint();                                                                                                 
                    }

                    if (rushing)
                    {
                        float rush = definition.chargeSpeed * (GM && GM.waves ? GM.waves.MovementSpeedScale : 1f);
                        MoveAt(player.Position, rush, dt);
                        chargeLeft -= rush * dt;
                        if (chargeLeft <= 0f)
                        {
                            rushing = false;
                            cooldown = attackInterval;
                            RefreshBodyTint();                                                                 
                        }

                        return true;
                    }

                    MoveTowards(player.Position, 1f, dt);
                    return true;
                case EnemyKind.Hollow:
                    if (cooldown <= 0f) StartBossMove(ChooseBossMove(distance), player);
                    Stop();
                    return true;
                case EnemyKind.Tyrant:
                                                                                                                    
                    if (cooldown <= 0f)
                    {
                        StartBossMove(ChooseBossMove(distance), player);
                        return true;
                    }

                    break;
            }

            return false;
        }

                                              

                                                                                                                                
                                                                                                                    
        public static int ProphetMoveFor(int phase, bool close, float roll, BossTuning b)
        {
            if (close) return phase == 2 && roll >= b.phase2ClosePulseShare ? InhaleMove : PulseMove;
            if (phase <= 1) return roll < b.phase1DoubleShotShare ? DoubleShotMove : RingMove;
            if (phase == 2)
                return roll < b.phase2DoubleShotShare ? DoubleShotMove
                    : roll < b.phase2DoubleShotShare + b.phase2RingShare ? RingMove : InhaleMove;
            return roll < b.phase3DoubleShotShare ? DoubleShotMove
                : roll < b.phase3DoubleShotShare + b.phase3RingShare ? RingMove : ProphecyMove;
        }

                                                                                                                               
                                                                                                 
        public static int TyrantMoveFor(float distance, bool onScreen, float roll, BossTuning b)
        {
            if (distance > b.tyrantFarRange || !onScreen) return LeapMove;
            if (distance <= b.tyrantCloseRange)
                return roll < b.closeCleaveShare ? CleaveMove
                    : roll < b.closeCleaveShare + b.closeSlamShare ? SlamMove : ChargeMove;
            if (distance <= b.tyrantMidRange) return roll < b.midSlamShare ? SlamMove : ChargeMove;
            return roll < b.farChargeShare ? ChargeMove : LeapMove;
        }

        int ChooseBossMove(float distance)
        {
            float roll = UnityEngine.Random.value;
            int move;
            if (definition.kind == EnemyKind.Hollow)
            {
                bool close = distance <= (bossPhase == 3 ? Boss.prophetPhase3CloseRange : Boss.prophetCloseRange);
                move = ProphetMoveFor(bossPhase, close, roll, Boss);
                                                                                             
                if (openingMove && !close) move = DoubleShotMove;
                openingMove = false;
            }
            else move = TyrantMoveFor(distance, SkillWorldAPI.IsOnScreen?.Invoke(Position) ?? true, roll, Boss);

            if (forcedMove >= 0)
            {
                move = forcedMove;
                forcedMove = -1;
            }

            return move;
        }

        float PulseReach => bossPhase == 3 ? Boss.phase3PulseReach : Boss.pulseReach;

                                                                                                                                   
        public float PlatformGoal => bossPhase == 3 ? Boss.prophetPlatformRadii.z
            : bossPhase == 2 ? Boss.prophetPlatformRadii.y : Boss.prophetPlatformRadii.x;

        void FacePlayer(PlayerController player)
        {
            if (!player) return;
            Vector3 to = Query.Flat(player.Position - Position);
            if (to.sqrMagnitude > 1e-4f) facing = to.normalized;
        }

        void StartBossMove(int move, PlayerController player)
        {
            FacePlayer(player);
            bossAttack = move;
            var b = Boss;
            if (definition.kind == EnemyKind.Hollow)
            {
                switch (move)
                {
                    case PulseMove: BeginWindup(b.pulseWindup, PulseReach); break;
                    case InhaleMove: BeginWindup(b.inhaleWindup, 0f); break;
                    case DoubleShotMove: BeginWindup(b.shotWindup, 0f); break;
                    case RingMove: BeginWindup(b.ringWindup, 0f); break;
                    default: BeginWindup(b.prophecyWindup, 0f); break;
                }

                return;
            }

            switch (move)
            {
                case CleaveMove: BeginWindup(b.cleaveWindup, b.cleaveRange); break;
                case BackSlashMove: BeginWindup(b.backSlashWindup, b.cleaveRange); break;
                case SlamMove: BeginWindup(b.slamWindup, b.slamRange); break;
                case ChargeMove:
                    chargesLeft = bossPhase == 3 ? b.phase3Charges : bossPhase == 2 ? b.phase2Charges : 1;
                    BeginWindup(b.chargeWindup, ChargeLineLength());
                    break;
                case LeapMove:
                                                                                                    
                    leapTarget = LeapLanding(player ? player.Position : Position);
                    BeginWindup(b.leapWindup, b.leapRadius);
                    break;
                default: BeginWindup(b.royalShieldCast, 0f, false); break;                                             
            }
        }

                                                                                                                                                
        float ChargeLineLength()
        {
            float length = TyrantChargeDistance;
            if (Query.WallBetween(Query.AtCastHeight(Position), Query.AtCastHeight(Position + facing * length), out var hit))
                length = Query.Flat(hit.point - Position).magnitude;
            var world = WorldMap.Active;
            if (world)
                for (float d = .5f; d < length; d += .5f)
                    if (world.IsHole(Position + facing * d))
                    {
                        length = d;
                        break;
                    }

            return Mathf.Max(.5f, length);
        }

                                                                                                                                      
                                                                                                                           
        Vector3 LeapLanding(Vector3 aim)
        {
            aim = Query.Flat(aim);
            var world = WorldMap.Active;
            if (!world) return aim;
            Vector3 from = Query.Flat(Position);
            Vector3 delta = aim - from;
            Vector3 dir = delta.sqrMagnitude > 1e-4f ? delta.normalized : facing;
            for (float d = delta.magnitude; d >= 0f; d -= .5f)
            {
                Vector3 at = from + dir * d;
                if (world.IsWalkable(at, radius) && !world.IsHole(at + dir * radius)) return at;
            }

            return from;
        }

        void BeginLeap()
        {
            leaping = true;
            leapTimer = 0f;
            leapFrom = Query.Flat(Position);
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;                              
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = false;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCharge, Position);
        }

        void TickLeap(float dt)
        {
            leapTimer += dt;
            float t = Mathf.Clamp01(leapTimer / Mathf.Max(.1f, Boss.leapFlight));
            rb.MovePosition(Vector3.Lerp(leapFrom, leapTarget, t) + Vector3.up * (Boss.leapHeight * 4f * t * (1f - t)));
            if (t < 1f) return;
            leaping = false;
            rb.position = leapTarget;
            transform.position = leapTarget;
            rb.isKinematic = false;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = true;
            if (telegraph) telegraph.gameObject.SetActive(false);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, leapTarget);
            Fx.Ring(leapTarget, Boss.leapRadius, definition.colour);
            PlayerBlast(leapTarget, Boss.leapRadius, attackDamage * Boss.leapDamage, Boss.leapPush);
            bossRecovery = Boss.leapRecovery;
        }

                                                                                                                          
        bool PlayerBlast(Vector3 centre, float reach, float damage, float push)
        {
            var player = GM ? GM.player : null;
            if (!player || !player.IsAlive) return false;
            Vector3 to = Query.Flat(player.Position - centre);
            if (to.magnitude - player.Radius > reach) return false;
            return DealHit(player, damage, to.sqrMagnitude > 1e-4f ? to : facing, push);
        }

        void TickBossPhase()
        {
            float hpRatio = Hp / maxHp;
            int nextPhase = hpRatio < (definition.kind == EnemyKind.Hollow ? 0.3f : 0.33f) ? 3 :
                hpRatio < (definition.kind == EnemyKind.Hollow ? 0.6f : 0.66f) ? 2 : 1;
            if (nextPhase <= bossPhase) return;
            bossPhase = nextPhase;
            if (definition.kind != EnemyKind.Tyrant) return;
                                                                                                       
            var balance = GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;
            statusBoard?.SetShield(maxHp * (balance ? balance.tyrantPhaseShieldShare : .15f));
            royalShield = true;
            var immunities = GM.waves.TyrantImmunities;
            if (Affix == ElementKind.None && immunities.Length > 0)
                SetAffix(immunities[UnityEngine.Random.Range(0, immunities.Length)]);
                                                                                                     
            royalCastPending = true;
        }

        void TickSummonClock(float dt)
        {
            var b = Boss;
            float rate = bossPhase == 3 ? definition.kind == EnemyKind.Hollow ? b.prophetPhase3SummonRate : b.tyrantPhase3SummonRate : 1f;
            if (royalShield && statusBoard && statusBoard.Shield > 0f) rate *= b.royalShieldSummonRate;
            summonTimer -= dt * rate;
            if (summonTimer > 0f) return;
            if (definition.kind == EnemyKind.Hollow)
            {
                BeginProphetSummon();
                summonTimer = b.prophetSummonInterval;
            }
            else
            {
                GM.waves.SpawnTyrantSummons(bossPhase == 3 ? b.tyrantPhase3SummonCount : b.tyrantSummonCount, Position);
                summonTimer = b.tyrantSummonInterval;
            }
        }

                                                                                                                                        
                                             
        void BeginProphetSummon()
        {
            if (summonPoints.Count > 0 || !GM || !GM.waves) return;
            int living = 0;
            foreach (var enemy in GM.waves.Alive)
                if (enemy && enemy != this && enemy.IsAlive && enemy.IsTrialSummon) living++;
            int count = Mathf.Min(Boss.prophetSummonCount, Boss.prophetSummonCap - living);
            if (count <= 0) return;
            summonKind = bossPhase == 3 ? EnemyKind.Skitter : EnemyKind.Grunt;
            var data = GM.waves.DefinitionOf(summonKind);
            if (!data) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = Query.Flat(Position) + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * Boss.prophetSummonRingRadius;
                summonPoints.Add(at);
                var marker = GM.waves.BorrowMarker();
                if (!marker) continue;
                marker.Begin(at, data, false, ElementKind.None);
                summonMarkers.Add(marker);
            }

            summonWarning = Boss.prophetSummonWarning;
        }

        void TickSummonWarning(float dt)
        {
            if (summonPoints.Count == 0) return;
            summonWarning -= dt;
            if (summonWarning > 0f) return;
            foreach (var marker in summonMarkers)
                if (marker)
                    marker.gameObject.SetActive(false);
            summonMarkers.Clear();
            GM.waves.SpawnExtrasAt(summonKind, summonPoints, true);
            summonPoints.Clear();
        }

                                                                                                                     
        public void ClearBossMarks()
        {
            foreach (var marker in summonMarkers)
                if (marker)
                    marker.gameObject.SetActive(false);
            summonMarkers.Clear();
            summonPoints.Clear();
            landingZones.Clear();
            ClearNashFight();
        }

        void TickPlatform(float dt)
        {
            var world = WorldMap.Active;
            if (!world || !world.TrialActive || !world.trialPlatform) return;
            world.trialRadius = Mathf.MoveTowards(world.trialRadius, PlatformGoal, dt * Boss.platformShrinkSpeed);
            world.trialPlatform.localScale = new Vector3(world.trialRadius * 2, 0.3f, world.trialRadius * 2);
        }

        void TickVolleys(float dt, PlayerController player)
        {
            if (volleyTimer > 0f)
            {
                volleyTimer -= dt;
                if (volleyTimer <= 0f) FirePredictiveVolley(player);
            }

            if (ringsLeft > 0)
            {
                ringTimer -= dt;
                if (ringTimer <= 0f) FireRing(player);
            }
        }

                                                                                                                                  
                                                                                                                              
        void FirePredictiveVolley(PlayerController player)
        {
            if (!player || !player.IsAlive) return;
            var b = Boss;
            Vector3 from = Query.Flat(Position);
            Vector3 now = Query.Flat(player.Position);
            var body = player.GetComponent<Rigidbody>();
            Vector3 velocity = body ? Query.Flat(body.linearVelocity) : Vector3.zero;
            float flight = Query.Flat(now - from).magnitude / b.shotSpeed;
            flight = Query.Flat(now + velocity * flight - from).magnitude / b.shotSpeed;
            float damage = attackDamage * b.shotDamage;
            Shoot(now, damage, b.shotSpeed);
            Shoot(now + velocity * flight, damage, b.shotSpeed);
            if (bossPhase >= 2) Shoot(now - velocity * flight, damage, b.shotSpeed);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSpit, Position);
        }

                                                                                                                                        
                                                                         
        void FireRing(PlayerController player)
        {
            var b = Boss;
            int count = Mathf.Max(1, bossPhase == 3 ? b.phase3RingShots : b.ringShots);
            float spacing = 360f / count;
                                                                                                               
            float turn = bossPhase >= 2 ? spacing / Mathf.Max(1, bossPhase == 3 ? b.phase3Rings : b.phase2Rings) : 0f;
            if (ringIndex == 0)
            {
                Vector3 aim = player ? Query.Flat(player.Position - Position) : facing;
                if (aim.sqrMagnitude < 1e-4f) aim = facing;
                ringBaseAngle = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            }

                                                                                                                
            float baseAngle = ringBaseAngle + turn * ringIndex;
            float damage = attackDamage * b.ringDamage;
            for (int i = 0; i < count; i++)
            {
                float angle = (baseAngle + spacing * i) * Mathf.Deg2Rad;
                Shoot(Query.Flat(Position) + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 10f, damage, b.ringSpeed);
            }

            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSpit, Position);
            ringIndex++;
            ringsLeft--;
            ringTimer = b.ringInterval;
        }

        void Shoot(Vector3 point, float damage, float speed)
        {
            Vector3 direction = Query.Flat(point - Position);
            if (direction.sqrMagnitude < 1e-4f) direction = facing;
            direction.Normalize();
            GM.waves.LaunchBossBolt(this, Position + direction * (radius + .3f), direction, damage, 0f, speed);
        }

                                                                                                                                       
                                                                                      
        void PlaceLandingZones(PlayerController player)
        {
            var world = WorldMap.Active;
            if (!world || !world.TrialActive || !player || !player.IsAlive) return;
            var b = Boss;
            float zone = b.prophecyDiameter * .5f;
            var body = player.GetComponent<Rigidbody>();
            Vector3 velocity = body ? Query.Flat(body.linearVelocity) : Vector3.zero;
            AddLandingZone(Query.Flat(player.Position) + velocity * b.prophecyFill, zone, world);
            AddLandingZone(Vector3.Lerp(Query.Flat(Position), Query.Flat(player.Position), UnityEngine.Random.Range(b.prophecyBetweenMin, Mathf.Max(b.prophecyBetweenMin, b.prophecyBetweenMax))), zone,
                world);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, Position);
        }

        void AddLandingZone(Vector3 point, float zone, WorldMap world)
        {
            Vector3 centre = Query.Flat(world.trialPlatform.position);
                                                                                                                                 
            float limit = Mathf.Max(0f, Mathf.Min(world.trialRadius, PlatformGoal) - Boss.prophecyEdgeMargin);
            Vector3 off = point - centre;
            if (off.magnitude > limit) point = centre + off.normalized * limit;
            Vector3 fromBody = point - Query.Flat(Position);
            float clear = radius + zone + .2f;
            if (fromBody.magnitude < clear) point = Query.Flat(Position) + (fromBody.sqrMagnitude > 1e-4f ? fromBody.normalized : -facing) * clear;
            landingZones.Add(new LandingZone { centre = point, radius = zone, fill = Boss.prophecyFill });
        }

        void TickLandingZones(float dt)
        {
            for (int i = landingZones.Count - 1; i >= 0; i--)
            {
                var zone = landingZones[i];
                zone.age += dt;
                if (zone.age < zone.fill)
                {
                    landingZones[i] = zone;
                    continue;
                }

                landingZones.RemoveAt(i);
                                                                                        
                if (WorldMap.Active) WorldMap.Active.CreateBossHole(zone.centre, zone.radius * 2f, Boss.prophecyHoleLife, this);
            }
        }

                                                                                                                                              
        public void InhaleContact(Combatant body)
        {
            if (!Inhaling || inhaleHit || body is not PlayerController player || !player.IsAlive) return;
            inhaleHit = DealHit(player, attackDamage * Boss.inhaleContactDamage, player.Position - Position, Boss.inhaleContactPush);
        }

                                                                                                                                         
                                                 
        public void SummonFell()
        {
            if (!IsAlive) return;
            ReceiveDamage(new DamageInfo { amount = maxHp * Boss.summonFallHpShare, source = DamageSource.Wall, kind = HitKind.Slam });
        }

                                                                                                                                                          
        void HoldBehindAllies(PlayerController player, float distance, float dt)
        {
            Enemy nearestGroup = null;
            float nearestDistance = float.MaxValue;
            foreach (var ally in GM.waves.Alive)
            {
                if (!ally || ally == this || !ally.IsAlive || !ally.definition ||
                    EnemyRoles.IsSupport(ally.definition.kind)) continue;
                float d = Query.Flat(ally.Position - Position).sqrMagnitude;
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearestGroup = ally;
                }
            }

            Vector3 supportPoint = nearestGroup
                ? nearestGroup.Position + Query.Flat(nearestGroup.Position - player.Position).normalized * 5f
                : player.Position;
            MoveTowards(distance < 6f ? player.Position : supportPoint,
                distance < 6f ? -1f : Query.Flat(supportPoint - Position).magnitude > 1f ? 1f : 0f, dt);
        }

        void MoveTowards(Vector3 point, float sign, float dt) => MoveAt(point, moveSpeed * sign, dt);

                                                                                  
        void MoveAt(Vector3 point, float speed, float dt)
        {
            Vector3 direction = WorldMap.Active
                ? WorldMap.Active.GetChaseDirection(Position, point, FollowingIntoGravity)
                : Query.Flat(point - Position).normalized;
            Vector3 v = KeepOffEdges(direction * speed, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

                                                                                                                                            
        void BeginWindup(float time, float range, bool scaled = true)
        {
            if (scaled && IsBoss && bossPhase == 3)
                time = Mathf.Max(0.8f, time / (definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f));
            windup = true;
            specialWindup = true;
            windupTimer = time;
            if (definition.kind == EnemyKind.Bloater)
                V2Audio.Instance?.PlayWorld(ArenaSoundCue.BloaterWarning, Position);
            else
                V3WorldAudio.Instance?.Play(
                    definition.kind == EnemyKind.Rammer || definition.kind == EnemyKind.Tyrant && bossAttack == ChargeMove
                        ? ArenaWorldCue.NpcChargeWarning
                        : ArenaWorldCue.NpcWindup, Position);
            SetLook(true);
            Stop();
            if (telegraph)
            {
                bool line = definition.kind == EnemyKind.Rammer ||
                            definition.kind == EnemyKind.Tyrant && bossAttack == ChargeMove;
                TelegraphRange = range;
                                                                                                       
                Vector3 origin = definition.kind == EnemyKind.Tyrant && bossAttack == LeapMove ? leapTarget : Position;
                telegraph.position = origin + (line ? facing * range * .5f : Vector3.zero) + Vector3.up * .03f;
                telegraph.rotation = Quaternion.LookRotation(facing);
                telegraph.localScale =
                    line ? new Vector3(radius * 2f, 0.025f, range) : new Vector3(range * 2f, 0.025f, range * 2f);
                                                                                                                                        
                                                                                                                                            
                                 
                telegraph.gameObject.SetActive(range > 0f &&
                                               (definition.kind == EnemyKind.Spire || line || CleaveTelegraph || CircleTelegraph));
            }

            if (definition.kind == EnemyKind.Shade)
            {
                revealTimer = 1.6f;
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShadeSpawn, Position);
            }
        }

        bool PerformSpecialAttack()
        {
            switch (definition.kind)
            {
                case EnemyKind.Spitter:
                    GM.waves.LaunchSpit(this, Position, facing);
                    return true;
                case EnemyKind.Rammer:
                    charging = true;
                    chargeLeft = definition.chargeDistance;
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcChargeRush, Position);
                    return true;
                case EnemyKind.Bloater:
                    dyingExplosion = true;
                    Explode();
                    Kill();
                    dyingExplosion = false;
                    return true;
                case EnemyKind.Spire:
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcPulse, Position);
                    PlayerPulse(definition.pulseRadius, attackDamage, 0f);                                  
                    TryLockLaser(definition.pulseRadius);
                    return true;
                case EnemyKind.Stomper:
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcPulse, Position);
                    ConeSlam(definition.pulseRadius, definition.coneAngle, attackDamage, attackKnockback);
                    return true;
                case EnemyKind.Hollow:
                    var prophet = Boss;
                    switch (bossAttack)
                    {
                        case PulseMove:
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position);
                            Fx.Ring(Position, PulseReach, definition.colour);
                            PlayerBlast(Position, PulseReach, attackDamage * prophet.pulseDamage, prophet.pulsePush);
                            break;
                        case InhaleMove:
                            inhaleLeft = prophet.inhaleSeconds;
                            inhaleHit = false;
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, Position);
                            break;
                        case DoubleShotMove:
                            FirePredictiveVolley(GM.player);
                            if (bossPhase == 3) volleyTimer = prophet.shotSecondVolleyDelay;
                            break;
                        case RingMove:
                            ringIndex = 0;
                            ringsLeft = bossPhase == 3 ? prophet.phase3Rings : bossPhase == 2 ? prophet.phase2Rings : 1;
                            FireRing(GM.player);
                            break;
                        default:
                            PlaceLandingZones(GM.player);
                            break;
                    }

                    return true;
                case EnemyKind.Tyrant:
                    var tyrant = Boss;
                    switch (bossAttack)
                    {
                        case ChargeMove:
                            charging = true;
                            chargeLeft = TyrantChargeDistance;
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCharge, Position);
                            break;
                        case SlamMove:
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position);
                            Fx.Ring(Position, tyrant.slamRange, definition.colour);
                            PlayerBlast(Position, tyrant.slamRange, attackDamage * tyrant.slamDamage, tyrant.slamPush);
                            break;
                        case LeapMove:
                            BeginLeap();
                            break;
                        case RoyalShieldMove:
                            break;
                        default:
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCleave, Position);
                            float share = bossAttack == BackSlashMove ? tyrant.backSlashDamage : tyrant.cleaveDamage;
                            foreach (var target in Query.InSector(this, Position, facing, tyrant.cleaveRange, tyrant.cleaveArc))
                                if (target.Team != Team.Enemy)                         
                                    DealHit(target, attackDamage * share, target.Position - Position, tyrant.cleavePush);
                                                                                                                
                            if (bossAttack == CleaveMove && bossPhase >= 2 && OutsideCut(GM.player, tyrant)) followUp = BackSlashMove;
                            break;
                    }

                    return true;
            }

            return false;
        }

                                                                                                                                                 
        void TryLockLaser(float pulseRadius)
        {
            var player = GM.player;
            if (!player || !player.IsAlive) return;
            Vector3 to = Query.Flat(player.Position - Position);
            if (to.sqrMagnitude < 1e-4f || to.magnitude > pulseRadius + player.Radius) return;
            LaserDirection = to.normalized;
            float length = SpireLaserLength;
            if (Query.WallBetween(Query.AtCastHeight(Position), Query.AtCastHeight(Position + LaserDirection * length),
                    out var wallHit))
                length = Query.Flat(wallHit.point - Position).magnitude;
            LaserLength = length;
            facing = LaserDirection;
            laserTimer = SpireLaserDelay;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcLaserLock, Position);
        }

        void FireLaser(PlayerController player)
        {
            laserTimer = 0f;
            LaserSequence++;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcLaserFire, Position);
            if (!player || !player.IsAlive) return;
            Vector3 to = Query.Flat(player.Position - Position);
            float along = Vector3.Dot(to, LaserDirection);
            float across = (to - LaserDirection * along).magnitude;
            if (along >= -player.Radius && along <= LaserLength + player.Radius &&
                across <= SpireLaserHalfWidth + player.Radius)
                DealHit(player, attackDamage, LaserDirection, 0f);
        }

                                                                                                                         
        bool OutsideCut(PlayerController player, BossTuning b)
        {
            if (!player || !player.IsAlive) return false;
            Vector3 to = Query.Flat(player.Position - Position);
            float d = to.magnitude;
            return d - player.Radius <= b.cleaveRange && d > player.Radius && Vector3.Angle(facing, to) > b.cleaveArc * .5f;
        }

        void AreaAttack(float reach, float damage, float push)
        {
            Fx.Ring(Position, reach, definition.colour);
            foreach (var target in Query.Characters(Position, reach))
                if (target != this && target.IsAlive)
                    DealHit(target, damage, target.Position - Position, push);
        }

                                                                                                                                          
        void PlayerPulse(float reach, float damage, float push)
        {
            PulseSequence++;                                                                                                 
            var player = GM ? GM.player : null;
            if (player && player.IsAlive && Query.Flat(player.Position - Position).magnitude - player.Radius <= reach)
                DealHit(player, damage, player.Position - Position, push);
        }

                                                                                                                           
                                                                                                           
        void ConeSlam(float reach, float arcDeg, float damage, float push)
        {
            Fx.Sector(Position, facing, reach, arcDeg, definition.colour);
            foreach (var target in Query.InSector(this, Position, facing, reach, arcDeg))
                if (target.IsAlive) DealHit(target, damage, target.Position - Position, push);
        }

                                                                                                             
        void Explode()
        {
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcBloaterBlast, Position);
                                                                      
            AreaAttack(definition ? definition.pulseRadius : 3f, attackDamage, KnockbackRules.Active.Distance(KnockbackRules.AttackTier(EnemyKind.Bloater)));
            var waves = GM ? GM.waves : null;
            AddRadiationZone(Position, waves ? waves.radiationRadius : 3f, waves ? waves.radiationLife : 4f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcRadiation, Position);
        }

                                                                                                                                                                             
        bool InFist(Combatant c)
        {
            Vector3 to = Query.Flat(c.Position - Position);
            float d = to.magnitude;
            if (d - radius - c.Radius > attackRange) return false;
            return d <= radius + c.Radius * 0.5f || Vector3.Angle(facing, to) <= attackArc * 0.5f;
        }

                                                                                                                                             
                                                                                                                                         
                                                                                                                           
        public bool FollowsIntoGravity => definition && definition.speed >= gravityFollowSpeed;

                                                                                                                                           
                                                                                                                                   
        bool FollowingIntoGravity => FollowsIntoGravity && WorldMap.Active &&
                                     WorldMap.Active.GetPull(WorldMap.Active.EnemyTarget).sqrMagnitude > 0.01f;

                     
                                                                                                             
                                                                                                                                                                                          
                                                                                                                            
                      
        Vector3 KeepOffEdges(Vector3 v, float dt)
        {
            if (!WorldMap.Active) return v;
            if (WorldMap.Active.TrialActive) return WorldMap.Active.KeepOnTrialGround(Position, v, dt, radius);                           
            if (!IsBoss) v *= Quicksand.SpeedFactor(Position);
            return WorldMap.Active.IsWalkable(Position + v * dt, radius, FollowingIntoGravity) ? v : Vector3.zero;
        }

                                                                                                                                                 
                                                                                                                                          
        protected override Vector3 GuardSlide(Vector3 v, float dt)
        {
            if (!WorldMap.Active) return v;
            if (WorldMap.Active.TrialActive) return WorldMap.Active.KeepOnTrialGround(Position, v, dt, radius);
            return WorldMap.Active.IsWalkable(Position + v * dt, radius, FollowingIntoGravity) ? v : Vector3.zero;
        }

                                                                                                                                              
                                                                                                                   
        protected override bool SlipsOnIce => !charging && !rushing;

        void Stop()
        {
            if (!rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        void CancelWindup()
        {
            if (!windup) return;
            windup = false;
            specialWindup = false;
            SetLook(false);
            if (telegraph && !leaping) telegraph.gameObject.SetActive(false);                                                    
        }

        void SetLook(bool yellow)
        {
            if (!bodyRenderer) return;
            if (windupMaterial) bodyRenderer.sharedMaterial = yellow ? windupMaterial : normalMaterial;
            RefreshBodyTint();
        }

                                                                                                                                         
                                                                                                            
        public static readonly Color WindupBodyColour = new Color(1f, .85f, .25f);
        static readonly Color ShadeCloakColour = new Color(0.38f, 0.45f, 0.7f, 0.22f);

        protected override Color? BodyTint => !definition ? null
            : windup || rushing ? WindupBodyColour
            : definition.kind == EnemyKind.Shade && Cloaked ? ShadeCloakColour
            : definition.colour;

        protected override void OnStateChanged(CharacterState from, CharacterState to)
        {
            if (to != CharacterState.Normal)
            {
                CancelWindup();
                laserTimer = 0f;
            }
        }

        [Header("Corpses can enter black holes.")] public float corpseLifetime = 8f;
        public float corpseDamping = 3f;
        public bool CorpseActive { get; private set; }
        public bool CorpseCarried { get; private set; }
        public bool CorpseFeedConsumed { get; private set; }
        float corpseSeconds;
        Vector3 corpseVelocity;

        void BeginCorpse()
        {
            if (!GM || !pooled || IsBoss || DeathCause != DeathCause.HpDepleted)
            {
                if (pooled) gameObject.SetActive(false);
                else Destroy(gameObject);
                return;
            }

            CorpseActive = true;
            CorpseCarried = false;
            corpseSeconds = corpseLifetime;
            corpseVelocity = Vector3.ClampMagnitude(DeathVelocity, 12f);
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = false;
            gameObject.SetActive(true);
        }

        public bool TryClaimCorpse(int version)
        {
            if (version != SpawnVersion || !CorpseActive || CorpseCarried || CorpseFeedConsumed) return false;
            CorpseCarried = true;
            gameObject.SetActive(false);
            return true;
        }

        public void DropCorpse(int version, Vector3 at)
        {
            if (version != SpawnVersion || !CorpseActive || !CorpseCarried || CorpseFeedConsumed) return;
            CorpseCarried = false;
            transform.position = new Vector3(at.x, 0, at.z);
            rb.position = transform.position;
            corpseVelocity = Vector3.zero;
            gameObject.SetActive(true);
        }

                                                                                                                                              
        public void KnockCorpse(Vector3 dir, float distance)
        {
            if (!CorpseActive || CorpseCarried || CorpseFeedConsumed) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
                                                                                                           
            corpseVelocity += dir.normalized * (distance * corpseDamping);
        }

        public bool TryConsumeCorpseFeed(int version)
        {
            if (version != SpawnVersion || !CorpseActive || CorpseFeedConsumed) return false;
            CorpseFeedConsumed = true;
            DespawnCorpse(version);
            return true;
        }

        public void DespawnCorpse(int version)
        {
            if (version != SpawnVersion || !CorpseActive) return;
            CorpseActive = CorpseCarried = false;
            gameObject.SetActive(false);
        }

        public void TickCorpse(float dt, Vector3 pull)
        {
            if (!CorpseActive || CorpseCarried || dt <= 0) return;
            corpseSeconds -= dt;
            if (corpseSeconds <= 0)
            {
                DespawnCorpse(SpawnVersion);
                return;
            }

            corpseVelocity *= Mathf.Exp(-corpseDamping * dt);
            Vector3 next = Position + (corpseVelocity + pull) * dt;
            var world = WorldMap.Active;
            if (world && !world.TrialActive)
            {
                next = world.ClampPoint(next, radius);
                if (world.IsWall(next, radius * .5f)) next = Position;
            }

            transform.position = new Vector3(next.x, 0, next.z);
            rb.position = transform.position;
        }

        protected override void OnDied()
        {
            if (definition && (IsBoss || !SuppressRewards || BossSummon))                                                                   
            {
                if (definition.kind == EnemyKind.Bloater && !dyingExplosion)
                {
                    dyingExplosion = true;
                    explosionTimer = 0.4f;
                    V2Audio.Instance?.PlayWorld(ArenaSoundCue.BloaterWarning, Position);
                    return;
                }

                if (definition.kind == EnemyKind.Hollow) WorldMap.Active.CompleteTrial();
                if (definition.kind == EnemyKind.Tyrant) GM.NotifyFinalBossDefeated();
            }

                                                                                                                           
            if (definition && definition.splitsOnDeath && !Mini && !IsTrialSummon &&
                DeathCause == DeathCause.HpDepleted && GM && GM.waves)
                GM.waves.QueueSplit(this);
            Fx.Pop(Position + Vector3.up * radius, bodyRenderer ? bodyRenderer.sharedMaterial.color : Color.red,
                radius * 3f);
            BeginCorpse();
        }

        protected override void OnDamaged(float amount) => revealTimer = 2f;

        protected override void OnFallFinished()
        {
            if (pooled) gameObject.SetActive(false);
            else Destroy(gameObject);
        }
    }
}