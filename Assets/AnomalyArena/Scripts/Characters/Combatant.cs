using System;
using UnityEngine;

namespace AnomalyArena
{

    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public abstract class Combatant : MonoBehaviour, IWeaponHolder, IDamageDealer
    {
        public static readonly System.Collections.Generic.List<Combatant> All = new();
        ElementStatus elementStatus;
        readonly System.Collections.Generic.Dictionary<object, float> movementModifiers = new();
        float stunTimer;
        int wallSlams;
        public bool ActionsBlocked => stunTimer > 0 || elementStatus && elementStatus.BlocksActions || GM&&GM.useV2&&Team==Team.Player&&GM.skills&&GM.skills.ActionLocked;
        public float ExternalMoveMultiplier
        {
            get { float multiplier = Team==Team.Enemy?SkillLoadout.EnemySlowMultiplier(this):1f; foreach (var value in movementModifiers.Values) multiplier *= value; return multiplier; }
        }
        public void AddMovementModifier(object source, float multiplier) => movementModifiers[source] = multiplier;
        public void RemoveMovementModifier(object source) => movementModifiers.Remove(source);
        public void Stun(float seconds) => stunTimer = Mathf.Max(stunTimer, seconds);
        [Header("Stats")] public float maxHp = 10f;
        public float moveSpeed = 6f;
        public float radius = 0.5f;

        [Tooltip("Multiplier on knockback distance (0.5 for large enemies)")]
        public float knockbackScale = 1f;

        [Tooltip("Protection time after being hit; attack damage is ignored during it (wall damage is not)")]
        public float protectionTime = 0f;

        [Header("Refs")] public Renderer bodyRenderer;
        public Transform hand;

        public float Hp { get; protected set; }
        public CharacterState State { get; private set; } = CharacterState.Normal;
        public DeathCause DeathCause { get; private set; }
        public Weapon Weapon { get; private set; }
        public bool Protected => protectTimer > 0f;

        public event Action<Combatant> Eliminated;
        public int SpawnVersion { get; private set; }
        protected Vector3 DeathVelocity { get; private set; }

        protected Rigidbody rb;
        protected Vector3 knockVel;

        Vector3 pushVel;

        protected float weaponCooldown;
        KnockKind knockKind;

        ThrowImpact? throwImpact;
        float throwLaunchSpeed;
        float protectTimer;
        float fallTimer;
        bool eliminated;

        protected static GameManager GM => GameManager.Instance;

        public Transform Root => transform;
        public Vector3 Position => rb ? rb.position : transform.position;
        public float Radius => radius;
        public bool IsAlive => gameObject.activeInHierarchy && State != CharacterState.Dead && State != CharacterState.Falling;
        public IWeaponHolder Owner => this;
        public abstract Team Team { get; }
        public abstract Vector3 AimDirection { get; }

        public virtual Vector3 AimPoint => Position + Query.Flat(AimDirection).normalized * DefaultAimDistance;

        const float DefaultAimDistance = 5f;

        public virtual float CorpseDamage => 0f;

        public Combatant HeldShield => GM && GM.useV2 && this is PlayerController ? GM.runWeapons.HeldEnemy : Weapon != null && Weapon.Active is Hook h ? h.HeldTarget : null;

        public bool MovementLocked => GM && GM.useV2 && this is PlayerController && ((GM.runWeapons && GM.runWeapons.HandHookBusy)||(GM.skills&&GM.skills.MovementLocked)) || Weapon != null && Weapon.Active != null && Weapon.Active.LocksMovement;
        public float SpeedMultiplier => Weapon != null && Weapon.Active != null ? Weapon.Active.SpeedMultiplier : 1f;

        public static Combatant From(Collider col)
        {
            if (col == null) return null;
            if (col.attachedRigidbody) return col.attachedRigidbody.GetComponent<Combatant>();
            return col.GetComponentInParent<Combatant>();
        }

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            Hp = maxHp;
            if (!All.Contains(this)) All.Add(this);
            elementStatus = GetComponent<ElementStatus>();
            if(elementStatus) elementStatus.DamageTicked += OnElementTick;
        }

