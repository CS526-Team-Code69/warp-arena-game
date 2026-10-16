using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{

    public class PlayerController : Combatant
    {
        [Header("Sprint")] public Stamina stamina = new Stamina();

        const float SprintDustInterval = 0.07f;

        Vector3 aim = Vector3.forward;

        Vector3 aimPoint;
        bool hasAimPoint;
        Vector2 moveInput;
        float sprintMultiplier = 1f;
        float dustTimer;

        [System.NonSerialized] public bool? debugSprintHeld;

        [System.NonSerialized] public bool debugLockAim;

        public override Team Team => Team.Player;
        public override Vector3 AimDirection => aim;
        public override Vector3 AimPoint => hasAimPoint ? aimPoint : base.AimPoint;

        void Update()
        {
            if (!IsAlive || GM == null) return;
            UpdateAim();

            bool playing = GM.CanPlayerMove;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            moveInput = Vector2.zero;
            if (playing && kb != null)
            {
                if (kb.wKey.isPressed) moveInput.y += 1f;
                if (kb.sKey.isPressed) moveInput.y -= 1f;
                if (kb.dKey.isPressed) moveInput.x += 1f;
                if (kb.aKey.isPressed) moveInput.x -= 1f;
                if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
            }

            TickSprint(playing, kb);
            if (!playing || mouse == null || !GM.CanUseCombatInput) return;
            var inventory = GM.runWeapons;
            if (ActionsBlocked) { inventory.HandleInput(false, false, true); return; }
            if (mouse.rightButton.wasPressedThisFrame) inventory.Switch();

            if (inventory.Active != null) inventory.HandleInput(mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed, mouse.leftButton.wasReleasedThisFrame);
        }

        void TickSprint(bool playing, Keyboard kb)
        {
            bool held = debugSprintHeld ?? (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed));
            bool moving = moveInput.sqrMagnitude > 0.01f || debugSprintHeld == true;
            bool want = playing && held && moving && State == CharacterState.Normal && !MovementLocked;
            float recovery = GM.runWeapons ? GM.runWeapons.StaminaRecoveryMultiplier : 1f;
            sprintMultiplier = stamina.Tick(Time.deltaTime, want, recovery);
            if (!stamina.Sprinting) return;
            dustTimer -= Time.deltaTime;
            if (dustTimer > 0f) return;
            dustTimer = SprintDustInterval;
            Fx.Pop(new Vector3(Position.x, 0.2f, Position.z), new Color(0.85f, 0.85f, 0.8f), 0.9f);
        }

        void UpdateAim()
        {
            var mouse = Mouse.current;
            var cam = GM.cam;
            if (debugLockAim || mouse == null || cam == null) return;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, Query.CastHeight, 0f));
            if (!plane.Raycast(ray, out float t)) return;
            aimPoint = Query.Flat(ray.GetPoint(t));
            hasAimPoint = true;
            Vector3 d = Query.Flat(aimPoint - Position);
            if (d.sqrMagnitude <= 0.01f) return;

            float turnLimit = GM.runWeapons ? GM.runWeapons.AimTurnRateLimit : 0f;
            aim = turnLimit > 0f ? Vector3.RotateTowards(aim, d.normalized, turnLimit * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized : d.normalized;
        }

        public void DebugSetAim(Vector3 dir)
        {
            debugLockAim = true;
            hasAimPoint = false;
            aim = Query.Flat(dir).normalized;
        }

        public void DebugSetAimPoint(Vector3 point)
        {
            debugLockAim = true;
            aimPoint = Query.Flat(point);
            hasAimPoint = true;
            Vector3 d = Query.Flat(aimPoint - Position);
            if (d.sqrMagnitude > 0.01f) aim = d.normalized;
        }

        protected override void TickNormal(float dt)
        {
            Vector3 v = Vector3.zero;
            if (!MovementLocked && GM.State == GameState.Playing)
                v = new Vector3(moveInput.x, 0f, moveInput.y) * (moveSpeed * sprintMultiplier * (GM.runWeapons ? GM.runWeapons.MoveMultiplier : 1f)
                    * (ElementStatus.For(this)?.PlayerChillMultiplier ?? 1f));
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        protected override void OnDamaged(float amount) => GM.hud.FlashDamage();

        protected override void OnDied()
        {
            if (bodyRenderer) bodyRenderer.transform.localScale = new Vector3(1.3f, 0.2f, 1.3f);
        }
    }
}
