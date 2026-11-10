using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>A saved pooled attack. It owns its timers and releases every borrowed movement modifier.</summary>
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
        [Tooltip("Scatter Blast shell fire (2026-10-27): it touches its shooter only with the shooter's centre inside it, as if stepped on; others by their edge as usual.")]
        public bool ownerStepsOn;
        // A ground field's self-damage below 1 per tick: what one tick could not deal waits for the next, so the 1-damage floor
        // does not round every tick up.
        float ownerCarry;
        /// <summary>Bodies already hit by any shot of the same volley (Shatter's three shards); null when the shot stands alone.</summary>
        [System.NonSerialized] public HashSet<Combatant> sharedStruck;
        public int Sequence { get; private set; }
        public bool IsReturning => returning;
        public bool PlantedRecall { get; private set; }
        public bool LobInFlight => mode == AttackMode.Lob || mode == AttackMode.Recall && !PlantedRecall;
        /// <summary>A black hole swallows every projectile, lobs still in the air included (rockets stuck to a body too); ground fields are not swallowed.</summary>
        public bool AffectedByHoles => mode is not (AttackMode.Field or AttackMode.Pull or AttackMode.Blast);
        public bool KnifeReturning => mode == AttackMode.Knife && returning;
        /// <summary>A Chain Blade hook pulling the enemy it caught (and its thrower toward it, RunWeapons.ReelPlayer).</summary>
        public bool HookReeling => mode == AttackMode.Hook && carrier;
        // Charged Shot Twist: a full-charge round deals pierceGrowth of its first hit more for every enemy it has passed
        // through, at most pierceGrowthCap more. Read on rifle rounds only; Configure clears it, RunWeapons sets it on each full-charge round.
        [System.NonSerialized] public float pierceGrowth, pierceGrowthCap;
        // Flying Blade Tame: the return flies this many times as fast (the throw after a catch). Read in Knife mode only; Configure resets it.
        [System.NonSerialized] public float knifeSpeedFactor = 1f;
        float pierceBase;
        Combatant knifeTurnBody; // the enemy the knife turned back from: its return starts touching it, so that touch is not a cut
        readonly List<(Combatant body, float enter)> knifeSweep = new(); // one return step's bodies, in the order the blade touches them
        // The knife no longer carries corpses (10-21 design). These stay inert for WorldMap and the attack presentation.
        public bool HasKnifeCorpse => false;
        public float KnifeCorpseRadius => 0f;
        public Sprite KnifeCorpseSprite => null;
        public Enemy KnifeCorpseSource => null;
        public int KnifeCorpseSpawnVersion => 0;
        public Vector3 KnifeCorpsePosition => Query.Flat(transform.position);
        public static System.Func<Enemy,int,bool> ClaimKnifeCorpse;
        public static System.Action<Enemy,int,Vector3> DropKnifeCorpse;
        public Vector3 KnifeReturnPoint { get; private set; }
        /// <summary>Where the owner stood at launch, readable before the return starts (Throwing Knife Twist marker).</summary>
        public Vector3 KnifeHomePoint => Query.Flat(launchPoint);
        public float KnifeSpinAngle { get; private set; }
        static int nextSequence;
        const float KnifeRadius = .35f;
        // A Barrage rocket must fly this far before it can burst on its own shooter.
        const float SwarmOwnerArmDistance = 1f;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly HashSet<Combatant> struck = new();
        readonly Collider[] knifeContacts = new Collider[128];
        readonly HashSet<Combatant> knifeReturnHits = new();
        readonly HashSet<Collider> passedWalls = new();
        readonly List<(Combatant body, Vector3 dir)> blastTargets = new();
        readonly List<Vector3> chainPoints = new();
        float flown, tick;
        bool returning, returnThroughWall, lostToHole, hookRetracting, stoppedOnWall;
        float attachedAge;
        Combatant knifeSeekTarget;
        Combatant carrier;
        float attachedLeft;
        bool attached;
        Vector3 lastCarrierPoint;
        Vector3 arcStart;
        Vector3 launchDirection;
        Vector3 launchPoint; // where the owner stood at launch; a knife flies back there (design document 6.5)
        bool crossed; // Lance Twist: this slug already burst on the previous trail
        Combatant hiveHost; // Barrage Twist: the body this rocket is stuck to; every rocket on it merges into one blast
        // Barrage: the body this missile is locked on (an enemy, or its shooter for the backfire) and that body's spawn
        // version; seconds of straight flight before it homes; seconds left circling its shooter (Tame escort) and where on the circle.
        Combatant homeTarget; int homeVersion; float homeDelay, escortLeft, escortAngle;
        public Combatant HomeTarget => homeTarget;
        public bool Escorting => escortLeft > 0f;
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
            struck.Clear(); knifeReturnHits.Clear(); passedWalls.Clear(); returning = returnThroughWall = lostToHole = attached = hookRetracting = stoppedOnWall = false;
            attachedAge = 0; internalWallsLeft = 0; splitChild = chainChild = false;
            knifeSeekTarget = null; KnifeSpinAngle = 0; KnifeReturnPoint = Vector3.zero;
            attachedLeft = 0; carrier = null;
            crossed = false; hiveHost = null;
            homeTarget = null; homeVersion = 0; homeDelay = escortLeft = escortAngle = 0f;
            PlantedRecall = false; arcElapsed = arcDuration = arcHeight = landedDuration = 0f;
            hurtOwner = ignoreShieldReduction = followCursor = pierce = false;
            ownerDamage = -1f; ownerCarry = 0f; sharedStruck = null;
            ownerStepsOn = false;
            ricochets = 0; targetsLeft = 4; element = w?.element ?? ElementKind.None;
            pierceGrowth = pierceGrowthCap = 0f; knifeSpeedFactor = 1f; knifeTurnBody = null; // starting weapons
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

        /// <summary>Deflect: a lob in the air turns toward a new landing point. The arc goes on from where the lob is
        /// now, with the same height and time left: its start moves so the ground path still passes through this point.</summary>
        public void BendArc(Vector3 destination)
        {
            if (mode != AttackMode.Lob || arcDuration <= 0f) return;
            float t = arcElapsed / arcDuration;
            if (t >= .99f) return;
            Vector3 now = Query.Flat(transform.position), end = Query.Flat(destination), start = (now - end * t) / (1f - t);
            arcStart = new Vector3(start.x, arcStart.y, start.z);
            point = end;
        }

        public void Release()
        {
            // Lance Twist: a slug that stops leaves its real path as the trail the next slug can cross (a black hole ends it at its edge).
            if (mode == AttackMode.Bolt && IsLanceTwist && gameObject.activeSelf) Shooter?.LanceTrail(weapon, launchPoint, transform.position);
            // Lance rail beam: the beam ends exactly where the slug stops.
            if (mode == AttackMode.Bolt && weapon != null && weapon.form == WeaponForm.Railgun && weapon.variant == 0 && gameObject.activeSelf) Shooter?.LanceSlugStopped(this);
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
                    // A knife lost to a black hole gets its own longer wait instead (RunWeapons.KnifeLostToHole).
                    if (!lostToHole) weapon.cooldown = Mathf.Max(weapon.cooldown, GameManager.Instance.runWeapons.ReturnCooldown(weapon));
                }
                if (mode == AttackMode.Hook)
                {
                    weapon.hookBusy = false;
                    // Came back empty: the re-throw lock starts now (a caught enemy starts it when it is flung instead).
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
        /// <summary>WorldMap calls this instead of Release when the attack enters a black hole. A swallowed throwing knife
        /// vanishes and comes back to its slot after a wait, so a lost knife never leaves the player without an attack.</summary>
        public void SwallowedByHole()
        {
            var lost = mode == AttackMode.Knife ? weapon : null;
            var weapons = lost != null && owner ? owner.GetComponent<RunWeapons>() : null;
            lostToHole = lost != null;
            Release();
            if (weapons) weapons.KnifeLostToHole(lost);
        }
        /// <summary>Inert: the knife no longer carries corpses.</summary>
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
                // Barrage Twist: the warning follows the body and shows the merged radius until the blast; one rocket of the hive draws it.
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
            if (mode == AttackMode.Swarm && SteerSwarm(dt)) return;
            float distance = speed * dt;
            int count = Physics.SphereCastNonAlloc(transform.position, .16f, direction, hits, distance,
                GameManager.WallMask | GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, count, HitComparer.Instance);
            if (IsLanceTwist && !crossed) CheckLanceCross(StepToWall(count, distance));
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                // A cast that starts inside a collider reports distance 0 and point (0,0,0): use where the shot is and face back.
                bool startInside = hit.distance <= 0f;
                Vector3 hitPoint = startInside ? transform.position : hit.point, hitNormal = startInside ? -direction : hit.normal;
                var body = Combatant.From(hit.collider);
                if (body == owner)
                {
                    // Barrage backfire: a rocket that sweeps into its own shooter bursts on them.
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
                    // Just off the surface, so the blast's wall check does not start inside this wall.
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
                // A Barrage missile locked onto its shooter bursts on whatever it meets; it never sticks.
                if (mode == AttackMode.Swarm && weapon != null && weapon.Has(UpgradeAxis.Twist) && homeTarget != owner) { StickToHive(body); return; }
                if (mode is AttackMode.Bouncy or AttackMode.Swarm)
                { transform.position = hitPoint; Explode(); Release(); return; }
                if (mode == AttackMode.Shard)
                {
                    // Shatter: the weapon resolves the hit (frozen bonus, Twist chain) and leaves the ice patch.
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
                // Charged Shot Twist (Q3): each enemy already passed through adds pierceGrowth of the first hit, up to the cap.
                if (pierceGrowth > 0f && weapon != null && weapon.form == WeaponForm.Rifle && weapon.variant == 0)
                { if (struck.Count == 0) pierceBase = damage; damage = pierceBase * (1f + Mathf.Min(pierceGrowthCap, pierceGrowth * struck.Count)); }
                struck.Add(body);
                Hit(body, damage, knock, direction);
                if (weapon != null && weapon.form == WeaponForm.SporeLauncher && weapon.variant == 1 && weapon.Has(UpgradeAxis.Twist)) Shooter?.NeedleLanded(this, body);
                if (!body.IsAlive && weapon != null && weapon.form == WeaponForm.Minigun && weapon.variant == 1 && weapon.Has(UpgradeAxis.Twist))
                    Shooter?.MeltdownKillVent(weapon, body);
                damage *= pierceDamageRetention;
                if (!pierce || piercesLeft-- <= 0) { Release(); return; }
            }
            transform.position += direction * distance; flown += distance;
            if (flown >= range && mode != AttackMode.Bouncy && !(mode == AttackMode.Swarm && homeTarget))
            {
                // A Barrage missile with nothing to chase bursts at the end of its range; a locked one keeps after its target.
                if (mode == AttackMode.Swarm) Explode();
                if (mode == AttackMode.Shard) Shooter?.ShardLanded(this, Query.Flat(transform.position));
                Release();
            }
        }

        RunWeapons Shooter => owner ? owner.GetComponent<RunWeapons>() : null;

        /// <summary>Reverse Shot Twist: the bounced round becomes two, either side of the bounce, each with a share of its damage.
        /// Neither splits or bounces again; the second keeps this round's remaining range and life.</summary>
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

        /// <summary>How far this step really goes: up to the first wall the cast found, or the whole step.</summary>
        float StepToWall(int count, float step)
        {
            for (int i = 0; i < count; i++)
                if (!Combatant.From(hits[i].collider) && !passedWalls.Contains(hits[i].collider)) return Mathf.Min(step, Mathf.Max(0f, hits[i].distance));
            return step;
        }

        /// <summary>Lance Twist: this step's segment against the previous slug's trail on the ground. A real crossing (not parallel,
        /// not overlapping) bursts once at the crossing point. A fast slug covers about 2.4 cells per physics step, so segments, not points.</summary>
        void CheckLanceCross(float step)
        {
            if (weapon.lanceTrailLeft <= 0f || step <= 0f) return;
            Vector3 from = Query.Flat(transform.position), to = from + direction * step;
            if (!SegmentsCross(from, to, Query.Flat(weapon.lanceTrailFrom), Query.Flat(weapon.lanceTrailTo), out var at)) return;
            crossed = true;
            Shooter?.LanceCross(this, at);
        }

        /// <summary>Intersection of segments ab and cd on the ground plane; parallel or collinear segments never cross.</summary>
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

        /// <summary>Barrage: locks this missile on target (null: it flies straight on). It flies straight for delay seconds first, then turns
        /// straight back for its target (the backfire's missile locked onto its shooter) and homes in.</summary>
        public void HomeOn(Combatant target, float delay)
        {
            homeTarget = target; homeVersion = target ? target.SpawnVersion : 0; homeDelay = Mathf.Max(0f, delay);
        }

        /// <summary>Barrage Tame with no enemy to lock: the missile circles its shooter for seconds, starting at this angle (degrees).</summary>
        public void EscortOwner(float seconds, float angle)
        {
            homeTarget = null; escortLeft = Mathf.Max(0f, seconds); escortAngle = angle;
            remaining = Mathf.Max(remaining, escortLeft + range / Mathf.Max(1f, speed) + 3f);
        }

        /// <summary>Barrage: an escorting missile circles its shooter until an enemy comes within its range (then locks it) or its time runs
        /// out (then it vanishes without a blast). A locked missile turns toward its target at barrageTurnRate, straight at it once close;
        /// one whose target died picks the visible enemy nearest itself, or with none flies on to the end of its range. True when this step is done.</summary>
        bool SteerSwarm(float dt)
        {
            var weapons = Shooter; if (!weapons) return false;
            var u = weapons.upgrades;
            if (escortLeft > 0f)
            {
                if (!owner || !owner.IsAlive) { Release(); return true; }
                var found = weapons.BarrageEscortTarget(weapon, range);
                if (!found)
                {
                    escortLeft -= dt;
                    if (escortLeft <= 0f) { Release(); return true; }
                    escortAngle += u.barrageEscortSpin * dt;
                    direction = Quaternion.Euler(0f, escortAngle + 90f, 0f) * Vector3.forward;
                    Vector3 spoke = Quaternion.Euler(0f, escortAngle, 0f) * Vector3.forward, centre = Query.AtCastHeight(owner.Position);
                    float reach = Mathf.Max(0f, u.barrageEscortRadius);
                    // It never passes a wall: where a wall cuts the circle, the missile keeps to your side of it (cast from you, never from inside a wall).
                    if (Physics.SphereCast(centre, .16f, spoke, out var wall, reach, GameManager.WallMask, QueryTriggerInteraction.Ignore)) reach = Mathf.Max(0f, wall.distance - .05f);
                    transform.position = centre + spoke * reach;
                    return true;
                }
                escortLeft = 0f; HomeOn(found, 0f); flown = 0f;
                remaining = range / Mathf.Max(1f, speed) + 3f;
            }
            if (!homeTarget) return false;
            bool turnBack = false;
            if (homeDelay > 0f) { homeDelay -= dt; if (homeDelay > 0f) return false; turnBack = true; }
            if (!homeTarget.IsAlive || homeTarget.SpawnVersion != homeVersion)
            {
                var next = homeTarget == owner ? null : weapons.BarrageNearest(transform.position, float.PositiveInfinity);
                if (!next) { homeTarget = null; return false; }
                HomeOn(next, 0f);
            }
            Vector3 to = Query.Flat(homeTarget.Position - transform.position);
            if (to.sqrMagnitude < 1e-4f) return false;
            float close = u.barrageTerminalRange + homeTarget.radius, turn = Mathf.Max(0f, u.barrageTurnRate) * dt;
            direction = turnBack || to.sqrMagnitude <= close * close ? to.normalized
                : Quaternion.Euler(0f, Mathf.Clamp(Vector3.SignedAngle(direction, to, Vector3.up), -turn, turn), 0f) * direction;
            return false;
        }

        /// <summary>Barrage Twist: the rocket sticks to the body it touched and waits for the merged blast. A rocket joining a body
        /// that already carries rockets takes their remaining fuse, so they all go off together. It does not slow or speed the body.</summary>
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
            // A warning ring that grows with every rocket on the body, so you can tell how far to back off.
            if (weapons) Fx.Ring(lastCarrierPoint, HiveRadius(weapons, HiveCount()), new Color(1f, .6f, .3f, .7f), .2f);
        }

        // Seconds between redraws of the Barrage Twist warning ring.
        const float HiveRingInterval = .1f;

        /// <summary>The oldest rocket on the body draws the hive's warning ring, so it is drawn once, not once per rocket.</summary>
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

        /// <summary>The first rocket on a body to reach its fuse takes every other rocket stuck there: damage and self-damage add up,
        /// and the radius grows per extra rocket up to the cap. The others go quietly.</summary>
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
                return; // A Recall dagger owns its pool slot until the second click or explicit cleanup.
            }
            if (weapon?.form == WeaponForm.Flamethrower && weapon.variant == 1)
                owner.GetComponent<RunWeapons>()?.WildfireLanded(weapon);
            mode = AttackMode.Field;
            direction = launchDirection;
            remaining = landedDuration;
            tick = 0f;
            transform.localScale = new Vector3(radius * 2f, .035f, radius * 2f);
            // Cloud Twist: a spore pod the Tame kicked bursts where it lands and may set off a cloud there.
            if (weapon?.form == WeaponForm.SporeLauncher && weapon.variant == 0) Shooter?.SporeLobLanded(this);
        }

        /// <param name="turnBody">The enemy the knife just struck and turns back from; the return starts touching it, which is no cut.</param>
        void BeginReturn(Combatant turnBody = null)
        {
            if (returning) return;
            returning = true; returnThroughWall = false; knifeTurnBody = turnBody; knifeSeekTarget = null; knifeReturnHits.Clear();
            // Design document 6.5: the knife flies back to where the player stood when it was thrown, so walking away dodges its return.
            KnifeReturnPoint = launchPoint;
            // Every return cuts now, so each one plays the cue that used to mark the kill return.
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponKnifeReturn, transform.position);
        }
        /// <summary>A wall stands between the returning knife's step (from-next) and body; checked both ways, as the step may start inside a wall.</summary>
        static bool WallHides(Vector3 from, Vector3 next, Combatant body)
        {
            Vector3 at = Query.AtCastHeight(body.Position), step = next - from;
            float t = step.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(at - from, step) / step.sqrMagnitude) : 0f;
            Vector3 near = from + step * t; near.y = at.y;
            return Query.WallBetween(near, at, out _) || Query.WallBetween(at, near, out _);
        }
        /// <summary>Flying Blade Tame (Q13): a return that sweeps through you is caught. It is back in your hand at once, its
        /// cooldown is knifeCatchCooldownShare of the usual one and the next throw gets the catch bonus. With the Twist it spins where you
        /// caught it, since a caught knife never reaches the throw point (the two cards of one combination should not cancel out).</summary>
        void CatchKnife(RunWeapons weapons)
        {
            weapon.knifeCaught = true;
            if (weapon.Has(UpgradeAxis.Twist)) KnifeSpin(weapons, owner.Position);
            Release();
            if (weapons) weapon.cooldown = weapons.ReturnCooldown(weapon) * Mathf.Clamp01(weapons.upgrades.knifeCatchCooldownShare);
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
                float step = (weapons ? weapons.knifeReturnSpeed : 14f) / cycle * Mathf.Max(1f, knifeSpeedFactor) * dt;
                if (to.sqrMagnitude > .000001f) direction = to.normalized;
                Vector3 next = Query.AtCastHeight(from + direction * Mathf.Min(step, to.magnitude));
                // Document 5.5, Q10/Q11: every return cuts what it sweeps, enemies and its thrower alike, each once per
                // return; the whole step is swept so a fast return cannot skip a body. It still flies home through walls, but a wall shields
                // what lies behind it: past the first wall it crosses it cuts nothing more. Tame: it never cuts you, and sweeping through you
                // is a catch (Q13). The step's bodies are taken in the order the blade touches them, so a catch stops it where it meets you.
                float wallAlong = Physics.Linecast(from, next, out var returnWall, GameManager.WallMask, QueryTriggerInteraction.Ignore) ? returnWall.distance : float.PositiveInfinity;
                int count = Physics.OverlapCapsuleNonAlloc(from, next, KnifeRadius,
                    knifeContacts, GameManager.CharacterMask, QueryTriggerInteraction.Ignore);
                knifeSweep.Clear();
                for (int i = 0; i < count; i++)
                {
                    var body = Combatant.From(knifeContacts[i]);
                    if (!body || !body.IsAlive || body == knifeTurnBody || WallHides(from, next, body)) continue;
                    // Where along the step the blade first touches the body (as on the way out).
                    Vector3 delta = Query.Flat(body.Position - from);
                    float along = Vector3.Dot(delta, direction), combined = body.radius + KnifeRadius;
                    float perpendicularSq = Mathf.Max(0f, delta.sqrMagnitude - along * along);
                    knifeSweep.Add((body, along - Mathf.Sqrt(Mathf.Max(0f, combined * combined - perpendicularSq))));
                }
                knifeSweep.Sort((a, b) => a.enter.CompareTo(b.enter));
                foreach (var (body, enter) in knifeSweep)
                {
                    if (!body.IsAlive) continue;
                    if (body == owner && weapon.Has(UpgradeAxis.Tame)) { CatchKnife(weapons); return; }
                    if (returnThroughWall || enter > wallAlong || !knifeReturnHits.Add(body)) continue;
                    // The thrower is cut for the weapon's base value, enemies for the scaled hit.
                    HitKnife(body, body == owner && ownerDamage >= 0f ? ownerDamage : damage, 0f, HitKind.Pierce);
                }
                if (wallAlong < float.PositiveInfinity) returnThroughWall = true;
                transform.position = next;
                if (to.magnitude <= step)
                {
                    if (weapon != null && weapon.Has(UpgradeAxis.Twist)) KnifeSpin(weapons, KnifeReturnPoint);
                    Release();
                }
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
                // Q10: a kill no longer cuts the bounce short; the knife goes on until its targets run out.
                knifeSeekTarget = --targetsLeft > 0 ? NextKnifeTarget(struckPoint) : null;
                if (!knifeSeekTarget) BeginReturn(first);
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

        /// <summary>Flying Blade Twist: the knife comes home and spins once there (or where you catch it, CatchKnife), cutting enemies around
        /// it that this return has not cut yet. Never the shooter; walls shield bodies behind them.</summary>
        void KnifeSpin(RunWeapons weapons, Vector3 centre)
        {
            float spin = weapons ? weapons.upgrades.knifeSpinRadius : 1.5f, share = weapons ? weapons.upgrades.knifeSpinShare : .8f;
            Vector3 home = Query.Flat(centre);
            blastTargets.Clear();
            foreach (var body in Combatant.All)
            {
                if (body == owner || !body.IsAlive || knifeReturnHits.Contains(body)) continue;
                Vector3 away = Query.Flat(body.Position) - home;
                if (away.sqrMagnitude > (spin + body.radius) * (spin + body.radius)) continue;
                if (Query.WallBetween(Query.AtCastHeight(home), Query.AtCastHeight(body.Position), out _)) continue;
                blastTargets.Add((body, away.sqrMagnitude > 0 ? away.normalized : direction));
            }
            // Collected first: a kill can wake a pooled enemy, which joins Combatant.All.
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
            // Needler Tame: a reload mist left before the card was taken spares you too.
            if (hurtOwner && weapon?.form == WeaponForm.SporeLauncher && weapon.variant == 1 && weapon.Has(UpgradeAxis.Tame)) hurtOwner = false;
            // Wildfire Tame: your own patches stop burning you, patches thrown before the card included.
            if (hurtOwner && weapon?.form == WeaponForm.Flamethrower && weapon.variant == 1 && weapon.Has(UpgradeAxis.Tame)) hurtOwner = false;
            // Scatter Blast Tame: its shell fires stop burning you too, from their next tick, ones lit before the card included.
            if (hurtOwner && weapon?.form == WeaponForm.ScatterCannon && weapon.variant == 0 && weapon.Has(UpgradeAxis.Tame)) hurtOwner = false;
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
                // Wildfire Twist: each tick also joins this patch to the newer ones by fire fences.
                if (weapon?.form == WeaponForm.Flamethrower && weapon.variant == 1 && weapon.Has(UpgradeAxis.Twist)) Shooter?.WildfireFence(this);
            }
        }
        /// <summary>Hook Twist chain: the hook starts already holding target and reels it in like a normal catch.</summary>
        public void BeginCarry(Combatant target)
        {
            if (!target || !target.IsAlive) return;
            carrier = target; carrier.SetState(CharacterState.Hooked);
            Shooter?.GuardReel(weapon); // Chain Blade Tame: guarded from the catch on
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
                float pull = (weapons ? weapons.hookPullSpeed : 25f) / cycle * dt;
                // Document 5.5: the hook drags its thrower toward the enemy as fast as it reels the enemy in, so the two
                // meet halfway (the hold point moves with the thrower).
                Vector3 reeled = weapons ? weapons.ReelPlayer(weapon, carrier, pull) : Vector3.zero;
                Vector3 destination = (weapons ? weapons.HookHoldPoint(carrier) : owner.Position + owner.AimDirection * (owner.radius + carrier.radius + .3f)) + reeled;
                Vector3 next = Vector3.MoveTowards(carrier.Position, destination, pull);
                carrier.SetMovePosition(next); transform.position = Query.AtCastHeight(next);
                if ((next - destination).sqrMagnitude <= .01f)
                { weapon.hooked = carrier; carrier = null; if (weapons) weapons.HookMet(weapon, weapon.hooked); Release(); }
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
            // Every hit of the step, nearest first: an enemy the head already overlaps (one touching you) comes back at distance 0,
            // which a single SphereCast would skip, so the hook would fly through it.
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
                    // The head deals one scaled capture hit, then the carrier branch only pulls; never hit every physics frame.
                    Hit(target,Mathf.Max(1f,damage),0f,direction);
                    if(target.IsAlive){carrier=target;carrier.SetState(CharacterState.Hooked);if(weapons)weapons.GuardReel(weapon);}
                    else{flown+=reached;transform.position=Query.AtCastHeight(owner.Position+direction*flown);hookRetracting=true;}
                    return;
                }
                // Tame: an empty hook that hits a wall pulls you toward it, as a push (no wall damage, you keep control).
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
            // 先收集再结算：击杀可能唤醒新敌人并加入 Combatant.All，边遍历边结算会打断循环。
            blastTargets.Clear();
            // Walls shield bodies behind them; only the Rocket Launcher strike falls from the sky and ignores them.
            bool strike = mode == AttackMode.Blast && weapon?.form == WeaponForm.RocketLauncher && weapon.variant == 0;
            bool overWalls = strike && !chainChild;
            foreach (var body in Combatant.All)
            {
                if (!body.IsAlive) continue;
                Vector3 away = Query.Flat(body.Position) - point;
                if (away.sqrMagnitude > (radius + body.radius) * (radius + body.radius)) continue;
                if (body == owner && ownerStepsOn && away.sqrMagnitude > radius * radius) continue;
                if (!overWalls && Query.WallBetween(Query.AtCastHeight(point), Query.AtCastHeight(body.Position), out _)) continue;
                // Bombard Tame: your own blast neither hurts nor pushes you; the first touch of a shot shortens its cooldown.
                if (body == owner && weapon != null && weapon.form == WeaponForm.SwarmLauncher && weapon.variant == 1 && weapon.Has(UpgradeAxis.Tame))
                { Shooter?.BombardSelfTouch(weapon); continue; }
                bool keepSelfPush=weapon!=null&&weapon.Has(UpgradeAxis.Tame)&&weapon.form==WeaponForm.PlasmaLauncher&&weapon.variant==1;
                if(body==owner&&(!hurtOwner||keepSelfPush))
                {
                    // Strike Tame: the blast throws you clear as a push (a rocket jump); point blank it sends you along the aim.
                    if(strike&&!chainChild&&weapon.Has(UpgradeAxis.Tame))
                        owner.Push(away.sqrMagnitude>.09f?away:owner.AimDirection,Shooter?Shooter.upgrades.strikeJumpDistance:6f);
                    else if(strike&&!chainChild || keepSelfPush)
                        owner.Knockback(knock<0?-away:away,Mathf.Abs(knock),KnockKind.Hit);
                    continue;
                }
                blastTargets.Add((body, away.sqrMagnitude > 0 ? away.normalized : direction));
            }
            // Strike Twist: every enemy this strike kills blasts again where it stood (StrikeChain); chain blasts never chain.
            bool chain = strike && !chainChild && weapon.Has(UpgradeAxis.Twist);
            chainPoints.Clear();
            // The shooter caught in its own blast takes the weapon's base value, not the scaled one.
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
            // Illustrated art: a Field or Pull tick is drawn by its presentation (real fire, gas, snow); anything else that bursts is a real explosion.
            if (V5Vfx.On) { if (mode is not (AttackMode.Field or AttackMode.Pull)) V5Vfx.I.Explosion(point, radius, element == ElementKind.None ? null : V5Vfx.ElementTint(element)); }
            else Fx.Ring(point, radius, ElementCombat.ColorFor(element));
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
            // A ground field (fire patch, poison cloud or mist) hits every tick: for stacks its weapon counts once per interval on each body.
            if (mode is AttackMode.Field or AttackMode.Pull) info.continuousSource = (object)weapon ?? this;
            if(target.ReceiveDamage(info))PlaySound(WeaponAudioPhase.Impact);
        }
        void PlaySound(WeaponAudioPhase phase)
        {
            V5WeaponAudio.Weapon(weapon,phase);
        }
        sealed class HitComparer : IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new();
            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }
    }
}