        void OnDestroy(){All.Remove(this);if(elementStatus)elementStatus.DamageTicked-=OnElementTick;}
        void OnElementTick(float amount,DamageSource source,Combatant dealer) => ReceiveDamage(new DamageInfo{amount=amount,source=source,dealer=dealer});
        public void ResetForSpawn()
        {
            SpawnVersion++;DeathVelocity=Vector3.zero;
            if(!rb)rb=GetComponent<Rigidbody>();
            Hp=maxHp;State=CharacterState.Normal;DeathCause=DeathCause.None;eliminated=false;
            stunTimer=0;wallSlams=0;movementModifiers.Clear();
            protectTimer=fallTimer=weaponCooldown=0;knockVel=pushVel=Vector3.zero;throwImpact=null;
            SetFlash(false);flashTimer=squashTimer=0;LastHitDirection=Vector3.forward;
            if(squashTarget)squashTarget.localScale=squashBase;
            rb.isKinematic=false;rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
            if(elementStatus)elementStatus.ResetForSpawn();gameObject.SetActive(true);
        }
        public void DiscardWithoutReward(){eliminated=true;State=CharacterState.Dead;if(rb){rb.linearVelocity=Vector3.zero;rb.isKinematic=true;}gameObject.SetActive(false);}
        public void ClearWaveState(){stunTimer=0;movementModifiers.Clear();knockVel=pushVel=Vector3.zero;protectTimer=0;throwImpact=null;if(IsAlive)SetState(CharacterState.Normal);if(rb&&!rb.isKinematic)rb.linearVelocity=Vector3.zero;if(elementStatus)elementStatus.ClearStatuses();}
        public void ProtectFor(float seconds)=>protectTimer=Mathf.Max(protectTimer,seconds);
        public void CancelDisplacement(){knockVel=pushVel=Vector3.zero;throwImpact=null;if(IsAlive)SetState(CharacterState.Normal);if(rb&&!rb.isKinematic)rb.linearVelocity=Vector3.zero;}
        public void BounceFromWall(Vector3 at,Vector3 away,float bounceDistance,bool takeDamage)
        {rb.position=at;transform.position=at;if(State is CharacterState.Dashing or CharacterState.Hooked)SetState(CharacterState.Normal);if(takeDamage)TakeWallDamage(away);if(IsAlive)Knockback(away,bounceDistance,KnockKind.Push);}

        protected Transform artPivot;

        protected Renderer artSprite;
        protected virtual Texture2D ArtTexture => null;

        protected virtual Texture2D ArtWindupTexture => null;

        protected virtual float ArtHeight => 2f;

        protected Renderer artWindupSprite;

        GameObject shieldVisual;

        public Vector3 HeadPoint(Camera cam)
        {
            if (artPivot && cam) return artPivot.position + cam.transform.up * (ArtHeight + 0.15f);
            return Position + Vector3.up * (radius * 2f + 0.6f);
        }

        protected virtual void Start()
        {
            if(GM&&GM.useV2){squashTarget=bodyRenderer?bodyRenderer.transform:null;if(squashTarget)squashBase=squashTarget.localScale;return;}
            if (Art.On && ArtTexture != null)
            {
                Art.HideWhitebox(gameObject);
                artPivot = Art.Billboard(transform, ArtTexture, ArtHeight, Art.OrderCharacter, out artSprite);
                if (ArtWindupTexture)
                {
                    artWindupSprite = Art.BillboardLayer(artPivot, ArtTexture, ArtHeight, ArtWindupTexture,
                        Art.OrderCharacter);
                    artWindupSprite.enabled = false;
                }
            }

            squashTarget = artPivot ? artPivot : bodyRenderer ? bodyRenderer.transform : null;
            if (squashTarget) squashBase = squashTarget.localScale;
            if (protectionTime > 0f) BuildShieldVisual();
        }

        protected bool ShowArtWindup(bool on)
        {
            if (!artWindupSprite) return false;
            artWindupSprite.enabled = on;
            artSprite.enabled = !on;
            return true;
        }

