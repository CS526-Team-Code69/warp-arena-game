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
        public bool pierce, hurtOwner, followCursor;
        [Tooltip("Lance (document 6.4): the hit still goes into the Shield first, but the Shield does not soften it.")]
        public bool ignoreShieldReduction;
        public int ricochets, targetsLeft;
        [Tooltip("Bodies a piercing bolt may still pass through before it stops; Configure resets it to no limit.")]
        public int piercesLeft = int.MaxValue;
        public int internalWallsLeft;
        [Tooltip("Reverse Shot Twist: this round came out of a split and never splits again.")]
        public bool splitChild;
        [Tooltip("Strike Twist: a small blast set off by a kill; it never chains again and never touches its shooter.")]
        public bool chainChild;
        public ElementKind element;
        [Tooltip("Damage this attack deals to its own shooter when it hurts them; negative means the same as damage. Backfires use the weapon's base value.")]
        public float ownerDamage = -1f;

        float ownerCarry;

        [System.NonSerialized] public HashSet<Combatant> sharedStruck;
        public int Sequence { get; private set; }
        public bool IsReturning => returning;
        public bool PlantedRecall { get; private set; }
        public bool LobInFlight => mode == AttackMode.Lob || mode == AttackMode.Recall && !PlantedRecall;

        public bool AffectedByHoles => mode is not (AttackMode.Field or AttackMode.Pull or AttackMode.Blast);
        public bool KnifeReturning => mode == AttackMode.Knife && returning;

        public bool KnifeKillReturn => mode == AttackMode.Knife && returning && killReturn;

        public bool HasKnifeCorpse => false;
        public float KnifeCorpseRadius => 0f;
        public Sprite KnifeCorpseSprite => null;
        public Enemy KnifeCorpseSource => null;
        public int KnifeCorpseSpawnVersion => 0;
        public Vector3 KnifeCorpsePosition => Query.Flat(transform.position);
        public static System.Func<Enemy,int,bool> ClaimKnifeCorpse;
        public static System.Action<Enemy,int,Vector3> DropKnifeCorpse;
        public Vector3 KnifeReturnPoint { get; private set; }

        public Vector3 KnifeHomePoint => Query.Flat(launchPoint);
        public float KnifeSpinAngle { get; private set; }
        static int nextSequence;
        const float KnifeRadius = .35f;

        const float SwarmOwnerArmDistance = 1f;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly HashSet<Combatant> struck = new();
        readonly Collider[] knifeContacts = new Collider[128];
        readonly HashSet<Combatant> knifeReturnHits = new();
        readonly HashSet<Collider> passedWalls = new();
        readonly List<(Combatant body, Vector3 dir)> blastTargets = new();
        readonly List<Vector3> chainPoints = new();
        float flown, tick;
        bool returning, killReturn, lostToHole, hookRetracting, stoppedOnWall;
        float attachedAge;
        Combatant knifeSeekTarget;
        Combatant carrier;
        float attachedLeft;
        bool attached;
        Vector3 lastCarrierPoint;
        Vector3 arcStart;
        Vector3 launchDirection;
        Vector3 launchPoint;                                                                                   
        bool crossed;                                                              
        Combatant hiveHost;                                                                                             
        float arcElapsed, arcDuration, arcHeight, landedDuration;

        public void Configure(AttackMode type, Combatant from, RunWeapon w, Vector3 at, Vector3 dir,
            float hit, float push, float reach, float velocity, float area = 0, float lifetime = 3)
        {
            mode = type; owner = from; weapon = w;
            transform.position = Query.AtCastHeight(at);
            direction = Query.Flat(dir).normalized; point = Query.Flat(at);
            launchDirection = direction;
            launchPoint = Query.AtCastHeight(from ? from.Position : at);
            damage = hit; knock = push; range = reach; speed = velocity;
            radius = area; remaining = lifetime; delay = tick = flown = 0;
            if (type == AttackMode.Hook) flown = Query.Flat(transform.position - from.Position).magnitude;
            tickInterval = .25f; pierceDamageRetention = 1; piercesLeft = int.MaxValue;
            struck.Clear(); knifeReturnHits.Clear(); passedWalls.Clear(); returning = killReturn = lostToHole = attached = hookRetracting = stoppedOnWall = false;
            attachedAge = 0; internalWallsLeft = 0; splitChild = chainChild = false;
            knifeSeekTarget = null; KnifeSpinAngle = 0; KnifeReturnPoint = Vector3.zero;
            attachedLeft = 0; carrier = null;
            crossed = false; hiveHost = null;
            PlantedRecall = false; arcElapsed = arcDuration = arcHeight = landedDuration = 0f;
            hurtOwner = ignoreShieldReduction = followCursor = pierce = false;
            ownerDamage = -1f; ownerCarry = 0f; sharedStruck = null;
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

            if (mode == AttackMode.Bolt && IsLanceTwist && gameObject.activeSelf) Shooter?.LanceTrail(weapon, launchPoint, transform.position);
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

                    if (!lostToHole) weapon.cooldown = Mathf.Max(weapon.cooldown, GameManager.Instance.runWeapons.ReturnCooldown(weapon));
                }
                if (mode == AttackMode.Hook)
                {
                    weapon.hookBusy = false;

                    if (weapon.hooked == null) weapon.cooldown = Mathf.Max(weapon.cooldown, GameManager.Instance.runWeapons.HookCooldown(weapon));
                }
                if (weapon.channelledField == this) weapon.channelledField = null;
                if (weapon.recallActor == this)
                {
                    weapon.recallActor = null; weapon.recallPlanted = false; weapon.inFlight = false;
                }
                if (weapon.stickyActor == this) weapon.stickyActor = null;
            }
            carrier = knifeSeekTarget = null; attached = PlantedRecall = lostToHole = false;
            gameObject.SetActive(false);
        }

        public void SwallowedByHole()
        {
            var lost = mode == AttackMode.Knife ? weapon : null;
            var weapons = lost != null && owner ? owner.GetComponent<RunWeapons>() : null;
            lostToHole = lost != null;
            Release();
            if (weapons) weapons.KnifeLostToHole(lost);
        }

        public bool TryConsumeKnifeCorpse(out Vector3 at) { at = KnifeCorpsePosition; return false; }

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

                if (hiveHost && attachedLeft > 0 && Mathf.Repeat(attachedAge, HiveRingInterval) < dt && IsHiveLeader())
                {
                    var weapons = Shooter;
                    if (weapons) Fx.Ring(lastCarrierPoint, HiveRadius(weapons, HiveCount()), new Color(1f, .6f, .3f, .7f), HiveRingInterval + .02f);
                }
                if (attachedLeft <= 0) { point = lastCarrierPoint; if (hiveHost) MergeHive(); Explode(); Release(); }
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
                if (mode is AttackMode.Bouncy or AttackMode.Swarm) Explode();
                if (mode == AttackMode.Shard) Shooter?.ShardLanded(this, Query.Flat(transform.position));
                Release(); return;
            }
            if (mode == AttackMode.Blast) { Explode(); Release(); return; }
            if (mode is AttackMode.Field or AttackMode.Pull)
            {
                TickField(dt); return;
            }
            if (stoppedOnWall) return;
            float distance = speed * dt;
            int count = Physics.SphereCastNonAlloc(transform.position, .16f, direction, hits, distance,
                GameManager.WallMask | GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, count, HitComparer.Instance);
            if (IsLanceTwist && !crossed) CheckLanceCross(StepToWall(count, distance));
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];

                bool startInside = hit.distance <= 0f;
                Vector3 hitPoint = startInside ? transform.position : hit.point, hitNormal = startInside ? -direction : hit.normal;
                var body = Combatant.From(hit.collider);
                if (body == owner)
                {

                    if (mode == AttackMode.Swarm && hurtOwner && owner.IsAlive && flown >= SwarmOwnerArmDistance)
                    { transform.position = hitPoint; Explode(); Release(); return; }
                    continue;
                }
                if (body && (!body.IsAlive || struck.Contains(body) || sharedStruck != null && sharedStruck.Contains(body))) continue;
                if (!body)
                {
                    if (passedWalls.Contains(hit.collider)) continue;
                    PlaySound(WeaponAudioPhase.Impact);
                    if (internalWallsLeft > 0 && !IsBoundary(hit.collider))
                    { passedWalls.Add(hit.collider); internalWallsLeft--; continue; }
                    if (ricochets > 0)
                    {
                        ricochets--; direction = Query.Flat(Vector3.Reflect(direction, hitNormal)).normalized;
                        transform.position = hitPoint + direction * .3f;
                        if (mode == AttackMode.Bouncy && ricochets == 0 && weapon.Has(UpgradeAxis.Twist)) stoppedOnWall = true;
                        if (mode == AttackMode.Bolt && !splitChild && weapon?.form == WeaponForm.Rifle && weapon.variant == 1 && weapon.Has(UpgradeAxis.Twist)) SplitReverseShot(hitPoint);
                        return;
                    }

                    transform.position = hitPoint + Query.Flat(hitNormal) * .1f;
                    if (mode is AttackMode.Bouncy or AttackMode.Swarm) Explode();
                    if (mode == AttackMode.Shard) Shooter?.ShardLanded(this, Query.Flat(hitPoint) - direction * .4f);
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
                if (mode == AttackMode.Swarm && weapon != null && weapon.Has(UpgradeAxis.Twist)) { StickToHive(body); return; }
                if (mode is AttackMode.Bouncy or AttackMode.Swarm)
                { transform.position = hitPoint; Explode(); Release(); return; }
                if (mode == AttackMode.Shard)
                {

                    struck.Add(body); sharedStruck?.Add(body);
                    Shooter?.ShardStruck(this, body);
                    Release(); return;
                }
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
                if (weapon != null && weapon.form == WeaponForm.SporeLauncher && weapon.variant == 1 && weapon.Has(UpgradeAxis.Twist)) Shooter?.NeedleLanded(this, body);
                if (!body.IsAlive && weapon != null && weapon.form == WeaponForm.Minigun && weapon.variant == 1 && weapon.Has(UpgradeAxis.Twist))
                    Shooter?.MeltdownKillVent(weapon, body);
                damage *= pierceDamageRetention;
                if (!pierce || piercesLeft-- <= 0) { Release(); return; }
            }
            transform.position += direction * distance; flown += distance;
            if (flown >= range && mode != AttackMode.Bouncy)
            {

                if (mode == AttackMode.Swarm) Explode();
                if (mode == AttackMode.Shard) Shooter?.ShardLanded(this, Query.Flat(transform.position));
                Release();
            }
        }

        RunWeapons Shooter => owner ? owner.GetComponent<RunWeapons>() : null;

        void SplitReverseShot(Vector3 hitPoint)
        {
            var tuning = Shooter ? Shooter.upgrades : null;
            float angle = tuning != null ? tuning.reverseSplitAngle : 15f, share = tuning != null ? tuning.reverseSplitShare : .6f;
            Vector3 bounce = direction;
            damage *= share; ricochets = 0; splitChild = true;
            var twin = AttackPool.Instance ? AttackPool.Instance.Acquire() : null;
            if (twin)
            {
                Vector3 other = Quaternion.Euler(0f, angle, 0f) * bounce;
                twin.Configure(AttackMode.Bolt, owner, weapon, hitPoint + other * .3f, other, damage, knock, Mathf.Max(0f, range - flown), speed, 0, remaining);
                twin.pierce = pierce; twin.ignoreShieldReduction = ignoreShieldReduction; twin.element = element; twin.splitChild = true;
            }
            direction = Quaternion.Euler(0f, -angle, 0f) * bounce;
            transform.position = hitPoint + direction * .3f;
        }

        bool IsLanceTwist => weapon != null && weapon.form == WeaponForm.Railgun && weapon.variant == 0 && weapon.Has(UpgradeAxis.Twist);

        float StepToWall(int count, float step)
        {
            for (int i = 0; i < count; i++)
                if (!Combatant.From(hits[i].collider) && !passedWalls.Contains(hits[i].collider)) return Mathf.Min(step, Mathf.Max(0f, hits[i].distance));
            return step;
        }

        void CheckLanceCross(float step)
        {
            if (weapon.lanceTrailLeft <= 0f || step <= 0f) return;
            Vector3 from = Query.Flat(transform.position), to = from + direction * step;
            if (!SegmentsCross(from, to, Query.Flat(weapon.lanceTrailFrom), Query.Flat(weapon.lanceTrailTo), out var at)) return;
            crossed = true;
            Shooter?.LanceCross(this, at);
        }

        public static bool SegmentsCross(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 at)
        {
            at = Vector3.zero;
            float rx = b.x - a.x, rz = b.z - a.z, sx = d.x - c.x, sz = d.z - c.z;
            float cross = rx * sz - rz * sx;
            if (Mathf.Abs(cross) < 1e-6f) return false;
            float qx = c.x - a.x, qz = c.z - a.z;
            float t = (qx * sz - qz * sx) / cross, u = (qx * rz - qz * rx) / cross;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            at = new Vector3(a.x + rx * t, 0f, a.z + rz * t);
            return true;
        }

        void StickToHive(Combatant body)
        {
            var weapons = Shooter;
            float fuse = weapons ? weapons.upgrades.swarmHiveFuse : 1f;
            if (AttackPool.Instance)
                foreach (var other in AttackPool.Instance.actors)
                    if (other && other != this && other.gameObject.activeSelf && other.mode == AttackMode.Sticky && other.attached && other.hiveHost == body && other.weapon == weapon)
                    { fuse = other.attachedLeft; break; }
            mode = AttackMode.Sticky; carrier = body; hiveHost = body; attached = true; attachedLeft = fuse;
            lastCarrierPoint = Query.Flat(body.Position);
            transform.position = Query.AtCastHeight(body.Position);
            PlaySound(WeaponAudioPhase.Impact);

            if (weapons) Fx.Ring(lastCarrierPoint, HiveRadius(weapons, HiveCount()), new Color(1f, .6f, .3f, .7f), .2f);
        }

        const float HiveRingInterval = .1f;

        bool IsHiveLeader()
        {
            if (AttackPool.Instance)
                foreach (var other in AttackPool.Instance.actors)
                    if (other && other != this && other.gameObject.activeSelf && other.mode == AttackMode.Sticky && other.attached && other.hiveHost == hiveHost && other.weapon == weapon && other.Sequence < Sequence) return false;
            return true;
        }

        int HiveCount()
        {
            int count = 0;
            if (AttackPool.Instance)
                foreach (var other in AttackPool.Instance.actors)
                    if (other && other.gameObject.activeSelf && other.mode == AttackMode.Sticky && other.attached && other.hiveHost == hiveHost && other.weapon == weapon) count++;
            return count;
        }

        float HiveRadius(RunWeapons weapons, int count) =>
            Mathf.Min(radius + weapons.upgrades.swarmHivePerRocketRadius * Mathf.Max(0, count - 1), Mathf.Max(radius, weapons.upgrades.swarmHiveMaxRadius));

        void MergeHive()
        {
            var weapons = Shooter; if (!weapons || !AttackPool.Instance) return;
            int count = 1; float total = damage, self = ownerDamage >= 0f ? ownerDamage : damage;
            foreach (var other in AttackPool.Instance.actors)
            {
                if (!other || other == this || !other.gameObject.activeSelf || other.mode != AttackMode.Sticky || !other.attached || other.hiveHost != hiveHost || other.weapon != weapon) continue;
                count++; total += other.damage; self += other.ownerDamage >= 0f ? other.ownerDamage : other.damage;
                other.hiveHost = null; other.Release();
            }
            radius = HiveRadius(weapons, count); damage = total; ownerDamage = self;
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

        void BeginReturn(bool afterKill)
        {
            if (returning) return;
            returning = true; killReturn = afterKill; knifeSeekTarget = null; knifeReturnHits.Clear();

            KnifeReturnPoint = launchPoint;
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
                if (killReturn)
                {

                    int count = Physics.OverlapCapsuleNonAlloc(from, next, KnifeRadius,
                        knifeContacts, GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                    {
                        var body = Combatant.From(knifeContacts[i]);
                        if (!body || !body.IsAlive) continue;

                        if (body == owner && weapon.Has(UpgradeAxis.Tame)) { weapon.knifeCaught = true; continue; }
                        if (!knifeReturnHits.Add(body)) continue;

                        HitKnife(body, body == owner && ownerDamage >= 0f ? ownerDamage : damage, 0f, HitKind.Pierce);
                    }
                }
                transform.position = next;
                if (to.magnitude <= step)
                {
                    if (weapon != null && weapon.Has(UpgradeAxis.Twist)) KnifeSpin(weapons);
                    Release();
                }
                return;
            }
            if (knifeSeekTarget)
            {
                if (!knifeSeekTarget.IsAlive || struck.Contains(knifeSeekTarget)) knifeSeekTarget = NextKnifeTarget(transform.position);
                if (!knifeSeekTarget) { BeginReturn(false); return; }
                direction = Query.Flat(knifeSeekTarget.Position - transform.position).normalized;
            }
            Vector3 start = transform.position;
            float distance = knifeSeekTarget ? speed * dt : Mathf.Min(speed * dt, Mathf.Max(0f, range - flown));
            bool wallHit = Physics.Raycast(start, direction, out var wall, distance, GameManager.WallMask, QueryTriggerInteraction.Ignore);
            float available = wallHit ? Mathf.Max(0f, wall.distance - .1f) : distance;
            Vector3 end = start + direction * available;
            int found = Physics.OverlapCapsuleNonAlloc(start, end, KnifeRadius, knifeContacts,
                GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
            Combatant first = null; float nearest = float.PositiveInfinity;
            for (int i = 0; i < found; i++)
            {
                var body = Combatant.From(knifeContacts[i]);
                if (!body || !body.IsAlive || body == owner || struck.Contains(body)) continue;
                Vector3 delta = Query.Flat(body.Position - start);
                float along = Vector3.Dot(delta, direction);
                float combined = body.radius + KnifeRadius;
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
                Vector3 struckPoint = first.Position;
                HitKnife(first, damage, 0f, HitKind.Pierce);

                if (!first.IsAlive) { V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponKnifeReturn, transform.position); BeginReturn(true); return; }
                knifeSeekTarget = --targetsLeft > 0 ? NextKnifeTarget(struckPoint) : null;
                if (!knifeSeekTarget) BeginReturn(false);
                return;
            }
            transform.position = end; flown += available;
            if (wallHit)
            {
                PlaySound(WeaponAudioPhase.Impact);
                if (ricochets > 0)
                { ricochets--; direction = Query.Flat(Vector3.Reflect(direction, wall.normal)).normalized; knifeSeekTarget = null; }
                else BeginReturn(false);
            }
            else if (!knifeSeekTarget && flown >= range) BeginReturn(false);
        }

        void KnifeSpin(RunWeapons weapons)
        {
            float spin = weapons ? weapons.upgrades.knifeSpinRadius : 1.5f, share = weapons ? weapons.upgrades.knifeSpinShare : .8f;
            Vector3 home = Query.Flat(KnifeReturnPoint);
            blastTargets.Clear();
            foreach (var body in Combatant.All)
            {
                if (body == owner || !body.IsAlive || knifeReturnHits.Contains(body)) continue;
                Vector3 away = Query.Flat(body.Position) - home;
                if (away.sqrMagnitude > (spin + body.radius) * (spin + body.radius)) continue;
                if (Query.WallBetween(Query.AtCastHeight(home), Query.AtCastHeight(body.Position), out _)) continue;
                blastTargets.Add((body, away.sqrMagnitude > 0 ? away.normalized : direction));
            }

            foreach (var (body, _) in blastTargets) HitKnife(body, damage * share, 0f, HitKind.Pierce);
            blastTargets.Clear();
            Fx.Ring(home, spin, new Color(.75f, .9f, 1f, .8f));
        }

        void HitKnife(Combatant target, float amount, float push, HitKind kind)
        {
            var info = DamageInfo.Attack(amount, owner, direction, push, kind);
            info.feel = WeaponFeel.Of(weapon);
            info.element = element;
            info.builtInElement = weapon != null && weapon.builtInElement;
            info.weapon = weapon;
            if(target.ReceiveDamage(info))PlaySound(WeaponAudioPhase.Impact);
        }
        void TickField(float dt)
        {
            if (mode == AttackMode.Field && weapon?.form == WeaponForm.SporeLauncher && weapon.variant == 0 && weapon.Has(UpgradeAxis.Twist))
            {

                var weapons = Shooter;
                if (weapons && weapons.IsMainCloud(this) && Query.Flat(owner.Position - point).magnitude <= radius)
                {
                    Vector3 next = Vector3.MoveTowards(point, Query.Flat(owner.Position), weapons.upgrades.cloudFollowSpeed * dt);
                    if (RunWeapons.ValidTeleportPoint(next,.2f) && !Query.WallBetween(Query.AtCastHeight(point),Query.AtCastHeight(next),out _))
                    { point=next;transform.position=Query.AtCastHeight(point); }
                }
            }

            if (hurtOwner && weapon?.form == WeaponForm.Flamethrower && weapon.variant == 1 && weapon.Has(UpgradeAxis.Tame)) hurtOwner = false;
            if (followCursor)
            {
                Vector3 target = owner.Position + Vector3.ClampMagnitude(Query.Flat(owner.AimPoint - owner.Position), range);
                point = Vector3.MoveTowards(point, target, 3 * dt);
                transform.position = Query.AtCastHeight(point);
            }
            tick -= dt;
            if (tick <= 0)
            {
                tick += tickInterval; Explode();

                if (weapon?.form == WeaponForm.Flamethrower && weapon.variant == 1 && weapon.Has(UpgradeAxis.Twist)) Shooter?.WildfireFence(this);
            }
        }

        public void BeginCarry(Combatant target)
        {
            if (!target || !target.IsAlive) return;
            carrier = target; carrier.SetState(CharacterState.Hooked);
            transform.position = Query.AtCastHeight(target.Position);
        }

        void TickHook(float dt)
        {
            if (!owner || !owner.IsAlive) { Release(); return; }
            var weapons = owner.GetComponent<RunWeapons>();
            float cycle = weapons ? weapons.StartingFormCycleFactor(WeaponForm.Knife) : .5f;
            if (carrier)
            {
                if (!carrier.IsAlive) { Release(); return; }
                Vector3 destination = weapons ? weapons.HookHoldPoint(carrier) : owner.Position + owner.AimDirection * (owner.radius + carrier.radius + .3f);
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

            int count = Physics.SphereCastNonAlloc(transform.position, .18f, direction, hits, step,
                GameManager.WallMask | GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, count, HitComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                var target = Combatant.From(hit.collider);
                if (target == owner) continue;
                float reached = Mathf.Max(0f, hit.distance);
                Vector3 hitPoint = hit.distance <= 0f ? transform.position : hit.point;
                if (target && target.IsAlive && target is not Enemy { Immovable: true })
                {

                    Hit(target,Mathf.Max(1f,damage),0f,direction);
                    if(target.IsAlive){carrier=target;carrier.SetState(CharacterState.Hooked);}
                    else{flown+=reached;transform.position=Query.AtCastHeight(owner.Position+direction*flown);hookRetracting=true;}
                    return;
                }

                if (!target && weapon.Has(UpgradeAxis.Tame))
                {
                    float gap = weapons ? weapons.upgrades.hookTameWallGap : 1f;
                    owner.Push(direction, Mathf.Max(0f, Query.Flat(hitPoint - owner.Position).magnitude - owner.radius - gap));
                }
                PlaySound(WeaponAudioPhase.Impact);
                flown += reached; transform.position = Query.AtCastHeight(owner.Position + direction * flown);
                hookRetracting = true; return;
            }
            transform.position += direction * step; flown += step;
            if (flown >= range) hookRetracting = true;
        }

        public void Explode()
        {
            if(mode is not (AttackMode.Field or AttackMode.Pull))PlaySound(WeaponAudioPhase.Explosion);
            if (mode is not (AttackMode.Field or AttackMode.Pull)) point = Query.Flat(transform.position);

            blastTargets.Clear();

            bool strike = mode == AttackMode.Blast && weapon?.form == WeaponForm.RocketLauncher && weapon.variant == 0;
            bool overWalls = strike && !chainChild;
            foreach (var body in Combatant.All)
            {
                if (!body.IsAlive) continue;
                Vector3 away = Query.Flat(body.Position) - point;
                if (away.sqrMagnitude > (radius + body.radius) * (radius + body.radius)) continue;
                if (!overWalls && Query.WallBetween(Query.AtCastHeight(point), Query.AtCastHeight(body.Position), out _)) continue;

                if (body == owner && weapon != null && weapon.form == WeaponForm.SwarmLauncher && weapon.variant == 1 && weapon.Has(UpgradeAxis.Tame))
                { Shooter?.BombardSelfTouch(weapon); continue; }
                bool keepSelfPush=weapon!=null&&weapon.Has(UpgradeAxis.Tame)&&weapon.form==WeaponForm.PlasmaLauncher&&weapon.variant==1;
                if(body==owner&&(!hurtOwner||keepSelfPush))
                {

                    if(strike&&!chainChild&&weapon.Has(UpgradeAxis.Tame))
                        owner.Push(away.sqrMagnitude>.09f?away:owner.AimDirection,Shooter?Shooter.upgrades.strikeJumpDistance:6f);
                    else if(strike&&!chainChild || keepSelfPush)
                        owner.Knockback(knock<0?-away:away,Mathf.Abs(knock),KnockKind.Hit);
                    continue;
                }
                blastTargets.Add((body, away.sqrMagnitude > 0 ? away.normalized : direction));
            }

            bool chain = strike && !chainChild && weapon.Has(UpgradeAxis.Twist);
            chainPoints.Clear();

            foreach (var (body, dir) in blastTargets)
            {
                Vector3 at = Query.Flat(body.Position);
                float amount = body == owner && ownerDamage >= 0f ? ownerDamage : damage;
                if (body == owner && ownerDamage >= 0f && (mode is AttackMode.Field or AttackMode.Pull))
                {
                    ownerCarry += amount; amount = Mathf.Floor(ownerCarry); ownerCarry -= amount;
                    if (amount <= 0f) continue;
                }
                Hit(body, amount, knock, dir);
                if (chain && body != owner && !body.IsAlive) chainPoints.Add(at);
            }
            blastTargets.Clear();
            if (chainPoints.Count > 0) Shooter?.StrikeChain(weapon, chainPoints, damage);
            chainPoints.Clear();
            Fx.Ring(point, radius, ElementCombat.ColorFor(element));
        }
        public bool TryManualDetonate()
        {
            if (!gameObject.activeSelf || mode != AttackMode.Sticky || !attached || !owner) return false;
            if (attachedAge < owner.GetComponent<RunWeapons>().upgrades.stickyManualDelay) return false;
            point=lastCarrierPoint;Explode();Release();return true;
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
            info.element = element;
            info.builtInElement = weapon != null && weapon.builtInElement; info.ignoreShieldReduction = ignoreShieldReduction; info.feel = WeaponFeel.Of(weapon); info.weapon = weapon;

            if (mode is AttackMode.Field or AttackMode.Pull) info.continuousSource = (object)weapon ?? this;
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
