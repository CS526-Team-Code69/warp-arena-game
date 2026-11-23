using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The Warden's guard duty. A Warden never attacks: it shields the monsters round it (Enemy.TickSpecial) and stands guard by one
    /// of them, its ward.
    /// Who: one living monster drawn at random from those within GuardPickRange cells of the Warden, a Spire counting
    /// GuardSpireWeight times in the draw. Never another Warden (two would only guard each other) and never a Shade (an escort
    /// would give the cloaked body away). A monster another Warden already guards is drawn only when no unguarded one is in range.
    /// With none in range the nearest anywhere is taken; with nobody at all the Warden walks up to what the monsters chase and
    /// waits GuardFreeNear to GuardFreeFar cells from it, so a wave never has to look for it.
    /// When: on its first step after its birth, and GuardRepickDelay seconds after its ward dies or leaves the fight. Until then
    /// it keeps its ward, so the pair can be read. A Warden held up far from its ward for GuardStallSeconds draws again.
    /// Where: beside the ward on the side of what the monsters chase (the player, a Mirror Image), never behind it: GuardBearing
    /// degrees off the line from the ward to that target, a gap of GuardGap cells between the two bodies, and no nearer than
    /// GuardStandoff cells to the target (a post nearer than that is swung further round the ward, as far as GuardSwingLimit, or
    /// pushed straight away from a target that stands right at the ward; either way it stays inside the Warden's shield reach
    /// of its ward). Several Wardens round one ward fan out over GuardSlots. Once there it stands
    /// still, and it moves again only when it finds itself behind its ward as the target sees it (more than GuardRearLimit degrees
    /// off that line), farther from the ward than GuardLeash, or nearer the target than GuardStandoff less GuardStandoffSlack. It
    /// walks round its ward, not through it, and outside the ring of posts (GuardArcStep, GuardLaneGap).
    /// A post must be open ground for its body with no wall between it and the ward; otherwise the mirrored side, the front and
    /// then the same places close against the ward are tried, and the ward's own place last.
    /// </summary>
    public partial class Enemy
    {
        /// <summary>How many times a Spire counts in the draw of a ward; every other monster counts once.</summary>
        public const float GuardSpireWeight = 2f;
        /// <summary>Cells round the Warden its ward is drawn from (about one screen); beyond it only the nearest monster is taken.</summary>
        public const float GuardPickRange = 30f;
        /// <summary>Seconds a Warden stands still after it loses its ward before it draws another.</summary>
        public const float GuardRepickDelay = .6f;
        /// <summary>Cells between the Warden's body and its ward's at the post.</summary>
        public const float GuardGap = 1.8f;
        /// <summary>Degrees off the line from the ward to the target at which a lone Warden posts itself: beside the ward and a little
        /// in front of it.</summary>
        public const float GuardBearing = 50f;
        /// <summary>Past this many degrees off that line the Warden counts as behind its ward and walks round to the target's side.</summary>
        public const float GuardRearLimit = 110f;
        /// <summary>The Warden keeps this far from the target; it backs off once the target is GuardStandoffSlack nearer than that.</summary>
        public const float GuardStandoff = 3.5f, GuardStandoffSlack = .7f;
        /// <summary>The farthest round its ward a post swings to keep the standoff: still on the target's side of the rear limit.</summary>
        public const float GuardSwingLimit = 100f;
        /// <summary>The farthest a standing Warden lets its ward get, centre to centre: inside its shield cast's 5 cells.</summary>
        public const float GuardLeash = 4.8f;
        /// <summary>With nobody to guard it waits between these distances from the target.</summary>
        public const float GuardFreeNear = 3.5f, GuardFreeFar = 7f;
        /// <summary>A Warden that wants to walk to its post and gets nowhere for this long settles where it is, or draws again when
        /// its ward is out of its leash.</summary>
        public const float GuardStallSeconds = 3f;

        const float GuardArrive = .45f, GuardArcStep = 60f, GuardLaneGap = 1.2f, GuardStallMove = .5f, GuardHugGap = .3f, GuardSettle = 1.5f, GuardFrontSwing = 35f;
        // Bearings of the posts round one ward, taken in this order by the Wardens that share it: both shoulders, the front, both flanks.
        static readonly float[] GuardSlots = { GuardBearing, -GuardBearing, 0f, 95f, -95f };

        static readonly System.Random GuardDice = new();
        static readonly List<Enemy> GuardCandidates = new(), GuardMates = new();
        static readonly List<float> GuardWeights = new();

        /// <summary>Tests: the rolls of the draw (uniform in [0, 1): first the ward, then the shoulder) in place of the dice.</summary>
        public static Func<float> GuardRoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetGuardRoll() => GuardRoll = null;

        Enemy ward;
        // The ward's spawn count when it was drawn and this body's own when it last looked: a body back from the pool is another monster.
        int wardVersion, guardVersion = -1;
        float guardPause, guardLook, guardSide = 1f, guardStill, guardSettle;
        bool guardMoving;
        Vector3 guardAnchor, guardPost;

        /// <summary>The monster this Warden guards now; null before its first draw, with nobody to guard, and for every other kind.</summary>
        public Enemy Ward => WardStands ? ward : null;
        /// <summary>The post the Warden last walked to (for checks).</summary>
        public Vector3 GuardPost => guardPost;
        /// <summary>The Warden stands at its post, or waits by the target with nobody to guard; false while it walks there.</summary>
        public bool AtGuardPost => guardVersion == SpawnVersion && !guardMoving && guardPause <= 0f;

        bool WardStands => ward && guardVersion == SpawnVersion && ward.SpawnVersion == wardVersion && CanGuard(ward);

        /// <summary>A monster's weight in the draw of a ward.</summary>
        public static float GuardWeight(EnemyKind kind) => kind == EnemyKind.Spire ? GuardSpireWeight : 1f;

        /// <summary>The index a roll (uniform in [0, 1)) draws from the weights: each one's share is its weight over their sum.
        /// -1 when no weight is above 0.</summary>
        public static int DrawGuard(IReadOnlyList<float> weights, float roll)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
            if (total <= 0f) return -1;
            float mark = Mathf.Clamp01(roll) * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                float weight = Mathf.Max(0f, weights[i]);
                if (weight <= 0f) continue;
                last = i;
                if (mark < weight) return i;
                mark -= weight;
            }
            return last;
        }

        static float RollGuard() => GuardRoll != null ? Mathf.Clamp01(GuardRoll()) : (float)GuardDice.NextDouble();

        bool CanGuard(Enemy other) => other && other != this && other.IsAlive && other.definition
                                      && other.definition.kind != EnemyKind.Warden && other.definition.kind != EnemyKind.Shade;

        static bool IsWarden(Enemy enemy) => enemy && enemy.IsAlive && enemy.definition && enemy.definition.kind == EnemyKind.Warden;

        bool GuardedByAnother(Enemy other)
        {
            foreach (var guard in GM.waves.Alive)
                if (guard != this && IsWarden(guard) && guard.Ward == other) return true;
            return false;
        }

        /// <summary>The Warden's step once its shield cast is done: keep its ward or draw one, then stand at its post or walk to it.</summary>
        void StandGuard(float dt)
        {
            var world = WorldMap.Active;
            Vector3 target = Query.Flat(world ? world.EnemyTarget : GM.player.Position);
            if (guardVersion != SpawnVersion)
            {
                guardVersion = SpawnVersion;
                ward = null;
                guardPause = guardLook = 0f;
                BeginGuardWalk();
            }

            if (!WardStands)
            {
                if (ward)
                {
                    ward = null;
                    guardPause = GuardRepickDelay;
                    guardLook = 0f;
                }

                if (guardPause > 0f)
                {
                    guardPause -= dt;
                    Stop();
                    return;
                }

                guardLook -= dt;
                if (guardLook <= 0f)
                {
                    DrawWard();
                    guardLook = GuardRepickDelay;
                }
            }

            if (WardStands) GuardWard(target, dt);
            else WaitByTarget(target, dt);
        }

        void BeginGuardWalk()
        {
            guardMoving = true;
            guardStill = guardSettle = 0f;
            guardAnchor = Query.Flat(Position);
        }

        /// <summary>Draws the ward: among the monsters in range the unguarded ones first, then any of them, by their weights; with
        /// none in range the nearest monster anywhere. Leaves no ward when there is nobody to guard.</summary>
        void DrawWard()
        {
            ward = null;
            var alive = GM.waves.Alive;
            float limit = GuardPickRange * GuardPickRange;
            for (int pass = 0; pass < 2 && !ward; pass++)
            {
                GuardCandidates.Clear();
                GuardWeights.Clear();
                foreach (var other in alive)
                {
                    if (!CanGuard(other) || Query.Flat(other.Position - Position).sqrMagnitude > limit) continue;
                    if (pass == 0 && GuardedByAnother(other)) continue;
                    GuardCandidates.Add(other);
                    GuardWeights.Add(GuardWeight(other.definition.kind));
                }

                if (GuardCandidates.Count == 0) continue;
                int drawn = DrawGuard(GuardWeights, RollGuard());
                if (drawn >= 0) ward = GuardCandidates[drawn];
            }

            GuardCandidates.Clear();
            if (!ward)
            {
                float nearest = float.MaxValue;
                foreach (var other in alive)
                {
                    if (!CanGuard(other)) continue;
                    float d = Query.Flat(other.Position - Position).sqrMagnitude;
                    if (d < nearest)
                    {
                        nearest = d;
                        ward = other;
                    }
                }
            }

            if (!ward) return;
            wardVersion = ward.SpawnVersion;
            guardSide = RollGuard() < .5f ? -1f : 1f;
            BeginGuardWalk();
        }

        /// <summary>Stands by the ward, or walks to the post when the Warden is behind it, too far from it or too near the target.</summary>
        void GuardWard(Vector3 target, float dt)
        {
            Vector3 at = Query.Flat(Position), centre = Query.Flat(ward.Position), mine = at - centre, toTarget = target - centre;
            float reach = toTarget.magnitude, own = mine.magnitude;
            // The line the posts are measured from runs from the ward to the target; with the target on top of the ward, the
            // Warden's own side stands in for it.
            Vector3 front = reach > .5f ? toTarget / reach : own > .1f ? mine / own : Query.Flat(facing).normalized;
            float bearing = own > .1f ? Vector3.SignedAngle(front, mine, Vector3.up) : 0f;
            // A wide ward's posts lie farther out than the leash (a boss; the shield cast does not reach a boss anyway).
            float touching = ward.radius + radius, distance = touching + GuardGap, leash = Mathf.Max(GuardLeash, distance + GuardLaneGap + .6f);
            bool wrong = own > leash || own < touching + .1f || reach > .5f && Mathf.Abs(bearing) > GuardRearLimit
                         || (at - target).magnitude < GuardStandoff - GuardStandoffSlack;
            if (!guardMoving)
            {
                if (guardSettle > 0f) guardSettle -= dt;
                else if (wrong) BeginGuardWalk();
            }

            if (!guardMoving)
            {
                Stop();
                return;
            }

            guardPost = PostBy(centre, front, target, bearing, touching, out bool atWard);
            // With no open post round its ward the Warden goes to the ward itself and is there once it stands against it.
            if ((guardPost - at).magnitude <= (atWard ? touching + GuardHugGap + .2f : GuardArrive))
            {
                // At the post. Where the post is only the best ground there is, the Warden stays on it for a while all the same.
                guardMoving = false;
                guardSettle = wrong ? GuardSettle : 0f;
                Stop();
                return;
            }

            if ((at - guardAnchor).sqrMagnitude > GuardStallMove * GuardStallMove)
            {
                guardAnchor = at;
                guardStill = 0f;
            }
            else if ((guardStill += dt) >= GuardStallSeconds)
            {
                guardStill = 0f;
                if (own <= leash)
                {
                    // Held up within reach of its ward (other bodies stand on the post): it stays where it got to.
                    guardMoving = false;
                    guardSettle = GuardSettle;
                    Stop();
                    return;
                }

                // Held up far from its ward: it looks for one it can get to.
                ward = null;
                guardPause = guardLook = 0f;
                Stop();
                return;
            }

            GuardWalk(guardPost, target, dt, own > leash);
        }

        /// <summary>Where this Warden should stand by its ward now: its slot's bearing off front (reached round the ward by at most
        /// GuardArcStep degrees a leg), a gap from the ward, kept at the standoff from the target (OpenPost); the first of the
        /// fallbacks that is open ground when that is not, and the ward's own place (atWard) when none is.</summary>
        Vector3 PostBy(Vector3 centre, Vector3 front, Vector3 target, float bearing, float touching, out bool atWard)
        {
            atWard = false;
            float distance = touching + GuardGap, hug = touching + GuardHugGap;
            float slot = SlotBearing(centre, front, distance, bearing);
            float turn = Mathf.DeltaAngle(bearing, slot);
            float leg = Mathf.Abs(turn) > GuardArcStep ? bearing + Mathf.Sign(turn) * GuardArcStep : slot;
            // On its way round, the Warden keeps GuardLaneGap outside the ring of posts: past a Warden that already stands on one.
            if (leg != slot && OpenPost(centre, front, target, leg, distance + GuardLaneGap, touching, out var post)
                || OpenPost(centre, front, target, leg, distance, touching, out post)
                || leg != slot && OpenPost(centre, front, target, slot, distance, touching, out post)
                || OpenPost(centre, front, target, -slot, distance, touching, out post)
                || OpenPost(centre, front, target, 0f, distance, touching, out post)
                || OpenPost(centre, front, target, slot, hug, touching, out post)
                || OpenPost(centre, front, target, -slot, hug, touching, out post)
                || OpenPost(centre, front, target, 0f, hug, touching, out post)) return post;
            atWard = true;
            return centre;
        }

        /// <summary>The post at this bearing and distance from the ward. One nearer the target than the standoff is swung further round
        /// the ward, away from the target, to where the standoff is kept, as far as GuardSwingLimit; with the target too close to the
        /// ward for that it is pushed straight away from the target instead, where that keeps it off the ward's body. The plain
        /// post when only that is open ground; false when nothing is.</summary>
        bool OpenPost(Vector3 centre, Vector3 front, Vector3 target, float bearing, float distance, float touching, out Vector3 post)
        {
            post = centre + Quaternion.Euler(0f, bearing, 0f) * front * distance;
            Vector3 fromTarget = post - target;
            if (fromTarget.magnitude < GuardStandoff)
            {
                // front points from the ward at the target, so the target stands at bearing 0, reach cells out.
                float reach = (target - centre).magnitude;
                float cosine = reach > .01f ? (distance * distance + reach * reach - GuardStandoff * GuardStandoff) / (2f * distance * reach) : -1f;
                float swing = cosine >= 1f ? 0f : cosine <= -1f ? 180f : Mathf.Acos(cosine) * Mathf.Rad2Deg + 1f;
                // The front post swings out past the shoulder posts, which swing to the same bearing.
                if (Mathf.Abs(bearing) <= .01f) swing = Mathf.Min(swing + GuardFrontSwing, Mathf.Max(swing, GuardSwingLimit));
                if (swing <= GuardSwingLimit)
                {
                    float side = Mathf.Abs(bearing) > .01f ? Mathf.Sign(bearing) : guardSide;
                    Vector3 swung = centre + Quaternion.Euler(0f, side * Mathf.Max(swing, Mathf.Abs(bearing)), 0f) * front * distance;
                    if (GuardGroundOpen(swung, centre))
                    {
                        post = swung;
                        return true;
                    }
                }

                Vector3 away = fromTarget.sqrMagnitude > .01f ? fromTarget.normalized : Query.Flat(Position - target).normalized;
                Vector3 pushed = target + away * GuardStandoff;
                if (away.sqrMagnitude > .5f && (pushed - centre).magnitude >= touching + GuardHugGap && GuardGroundOpen(pushed, centre))
                {
                    post = pushed;
                    return true;
                }
            }

            return GuardGroundOpen(post, centre);
        }

        /// <summary>Ground this body can stand on with no wall between it and the ward; in the void trial, the trial's own ground.</summary>
        bool GuardGroundOpen(Vector3 point, Vector3 centre)
        {
            var world = WorldMap.Active;
            if (!world) return true;
            if (world.TrialActive) return world.OnTrialGround(point, radius) && !world.IsHole(point);
            return world.IsWalkable(point, radius) && !Query.WallBetween(Query.AtCastHeight(centre), Query.AtCastHeight(point), out _);
        }

        /// <summary>The bearing of this Warden's post. Alone by its ward it takes the shoulder on the side it is on. Wardens that share
        /// a ward take GuardSlots in order, each slot going to the nearest of them that has none yet.</summary>
        float SlotBearing(Vector3 centre, Vector3 front, float distance, float bearing)
        {
            GuardMates.Clear();
            foreach (var guard in GM.waves.Alive)
                if (guard == this || IsWarden(guard) && guard.Ward == ward) GuardMates.Add(guard);
            if (GuardMates.Count <= 1)
            {
                GuardMates.Clear();
                if (Mathf.Abs(bearing) > 15f && Mathf.Abs(bearing) < 165f) guardSide = Mathf.Sign(bearing);
                return guardSide * GuardBearing;
            }

            float found = guardSide * GuardBearing;
            for (int slot = 0; slot < GuardMates.Count; slot++)
            {
                float angle = GuardSlots[slot % GuardSlots.Length];
                Vector3 point = centre + Quaternion.Euler(0f, angle, 0f) * front * distance;
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < GuardMates.Count; i++)
                {
                    if (!GuardMates[i]) continue;
                    float d = Query.Flat(GuardMates[i].Position - point).sqrMagnitude;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }

                if (best < 0) break;
                if (GuardMates[best] == this)
                {
                    found = angle;
                    break;
                }

                GuardMates[best] = null;
            }

            GuardMates.Clear();
            return found;
        }

        /// <summary>One step toward a point at walking speed, round walls as every walk goes. A step that would take the Warden nearer
        /// the target than its standoff allows is turned along the standoff instead. noted: a walk refused for long sets the body
        /// down on open ground (Enemy.Unstick.cs); a short shift round its ward is not such a walk.</summary>
        void GuardWalk(Vector3 point, Vector3 target, float dt, bool noted)
        {
            var world = WorldMap.Active;
            Vector3 direction = world
                ? world.GetChaseDirection(Position, point, FollowingIntoGravity, radius)
                : Query.Flat(point - Position).normalized;
            Vector3 toTarget = target - Query.Flat(Position);
            float away = toTarget.magnitude;
            if (away > .01f && away < GuardStandoff - GuardStandoffSlack)
            {
                Vector3 inward = toTarget / away;
                float closing = Vector3.Dot(direction, inward);
                if (closing > 0f)
                {
                    direction -= inward * closing;
                    direction = direction.sqrMagnitude > .01f ? direction.normalized : Vector3.zero;
                }
            }

            Vector3 v = KeepOffEdges(direction * moveSpeed, dt, noted);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        /// <summary>Nobody to guard: the Warden comes up to what the monsters chase and waits GuardFreeNear to GuardFreeFar cells
        /// from it, backing off when it comes closer.</summary>
        void WaitByTarget(Vector3 target, float dt)
        {
            float away = (target - Query.Flat(Position)).magnitude;
            if (!guardMoving && (away > GuardFreeFar || away < GuardFreeNear - GuardStandoffSlack)) BeginGuardWalk();
            if (!guardMoving)
            {
                Stop();
                return;
            }

            float middle = (GuardFreeNear + GuardFreeFar) * .5f;
            if (away > middle) GuardWalk(target, target, dt, true);
            else if (away < GuardFreeNear) MoveTowards(target, -1f, dt);
            else
            {
                guardMoving = false;
                Stop();
            }
        }
    }
}