        void BuildShieldVisual()
        {
            if (artPivot && Art.Set.shieldBubble)
            {

                var bubble = Art.Set.shieldBubble;
                var r = Art.BillboardLayer(artPivot, bubble, ArtHeight * 1.15f, bubble, Art.OrderHeld);
                r.transform.localPosition = new Vector3(0f, ArtHeight * 0.5f, -0.02f);
                shieldVisual = r.gameObject;
            }
            else
            {
                shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(shieldVisual
                    .GetComponent<Collider>());                                                            
                shieldVisual.name = "Shield";
                shieldVisual.transform.SetParent(transform, false);
                shieldVisual.transform.localPosition = new Vector3(0f, radius * 1.6f, 0f);
                shieldVisual.transform.localScale = Vector3.one * (radius * 2.8f);
                var mr = shieldVisual.GetComponent<Renderer>();
                mr.sharedMaterial = GM.FxMat(new Color(0.6f, 0.85f, 1f, 0.28f));
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            shieldVisual.SetActive(false);
        }

        const float SquashTime = 0.15f;

        static readonly Color ArtFlashColor = new Color(6f, 6f, 6f, 1f);
        static MaterialPropertyBlock flashBlock;

        float flashTimer;
        float squashTimer;
        public float HitFlash01 => Mathf.Clamp01(flashTimer/Mathf.Max(.001f,GM?GM.feedback.flashTime:.1f));
        public float HitSquash01 => Mathf.Clamp01(squashTimer/SquashTime);

        public bool HitPaused => hitPauseTimer > 0f;
        public bool LastHitCritical { get; private set; }
        float hitPauseTimer;
        static float nextHitKickAt;
        public Vector3 LastHitDirection { get; private set; } = Vector3.forward;
        bool flashing;
        Transform squashTarget;
        Vector3 squashBase;

        static Color HitColor(HitKind kind) => kind switch
        {
            HitKind.Bullet => new Color(1f, 0.95f, 0.45f),
            HitKind.Pierce => new Color(0.7f, 0.95f, 1f),
            HitKind.Slam => new Color(0.95f, 0.9f, 0.8f),
            HitKind.Blast => new Color(1f, 0.5f, 0.15f),
            _ => new Color(1f, 0.8f, 0.35f),
        };

        void PlayHitFeedback(float amount, Vector3 dir, HitKind kind, bool crit = false)
        {
            var fb = GM.feedback;
            float heavyAt = fb.heavyDamage * (Team == Team.Enemy ? ElementCombat.WaveScale : 1f);
            bool heavy = crit || amount >= heavyAt || kind == HitKind.Slam || kind == HitKind.Blast;
            bool isPlayer = Team == Team.Player;
            flashTimer = fb.flashTime;
            squashTimer = SquashTime;
            LastHitDirection=Query.Flat(dir).sqrMagnitude>.0001f?Query.Flat(dir).normalized:Vector3.forward;

            Vector3 at = Query.AtCastHeight(Position);
            Color c = HitColor(kind);
            float size = Mathf.Max(0.12f, radius * 0.3f);

            var (count, speed, spread) = kind switch
            {
                HitKind.Bullet => (9, 12f, 50f),
                HitKind.Pierce => (10, 15f, 30f),
                HitKind.Slam => (14, 8f, 360f),
                HitKind.Blast => (16, 10f, 360f),
                _ => (heavy ? 14 : 9, heavy ? 11f : 8f, 120f),
            };
            Fx.Burst(at, spread >= 360f ? Vector3.zero : dir, c, count, speed, spread, size);
            var art = Art.On ? Art.Set : null;
            bool v4Sparks = GM.useV2 && FxPool.Active && FxPool.Active.hitSparks is { Length: > 0 };
            if (v4Sparks) { }
            else if (art && art.hitSpark)
                Fx.SpriteBillboard(art.hitSpark, at + Vector3.up * 0.3f, radius * 1.2f, radius * (heavy ? 3.2f : 2.4f),
                    0.2f);
            else
                Fx.Pop(at, c, radius * (heavy ? 3.5f : 2.5f));
            if (heavy) Fx.Ring(Position, radius + 2.5f, c);

            if (kind == HitKind.Slam && art && art.dust)
                Fx.SpriteBillboard(art.dust, new Vector3(Position.x, 0.1f, Position.z), radius * 1.5f, radius * 3.5f,
                    0.45f);

            if (!isPlayer && GM.useV2) PlayLocalImpact(at, dir, heavy, crit, c);
            if(amount>0f)GM.hud.DamageNumber(Position, amount, isPlayer ? new Color(1f, 0.3f, 0.25f) : crit ? new Color(1f, 0.55f, 0.12f) : new Color(1f, 0.95f, 0.6f),
                heavy || isPlayer);
            if (isPlayer)
            {
                GM.HitStop(fb.heavyHitStop);
                GM.Shake(fb.playerHitShake);
            }
            else
            {

                if (!GM.useV2 && kind != HitKind.Bullet)
                {
                    GM.HitStop(heavy ? fb.heavyHitStop : fb.hitStop);
                    if (heavy) GM.Shake(fb.heavyShake);
                }
            }
        }

        void PlayLocalImpact(Vector3 at, Vector3 dir, bool heavy, bool crit, Color color)
        {
            var fb = GM.feedback;
            hitPauseTimer = Mathf.Max(hitPauseTimer, crit ? fb.critHitPause : heavy ? fb.enemyHeavyHitPause : fb.enemyHitPause);
            if (crit) squashTimer = SquashTime * 1.35f;
            var pool = FxPool.Active;
            if (pool)
            {
                var flatDir = Query.Flat(dir).sqrMagnitude > .0001f ? Query.Flat(dir).normalized : LastHitDirection;
                if (pool.hitSparks != null && pool.hitSparks.Length > 0)
                    pool.EmitDirectional(pool.hitSparks[UnityEngine.Random.Range(0, pool.hitSparks.Length)], at + Vector3.up * .25f, flatDir,
                        radius * (heavy ? 4.2f : 3.2f), heavy ? .16f : .12f, Color.Lerp(Color.white, color, .35f));
                if (crit && pool.hitCrit) pool.Emit(at + Vector3.up * .6f, new Color(1f, .82f, .35f), radius * 4.5f, .26f, texture: pool.hitCrit, pulse: true);
                if (heavy && pool.hitRing) pool.Emit(new Vector3(Position.x, .08f, Position.z), new Color(color.r, color.g, color.b, .8f), radius * 3.2f, .22f, texture: pool.hitRing, flat: true, pulse: true);
            }
            if ((heavy || crit) && Time.unscaledTime >= nextHitKickAt)
            {
                nextHitKickAt = Time.unscaledTime + fb.hitKickInterval;
                GM.Shake(crit ? fb.critShake : fb.enemyHeavyShake);
            }
        }

        void LateUpdate()
        {

            float dt = Time.unscaledDeltaTime;
            if (shieldVisual)
            {

                bool show = Protected && IsAlive && (protectTimer > 0.15f || Mathf.Repeat(Time.time * 20f, 1f) < 0.5f);
                if (shieldVisual.activeSelf != show) shieldVisual.SetActive(show);
            }

            if (flashTimer > 0f || flashing)
            {
                flashTimer -= dt;
                SetFlash(flashTimer > 0f && IsAlive);
            }

            if (squashTimer > 0f && squashTarget && IsAlive)
            {
                squashTimer = Mathf.Max(0f, squashTimer - dt);
                float s = GM.feedback.squash * (squashTimer / SquashTime);

                squashTarget.localScale = artPivot
                    ? Vector3.Scale(squashBase, new Vector3(1f + s, 1f - s, 1f))
                    : Vector3.Scale(squashBase, new Vector3(1f + s, 1f - s, 1f + s));
            }
        }

        void SetFlash(bool on)
        {
            if (on == flashing) return;
            flashing = on;
            if (artSprite)
            {
                Art.Tint(artSprite, on ? ArtFlashColor : Color.white);
                Art.Tint(artWindupSprite, on ? ArtFlashColor : Color.white);
            }
            else if (bodyRenderer)
            {

                if (on)
                {
                    flashBlock ??= new MaterialPropertyBlock();
                    flashBlock.SetColor("_BaseColor", Color.white);
                    bodyRenderer.SetPropertyBlock(flashBlock);
                }
                else bodyRenderer.SetPropertyBlock(null);
            }
        }

        public void SetState(CharacterState s)
        {
            if (State == s || State == CharacterState.Dead) return;
            var old = State;

            if (old == CharacterState.Hooked || old == CharacterState.Dashing)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
            }

            if (old == CharacterState.Thrown)
            {
                if (s == CharacterState.Normal && throwImpact.HasValue) Stun(throwImpact.Value.stunSeconds);
                throwImpact = null;
            }

            State = s;

            switch (s)
            {
                case CharacterState.Normal:
                    knockVel = Vector3.zero;
                    break;
                case CharacterState.Hooked:
                case CharacterState.Dashing:
                    knockVel = Vector3.zero;
                    if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    break;
                case CharacterState.Falling:
                    fallTimer = 0f;
                    rb.isKinematic = false;
                    break;
                case CharacterState.Dead:
                    if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    break;
            }

            OnStateChanged(old, s);
        }

