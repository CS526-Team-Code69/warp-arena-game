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

            if (!runtime && !TouchesSavedWall(start, from, turn, probes, reach, halfWidth)) return 0;
            if (!Begin()) return 0;
            pieceSize = Mathf.Max(.5f, pieceSize);
            int broken = 0;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var piece = pieces[i];
                if (!Touched(piece.rect, start, from, turn, probes, reach, halfWidth)) continue;
                pieces.RemoveAt(i);
                bool cut = !piece.fragment && (piece.rect.width > pieceSize + CutTolerance || piece.rect.height > pieceSize + CutTolerance);
                if (cut)

                    foreach (var rect in Cut(piece.rect, pieceSize))
                    {
                        if (Touched(rect, start, from, turn, probes, reach, halfWidth))
                        {
                            Rubble(rect);
                            broken++;
                        }
                        else pieces.Add(new Piece { rect = rect, body = Copy(piece.body, rect) });
                    }
                else
                {
                    Rubble(piece.rect);
                    broken++;
                }

                Hide(piece);
            }

            if (broken > 0) Apply();
            return broken;
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
                Rubble(piece.rect);
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

        bool TouchesSavedWall(Vector2 origin, Vector3 from, float turn, int probes, float reach, float halfWidth)
        {
            var map = GetComponent<WorldMap>();
            var walls = map && map.layout ? map.layout.walls : null;
            if (walls != null)
                foreach (var rect in walls)
                    if (Touched(rect, origin, from, turn, probes, reach, halfWidth))
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

        static void Rubble(Rect rect) =>
            Fx.Burst(Query.AtCastHeight(new Vector3(rect.center.x, 0f, rect.center.y)), Vector3.zero, RubbleColour, 6, 7f, 360f, .3f);

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
