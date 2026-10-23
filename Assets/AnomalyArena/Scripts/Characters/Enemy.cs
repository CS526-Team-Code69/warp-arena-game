using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public class Enemy : Combatant
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

        public bool Immovable => IsBoss || definition && definition.kind == EnemyKind.Spire;
        public bool IsTrialSummon { get; set; }
        public bool SuppressRewards { get; set; }
        public bool Cloaked => definition && definition.kind == EnemyKind.Shade && revealTimer <= 0f;

        float spawnVisible,
            revealTimer,
            supportTimer,
            summonTimer,
            stagger,
            bossStun,
            chargeLeft,
            inhaleLeft,
            bossRecovery,
            laserTimer;

        public bool Inhaling => IsBoss && definition.kind == EnemyKind.Hollow && inhaleLeft > 0f;
        bool charging, dyingExplosion;
        float explosionTimer;

        public bool BloaterWarningActive => definition && definition.kind == EnemyKind.Bloater &&
                                            (IsAlive && windup || dyingExplosion && explosionTimer > 0f);

        int bossPhase = 1, bossAttack;
        public int BossPhase => bossPhase;
        public int BossAttackIndex => bossAttack;
        public bool TelegraphActive => windup && telegraph && telegraph.gameObject.activeSelf;

        public bool WindupTinted => windup && specialWindup;
        bool specialWindup;

        public bool LineTelegraph => definition && (definition.kind == EnemyKind.Rammer ||
                                                    definition.kind == EnemyKind.Tyrant && bossAttack == 2);

        public bool CleaveTelegraph => definition && definition.kind == EnemyKind.Tyrant && bossAttack == 0;
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
            attackKnockback =
                EnemyRoles.KnocksBackPlayer(data.kind) ? data.knockback : 0f;                                      
            knockbackScale = data.knockbackScale * (elite ? 0.5f : 1f);
            large = data.kind == EnemyKind.Stomper || IsBoss;                                                        
            ResetForSpawn();
            AttackSequence = 0;
            windup = false;
            charging = false;
            dyingExplosion = false;
            SuppressRewards = false;
            cooldown = 0.5f;
            spawnVisible = data.kind == EnemyKind.Shade ? 2f : 0f;
            revealTimer = spawnVisible;
            supportTimer = 0f;
            summonTimer = data.kind == EnemyKind.Hollow ? 10f : 15f;
            stagger = 0f;
            bossStun = 0f;
            bossPhase = 1;
            inhaleLeft = bossRecovery = 0f;
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
                statusBoard.OnControlThreshold -= AddStagger;
                if (IsBoss) statusBoard.OnControlThreshold += AddStagger;
                if (data.kind == EnemyKind.Hollow)
                {

                    statusBoard.SetImmunity(ElementKind.Frost, 1f);
                    statusBoard.SetImmunity(ElementKind.Lightning, 1f);
                }
            }

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

        void AddStagger(float amount)
        {
            stagger += amount;
            float limit = definition.kind == EnemyKind.Hollow ? 40f : 60f;
            if (stagger < limit) return;
            stagger = 0f;
            bossStun = definition.kind == EnemyKind.Hollow ? 3f : 2.5f;
            charging = false;
            inhaleLeft = 0f;
            CancelWindup();
        }

        public override bool ReceiveDamage(DamageInfo info)
        {
            if (IsBoss)
            {
                if (info.knockDistance > 0f) AddStagger(info.knockDistance);
                if (bossStun > 0f) info.amount *= 1.5f;
                info.knockDistance = 0f;
            }
            else if (Immovable) info.knockDistance = 0f;

            return base.ReceiveDamage(info);
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
                ? WorldMap.Active.GetChaseDirection(Position, WorldMap.Active.EnemyTarget, moveSpeed)
                : facing;
            Vector3 v = edge > triggerRange ? direction * moveSpeed : Vector3.zero;
            v = KeepOffEdges(v, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
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

            if (IsBoss)
            {
                summonTimer -= dt * (bossPhase == 3 ? definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f : 1f);
                if (summonTimer <= 0f)
                {
                    int count = definition.kind == EnemyKind.Hollow ? 4 : bossPhase == 3 ? 9 : 6;
                    if (definition.kind == EnemyKind.Hollow)
                        GM.waves.SpawnExtras(bossPhase == 3 ? EnemyKind.Skitter : EnemyKind.Grunt, count, Position,
                            true);
                    else GM.waves.SpawnTyrantSummons(count, Position);
                    summonTimer = definition.kind == EnemyKind.Hollow ? 10f : 15f;
                }

                float hpRatio = Hp / maxHp;
                int nextPhase = hpRatio < (definition.kind == EnemyKind.Hollow ? 0.3f : 0.33f) ? 3 :
                    hpRatio < (definition.kind == EnemyKind.Hollow ? 0.6f : 0.66f) ? 2 : 1;
                if (nextPhase > bossPhase)
                {
                    bossPhase = nextPhase;
                    if (definition.kind == EnemyKind.Tyrant)
                    {
                        var balance = GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;
                        statusBoard?.SetShield(balance ? maxHp * balance.tyrantPhaseShieldShare : 400f);
                        var immunities = GM.waves.TyrantImmunities;
                        if (Affix == ElementKind.None && immunities.Length > 0)
                            SetAffix(immunities[UnityEngine.Random.Range(0, immunities.Length)]);
                    }
                }

                if (definition.kind == EnemyKind.Hollow && WorldMap.Active)
                {
                    float goal = bossPhase == 3 ? 7f : bossPhase == 2 ? 9f : 12f;
                    WorldMap.Active.trialRadius = Mathf.MoveTowards(WorldMap.Active.trialRadius, goal, dt * 0.3f);
                    var platform = WorldMap.Active.trialPlatform;
                    platform.localScale = new Vector3(WorldMap.Active.trialRadius * 2, 0.3f,
                        WorldMap.Active.trialRadius * 2);
                }

                if (bossStun > 0f)
                {
                    bossStun -= dt;
                    Stop();
                    return true;
                }

                if (inhaleLeft > 0f)
                {
                    inhaleLeft -= dt;
                    if (inhaleLeft <= 0f) bossRecovery = 1.5f;
                    Stop();
                    return true;
                }

                if (bossRecovery > 0f)
                {
                    bossRecovery -= dt;
                    Stop();
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
                        bossStun = 2.5f;
                        statusBoard?.SetShield(0f);
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

                    bool landed = DealHit(player, IsBoss ? 20 : attackDamage, facing, IsBoss ? 8 : definition.knockback);
                    if (landed && !IsBoss && player.IsAlive)
                    {
                        player.Stun(definition.chargeStun);
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.PlayerStunned, player.Position);
                    }
                    charging = false;
                    if (IsBoss) bossRecovery = 1.5f;
                }

                if (chargeLeft <= 0f)
                {
                    charging = false;
                    cooldown = 1.5f;
                    if (IsBoss) bossRecovery = 1.5f;
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
                            if (enemy && enemy != this && Query.Flat(enemy.Position - Position).magnitude < 5f)
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
                    if (distance <= radius + player.Radius + 0.5f)
                    {
                        BeginWindup(windupTime, definition.pulseRadius);
                        return true;
                    }

                    MoveTowards(player.Position, 1f, dt);
                    return true;
                case EnemyKind.Hollow:
                    if (cooldown <= 0f)
                    {
                        bossAttack = bossPhase >= 2 && UnityEngine.Random.value < 0.5f ? 1 : 0;
                        BeginWindup(0.8f, bossAttack == 1 ? 10f : 6f);
                    }

                    Stop();
                    return true;
                case EnemyKind.Tyrant:
                    if (cooldown <= 0f && distance < 20f)
                    {
                        bossAttack = UnityEngine.Random.Range(0, 3);
                        BeginWindup(bossAttack == 0 ? 0.8f : 1f, bossAttack == 1 ? 8f : 5f);
                        return true;
                    }

                    break;
            }

            return false;
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

        void MoveTowards(Vector3 point, float sign, float dt)
        {
            Vector3 direction = WorldMap.Active
                ? WorldMap.Active.GetChaseDirection(Position, point, moveSpeed)
                : Query.Flat(point - Position).normalized;
            Vector3 v = KeepOffEdges(direction * moveSpeed * sign, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        void BeginWindup(float time, float range)
        {
            if (IsBoss && bossPhase == 3)
                time = Mathf.Max(0.8f, time / (definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f));
            windup = true;
            specialWindup = true;
            windupTimer = time;
            if (definition.kind == EnemyKind.Bloater)
                V2Audio.Instance?.PlayWorld(ArenaSoundCue.BloaterWarning, Position);
            else
                V3WorldAudio.Instance?.Play(
                    definition.kind == EnemyKind.Rammer || definition.kind == EnemyKind.Tyrant && bossAttack == 2
                        ? ArenaWorldCue.NpcChargeWarning
                        : ArenaWorldCue.NpcWindup, Position);
            SetLook(true);
            Stop();
            if (telegraph)
            {
                bool line = definition.kind == EnemyKind.Rammer ||
                            definition.kind == EnemyKind.Tyrant && bossAttack == 2;
                TelegraphRange = range;
                telegraph.position = Position + (line ? facing * range * .5f : Vector3.zero) + Vector3.up * .03f;
                telegraph.rotation = Quaternion.LookRotation(facing);
                telegraph.localScale =
                    line ? new Vector3(radius * 2f, 0.025f, range) : new Vector3(range * 2f, 0.025f, range * 2f);

                telegraph.gameObject.SetActive(definition.kind == EnemyKind.Spire || line || CleaveTelegraph);
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
                    PlayerPulse(definition.pulseRadius, attackDamage, 5f);
                    TryLockLaser(definition.pulseRadius);
                    return true;
                case EnemyKind.Stomper:
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcPulse, Position);
                    ConeSlam(definition.pulseRadius, definition.coneAngle, attackDamage, definition.knockback);
                    return true;
                case EnemyKind.Hollow:
                    if (bossAttack == 0)
                    {
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position);
                        AreaAttack(6f, 5f, 8f);
                        bossRecovery = 1.5f;
                    }
                    else
                    {
                        inhaleLeft = 2f;
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, Position);
                    }

                    return true;
                case EnemyKind.Tyrant:
                    if (bossAttack == 2)
                    {
                        charging = true;
                        chargeLeft = 20f;
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCharge, Position);
                    }
                    else if (bossAttack == 1)
                    {
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position);
                        AreaAttack(8f, 10f, 10f);
                    }
                    else
                    {
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCleave, Position);
                        foreach (var target in Query.InSector(this, Position, facing, 5f, 150f))
                            DealHit(target, 15f, target.Position - Position, 8f);
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
            AreaAttack(definition ? definition.pulseRadius : 3f, attackDamage, 5f);
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

        Vector3 KeepOffEdges(Vector3 v, float dt)
        {
            if (!WorldMap.Active || WorldMap.Active.TrialActive) return v;
            bool allowGravity = definition && definition.speed >= gravityFollowSpeed &&
                                WorldMap.Active.GetPull(GM.player.Position).sqrMagnitude > 0.01f;
            return WorldMap.Active.IsWalkable(Position + v * dt, radius, allowGravity) ? v : Vector3.zero;
        }

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
            if (telegraph) telegraph.gameObject.SetActive(false);
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
            : windup ? WindupBodyColour
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
            if (definition && (IsBoss || !SuppressRewards))
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