        protected virtual void OnStateChanged(CharacterState from, CharacterState to)
        {
        }

        protected virtual void FixedUpdate()
        {
            float dt = GM && GM.useV2 && Team == Team.Enemy ? GM.WorldFixedDeltaTime : Time.fixedDeltaTime;
            if(GM&&GM.useV2&&!GM.CanPlayerMove){if(!rb.isKinematic)rb.linearVelocity=Vector3.zero;return;}
            if(GM&&GM.useV2&&Team==Team.Player&&GM.skills&&GM.skills.MovementLocked&&IsAlive){if(!rb.isKinematic)rb.linearVelocity=Vector3.zero;return;}
            stunTimer=Mathf.Max(0,stunTimer-dt);
            if (protectTimer > 0f) protectTimer -= dt;
            if (weaponCooldown > 0f) weaponCooldown -= dt;

            if (IsAlive && State != CharacterState.Hooked && State != CharacterState.Dashing && rb.position.y < -2f)
                OnEnterFallZone(Vector3.zero);

            switch (State)
            {
                case CharacterState.Knocked:
                case CharacterState.Thrown:
                    knockVel *= Mathf.Exp(-GM.rules.knockDamping * dt);
                    rb.linearVelocity = new Vector3(knockVel.x, rb.linearVelocity.y, knockVel.z);
                    if (knockVel.magnitude < 0.3f) SetState(CharacterState.Normal);
                    break;
                case CharacterState.Falling:
                    fallTimer += dt;
                    if (fallTimer > 2f) OnFallFinished();
                    break;
                case CharacterState.Normal:
                    if(hitPauseTimer>0f)hitPauseTimer-=dt;
                    if(stunTimer>0||hitPauseTimer>0f||elementStatus&&elementStatus.BlocksMovement)rb.linearVelocity=new Vector3(0,rb.linearVelocity.y,0);
                    else
                    {

                        float rate=Team==Team.Enemy&&elementStatus?elementStatus.SpeedMultiplier:1f;
                        TickNormal(dt*rate);
                        float move=ExternalMoveMultiplier*rate;
                        if(!rb.isKinematic) rb.linearVelocity=new Vector3(rb.linearVelocity.x*move,rb.linearVelocity.y,rb.linearVelocity.z*move);
                    }
                    if (pushVel.sqrMagnitude > 1e-4f)
                    {
                        rb.linearVelocity += pushVel;
                        pushVel *= Mathf.Exp(-GM.rules.knockDamping * dt);
                    }

                    break;
            }

            if(GM&&GM.useV2&&Team==Team.Enemy&&!rb.isKinematic&&State is CharacterState.Normal or CharacterState.Knocked or CharacterState.Thrown)
                rb.linearVelocity=new Vector3(rb.linearVelocity.x*GM.WorldTimeScale,rb.linearVelocity.y,rb.linearVelocity.z*GM.WorldTimeScale);

            if (IsAlive && State != CharacterState.Dashing && State != CharacterState.Hooked)
            {
                Vector3 face = Query.Flat(AimDirection);
                if (face.sqrMagnitude > 1e-4f) rb.MoveRotation(Quaternion.LookRotation(face));
            }
        }

