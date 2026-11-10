using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What the player's attacks hit on the campaign's black hole (2026-11-10, its timed damage check, V2Campaign.Hole.cs). An enemy-side
    /// body at the hole's centre, as wide as its kill core, so every kind of player attack finds it the way it finds a monster: shots
    /// meet its collider on the Character layer (their sphere casts), blasts, cones and auras read Combatant.All with its radius, and
    /// burn and poison tick on its own ElementStatus. Otherwise it stays out of the world: its body and collider touch nothing
    /// (excludeLayers everything), it never moves (Immovable; FixedUpdate sets it back on the hole), never dies (Invulnerable, its HP
    /// refilled after every hit) and is in no wave list, so no hole swallows it, no wave counts it and no monster tends it. Enemy shots
    /// pass it (they hit the player's side only). A hook that catches it lets go at once: the body leaves play for HookRestSeconds, so
    /// the hook never reels the player in toward the hole. The campaign reads DamageTaken and shows it only while the check can count.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackHoleTarget : Combatant
    {
        // Long enough for a hook that caught it to see it gone and let go (its next ticks), short enough to miss no real attack.
        const float HookRestSeconds = .25f;
        // The collider reaches this far past the kill core, so a shot meets it before the hole swallows the shot (WorldMap.FixedUpdate).
        const float ShotMargin = .3f;
        // As much HP as no attack can take in one hit; it is refilled after each.
        const float BodyHp = 1e7f;
        // Height of the collider, centred on Query.CastHeight where every shot and sweep flies.
        const float ColliderHeight = 3f;

        /// <summary>Game time before which the body stays out of play (a hook caught it).</summary>
        public float RestUntil { get; private set; }

        /// <inheritdoc/>
        public override Team Team => Team.Enemy;

        /// <inheritdoc/>
        public override Vector3 AimDirection => Vector3.forward;

        /// <inheritdoc/>
        public override bool Immovable => true;

        /// <summary>A body for the hole under hole (at its centre), bodyRadius wide, burning and poisoning by statusRules (the player's,
        /// shared by every status board). It starts out of play; Show brings it in.</summary>
        public static BlackHoleTarget Create(Transform hole, float bodyRadius, StatusRules statusRules)
        {
            var go = new GameObject("BlackHoleTarget");
            // Inactive while its parts go on, so Awake (Combatant's and the status board's) finds them all in place.
            go.SetActive(false);
            go.layer = GameManager.CharacterLayer;
            go.transform.SetParent(hole, false);
            var status = go.AddComponent<ElementStatus>();
            status.Rules = statusRules;
            var target = go.AddComponent<BlackHoleTarget>(); // its Rigidbody and CapsuleCollider come with it (Combatant requires them)
            target.radius = Mathf.Max(.5f, bodyRadius);
            target.maxHp = BodyHp;
            target.Invulnerable = true;
            var body = go.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.excludeLayers = ~0;
            var capsule = go.GetComponent<CapsuleCollider>();
            capsule.radius = target.radius + ShotMargin;
            capsule.height = Mathf.Max(ColliderHeight, capsule.radius * 2f);
            capsule.center = new Vector3(0f, Query.CastHeight, 0f);
            capsule.excludeLayers = ~0;
            return target;
        }

        /// <summary>In play (on) or out of it; it stays out while a hook's catch rests it.</summary>
        public void Show(bool on)
        {
            on &= Time.time >= RestUntil;
            if (gameObject.activeSelf == on) return;
            if (on) HoldStill(); // back in play as a plain body on the hole, whatever caught it before
            gameObject.SetActive(on);
        }

        /// <inheritdoc/>
        protected override void TickNormal(float dt)
        {
        }

        // Why not the base step: it walks, slides, falls and turns a body; this one only stays on the hole.
        /// <inheritdoc/>
        protected override void FixedUpdate() => HoldStill();

        /// <inheritdoc/>
        protected override void OnStateChanged(CharacterState from, CharacterState to)
        {
            if (to != CharacterState.Hooked) return;
            // Out of play for a moment: the hook sees its catch gone and lets go before it reels anyone (AttackActor.TickHook).
            RestUntil = Time.time + HookRestSeconds;
            gameObject.SetActive(false);
        }

        /// <inheritdoc/>
        protected override void OnDamaged(float amount) => Hp = maxHp;

        void HoldStill()
        {
            if (State != CharacterState.Normal) SetState(CharacterState.Normal);
            if (!rb) return;
            if (!rb.isKinematic) rb.isKinematic = true;
            transform.localPosition = Vector3.zero;
            rb.position = transform.position;
        }
    }
}
