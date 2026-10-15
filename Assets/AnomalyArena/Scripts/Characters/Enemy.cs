using UnityEngine;

namespace AnomalyArena
{
    public class Enemy : Combatant
    {
        [Header("Attack")] public float attackDamage = 5f;

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

        [Header("v2 content")]
        public EnemyDefinition definition;
        public bool pooled;
        public Transform telegraph;
        public ElementKind Affix { get; private set; }
        public bool Elite { get; private set; }
        public int XpValue => definition ? definition.xp * (Elite ? 5 : 1) + (Affix == ElementKind.None ? 0 : 1) : IsLarge ? 4 : 1;
        public bool IsBoss => definition && (definition.kind == EnemyKind.Hollow || definition.kind == EnemyKind.Tyrant);
        public bool IsBossOrElite => IsBoss || Elite;
        public bool Immovable => IsBoss || definition && definition.kind == EnemyKind.Spire;
        public bool IsTrialSummon { get; set; }
        public bool SuppressRewards { get; set; }
        public bool Cloaked => definition && definition.kind == EnemyKind.Shade && revealTimer <= 0f;
        float spawnVisible, revealTimer, supportTimer, summonTimer, stagger, bossStun, chargeLeft, inhaleLeft, bossRecovery;
        public bool Inhaling => IsBoss && definition.kind == EnemyKind.Hollow && inhaleLeft > 0f;
        bool charging, dyingExplosion;
        float explosionTimer;
        public bool BloaterWarningActive => definition && definition.kind == EnemyKind.Bloater && (IsAlive && windup || dyingExplosion && explosionTimer > 0f);
        int bossPhase = 1, bossAttack;
        public int BossPhase => bossPhase;
        public int BossAttackIndex => bossAttack;
        public bool TelegraphActive => windup && telegraph && telegraph.gameObject.activeSelf;
        public bool LineTelegraph => definition && (definition.kind==EnemyKind.Rammer || definition.kind==EnemyKind.Tyrant&&bossAttack==2);
        public bool CleaveTelegraph => definition && definition.kind==EnemyKind.Tyrant&&bossAttack==0;
        public float TelegraphRange { get; private set; }
        public Vector3 TelegraphFacing => facing;
        ElementStatus statusBoard;
        float farTime;
        Vector3 savedBodyScale;

        public void TickDistanceRecycle(float dt, Vector3 player)
        {
            farTime = Query.Flat(Position - player).sqrMagnitude > 1600f ? farTime + dt : 0f;
            if (farTime < 5f || IsBoss) return;
            farTime = 0f;
            if (!GM.waves.TryFindV2SpawnPoint(definition, out Vector3 at)) return;
            rb.position = at;
            transform.position = at;
        }

        public void Configure(EnemyDefinition data, float hpMultiplier, bool elite, ElementKind affix = ElementKind.None)
        {
            CorpseActive=CorpseCarried=CorpseFeedConsumed=false;corpseSeconds=0;corpseVelocity=Vector3.zero;
            var capsule=GetComponent<CapsuleCollider>();if(capsule)capsule.enabled=true;
            definition = data;
            Elite = elite;
            Affix = data.canAffix ? affix : ElementKind.None;
            bool fodder = !elite && (data.kind is EnemyKind.Grunt or EnemyKind.Skitter);
            maxHp = fodder ? data.hp : Mathf.Round(data.hp * hpMultiplier * (elite ? 3f : 1f));
            var balance = GM && GM.useV2 && GM.waves && GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;
            if (balance) maxHp = balance.MaxHp(data, GM.waves.WaveIndex + 1, elite, Affix);
            moveSpeed = data.speed * (GM&&GM.useV2&&GM.waves?GM.waves.EnemySpeedFactor:1f);
            radius = data.radius;
            attackDamage = data.damage * (elite ? 1.5f : 1f) * (GM&&GM.useV2&&GM.waves?GM.waves.EnemyDamageFactor:1f);
            windupTime = data.windup;
            attackInterval = data.interval;
            attackRange = data.reach;
            attackKnockback = data.knockback;
            knockbackScale = data.knockbackScale * (elite ? 0.5f : 1f);
            large = data.kind == EnemyKind.Brute || data.kind == EnemyKind.Stomper || IsBoss;
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
            farTime = 0f;
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
            }
            if (bodyRenderer)
            {
                if (savedBodyScale == Vector3.zero) savedBodyScale = bodyRenderer.transform.localScale;
                bodyRenderer.transform.localScale = savedBodyScale;
                bodyRenderer.enabled = true;
                Art.Tint(bodyRenderer, data.colour);
            }
            if (telegraph) telegraph.gameObject.SetActive(false);
            if (rb) { rb.isKinematic = data.kind == EnemyKind.Spire; rb.constraints = RigidbodyConstraints.FreezeRotation; }
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
            if (ActionsBlocked) { CancelWindup(); charging = false; }
            base.FixedUpdate();
        }