        protected abstract void TickNormal(float dt);

        public void Push(Vector3 dir, float distance)
        {
            if (!IsAlive) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;

            pushVel += dir.normalized * (distance * GM.rules.knockDamping);
        }

        public void SlamIntoWall(Vector3 at, Vector3 away, float bounceDistance)
        {
            if (!IsAlive) return;

            rb.position = at;
            transform.position = at;
            if (State == CharacterState.Dashing || State == CharacterState.Hooked) SetState(CharacterState.Normal);
            TakeWallDamage(away);
            if (IsAlive) Knockback(away, bounceDistance, KnockKind.Push);
        }

        public void Knockback(Vector3 dir, float distance, KnockKind kind)
        {
            if (!IsAlive || State == CharacterState.Hooked || State == CharacterState.Dashing) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
            dir.Normalize();
            wallSlams=0;
            if (kind == KnockKind.Hit) distance *= knockbackScale;
            if(elementStatus)distance*=elementStatus.FrozenKnockbackMultiplier;
            knockVel = dir * (distance * GM.rules.knockDamping);
            Vector3 cur = Query.Flat(rb.linearVelocity);
            rb.AddForce(knockVel - cur, ForceMode.VelocityChange);
            knockKind = kind;
            SetState(kind == KnockKind.Throw ? CharacterState.Thrown : CharacterState.Knocked);
        }

