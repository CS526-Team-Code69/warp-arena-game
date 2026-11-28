using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// A body's own walking on the map. The map counts a body as touching a wall when its centre is within its radius of the wall's
    /// rect (IsWall), and a body does not walk into that margin by itself. WalkStep says which part of a step it may take: the whole
    /// step where the ground is open, otherwise the part along the wall (first the axis the step mostly follows, then the other),
    /// otherwise a step aside from which the step is open (round the end of a margin it catches by a hair), and nothing only when
    /// none of these is open. A body that already stands in a margin (pushed or thrown there, born at its edge, or wider
    /// than the ground it was sent over) may take any step that does not carry it deeper, so it walks out or along the wall instead of
    /// standing where it is for good. Holes and gravity rings are never stepped into, as before. OpenGroundOnPath is the last resort
    /// for a body whose walking is still refused: the nearest ground on its way where it can stand.
    /// </summary>
    public sealed partial class WorldMap
    {
        // A step that keeps its depth within this is not "deeper" (rounding along a wall).
        const float DepthSlack = .0001f;
        // A slide along a wall keeps at least this share of the walking speed, and is tried only for an axis that carries at least
        // MinSlideShare of the step (a step straight at a wall has no side to slide to).
        const float SlideShare = .5f, MinSlideShare = .02f;
        // How far OpenGroundOnPath looks for ground, in cells, and the least distance it moves a body.
        const int UnstickReachCells = 6;
        const float UnstickMinMove = .75f;

        /// <summary>How far a body of this radius standing at the point reaches into a wall's margin or past the map's edge, in cells:
        /// 0 on ground it may stand on, growing toward the wall's middle.</summary>
        public float WallDepth(Vector3 point, float radius)
        {
            Vector3 extents = Bounds.extents;
            float depth = Mathf.Max(0f, Mathf.Max(Mathf.Abs(point.x) - (extents.x - radius), Mathf.Abs(point.z) - (extents.z - radius)));
            foreach (var rect in extraWalls) depth = Mathf.Max(depth, RectDepth(rect, point, radius));
            if (layout && layout.walls != null)
                foreach (var rect in layout.walls) depth = Mathf.Max(depth, RectDepth(rect, point, radius));
            if (temporaryWalls != null)
                foreach (var wall in temporaryWalls)
                {
                    if (!wall || !wall.gameObject.activeInHierarchy || !wall.TryGetComponent(out Collider body)) continue;
                    Bounds bounds = body.bounds;
                    // As IsWall: a temporary wall counts where the body's centre is inside its box.
                    depth = Mathf.Max(depth, RectDepth(Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z), point, 0f));
                }
            return depth;
        }

        /// <summary>How far inside the rect grown by margin the point lies (the distance to its nearest side), 0 outside it.</summary>
        static float RectDepth(Rect rect, Vector3 point, float margin)
        {
            float x = Mathf.Min(point.x - (rect.xMin - margin), rect.xMax + margin - point.x);
            float z = Mathf.Min(point.z - (rect.yMin - margin), rect.yMax + margin - point.z);
            return Mathf.Max(0f, Mathf.Min(x, z));
        }

        /// <summary>The velocity a walking body of this radius may take for a step of dt from position, wanting v: v itself where the
        /// step is open, else a slide along the wall, else none. allowGravity lets the step enter a gravity ring (a fast kind following
        /// what it chases); no step enters a hole. keepPace is for a walk: its slide keeps at least SlideShare of its speed, so a body
        /// sent at a wall at a slant does not creep; without it the slide is the plain part of v along the wall (a body slipping on ice).</summary>
        public Vector3 WalkStep(Vector3 position, Vector3 v, float dt, float radius, bool allowGravity, bool keepPace = true)
        {
            Vector3 flat = new(v.x, 0f, v.z);
            float speed = flat.magnitude;
            if (speed < 1e-4f || dt <= 0f) return v;
            position = Query.Flat(position);
            float depth = -1f; // how deep the body stands now: asked only once a step ends in a margin
            if (StepOpen(position, position + flat * dt, radius, allowGravity, ref depth)) return v;
            bool xFirst = Mathf.Abs(flat.x) >= Mathf.Abs(flat.z);
            for (int pass = 0; pass < 2; pass++)
            {
                bool alongX = pass == 0 ? xFirst : !xFirst;
                float part = alongX ? flat.x : flat.z;
                if (Mathf.Abs(part) < speed * MinSlideShare) continue;
                float slide = keepPace ? Mathf.Sign(part) * Mathf.Max(Mathf.Abs(part), speed * SlideShare) : part;
                Vector3 along = alongX ? new Vector3(slide, 0f, 0f) : new Vector3(0f, 0f, slide);
                if (StepOpen(position, position + along * dt, radius, allowGravity, ref depth)) return new Vector3(along.x, v.y, along.z);
            }
            // Neither the step nor a slide. A walking body may be catching the end of a wall's margin by a hair: it was sent along the
            // margin's edge (a cell's centre may lie a hundredth of a cell outside it), stands a little inside the line of the margin's
            // end, and its way has too little sideways in it for a slide. A step aside from which the step it wants is open takes it
            // clear of the end; the side its way leans to is tried first. Straight at the middle of a wall no side opens the step, so
            // the body stops there as before.
            if (keepPace)
            {
                float lean = xFirst ? flat.z : flat.x;
                for (int pass = 0; pass < 2; pass++)
                {
                    float sign = (pass == 0) == (lean >= 0f) ? 1f : -1f;
                    Vector3 aside = (xFirst ? Vector3.forward : Vector3.right) * (sign * speed * SlideShare);
                    Vector3 from = position + aside * dt;
                    float depthAside = -1f;
                    if (StepOpen(position, from, radius, allowGravity, ref depth) && StepOpen(from, from + flat * dt, radius, allowGravity, ref depthAside))
                        return new Vector3(aside.x, v.y, aside.z);
                }
            }
            return new Vector3(0f, v.y, 0f);
        }

        /// <summary>Whether a walking step from from may end at next: never in a hole, in a gravity ring only when allowed, and on ground
        /// the body may stand on or, from inside a wall's margin, no deeper in it than it stands now (depthNow, worked out here the
        /// first time it is needed and kept for the caller's next try).</summary>
        bool StepOpen(Vector3 from, Vector3 next, float radius, bool allowGravity, ref float depthNow)
        {
            if (IsHole(next) || !allowGravity && GetPull(next).sqrMagnitude >= 0.01f) return false;
            float depth = WallDepth(next, radius);
            if (depth <= 0f) return true;
            if (depthNow < 0f) depthNow = WallDepth(from, radius);
            return depthNow > 0f && depth <= depthNow + DepthSlack;
        }

        /// <summary>
        /// The nearest ground on the way to the base (raid) or to what the enemies chase where a body of this radius can stand, within
        /// UnstickReachCells: for a body whose own walking has been refused where it stands for too long (Enemy.SetDownOnOpenGround).
        /// False when there is none. clearOfColliders: the body was held up by a wall's collider the map's walls do not cover, so a
        /// point is also checked against the wall colliders themselves (both wall layers), or the body could be set down in that wall.
        /// </summary>
        public bool OpenGroundOnPath(Vector3 position, float radius, bool raid, bool allowGravity, out Vector3 ground, bool clearOfColliders = false)
        {
            ground = position;
            if (TrialActive || Grid == null) return false;
            float room = radius + .05f;
            return Grid.NearestOnPath(position, radius, raid, allowGravity, UnstickReachCells, UnstickMinMove,
                centre => IsWalkable(centre, room, allowGravity) && (!clearOfColliders || !WallColliderAt(centre, room)), out ground);
        }

        /// <summary>Whether a wall's collider (either wall layer, a Bulwark included) stands within room of the point, from a body's
        /// feet to above its middle.</summary>
        static bool WallColliderAt(Vector3 point, float room)
        {
            float low = room + .05f, high = Mathf.Max(low, 1.6f);
            return Physics.CheckCapsule(new Vector3(point.x, low, point.z), new Vector3(point.x, high, point.z), room,
                GameManager.WallMask | 1 << Combatant.BodyOnlyWallLayer, QueryTriggerInteraction.Ignore);
        }
    }
}
