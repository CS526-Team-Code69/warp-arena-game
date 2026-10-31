using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed class V5EarthWall : MonoBehaviour
    {

        const float RiseTime = .17f, Stagger = .12f;
        const int Shapes = 6;

        sealed class Rock
        {
            public Transform pivot;
            public MeshFilter filter;
            public float x, z, height, delay;
        }

        static readonly Mesh[] shard = new Mesh[Shapes];
        static readonly Dictionary<V5WallStyle, Material> materials = new();
        static Material shadowMaterial;

        readonly List<Rock> rocks = new();
        Transform root, shadow;
        Material rockMaterial;
        float age;

        public static bool Raise(GameObject wall, float width, float depth)
        {
            var world = WorldMap.Active;
            var style = !world || world.TrialActive ? V5WallStyle.Meteor : world.biome && world.biome.displayName.StartsWith("Dunes") ? V5WallStyle.Dunes : V5WallStyle.Ruins;
            var material = MaterialFor(style);
            if (!material) return false;
            if (!wall.TryGetComponent(out V5EarthWall art)) art = wall.AddComponent<V5EarthWall>();
            art.rockMaterial = material;
            art.Lay(width, depth);
            art.age = 0f;
            art.enabled = true;
            art.Tick();
            return true;
        }

        static Material MaterialFor(V5WallStyle style)
        {
            if (materials.TryGetValue(style, out var known) && known) return known;
            var shader = Resources.Load<Shader>("V5/V5Rock");
            var walls = Resources.Load<Material>(style switch { V5WallStyle.Dunes => "V5/Terrain/V5WallDunes", V5WallStyle.Meteor => "V5/Terrain/V5WallMeteor", _ => "V5/Terrain/V5WallRuins" });
            if (!shader || !walls) return null;
            var material = new Material(shader);
            material.SetTexture("_BaseMap", walls.GetTexture("_BaseMap"));

            material.SetColor("_Tint", style switch
            {
                V5WallStyle.Dunes => new Color(.92f, .72f, .52f),
                V5WallStyle.Meteor => new Color(.62f, .52f, .52f),
                _ => new Color(.84f, .65f, .48f),
            });
            materials[style] = material;
            return material;
        }

        static Mesh MakeShard(int seed)
        {
            var rng = new System.Random(seed);
            float J(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int sides = rng.Next(5, 8);
            float[] heights = { 0f, J(.32f, .62f), 1f };
            float[] radii = { 1f, J(.62f, .9f), J(.1f, .36f) };
            float leanX = J(-.32f, .32f), leanZ = J(-.32f, .32f);
            var ring = new Vector3[heights.Length, sides];
            for (int r = 0; r < heights.Length; r++)
                for (int s = 0; s < sides; s++)
                {
                    float angle = (s + J(-.28f, .28f)) * Mathf.PI * 2f / sides;
                    float radius = radii[r] * J(.82f, 1.14f);
                    ring[r, s] = new Vector3(Mathf.Cos(angle) * radius + leanX * heights[r], heights[r] * (r == 2 ? J(.82f, 1f) : 1f), Mathf.Sin(angle) * radius + leanZ * heights[r]);
                }
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            void Face(params Vector3[] corners)
            {
                float shade = J(.78f, 1.08f);
                var colour = new Color(shade, shade * J(.96f, 1.02f), shade * J(.92f, 1.02f));
                int first = vertices.Count;
                foreach (var corner in corners)
                {
                    vertices.Add(corner);
                    colours.Add(colour);
                }
                for (int i = 1; i + 1 < corners.Length; i++)
                {
                    triangles.Add(first);
                    triangles.Add(first + i);
                    triangles.Add(first + i + 1);
                }
            }
            for (int r = 0; r + 1 < heights.Length; r++)
                for (int s = 0; s < sides; s++)
                {
                    int next = (s + 1) % sides;
                    Face(ring[r, s], ring[r + 1, s], ring[r + 1, next]);
                    Face(ring[r, s], ring[r + 1, next], ring[r, next]);
                }
            var top = new Vector3(leanX, 1f, leanZ);
            for (int s = 0; s < sides; s++) Face(top, ring[2, (s + 1) % sides], ring[2, s]);
            var mesh = new Mesh { name = "V5 Earth Shard" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Shard()
        {
            int pick = Random.Range(0, Shapes);
            if (!shard[pick]) shard[pick] = MakeShard(4001 + pick * 37);
            return shard[pick];
        }

        void Lay(float width, float depth)
        {
            if (!root)
            {
                root = new GameObject("Earth wall").transform;
                root.SetParent(transform, false);
                MakeShadow();
            }

            var scale = transform.localScale;
            root.localScale = new Vector3(1f / Mathf.Max(.01f, scale.x), 1f / Mathf.Max(.01f, scale.y), 1f / Mathf.Max(.01f, scale.z));
            root.position = new Vector3(transform.position.x, 0f, transform.position.z);
            root.localRotation = Quaternion.identity;
            if (shadow)
            {
                shadow.localPosition = new Vector3(0f, .05f, 0f);
                shadow.localScale = new Vector3(width + 2.6f, depth + 2.6f, 1f);
            }

            int used = 0;

            int ridge = Mathf.Max(6, Mathf.RoundToInt(width * 3f));
            for (int i = 0; i < ridge; i++)
            {
                float along = (i + .5f) / ridge * 2f - 1f;
                float taper = 1f - along * along * .6f;
                float side = i % 2 == 0 ? 1f : -1f;
                Place(used++, along * width * .5f + Random.Range(-.08f, .08f), side * depth * Random.Range(.05f, .24f), Random.Range(2f, 3.1f) * taper,
                    Random.Range(.6f, .86f) * depth * (.72f + .28f * taper), Mathf.Abs(along), side * Random.Range(10f, 24f));
            }

            int foot = Mathf.Max(8, Mathf.RoundToInt(width * 3.2f));
            for (int i = 0; i < foot; i++)
            {
                float along = Random.Range(-1f, 1f);
                float side = i % 2 == 0 ? 1f : -1f;
                Place(used++, along * (width * .5f + .35f), side * depth * Random.Range(.55f, .92f), Random.Range(.28f, .8f), Random.Range(.28f, .55f), Mathf.Abs(along) + .35f, side * Random.Range(4f, 30f));
            }
            for (int i = used; i < rocks.Count; i++) rocks[i].pivot.gameObject.SetActive(false);
        }

        void MakeShadow()
        {
            if (!V5Vfx.I) return;
            if (!shadowMaterial) shadowMaterial = V5Vfx.I.EffectMaterial("glow");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var box = quad.GetComponent<Collider>();
            box.enabled = false;
            Destroy(box);
            quad.name = "Earth wall shadow";
            quad.transform.SetParent(root, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var view = quad.GetComponent<MeshRenderer>();
            view.sharedMaterial = shadowMaterial;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(.03f, .02f, .015f, .8f));
            view.SetPropertyBlock(block);
            shadow = quad.transform;
        }

        void Place(int index, float x, float z, float height, float radius, float lateness, float tilt)
        {
            if (index >= rocks.Count) rocks.Add(MakeRock());
            var rock = rocks[index];
            rock.pivot.gameObject.SetActive(true);
            rock.filter.sharedMesh = Shard();
            rock.filter.GetComponent<MeshRenderer>().sharedMaterial = rockMaterial;
            rock.x = x;
            rock.z = z;
            rock.height = height;
            rock.delay = lateness * Stagger + Random.Range(0f, .03f);

            rock.filter.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            rock.pivot.localRotation = Quaternion.Euler(tilt, 0f, Random.Range(-6f, 6f));
            rock.pivot.localScale = new Vector3(radius, height, radius * Random.Range(.8f, 1.15f));
        }

        Rock MakeRock()
        {
            var pivot = new GameObject("Rock").transform;
            pivot.SetParent(root, false);
            var body = new GameObject("Shard", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(pivot, false);
            var view = body.GetComponent<MeshRenderer>();
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Rock { pivot = pivot, filter = body.GetComponent<MeshFilter>() };
        }

        void Update()
        {
            age += Time.deltaTime;
            Tick();
            if (age > RiseTime + Stagger * 2f) enabled = false;
        }

        void Tick()
        {
            foreach (var rock in rocks)
            {
                if (!rock.pivot.gameObject.activeSelf) continue;
                float t = Mathf.Clamp01((age - rock.delay) / RiseTime);

                float eased = 1f + 2.4f * Mathf.Pow(t - 1f, 3f) + 1.4f * Mathf.Pow(t - 1f, 2f);
                rock.pivot.localPosition = new Vector3(rock.x, (eased - 1f) * rock.height, rock.z);
            }
        }
    }
}
