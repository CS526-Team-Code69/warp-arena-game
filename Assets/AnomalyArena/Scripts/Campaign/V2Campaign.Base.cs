using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                                                                   
    public enum CampaignUpgrade { Stat, Skill, Infusion, Tame, Twist }

                 
                                                                                                                                         
                                                                                                                                          
                                                                                                                                           
                                                                                                                                        
                                              
                  
    public sealed partial class V2Campaign
    {
        public sealed class RoomState { public int level; public bool damaged; }

                                                                                                                 
        public static readonly CampaignRoom[] SideRoom = { CampaignRoom.Ship, CampaignRoom.Armament, CampaignRoom.Control, CampaignRoom.Energy };
        public static readonly Vector3[] SideNormal = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        public const float BaseHalf = 5f;                                                                     
        public static readonly Vector3 BaseCentre = Vector3.zero;
        public static readonly Vector3 ShipCentre = new(0f, 0f, 13f);
        public const float ShipHalfLength = 7f, ShipHalfWidth = 3f;
        public Vector3 DoorPoint => BaseCentre + Vector3.back * (BaseHalf + 1.4f);

        readonly RoomState[] rooms = { new(), new(), new(), new() };
        public RoomState Room(CampaignRoom room) => rooms[(int)room];
        public int Durability { get; private set; } = 1;
        public int ShieldLevel { get; private set; }
        public float BaseHp { get; private set; }
        public float BaseShield { get; private set; }
        public float BaseMaxHp => rules.BaseMaxHp(Durability);
        public float BaseMaxShield => BaseMaxHp * rules.shieldShare * ShieldLevel;
        readonly float[] sideDamageToday = new float[4];
        readonly float[] sideFlash = new float[4];
        bool engineFixed, armorFixed;
        int fuel;
        public bool EngineFixed => engineFixed;
        public bool ArmorFixed => armorFixed;
        public int Fuel => fuel;
        public bool FuelFull => fuel >= rules.fuelUnits;
        float shieldTopUpAt;

        Transform worldRoot, baseRoot, shipRoot;
        Renderer[] baseRenderers;
        readonly Renderer[] sidePanels = new Renderer[4];
        Material[] baseIntactMaterials;
        GameObject shipWreckView, shipIntactView;
        Renderer engineBlock, armorPlates, fuelTank;
        Light baseLight;
        V2ShipView shipView;
                                                                                                                                       
        readonly bool[] sideShowsDamage = new bool[4];
        readonly List<GameObject>[] sideScorches = { new(), new(), new(), new() };
        bool sidesSplit;
        readonly List<Mesh> splitMeshes = new();
        static Material scorchMaterial;
        static readonly Mesh[] scorchMeshes = new Mesh[4];

                                                                                                       
        public Texture ShipViewTexture => shipView ? shipView.Texture : null;
        public void ShowShipView(bool on) { if (shipView) shipView.Live = on; }
                                                                                                                       
        public bool SideShowsDamage(int side) => sideShowsDamage[side];
        public int ScorchCount(int side) => sideScorches[side].Count;
        public bool SidesSplit => sidesSplit;
        readonly Dictionary<Enemy, (int side, Vector3 spot)> raidSpots = new();

        void ResetBase()
        {
            foreach (var room in rooms) { room.level = 0; room.damaged = false; }
            rooms[(int)CampaignRoom.Control].level = 1;                                                                          
            Durability = 1; ShieldLevel = 0; BaseHp = rules.BaseMaxHp(1); BaseShield = 0f;
            System.Array.Clear(sideDamageToday, 0, 4);
            engineFixed = armorFixed = false; fuel = 0; shieldTopUpAt = 0f;
            weaponDamageBought = newWeaponsBought = 0;
            System.Array.Clear(upgradesBought, 0, upgradesBought.Length);
            raidSpots.Clear();
        }

                                    

        void BuildWorld()
        {
            if (worldRoot) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("V2 Campaign World").transform;
            BuildBase();
            BuildShip();
            var map = WorldMap.Active;
            if (map)
            {
                var walls = new List<Rect>
                {
                    new(BaseCentre.x - BaseHalf, BaseCentre.z - BaseHalf, BaseHalf * 2f, BaseHalf * 2f),
                    new(ShipCentre.x - ShipHalfLength, ShipCentre.z - ShipHalfWidth, ShipHalfLength * 2f, ShipHalfWidth * 2f),
                };
                map.SetExtraWalls(walls);
            }
            BuildDarkness();
        }

        void BuildBase()
        {
            baseRoot = new GameObject("Base").transform;
            baseRoot.SetParent(worldRoot, false);
            baseRoot.position = BaseCentre;
            AddWallCollider(baseRoot.gameObject, new Vector3(BaseHalf * 2f, 4f, BaseHalf * 2f), new Vector3(0f, 2f, 0f));
            System.Array.Clear(sideShowsDamage, 0, 4);
            foreach (var list in sideScorches) list.Clear();
            sidesSplit = false;
            foreach (var mesh in splitMeshes) if (mesh) Destroy(mesh);                                              
            splitMeshes.Clear();
            if (art && art.basePrefab && Art.Enabled)
            {
                var model = Instantiate(art.basePrefab, baseRoot);
                model.name = "BaseModel";
                                                                                                                  
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                FitFootprint(model.transform, BaseHalf * 2f);
                StripColliders(model);
                baseRenderers = model.GetComponentsInChildren<Renderer>();
                baseIntactMaterials = baseRenderers.Length > 0 ? baseRenderers[0].sharedMaterials : null;
                sidesSplit = SplitBaseSides(model);
            }
            else
            {
                var core = Box(baseRoot, "Core", new Vector3(0f, 1.6f, 0f), new Vector3(BaseHalf * 2f - .6f, 3.2f, BaseHalf * 2f - .6f), new Color(.36f, .42f, .5f));
                Box(baseRoot, "Roof", new Vector3(0f, 3.5f, 0f), new Vector3(5f, .6f, 5f), new Color(.55f, .62f, .7f));
                Box(baseRoot, "Door", new Vector3(0f, 1.1f, -BaseHalf), new Vector3(2.4f, 2.2f, .3f), new Color(.15f, .85f, 1f));
                baseRenderers = new[] { core };
            }
                                                                                                                                      
                                          
            bool onModel = baseRenderers != null && baseRenderers.Length > 0 && baseRenderers[0] && baseRenderers[0].name != "Core";
            for (int side = 0; side < 4; side++)
            {
                Vector3 n = SideNormal[side];
                Vector3 size = onModel ? side % 2 == 0 ? new Vector3(7f, .08f, .3f) : new Vector3(.3f, .08f, 7f)
                    : side % 2 == 0 ? new Vector3(6f, .5f, .35f) : new Vector3(.35f, .5f, 6f);
                Vector3 at = onModel ? n * (BaseHalf + .45f) + Vector3.up * .05f : n * (BaseHalf + .05f) + Vector3.up * 3.4f;
                sidePanels[side] = Box(baseRoot, "Side " + SideRoom[side], at, size, RoomColour(SideRoom[side]));
                sidePanels[side].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lightGo = new GameObject("BaseLight");
            lightGo.transform.SetParent(baseRoot, false);
            lightGo.transform.localPosition = new Vector3(0f, 6f, 0f);
            baseLight = lightGo.AddComponent<Light>();
            baseLight.type = LightType.Point; baseLight.range = rules.baseLightRadius * 1.4f; baseLight.intensity = 0f;
            baseLight.color = new Color(.75f, .9f, 1f);
        }

        void BuildShip()
        {
            shipRoot = new GameObject("Ship").transform;
            shipRoot.SetParent(worldRoot, false);
            shipRoot.position = ShipCentre;
            AddWallCollider(shipRoot.gameObject, new Vector3(ShipHalfLength * 2f, 3f, ShipHalfWidth * 2f), new Vector3(0f, 1.5f, 0f));
            if (art && Art.Enabled && art.shipWreckPrefab)
            {
                shipWreckView = Instantiate(art.shipWreckPrefab, shipRoot);
                shipWreckView.name = "ShipWreck";
                FitFootprint(shipWreckView.transform, ShipHalfLength * 2f, true);
                StripColliders(shipWreckView);
                if (art.shipIntactPrefab)
                {
                    shipIntactView = Instantiate(art.shipIntactPrefab, shipRoot);
                    shipIntactView.name = "ShipIntact";
                    FitFootprint(shipIntactView.transform, ShipHalfLength * 2f, true);
                    StripColliders(shipIntactView);
                    shipIntactView.SetActive(false);
                }
            }
            else
            {
                Box(shipRoot, "Hull", new Vector3(0f, 1.3f, 0f), new Vector3(10f, 2.2f, 3.6f), new Color(.82f, .84f, .86f));
                Box(shipRoot, "Nose", new Vector3(5.8f, 1.2f, 0f), new Vector3(1.8f, 1.6f, 2.4f), new Color(.9f, .55f, .2f));
            }
                                                                                                         
            engineBlock = Box(shipRoot, "Engine", new Vector3(-6.2f, 1.4f, 0f), new Vector3(1.4f, 1.8f, 2.6f), new Color(.3f, .3f, .32f));
            armorPlates = Box(shipRoot, "ArmorPlates", new Vector3(0f, 2.75f, 0f), new Vector3(7f, .3f, 3f), new Color(.3f, .3f, .32f));
            fuelTank = Box(shipRoot, "FuelTank", new Vector3(-2.5f, 0.6f, -2.4f), new Vector3(3f, 1f, 1f), new Color(.2f, .1f, .25f));
            if (Art.Enabled && art && art.shipWreckPrefab)
            {
                                                                                              
                engineBlock.transform.localScale = new Vector3(.5f, .5f, .5f);
                armorPlates.transform.localScale = new Vector3(2.5f, .15f, .5f);
                fuelTank.transform.localScale = new Vector3(1.6f, .35f, .35f);
            }
            RefreshShip();
            shipView = V2ShipView.Create(worldRoot, ShipCentre);
        }

                                                                                                                                            
                                                                                                                                             
                                                                                                                                       
                                                               
        bool SplitBaseSides(GameObject model)
        {
            var filters = model.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return false;
            foreach (var f in filters)
                if (!f.sharedMesh || !f.sharedMesh.isReadable || f.sharedMesh.subMeshCount != 1 || !f.TryGetComponent<Renderer>(out _)) return false;
            foreach (var f in filters)
            {
                var source = f.sharedMesh;
                Matrix4x4 toBase = baseRoot.worldToLocalMatrix * f.transform.localToWorldMatrix;
                var vertices = source.vertices;
                var triangles = source.triangles;
                var parts = new List<int>[4];
                for (int s = 0; s < 4; s++) parts[s] = new List<int>(triangles.Length / 4 + 3);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    Vector3 c = toBase.MultiplyPoint3x4((vertices[triangles[t]] + vertices[triangles[t + 1]] + vertices[triangles[t + 2]]) / 3f);
                    int side = Mathf.Abs(c.x) > Mathf.Abs(c.z) ? c.x > 0f ? 1 : 3 : c.z > 0f ? 0 : 2;
                    parts[side].Add(triangles[t]); parts[side].Add(triangles[t + 1]); parts[side].Add(triangles[t + 2]);
                }
                var mesh = Instantiate(source);
                mesh.name = source.name + " (four sides)";
                mesh.subMeshCount = 4;
                for (int s = 0; s < 4; s++) mesh.SetTriangles(parts[s], s, false);
                f.sharedMesh = mesh;
                splitMeshes.Add(mesh);
                var r = f.GetComponent<Renderer>();
                var intact = r.sharedMaterial;
                r.sharedMaterials = new[] { intact, intact, intact, intact };
            }
            return true;
        }

                                                                                                                                         
                                                                                                               
        void RefreshSideDamage()
        {
            bool hull = BaseHp < BaseMaxHp * .5f;
            bool changed = false;
            for (int side = 0; side < 4; side++)
            {
                bool show = rooms[(int)SideRoom[side]].damaged || hull;
                if (show == sideShowsDamage[side]) continue;
                sideShowsDamage[side] = show;
                changed = true;
                SetScorches(side, show);
            }
            if (!changed || baseRenderers == null || !art || !art.baseDamagedMaterial || baseIntactMaterials == null || baseIntactMaterials.Length == 0) return;
            var intact = baseIntactMaterials[0];
            bool any = sideShowsDamage[0] || sideShowsDamage[1] || sideShowsDamage[2] || sideShowsDamage[3];
            foreach (var r in baseRenderers)
            {
                if (!r) continue;
                var materials = r.sharedMaterials;
                if (sidesSplit && materials.Length == 4)
                {
                    for (int s = 0; s < 4; s++) materials[s] = sideShowsDamage[s] ? art.baseDamagedMaterial : intact;
                    r.sharedMaterials = materials;
                }
                else r.sharedMaterial = any ? art.baseDamagedMaterial : intact;
            }
        }

                                                                                                                                          
                                                                    
        void SetScorches(int side, bool on)
        {
            var list = sideScorches[side];
            if (!on)
            {
                foreach (var go in list) if (go) Destroy(go);
                list.Clear();
                return;
            }
            if (list.Count > 0 || !Art.Enabled || !baseRoot) return;
            var material = ScorchMaterial();
            if (!material) return;
            var rng = new System.Random(side * 7919 + Day * 31 + 5);
            const int marks = 4;
            for (int i = 0; i < marks; i++)
            {
                float along = -BaseHalf + 1.2f + i * (BaseHalf * 2f - 2.4f) / (marks - 1) + (float)(rng.NextDouble() - .5) * 1.4f;
                Vector3 at = BaseCentre + SideNormal[side] * (BaseHalf + .5f + (float)rng.NextDouble() * .9f) + Tangent(side) * along;
                var go = new GameObject("Burn mark");
                go.transform.SetParent(baseRoot, true);
                go.transform.SetPositionAndRotation(new Vector3(at.x, .05f + i * .004f, at.z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                go.transform.localScale = Vector3.one * (2.9f + (float)rng.NextDouble() * 1.7f);
                go.AddComponent<MeshFilter>().sharedMesh = ScorchMesh(rng.Next(4));
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                list.Add(go);
            }
        }

                                                                                                                                     
                                                 
        static Material ScorchMaterial()
        {
            if (scorchMaterial) return scorchMaterial;
            var shader = Resources.Load<Shader>("V5/V5Particle");
            var texture = Resources.Load<Texture2D>("V5/Fx/scorch");
            if (!shader || !texture) return null;
            scorchMaterial = new Material(shader) { name = "Base burn marks" };
            scorchMaterial.SetTexture("_BaseMap", texture);
            scorchMaterial.SetColor("_BaseColor", new Color(.05f, .04f, .035f, .8f));
            scorchMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            scorchMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            scorchMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            return scorchMaterial;
        }

                                                                                                                                                   
        static Mesh ScorchMesh(int tile)
        {
            tile = Mathf.Clamp(tile, 0, 3);
            if (scorchMeshes[tile]) return scorchMeshes[tile];
            float u = tile % 2 * .5f, v = tile / 2 * .5f;
            var mesh = new Mesh { name = "Burn mark " + tile };
            mesh.vertices = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(.5f, 0f, -.5f), new Vector3(-.5f, 0f, .5f), new Vector3(.5f, 0f, .5f) };
            mesh.uv = new[] { new Vector2(u, v), new Vector2(u + .5f, v), new Vector2(u, v + .5f), new Vector2(u + .5f, v + .5f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return scorchMeshes[tile] = mesh;
        }

        static void AddWallCollider(GameObject go, Vector3 size, Vector3 centre)
        {
            go.layer = GameManager.WallLayer;
            var box = go.AddComponent<BoxCollider>();
            box.size = size; box.center = centre;
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }

                                                                                                                                          
                                                                              
        static void FitFootprint(Transform model, float size, bool alongX = false)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (alongX && bounds.size.z > bounds.size.x * 1.05f)
            {
                model.Rotate(0f, 90f, 0f, Space.World);
                bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            }
            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest > .01f) model.localScale *= size / longest;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var parent = model.parent ? model.parent.position : Vector3.zero;
            model.position += new Vector3(parent.x - bounds.center.x, parent.y - bounds.min.y, parent.z - bounds.center.z);
        }

        Renderer Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            if (gm && gm.litMaterial) r.sharedMaterial = gm.Mat(colour);
            return r;
        }

        public static Color RoomColour(CampaignRoom room) => room switch
        {
            CampaignRoom.Control => new Color(.2f, .85f, 1f),
            CampaignRoom.Armament => new Color(1f, .58f, .18f),
            CampaignRoom.Energy => new Color(.72f, .4f, 1f),
            _ => new Color(.95f, .95f, .9f)
        };

        void RefreshShip()
        {
            if (!shipRoot) return;
            if (shipIntactView) { shipIntactView.SetActive(armorFixed); if (shipWreckView) shipWreckView.SetActive(!armorFixed); }
            SetColour(engineBlock, engineFixed ? new Color(.45f, .8f, 1f) : new Color(.3f, .3f, .32f));
            SetColour(armorPlates, armorFixed ? new Color(.92f, .92f, .95f) : new Color(.3f, .3f, .32f));
            float f = rules ? fuel / (float)Mathf.Max(1, rules.fuelUnits) : 0f;
            SetColour(fuelTank, Color.Lerp(new Color(.2f, .1f, .25f), new Color(.78f, .3f, 1f), f));
        }

        void SetColour(Renderer r, Color c)
        {
            if (r && gm && gm.litMaterial) r.sharedMaterial = gm.Mat(c);
        }

        void TickBaseVisuals(float dt)
        {
            for (int side = 0; side < 4; side++)
            {
                sideFlash[side] = Mathf.Max(0f, sideFlash[side] - dt * 3f);
                var room = rooms[(int)SideRoom[side]];
                Color baseColour = room.damaged ? new Color(1f, .2f, .15f) : room.level <= 0 ? new Color(.25f, .25f, .27f) : RoomColour(SideRoom[side]);
                if (sidePanels[side]) Art.Tint(sidePanels[side], Color.Lerp(baseColour, Color.white, sideFlash[side]));
                                        
                if (room.damaged && Random.value < dt * 3f)
                    Fx.Pop(BaseCentre + SideNormal[side] * (BaseHalf + .3f) + Vector3.up * Random.Range(1f, 3.5f) + Tangent(side) * Random.Range(-3f, 3f), new Color(.25f, .25f, .25f), Random.Range(.8f, 1.4f));
            }
            RefreshSideDamage();
        }

        static Vector3 Tangent(int side) => side % 2 == 0 ? Vector3.right : Vector3.forward;

                            

                                                                                                                                          
                                                                                                                                           
                           
        void OnEnemySpawned(Enemy enemy)
        {
            if (!Running || !enemy || !enemy.definition) return;
            enemy.Raider = false;
            if (Stage is not (CampaignStage.Day or CampaignStage.Lull)) return;
            if (enemy.IsBoss || enemy.IsTrialSummon || enemy.Immovable) return;
            enemy.Raider = true;
        }

                                                                                                                                                    
        Vector3? RaidSpotFor(Enemy enemy)
        {
            if (!Running || BaseDestroyed || Stage is not (CampaignStage.Day or CampaignStage.Lull)) return null;
            if (raidSpots.TryGetValue(enemy, out var known)) return known.spot;
            Vector3 d = Query.Flat(enemy.Position - BaseCentre);
            int side = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? d.x > 0f ? 1 : 3 : d.z > 0f ? 0 : 2;
            float along = Mathf.Clamp(Vector3.Dot(d, Tangent(side)) + Random.Range(-2f, 2f), -BaseHalf + 1f, BaseHalf - 1f);
            Vector3 spot = BaseCentre + SideNormal[side] * (BaseHalf + enemy.Radius + .35f) + Tangent(side) * along;
            raidSpots[enemy] = (side, spot);
            return spot;
        }

        void OnRaidStrike(Enemy enemy, float damage)
        {
            int side = raidSpots.TryGetValue(enemy, out var known) ? known.side : 2;
            Vector3 at = raidSpots.TryGetValue(enemy, out known) ? known.spot - SideNormal[side] * .4f + Vector3.up : BaseCentre;
            DamageBase(side, damage * rules.raidDamage, at);
        }

                                                                                                                                     
                                                                                         
        public void DamageBase(int side, float amount, Vector3 at)
        {
            if (!Running || BaseDestroyed || amount <= 0f) return;
            float toShield = Mathf.Min(BaseShield, amount);
            BaseShield -= toShield;
            float toHp = amount - toShield;
            BaseHp -= toHp;
            CampaignEvents.RaiseBaseDamaged(amount, toHp, SideRoom[side]);
            sideFlash[side] = 1f;
            gm.hud?.DamageNumber(at, amount, toHp > 0f ? new Color(1f, .55f, .2f) : new Color(.5f, .9f, 1f), false);
            Fx.Pop(at, toHp > 0f ? new Color(1f, .5f, .2f) : new Color(.5f, .9f, 1f), .9f);
            if (toHp > 0f)
            {
                sideDamageToday[side] += toHp;
                var room = rooms[(int)SideRoom[side]];
                if (!room.damaged && room.level > 0 && sideDamageToday[side] >= BaseMaxHp * rules.sideBreakShare)
                {
                    room.damaged = true;
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcBloaterBlast, at, .8f, .7f);
                    SayRoomOffline(SideRoom[side]);
                }
            }
            if (BaseHp <= 0f)
            {
                BaseHp = 0f; BaseDestroyed = true;
                V3WorldAudio.Instance?.Play(ArenaWorldCue.BossDeath, BaseCentre);
                gm.Shake(.6f);
            }
        }

                                                                                                                                    
                                                      
        void NightlyBaseUpkeep()
        {
            BaseHp = Mathf.Min(BaseMaxHp, BaseHp + BaseMaxHp * rules.nightSelfRepair);
            if (ShieldLevel > 0 && !rooms[(int)CampaignRoom.Control].damaged) { BaseShield = BaseMaxShield; Say(Line.S06); }
            System.Array.Clear(sideDamageToday, 0, 4);
        }

                                                             

        public bool ControlWorks => !rooms[(int)CampaignRoom.Control].damaged;

        public CampaignCost? NextLevelCost(CampaignRoom room)
        {
            int level = rooms[(int)room].level;
            var table = room switch
            {
                CampaignRoom.Armament => rules.armamentLevels,
                CampaignRoom.Energy => rules.energyLevels,
                CampaignRoom.Ship => rules.shipLevels,
                _ => null
            };
            if (table == null || level >= table.Length) return null;
            return table[level];
        }

                                                                                                                         
        public bool TryUpgradeRoom(CampaignRoom room)
        {
            var cost = NextLevelCost(room);
            if (cost == null || !ControlWorks || rooms[(int)room].damaged || !TrySpend(cost.Value)) return false;
            rooms[(int)room].level++;
            Bought("room_upgrade", $"{room} level {rooms[(int)room].level}", room, cost.Value);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(ArenaText.Pair($"{RoomName(room)} reached level {rooms[(int)room].level}",$"{RoomName(room)} reached level {rooms[(int)room].level}"));
            return true;
        }

        public int RepairCost(CampaignRoom room) => room == CampaignRoom.Control ? rules.ControlRepairCost(Day) : rules.RoomRepairCost(rooms[(int)room].level, Day);

                                                                                                                                             
        public bool TryRepairRoom(CampaignRoom room)
        {
            var state = rooms[(int)room];
            if (!state.damaged || room != CampaignRoom.Control && !ControlWorks) return false;
            var repair = new CampaignCost(RepairCost(room));
            if (!TrySpend(repair)) return false;
            state.damaged = false;
            Bought("repair", room.ToString(), room, repair);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair, BaseCentre);
            Say(Line.S05);
            return true;
        }

                                                                                                                                           
                                                                                     
        public CampaignCost? PlayerLevelCost => gm && gm.progression && !gm.progression.AtMaxLevel ? rules.PlayerLevelCost(gm.progression.Level) : null;

                                                                                                                          
        public bool TryBuyPlayerLevel()
        {
            var cost = PlayerLevelCost;
            if (cost == null || !PlayerLevelsForSale || !TrySpend(cost.Value)) return false;
            gm.progression.BuyLevel();
            Bought("player_level", $"level {gm.progression.Level}", CampaignRoom.Armament, cost.Value);
            ui.Flash(ArenaText.Pair(                $"Armor level {gm.progression.Level}: max HP +{Mathf.RoundToInt(gm.progression.LastLevelHpGain)}, max energy +{Mathf.RoundToInt(gm.progression.LastLevelEnergyGain)}",                $"Armor level {gm.progression.Level}: max HP +{Mathf.RoundToInt(gm.progression.LastLevelHpGain)}, max energy +{Mathf.RoundToInt(gm.progression.LastLevelEnergyGain)}"));
            return true;
        }

                                                                                                                                          
                                                                                                                                   

        int weaponDamageBought, newWeaponsBought;
        readonly int[] upgradesBought = new int[5];

        public bool WeaponDamageForSale => Running && Works(CampaignRoom.Armament, 1);
        public bool NewWeaponForSale => Running && Works(CampaignRoom.Armament, 2);
        public CampaignCost WeaponDamageCost => CampaignRules.Rising(rules.weaponResearchCost, rules.weaponResearchCostPerBuy, weaponDamageBought);
        public CampaignCost NewWeaponCost => CampaignRules.Rising(rules.newWeaponCost, rules.newWeaponCostPerBuy, newWeaponsBought);
                                                                                                                                            
                                               
        public bool WeaponSlotsFull => gm && gm.runWeapons && !System.Array.Exists(gm.runWeapons.Hand, w => w == null || w.form == WeaponForm.Pistol)
            && !System.Array.Exists(gm.runWeapons.Auto, w => w == null);
                                                                                                                                                       
        public bool HoldsAnomalyWeapon
        {
            get
            {
                var weapons = gm ? gm.runWeapons : null;
                if (!weapons) return false;
                foreach (var w in weapons.Hand) if (w != null && w.IsAnomalous) return true;
                foreach (var w in weapons.Auto) if (w != null && w.IsAnomalous) return true;
                return false;
            }
        }
                                                                                                                                                 
        public bool NewWeaponReplacesPistol => gm && gm.runWeapons && PistolHandIfFull(gm.runWeapons) >= 0;
                                                                                                  
        static int PistolHandIfFull(RunWeapons weapons)
        {
            if (System.Array.Exists(weapons.Hand, w => w == null)) return -1;
            return System.Array.FindIndex(weapons.Hand, w => w != null && w.form == WeaponForm.Pistol);
        }

        public static CampaignRoom UpgradeRoom(CampaignUpgrade kind) => kind is CampaignUpgrade.Tame or CampaignUpgrade.Twist ? CampaignRoom.Armament : CampaignRoom.Energy;
        public static int UpgradeLevel(CampaignUpgrade kind) => kind switch
        {
            CampaignUpgrade.Stat => 1,
            CampaignUpgrade.Skill or CampaignUpgrade.Tame => 2,
            _ => 3                                                   
        };
        public bool UpgradeForSale(CampaignUpgrade kind) => Running && Works(UpgradeRoom(kind), UpgradeLevel(kind));
        public CampaignCost UpgradeCost(CampaignUpgrade kind)
        {
            int bought = upgradesBought[(int)kind];
            return kind switch
            {
                CampaignUpgrade.Stat => CampaignRules.Rising(rules.statCost, rules.statCostPerBuy, bought),
                CampaignUpgrade.Skill => CampaignRules.Rising(rules.skillCost, rules.skillCostPerBuy, bought),
                CampaignUpgrade.Infusion => CampaignRules.Rising(rules.infusionCost, rules.infusionCostPerBuy, bought),
                CampaignUpgrade.Tame => CampaignRules.Rising(rules.tameCost, rules.tameCostPerBuy, bought),
                _ => CampaignRules.Rising(rules.twistCost, rules.twistCostPerBuy, bought)
            };
        }
        public static string UpgradeName(CampaignUpgrade kind) => kind switch
        {
            CampaignUpgrade.Stat => ArenaText.Pair("Upgrade the body","Upgrade the body"),
            CampaignUpgrade.Skill => ArenaText.Pair("Unlock an alien skill","Unlock an alien skill"),
            CampaignUpgrade.Infusion => ArenaText.Pair("Infuse a weapon","Infuse a weapon"),
            CampaignUpgrade.Tame => ArenaText.Pair("Tame a weapon","Tame a weapon"),
            _ => ArenaText.Pair("Twist a weapon","Twist a weapon")
        };

                                                                                                                                              
                                                                                                                                           
                                                                                                                                       
        public bool TryBuyWeaponDamage()
        {
            var weapons = gm ? gm.runWeapons : null;
            if (!WeaponDamageForSale || !weapons || !gm.waves) return false;
            if (!HoldsAnomalyWeapon)
            {
                V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
                ui.Flash(ArenaText.Pair("The pistol cannot be boosted: unlock an anomaly weapon at the Armament Bay first","The pistol cannot be boosted: unlock an anomaly weapon at the Armament Bay first"));
                return false;
            }
            var cost = WeaponDamageCost;
            if (!CanAfford(cost)) { TrySpend(cost); return false; }                                                       
            armoryMode = ArmoryMode.UpgradesOnly;
                                                                                                       
            bool handBoostable = weapons.Active != null && weapons.Active.IsAnomalous;
            try
            {
                int index = -1;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    weapons.RollArmory(gm.waves.WaveIndex + 2);
                    var offers = weapons.Offers;
                    int other = -1, active = -1;
                    for (int i = 0; i < offers.Length; i++)
                    {
                        if (offers[i]?.upgradeOf == null || !offers[i].upgradeOf.IsAnomalous) continue;
                        if (offers[i].upgradeOf == weapons.Active) { active = i; break; }
                        if (other < 0) other = i;
                    }
                    index = active >= 0 ? active : other;
                                                                                                                                        
                    if (active >= 0 || index < 0 || !handBoostable) break;
                }
                if (index < 0)
                {
                    ui.Flash(ArenaText.Pair("No weapon can be boosted now: every weapon is at today's Mk limit","No weapon can be boosted now: every weapon is at today's Mk limit"));
                    return false;
                }
                var owned = weapons.Offers[index].upgradeOf;
                string before = owned.MkLabel;
                if (!TrySpend(cost)) return false;
                if (!weapons.TakeOffer(index)) { Refund(cost); return false; }
                weaponDamageBought++;
                Bought("weapon_damage", $"{owned.form} Mk{owned.mk}", CampaignRoom.Armament, cost);
                V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
                ui.Flash(ArenaText.Pair($"Weapon damage up: {owned.FormName} {before} → {owned.MkLabel}",$"Weapon damage up: {owned.FormName} {before} → {owned.MkLabel}"));
                return true;
            }
            finally
            {
                armoryMode = ArmoryMode.Rooms;
                weapons.Offers = new RunWeapon[RunWeapons.ArmoryOfferCount];
            }
        }

                                                                                                                                             
                                                                                                                                             
                                                                                                                       
        public bool TryBuyNewWeapon()
        {
            var weapons = gm ? gm.runWeapons : null;
            if (!NewWeaponForSale || !weapons || !gm.waves) return false;
            if (WeaponSlotsFull) { ui.Flash(ArenaText.Pair("Every weapon slot is taken: no room for a new weapon","Every weapon slot is taken: no room for a new weapon")); return false; }
            var cost = NewWeaponCost;
            if (!CanAfford(cost)) { TrySpend(cost); return false; }
            RunWeapon got = null;
            string message = "";
            armoryMode = ArmoryMode.NewWeaponsOnly;
            try
            {
                for (int attempt = 0; attempt < 3 && got == null; attempt++)
                {
                    weapons.RollArmory(gm.waves.WaveIndex + 2);
                    var offers = weapons.Offers;
                    for (int i = 0; i < offers.Length; i++)
                    {
                        var weapon = offers[i];
                        if (weapon == null || weapon.upgradeOf != null || !weapon.IsAnomalous) continue;
                        int replace = weapon.IsAuto ? -1 : PistolHandIfFull(weapons);
                        if (replace < 0 && !System.Array.Exists(weapon.IsAuto ? weapons.Auto : weapons.Hand, w => w == null)) continue;
                        var pistol = replace >= 0 ? weapons.Hand[replace] : null;
                        if (!TrySpend(cost)) return false;
                        if (!weapons.TakeOffer(i, replace)) { Refund(cost); return false; }
                        got = weapon;
                        newWeaponsBought++;
                        Bought("new_weapon", $"{weapon.form} Mk{weapon.mk} {(weapon.IsAuto ? "auto" : "hand")}{(pistol != null ? ", replaces Pistol" : "")}", CampaignRoom.Armament, cost);
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
                        message = weapon.IsAuto
                            ? ArenaText.Pair($"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}, auto slot)",$"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}, auto slot)")
                            : pistol != null
                                ? ArenaText.Pair($"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}); it replaces the {pistol.FormName}",$"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}); it replaces the {pistol.FormName}")
                                : ArenaText.Pair($"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}, hand slot)",$"New anomaly weapon researched: {weapon.FormName} ({weapon.MkLabel}, hand slot)");
                        break;
                    }
                }
            }
            finally
            {
                armoryMode = ArmoryMode.Rooms;
                weapons.Offers = new RunWeapon[RunWeapons.ArmoryOfferCount];
            }
            if (got == null)
            {
                ui.Flash(ArenaText.Pair("No new weapon can be researched right now","No new weapon can be researched right now"));
                return false;
            }
            AnnounceWeapon(got, message);
            return true;
        }

                                                                                                                                            
                                                                                                                                          
                                                                                                                                          
                                        
        void AnnounceWeapon(RunWeapon weapon, string message)
        {
            if (weapon == null || !ui.BaseOpen || Stage != CampaignStage.Base) { ui.Flash(message, 3.5f); return; }
            ui.ClearFlash();
            ui.HideBase();
            gm.CampaignShowWeaponCard(weapon, () =>
            {
                if (!Running || Stage != CampaignStage.Base) return;
                gm.CampaignSetPhase(ArenaUiState.Base);
                ui.ResumeBase();
                ui.Flash(message, 3.5f);
            });
        }

                                                                                                                                            
                                                                                                                                         
                                                                                                                                           
                                                                              
        public bool TryBuyUpgrade(CampaignUpgrade kind)
        {
            if (!UpgradeForSale(kind) || !gm || !gm.progression) return false;
            var cost = UpgradeCost(kind);
            if (!CanAfford(cost)) { TrySpend(cost); return false; }
            int bankedBefore = gm.progression.BankedDrafts;
            string got = null;
            activeUpgrade = kind;
            try
            {
                                                                                                                                          
                                                                                             
                for (int attempt = 0; attempt < 3 && string.IsNullOrEmpty(got); attempt++)
                {
                    got = gm.CampaignApplyUpgrade();
                                                                                                                                
                    while (gm.progression.BankedDrafts > bankedBefore) gm.progression.ConsumeDraft();
                }
            }
            finally { activeUpgrade = null; }
            if (string.IsNullOrEmpty(got))
            {
                ui.Flash(ArenaText.Pair($"{UpgradeName(kind)}: nothing can be upgraded right now",$"{UpgradeName(kind)}: nothing can be upgraded right now"));
                return false;
            }
                                                                                                    
            if (!TrySpend(cost)) return false;
            upgradesBought[(int)kind]++;
            Bought(UpgradeItem(kind), got, UpgradeRoom(kind), cost);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            ui.Flash(ArenaText.Pair($"{UpgradeName(kind)}: {got}",$"{UpgradeName(kind)}: {got}"));
            return true;
        }

                                                                                                                                                     

        public bool RocketForSale => Running && Works(CampaignRoom.Armament, 2) && gm && gm.runWeapons && !gm.runWeapons.OwnsForm(WeaponForm.RocketLauncher);
        public CampaignCost RocketCost => rules.rocketLauncherCost;
                                                                                                                                            
                                                                                                                                   
        int RocketHand
        {
            get
            {
                var weapons = gm ? gm.runWeapons : null;
                if (!weapons) return 1;
                int free = System.Array.FindIndex(weapons.Hand, w => w == null);
                if (free >= 0) return free;
                int pistol = PistolHandIfFull(weapons);
                return pistol >= 0 ? pistol : 1 - weapons.ActiveIndex;
            }
        }
                                                                                                                   
        public RunWeapon RocketReplaces => gm && gm.runWeapons ? gm.runWeapons.Hand[RocketHand] : null;
        public bool TryBuyRocketLauncher()
        {
            var weapons = gm ? gm.runWeapons : null;
            if (!RocketForSale || !weapons || !TrySpend(RocketCost)) return false;
            int hand = RocketHand;
            var old = weapons.GiveToHand(hand, WeaponForm.RocketLauncher);
            var rocket = weapons.Hand[hand];
            Bought("new_weapon", old != null ? $"RocketLauncher Mk1 hand, replaces {old.form}" : "RocketLauncher Mk1 hand", CampaignRoom.Armament, RocketCost);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            AnnounceWeapon(rocket, old != null ? ArenaText.Pair($"Rocket launcher unlocked (an anomaly weapon, Mk 1): it replaces the {old.FormName}",$"Rocket launcher unlocked (an anomaly weapon, Mk 1): it replaces the {old.FormName}")
                : ArenaText.Pair("Rocket launcher unlocked (an anomaly weapon, Mk 1): it goes into the free hand","Rocket launcher unlocked (an anomaly weapon, Mk 1): it goes into the free hand"));
            return true;
        }

                                                                                                                                   
        void Bought(string item, string detail, CampaignRoom room, CampaignCost cost) => CampaignEvents.RaisePurchased(item, detail, room, cost, Day);
        static string UpgradeItem(CampaignUpgrade kind) => kind switch
        {
            CampaignUpgrade.Stat => "constitution",
            CampaignUpgrade.Skill => "unlock_skill",
            CampaignUpgrade.Infusion => "weapon_infusion",
            CampaignUpgrade.Tame => "weapon_tame",
            _ => "weapon_twist"
        };

                                                                                                                                                     
        void Refund(CampaignCost cost) { alloy += cost.alloy; energy += cost.energy; dark += cost.dark; }

                                                                                                                                
                                                                 
        public int ResearchQueued => 0;
        public bool TryBuyWeaponResearch() => TryBuyWeaponDamage();
        public bool TryBuyCardPick(CampaignRoom room) => TryBuyUpgrade(room == CampaignRoom.Energy ? CampaignUpgrade.Stat : CampaignUpgrade.Tame);

        public CampaignCost? DurabilityCost => Durability - 1 < rules.durabilityLevels.Length ? rules.durabilityLevels[Durability - 1] : null;
        public bool TryUpgradeDurability()
        {
            var cost = DurabilityCost;
            if (cost == null || !ControlWorks || !TrySpend(cost.Value)) return false;
            float share = BaseHp / BaseMaxHp;
            Durability++;
            Bought("room_upgrade", $"durability {Durability}", CampaignRoom.Control, cost.Value);
            BaseHp = Mathf.Max(BaseHp, share * BaseMaxHp);
            BaseShield = Mathf.Min(BaseShield, BaseMaxShield);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, BaseCentre);
            return true;
        }

        public CampaignCost? ShieldCost => ShieldLevel < rules.shieldLevels.Length ? rules.shieldLevels[ShieldLevel] : null;
        public bool TryUpgradeShield()
        {
            var cost = ShieldCost;
            if (cost == null || !ControlWorks || !TrySpend(cost.Value)) return false;
            ShieldLevel++;
            Bought("shield", $"level {ShieldLevel}", CampaignRoom.Control, cost.Value);
            BaseShield = BaseMaxShield;                                    
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShield, BaseCentre);
            return true;
        }

        public int HullRepairCost => rules.HullRepairCost(1f - BaseHp / BaseMaxHp, Durability);
        public bool TryRepairHull()
        {
            if (BaseHp >= BaseMaxHp - .5f || !ControlWorks) return false;
            var hull = new CampaignCost(HullRepairCost);
            if (!TrySpend(hull)) return false;
            BaseHp = BaseMaxHp;
            Bought("repair", "hull", CampaignRoom.Control, hull);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair, BaseCentre);
            return true;
        }

        bool ShipWorks(int level) => rooms[(int)CampaignRoom.Ship].level >= level && !rooms[(int)CampaignRoom.Ship].damaged;
        public bool CanRepairEngine => !engineFixed && ShipWorks(1);
        public bool CanRepairArmor => !armorFixed && ShipWorks(2);
        public bool CanSynthesiseFuel => !FuelFull && ShipWorks(3);

        public bool TryRepairEngine()
        {
            if (!CanRepairEngine || !TrySpend(rules.engineRepair)) return false;
            engineFixed = true; RefreshShip(); Say(Line.S08);
            Bought("ship_part", "engine", CampaignRoom.Ship, rules.engineRepair);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair, ShipCentre);
            return true;
        }

        public bool TryRepairArmor()
        {
            if (!CanRepairArmor || !TrySpend(rules.armorRepair)) return false;
            armorFixed = true; RefreshShip(); Say(Line.S09);
            Bought("ship_part", "armor", CampaignRoom.Ship, rules.armorRepair);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair, ShipCentre);
            return true;
        }

        public bool TrySynthesiseFuel()
        {
            if (!CanSynthesiseFuel || !TrySpend(new CampaignCost(0, 0, rules.darkPerFuelUnit))) return false;
            fuel++; RefreshShip();
            Bought("fuel", $"{fuel}/{rules.fuelUnits}", CampaignRoom.Ship, new CampaignCost(0, 0, rules.darkPerFuelUnit));
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PickupEnergy, ShipCentre, .7f);
            return true;
        }

        public bool CanIgnite => FuelFull && engineFixed && armorFixed;

        public bool TryCraftLamp()
        {
            if (!LampsCraftable || !TrySpend(new CampaignCost(rules.lampCost))) return false;
            lampStock++;
            Bought("lamp", $"stock {lampStock}", CampaignRoom.Ship, new CampaignCost(rules.lampCost));
            return true;
        }

        public bool CanTopUpShield => ShieldLevel > 0 && BaseShield < BaseMaxShield - .5f && Time.time >= shieldTopUpAt && ControlWorks;
        public bool TryTopUpShield()
        {
            if (!CanTopUpShield || !TrySpend(new CampaignCost(0, rules.ShieldTopUpCost(Day)))) return false;
            BaseShield = Mathf.Min(BaseMaxShield, BaseShield + BaseMaxShield * rules.shieldTopUpShare);
            shieldTopUpAt = Time.time + rules.shieldTopUpCooldown;
            Bought("shield", "top_up", CampaignRoom.Control, new CampaignCost(0, rules.ShieldTopUpCost(Day)));
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShield, BaseCentre);
            Fx.Ring(BaseCentre, BaseHalf + 2f, new Color(.5f, .9f, 1f), .5f);
            return true;
        }

        public static string RoomName(CampaignRoom room) => room switch
        {
            CampaignRoom.Control => ArenaText.Pair("Central Control","Central Control"),
            CampaignRoom.Armament => ArenaText.Pair("Armament Bay","Armament Bay"),
            CampaignRoom.Energy => ArenaText.Pair("Energy Lab","Energy Lab"),
            _ => ArenaText.Pair("Ship Launch Bay","Ship Launch Bay")
        };

                                                       

        void TickDayInteraction(float dt)
        {
            var p = gm.player;
            if (!p || !p.IsAlive) { prompt = ""; return; }
            bool atDoor = Query.Flat(p.Position - DoorPoint).magnitude < 3.2f;
            if (atDoor && ShieldLevel > 0)
            {
                prompt = CanTopUpShield ? ArenaText.Pair($"E: emergency shield top-up ({rules.ShieldTopUpCost(Day)} energy)",$"E: emergency shield top-up ({rules.ShieldTopUpCost(Day)} energy)")
                    : ArenaText.Pair("Shield top-up cooling down","Shield top-up cooling down");
                if (EPressed && !TryTopUpShield()) ui.Flash(ArenaText.Pair("Not enough energy, or the shield is full","Not enough energy, or the shield is full"));
            }
            else prompt = "";
        }
    }
}
