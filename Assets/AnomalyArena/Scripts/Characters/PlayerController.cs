using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    public class PlayerController : Combatant
    {
        [Header("Punch (unarmed basic attack)")]
        public float punchDamage = 3f;

        [Tooltip("Measured from the body edge")]
        public float punchRange = 1.2f;

        public float punchArc = 90f;
        public float punchKnockback = 3f;
        public float punchCooldown = 0.4f;

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

        public Weapon NearPickup { get; private set; }
        public override Team Team => Team.Player;
        public override Vector3 AimDirection => aim;
        public override Vector3 AimPoint => hasAimPoint ? aimPoint : base.AimPoint;
        protected override Texture2D ArtTexture => Art.Set ? Art.Set.player : null;
        protected override float ArtHeight => Art.Set.playerHeight;

        void Update()
        {
            if (!IsAlive || GM == null) return;
            UpdateAim();

            bool playing = GM.useV2 ? GM.CanPlayerMove : GM.State == GameState.Playing;
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
            if (!playing || mouse == null) return;
            if(GM.useV2)
            {
                if(!GM.CanUseCombatInput)return;
                var inventory=GM.runWeapons;
                if(ActionsBlocked){inventory.HandleInput(false,false,true);return;}
                if(mouse.rightButton.wasPressedThisFrame)inventory.Switch();
                if(inventory.Active==null){if(mouse.leftButton.wasPressedThisFrame)Punch();}
                else inventory.HandleInput(mouse.leftButton.wasPressedThisFrame,mouse.leftButton.isPressed,mouse.leftButton.wasReleasedThisFrame);
                return;
            }

            HandlePickup(mouse.rightButton.wasPressedThisFrame);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (Weapon == null) Punch();
                else TryUseWeapon();
            }

            else if (mouse.leftButton.isPressed && Weapon != null && Weapon.Effect && Weapon.Effect.holdToRepeat &&
                     Weapon.Revealed)
            {
                TryUseWeapon();
            }

            if (mouse.leftButton.wasReleasedThisFrame) ReleaseWeapon();
        }


        void TickSprint(bool playing, Keyboard kb)
        {
            bool held = debugSprintHeld ?? (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed));
            bool moving = moveInput.sqrMagnitude > 0.01f || debugSprintHeld == true;
            bool want = playing && held && moving && State == CharacterState.Normal && !MovementLocked;
            float recovery = GM.useV2 && GM.runWeapons ? GM.runWeapons.StaminaRecoveryMultiplier : 1f;
            sprintMultiplier = stamina.Tick(Time.deltaTime, want, recovery);
            if (!stamina.Sprinting) return;
            dustTimer -= Time.deltaTime;
            if (dustTimer > 0f) return;
            dustTimer = SprintDustInterval;
            var feet = new Vector3(Position.x, 0.2f, Position.z);
            var dust = Art.On ? Art.Set.dust : null;
            if (dust) Fx.SpriteBillboard(dust, feet, 0.4f, 1.1f, 0.4f);
            else Fx.Pop(feet, new Color(0.85f, 0.85f, 0.8f), 0.9f);
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
            if (d.sqrMagnitude > 0.01f) aim = d.normalized;
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
                v = new Vector3(moveInput.x, 0f, moveInput.y) * (moveSpeed * SpeedMultiplier * sprintMultiplier * (GM.useV2&&GM.runWeapons?GM.runWeapons.MoveMultiplier:1));
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }


        void Punch()
        {
            if (ActionsBlocked || State != CharacterState.Normal || weaponCooldown > 0f) return;
            var stats = GM.useV2 && GM.progression ? GM.progression.Stats : null;
            weaponCooldown = punchCooldown * (stats?.CooldownMultiplier ?? 1f) / (GM.useV2 && GM.skills ? GM.skills.AttackSpeedMultiplier : 1f);
            float reach = radius + punchRange;
            Fx.Sector(Position, aim, reach, punchArc, new Color(1f, 1f, 1f, 0.5f));
            var c = Query.MeleeTarget(this, Position, aim, reach, punchArc);
            if (c == null) return;
            Vector3 to = Query.Flat(c.Position - Position);
            Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : aim;
            float damage = punchDamage * (stats?.DamageMultiplier ?? 1f);
            float knock = punchKnockback * (stats?.KnockbackMultiplier ?? 1f) * (GM.useV2 && GM.skills ? GM.skills.KnockbackMultiplier : 1f);
            c.ReceiveDamage(DamageInfo.Attack(damage, this, dir, knock));
        }

        void HandlePickup(bool rightClick)
        {
            var spawner = GM.weapons;
            NearPickup = spawner.FindNear(Position);
            if (NearPickup == null) return;

            if (Weapon == null)
            {
                if (State == CharacterState.Normal) PickUp(NearPickup);
                return;
            }

            if (!rightClick || State != CharacterState.Normal) return;
            if (Weapon.Active != null && !Weapon.Active.CanSwap) return;

            var target = NearPickup;

            if (Weapon.Active != null) Weapon.Active.OnSwapAway();
            if (Weapon != null)
            {
                var old = Unequip();
                spawner.Drop(old, Position);
            }

            PickUp(target);
        }

        void PickUp(Weapon w)
        {
            GM.weapons.Take(w);
            Equip(w);
            NearPickup = null;
        }

        protected override void OnWeaponRevealed(Weapon w) => GM.hud.Reveal(w);

        protected override void OnDamaged(float amount) => GM.hud.FlashDamage();

        protected override void OnDied()
        {
            if (bodyRenderer) bodyRenderer.transform.localScale = new Vector3(1.3f, 0.2f, 1.3f);
            if (artPivot) artPivot.localScale = new Vector3(1.3f, 0.3f, 1f);
        }
    }
}
