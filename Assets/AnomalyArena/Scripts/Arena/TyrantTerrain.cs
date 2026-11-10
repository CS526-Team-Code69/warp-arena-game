using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// The final fight's changing ground (system doc 13.2): the Tyrant's Tail Sweep breaks the map walls its tail touches, its Rampage Charge
    /// breaks every wall its running body runs into, and its Star Throw leaves meteor fragments that stand as small walls
    /// for a while. These work on a runtime copy of the map layout (the saved asset never changes), so every check that reads the layout's
    /// walls follows at once (enemy paths, spawn points, landing points, skill targets), and on the wall objects themselves, so collisions and
    /// shots follow too. A map wall breaks only in the pieces touched and stays broken for the rest of the fight; a fragment breaks whole. A
    /// bulwark is not a map wall: the tail never breaks one, while the charge does, and it goes as when its time is up. ResetTerrain puts
    /// the map back (a new Tyrant, the enemy lab). Added to the WorldMap object on first use.
    /// </summary>
    public sealed class TyrantTerrain : MonoBehaviour
    {
        // One standing wall rect: the scene's saved wall object, a copy cut from it, or a meteor fragment (a copy of the saved wall
        // nearest its size, LayoutWalls).
        sealed class Piece
        {
            public Rect rect;
            public GameObject body;
            public bool saved, fragment;
            public float life;
        }

        // Turn between two probes of the tail while it sweeps, degrees (geometry sampling, not tuning).
        const float ProbeDegrees = 2f;
        // A wall rect this close to the piece size is not cut again (float tolerance).
        const float CutTolerance = .01f;
        static readonly Color RubbleColour = new(.78f, .72f, .64f);

        WorldMap world;
        MapLayout original, runtime;
        Transform group;
        int savedCount;
        readonly List<Piece> pieces = new();
        readonly List<GameObject> hidden = new(), copies = new();

        public static TyrantTerrain For(WorldMap world)
        {
            if (!world) return null;
            return world.TryGetComponent(out TyrantTerrain terrain) ? terrain : world.gameObject.AddComponent<TyrantTerrain>();
        }

        /// <summary>The fight has changed the map: a wall broken or a fragment standing (or both, once).</summary>
        public bool Changed => runtime;

        /// <summary>Footprints of the meteor fragments standing now.</summary>
        public List<Rect> Fragments
        {
            get
            {
                var list = new List<Rect>();
                foreach (var piece in pieces)
                    if (piece.fragment)
                        list.Add(piece.rect);
                return list;
            }
        }

        /// <summary>
        /// The Tail Sweep's tail turned from one direction to the other round origin this step, reach long and halfWidth thick to each side:
        /// every wall piece it touched breaks (BreakTouched). It never breaks a bulwark. Returns how many pieces broke.
        /// </summary>
        public int BreakSwept(Vector3 origin, Vector3 from, Vector3 to, float reach, float halfWidth, float pieceSize)
        {
            if (reach <= 0f) return 0;
            from = Query.Flat(from).normalized;
            to = Query.Flat(to).normalized;
            float turn = Vector3.SignedAngle(from, to, Vector3.up);
            int probes = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(turn) / ProbeDegrees));
            var start = new Vector2(origin.x, origin.z);
            return BreakTouched(rect => Touched(rect, start, from, turn, probes, reach, halfWidth), pieceSize);
        }

        /// <summary>
        /// The Rampage Charge's body, radius wide, ran from one point to the next: every wall piece it ran into breaks (BreakTouched), and so
        /// does every bulwark it ran into, whole, as when its time is up. Running into means touching something that lies at least partly
        /// ahead of its centre where the step began, so a wall it touches behind its back as it sets off stands. Returns how many pieces and
        /// bulwarks broke.
        /// </summary>
        public int BreakAlong(Vector3 from, Vector3 to, float radius, float pieceSize)
        {
            if (radius <= 0f) return 0;
            Vector2 a = new(from.x, from.z), b = new(to.x, to.z);
            return BreakTouched(rect => RunTouches(rect, a, b, radius), pieceSize) + BreakBulwarks(a, b, radius);
        }

        /// <summary>
        /// WorldMap.IsWalkable at the point for a body radius wide, once a run from from to to, runRadius wide, has broken what it runs into
        /// (BreakAlong with the same pieceSize): the wall pieces, fragments and bulwarks that run breaks no longer count, while the parts of a
        /// long wall it leaves standing still do. The map's edge, holes and gravity rings count as ever.
        /// </summary>
        public bool WalkableAfterRun(Vector3 point, float radius, Vector3 from, Vector3 to, float runRadius, float pieceSize)
        {
            var map = GetComponent<WorldMap>();
            if (!map) return true;
            if (!map.Contains(point, radius) || map.IsHole(point) || map.GetPull(point).sqrMagnitude >= .01f) return false;
            Vector2 a = new(from.x, from.z), b = new(to.x, to.z);
            pieceSize = Mathf.Max(.5f, pieceSize);
            // WorldMap.IsWall's own tests, less what the run breaks; once the fight has changed the map, its standing pieces are the
            // layout's walls (Apply).
            if (runtime && map.layout == runtime)
            {
                foreach (var piece in pieces)
                    if (Blocks(piece.rect, piece.fragment))
                        return false;
            }
            else if (map.layout && map.layout.walls != null)
                foreach (var rect in map.layout.walls)
                    if (Blocks(rect, false))
                        return false;

            if (map.temporaryWalls != null)
                foreach (var wall in map.temporaryWalls)
                    if (wall && wall.gameObject.activeInHierarchy && wall.TryGetComponent(out Collider body) &&
                        body.bounds.Contains(new Vector3(point.x, wall.transform.position.y, point.z)) && !RunTouches(wall, a, b, runRadius))
                        return false;
            return true;

            // A standing rect near the point blocks it unless the run breaks every part of it near the point: BreakTouched cuts a map
            // wall it runs into and breaks only the parts it touches; a fragment goes whole.
            bool Blocks(Rect rect, bool whole)
            {
                if (!Near(rect, point, radius)) return false;
                if (!RunTouches(rect, a, b, runRadius)) return true;
                if (whole || !Cuts(rect, pieceSize)) return false;
                foreach (var part in Cut(rect, pieceSize))
                    if (Near(part, point, radius) && !RunTouches(part, a, b, runRadius))
                        return true;
                return false;
            }
        }

        /// <summary>Star Throw: a square wall of side size at each point that lies on the map, clear of walls, holes, bodies and other
        /// fragments; each stands life seconds (the Tail Sweep or the Rampage Charge breaks it sooner). Returns how many stand.</summary>
        public int AddFragments(IReadOnlyList<Vector3> points, float size, float life)
        {
            if (points == null || points.Count == 0 || size <= 0f || life <= 0f || !Begin()) return 0;
            float half = size * .5f;
            int added = 0;
            foreach (var point in points)
            {
                Vector3 at = Query.Flat(point);
                var rect = new Rect(at.x - half, at.z - half, size, size);
                if (!world.Contains(at, half) || world.IsWall(at, half) || world.IsHole(at) || Occupied(rect) || Overlaps(rect)) continue;
                var template = group && savedCount > 0 ? LayoutWalls.ClosestInSize(group, Mathf.Min(savedCount, group.childCount), rect) : null;
                pieces.Add(new Piece { rect = rect, body = Copy(template ? template.gameObject : null, rect, true), fragment = true, life = life });
                added++;
            }

            if (added > 0) Apply();
            return added;
        }

        /// <summary>Puts the map back as it was saved: broken walls stand again, cut pieces and fragments go, the saved layout returns.</summary>
        public void ResetTerrain()
        {
            if (!runtime) return;
            foreach (var copy in copies)
                if (copy)
                {
                    copy.SetActive(false);
                    Destroy(copy);
                }

            foreach (var wall in hidden)
                if (wall)
                    wall.SetActive(true);
            copies.Clear();
            hidden.Clear();
            pieces.Clear();
            if (world && world.layout == runtime) world.layout = original;
            Destroy(runtime);
            runtime = null;
            Physics.SyncTransforms();
            if (world) world.HoleChanged();
        }

        void Update()
        {
            var game = GameManager.Instance;
            // During a void trial the battlefield stands frozen (document 12.5): its fragments keep their time and the map is not rebuilt.
            if (!runtime || !game || !game.IsCombatActive || world && world.TrialActive) return;
            float dt = game.WorldDeltaTime;
            bool changed = false;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var piece = pieces[i];
                if (!piece.fragment) continue;
                piece.life -= dt;
                if (piece.life > 0f) continue;
                pieces.RemoveAt(i);
                Rubble(piece.rect.center);
                Hide(piece);
                changed = true;
            }

            if (changed) Apply();
        }

        /// <summary>Takes over the map on the first change: a runtime copy of its layout, one piece per saved wall rect and object.</summary>
        bool Begin()
        {
            if (runtime) return true;
            world = GetComponent<WorldMap>();
            original = world ? world.layout : null;
            if (!original) return false;
            runtime = Instantiate(original);
            runtime.name = original.name + " (Tyrant fight)";
            group = ActiveWallGroup(world);
            var walls = original.walls ?? Array.Empty<Rect>();
            savedCount = walls.Length;
            pieces.Clear();
            for (int i = 0; i < walls.Length; i++)
                pieces.Add(new Piece { rect = walls[i], body = group && i < group.childCount ? group.GetChild(i).gameObject : null, saved = true });
            world.layout = runtime;
            return true;
        }

        /// <summary>Every standing piece the touched test picks breaks: a map wall is first cut into pieces at most pieceSize across and only
        /// the touched pieces go, the rest standing on as copies of it; a meteor fragment goes whole. Returns how many pieces broke.</summary>
        int BreakTouched(Func<Rect, bool> touched, float pieceSize)
        {
            // Until something breaks, the saved map stays in charge.
            if (!runtime && !TouchesSavedWall(touched)) return 0;
            if (!Begin()) return 0;
            pieceSize = Mathf.Max(.5f, pieceSize);
            int broken = 0;
            bool changed = false;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var piece = pieces[i];
                if (!touched(piece.rect)) continue;
                pieces.RemoveAt(i);
                changed = true;
                if (!piece.fragment && Cuts(piece.rect, pieceSize))
                    // The untouched pieces stand on as copies of the wall; the copies are made before the wall itself is hidden.
                    foreach (var rect in Cut(piece.rect, pieceSize))
                    {
                        if (touched(rect))
                        {
                            Rubble(rect.center);
                            broken++;
                        }
                        else pieces.Add(new Piece { rect = rect, body = Copy(piece.body, rect) });
                    }
                else
                {
                    Rubble(piece.rect.center);
                    broken++;
                }

                Hide(piece);
            }

            // A wall cut into copies changed the layout even in the rare case that none of its pieces counts as touched on its own.
            if (changed) Apply();
            return broken;
        }

        /// <summary>Every bulwark (the player's skill wall) the run ran into goes at once, as when its time is up (WorldMap.BreakBulwarks,
        /// shared with the Nash Prophet's pulse). Returns how many broke.</summary>
        int BreakBulwarks(Vector2 a, Vector2 b, float radius)
        {
            var map = GetComponent<WorldMap>();
            return map ? map.BreakBulwarks(wall => RunTouches(wall, a, b, radius)) : 0;
        }

        bool TouchesSavedWall(Func<Rect, bool> touched)
        {
            var map = GetComponent<WorldMap>();
            var walls = map && map.layout ? map.layout.walls : null;
            if (walls != null)
                foreach (var rect in walls)
                    if (touched(rect))
                        return true;
            return false;
        }

        /// <summary>The wall group of the map this run is on; its children follow the layout's rects in order (LayoutWalls).</summary>
        static Transform ActiveWallGroup(WorldMap world)
        {
            if (world.biomes == null || world.biomeWallGroups == null) return null;
            int index = Array.IndexOf(world.biomes, world.biome);
            return index >= 0 && index < world.biomeWallGroups.Length ? world.biomeWallGroups[index] : null;
        }

        /// <summary>The standing pieces become the layout's walls; paths are rebuilt at once (WorldMap.HoleChanged).</summary>
        void Apply()
        {
            var rects = new Rect[pieces.Count];
            for (int i = 0; i < pieces.Count; i++) rects[i] = pieces[i].rect;
            runtime.walls = rects;
            Physics.SyncTransforms();
            world.HoleChanged();
        }

        /// <summary>A copy of a wall object on rect, scaled the way LayoutWalls places the walls a layout gained. Its stone is rebuilt for the rect at
        /// once, in meteor rock for a fragment.</summary>
        GameObject Copy(GameObject template, Rect rect, bool fragment = false)
        {
            if (!template || !group) return null;
            var copy = Instantiate(template, group);
            copy.name = template.name + " (piece)";
            var t = copy.transform;
            t.localPosition = new Vector3(rect.center.x, template.transform.localPosition.y, rect.center.y);
            t.localRotation = template.transform.localRotation;
            t.localScale = new Vector3(rect.width, template.transform.localScale.y, rect.height);
            copy.SetActive(true);
            if (fragment && copy.TryGetComponent(out V5TerrainWallArt art)) art.style = V5WallStyle.Meteor;
            if (copy.TryGetComponent(out V2TerrainPresentation look)) look.Refresh();
            copies.Add(copy);
            return copy;
        }

        /// <summary>The piece's object leaves the field at once (its collider with it); a saved wall waits hidden for ResetTerrain.</summary>
        void Hide(Piece piece)
        {
            if (!piece.body) return;
            piece.body.SetActive(false);
            if (piece.saved) hidden.Add(piece.body);
            else
            {
                copies.Remove(piece.body);
                Object.Destroy(piece.body);
            }
        }

        internal static void Rubble(Vector2 at) =>
            Fx.Burst(Query.AtCastHeight(new Vector3(at.x, 0f, at.y)), Vector3.zero, RubbleColour, 6, 7f, 360f, .3f);

        /// <summary>A living body stands on the square (a fragment never lands on anyone).</summary>
        static bool Occupied(Rect rect)
        {
            foreach (var body in Combatant.All)
                if (body && body.IsAlive && Distance(rect, new Vector2(body.Position.x, body.Position.z)) < body.Radius)
                    return true;
            return false;
        }

        bool Overlaps(Rect rect)
        {
            foreach (var piece in pieces)
                if (piece.rect.Overlaps(rect))
                    return true;
            return false;
        }

        /// <summary>Whether the tail, turning by turn degrees from from in probes steps, touched the rect grown by halfWidth.</summary>
        static bool Touched(Rect rect, Vector2 origin, Vector3 from, float turn, int probes, float reach, float halfWidth)
        {
            var grown = new Rect(rect.xMin - halfWidth, rect.yMin - halfWidth, rect.width + halfWidth * 2f, rect.height + halfWidth * 2f);
            if (Distance(grown, origin) > reach) return false;
            for (int i = 0; i <= probes; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, turn * i / probes, 0f) * from;
                if (SegmentHits(origin, origin + new Vector2(direction.x, direction.z) * reach, grown)) return true;
            }

            return false;
        }

        static float Distance(Rect rect, Vector2 point)
        {
            float dx = Mathf.Max(Mathf.Max(rect.xMin - point.x, 0f), point.x - rect.xMax);
            float dz = Mathf.Max(Mathf.Max(rect.yMin - point.y, 0f), point.y - rect.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Distance from the segment from a to b to the rect, 0 where they cross.</summary>
        static float SegmentDistance(Rect rect, Vector2 a, Vector2 b)
        {
            if (SegmentHits(a, b, rect)) return 0f;
            // Apart, the nearest two points are an end of the segment against the rect or a corner of the rect against the segment.
            float near = Mathf.Min(Distance(rect, a), Distance(rect, b));
            near = Mathf.Min(near, PointToSegment(new Vector2(rect.xMin, rect.yMin), a, b));
            near = Mathf.Min(near, PointToSegment(new Vector2(rect.xMax, rect.yMin), a, b));
            near = Mathf.Min(near, PointToSegment(new Vector2(rect.xMin, rect.yMax), a, b));
            return Mathf.Min(near, PointToSegment(new Vector2(rect.xMax, rect.yMax), a, b));
        }

        static float PointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }

        /// <summary>Whether a body radius wide running from a to b runs into the rect: it comes within radius of the rect, and part of the
        /// rect lies ahead of the body's centre at a. A run that does not move runs into nothing.</summary>
        static bool RunTouches(Rect rect, Vector2 a, Vector2 b, float radius)
        {
            Vector2 run = b - a;
            if (run.sqrMagnitude < 1e-8f) return false;
            // The rect's farthest reach along the run, axis by axis.
            float ahead = Mathf.Max(run.x * (rect.xMin - a.x), run.x * (rect.xMax - a.x)) +
                          Mathf.Max(run.y * (rect.yMin - a.y), run.y * (rect.yMax - a.y));
            return ahead > 0f && SegmentDistance(rect, a, b) <= radius;
        }

        /// <summary>RunTouches for a bulwark: its box seen from above, turned with it.</summary>
        static bool RunTouches(TemporaryWall wall, Vector2 a, Vector2 b, float radius)
        {
            if (!wall.TryGetComponent(out BoxCollider box))
            {
                if (!wall.TryGetComponent(out Collider body)) return false;
                var bounds = body.bounds;
                return RunTouches(new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z), a, b, radius);
            }

            var t = box.transform;
            Vector3 centre = t.TransformPoint(box.center), size = Vector3.Scale(box.size, t.lossyScale);
            Vector3 across = Query.Flat(t.right).normalized, ahead = Query.Flat(t.forward).normalized;
            float width = Mathf.Abs(size.x), depth = Mathf.Abs(size.z);
            var face = new Rect(-width * .5f, -depth * .5f, width, depth);

            Vector2 Local(Vector2 p)
            {
                var offset = new Vector3(p.x - centre.x, 0f, p.y - centre.z);
                return new Vector2(Vector3.Dot(offset, across), Vector3.Dot(offset, ahead));
            }

            return RunTouches(face, Local(a), Local(b), radius);
        }

        /// <summary>WorldMap.IsWall's test of one rect: the point lies inside the rect grown by margin.</summary>
        static bool Near(Rect rect, Vector3 point, float margin) =>
            point.x >= rect.xMin - margin && point.x <= rect.xMax + margin && point.z >= rect.yMin - margin && point.z <= rect.yMax + margin;

        /// <summary>A map wall this big is cut into pieces before it breaks (Cut).</summary>
        static bool Cuts(Rect rect, float size) => rect.width > size + CutTolerance || rect.height > size + CutTolerance;

        /// <summary>Liang-Barsky: whether the segment from a to b crosses the rect.</summary>
        static bool SegmentHits(Vector2 a, Vector2 b, Rect r)
        {
            Vector2 d = b - a;
            float enter = 0f, leave = 1f;
            return Clip(-d.x, a.x - r.xMin, ref enter, ref leave) && Clip(d.x, r.xMax - a.x, ref enter, ref leave) &&
                   Clip(-d.y, a.y - r.yMin, ref enter, ref leave) && Clip(d.y, r.yMax - a.y, ref enter, ref leave);
        }

        static bool Clip(float p, float q, ref float enter, ref float leave)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float t = q / p;
            if (p < 0f)
            {
                if (t > leave) return false;
                if (t > enter) enter = t;
            }
            else
            {
                if (t < enter) return false;
                if (t < leave) leave = t;
            }

            return true;
        }

        /// <summary>The rect cut into equal pieces at most size across on each side.</summary>
        static IEnumerable<Rect> Cut(Rect rect, float size)
        {
            int across = Mathf.Max(1, Mathf.CeilToInt(rect.width / size - CutTolerance));
            int deep = Mathf.Max(1, Mathf.CeilToInt(rect.height / size - CutTolerance));
            float w = rect.width / across, h = rect.height / deep;
            for (int x = 0; x < across; x++)
            for (int z = 0; z < deep; z++)
                yield return new Rect(rect.xMin + x * w, rect.yMin + z * h, w, h);
        }
    }
}
