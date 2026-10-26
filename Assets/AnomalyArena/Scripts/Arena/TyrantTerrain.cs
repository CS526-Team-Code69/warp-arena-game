using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    public sealed class TyrantTerrain : MonoBehaviour
    {

        sealed class Piece
        {
            public Rect rect;
            public GameObject body;
            public bool saved, fragment;
            public float life;
        }

        const float ProbeDegrees = 2f;

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

        public bool Changed => runtime;

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

        public int BreakAlong(Vector3 from, Vector3 to, float radius, float pieceSize)
        {
            if (radius <= 0f) return 0;
            Vector2 a = new(from.x, from.z), b = new(to.x, to.z);
            return BreakTouched(rect => RunTouches(rect, a, b, radius), pieceSize) + BreakBulwarks(a, b, radius);
        }

        public bool WalkableAfterRun(Vector3 point, float radius, Vector3 from, Vector3 to, float runRadius, float pieceSize)
        {
            var map = GetComponent<WorldMap>();
            if (!map) return true;
            if (!map.Contains(point, radius) || map.IsHole(point) || map.GetPull(point).sqrMagnitude >= .01f) return false;
            Vector2 a = new(from.x, from.z), b = new(to.x, to.z);
            pieceSize = Mathf.Max(.5f, pieceSize);

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
                pieces.Add(new Piece { rect = rect, body = Copy(template ? template.gameObject : null, rect), fragment = true, life = life });
                added++;
            }

            if (added > 0) Apply();
            return added;
        }

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

        int BreakTouched(Func<Rect, bool> touched, float pieceSize)
        {

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

            if (changed) Apply();
            return broken;
        }

        int BreakBulwarks(Vector2 a, Vector2 b, float radius)
        {
            var map = GetComponent<WorldMap>();
            if (!map || map.temporaryWalls == null) return 0;
            int broken = 0;
            foreach (var wall in map.temporaryWalls)
            {
                if (!wall || !wall.gameObject.activeInHierarchy || !RunTouches(wall, a, b, radius)) continue;
                Vector3 at = wall.transform.position;
                Rubble(new Vector2(at.x, at.z));
                wall.gameObject.SetActive(false);
                broken++;
            }

            if (broken > 0) map.HoleChanged();
            return broken;
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

        static Transform ActiveWallGroup(WorldMap world)
        {
            if (world.biomes == null || world.biomeWallGroups == null) return null;
            int index = Array.IndexOf(world.biomes, world.biome);
            return index >= 0 && index < world.biomeWallGroups.Length ? world.biomeWallGroups[index] : null;
        }

        void Apply()
        {
            var rects = new Rect[pieces.Count];
            for (int i = 0; i < pieces.Count; i++) rects[i] = pieces[i].rect;
            runtime.walls = rects;
            Physics.SyncTransforms();
            world.HoleChanged();
        }

        GameObject Copy(GameObject template, Rect rect)
        {
            if (!template || !group) return null;
            var copy = Instantiate(template, group);
            copy.name = template.name + " (piece)";
            var t = copy.transform;
            t.localPosition = new Vector3(rect.center.x, template.transform.localPosition.y, rect.center.y);
            t.localRotation = template.transform.localRotation;
            t.localScale = new Vector3(rect.width, template.transform.localScale.y, rect.height);
            copy.SetActive(true);
            copies.Add(copy);
            return copy;
        }

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

        static void Rubble(Vector2 at) =>
            Fx.Burst(Query.AtCastHeight(new Vector3(at.x, 0f, at.y)), Vector3.zero, RubbleColour, 6, 7f, 360f, .3f);

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

        static float SegmentDistance(Rect rect, Vector2 a, Vector2 b)
        {
            if (SegmentHits(a, b, rect)) return 0f;

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

        static bool RunTouches(Rect rect, Vector2 a, Vector2 b, float radius)
        {
            Vector2 run = b - a;
            if (run.sqrMagnitude < 1e-8f) return false;

            float ahead = Mathf.Max(run.x * (rect.xMin - a.x), run.x * (rect.xMax - a.x)) +
                          Mathf.Max(run.y * (rect.yMin - a.y), run.y * (rect.yMax - a.y));
            return ahead > 0f && SegmentDistance(rect, a, b) <= radius;
        }

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

        static bool Near(Rect rect, Vector3 point, float margin) =>
            point.x >= rect.xMin - margin && point.x <= rect.xMax + margin && point.z >= rect.yMin - margin && point.z <= rect.yMax + margin;

        static bool Cuts(Rect rect, float size) => rect.width > size + CutTolerance || rect.height > size + CutTolerance;

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
