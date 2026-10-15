using UnityEngine;

namespace AnomalyArena
{
    public class Hook : WeaponRuntime
    {
        enum Phase
        {
            Extend,
            Pull,
            Hold,
            Retract
        }

        KnifeHookEffect cfg;
        Phase phase;
        Vector3 dir;
        float length;
        float pullDistance;
        Combatant target;

        public Combatant HeldTarget => phase == Phase.Hold && Valid() ? target : null;
        public override bool LocksMovement => phase != Phase.Hold;
        public override bool CanSwap => phase == Phase.Hold;

        public void Begin(KnifeHookEffect e, Weapon w, IWeaponHolder u)
        {
            cfg = e;
            Bind(w, u);
            dir = u.AimDirection;
            phase = Phase.Extend;
            if (Art.On && Art.Set.hookMid)
            {
                GetComponent<Renderer>().enabled = false;
                blade = new StretchBlade(Art.Set, 0.55f);
            }

            UpdateBlade();
        }

        StretchBlade blade;

        void OnDestroy() => blade?.Destroy();

        bool Valid() => target != null && target.IsAlive && target.State == CharacterState.Hooked;

        float HoldDistance => user.Radius + target.Radius + 0.1f;

        void FixedUpdate()
        {
            if (user == null || !user.IsAlive)
            {
                Cancel();
                return;
            }

            float dt = Time.fixedDeltaTime;
            switch (phase)
            {
                case Phase.Extend:
                {
                    length = Mathf.Min(length + cfg.extendSpeed * dt, cfg.range);
                    Vector3 origin = Query.AtCastHeight(user.Position);
                    int mask = GameManager.WallMask | GameManager.CharacterMask;

                    if (Physics.SphereCast(origin, cfg.hookRadius, dir, out var hit, length, mask,
                            QueryTriggerInteraction.Ignore))
                    {
                        var c = Combatant.From(hit.collider);
                        if (c != null && (IWeaponHolder)c != user && c.IsAlive && c.State != CharacterState.Hooked)
                        {
                            target = c;
                            c.SetState(CharacterState.Hooked);
                            pullDistance = Query.Flat(c.Position - user.Position).magnitude;
                            phase = Phase.Pull;
                        }
                        else if (c == null || (IWeaponHolder)c != user)
                        {
                            length = hit.distance;
                            phase = Phase.Retract;
                        }
                    }
                    else if (length >= cfg.range)
                    {
                        phase = Phase.Retract;
                    }

                    break;
                }
                case Phase.Pull:
                {
                    if (!Valid())
                    {
                        Release();
                        phase = Phase.Retract;
                        break;
                    }

                    pullDistance = Mathf.MoveTowards(pullDistance, HoldDistance, cfg.pullSpeed * dt);
                    dir = user.AimDirection;
                    PlaceTarget(pullDistance);
                    if (pullDistance <= HoldDistance + 1e-3f) phase = Phase.Hold;
                    break;
                }
                case Phase.Hold:
                {
                    if (!Valid())
                    {
                        target = null;
                        Finish();
                        return;
                    }

                    dir = user.AimDirection;
                    PlaceTarget(HoldDistance);
                    break;
                }
                case Phase.Retract:
                    length -= cfg.retractSpeed * dt;
                    if (length <= 0f)
                    {
                        Finish();
                        return;
                    }

                    break;
            }

            UpdateBlade();
        }

        void PlaceTarget(float distance)
        {
            Vector3 p = ArenaShape.ClampInside(user.Position + dir * distance, target.Radius);
            p.y = target.Position.y;
            target.SetMovePosition(p);
            length = Query.Flat(p - user.Position).magnitude;
        }

        public override void OnUsePressed()
        {
            if (phase == Phase.Hold) Fling();
        }

        public override void OnSwapAway()
        {
            if (phase == Phase.Hold) Fling();
        }

        void Fling()
        {
            var t = target;
            target = null;
            if (t != null && t.IsAlive && t.State == CharacterState.Hooked)
            {
                t.SetState(CharacterState.Normal);
                t.Throw(user.AimDirection, cfg.throwDistance, cfg.Impact(user as IDamageDealer));
            }

            Finish();
        }

        void Release()
        {
            if (target != null && target.State == CharacterState.Hooked) target.SetState(CharacterState.Normal);
            target = null;
        }

        public override void Cancel()
        {
            Release();
            Finish();
        }

        void UpdateBlade()
        {
            Vector3 origin = Query.AtCastHeight(user.Position);
            float len = Mathf.Max(0.05f, length);
            transform.position = origin + dir * (len * 0.5f);
            transform.rotation = Quaternion.LookRotation(dir);
            transform.localScale = new Vector3(0.12f, 0.06f, len);
            blade?.Set(origin, dir, len);
        }
    }
}