using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    public sealed class AttackActor : MonoBehaviour
    {
        public Renderer visual;
        public AttackMode mode;
        public Combatant owner;
        public RunWeapon weapon;
        public Vector3 direction, point;
        public float speed, range, damage, knock, radius, remaining, delay, tickInterval = .25f;
        public float pierceDamageRetention = 1f;
        public bool pierce, hurtOwner, ignoreShield, followCursor;
        public int ricochets, targetsLeft;
        public int internalWallsLeft;
        public ElementKind element;
        public int Sequence { get; private set; }
        public bool IsReturning => returning;
        public bool PlantedRecall { get; private set; }
        public bool LobInFlight => mode == AttackMode.Lob || mode == AttackMode.Recall && !PlantedRecall;
        public bool AffectedByHoles => mode is not (AttackMode.Field or AttackMode.Pull or AttackMode.Blast) && !LobInFlight && !(mode == AttackMode.Swarm && stoppedOnWall);
        public bool KnifeReturning => mode == AttackMode.Knife && returning;
        public bool HasKnifeCorpse { get; private set; }
        public float KnifeCorpseRadius { get; private set; }
        public Sprite KnifeCorpseSprite { get; private set; }
        public Enemy KnifeCorpseSource { get; private set; }
        public int KnifeCorpseSpawnVersion { get; private set; }
        public Vector3 KnifeCorpsePosition => Query.Flat(transform.position);
        public static System.Func<Enemy,int,bool> ClaimKnifeCorpse;
        public static System.Action<Enemy,int,Vector3> DropKnifeCorpse;
        public Vector3 KnifeReturnPoint { get; private set; }
        public float KnifeSpinAngle { get; private set; }
        static int nextSequence;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly HashSet<Combatant> struck = new();
        readonly Collider[] knifeContacts = new Collider[128];
        readonly HashSet<Combatant> knifeReturnHits = new();
        readonly HashSet<Collider> passedWalls = new();
        readonly List<(Combatant body, Vector3 dir)> blastTargets = new();
        float flown, tick, corpseDamage;
        bool returning, hookRetracting, stoppedOnWall, finalFieldPush, knifeCorpseClaimedThisThrow;
        float attachedAge;
        Combatant knifeSeekTarget;
        Combatant carrier;
        float attachedLeft;
        bool attached;
        Vector3 lastCarrierPoint;
        Vector3 arcStart;
        Vector3 launchDirection;
        float arcElapsed, arcDuration, arcHeight, landedDuration;

        public void Configure(AttackMode type, Combatant from, RunWeapon w, Vector3 at, Vector3 dir,
            float hit, float push, float reach, float velocity, float area = 0, float lifetime = 3)
        {
            mode = type; owner = from; weapon = w;
            transform.position = Query.AtCastHeight(at);
            direction = Query.Flat(dir).normalized; point = Query.Flat(at);
            launchDirection = direction;
            damage = hit; knock = push; range = reach; speed = velocity;
            radius = area; remaining = lifetime; delay = tick = flown = 0;
            if (type == AttackMode.Hook) flown = Query.Flat(transform.position - from.Position).magnitude;
            tickInterval = .25f; pierceDamageRetention = 1;
            struck.Clear(); knifeReturnHits.Clear(); passedWalls.Clear(); returning = attached = hookRetracting = stoppedOnWall = finalFieldPush = false;
            attachedAge = 0; internalWallsLeft = 0;
            knifeSeekTarget = null; HasKnifeCorpse = false; KnifeCorpseRadius = KnifeSpinAngle = 0;
            KnifeCorpseSprite = null; KnifeReturnPoint = Vector3.zero;
            KnifeCorpseSource = null; KnifeCorpseSpawnVersion = 0; knifeCorpseClaimedThisThrow = false;
            corpseDamage = attachedLeft = 0; carrier = null;
            PlantedRecall = false; arcElapsed = arcDuration = arcHeight = landedDuration = 0f;
            hurtOwner = ignoreShield = followCursor = pierce = false;
            ricochets = 0; targetsLeft = 4; element = w?.element ?? ElementKind.None;
            Sequence = ++nextSequence;
            bool field = type is AttackMode.Field or AttackMode.Pull or AttackMode.Blast;
            transform.localScale = field ? new Vector3(area * 2, .035f, area * 2) : Vector3.one * .22f;
            if (visual) visual.sharedMaterial = GameManager.Instance.Mat(ElementCombat.ColorFor(element));
        }

        public void StartArc(Vector3 destination, float height, float durationAfterLanding)
        {
            point = Query.Flat(destination);
            arcStart = transform.position;
            arcElapsed = 0f;
            arcDuration = Mathf.Max(.2f, Query.Flat(point - arcStart).magnitude / Mathf.Max(1f, speed));
            arcHeight = Mathf.Max(0f, height);
            landedDuration = durationAfterLanding;
        }

        public void Release()
        {
            if (HasKnifeCorpse && KnifeCorpseSource)
                DropKnifeCorpse?.Invoke(KnifeCorpseSource,KnifeCorpseSpawnVersion,KnifeCorpsePosition);
            if (carrier)
            {
                carrier.RemoveMovementModifier(this);
                if(mode==AttackMode.Hook&&carrier.IsAlive)carrier.SetState(CharacterState.Normal);
            }
            if (weapon != null)
            {
                if (mode == AttackMode.Knife)
                {
                    weapon.inFlight = false;
                    weapon.cooldown = Mathf.Max(weapon.cooldown, GameManager.Instance.runWeapons.ReturnCooldown(weapon));
                }
                if (mode == AttackMode.Hook) weapon.hookBusy = false;
                if (weapon.channelledField == this) weapon.channelledField = null;
                if (weapon.recallActor == this)
                {
                    weapon.recallActor = null; weapon.recallPlanted = false; weapon.inFlight = false;
                }
                if (weapon.stickyActor == this) weapon.stickyActor = null;
            }
            carrier = knifeSeekTarget = null; attached = PlantedRecall = HasKnifeCorpse = false;
            KnifeCorpseSprite = null;
            KnifeCorpseSource = null; KnifeCorpseSpawnVersion = 0;
            gameObject.SetActive(false);
        }
        public bool TryConsumeKnifeCorpse(out Vector3 at)
        {
            at = KnifeCorpsePosition;
            if (!HasKnifeCorpse) return false;
            HasKnifeCorpse = false; corpseDamage = 0f; KnifeCorpseSprite = null;
            KnifeCorpseSource = null; KnifeCorpseSpawnVersion = 0;

            return true;
        }

        void FixedUpdate()
        {
            var gm = GameManager.Instance;
            if (!gm || !gm.IsCombatActive) return;
            float dt = gm.WorldFixedDeltaTime;
            if (mode is AttackMode.Lob or AttackMode.Recall)
            {
                if (!owner || !owner.IsAlive) { Release(); return; }
                if (!PlantedRecall) TickArc(dt);
                else if (weapon.Has(UpgradeAxis.Tame)) { remaining -= dt; if (remaining <= 0) Release(); }
                return;
            }
            if (mode == AttackMode.Hook) { TickHook(dt); return; }
            if (mode == AttackMode.Knife) { TickKnife(dt); return; }
            if (mode == AttackMode.Sticky && attached)
            {
                if (carrier)
                {
                    if (carrier.IsAlive) lastCarrierPoint = Query.Flat(carrier.Position);
                    else { carrier.RemoveMovementModifier(this); carrier = null; }
                }
                transform.position = Query.AtCastHeight(lastCarrierPoint);
                attachedLeft -= dt; attachedAge += dt;
                if (attachedLeft <= 0) { point = lastCarrierPoint; Explode(); Release(); }
                return;
            }
            if (delay > 0)
            {
                delay -= dt;
                if (delay > 0) return;
                if (mode == AttackMode.Blast) { Explode(); Release(); return; }
            }
            remaining -= dt;
            if (remaining <= 0)
            {
                if (mode == AttackMode.Bouncy || mode == AttackMode.Swarm && !stoppedOnWall) Explode();
                if (mode == AttackMode.Pull && weapon.Has(UpgradeAxis.Twist)) PushEnemies(owner.GetComponent<RunWeapons>().upgrades.attractEndPush);
                Release(); return;
            }
            if (mode == AttackMode.Blast) { Explode(); Release(); return; }
            if (mode is AttackMode.Field or AttackMode.Pull)
            {
                TickField(dt); return;
            }
            if (stoppedOnWall)
            {
                if (mode == AttackMode.Swarm) foreach (var body in Combatant.All)
                    if (body != owner && body.IsAlive && Query.Flat(body.Position-transform.position).magnitude <= radius+body.radius)
                    { Explode(); Release(); break; }
                return;
            }
            if(mode==AttackMode.Swarm&&returning)
                direction=Query.Flat(owner.Position-transform.position).normalized;
            float distance = speed * dt;
            int count = Physics.SphereCastNonAlloc(transform.position, .16f, direction, hits, distance,
                GameManager.WallMask | GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, count, HitComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                var body = Combatant.From(hit.collider);
                if (body == owner)
                {
                    if (mode == AttackMode.Swarm && returning)
                    { transform.position = hit.point; hurtOwner = true; Explode(); Release(); return; }
                    continue;
                }
                if (body && (!body.IsAlive || struck.Contains(body))) continue;
                if (!body)
                {
                    if (passedWalls.Contains(hit.collider)) continue;
                    PlaySound(WeaponAudioPhase.Impact);
                    if (internalWallsLeft > 0 && !IsBoundary(hit.collider))
                    { passedWalls.Add(hit.collider); internalWallsLeft--; continue; }
                    if (ricochets > 0)
                    {
                        ricochets--; direction = Query.Flat(Vector3.Reflect(direction, hit.normal)).normalized;
                        transform.position = hit.point + direction * .3f;
                        if (mode == AttackMode.Bouncy && ricochets == 0 && weapon.Has(UpgradeAxis.Twist)) stoppedOnWall = true;
                        return;
                    }
                    transform.position = hit.point;
                    if (mode == AttackMode.Swarm && weapon.Has(UpgradeAxis.Twist))
                    {
                        transform.position=hit.point+hit.normal*.2f;stoppedOnWall=true;
                        remaining=owner.GetComponent<RunWeapons>().upgrades.swarmMineDuration;return;
                    }
                    if (mode is AttackMode.Bouncy or AttackMode.Swarm) Explode();
                    Release(); return;
                }
                if (mode == AttackMode.Sticky)
                {
                    carrier = body; attached = true; attachedLeft = 2f;
                    lastCarrierPoint = Query.Flat(body.Position);
                    if (!weapon.Has(UpgradeAxis.Tame)) body.AddMovementModifier(this, 1.5f);
                    hurtOwner = true;
                    transform.position = Query.AtCastHeight(body.Position);
                    PlaySound(WeaponAudioPhase.Impact);
                    return;
                }
                if (mode is AttackMode.Bouncy or AttackMode.Swarm)
                { transform.position = hit.point; Explode(); Release(); return; }
                if (mode == AttackMode.Swap)
                {
                    bool movable = body.State != CharacterState.Hooked && body is not Enemy { Immovable: true };
                    struck.Add(body);
                    Hit(body, damage, 0f, direction);
                    if (weapon.Has(UpgradeAxis.Twist))
                    {
                        if (!movable || --targetsLeft > 0) continue;
                    }
                    if (body.IsAlive && movable) owner.GetComponent<RunWeapons>()?.CompleteSwap(this, body);
                    Release(); return;
                }
                struck.Add(body);
                Hit(body, damage, knock, direction);
                damage *= pierceDamageRetention;
                if (!pierce) { Release(); return; }
            }
            transform.position += direction * distance; flown += distance;
            if (flown >= range && mode != AttackMode.Bouncy)
            {
                if (mode == AttackMode.Swarm && !returning)
                {
                    if (weapon.Has(UpgradeAxis.Tame)) { Release(); return; }
                    returning = true; direction = Query.Flat(owner.Position - transform.position).normalized;
                    flown = 0; range = Vector3.Distance(Query.Flat(owner.Position), Query.Flat(transform.position)) + 1;
                    hurtOwner = true;
                }
                else
                {
                    if (mode == AttackMode.Swarm) Explode();
                    Release();
                }
            }
        }

        void TickArc(float dt)
        {
            arcElapsed = Mathf.Min(arcDuration, arcElapsed + dt);
            float t = arcElapsed / arcDuration;
            Vector3 end = new Vector3(point.x, .12f, point.z);
            Vector3 next = Vector3.Lerp(arcStart, end, t) + Vector3.up * (4f * arcHeight * t * (1f - t));
            Vector3 delta = next - transform.position;
            if (delta.sqrMagnitude > .000001f && Physics.SphereCast(transform.position, .16f, delta.normalized,
                    out var wall, delta.magnitude, GameManager.WallMask, QueryTriggerInteraction.Ignore))
            {
                point = Query.Flat(wall.point + wall.normal * .25f);
                Land();
                return;
            }
            transform.position = next;
            if (delta.sqrMagnitude > .000001f) direction = delta.normalized;
            if (t >= 1f) Land();
        }

        void Land()
        {
            if (!RunWeapons.ValidTeleportPoint(point, .2f)) { Release(); return; }
            transform.position = new Vector3(point.x, .12f, point.z);
            PlaySound(mode==AttackMode.Lob?WeaponAudioPhase.Fire:WeaponAudioPhase.Impact);
            if (mode == AttackMode.Recall)
            {
                PlantedRecall = true;
                remaining = weapon.Has(UpgradeAxis.Tame) ? owner.GetComponent<RunWeapons>().upgrades.recallTimeout : float.PositiveInfinity;
                if (weapon != null) { weapon.recallPoint = point; weapon.recallPlanted = true; weapon.inFlight = false; }
                return;                                                                                  
            }
            if (weapon?.form == WeaponForm.Flamethrower && weapon.variant == 1)
                owner.GetComponent<RunWeapons>()?.WildfireLanded(weapon);
            mode = AttackMode.Field;
            direction = launchDirection;
            remaining = landedDuration;
            tick = 0f;
            transform.localScale = new Vector3(radius * 2f, .035f, radius * 2f);
        }

        void BeginReturn()
        {
            if (returning) return;
            returning = true; knifeSeekTarget = null;

            KnifeReturnPoint = Query.AtCastHeight(owner ? owner.Position : transform.position);
        }
        Combatant NextKnifeTarget(Vector3 from)
        {
            var weapons = owner.GetComponent<RunWeapons>();
            float seekRange = weapons ? weapons.knifeSeekRange : 7f;
            Combatant best = null; float nearest = float.PositiveInfinity;
            foreach (var body in Combatant.All)
            {
                if (body == owner || !body.IsAlive || struck.Contains(body)) continue;
                float distance = Query.Flat(body.Position - from).sqrMagnitude;
                if (distance > (seekRange + body.radius) * (seekRange + body.radius) || distance >= nearest || Physics.Linecast(Query.AtCastHeight(from),
                    Query.AtCastHeight(body.Position), GameManager.WallMask)) continue;
                best = body; nearest = distance;
            }
            return best;
        }

        void TickKnife(float dt)
        {
            if (!owner || !owner.IsAlive) { Release(); return; }
            var weapons = owner.GetComponent<RunWeapons>();
            float cycle = weapons ? weapons.StartingFormCycleFactor(WeaponForm.Knife) : .5f;
            KnifeSpinAngle = Mathf.Repeat(KnifeSpinAngle + (returning ? -1080f : 1080f) * dt / cycle, 360f);
            if (returning)
            {
                Vector3 from = transform.position;
                Vector3 to = Query.Flat(KnifeReturnPoint - from);
                float step = (weapons ? weapons.knifeReturnSpeed : 14f) / cycle * dt;
                if (to.sqrMagnitude > .000001f) direction = to.normalized;
                Vector3 next = Query.AtCastHeight(from + direction * Mathf.Min(step, to.magnitude));
                if (HasKnifeCorpse)
                {
                    int count = Physics.OverlapCapsuleNonAlloc(from, next, KnifeCorpseRadius + .1f,
                        knifeContacts, GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                    {
                        var body = Combatant.From(knifeContacts[i]);
                        if (!body || !body.IsAlive || body == owner && weapon.Has(UpgradeAxis.Tame) || !knifeReturnHits.Add(body)) continue;
                        HitKnife(body, corpseDamage, 5f, HitKind.Slam);
                    }
                }
                transform.position = next;
                if (to.magnitude <= step) Release();
                return;
            }
            if (knifeSeekTarget)
            {
                if (!knifeSeekTarget.IsAlive || struck.Contains(knifeSeekTarget)) knifeSeekTarget = NextKnifeTarget(transform.position);
                if (!knifeSeekTarget) { BeginReturn(); return; }
                direction = Query.Flat(knifeSeekTarget.Position - transform.position).normalized;
            }
            Vector3 start = transform.position;
            float distance = knifeSeekTarget ? speed * dt : Mathf.Min(speed * dt, Mathf.Max(0f, range - flown));
            bool wallHit = Physics.Raycast(start, direction, out var wall, distance, GameManager.WallMask, QueryTriggerInteraction.Ignore);
            float available = wallHit ? Mathf.Max(0f, wall.distance - .1f) : distance;
            Vector3 end = start + direction * available;
            int found = Physics.OverlapCapsuleNonAlloc(start, end, .35f, knifeContacts,
                GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
            Combatant first = null; float nearest = float.PositiveInfinity;
            for (int i = 0; i < found; i++)
            {
                var body = Combatant.From(knifeContacts[i]);
                if (!body || !body.IsAlive || body == owner || struck.Contains(body)) continue;
                Vector3 delta = Query.Flat(body.Position - start);
                float along = Vector3.Dot(delta, direction);
                float combined = body.radius + .35f;
                float perpendicularSq = Mathf.Max(0f, delta.sqrMagnitude - along * along);
                if (perpendicularSq > combined * combined) continue;
                float enter = Mathf.Max(0f, along - Mathf.Sqrt(combined * combined - perpendicularSq));
                if (enter > available || enter >= nearest) continue;
                first = body; nearest = enter;
            }
            if (first)
            {
                transform.position = start + direction * nearest;
                struck.Add(first);

                float capturedDamage = first.CorpseDamage, capturedRadius = first.radius;
                var capturedSource = first as Enemy;
                int capturedVersion = first.SpawnVersion;
                var look = first.GetComponent<V2CharacterIllustration>();
                Sprite capturedSprite = look && look.illustration ? look.illustration.sprite : null;
                Vector3 struckPoint = first.Position;
                HitKnife(first, damage, 0f, HitKind.Pierce);
                if (!first.IsAlive && !knifeCorpseClaimedThisThrow)
                {
                    knifeCorpseClaimedThisThrow = true;
                    if (capturedSource && ClaimKnifeCorpse?.Invoke(capturedSource,capturedVersion) == true)
                    {
                        HasKnifeCorpse = true; KnifeCorpseRadius = capturedRadius; KnifeCorpseSprite = capturedSprite;
                        KnifeCorpseSource = capturedSource; KnifeCorpseSpawnVersion = capturedVersion;
                        corpseDamage = capturedDamage * (weapons ? weapons.StartingFormDamageFactor(WeaponForm.Knife) : .5f);
                        knifeReturnHits.Add(first);
                    }
                }
                knifeSeekTarget = --targetsLeft > 0 ? NextKnifeTarget(struckPoint) : null;
                if (!knifeSeekTarget) BeginReturn();
                return;
            }
            transform.position = end; flown += available;
            if (wallHit)
            {
                PlaySound(WeaponAudioPhase.Impact);
                if (ricochets > 0)
                { ricochets--; direction = Query.Flat(Vector3.Reflect(direction, wall.normal)).normalized; knifeSeekTarget = null; }
                else BeginReturn();
            }
            else if (!knifeSeekTarget && flown >= range) BeginReturn();
        }

        void HitKnife(Combatant target, float amount, float push, HitKind kind)
        {
            var info = DamageInfo.Attack(amount, owner, direction, push, kind);
            info.element = element; info.baseDamage = amount;
            info.builtInElement = weapon != null && weapon.builtInElement;
            if(target.ReceiveDamage(info))PlaySound(WeaponAudioPhase.Impact);
        }
        void TickField(float dt)
        {
            if (mode == AttackMode.Field && weapon?.form == WeaponForm.SporeLauncher && weapon.variant == 0 && weapon.Has(UpgradeAxis.Twist))
            {
                Vector3 next=point+Query.Flat(direction).normalized*owner.GetComponent<RunWeapons>().upgrades.cloudDrift*dt;
                if (RunWeapons.ValidTeleportPoint(next,.2f) && !Query.WallBetween(Query.AtCastHeight(point),Query.AtCastHeight(next),out _))
                { point=next;transform.position=Query.AtCastHeight(point); }
            }
            if (followCursor)
            {
                Vector3 target = owner.Position + Vector3.ClampMagnitude(Query.Flat(owner.AimPoint - owner.Position), range);
                point = Vector3.MoveTowards(point, target, 3 * dt);
                transform.position = Query.AtCastHeight(point);
            }
            if (mode == AttackMode.Pull)
            {
                foreach (var body in Combatant.All)
                {
                    if (!body.IsAlive || body is Enemy { Immovable: true } || body == owner && weapon.Has(UpgradeAxis.Tame)) continue;
                    Vector3 to = point - Query.Flat(body.Position);
                    if (to.magnitude >= radius) continue;
                    body.SetMovePosition(body.Position + to.normalized * speed * dt * (body == owner ? .5f : 1f));
                }
            }
            else
            {
                tick -= dt;
                if (tick <= 0)
                {
                    tick += tickInterval; Explode();
                    if (!finalFieldPush && remaining <= tickInterval && weapon?.form == WeaponForm.Flamethrower && weapon.variant == 1 && weapon.Has(UpgradeAxis.Twist))
                    { finalFieldPush=true;PushEnemies(owner.GetComponent<RunWeapons>().upgrades.wildfireEndPush); }
                }
            }
        }
        void TickHook(float dt)
        {
            if (!owner || !owner.IsAlive) { Release(); return; }
            var weapons = owner.GetComponent<RunWeapons>();
            float cycle = weapons ? weapons.StartingFormCycleFactor(WeaponForm.Knife) : .5f;
            if (carrier)
            {
                if (!carrier.IsAlive) { Release(); return; }
                Vector3 destination = owner.Position + owner.AimDirection * (owner.radius + carrier.radius + .3f);
                Vector3 next = Vector3.MoveTowards(carrier.Position, destination, (weapons ? weapons.hookPullSpeed : 25f) / cycle * dt);
                carrier.SetMovePosition(next); transform.position = Query.AtCastHeight(next);
                if ((next - destination).sqrMagnitude <= .01f)
                { weapon.hooked = carrier; carrier = null; Release(); }
                return;
            }
            if (hookRetracting)
            {
                flown = Mathf.Max(0f, flown - (weapons ? weapons.hookRetractSpeed : 60f) / cycle * dt);
                transform.position = Query.AtCastHeight(owner.Position + direction * flown);
                if (flown <= 0f) Release();
                return;
            }
            float step = Mathf.Min(speed * dt, Mathf.Max(0f, range - flown));
            if (Physics.SphereCast(transform.position, .18f, direction, out var hit, step,
                GameManager.WallMask | GameManager.CharacterMask, QueryTriggerInteraction.Ignore))
            {
                var target = Combatant.From(hit.collider);
                if (target && target != owner && target.IsAlive && target is not Enemy { Immovable: true })
                {
                    Hit(target,Mathf.Max(1f,damage),0f,direction);
                    if(target.IsAlive){carrier=target;carrier.SetState(CharacterState.Hooked);}
                    else{flown+=hit.distance;transform.position=Query.AtCastHeight(owner.Position+direction*flown);hookRetracting=true;}
                    return;
                }
                if (!target && weapon.Has(UpgradeAxis.Twist))
                {
                    Vector3 destination = Query.Flat(hit.point) - direction * owner.radius;
                    owner.GetComponent<Rigidbody>().position = destination;
                    owner.transform.position = destination;
                }
                if (target != owner)
                {
                    PlaySound(WeaponAudioPhase.Impact);
                    flown += hit.distance; transform.position = Query.AtCastHeight(owner.Position + direction * flown);
                    hookRetracting = true; return;
                }
            }
            transform.position += direction * step; flown += step;
            if (flown >= range) hookRetracting = true;
        }

        public void Explode()
        {
            if(mode is not (AttackMode.Field or AttackMode.Pull))PlaySound(WeaponAudioPhase.Explosion);
            if (mode is not (AttackMode.Field or AttackMode.Pull)) point = Query.Flat(transform.position);
            blastTargets.Clear();
            foreach (var body in Combatant.All)
            {
                if (!body.IsAlive) continue;
                Vector3 away = Query.Flat(body.Position) - point;
                if (away.sqrMagnitude > (radius + body.radius) * (radius + body.radius)) continue;
                bool keepSelfPush=weapon!=null&&weapon.Has(UpgradeAxis.Tame)&&(weapon.form==WeaponForm.PlasmaLauncher&&weapon.variant==1||weapon.form==WeaponForm.SwarmLauncher&&weapon.variant==1);
                if(body==owner&&(!hurtOwner||keepSelfPush))
                {
                    if(weapon?.form==WeaponForm.RocketLauncher&&weapon.variant==0 || weapon!=null&&weapon.Has(UpgradeAxis.Tame)&&(weapon.form==WeaponForm.PlasmaLauncher&&weapon.variant==1 || weapon.form==WeaponForm.SwarmLauncher&&weapon.variant==1))
                        owner.Knockback(knock<0?-away:away,Mathf.Abs(knock),KnockKind.Hit);
                    continue;
                }
                blastTargets.Add((body, away.sqrMagnitude > 0 ? away.normalized : direction));
            }
            foreach (var (body, dir) in blastTargets) Hit(body, damage, knock, dir);
            blastTargets.Clear();
            Fx.Ring(point, radius, ElementCombat.ColorFor(element));
        }
        public bool TryManualDetonate()
        {
            if (!gameObject.activeSelf || mode != AttackMode.Sticky || !attached || !owner) return false;
            if (attachedAge < owner.GetComponent<RunWeapons>().upgrades.stickyManualDelay) return false;
            point=lastCarrierPoint;Explode();Release();return true;
        }
        void PushEnemies(float distance)
        {
            var weapons=owner.GetComponent<RunWeapons>();
            foreach(var body in Combatant.All)
                if(body!=owner&&body.IsAlive&&Query.Flat(body.Position-point).magnitude<=radius+body.radius)
                    body.Knockback(Query.Flat(body.Position-point).normalized,distance*weapons.UpgradePushScale,KnockKind.Hit);
        }
        static bool IsBoundary(Collider collider)
        {
            for(var at=collider.transform;at;at=at.parent)if(at.name=="Boundary")return true;
            return false;
        }
        public void Hit(Combatant target, float amount, float push, Vector3 dir)
        {
            var info = DamageInfo.Attack(amount, owner, push < 0 ? -dir : dir, Mathf.Abs(push),
                mode is AttackMode.Field or AttackMode.Blast or AttackMode.Sticky or AttackMode.Bouncy or AttackMode.Swarm ? HitKind.Blast : pierce ? HitKind.Pierce : HitKind.Bullet);
            info.element = element; info.baseDamage = amount;
            info.builtInElement = weapon != null && weapon.builtInElement; info.ignoreShield = ignoreShield;
            if(target.ReceiveDamage(info))PlaySound(WeaponAudioPhase.Impact);
        }
        void PlaySound(WeaponAudioPhase phase)
        {
            if(weapon!=null)V2Audio.Instance?.PlayWeapon(weapon.form,weapon.variant,phase);
        }
        sealed class HitComparer : IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new();
            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }
    }
}