        public void Throw(Vector3 dir, float distance, ThrowImpact impact)
        {
            Knockback(dir, distance, KnockKind.Throw);
            if (State != CharacterState.Thrown) return;
            throwImpact = impact;
            throwLaunchSpeed = knockVel.magnitude;
        }

        void OnCollisionEnter(Collision c) => CheckCollision(c);
        void OnCollisionStay(Collision c) => CheckCollision(c);

        void CheckCollision(Collision c)
        {

            CheckWallHit(c);
            CheckThrowImpact(c);
        }

        void CheckThrowImpact(Collision c)
        {
            if (State != CharacterState.Thrown || throwImpact == null) return;
            var hit = From(c.collider);
            if (hit == null || hit == this || !hit.IsAlive || hit.Team == Team.Player) return;
            float speed = knockVel.magnitude;
            if (speed < GM.rules.wallHitSpeed) return;

            var impact = throwImpact.Value;
            float speedRatio = Mathf.Clamp01(speed / throwLaunchSpeed);
            float damage = impact.DamageAt(speedRatio, radius);
            float push = impact.KnockbackAt(speedRatio, radius);
            float reach = radius + impact.radius;
            Vector3 center = Position;

            SetState(CharacterState.Normal);
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            Fx.Ring(center, reach, new Color(0.95f, 0.9f, 0.8f, 0.7f));
            foreach (var victim in Query.Characters(Query.AtCastHeight(center), reach))
            {
                if (!victim.IsAlive || victim.Team == Team.Player) continue;
                bool self = victim == this;
                Vector3 away = self ? Vector3.zero : Query.Flat(victim.Position - center);
                var info=DamageInfo.Attack(damage,impact.dealer,away,self?0:push,HitKind.Slam);
                info.element=impact.element;info.baseDamage=damage;
                victim.ReceiveDamage(info);
            }
        }

