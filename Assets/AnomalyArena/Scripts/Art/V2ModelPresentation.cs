using UnityEngine;

namespace AnomalyArena
{

    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Combatant))]
    public sealed class V2ModelPresentation : MonoBehaviour
    {
        [Header("Saved model and clips")]
        public Transform modelRoot;
        public Animator animator;
        public AnimationClip idleClip, runClip, attackClip;
        public Renderer[] modelRenderers;
        [Header("Candidate visual tuning — does not change combat")]
        [Tooltip("Source forward correction in degrees; native facing has not been play verified.")]
        public Vector3 rotationOffsetEuler;
        [Min(.01f)] public float runReferenceSpeed = 5.4f;
        [Min(0f)] public float movementThreshold = .12f;
        [Min(0f)] public float blendSeconds = .08f;
        [Range(.05f, .9f), Tooltip("Candidate contact frame in the supplied attack; enemy release aligns to the real attack sequence.")]
        public float attackContactNormalized = .35f;
        [Min(.01f)] public float minimumAttackVisualInterval = .16f;
        public bool playerFacesAim = true;
        public bool scaleWithBodyRadius = true;
        [Min(.01f)] public float referenceBodyRadius = .5f;
        public Vector3 authoredScale = Vector3.one;
        [Min(0f), Tooltip("V4 visual-only enlargement; colliders and hit radii stay unchanged. 0 uses the default: player 1.3, enemies 1.35, bosses 1.1.")]
        public float presentationScale;
        public Color critFlashEmission = new Color(1.3f, .85f, .3f);
        public Vector3 authoredPosition;
        public Color flashEmission = new Color(.7f, .65f, .4f);
        public Color windupEmission = new Color(.18f, .12f, .025f);
        [Range(0f, .4f)] public float hitSquash = .12f;
        [Tooltip("No Death clip was purchased. The saved model is paused and laid onto the ground.")]
        public Vector3 corpseRotationEuler = new Vector3(90f, 0f, 0f);
        [Min(0f)] public float corpseGroundClearance = .025f;

        Combatant body;
        Enemy enemy;
        Rigidbody motion;
        MaterialPropertyBlock block;
        int spawnVersion, attackSequence, requestedAttack, consumedAttack, stateHash;
        float attackRemaining, sinceAttack = 100f, windupLength;
        bool winding, corpse, modeInitialized, lastMode;
        static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
        static readonly int AttackHash = Animator.StringToHash("Base Layer.Attack");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public bool HasAssignedModel => modelRoot && animator && animator.runtimeAnimatorController && idleClip && runClip && attackClip && modelRenderers != null && modelRenderers.Length > 0;
        public bool UsingModel => HasAssignedModel && Art.Enabled && (!GameManager.Instance || GameManager.Instance.useV2);

        void Awake()
        {
            body = GetComponent<Combatant>(); enemy = body as Enemy;
            motion = GetComponent<Rigidbody>(); block = new MaterialPropertyBlock();
            if (animator) animator.applyRootMotion = false;
        }
        void OnEnable() { modeInitialized = false; stateHash = 0; winding = corpse = false; }
        void OnDisable() { if (animator) animator.speed = 0f; }

        public void MarkAttack() => requestedAttack++;

        void ResetPose()
        {
            spawnVersion = body.SpawnVersion;
            attackSequence = enemy ? enemy.AttackSequence : 0;
            consumedAttack = requestedAttack;
            attackRemaining = 0f; sinceAttack = 100f; winding = corpse = false;
            modelRoot.localPosition = authoredPosition; modelRoot.localScale = authoredScale;
            animator.Rebind(); animator.Play(IdleHash, 0, 0f); animator.Update(0f);
            stateHash = IdleHash;
        }

        void LateUpdate()
        {
            if (!body || !HasAssignedModel) return;
            bool visible = UsingModel;
            if (!modeInitialized || visible != lastMode)
            {
                modeInitialized = true; lastMode = visible;
                animator.enabled = visible;
                if (visible) ResetPose();
            }
            bool cloaked = enemy && enemy.IsAlive && enemy.Cloaked;
            foreach (var renderer in modelRenderers)
                if (renderer && renderer.enabled != (visible && !cloaked)) renderer.enabled = visible && !cloaked;
            if (!visible) return;
            if (spawnVersion != body.SpawnVersion) ResetPose();

            var gm = GameManager.Instance;
            float dt = gm ? body.Team == Team.Player ? gm.PlayerDeltaTime : gm.WorldDeltaTime : Time.deltaTime;
            float clockScale = enemy && gm ? gm.WorldTimeScale : 1f;
            var status = ElementStatus.For(body);
            bool frozen = body.ActionsBlocked || status && status.Frozen;
            if (body.State == CharacterState.Dead)
            {
                if (!corpse)
                {
                    corpse = true; animator.Play(IdleHash, 0, 0f); animator.Update(0f);
                    modelRoot.localRotation *= Quaternion.Euler(corpseRotationEuler);
                    float bottom = float.PositiveInfinity;
                    foreach (var renderer in modelRenderers) if (renderer) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                    if (!float.IsPositiveInfinity(bottom))
                        modelRoot.position += Vector3.up * Mathf.Max(0f, body.Position.y + corpseGroundClearance - bottom);
                }
                animator.speed = 0f;
                ApplyHitFeedback();
                return;
            }

            frozen |= body.HitPaused;
            animator.speed = frozen ? 0f : clockScale;
            if (dt <= 0f || frozen)
            {
                float held = (scaleWithBodyRadius ? body.radius / Mathf.Max(.01f, referenceBodyRadius) : 1f) * PresentationScale;
                float heldSquash = body.HitSquash01 * hitSquash;
                modelRoot.localScale = Vector3.Scale(authoredScale * held, new Vector3(1f + heldSquash, 1f - heldSquash, 1f + heldSquash));
                ApplyHitFeedback(); return;
            }
            sinceAttack += dt;
            attackRemaining = Mathf.Max(0f, attackRemaining - dt);

            if (enemy)
            {
                if (enemy.AttackSequence != attackSequence)
                {
                    attackSequence = enemy.AttackSequence;
                    BeginAttack(attackContactNormalized);
                    winding = false;
                }
                else if (enemy.WindingUp)
                {
                    if (!winding)
                    {
                        winding = true;
                        windupLength = Mathf.Max(.01f, enemy.WindupSecondsRemaining);
                    }
                    float anticipation = 1f - Mathf.Clamp01(enemy.WindupSecondsRemaining / windupLength);

                    animator.Play(AttackHash, 0, anticipation * attackContactNormalized);
                    animator.Update(0f); stateHash = AttackHash;
                }
                else if (winding)
                {
                    winding = false; attackRemaining = 0f; stateHash = 0;
                }
            }
            else if (requestedAttack != consumedAttack)
            {
                consumedAttack = requestedAttack;
                if (attackRemaining <= 0f && sinceAttack >= minimumAttackVisualInterval) BeginAttack(0f);
            }

            Vector3 velocity = motion ? Query.Flat(motion.linearVelocity) : Vector3.zero;
            bool walking = body.State == CharacterState.Normal && velocity.sqrMagnitude > movementThreshold * movementThreshold;
            bool attacking = winding || attackRemaining > 0f;
            Vector3 facing = attacking || !enemy && playerFacesAim ? Query.Flat(body.AimDirection) : walking ? velocity : Query.Flat(body.AimDirection);
            if (facing.sqrMagnitude > .0001f)
                modelRoot.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up) * Quaternion.Euler(rotationOffsetEuler);
            float scale = (scaleWithBodyRadius ? body.radius / Mathf.Max(.01f, referenceBodyRadius) : 1f) * PresentationScale;
            float squash = body.HitSquash01 * hitSquash;
            modelRoot.localScale = Vector3.Scale(authoredScale * scale, new Vector3(1f + squash, 1f - squash, 1f + squash));
            if (!attacking)
            {
                SwitchState(walking ? RunHash : IdleHash);

                animator.speed = walking ? velocity.magnitude / Mathf.Max(.01f, runReferenceSpeed) : clockScale;
            }
            ApplyHitFeedback();
        }

        void BeginAttack(float startNormalized)
        {
            animator.CrossFadeInFixedTime(AttackHash, blendSeconds, 0, attackClip.length * startNormalized);
            stateHash = AttackHash; sinceAttack = 0f;
            attackRemaining = attackClip.length * (1f - startNormalized);
        }
        void SwitchState(int next)
        {
            if (stateHash == next) return;
            animator.CrossFadeInFixedTime(next, blendSeconds, 0, 0f); stateHash = next;
        }
        float PresentationScale => presentationScale > 0f ? presentationScale : enemy ? (enemy.IsBoss ? 1.1f : 1.35f) : 1.3f;
        void ApplyHitFeedback()
        {
            Color glow = body.IsAlive ? (body.LastHitCritical ? critFlashEmission : flashEmission) * body.HitFlash01 + (enemy && enemy.WindingUp ? windupEmission : Color.black) + StatusGlow() : Color.black;
            foreach (var renderer in modelRenderers)
            {
                if (!renderer) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, glow);
                renderer.SetPropertyBlock(block);
            }
        }

        Color StatusGlow()
        {
            var status = ElementStatus.For(body);
            if (!status) return Color.black;
            Color glow = Color.black;
            float flicker = .65f + .35f * Mathf.PerlinNoise(Time.time * 9f, (GetHashCode() & 1023) * .013f);
            if (status.Frozen) glow += new Color(.16f, .5f, .7f);
            else if (status.Frost > .05f) glow += new Color(.06f, .2f, .3f) * status.Chill01;
            if (status.Fire > .3f) glow += new Color(.5f, .16f, .02f) * flicker;
            if (status.Poison > .5f) glow += new Color(.14f, .32f, .04f);
            if (status.Twitching) glow += new Color(.8f, .68f, .16f);
            else if (status.Paralysed) glow += new Color(.22f, .19f, .04f) * flicker;
            return glow;
        }
    }
}
