using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The last resort for a monster whose own walking is refused where it stands (Enemy.KeepOffEdges, WorldMap.WalkStep): after
    /// WalkRefusedLimit seconds of wanting to walk toward its goal and being allowed no step at all, not even along a wall, it is set
    /// down on the nearest ground on its way where it can stand (WorldMap.OpenGroundOnPath), and a line in the log says where. The same
    /// goes for a chaser the map gives no way at all for as long (ground closed off from what it chases). A body in a gravity ring is
    /// left to the ring, a slow kind waits at a ring's edge for a player inside it, a body backing away from the player has simply
    /// reached a wall, a boss is left to its own moves, and the void trial has its own edge rule.
    /// A raider held up by other bodies is another matter (its steps are open, it only cannot pass): it steps aside for a moment
    /// (RoundBodies).
    /// </summary>
    public partial class Enemy
    {
        const float WalkRefusedLimit = 2.5f;
        float walkRefused, noWay;

        EnemyUnstickFade unstickFade;

        /// <summary>The body is fading out to be set down elsewhere, or fading in where it was set down.</summary>
        public bool UnstickFading => unstickFade && unstickFade.Active;

        /// <summary>EnemyUnstickFade, half way: the body is out of sight, so it is moved to where it is set down.</summary>
        internal void MoveForSetDown(Vector3 ground)
        {
            if (!rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            rb.position = ground;
            transform.position = ground;
        }

        /// <summary>How many times this body has been set down on open ground since it was born (for checks).</summary>
        public int SetDownCount { get; private set; }

        void ResetWalkWatch()
        {
            walkRefused = noWay = jamClock = sidestepLeft = 0f;
            jamAnchor = Query.Flat(transform.position);
            SetDownCount = 0;
        }

        /// <summary>Counts the time a walk toward the goal is refused outright; a step taken, even a slide, starts the count again.</summary>
        void NoteWalk(Vector3 wanted, Vector3 taken, float dt)
        {
            bool refused = Query.Flat(wanted).sqrMagnitude > .01f && Query.Flat(taken).sqrMagnitude < 1e-6f;
            if (!refused || IsBoss || WorldMap.Active.GetPull(Position).sqrMagnitude > .01f)
            {
                walkRefused = 0f;
                return;
            }

            walkRefused += dt;
            if (walkRefused < WalkRefusedLimit) return;
            walkRefused = 0f;
            SetDownOnOpenGround();
        }

        /// <summary>Counts the time a chaser that is not yet at its target is given no direction (noDirection); any direction starts the
        /// count again. A slow kind waiting at a gravity ring's edge for a target inside the ring is where it should be.</summary>
        void NoteWay(bool noDirection, float dt)
        {
            var world = WorldMap.Active;
            if (!noDirection || IsBoss || !world || world.TrialActive || world.GetPull(Position).sqrMagnitude > .01f
                || world.GetPull(world.EnemyTarget).sqrMagnitude > .01f)
            {
                noWay = 0f;
                return;
            }

            noWay += dt;
            if (noWay < WalkRefusedLimit) return;
            noWay = 0f;
            SetDownOnOpenGround();
        }

        // A raider that walks and gets nowhere for JamSeconds (other bodies stand in its way: two raiders pushing at each other at a
        // corner of the base, one behind a row at the wall) walks SidestepDegrees off its way for SidestepSeconds, to one side and the
        // next time to the other, so the two pass each other and the one behind finds a free place at the wall.
        const float JamSeconds = 1.2f, JamMove = .3f, SidestepSeconds = .8f, SidestepDegrees = 70f;
        float jamClock, sidestepLeft, sidestepSide = 1f;
        Vector3 jamAnchor;

        /// <summary>The way a raider takes this step: its own, or turned aside while it steps round the bodies that hold it up. dt is the
        /// walk's own step, so a raider slowed by a status is given as much more time before it counts as held up.</summary>
        Vector3 RoundBodies(Vector3 direction, float dt)
        {
            if (direction.sqrMagnitude < .0001f)
            {
                jamClock = 0f;
                return direction;
            }

            if (sidestepLeft > 0f)
            {
                sidestepLeft -= dt;
                return Quaternion.Euler(0f, SidestepDegrees * sidestepSide, 0f) * direction;
            }

            Vector3 at = Query.Flat(Position);
            if ((at - jamAnchor).sqrMagnitude > JamMove * JamMove)
            {
                jamAnchor = at;
                jamClock = 0f;
                return direction;
            }

            jamClock += dt;
            if (jamClock < JamSeconds) return direction;
            jamClock = 0f;
            jamAnchor = at;
            sidestepLeft = SidestepSeconds;
            sidestepSide = -sidestepSide;
            return Quaternion.Euler(0f, SidestepDegrees * sidestepSide, 0f) * direction;
        }

        /// <summary>A body whose walk stays refused is set down on the nearest open ground on its way, out of sight: it fades out where
        /// it stands and in where it is set down (EnemyUnstickFade), holding still meanwhile; nothing starts while a fade runs.
        /// heldByWall: what stopped it was a wall's collider the map's walls do not cover (Enemy.NoProgress.cs), so the ground it is set
        /// down on is checked against the colliders too, and the log line says so.</summary>
        void SetDownOnOpenGround(bool heldByWall = false)
        {
            if (UnstickFading) return;
            var world = WorldMap.Active;
            bool raiding = Raider && !raidAggro && RaidSpot != null;
            if (!world || !world.OpenGroundOnPath(Position, radius, raiding, FollowingIntoGravity, out var ground, heldByWall)) return;
            Vector3 from = Position;
            ground.y = from.y;
            unstickFade = EnemyUnstickFade.Begin(this, ground);
            SetDownCount++;
            Debug.Log($"[Unstick] {(definition ? definition.kind.ToString() : name)} could not walk on at ({from.x:0.0}, {from.z:0.0})" +
                      $"{(heldByWall ? " (held by a wall the map does not know)" : "")}; set down at ({ground.x:0.0}, {ground.z:0.0})");
        }
    }
}