        void CheckWallHit(Collision c)
        {
            if (State != CharacterState.Knocked && State != CharacterState.Thrown) return;
            if (c.gameObject.layer != GameManager.WallLayer) return;
            for (int i = 0; i < c.contactCount; i++)
            {
                Vector3 to = Query.Flat(c.GetContact(i).point - rb.position);
                if (to.sqrMagnitude < 1e-6f) continue;
                if (Vector3.Dot(knockVel, to.normalized) < GM.rules.wallHitSpeed) continue;
                bool damage = knockKind != KnockKind.Push;
                Vector3 rebound=Vector3.Reflect(knockVel,c.GetContact(i).normal)*.5f;
                SetState(CharacterState.Normal);
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                if (damage) TakeWallDamage(-to.normalized);
                if(GM.useV2&&IsAlive&&damage&&++wallSlams<3&&rebound.magnitude>.3f)
                {knockVel=rebound;knockKind=KnockKind.Hit;SetState(CharacterState.Knocked);rb.linearVelocity=rebound*(Team==Team.Enemy?GM.WorldTimeScale:1);}
                return;
            }
        }

        void TakeWallDamage(Vector3 away)
        {
            if (!IsAlive) return;
            float damage=GM.rules.wallDamage;

            if(GM.useV2&&Team==Team.Enemy)damage=(damage+(GM.progression?GM.progression.Stats.impact:0))*GM.waves.HpMultiplier;
            ReceiveDamage(new DamageInfo{amount=damage,knockDir=away,kind=HitKind.Slam,source=DamageSource.Wall});
        }

        public virtual bool ReceiveDamage(DamageInfo info)
        {
            if (!IsAlive) return false;
            if (info.isAttack && (Protected || State == CharacterState.Dashing)) return false;
            float beforeHp=Hp,beforeShield=elementStatus?elementStatus.Shield:0;
            bool crit=false;
            var runStats=GM&&GM.useV2&&GM.progression?GM.progression.Stats:null;
            if(runStats!=null&&info.isAttack&&Team==Team.Enemy&&info.source==DamageSource.Hit&&info.dealer?.Owner is Combatant{Team:Team.Player}
               &&runStats.critChance>0f&&UnityEngine.Random.value<runStats.critChance)
            {crit=true;info.amount*=runStats.CritMultiplier;}
            LastHitCritical=crit;
            float raw=info.amount*ElementCombat.DirectDamageFactor(this,info.element);
            if(runStats!=null&&Team==Team.Player)raw*=runStats.DamageTakenMultiplier;

            if(info.isAttack||raw>0f)raw=Mathf.Max(1f,raw);
            float dealt=elementStatus?elementStatus.ResolveDamage(raw,info.source,info.ignoreShield):raw;
            if(dealt>0f)dealt=Mathf.Max(1f,dealt);                                                                 
            Hp -= dealt;
            if (info.isAttack && raw>0f && protectionTime > 0f) protectTimer = protectionTime;
            bool tick=info.source==DamageSource.Burn||info.source==DamageSource.Poison;
            float hpDamage=Mathf.Min(beforeHp,Mathf.Max(0f,dealt));
            float shieldDamage=Mathf.Max(0f,beforeShield-(elementStatus?elementStatus.Shield:0));
            float actualDamage=hpDamage+shieldDamage;
            if(!tick&&actualDamage>0f)
            {
                PlayHitFeedback(hpDamage,info.knockDir,info.kind,crit);
            }
            else if(tick&&hpDamage>0f)
                GM.hud?.DamageNumber(Position,hpDamage,info.source==DamageSource.Burn?new Color(1f,.48f,.2f):new Color(.6f,.9f,.35f),false);
            if(shieldDamage>0f)GM.hud?.DamageNumber(Position+Vector3.up*.45f,shieldDamage,Color.yellow,false);
            OnDamaged(dealt);
            if(!tick&&actualDamage>0&&GM&&GM.useV2)V3WorldAudio.Instance?.OnConfirmedHit(this,info.kind);
            if (Hp <= 0f)
            {
                Hp = 0f;
                Die(DeathCause.HpDepleted);
            }
            else if (info.knockDistance > 0f) Knockback(info.knockDir, info.knockDistance, KnockKind.Hit);
            if(!tick&&info.source==DamageSource.Hit&&info.dealer?.Owner is Combatant { Team: Team.Player } source)
                ElementCombat.ApplyHit(this,info.element,info.baseDamage>0?info.baseDamage:info.amount,actualDamage,source,info.builtInElement);
            return true;
        }

