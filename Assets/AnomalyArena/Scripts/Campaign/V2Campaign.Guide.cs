using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
                 
                                                                                                                                            
                                                                                                                                         
                                                                                                                                    
                                                                                                                                        
                                                                                                                                      
                                                                                                                                      
                                                                                                                      
                                                                                                                                       
                                                                                                                                          
                  
    public sealed partial class V2Campaign
    {
                                                                                                                                        
                                                                                                                                         
                                                                                                                                         
                                                                                           
        const int LineShards = 110, FieldShards = 140, ShardTries = 8;
        const float LineStart = 22f, LineSpread = 2.4f, LineHalfWidth = 6f, FieldReach = 34f, FieldRingGap = 1.2f, ShardClearance = .35f;
                                                                                                                                       
                                                                                                                                         
                                                                                                                  
                                                                                                                                          
        const float ShardGlow = 1.2f, ShardBreath = .12f, ShardBreathSpeed = 1.7f;
                                                                                                                                          
        const float HumFadeSeconds = .6f, HumFarPitch = .85f;
                                                                                                                                         
        static readonly Color ShardDeep = new(.34f, .08f, .66f), ShardBright = new(.66f, .32f, 1f);
        static readonly Vector3 ShardLight = new Vector3(-.35f, 1f, -.45f).normalized;

        Transform holeGuide;
        GameObject guideShards;
        Mesh guideMesh;
        Material guideMaterial;
        AudioSource holeHum;
        float humLevel, guideGlow = 1f;
        int shardCount;
        bool humPaused;

                                                                                                                                           
                                                            
        public bool HoleShardsShown => guideShards && guideShards.activeSelf;
        public int HoleShardCount => holeGuide ? shardCount : 0;
        public float HoleHumLevel => holeHum && holeHum.isPlaying ? holeHum.volume : 0f;

                                                                                                                                        
                                                    
        void BuildHoleGuide(WorldMap map)
        {
            ClearHoleGuide();
            var hole = map ? map.CampaignHole : null;
            if (!hole) return;
            holeGuide = new GameObject("V2 Hole Guide").transform;
            guideMesh = BuildShardMesh(map, hole);
            if (guideMesh)
            {
                guideShards = new GameObject("Shards");
                guideShards.transform.SetParent(holeGuide, false);
                guideShards.AddComponent<MeshFilter>().sharedMesh = guideMesh;
                var r = guideShards.AddComponent<MeshRenderer>();
                guideGlow = Art.Enabled ? ShardGlow : 1f;
                guideMaterial = ShardMaterial(guideGlow);
                if (guideMaterial) r.sharedMaterial = guideMaterial;
                else if (gm && gm.litMaterial) r.sharedMaterial = gm.Mat(ShardDeep);                                            
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                guideShards.SetActive(false);                                                                    
            }
            holeHum = holeGuide.gameObject.AddComponent<AudioSource>();
            holeHum.playOnAwake = false; holeHum.loop = true; holeHum.spatialBlend = 0f; holeHum.dopplerLevel = 0f; holeHum.priority = 100;
            holeHum.volume = 0f;
            humLevel = 0f; humPaused = false;
        }

                                                                                                                                           
                                               
        void ClearHoleGuide()
        {
            if (holeHum) holeHum.Stop();
            if (holeGuide) Destroy(holeGuide.gameObject);
            if (guideMesh) Destroy(guideMesh);
            if (guideMaterial) Destroy(guideMaterial);
            holeGuide = null; guideShards = null; guideMesh = null; guideMaterial = null; holeHum = null;
            humLevel = 0f; humPaused = false; shardCount = 0;
        }

                                                                                                                                             
                                                                                                                                      
        void TickHoleGuide(WorldMap map)
        {
            if (!holeGuide) return;
            var hole = map.CampaignHole;
                                                                                                                                          
                                                                                                                                      
            if (!hole || hole.Phase == HolePhase.Gone) { ClearHoleGuide(); return; }
            bool shown = !map.CampaignHoleHidden;
            if (guideShards && guideShards.activeSelf != shown) guideShards.SetActive(shown);
            if (shown && guideMaterial) guideMaterial.SetFloat("_Intensity", guideGlow * (1f + ShardBreath * Mathf.Sin(Time.time * ShardBreathSpeed)));
            TickHoleHum(map, hole, shown);
        }

                                                                                                                                           
                                                                                                                                            
                                                                                                                                          
                                                                                                                                         
                                                                                                                                               
        void TickHoleHum(WorldMap map, BlackHole hole, bool shown)
        {
            if (!holeHum) return;
            bool paused = gm && gm.RunPhase == ArenaUiState.Paused;
            if (paused != humPaused)
            {
                humPaused = paused;
                if (paused) holeHum.Pause(); else holeHum.UnPause();
            }
            if (paused) return;
            var p = gm ? gm.player : null;
            float target = 0f, near = 0f;
            if (shown && p && !map.TrialActive && !map.TrialWon && Stage is (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk))
            {
                float ring = hole.GravityRadius;
                near = Mathf.InverseLerp(Mathf.Max(ring + 1f, rules.holeHumReach), ring, Query.Flat(p.Position - hole.Centre).magnitude);
                var audio = V3WorldAudio.Instance;
                target = rules.holeHumVolume * near * near * (audio ? audio.masterGain : 1f);
            }
            humLevel = Mathf.MoveTowards(humLevel, target, Time.unscaledDeltaTime * Mathf.Max(.05f, rules.holeHumVolume) / HumFadeSeconds);
            if (humLevel <= .001f)
            {
                if (holeHum.isPlaying) holeHum.Stop();
                return;
            }
            if (!holeHum.clip) holeHum.clip = V5WeaponAudio.Instance ? V5WeaponAudio.Instance.ClipOf(V5Sfx.LoopGravity) : null;
            if (!holeHum.clip) return;                                                     
            holeHum.volume = humLevel;
            if (target > 0f) holeHum.pitch = Mathf.Lerp(HumFarPitch, 1f, near);
            if (!holeHum.isPlaying) holeHum.Play();
        }

                                                                                                                                      
                                                                         
        Mesh BuildShardMesh(WorldMap map, BlackHole hole)
        {
            Vector3 centre = Query.Flat(hole.Centre), toHole = centre - BaseCentre;
            float distance = toHole.magnitude, ring = hole.GravityRadius + FieldRingGap;
            if (distance < LineStart + ring) return null;
            Vector3 dir = toHole / distance, side = new(-dir.z, 0f, dir.x);
            var rng = new System.Random(MapSeed * 31 + 7);
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            for (int i = 0; i < LineShards; i++)
                for (int attempt = 0; attempt < ShardTries; attempt++)
                {
                                                                                                                                    
                    float along = Mathf.Lerp(LineStart, distance - ring, Mathf.Sqrt(Unit(rng)));
                    Vector3 at = BaseCentre + dir * along + side * Mathf.Clamp(Gaussian(rng) * LineSpread, -LineHalfWidth, LineHalfWidth);
                    if (!ShardSpotOk(map, at)) continue;
                    AddShard(vertices, colours, triangles, rng, at, along / distance);
                    break;
                }
            for (int i = 0; i < FieldShards; i++)
                for (int attempt = 0; attempt < ShardTries; attempt++)
                {
                                                                           
                    float r = ring + (FieldReach - ring) * Mathf.Pow(Unit(rng), 1.8f), a = Unit(rng) * Mathf.PI * 2f;
                    Vector3 at = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!ShardSpotOk(map, at)) continue;
                    AddShard(vertices, colours, triangles, rng, at, 1f - .3f * (r - ring) / Mathf.Max(1f, FieldReach - ring));
                    break;
                }
            if (vertices.Count == 0) return null;
            shardCount = vertices.Count / 24;                                     
            var mesh = new Mesh { name = "Black hole shards" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[vertices.Count]));                                                                   
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

                                                                                                                                            
                                             
        static bool ShardSpotOk(WorldMap map, Vector3 at) =>
            Query.Flat(at - BaseCentre).magnitude >= BaseClearRadius && map.IsWalkable(at, ShardClearance) && Quicksand.SpeedFactor(at) > .99f;

                                                                                                                                              
                                                                                                                                         
                                                   
        static void AddShard(List<Vector3> vertices, List<Color> colours, List<int> triangles, System.Random rng, Vector3 at, float near)
        {
            near = Mathf.Clamp01(near);
                                                                                            
            float length = Mathf.Lerp(.16f, .36f, Unit(rng)) * (1f + .35f * near), width = length * Mathf.Lerp(.18f, .3f, Unit(rng));
            var turn = Quaternion.Euler(0f, Unit(rng) * 360f, 0f) * Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f, 32f, Unit(rng)));
            Vector3 axis = turn * Vector3.right, up = turn * Vector3.up, across = turn * Vector3.forward;
                                                                                                                                          
                                                  
            var middle = new Vector3(at.x, .02f + width * .35f, at.z);
            Vector3 tipA = middle + axis * (length * .5f), tipB = middle - axis * (length * .5f);
            float roll = Unit(rng) * Mathf.PI * .5f;
            var rim = new Vector3[4];
            for (int j = 0; j < 4; j++)
            {
                float a = roll + j * Mathf.PI * .5f;
                rim[j] = middle + (up * Mathf.Cos(a) + across * Mathf.Sin(a)) * (width * (j % 2 == 0 ? 1f : .6f));
            }
            Color tint = Color.Lerp(ShardDeep, ShardBright, Unit(rng) * .3f);
            float brightness = Mathf.Lerp(.7f, 1f, near);
            for (int j = 0; j < 4; j++)
            {
                Face(vertices, colours, triangles, tipA, rim[j], rim[(j + 1) % 4], tint, brightness);
                Face(vertices, colours, triangles, tipB, rim[(j + 1) % 4], rim[j], tint, brightness);
            }
        }

        static void Face(List<Vector3> vertices, List<Color> colours, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Color tint, float brightness)
        {
                                                                                                                              
            float lit = Mathf.Abs(Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, ShardLight));
            Color colour = Color.Lerp(tint * .8f, ShardBright, lit * .6f) * brightness;
            colour.a = .92f;
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colours.Add(colour); colours.Add(colour); colours.Add(colour);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
        }

                                                                                                                                          
                                                                  
        static Material ShardMaterial(float glow)
        {
            var shader = Resources.Load<Shader>("V5/V5Particle");
            if (!shader) return null;
            var material = new Material(shader) { name = "Black hole shards" };
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Intensity", glow);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            return material;
        }

        static float Unit(System.Random rng) => (float)rng.NextDouble();

                                                                     
        static float Gaussian(System.Random rng) => Mathf.Sqrt(-2f * Mathf.Log(1f - Unit(rng))) * Mathf.Cos(2f * Mathf.PI * Unit(rng));
    }
}