        public void SetAffix(ElementKind element) { Affix = element; if (statusBoard) statusBoard.CarriesVoid = element == ElementKind.Void; }

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
        public float WindupSecondsRemaining => windup ? Mathf.Max(0f, windupTimer) : 0f;
        public int AttackSequence { get; private set; }
        public override Team Team => Team.Enemy;
        public override Vector3 AimDirection => facing;
        public override float CorpseDamage => attackDamage;
        protected override Texture2D ArtTexture => Art.Set ? (IsLarge ? Art.Set.enemyLarge : Art.Set.enemySmall) : null;
        protected override float ArtHeight => IsLarge ? Art.Set.enemyLargeHeight : Art.Set.enemySmallHeight;

        protected override Texture2D ArtWindupTexture =>
            Art.Set ? (IsLarge ? Art.Set.enemyLargeWindup : Art.Set.enemySmallWindup) : null;

        Vector3 facing = Vector3.back;
        bool windup;
        float windupTimer;
        float cooldown;

        protected override void TickNormal(float dt)
        {
            if (cooldown > 0f) cooldown -= dt * (IsBoss && bossPhase == 3 ? definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f : 1f);
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

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 30f);
                if (artWindupSprite)
                    Art.Tint(artWindupSprite, Color.Lerp(Color.white, new Color(1.35f, 1.3f, 1.1f), pulse));
                else if (artSprite) Art.Tint(artSprite, Color.Lerp(Color.white, new Color(1f, 0.85f, 0.2f), pulse));
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
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcWindup,Position);
                if (definition && definition.kind == EnemyKind.Shade) revealTimer = windupTime + 1f;
                SetLook(true);
                Stop();
                return;
            }

            Vector3 direction = WorldMap.Active ? WorldMap.Active.GetChaseDirection(Position, WorldMap.Active.EnemyTarget, moveSpeed) : facing;
            Vector3 v = edge > triggerRange ? direction * moveSpeed : Vector3.zero;
            v = KeepOffEdges(v, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        void PerformAttack()
        {
            AttackSequence++;
            if (!definition || definition.kind is EnemyKind.Grunt or EnemyKind.Brute or EnemyKind.Skitter or EnemyKind.Splitter or EnemyKind.Shade)
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSwing, Position);
            if (definition && PerformSpecialAttack()) return;
            var arc = Art.On ? Art.Set.enemyPunchArc : null;
            if (arc)
            {
                float reach = radius + attackRange;
                Fx.SpriteFlat(arc, Query.AtCastHeight(Position) + facing * (reach * 0.7f), facing, reach * 1.4f, 0.2f);
            }
            else
            {
                Fx.Sector(Position, facing, radius + attackRange, attackArc, new Color(1f, 0.3f, 0.2f, 0.55f));
            }

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

        void DealHit(Combatant target, float damage, Vector3 direction, float knock)
        {
            float beforeHp = target.Hp;
            var board = ElementStatus.For(target);
            float beforeShield = board ? board.Shield : 0f;
            var hit = DamageInfo.Attack(damage, this, direction, knock);
            hit.element = Affix;                                                                     
            if (target.ReceiveDamage(hit) && Affix != ElementKind.None)
                ElementCombat.ApplyAffix(this, target, Affix, Mathf.Max(0f, beforeHp - target.Hp) + Mathf.Max(0f, beforeShield - (board ? board.Shield : 0f)));
        }

        bool TickSpecial(float dt, PlayerController player)
        {
            revealTimer -= dt;
            if (definition.kind == EnemyKind.Shade)
            {
                if (bodyRenderer) Art.Tint(bodyRenderer, Cloaked ? new Color(0.38f, 0.45f, 0.7f, 0.22f) : definition.colour);
                if (spawnVisible > 0f) { spawnVisible -= dt; Stop(); return true; }
            }
            if (IsBoss)
            {
                summonTimer -= dt * (bossPhase == 3 ? definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f : 1f);
                if (summonTimer <= 0f)
                {
                    int count = definition.kind == EnemyKind.Hollow ? 4 : bossPhase == 3 ? 9 : 6;
                    if (definition.kind == EnemyKind.Hollow) GM.waves.SpawnExtras(bossPhase == 3 ? EnemyKind.Skitter : EnemyKind.Grunt, count, Position, true);
                    else GM.waves.SpawnTyrantSummons(count, Position);
                    summonTimer = definition.kind == EnemyKind.Hollow ? 10f : 15f;
                }
                float hpRatio = Hp / maxHp;
                int nextPhase = hpRatio < (definition.kind == EnemyKind.Hollow ? 0.3f : 0.33f) ? 3 : hpRatio < (definition.kind == EnemyKind.Hollow ? 0.6f : 0.66f) ? 2 : 1;
                if (nextPhase > bossPhase)
                {
                    bossPhase = nextPhase;
                    if (definition.kind == EnemyKind.Tyrant)
                    {
                        var balance = GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;
                        statusBoard?.SetShield(balance ? maxHp * balance.tyrantPhaseShieldShare : 400f);
                        var immunities = GM.waves.TyrantImmunities;
                        if (Affix == ElementKind.None && immunities.Length > 0) SetAffix(immunities[UnityEngine.Random.Range(0, immunities.Length)]);
                    }
                }
                if (definition.kind == EnemyKind.Hollow && WorldMap.Active)
                {
                    float goal = bossPhase == 3 ? 7f : bossPhase == 2 ? 9f : 12f;
                    WorldMap.Active.trialRadius = Mathf.MoveTowards(WorldMap.Active.trialRadius, goal, dt * 0.3f);
                    var platform = WorldMap.Active.trialPlatform;
                    platform.localScale = new Vector3(WorldMap.Active.trialRadius * 2, 0.3f, WorldMap.Active.trialRadius * 2);
                }
                if (bossStun > 0f) { bossStun -= dt; Stop(); return true; }
                if (inhaleLeft > 0f) { inhaleLeft -= dt; if (inhaleLeft <= 0f) bossRecovery = 1.5f; Stop(); return true; }
                if (bossRecovery > 0f) { bossRecovery -= dt; Stop(); return true; }
            }
            if (charging)
            {
                float speed = IsBoss ? 18f * (bossPhase == 3 ? 1.25f : 1f) : 16f;
                speed *= GM && GM.useV2 && GM.waves ? GM.waves.MovementSpeedScale : 1f;
                float step = speed * dt;
                bool wall = Query.WallBetween(Query.AtCastHeight(Position), Query.AtCastHeight(Position + facing * (step + radius)), out _);
                bool hole = IsBoss && WorldMap.Active && WorldMap.Active.IsHole(Position + facing * (step + radius));
                if (wall || hole)
                {
                    charging = false;
                    Stop();
                    if (IsBoss) { bossStun = 2.5f; statusBoard?.SetShield(0f); }
                    else { ReceiveDamage(new DamageInfo { amount = GM.rules.wallDamage * GM.waves.HpMultiplier * 2, kind = HitKind.Slam }); cooldown = 1.5f; Stun(1.5f); }
                    return true;
                }
                rb.linearVelocity = facing * speed + Vector3.up * rb.linearVelocity.y;
                chargeLeft -= step;
                if (InFist(player)) { DealHit(player, IsBoss ? 20 : 12, facing, 8); charging = false; if (IsBoss) bossRecovery = 1.5f; }
                if (chargeLeft <= 0f) { charging = false; cooldown = 1.5f; if (IsBoss) bossRecovery = 1.5f; }
                return true;
            }
            Vector3 to = Query.Flat(player.Position - Position);
            float distance = to.magnitude;
            if (windup) return false;
            if (distance > 0.01f) facing = to / distance;
            switch (definition.kind)
            {
                case EnemyKind.Spitter:
                    MoveTowards(player.Position, distance < 8f ? -1f : distance > 10f ? 1f : 0f, dt);
                    if (cooldown <= 0f && !Query.WallBetween(Query.AtCastHeight(Position), Query.AtCastHeight(player.Position), out _)) BeginWindup(0.8f, 10f);
                    return true;
                case EnemyKind.Warden:
                    supportTimer -= dt;
                    if (supportTimer <= 0f)
                    {
                        bool shieldAdded = false;
                        foreach (var enemy in GM.waves.Alive)
                            if (enemy && enemy != this && Query.Flat(enemy.Position - Position).magnitude < 5f)
                            { var board = ElementStatus.For(enemy); if (board != null) { float before=board.Shield; board.SetShield(Mathf.Max(board.Shield, enemy.maxHp * 0.3f)); shieldAdded |= board.Shield>before; } }
                        if (shieldAdded)
                        {
                            AttackSequence++;                                                               
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShield, Position);
                        }
                        supportTimer = 4f;
                    }
                    Enemy nearestGroup = null;
                    float nearestDistance = float.MaxValue;
                    foreach (var ally in GM.waves.Alive)
                    {
                        if (!ally || ally == this || !ally.IsAlive || ally.definition.kind == EnemyKind.Warden) continue;
                        float d = Query.Flat(ally.Position - Position).sqrMagnitude;
                        if (d < nearestDistance) { nearestDistance = d; nearestGroup = ally; }
                    }
                    Vector3 supportPoint = nearestGroup ? nearestGroup.Position + Query.Flat(nearestGroup.Position - player.Position).normalized * 5f : player.Position;
                    MoveTowards(distance < 6f ? player.Position : supportPoint, distance < 6f ? -1f : Query.Flat(supportPoint - Position).magnitude > 1f ? 1f : 0f, dt);
                    return true;
                case EnemyKind.Spire:
                    if (cooldown <= 0f) BeginWindup(0.8f, 6f);
                    return true;
                case EnemyKind.Stomper:
                    if (distance <= 4f && cooldown <= 0f) { BeginWindup(1f, 5f); return true; }
                    break;
                case EnemyKind.Rammer:
                    if (distance <= 16f && cooldown <= 0f) { BeginWindup(0.8f, 12f); return true; }
                    break;
                case EnemyKind.Bloater:
                    if (distance <= radius + player.Radius + 0.5f) { BeginWindup(0.4f, 3f); return true; }
                    MoveTowards(player.Position, 1f, dt);
                    return true;
                case EnemyKind.Hollow:
                    if (cooldown <= 0f) { bossAttack = bossPhase >= 2 && UnityEngine.Random.value < 0.5f ? 1 : 0; BeginWindup(0.8f, bossAttack == 1 ? 10f : 6f); }
                    Stop();
                    return true;
                case EnemyKind.Tyrant:
                    if (cooldown <= 0f && distance < 20f) { bossAttack = UnityEngine.Random.Range(0, 3); BeginWindup(bossAttack == 0 ? 0.8f : 1f, bossAttack == 1 ? 8f : 5f); return true; }
                    break;
            }
            return false;
        }

        void MoveTowards(Vector3 point, float sign, float dt)
        {
            Vector3 direction = WorldMap.Active ? WorldMap.Active.GetChaseDirection(Position, point, moveSpeed) : Query.Flat(point - Position).normalized;
            Vector3 v = KeepOffEdges(direction * moveSpeed * sign, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        void BeginWindup(float time, float range)
        {
            if (IsBoss && bossPhase == 3) time = Mathf.Max(0.8f, time / (definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f));
            windup = true;
            windupTimer = time;
            if (definition.kind == EnemyKind.Bloater) V2Audio.Instance?.PlayWorld(ArenaSoundCue.BloaterWarning, Position);
            else V3WorldAudio.Instance?.Play(definition.kind==EnemyKind.Rammer || definition.kind==EnemyKind.Tyrant&&bossAttack==2 ? ArenaWorldCue.NpcChargeWarning : ArenaWorldCue.NpcWindup,Position);
            SetLook(true);
            Stop();
            if (telegraph)
            {
                bool line = definition.kind == EnemyKind.Rammer || definition.kind == EnemyKind.Tyrant && bossAttack == 2;
                TelegraphRange=range;
                telegraph.position=Position+(line?facing*range*.5f:Vector3.zero)+Vector3.up*.03f;
                telegraph.rotation = Quaternion.LookRotation(facing);
                telegraph.localScale = line ? new Vector3(radius * 2f, 0.025f, range) : new Vector3(range * 2f, 0.025f, range * 2f);
                telegraph.gameObject.SetActive(true);
            }
            if (definition.kind == EnemyKind.Shade) { revealTimer = 1.6f; V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShadeSpawn,Position); }
        }

        bool PerformSpecialAttack()
        {
            switch (definition.kind)
            {
                case EnemyKind.Spitter: GM.waves.LaunchSpit(this, Position, facing); return true;
                case EnemyKind.Rammer: charging = true; chargeLeft = 12f; V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcChargeRush, Position); return true;
                case EnemyKind.Bloater: dyingExplosion = true; Explode(); Kill(); dyingExplosion = false; return true;
                case EnemyKind.Spire: V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcPulse, Position); AreaAttack(6f, 8f, 5f); return true;
                case EnemyKind.Stomper: V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcPulse, Position); AreaAttack(5f, 6f, 8f); return true;
                case EnemyKind.Hollow:
                    if (bossAttack == 0) { V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position); AreaAttack(6f, 5f, 8f); bossRecovery = 1.5f; }
                    else { inhaleLeft = 2f; V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, Position); }
                    return true;
                case EnemyKind.Tyrant:
                    if (bossAttack == 2) { charging = true; chargeLeft = 20f; V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCharge, Position); }
                    else if (bossAttack == 1) { V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position); AreaAttack(8f, 10f, 10f); }
                    else { V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCleave, Position); foreach (var target in Query.InSector(this, Position, facing, 5f, 150f)) DealHit(target, 15f, target.Position - Position, 8f); }
                    return true;
            }
            return false;
        }

        void AreaAttack(float reach, float damage, float push)
        {
            Fx.Ring(Position, reach, definition.colour);
            foreach (var target in Query.Characters(Position, reach))
                if (target != this && target.IsAlive) DealHit(target, damage, target.Position - Position, push);
        }

        void Explode() { V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcBloaterBlast, Position); AreaAttack(3f, attackDamage, 5f); }

        bool InFist(Combatant c)
        {
            Vector3 to = Query.Flat(c.Position - Position);
            float d = to.magnitude;
            if (d - radius - c.Radius > attackRange) return false;
            return d <= radius + c.Radius * 0.5f || Vector3.Angle(facing, to) <= attackArc * 0.5f;
        }

        Vector3 KeepOffEdges(Vector3 v, float dt)
        {
            if (WorldMap.Active)
            {
                if (WorldMap.Active.TrialActive) return v;
                bool allowGravity = moveSpeed >= 4.5f && WorldMap.Active.GetPull(GM.player.Position).sqrMagnitude > 0.01f;
                if (WorldMap.Active.IsWalkable(Position + v * dt, radius, allowGravity)) return v;
                return Vector3.zero;
            }
            if (ArenaShape.ContainsWithMargin(Position + v * dt, radius)) return v;
            var alongX = new Vector3(v.x, 0f, 0f);
            if (ArenaShape.ContainsWithMargin(Position + alongX * dt, radius)) return alongX;
            var alongZ = new Vector3(0f, 0f, v.z);
            if (ArenaShape.ContainsWithMargin(Position + alongZ * dt, radius)) return alongZ;
            return Vector3.zero;
        }

        void Stop() { if (!rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f); }

        void CancelWindup()
        {
            if (!windup) return;
            windup = false;
            SetLook(false);
            if (telegraph) telegraph.gameObject.SetActive(false);
        }

        void SetLook(bool yellow)
        {
            if (bodyRenderer) bodyRenderer.sharedMaterial = yellow && windupMaterial ? windupMaterial : normalMaterial;
            if (ShowArtWindup(yellow)) return;
            if (!yellow && artSprite) Art.Tint(artSprite, Color.white);
        }

        protected override void OnStateChanged(CharacterState from, CharacterState to)
        {
            if (to != CharacterState.Normal) CancelWindup();
        }

        [Header("Corpses can enter black holes.")]
        public float corpseLifetime = 8f;
        public float corpseDamping = 3f;
        public bool CorpseActive { get; private set; }
        public bool CorpseCarried { get; private set; }
        public bool CorpseFeedConsumed { get; private set; }
        float corpseSeconds;
        Vector3 corpseVelocity;

        void BeginCorpse()
        {
            if(!GM||!GM.useV2||!pooled||IsBoss||DeathCause!=DeathCause.HpDepleted)
            {if(pooled)gameObject.SetActive(false);else Destroy(gameObject);return;}
            CorpseActive=true;CorpseCarried=false;corpseSeconds=corpseLifetime;
            corpseVelocity=Vector3.ClampMagnitude(DeathVelocity,12f);
            var capsule=GetComponent<CapsuleCollider>();if(capsule)capsule.enabled=false;
            gameObject.SetActive(true);
        }
        public bool TryClaimCorpse(int version)
        {
            if(version!=SpawnVersion||!CorpseActive||CorpseCarried||CorpseFeedConsumed)return false;
            CorpseCarried=true;gameObject.SetActive(false);return true;
        }
        public void DropCorpse(int version,Vector3 at)
        {
            if(version!=SpawnVersion||!CorpseActive||!CorpseCarried||CorpseFeedConsumed)return;
            CorpseCarried=false;transform.position=new Vector3(at.x,0,at.z);rb.position=transform.position;
            corpseVelocity=Vector3.zero;gameObject.SetActive(true);
        }
        public bool TryConsumeCorpseFeed(int version)
        {
            if(version!=SpawnVersion||!CorpseActive||CorpseFeedConsumed)return false;
            CorpseFeedConsumed=true;DespawnCorpse(version);return true;
        }
        public void DespawnCorpse(int version)
        {
            if(version!=SpawnVersion||!CorpseActive)return;
            CorpseActive=CorpseCarried=false;gameObject.SetActive(false);
        }
        public void TickCorpse(float dt,Vector3 pull)
        {
            if(!CorpseActive||CorpseCarried||dt<=0)return;
            corpseSeconds-=dt;
            if(corpseSeconds<=0){DespawnCorpse(SpawnVersion);return;}
            corpseVelocity*=Mathf.Exp(-corpseDamping*dt);
            Vector3 next=Position+(corpseVelocity+pull)*dt;
            var world=WorldMap.Active;
            if(world&&!world.TrialActive){next=world.ClampPoint(next,radius);if(world.IsWall(next,radius*.5f))next=Position;}
            transform.position=new Vector3(next.x,0,next.z);rb.position=transform.position;
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
                if (definition.kind == EnemyKind.Splitter) { GM.waves.SpawnExtras(EnemyKind.Skitter, 3, Position, false); V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSplit,Position); }
                if (definition.kind == EnemyKind.Hollow) WorldMap.Active.CompleteTrial();
                if (definition.kind == EnemyKind.Tyrant) GM.NotifyFinalBossDefeated();
            }
            Fx.Pop(Position + Vector3.up * radius, bodyRenderer ? bodyRenderer.sharedMaterial.color : Color.red,
                radius * 3f);
            BeginCorpse();
        }

        protected override void OnDamaged(float amount) => revealTimer = 2f;

        protected override void OnFallFinished()
        {
            if (pooled) gameObject.SetActive(false); else Destroy(gameObject);
        }
    }
}