        public void Heal(float amount){float gained=Mathf.Min(maxHp-Hp,amount);Hp+=gained;if(elementStatus&&gained>0)elementStatus.OnHeal();}

        public void Kill()
        {
            if (!IsAlive) return;
            PlayHitFeedback(Mathf.Max(Hp, GM.feedback.heavyDamage), Vector3.zero, HitKind.Pierce);
            Hp = 0f;
            Die(DeathCause.HpDepleted);
        }

        protected void Die(DeathCause cause)
        {
            if (!IsAlive) return;
            DeathVelocity=rb&&!rb.isKinematic?Query.Flat(rb.linearVelocity):Vector3.zero;
            if(DeathVelocity.sqrMagnitude<.01f)DeathVelocity=LastHitDirection*3f;
            Hp = Mathf.Max(0f, Hp);
            DeathCause = cause;
            SetState(CharacterState.Dead);
            if(GM&&GM.useV2&&Team==Team.Player)V3WorldAudio.Instance?.OnPlayerDeath(this);
            if(GM&&GM.useV2&&Team==Team.Enemy)SkillLoadout.NotifyEnemyDied(this);
            OnDied();
            ReportEliminated();
        }

        public void OnEnterFallZone(Vector3 outward)
        {
            if (!IsAlive || State == CharacterState.Hooked) return;
            DeathCause = DeathCause.FellIntoGap;
            Vector3 v = State == CharacterState.Dashing || rb.isKinematic
                ? Vector3.zero
                : Query.Flat(rb.linearVelocity);
            SetState(CharacterState.Falling);
            if(GM&&GM.useV2&&Team==Team.Player)V3WorldAudio.Instance?.OnPlayerDeath(this);
            rb.linearVelocity = v + outward * 3f;
            OnFell();
            ReportEliminated();
        }

        void ReportEliminated()
        {
            if (eliminated) return;
            eliminated = true;
            Eliminated?.Invoke(this);
        }

        protected virtual void OnDamaged(float amount)
        {
        }

        protected virtual void OnDied()
        {
        }

        protected virtual void OnFell()
        {
        }

        protected virtual void OnFallFinished()
        {
            gameObject.SetActive(false);
        }

        public void SetMovePosition(Vector3 p) => rb.MovePosition(p);

        public void Equip(Weapon w)
        {
            Weapon = w;
            w.AttachTo(this, hand ? hand : transform);
        }

        public void ReleaseWeapon()
        {
            if (Weapon != null && Weapon.Active != null) Weapon.Active.OnUseReleased();
        }

        public Weapon Unequip()
        {
            var w = Weapon;
            Weapon = null;
            return w;
        }

        public bool TryUseWeapon()
        {
            var w = Weapon;
            if (w == null || !IsAlive) return false;
            if (w.Active != null)
            {
                w.Active.OnUsePressed();
                return true;
            }

            if (w.InFlight) return false;
            if (State != CharacterState.Normal || weaponCooldown > 0f) return false;
            w.ApplyDebugForce();
            bool first = !w.Revealed;
            bool fromClip = w.RoundsLeft > 0;
            w.Effect.Use(w, this);
            if (fromClip) w.RoundsLeft--;
            else
            {
                w.UsesLeft--;
                w.RoundsLeft = Mathf.Max(1, w.Effect.roundsPerUse) - 1;
            }

            w.Revealed = true;
            weaponCooldown = w.Effect.cooldown;
            if (first) OnWeaponRevealed(w);
            CheckWeaponDepleted();
            return true;
        }

        protected virtual void OnWeaponRevealed(Weapon w)
        {
        }

        public void OnWeaponRuntimeFinished() => CheckWeaponDepleted();

        public void CheckWeaponDepleted()
        {
            if (Weapon == null || Weapon.UsesLeft > 0 || Weapon.RoundsLeft > 0 || Weapon.Active != null ||
                Weapon.InFlight) return;
            var w = Unequip();
            w.Consume();
        }
    }
}
