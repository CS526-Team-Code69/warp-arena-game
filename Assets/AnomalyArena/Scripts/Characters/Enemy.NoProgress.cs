using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Watches a walk for a stretch that gets nowhere: steps that are not taken whole (slides, steps aside, refusals) while the body
    /// stays within Reach of where that began. A body whose way points across a wall's margin with only a hair of sideways in it slides
    /// a step to one side, finds its way leaning to the other side from there, slides back, and so shuttles in place (the ring of the
    /// Tyrant's meteor fragments round the player leaves such ways). A slide along a wall keeps at least half the walking speed
    /// (WorldMap.WalkStep), so a body that is getting somewhere leaves Reach behind well within the limit; a whole step, or a gap of
    /// more than Gap seconds in the noted walk (a wind-up, a knockback), starts the count again.
    /// </summary>
    public sealed class WalkProgressWatch
    {
        public const float Reach = .5f, Gap = .25f;
        float clock, notedAt = float.NegativeInfinity;
        Vector3 anchor;

        /// <summary>Notes one step of a walk (at: where the body stands; wanted and taken: the velocities asked and allowed; dt: the
        /// step's time; now: the clock it is noted on). True once the walk has got nowhere for limit seconds; the count then starts again.</summary>
        public bool Note(Vector3 at, Vector3 wanted, Vector3 taken, float dt, float now, float limit)
        {
            at = Query.Flat(at);
            Vector3 want = Query.Flat(wanted);
            bool whole = (Query.Flat(taken) - want).sqrMagnitude <= 1e-4f * Mathf.Max(want.sqrMagnitude, 1e-4f);
            bool resumed = now - notedAt > Gap;
            notedAt = now;
            if (whole || resumed || want.sqrMagnitude <= .01f || (at - anchor).sqrMagnitude >= Reach * Reach)
            {
                Restart(at);
                return false;
            }

            clock += dt;
            if (clock < limit) return false;
            Restart(at);
            return true;
        }

        public void Restart(Vector3 at)
        {
            clock = 0f;
            anchor = Query.Flat(at);
        }
    }

    /// <summary>
    /// The set-down rule of Enemy.Unstick.cs for a chasing walk that is not refused outright but gets nowhere (WalkProgressWatch): each
    /// slide is a step, so the count of a refused walk starts again every time and never reaches its limit. A walk that has got nowhere
    /// for WalkRefusedLimit seconds of its own walking is set down on open ground as a refused one is. Bosses and bodies in a gravity
    /// ring are left alone, as there.
    /// </summary>
    public partial class Enemy
    {
        /// <summary>How long a walk may be refused, or get nowhere, before the body is set down on open ground.</summary>
        public static float WalkRefusedSeconds => WalkRefusedLimit;

        readonly WalkProgressWatch progressWatch = new();
        int progressSetDowns;

        void NoteProgress(Vector3 wanted, Vector3 taken, float dt)
        {
            if (IsBoss || SetDownCount != progressSetDowns || WorldMap.Active.GetPull(Position).sqrMagnitude > .01f)
            {
                progressSetDowns = SetDownCount;
                progressWatch.Restart(Position);
                return;
            }

            if (!progressWatch.Note(Position, wanted, taken, dt, Time.time, WalkRefusedLimit)) return;
            SetDownOnOpenGround();
            progressSetDowns = SetDownCount;
        }
    }
}
