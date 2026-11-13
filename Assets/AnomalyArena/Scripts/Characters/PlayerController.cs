using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>Player: the move keys and the sprint key (ArenaKeys: W A S D and Shift unless changed in the settings; sprinting uses stamina), mouse to aim, left button to use the hand weapon, right button to switch hand slots.
    /// Once the void trial is won, Space (read in GameManagerFlow.cs, the one script that reads it) is the void dash toward the cursor,
    /// paid from the same stamina (TryVoidDash).</summary>
    public class PlayerController : Combatant
    {
        [Header("Sprint")] public Stamina stamina = new Stamina();
        [Header("Void dash")] public VoidDash voidDash = new VoidDash();

        // Interval between dust puffs while sprinting (seconds)
        const float SprintDustInterval = 0.07f;

        Vector3 aim = Vector3.forward;

        // The cursor's ground point. Until the mouse (or a test) has set one, the aim point is derived from the aim direction
        Vector3 aimPoint;
        bool hasAimPoint;
        Vector2 moveInput;
        // True when moveInput came from the keys, which follow the screen (ScreenAxes); debugMoveHeld stays on the map's axes.
        bool moveFollowsScreen;
        float sprintMultiplier = 1f;
        float dustTimer;

        /// <summary>For automated tests: replaces the keyboard, forcing the sprint key held / released.</summary>
        [System.NonSerialized] public bool? debugSprintHeld;

        /// <summary>For automated tests: replaces the WASD keys, holding this move direction (x along the map's x, y along the map's z,
        /// whichever way the camera is turned) until set back to null.</summary>
        [System.NonSerialized] public Vector2? debugMoveHeld;

        /// <summary>For automated tests: locks the aim direction so it no longer follows the mouse.</summary>
        [System.NonSerialized] public bool debugLockAim;

        public override Team Team => Team.Player;
        public override Vector3 AimDirection => aim;
        public override Vector3 AimPoint => hasAimPoint ? aimPoint : base.AimPoint;

        /// <summary>Which of the player's own inputs work right now; null lets every one through. The V2 campaign's forced tutorial
        /// steps (V2Campaign.Tutorial.cs) set it so that only the input being taught works.</summary>
        public static System.Func<PlayerAct, bool> InputGate;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInputGate() => InputGate = null;

        static bool Lets(PlayerAct act) => InputGate == null || InputGate(act);

        void Update()
        {
            if (!IsAlive || GM == null) return;
            UpdateAim();

            bool playing = GM.CanPlayerMove;
            bool walking = playing && Lets(PlayerAct.Move);
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            moveInput = Vector2.zero;
            moveFollowsScreen = false;
            if (walking && kb != null)
            {
                if (ArenaKeys.Held(GameAction.MoveUp)) moveInput.y += 1f;
                if (ArenaKeys.Held(GameAction.MoveDown)) moveInput.y -= 1f;
                if (ArenaKeys.Held(GameAction.MoveRight)) moveInput.x += 1f;
                if (ArenaKeys.Held(GameAction.MoveLeft)) moveInput.x -= 1f;
                if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
                moveFollowsScreen = true;
            }
            if (walking && debugMoveHeld.HasValue) { moveInput = Vector2.ClampMagnitude(debugMoveHeld.Value, 1f); moveFollowsScreen = false; }

            TickSprint(playing && Lets(PlayerAct.Sprint), kb);
            if (!playing || mouse == null || !GM.CanUseCombatInput) return;
            var inventory = GM.runWeapons;
            if (ActionsBlocked) { inventory.HandleInput(false, false, true); return; }
            if (mouse.rightButton.wasPressedThisFrame && Lets(PlayerAct.Switch)) inventory.Switch();
            // No unarmed attack: an empty hand slot does nothing. Held back by the gate, the weapon sees the button up (a "release" here
            // would fire a charged shot).
            if (inventory.Active == null) return;
            if (Lets(PlayerAct.Fire)) inventory.HandleInput(mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed, mouse.leftButton.wasReleasedThisFrame);
            else inventory.HandleInput(false, false, false);
        }

        /// <summary>Sprint: while Shift is held and the player is moving, speed ×2 and stamina drains; it recovers when not sprinting.</summary>
        void TickSprint(bool playing, Keyboard kb)
        {
            bool held = debugSprintHeld ?? ArenaKeys.Held(GameAction.Sprint);
            bool moving = moveInput.sqrMagnitude > 0.01f || debugSprintHeld == true;
            bool want = playing && held && moving && State == CharacterState.Normal && !WalkHeld;
            float recovery = GM.runWeapons ? GM.runWeapons.StaminaRecoveryMultiplier : 1f;
            sprintMultiplier = stamina.Tick(Time.deltaTime, want, recovery);
            if (!stamina.Sprinting) return;
            dustTimer -= Time.deltaTime;
            if (dustTimer > 0f) return;
            dustTimer = SprintDustInterval;
            Fx.Pop(new Vector3(Position.x, 0.2f, Position.z), new Color(0.85f, 0.85f, 0.8f), 0.9f);
        }

        /// <summary>The void dash (VoidDash) toward the cursor's point on the ground. GameManager asks only once the dash is unlocked,
        /// while the player can move and no screen is open (GameManagerFlow.cs). Refused, with nothing spent: stunned, frozen or
        /// paralysed, hooked, in a rocket flight, held still, while the cooldown runs, on an exhausted stamina bar, or with a wall right
        /// in front. A knockback or a throw in progress is cancelled, as Blink cancels it; the body moves as Blink moves it
        /// (SkillWorldAPI.Teleport) and shows Blink's effect and sound.</summary>
        public bool TryVoidDash()
        {
            if (!IsAlive || GM == null || !Lets(PlayerAct.Move) || !Lets(PlayerAct.Sprint)) return false;
            if (State is not (CharacterState.Normal or CharacterState.Knocked or CharacterState.Thrown) || ActionsBlocked || WalkHeld) return false;
            if (voidDash.CoolingDown || stamina.Exhausted || stamina.Value <= 0f) return false;
            Vector3 from = Position, toward = Query.Flat(AimPoint - from);
            // The cursor right on the player gives no way to go: the way the player last aimed then.
            Vector3 direction = toward.sqrMagnitude > .01f ? toward.normalized : Query.Flat(aim).normalized;
            if (direction.sqrMagnitude < .5f) return false;
            Vector3 to = VoidDash.Reach(WorldMap.Active, from, direction, voidDash.distance, Radius);
            to.y = from.y;
            if (Query.Flat(to - from).magnitude < VoidDash.LeastMove) return false;
            // Restore sets the bar to what is left and marks an empty one exhausted, the sprint's rule for a bar run empty.
            stamina.Restore(voidDash.StaminaAfter(stamina.Value));
            voidDash.Started();
            CancelDisplacement();
            if (SkillWorldAPI.Teleport != null) SkillWorldAPI.Teleport(this, to);
            else
            {
                rb.position = to;
                rb.linearVelocity = Vector3.zero;
                transform.position = to;
            }
            ShowVoidDash(from, to);
            return true;
        }

        /// <summary>Blink's look and sound for the dash: with the art on, the streak from the start to the end; in whitebox, the ring at
        /// the end in Blink's colour.</summary>
        void ShowVoidDash(Vector3 from, Vector3 to)
        {
            if (V5Vfx.On) V5Vfx.I.Blink(from, to);
            else
            {
                var blink = GM.skills && GM.skills.Catalog ? GM.skills.Catalog.Find(SkillId.Blink) : null;
                Fx.Ring(to, .4f, blink ? ElementCombat.ColorFor(blink.element) : Color.white, .35f);
            }
            V2Audio.Instance?.PlayWorld(ArenaSoundCue.Pulse, to);
        }

        void UpdateAim()
        {
            var mouse = Mouse.current;
            var cam = GM.cam;
            if (debugLockAim || mouse == null || cam == null) return;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (V5Vfx.On && cam.TryGetComponent<CameraRig>(out var rig)) ray.origin -= rig.Transient;
            var plane = new Plane(Vector3.up, new Vector3(0f, Query.CastHeight, 0f));
            if (!plane.Raycast(ray, out float t)) return;
            aimPoint = Query.Flat(ray.GetPoint(t));
            hasAimPoint = true;
            Vector3 d = Query.Flat(aimPoint - Position);
            if (d.sqrMagnitude <= 0.01f) return;
            // Spin-up inertia: a fully spun minigun limits how fast the aim can turn.
            float turnLimit = GM.runWeapons ? GM.runWeapons.AimTurnRateLimit : 0f;
            aim = turnLimit > 0f ? Vector3.RotateTowards(aim, d.normalized, turnLimit * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized : d.normalized;
        }

        public void DebugSetAim(Vector3 dir)
        {
            debugLockAim = true;
            hasAimPoint = false;
            aim = Query.Flat(dir).normalized;
        }

        /// <summary>For automated tests: locks the aim onto a ground point, as if the cursor rested there.</summary>
        public void DebugSetAimPoint(Vector3 point)
        {
            debugLockAim = true;
            aimPoint = Query.Flat(point);
            hasAimPoint = true;
            Vector3 d = Query.Flat(aimPoint - Position);
            if (d.sqrMagnitude > 0.01f) aim = d.normalized;
        }

        /// <summary>Walking is held still: the hook or a skill locks it, or the player has just come out of a void-trial portal
        /// (WorldMap.PortalArrivalHold), so a key still held from the way in does not walk them on.</summary>
        bool WalkHeld => MovementLocked || WorldMap.Active && WorldMap.Active.PortalArrivalHold;

        /// <summary>WASD follow the screen: a camera view turned round the vertical (CameraRig.MoveYaw, the 2.5D views of 2026-11-06)
        /// turns them with it, so W always walks up the screen; an unturned view keeps the map's axes.</summary>
        Vector3 ScreenAxes(Vector3 move)
        {
            float yaw = GM.cam && GM.cam.TryGetComponent<CameraRig>(out var rig) ? rig.MoveYaw : 0f;
            return yaw == 0f ? move : Quaternion.Euler(0f, yaw, 0f) * move;
        }

        protected override void TickNormal(float dt)
        {
            Vector3 v = Vector3.zero;
            Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);
            if (moveFollowsScreen) move = ScreenAxes(move);
            if (!WalkHeld && GM.State == GameState.Playing)
                v = move * (moveSpeed * sprintMultiplier * (GM.runWeapons ? GM.runWeapons.MoveMultiplier : 1f)
                    * (ElementStatus.For(this)?.PlayerChillMultiplier ?? 1f) * (GM.skills ? GM.skills.PlayerMoveMultiplier : 1f));
            v *= Quicksand.SpeedFactor(Position); // Dunes quicksand (system doc 12.1) slows walking and sprinting alike.
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        /// <summary>V2 day campaign (since 2026-11-10, Central Control's shield): what is left of a hit on the player once the base's shield
        /// has taken its share near the base. Null outside a running campaign (the whole hit).</summary>
        public static System.Func<PlayerController, float, float> HitShare;

        public override bool ReceiveDamage(DamageInfo info)
        {
            // Only a hit that would land is shared: never the player's own backfire, never one that hit protection or a skill's
            // invincibility stops anyway, so the base's shield is not spent on nothing.
            if (HitShare != null && IsAlive && info.amount > 0f && !(info.dealer != null && info.dealer.Owner == this)
                && !(info.isAttack && Protected) && !(GM && GM.skills && GM.skills.Invincible))
                info.amount = HitShare(this, info.amount);
            return base.ReceiveDamage(info);
        }

        protected override void OnDamaged(float amount) => GM.hud.FlashDamage();

        protected override void OnDied()
        {
            if (bodyRenderer) bodyRenderer.transform.localScale = new Vector3(1.3f, 0.2f, 1.3f);
        }
    }
}
