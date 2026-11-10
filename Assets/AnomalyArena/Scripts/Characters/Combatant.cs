using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Shared implementation for the player and enemies: HP, damage and protection, rigidbody-impulse knockback, wall damage, falling into gaps, state machine.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public abstract class Combatant : MonoBehaviour, IDamageReceiver, IDamageDealer
    {
        public static readonly System.Collections.Generic.List<Combatant> All = new();
        ElementStatus elementStatus;
        readonly System.Collections.Generic.Dictionary<object, float> movementModifiers = new();
        float stunTimer;
        int wallSlams;
        public bool ActionsBlocked => stunTimer > 0 || elementStatus && elementStatus.BlocksActions || GM&&Team==Team.Player&&GM.skills&&GM.skills.ActionLocked;
        public float ExternalMoveMultiplier
        {
            get { float multiplier = Team==Team.Enemy?SkillLoadout.EnemySlowMultiplier(this):1f; foreach (var value in movementModifiers.Values) multiplier *= value; return multiplier; }
        }
        public void AddMovementModifier(object source, float multiplier) => movementModifiers[source] = multiplier;
        public void RemoveMovementModifier(object source) => movementModifiers.Remove(source);
        public void Stun(float seconds) => stunTimer = Mathf.Max(stunTimer, seconds);
        /// <summary>A real stun (rammer hit, hook-throw crash): no movement, no attacks, no skills. Ordinary hits never stun the player.</summary>
        public bool Stunned => stunTimer > 0f;
        [Header("Stats")] public float maxHp = 10f;
        public float moveSpeed = 6f;
        public float radius = 0.5f;

        [Tooltip("Multiplier on knockback distance. The player keeps this value; an enemy's is set from its body size when it spawns (GameManager > Rules > Knockback).")]
        public float knockbackScale = 1f;

        [Tooltip("Protection time after being hit; attack damage is ignored during it (wall damage is not)")]
        public float protectionTime = 0f;

        [Header("Refs")] public Renderer bodyRenderer;
        public Transform hand;

        public float Hp { get; protected set; }
        public CharacterState State { get; private set; } = CharacterState.Normal;
        public DeathCause DeathCause { get; private set; }
        /// <summary>Who dealt the last hit or tick that cost HP or Shield (null for walls and other hazards), and its source; read by the run-end report.</summary>
        public Combatant LastDamagedBy { get; private set; }
        public DamageSource LastDamageSource { get; private set; }
        public bool Protected => protectTimer > 0f;
        /// <summary>Enemy lab: hits still land (damage, stun, feedback) but the body cannot die; HP stops at 1 and a fall puts it back on safe ground.</summary>
        [System.NonSerialized] public bool Invulnerable;

        public event Action<Combatant> Eliminated;
        /// <summary>An accepted attack that cost HP or Shield; damage over time does not raise it.</summary>
        public event Action<DamageInfo> AttackReceived;
        // Any damage that took HP or shield: hits, burn and poison ticks, wall slams. The float is the amount taken.
        public event Action<DamageInfo, float> DamageTaken;
        public int SpawnVersion { get; private set; }
        protected Vector3 DeathVelocity { get; private set; }

        protected Rigidbody rb;
        protected Vector3 knockVel;

        /// <summary>Push added on top of normal movement (burst-fire recoil), decaying with the same factor as knockback.</summary>
        Vector3 pushVel;

        /// <summary>Shatter ice: the walking velocity carried on ice, which eases toward the one the body asks for.</summary>
        Vector3 slideVel;
        float slideFxClock;

        KnockKind knockKind;

        // Set while Thrown by the hook: what happens if this character crashes into another one, and the speed it was launched at
        ThrowImpact? throwImpact;
        float throwLaunchSpeed;
        float protectTimer;
        float fallTimer;
        bool eliminated;

        protected static GameManager GM => GameManager.Instance;

        // ───── Body ─────
        public Vector3 Position => rb ? rb.position : transform.position;
        public float Radius => radius;
        public bool IsAlive => gameObject.activeInHierarchy && State != CharacterState.Dead && State != CharacterState.Falling;
        public Combatant Owner => this;
        public abstract Team Team { get; }
        public abstract Vector3 AimDirection { get; }

        /// <summary>Without a cursor, the aim point is a fixed distance along the aim direction.</summary>
        public virtual Vector3 AimPoint => Position + Query.Flat(AimDirection).normalized * DefaultAimDistance;

        const float DefaultAimDistance = 5f;

        /// <summary>Damage this corpse deals when carried back by the throwing knife (= its attack damage when alive).</summary>
        public virtual float CorpseDamage => 0f;

        /// <summary>The enemy this character is holding with the hook and can use as cover.</summary>
        public Combatant HeldShield => GM && GM.runWeapons && this is PlayerController ? GM.runWeapons.HeldEnemy : null;

        public bool MovementLocked => GM && this is PlayerController && ((GM.runWeapons && GM.runWeapons.HookLocksMovement)||(GM.skills&&GM.skills.MovementLocked));

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
        // Burn and Poison ticks carry their element, so an immune body takes none of them (document 9.5).
        void OnElementTick(float amount,DamageSource source,Combatant dealer) => ReceiveDamage(new DamageInfo{amount=amount,source=source,dealer=dealer,element=source==DamageSource.Burn?ElementKind.Fire:ElementKind.Poison});
        public void ResetForSpawn()
        {
            SpawnVersion++;DeathVelocity=Vector3.zero;
            if(!rb)rb=GetComponent<Rigidbody>();
            Hp=maxHp;State=CharacterState.Normal;DeathCause=DeathCause.None;eliminated=false;LastDamagedBy=null;LastDamageSource=DamageSource.Hit;
            stunTimer=0;wallSlams=0;movementModifiers.Clear();
            protectTimer=fallTimer=0;knockVel=pushVel=slideVel=Vector3.zero;throwImpact=null;
            SetFlash(false);flashTimer=squashTimer=0;LastHitDirection=Vector3.forward;
            if(squashTarget)squashTarget.localScale=squashBase;
            rb.isKinematic=false;rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
            if(elementStatus)elementStatus.ResetForSpawn();gameObject.SetActive(true);
        }
        public void DiscardWithoutReward(){eliminated=true;State=CharacterState.Dead;if(rb){rb.linearVelocity=Vector3.zero;rb.isKinematic=true;}gameObject.SetActive(false);}
        public void ClearWaveState(){stunTimer=0;movementModifiers.Clear();knockVel=pushVel=slideVel=Vector3.zero;protectTimer=0;throwImpact=null;if(IsAlive)SetState(CharacterState.Normal);if(rb&&!rb.isKinematic)rb.linearVelocity=Vector3.zero;if(elementStatus)elementStatus.ClearStatuses();}
        public void ProtectFor(float seconds)=>protectTimer=Mathf.Max(protectTimer,seconds);
        public void CancelDisplacement(){knockVel=pushVel=slideVel=Vector3.zero;throwImpact=null;if(IsAlive)SetState(CharacterState.Normal);if(rb&&!rb.isKinematic)rb.linearVelocity=Vector3.zero;}
        public void BounceFromWall(Vector3 at,Vector3 away,float bounceDistance,bool takeDamage)
        {rb.position=at;transform.position=at;if(State is CharacterState.Dashing or CharacterState.Hooked)SetState(CharacterState.Normal);if(takeDamage)TakeWallDamage(away);if(IsAlive)Knockback(away,bounceDistance,KnockKind.Push);}


        /// <summary>Position above the head (where the HP bar goes): in illustrated mode this is the top of the billboard along the camera's up.</summary>
        public Vector3 HeadPoint(Camera cam) => Position + Vector3.up * (radius * 2f + 0.6f);

        V2ModelPresentation shownModel;
        WhiteboxCharacter shownWhitebox;
        bool shownModelLooked;

        /// <summary>The top of the shown 3D model as cam sees it: the middle of the model's bounds, pushed along the camera's up to the
        /// bounds' edge, so a label drawn there sits just over the model in any view (2026-11-06, the 2.5D camera). In whitebox (since
        /// 2026-11-10) the top of the whitebox look (WhiteboxCharacter.TryTop). False while neither is shown: the caller then keeps
        /// HeadPoint.</summary>
        public bool TryModelTop(Camera cam, out Vector3 point)
        {
            point = default;
            if (!cam) return false;
            if (!shownModelLooked) { shownModel = GetComponent<V2ModelPresentation>(); shownWhitebox = GetComponent<WhiteboxCharacter>(); shownModelLooked = true; }
            if (shownWhitebox && shownWhitebox.TryTop(cam, out point)) return true;
            if (!shownModel || !shownModel.UsingModel || shownModel.modelRenderers == null) return false;
            bool any = false;
            Bounds bounds = default;
            foreach (var shown in shownModel.modelRenderers)
            {
                if (!shown || !shown.enabled || !shown.gameObject.activeInHierarchy) continue;
                if (any) bounds.Encapsulate(shown.bounds);
                else { bounds = shown.bounds; any = true; }
            }
            if (!any) return false;
            Vector3 up = cam.transform.up, half = bounds.extents;
            point = bounds.center + up * (Mathf.Abs(up.x) * half.x + Mathf.Abs(up.y) * half.y + Mathf.Abs(up.z) * half.z);
            return true;
        }

        protected virtual void Start()
        {
            squashTarget=bodyRenderer?bodyRenderer.transform:null;if(squashTarget)squashBase=squashTarget.localScale;
        }

        // ───── Hit feedback ─────

        // Duration of the squash-and-recover (seconds, real time)
        const float SquashTime = 0.15f;

        static MaterialPropertyBlock flashBlock;

        float flashTimer;
        float squashTimer;
        public float HitFlash01 => Mathf.Clamp01(flashTimer/Mathf.Max(.001f,GM?GM.feedback.flashTime:.1f));
        public float HitSquash01 => Mathf.Clamp01(squashTimer/SquashTime);
        /// <summary>Local hit-stop: only this body holds its pose and animation; the world keeps running.</summary>
        public bool HitPaused => hitPauseTimer > 0f;
        /// <summary>The model holds its pose: a local hit-stop on the victim, or the attacker's short melee bite (movement is not held).</summary>
        public bool PoseHeld => hitPauseTimer > 0f || poseHoldTimer > 0f;
        float poseHoldTimer;
        float hitPauseReadyAt;
        Vector3 kickDirection;
        float kickAmount, kickLength, kickAge = 1f;

        /// <summary>Visual-only offset of the model (hit jolt, firing recoil); it springs back over its length.</summary>
        public Vector3 VisualOffset
        {
            get
            {
                if (kickAge >= kickLength || kickLength <= 0f) return Vector3.zero;
                float k = 1f - kickAge / kickLength;
                return kickDirection * (kickAmount * k * k);
            }
        }

        /// <summary>Moves the model along dir by amount (negative = against dir) and lets it spring back over seconds.</summary>
        public void VisualKick(Vector3 dir, float amount, float seconds)
        {
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-4f || Mathf.Approximately(amount, 0f) || seconds <= 0f) return;
            if (Mathf.Abs(amount) < Mathf.Abs(VisualOffset.magnitude)) return; // a weaker kick never cuts a stronger one short
            kickDirection = dir.normalized; kickAmount = amount; kickLength = seconds; kickAge = 0f;
        }

        /// <summary>Holds this model's animation for a moment; movement and actions continue.</summary>
        public void HoldPose(float seconds) => poseHoldTimer = Mathf.Max(poseHoldTimer, seconds);
        public bool LastHitCritical { get; private set; }
        float hitPauseTimer;
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

        /// <summary>
        /// Hit feedback: white flash, squash and recover, shards flying in the hit direction, shock ring, floating damage number;
        /// the player being hit, heavy hits, wall impacts and explosions also add hit stop and screen shake. Finite attack and wall damage share this feedback path.
        /// </summary>
        void PlayHitFeedback(float amount, Vector3 dir, HitKind kind, bool crit = false, HitFeel feel = HitFeel.Auto, Combatant attacker = null)
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
            // Bullet, pierce: a narrow fast jet; blunt: a wide fan; slam, blast: all directions
            var (count, speed, spread) = kind switch
            {
                HitKind.Bullet => (9, 12f, 50f),
                HitKind.Pierce => (10, 15f, 30f),
                HitKind.Slam => (14, 8f, 360f),
                HitKind.Blast => (16, 10f, 360f),
                _ => (heavy ? 14 : 9, heavy ? 11f : 8f, 120f),
            };
            if (feel == HitFeel.Auto) feel = WeaponFeel.FromKind(kind);
            var profile = fb.For(feel);
            if (!isPlayer) count = Mathf.Max(1, Mathf.RoundToInt(count * profile.splash));
            if (V5Vfx.On)
            {
                // Illustrated art: sparks and a flash on the body; the big ground ring stays for real area attacks only.
                V5Vfx.I.Hit(at, dir, c, radius, crit || amount >= heavyAt || kind == HitKind.Slam, crit, this is Enemy);
                // A body thrown into a wall: dust and grit where it struck (document 4.4; the terrain is a weapon).
                if (kind == HitKind.Slam) V5Vfx.I.GroundSlam(Position, radius + 1.4f, c, false);
            }
            else
            {
                Fx.Burst(at, spread >= 360f ? Vector3.zero : dir, c, count, speed, spread, size);
                bool v4Sparks = FxPool.Active && FxPool.Active.hitSparks is { Length: > 0 };
                if (!v4Sparks) Fx.Pop(at, c, radius * (heavy ? 3.5f : 2.5f));
                // The ground shock ring marks a genuinely heavy impact; an area tick (flame, spores) that only counts as a blast by kind draws none.
                if (crit || amount >= heavyAt || kind == HitKind.Slam) Fx.Ring(Position, radius + 2.5f, c);
            }

            if (!isPlayer) PlayLocalImpact(at, dir, heavy, crit, c, profile, attacker);
            if(amount>0f)GM.hud.DamageNumber(Position, amount, isPlayer ? new Color(1f, 0.3f, 0.25f) : crit ? new Color(1f, 0.55f, 0.12f) : new Color(1f, 0.95f, 0.6f),
                heavy || isPlayer);
            if (isPlayer)
            {
                // The player flinches along the hit and the camera is pushed away from the attacker; the game clock never slows.
                var enemy = attacker as Enemy;
                float push = enemy && enemy.IsBoss ? fb.bossHitKick : enemy && enemy.IsSpecial ? fb.eliteHitKick : fb.basicHitKick;
                GM.Kick(LastHitDirection, push);
                VisualKick(LastHitDirection, fb.playerFlinch, .15f);
            }
        }

        /// <summary>V4 impact without a world pause: this body holds its pose, a directional spark and crit star mark the hit, heavy hits kick the camera a little.</summary>
        void PlayLocalImpact(Vector3 at, Vector3 dir, bool heavy, bool crit, Color color, HitFeelProfile profile, Combatant attacker)
        {
            var fb = GM.feedback;
            bool big = heavy || crit;
            // Hold the pose at most once per cooldown, so rapid fire or a stream never freezes the target. A boss only jolts: a hold
            // stops its move, summon and platform clocks, and steady fire would slow the whole fight.
            float hold = big ? profile.victimHeavyHold : profile.victimHold;
            if (hold > 0f && Time.unscaledTime >= hitPauseReadyAt && !(this is Enemy { IsBoss: true }))
            {
                hitPauseTimer = Mathf.Max(hitPauseTimer, hold);
                hitPauseReadyAt = Time.unscaledTime + hold + fb.victimHoldCooldown;
            }
            if (crit) squashTimer = SquashTime * 1.35f;
            var hitDirection = Query.Flat(dir).sqrMagnitude > .0001f ? Query.Flat(dir).normalized : LastHitDirection;
            VisualKick(hitDirection, profile.victimJolt * (big ? 1.4f : 1f), .12f);
            if (attacker is PlayerController && profile.attackerHold > 0f) attacker.HoldPose(profile.attackerHold);
            var pool = V5Vfx.On ? null : FxPool.Active;
            if (pool)
            {
                var flatDir = Query.Flat(dir).sqrMagnitude > .0001f ? Query.Flat(dir).normalized : LastHitDirection;
                if (pool.hitSparks != null && pool.hitSparks.Length > 0)
                    pool.EmitDirectional(pool.hitSparks[UnityEngine.Random.Range(0, pool.hitSparks.Length)], at + Vector3.up * .25f, flatDir,
                        radius * (heavy ? 4.2f : 3.2f), heavy ? .16f : .12f, Color.Lerp(Color.white, color, .35f));
                if (crit && pool.hitCrit) pool.Emit(at + Vector3.up * .6f, new Color(1f, .82f, .35f), radius * 4.5f, .26f, texture: pool.hitCrit, pulse: true);
                if (heavy && pool.hitRing) pool.Emit(new Vector3(Position.x, .08f, Position.z), new Color(color.r, color.g, color.b, .8f), radius * 3.2f, .22f, texture: pool.hitRing, flat: true, pulse: true);
            }
            GM.HitKick(hitDirection, big ? profile.heavyHitKick : profile.hitKick);
        }

        /// <summary>Flash and squash only change appearance, so they run in LateUpdate: scale and color are overridden after all movement and state are done.</summary>
        void LateUpdate()
        {
            // Uses real time: flash and squash keep playing during hit stop, which gives the 'freeze frame' look
            float dt = Time.unscaledDeltaTime;
            if (poseHoldTimer > 0f) poseHoldTimer -= dt;
            if (kickAge < kickLength) kickAge += dt;
            if (flashTimer > 0f || flashing)
            {
                flashTimer -= dt;
                SetFlash(flashTimer > 0f && IsAlive);
            }

            if (squashTimer > 0f && squashTarget && IsAlive)
            {
                squashTimer = Mathf.Max(0f, squashTimer - dt);
                float s = GM.feedback.squash * (squashTimer / SquashTime);
                // Billboard: wider horizontally, shorter vertically; whitebox capsule: wider on both horizontal axes, shorter in height
                squashTarget.localScale = Vector3.Scale(squashBase, new Vector3(1f + s, 1f - s, 1f + s));
            }
        }

        void SetFlash(bool on)
        {
            if (on == flashing) return;
            flashing = on;
            if (bodyRenderer)
            {
                // Whitebox overrides color with a PropertyBlock: the shared material is untouched.
                if (on)
                {
                    flashBlock ??= new MaterialPropertyBlock();
                    flashBlock.SetColor("_BaseColor", Color.white);
                    bodyRenderer.SetPropertyBlock(flashBlock);
                }
                else RefreshBodyTint(); // back to the body colour (an enemy's kind or wind-up colour), not to the bare material
            }
        }

        /// <summary>Whitebox body colour this character shows when not flashing; null keeps the material's own colour.</summary>
        protected virtual Color? BodyTint => null;

        /// <summary>Applies BodyTint now (skipped during a hit flash, which restores it when it ends).</summary>
        protected void RefreshBodyTint()
        {
            if (!bodyRenderer || flashing) return;
            var tint = BodyTint;
            if (tint.HasValue) Art.Tint(bodyRenderer, tint.Value);
            else bodyRenderer.SetPropertyBlock(null);
        }

        // ───── State machine ─────

        public void SetState(CharacterState s)
        {
            if (State == s || State == CharacterState.Dead) return;
            var old = State;

            // Exit the old state
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
            slideVel = Vector3.zero; // a slide is a walk: a knockback, a throw or a stop ends it

            // Enter the new state
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
            float dt = GM && Team == Team.Enemy ? GM.WorldFixedDeltaTime : Time.fixedDeltaTime;
            if(GM&&!GM.CanPlayerMove){if(!rb.isKinematic)rb.linearVelocity=Vector3.zero;slideVel=Vector3.zero;return;}
            if(GM&&Team==Team.Player&&GM.skills&&GM.skills.MovementLocked&&IsAlive){if(!rb.isKinematic)rb.linearVelocity=Vector3.zero;slideVel=Vector3.zero;return;}
            stunTimer=Mathf.Max(0,stunTimer-dt);
            if (protectTimer > 0f) protectTimer -= dt;

            // Fallback: far off the platform without having touched a FallZone
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
                    if(stunTimer>0||hitPauseTimer>0f||elementStatus&&elementStatus.BlocksMovement){rb.linearVelocity=new Vector3(0,rb.linearVelocity.y,0);slideVel=Vector3.zero;}
                    else
                    {
                        // Chill and paralysis slow both the walk and the attack timers of this enemy only.
                        float rate=Team==Team.Enemy&&elementStatus?elementStatus.SpeedMultiplier:1f;
                        bool slips=SlipsOnIce;
                        TickNormal(dt*rate);
                        float move=ExternalMoveMultiplier*rate;
                        if(!rb.isKinematic) rb.linearVelocity=new Vector3(rb.linearVelocity.x*move,rb.linearVelocity.y,rb.linearVelocity.z*move);
                        // A charge or a rush that starts or ends this frame is not a walk either: it ends with a stop, as off the ice.
                        if(!rb.isKinematic){if(slips&&SlipsOnIce)SlideOnIce(dt);else slideVel=Vector3.zero;}
                    }
                    if (pushVel.sqrMagnitude > 1e-4f)
                    {
                        rb.linearVelocity += pushVel;
                        pushVel *= Mathf.Exp(-GM.rules.knockDamping * dt);
                    }

                    break;
            }

            if(GM&&Team==Team.Enemy&&!rb.isKinematic&&State is CharacterState.Normal or CharacterState.Knocked or CharacterState.Thrown)
                rb.linearVelocity=new Vector3(rb.linearVelocity.x*GM.WorldTimeScale,rb.linearVelocity.y,rb.linearVelocity.z*GM.WorldTimeScale);

            if (IsAlive && State != CharacterState.Dashing && State != CharacterState.Hooked)
            {
                Vector3 face = Query.Flat(AimDirection);
                if (face.sqrMagnitude > 1e-4f) rb.MoveRotation(Quaternion.LookRotation(face));
            }
        }

        protected abstract void TickNormal(float dt);

        /// <summary>Shatter ice: on ice the walk changes gradually instead of at once, so starting, stopping and turning
        /// take a moment and a body that stops walking slides on (RunWeapons.IceGrip). Only the walk: knockback and pushes are untouched.</summary>
        void SlideOnIce(float dt)
        {
            Vector3 want=new Vector3(rb.linearVelocity.x,0f,rb.linearVelocity.z);
            float grip=GM&&GM.runWeapons?GM.runWeapons.IceGrip(this):0f;
            if(grip<=0f){slideVel=want;return;}
            slideVel=GuardSlide(Vector3.Lerp(slideVel,want,1f-Mathf.Exp(-grip*dt)),dt);
            rb.linearVelocity=new Vector3(slideVel.x,rb.linearVelocity.y,slideVel.z);
            // Frost puffs under a body that slips, so the slide reads even where the ground art is off (whitebox).
            if(slideVel.sqrMagnitude>1f&&(slideFxClock-=dt)<=0f){slideFxClock=.2f;Fx.Pop(new Vector3(Position.x,.15f,Position.z),new Color(.8f,.95f,1f,.8f),.7f);}
        }

        /// <summary>The part of a slide this body may take: all of it for the player; an enemy never slides into what it would not walk into.</summary>
        protected virtual Vector3 GuardSlide(Vector3 velocity,float dt)=>velocity;

        /// <summary>This frame's own move is a walk, the only move ice changes; an enemy's charge or rush is not (Enemy).</summary>
        protected virtual bool SlipsOnIce=>true;

        // ───── Knockback and wall hits ─────

        /// <summary>Never displaced by a knockback, a throw or a push: bosses (document 5.3) and the Spire (document 10.2).</summary>
        public virtual bool Immovable => false;

        /// <inheritdoc/>
        public void Push(Vector3 dir, float distance)
        {
            if (!IsAlive || Immovable) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
            // Same as knockback: initial speed = distance × damping, exponential decay pushes exactly distance; stacks shot by shot in a burst
            pushVel += dir.normalized * (distance * GM.rules.knockDamping);
        }

        /// <summary>Knockback by rigidbody impulse. Distance d maps to initial speed d × damping; exponential decay makes it slide exactly d.
        /// A knockback that body size scales to nothing changes nothing: no knocked state, so a wind-up is not cut short.</summary>
        public void Knockback(Vector3 dir, float distance, KnockKind kind)
        {
            if (!IsAlive || Immovable || State == CharacterState.Hooked || State == CharacterState.Dashing) return;
            // The Singularity's invincibility: nothing knocks the player back meanwhile, not even their own blast.
            if (Team == Team.Player && GM && GM.skills && GM.skills.SingularityActive) return;
            // Chain Blade Tame (document 5.8): unstoppable during the two-way pull, so nothing knocks the player off it.
            if (Team == Team.Player && GM && GM.runWeapons && GM.runWeapons.ReelGuarded) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
            dir.Normalize();
            if (kind == KnockKind.Hit) distance *= knockbackScale;
            // Spin-up Tame: a player braced against a wall at full spin slides only part of any knockback.
            if (GM && GM.runWeapons && this == (Combatant)GM.player && GM.runWeapons.Anchored) distance *= GM.runWeapons.upgrades.spinAnchorKnockFactor;
            if (distance <= 0f) return;
            wallSlams=0;
            knockVel = dir * (distance * GM.rules.knockDamping);
            Vector3 cur = Query.Flat(rb.linearVelocity);
            rb.AddForce(knockVel - cur, ForceMode.VelocityChange);
            knockKind = kind;
            SetState(kind == KnockKind.Throw ? CharacterState.Thrown : CharacterState.Knocked);
        }

        /// <summary>
        /// Hook throw: slides distance along dir like a knockback (body size does not shorten it). If it crashes into another character on the way,
        /// both of them and everyone around take damage that depends on the speed still left and on this body's size (see ThrowImpact).
        /// </summary>
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
            // A wall ends the throw first: no crash damage against a wall, only wall damage
            CheckWallHit(c);
            CheckThrowImpact(c);
        }

        /// <summary>
        /// A thrown character crashing into another one: it stops, and it, the one it hit and everyone within reach take crash damage and are pushed away.
        /// Only above the same speed threshold as a wall hit, and once per throw. The thrower's side (the player) is neither a trigger nor a victim.
        /// </summary>
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
                // Like every area hit, the crash ring does not reach bodies behind a wall.
                if (!self && Query.WallBetween(Query.AtCastHeight(center), Query.AtCastHeight(victim.Position), out _)) continue;
                Vector3 away = self ? Vector3.zero : Query.Flat(victim.Position - center);
                var info=DamageInfo.Attack(damage,impact.dealer,away,self?0:push,HitKind.Slam);
                info.element=impact.element;
                victim.ReceiveDamage(info);
            }
            // Hook Twist: the hook reels in the body this one crashed into (RunWeapons.ChainHook checks the rest).
            if (impact.chainHook != null && hit.IsAlive && GM && GM.runWeapons) GM.runWeapons.ChainHook(impact.chainHook, hit);
        }

        /// <summary>Unity's built-in Ignore Raycast layer, used for a wall that stops bodies but not shots or sight: the V2 base's footprint
        /// box (V2Campaign.Base.cs, 2026-11-10), whose shots stop at the model's own outline. Every cast for shots, area hits and sight uses
        /// GameManager.WallMask, which leaves this layer out; nothing else in the project uses the layer.</summary>
        public const int BodyOnlyWallLayer = 2;

        /// <summary>Hitting a wall during knockback: stop; wall damage only applies above the speed threshold, once per impact. A body-only
        /// wall counts as a wall here: a body knocked into the V2 base slams as it did before the base had two walls.</summary>
        void CheckWallHit(Collision c)
        {
            if (State != CharacterState.Knocked && State != CharacterState.Thrown) return;
            if (c.gameObject.layer != GameManager.WallLayer && c.gameObject.layer != BodyOnlyWallLayer) return;
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
                if(IsAlive&&damage&&++wallSlams<3&&rebound.magnitude>.3f)
                {knockVel=rebound;knockKind=KnockKind.Hit;SetState(CharacterState.Knocked);rb.linearVelocity=rebound*(Team==Team.Enemy?GM.WorldTimeScale:1);}
                return;
            }
        }

        /// <param name="away">Direction away from the wall: shards fly this way</param>
        void TakeWallDamage(Vector3 away)
        {
            if (!IsAlive) return;
            float damage=GM.rules.wallDamage;
            // Impact is a wave-1 bonus that grows with the same HP multiplier as the wall damage itself.
            if(Team==Team.Enemy)damage=(damage+(GM.progression?GM.progression.Stats.impact:0))*GM.waves.HpMultiplier;
            ReceiveDamage(new DamageInfo{amount=damage,knockDir=away,kind=HitKind.Slam,source=DamageSource.Wall});
        }

        // ───── Damage and death ─────

        /// <summary>Void Walk and the Singularity: the player takes no damage of any kind (attacks, burn, poison, wall)
        /// and is not killed outright; no hit lands, so nor does the knockback, stun or element stack it would carry.</summary>
        bool SkillInvincible => Team == Team.Player && GM && GM.skills && GM.skills.Invincible;

        public virtual bool ReceiveDamage(DamageInfo info)
        {
            if (!IsAlive || SkillInvincible) return false;
            // A body's own weapon hurting it is a backfire: it never grants or uses hit protection and pays no element payoffs,
            // otherwise standing in your own fire would make you immune to enemies.
            bool selfHit = info.dealer != null && info.dealer.Owner == this;
            // Rule R6, Q105: a starting form's backfire takes the starting-form damage factor like its hits (Strike 10 -> 5,
            // knife return 6.1 -> about 3); every other weapon's backfire stays its fixed base value.
            if (selfHit && info.weapon != null && Team == Team.Player && GM && GM.runWeapons) info.amount *= GM.runWeapons.StartingFormDamageFactor(info.weapon.form);
            // Design document 4.5: a human missile in flight is immune to knockback only (Knockback skips Dashing); attacks still hurt it.
            if (info.isAttack && Protected && !selfHit) return false;
            // Drain: the same attacks hit protection covers may be taken whole by the player's Energy Shield.
            if (info.isAttack && !selfHit && Team == Team.Player && GM && GM.runWeapons && GM.runWeapons.EnergyShieldBlocks()) return false;
            float beforeHp=Hp,beforeShield=elementStatus?elementStatus.Shield:0;
            bool crit=false;
            // A Fire hit's burn follows the hit as dealt, before a crit and this body's own modifiers.
            float baseDamage=info.amount;
            var runStats=GM&&GM.progression?GM.progression.Stats:null;
            if(runStats!=null&&info.isAttack&&Team==Team.Enemy&&info.source==DamageSource.Hit&&info.dealer?.Owner is Combatant{Team:Team.Player}
               &&runStats.critChance>0f&&UnityEngine.Random.value<runStats.critChance)
            {crit=true;info.amount*=runStats.CritMultiplier;}
            LastHitCritical=crit;
            // Document 9.5: an element this body is immune to deals none of its damage, or only the attacker's Damage Penetration share.
            float share=elementStatus?elementStatus.DamageFactor(info.element,info.dealer?.Owner):1f;
            float raw=info.amount*ElementCombat.DirectDamageFactor(this,info.element)*share;
            if(info.isAttack)raw*=ControlVulnerability(info);
            if(runStats!=null&&Team==Team.Player)raw*=runStats.DamageTakenMultiplier;
            // Charged Shot: an enemy attack landing while you charge hurts more, or less once tamed (ticks and walls do not).
            if(info.isAttack&&!selfHit&&Team==Team.Player&&info.dealer?.Owner is Enemy&&GM&&GM.runWeapons)raw*=GM.runWeapons.ChargeDamageTakenMultiplier;
            // Blizzard Tame: standing in your own snow cuts the damage you take.
            if(Team==Team.Player&&GM&&GM.runWeapons)raw*=GM.runWeapons.PlayerDamageTakenFactor;
            // The final rule floors accepted attacks and positive ticks before Shield; a zero non-attack tick, and a hit the body is
            // wholly immune to, stay zero.
            if(share>0f&&(info.isAttack||raw>0f))raw=Mathf.Max(1f,raw);
            float dealt=elementStatus?elementStatus.ResolveDamage(raw,info.source,info.ignoreShield,info.ignoreShieldReduction):raw;
            if(dealt>0f)dealt=Mathf.Max(1f,dealt); // Zero HP damage after full shield absorption must remain zero.
            Hp -= dealt;
            if (info.isAttack && raw>0f && protectionTime > 0f && !selfHit) protectTimer = protectionTime;
            bool tick=info.source==DamageSource.Burn||info.source==DamageSource.Poison;
            float hpDamage=Mathf.Min(beforeHp,Mathf.Max(0f,dealt));
            float shieldDamage=Mathf.Max(0f,beforeShield-(elementStatus?elementStatus.Shield:0));
            float actualDamage=hpDamage+shieldDamage;
            if(actualDamage>0f){LastDamagedBy=info.dealer?.Owner;LastDamageSource=info.source;DamageTaken?.Invoke(info,actualDamage);}
            if(!tick&&actualDamage>0f)
            {
                PlayHitFeedback(hpDamage,info.knockDir,info.kind,crit,info.feel,info.dealer?.Owner);
            }
            else if(tick&&hpDamage>0f)
                GM.hud?.DamageNumber(Position,hpDamage,info.source==DamageSource.Burn?new Color(1f,.48f,.2f):new Color(.6f,.9f,.35f),false);
            if(shieldDamage>0f)GM.hud?.DamageNumber(Position+Vector3.up*.45f,shieldDamage,Color.yellow,false);
            OnDamaged(dealt);
            if(!tick&&actualDamage>0&&GM)V3WorldAudio.Instance?.OnConfirmedHit(this,info.kind);
            if(info.isAttack&&actualDamage>0f)AttackReceived?.Invoke(info);
            if (Hp <= 0f)
            {
                Hp = 0f;
                Die(DeathCause.HpDepleted);
            }
            // Only bosses, the Stomper and the Rammer push the player; any other enemy's hit is feedback only (no push, no stun).
            else if (info.knockDistance > 0f && !(Team == Team.Player && info.dealer?.Owner is Enemy { PushesPlayer: false }))
            {
                float distance = info.knockDistance;
                // In the void trial a boss never pushes the player into an open hole; the platform edge no longer stops it.
                // The player's own recoil does not come through here.
                if (Team == Team.Player && info.dealer?.Owner is Enemy { IsBoss: true } && WorldMap.Active && WorldMap.Active.TrialActive)
                    distance = WorldMap.Active.LimitBossKnockback(Position, info.knockDir, distance * knockbackScale) / Mathf.Max(.0001f, knockbackScale);
                if (distance > 0f) Knockback(info.knockDir, distance, KnockKind.Hit);
            }
            // Self-hits add no stacks, no Light/Earth/Void payoff and no lifesteal.
            if(!tick&&!selfHit&&info.source==DamageSource.Hit&&info.dealer?.Owner is Combatant { Team: Team.Player } source)
                ElementCombat.ApplyHit(this,info.element,actualDamage,source,info.builtInElement,info.noLightningJump,info.continuousSource,baseDamage,info.weapon);
            return true;
        }

        /// <summary>
        /// Attack damage multiplier from control statuses: a frozen target (either team) takes frozenDamageTaken; an enemy hit by the
        /// player takes more for each Lightning stack it carries and the most while paralysed (ElementStatus.LightningDamageTaken).
        /// </summary>
        float ControlVulnerability(DamageInfo info)
        {
            if (!elementStatus || !elementStatus.Rules) return 1f;
            float multiplier = elementStatus.Frozen ? Mathf.Max(1f, elementStatus.Rules.frozenDamageTaken) : 1f;
            if (Team == Team.Enemy && info.dealer?.Owner is Combatant { Team: Team.Player }) multiplier *= elementStatus.LightningDamageTaken;
            return multiplier;
        }

        public void Heal(float amount){float gained=Mathf.Min(maxHp-Hp,amount);Hp+=gained;if(elementStatus&&gained>0)elementStatus.OnHeal();}

        /// <summary>Instant death (hit by the human missile; large enemies too). Plays one pierce hit effect scaled by remaining HP.
        /// by is the killer when there is one (the Shade's assassination), so the run-end report can name it.</summary>
        public void Kill(Combatant by = null)
        {
            if (!IsAlive || SkillInvincible || Invulnerable) return;
            LastDamagedBy = by; LastDamageSource = DamageSource.Hit;
            PlayHitFeedback(Mathf.Max(Hp, GM.feedback.heavyDamage), Vector3.zero, HitKind.Pierce);
            Hp = 0f;
            Die(DeathCause.HpDepleted);
        }

        protected void Die(DeathCause cause)
        {
            if (!IsAlive) return;
            if (Invulnerable) { Hp = Mathf.Max(Hp, 1f); return; }
            DeathVelocity=rb&&!rb.isKinematic?Query.Flat(rb.linearVelocity):Vector3.zero;
            if(DeathVelocity.sqrMagnitude<.01f)DeathVelocity=LastHitDirection*3f;
            Hp = Mathf.Max(0f, Hp);
            DeathCause = cause;
            SetState(CharacterState.Dead);
            if(GM&&Team==Team.Player)V3WorldAudio.Instance?.OnPlayerDeath(this);
            if(GM&&Team==Team.Enemy)SkillLoadout.NotifyEnemyDied(this);
            OnDied();
            ReportEliminated();
        }

        /// <summary>FallZone trigger: no fall while hooked; otherwise counts as falling into the gap immediately. A body that cannot
        /// die (enemy lab) is set back on safe ground instead.</summary>
        public void OnEnterFallZone(Vector3 outward)
        {
            if (!IsAlive || State == CharacterState.Hooked) return;
            if (Invulnerable && WorldMap.Active) { WorldMap.Active.SetBackOnSafeGround(this); return; }
            DeathCause = DeathCause.FellIntoGap;
            Vector3 v = State == CharacterState.Dashing || rb.isKinematic
                ? Vector3.zero
                : Query.Flat(rb.linearVelocity);
            SetState(CharacterState.Falling);
            if(GM&&Team==Team.Player)V3WorldAudio.Instance?.OnPlayerDeath(this);
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

        /// <summary>Scripted moments (the black-hole cutscene) end a fall immediately instead of waiting the two seconds.</summary>
        public void FinishFallNow()
        {
            if (State == CharacterState.Falling) OnFallFinished();
        }

        public void SetMovePosition(Vector3 p) => rb.MovePosition(p);

    }
}
