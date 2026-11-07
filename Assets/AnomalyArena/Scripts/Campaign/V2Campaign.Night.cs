using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                 
                                                                                                                                             
                                                                                                                                            
                                                                                                                                           
                                                                                                                                          
                                                                                           
                                                                                                                                        
                                                                                                                                        
                                                  
                  
    public sealed partial class V2Campaign
    {
        public enum NodeKind { Ore, Dark, Core }

        sealed class Node
        {
            public NodeKind kind; public Vector3 position; public int amount; public bool mined; public Transform view; public float glint;
        }

        sealed class Lamp
        {
            public Vector3 position; public float hp; public int region; public Transform view; public Light light;
        }

        sealed class Shadow
        {
            public Vector3 position, home, wander; public int region; public Transform view; public float hitAt, wanderAt, bob; public bool overrun;
        }

        readonly List<Node> nodes = new();
        readonly List<Lamp> lamps = new();
        readonly List<Shadow> shadows = new();
        readonly Dictionary<int, int> regionKills = new();
        readonly Dictionary<int, int> regionShadows = new();
        int lampStock, nightAlloy, nightDark;
        bool knockedOut, nightWhispers;
        Node mining; float mineProgress;
        string prompt = "";
        V2Darkness darkness;
        float[] regionDarkness;
        float darknessLevel, enhancementPulse;

        void ResetNight()
        {
            ClearNight(true);
            regionKills.Clear(); regionShadows.Clear();
            lampStock = 0; nightAlloy = nightDark = 0; knockedOut = false;
            baseVisitFrom = CampaignStage.Off;
        }

        void BuildDarkness()
        {
            if (!darkness) darkness = V2Darkness.Create(gm);
            darkness.SetActive(false);
        }

                              

        public int RegionOf(Vector3 p)
        {
            var b = WorldMap.Active ? WorldMap.Active.Bounds : new Bounds(Vector3.zero, new Vector3(180f, 20f, 120f));
            int cols = Mathf.Max(1, rules.regions.x), rows = Mathf.Max(1, rules.regions.y);
            int cx = Mathf.Clamp(Mathf.FloorToInt((p.x - b.min.x) / b.size.x * cols), 0, cols - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((p.z - b.min.z) / b.size.z * rows), 0, rows - 1);
            return cz * cols + cx;
        }

        Vector3 RegionCentre(int region, out Vector2 half)
        {
            var b = WorldMap.Active ? WorldMap.Active.Bounds : new Bounds(Vector3.zero, new Vector3(180f, 20f, 120f));
            int cols = Mathf.Max(1, rules.regions.x), rows = Mathf.Max(1, rules.regions.y);
            float w = b.size.x / cols, h = b.size.z / rows;
            half = new Vector2(w * .5f, h * .5f);
            return new Vector3(b.min.x + (region % cols + .5f) * w, 0f, b.min.z + (region / cols + .5f) * h);
        }

        int RegionCount => Mathf.Max(1, rules.regions.x) * Mathf.Max(1, rules.regions.y);
        public bool Overrun(int region) => regionShadows.TryGetValue(region, out int n) && n >= rules.overrunShadows;
        public int ShadowsIn(int region) => regionShadows.TryGetValue(region, out int n) ? n : 0;

        void OnEnemyEliminated(Enemy enemy)
        {
            if (!Running || !enemy) return;
            raidSpots.Remove(enemy);
            if (Stage is not (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Tutorial)) return;
            if (enemy.SuppressRewards || enemy.IsTrialSummon || enemy.DeathCause == DeathCause.FellIntoGap || enemy.IsBoss) return;
            int region = RegionOf(enemy.Position);
            regionKills[region] = (regionKills.TryGetValue(region, out int n) ? n : 0) + 1;
            DropCurrencies(enemy);
        }

                           

        void BeginDusk()
        {
            Stage = CampaignStage.Dusk; stageClock = 0f; stageLength = rules.duskSeconds;
            CampaignEvents.RaisePhase("dusk", Day);
            gm.CampaignSetPhase(ArenaUiState.Night);
            NightlyBaseUpkeep();
            lampStock += rules.freeLampsPerNight;
            nightAlloy = nightDark = 0; knockedOut = false;
            BankHaul();
            SpawnOreNodes(rules.OreNodes(Day));
            darkness.SetActive(true);
            enhancementPulse = 1.4f;
                                                                                                                                        
                                                                                            
            ShowEnhancement();
            Say(Line.D01);
            Objective(ArenaText.Pair("Dusk: mine alloy ore (hold E) while there is light; E at the base door to upgrade","Dusk: mine alloy ore (hold E) while there is light; E at the base door to upgrade"), null);
        }

        void ShowEnhancement()
        {
            var set = gm.waves ? gm.waves.waveSet : null;
            if (!set || !set.encounterBalance || Day >= rules.days && !Overtime) { gm.hud?.Banner(ArenaText.Pair("DUSK · REST AND MINING","DUSK · REST AND MINING"), ArenaText.Pair("Ore has surfaced. Mine some alloy before dark.","Ore has surfaced. Mine some alloy before dark.")); return; }
            int today = Mathf.Clamp(rules.WaveNumber(Day, rules.wavesPerDay - 1), 1, set.waves.Length), tomorrow = Mathf.Clamp(rules.WaveNumber(Day + 1, rules.wavesPerDay - 1), 1, set.waves.Length);
            float hp = set.encounterBalance.DpsAt(tomorrow) / set.encounterBalance.DpsAt(today) - 1f;
            float damage = set.DamageFactor(tomorrow) / set.DamageFactor(today) - 1f;
            gm.hud?.Banner(ArenaText.Pair("DUSK · ALIEN POWER STRENGTHENS THE MONSTERS","DUSK · ALIEN POWER STRENGTHENS THE MONSTERS"),
                ArenaText.Pair(                    $"Tomorrow's monsters: HP +{hp * 100f:0}%, damage +{damage * 100f:0}%. Ore has surfaced; mine alloy before dark.",                    $"Tomorrow's monsters: HP +{hp * 100f:0}%, damage +{damage * 100f:0}%. Ore has surfaced; mine alloy before dark."));
        }

        void TickDusk(float dt)
        {
            prompt = "";
            if (CaughtNightDeath()) return;
            stageClock += dt;
            float t = Mathf.Clamp01(stageClock / Mathf.Max(.1f, stageLength));
            darknessLevel = rules.duskDarkness * t;
            LerpLighting(t);
            TickPickups(dt);
            TickMining(dt, true);
            TickLampPlacement();
            if (TickBaseDoor()) return;                              
            if (stageClock >= stageLength) BeginNight();
        }

        void LerpLighting(float t)
        {
            var biome = WorldMap.Active ? WorldMap.Active.biome : null;
            if (!biome) return;
            RenderSettings.ambientLight = Color.Lerp(biome.dayAmbient, biome.ambient * .55f, t);
            var sun = RenderSettings.sun;
            if (!sun) foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (light.type == LightType.Directional && light.enabled) { sun = light; break; }
            if (!sun) return;
            sun.color = Color.Lerp(biome.daySun, Color.Lerp(biome.nightSun, new Color(1f, .55f, .35f), (1f - t) * .6f), t);
            sun.intensity = Mathf.Lerp(biome.daySunIntensity, biome.nightSunIntensity * .45f, t);
        }

        void ApplyDaylight()
        {
            var biome = WorldMap.Active ? WorldMap.Active.biome : null;
            if (biome) biome.ApplyLighting(false);
            darknessLevel = 0f;
            darkness?.SetActive(false);
            if (baseLight) baseLight.intensity = 0f;
        }

                            

        void BeginNight()
        {
            Stage = CampaignStage.Night; stageClock = 0f; stageLength = rules.NightLength(Day);
            CampaignEvents.RaisePhase("night", Day);
            LerpLighting(1f);
            darknessLevel = rules.nightDarkness;
            if (baseLight) baseLight.intensity = 2.2f;
            SpawnShadows();
            SpawnDarkClusters();
            Say(Line.S02);
            nightWhispers = true;
            Objective(ArenaText.Pair("Night: F sets a lamp, hold E in its light for dark matter; E at the base door to repair, upgrade or wait for dawn","Night: F sets a lamp, hold E in its light for dark matter; E at the base door to repair, upgrade or wait for dawn"), null);
        }

        void TickNight(float dt)
        {
            prompt = "";
            if (CaughtNightDeath()) return;
            stageClock += dt;
            TickPickups(dt);
            TickShadows(dt);
            TickLamps(dt);
            TickMining(dt, false);
            TickLampPlacement();
            if (TickBaseDoor()) return;                               
            TickWhispers(dt);
            if (Day == 0) TickTutorialNight(dt);
            if (stageClock >= stageLength) EndNight(false);
        }

                                                                                                                                          
                                                                                                                                          
                                                                                                                                       
                                                         
        public void EndNight(bool waitedAtBase)
        {
            if (Stage != CampaignStage.Night && Stage != CampaignStage.Base) return;
            if (Stage == CampaignStage.Base) ui.HideBase();
            baseVisitFrom = CampaignStage.Off;
            CampaignEvents.RaisePhase("dawn", Day);
            CampaignEvents.RaiseDawn(Day, new CampaignCost(alloy, energy, dark), new CampaignCost(EarnedAlloy, EarnedEnergy, EarnedDark));
            ClearNight(false);
            ApplyDaylight();
            nightWhispers = false;
            Objective("", null);
            PlacePlayer(DoorPoint);
            if (Day >= rules.days && CanIgnite) { Ignite(); return; }
            if (Day >= rules.days)
            {
                                                                                                                                       
                string missing = !engineFixed ? ArenaText.Pair("The engine is not repaired","The engine is not repaired")
                    : !armorFixed ? ArenaText.Pair("The armor is not repaired","The armor is not repaired")
                    : ArenaText.Pair("The tank is not full","The tank is not full");
                ui.Flash(ArenaText.Pair($"{missing}: the ship cannot leave. Hold out another day.",$"{missing}: the ship cannot leave. Hold out another day."), 3.5f);
            }
            Stage = CampaignStage.Build;                                                  
            gm.CampaignStartDay();
        }

        void ClearNight(bool everything)
        {
            foreach (var s in shadows) if (s.view) Destroy(s.view.gameObject);
            shadows.Clear();
            foreach (var l in lamps) if (l.view) Destroy(l.view.gameObject);
            lamps.Clear();
            for (int i = nodes.Count - 1; i >= 0; i--)
                if (everything || nodes[i].kind != NodeKind.Core) { if (nodes[i].view) Destroy(nodes[i].view.gameObject); nodes.RemoveAt(i); }
            regionShadows.Clear();
            mining = null; mineProgress = 0f;
            if (everything) { ClearPickups(); lampStock = 0; }
        }

                                                                                                                                        
                                                                                            
        bool TickBaseDoor()
        {
            var p = gm.player;
            if (!p || mining != null || Query.Flat(p.Position - DoorPoint).magnitude >= 3.2f) return false;
            prompt = Stage == CampaignStage.Dusk ? ArenaText.Pair("E: enter the base to upgrade or repair (dusk waits)","E: enter the base to upgrade or repair (dusk waits)")
                : ArenaText.Pair("E: enter the base to upgrade or repair (the night waits)","E: enter the base to upgrade or repair (the night waits)");
            if (!EPressed) return false;
            OpenBase();
            return true;
        }

        string NightHint()
        {
            string lampText = ArenaText.Pair($"LAMPS ×{lampStock} (F TO PLACE)",$"LAMPS ×{lampStock} (F TO PLACE)");
            string haul = ArenaText.Pair($"TONIGHT: ALLOY +{nightAlloy} · DARK MATTER +{nightDark}",$"TONIGHT: ALLOY +{nightAlloy} · DARK MATTER +{nightDark}");
                                                                                                                                        
            var risk = TonightsHaul;
            string atRisk = ArenaText.Pair($"A DEATH LOSES: ALLOY {risk.alloy} · ENERGY {risk.energy} · DARK MATTER {risk.dark} (THE BASE KEEPS IT)",$"A DEATH LOSES: ALLOY {risk.alloy} · ENERGY {risk.energy} · DARK MATTER {risk.dark} (THE BASE KEEPS IT)");
            return Stage == CampaignStage.Dusk ? $"{ArenaText.Pair("AT DUSK ORE CAN BE MINED WITHOUT A LAMP","AT DUSK ORE CAN BE MINED WITHOUT A LAMP")} · {lampText} · {haul}"
                : $"{ArenaText.Pair("NO FIGHTING AT NIGHT · HOLD E IN LAMP LIGHT TO MINE","NO FIGHTING AT NIGHT · HOLD E IN LAMP LIGHT TO MINE")} · {lampText} · {haul} · {atRisk}";
        }

                            

        void TickLampPlacement()
        {
            if (!FPressed) return;
            var p = gm.player;
            if (!p || !p.IsAlive) return;
            if (lampStock <= 0) { ui.Flash(ArenaText.Pair("No lamps left: craft them with alloy at the Ship Launch Bay","No lamps left: craft them with alloy at the Ship Launch Bay")); return; }
            Vector3 at = Query.Flat(p.Position + p.AimDirection.normalized * .8f);
            if (WorldMap.Active && (WorldMap.Active.IsWall(at, .4f) || !WorldMap.Active.Contains(at, .5f))) at = Query.Flat(p.Position);
            PlaceLamp(at);
            lampStock--;
        }

        void PlaceLamp(Vector3 at)
        {
            var lamp = new Lamp { position = at, hp = rules.lampHp, region = RegionOf(at) };
            var root = new GameObject("Lamp").transform;
            root.SetParent(worldRoot, false);
            root.position = at;
            if (art && art.lampPrefab && Art.Enabled)
            {
                var model = Instantiate(art.lampPrefab, root);
                StripColliders(model);
                FitHeight(model.transform, 1.6f);
            }
            else
            {
                Box(root, "Post", new Vector3(0f, .6f, 0f), new Vector3(.18f, 1.2f, .18f), new Color(.3f, .3f, .32f));
                var head = Box(root, "Head", new Vector3(0f, 1.35f, 0f), new Vector3(.5f, .4f, .5f), new Color(1f, .9f, .55f));
                head.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lightGo = new GameObject("LampLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            lamp.light = lightGo.AddComponent<Light>();
            lamp.light.type = LightType.Point; lamp.light.range = rules.lampRadius * 1.5f; lamp.light.intensity = 3f; lamp.light.color = new Color(1f, .9f, .7f);
            lamp.view = root;
            lamps.Add(lamp);
            Fx.Ring(at, rules.lampRadius, new Color(1f, .9f, .6f), .45f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair, at, 1.2f, .6f);
        }

        void TickLamps(float dt)
        {
            for (int i = lamps.Count - 1; i >= 0; i--)
            {
                var lamp = lamps[i];
                if (lamp.light) lamp.light.intensity = Mathf.Lerp(.8f, 3f, lamp.hp / rules.lampHp) * (1f + .08f * Mathf.Sin(Time.time * 13f + i));
                if (lamp.hp > 0f) continue;
                Fx.Burst(lamp.position + Vector3.up, Vector3.zero, new Color(1f, .8f, .4f), 10, 6f, 360f, .3f);
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcHit, lamp.position, .6f);
                if (lamp.view) Destroy(lamp.view.gameObject);
                lamps.RemoveAt(i);
                Say(Line.S03);
            }
        }

                                                                                                                                        
                                                       
        float LampLight(Vector3 p, bool includeBase = true)
        {
            float lit = 0f;
            foreach (var lamp in lamps)
            {
                float d = Query.Flat(p - lamp.position).magnitude / rules.lampRadius;
                if (d < 1f) lit = Mathf.Max(lit, 1f - d * d);
            }
            if (includeBase)
            {
                float b = Query.Flat(p - BaseCentre).magnitude / rules.baseLightRadius;
                if (b < 1f) lit = Mathf.Max(lit, 1f - b * b);
            }
            return lit;
        }

        bool InLampLight(Vector3 p) => LampLight(p) > .05f;
        bool InBaseLight(Vector3 p) => Query.Flat(p - BaseCentre).magnitude < rules.baseLightRadius;

                                       

        void SpawnOreNodes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryNodePoint(out var at, i)) continue;
                AddNode(NodeKind.Ore, at, rules.OreYield(Mathf.Max(1, Day)));
            }
        }

        void SpawnDarkClusters()
        {
            if (Day == 0) return;                                     
            var regionsLeft = new List<int>();
            for (int r = 0; r < RegionCount; r++)
            {
                RegionCentre(r, out _);
                regionsLeft.Add(r);
            }
            for (int c = 0; c < rules.darkClusters && regionsLeft.Count > 0; c++)
            {
                int pick = Random.Range(0, regionsLeft.Count);
                int region = regionsLeft[pick]; regionsLeft.RemoveAt(pick);
                var centre = RegionCentre(region, out var half);
                Vector3 at = centre + new Vector3(Random.Range(-half.x * .7f, half.x * .7f), 0f, Random.Range(-half.y * .7f, half.y * .7f));
                if (Query.Flat(at - BaseCentre).magnitude < rules.nodeBaseClearance) { c--; continue; }
                AddCluster(FindOpen(at, 1.2f));
            }
        }

        void AddCluster(Vector3 centre)
        {
            for (int n = 0; n < rules.nodesPerCluster; n++)
            {
                float a = n * Mathf.PI * 2f / rules.nodesPerCluster + Random.Range(-.3f, .3f);
                Vector3 at = FindOpen(centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.1f, .8f, 4f);
                AddNode(NodeKind.Dark, at, rules.darkPerNode);
            }
        }

        bool TryNodePoint(out Vector3 at, int salt)
        {
            var b = WorldMap.Active ? WorldMap.Active.Bounds : new Bounds(Vector3.zero, new Vector3(180f, 20f, 120f));
            for (int attempt = 0; attempt < 30; attempt++)
            {
                at = new Vector3(Random.Range(b.min.x + 6f, b.max.x - 6f), 0f, Random.Range(b.min.z + 6f, b.max.z - 6f));
                if (Query.Flat(at - BaseCentre).magnitude < rules.nodeBaseClearance) continue;
                if (WorldMap.Active && (!WorldMap.Active.IsWalkable(at, 1.2f) || Quicksand.SpeedFactor(at) < .99f)) continue;
                return true;
            }
            at = Vector3.zero;
            return false;
        }

        Node AddNode(NodeKind kind, Vector3 at, int amount)
        {
            var node = new Node { kind = kind, position = at, amount = amount };
            var root = new GameObject(kind + "Node").transform;
            root.SetParent(worldRoot, false);
            root.position = at;
            root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var prefab = !art || !Art.Enabled ? null : kind switch { NodeKind.Ore => art.oreNodePrefab, NodeKind.Dark => art.darkNodePrefab, _ => null };
            if (prefab)
            {
                var model = Instantiate(prefab, root);
                StripColliders(model);
                FitFootprint(model.transform, kind == NodeKind.Dark ? 1.5f : 1.8f);
            }
            else if (kind == NodeKind.Core)
            {
                var core = Box(root, "Core", new Vector3(0f, .9f, 0f), new Vector3(.8f, .8f, .8f), new Color(.75f, .25f, 1f));
                core.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            }
            else
            {
                Box(root, "Rock", new Vector3(0f, .35f, 0f), new Vector3(1.4f, .7f, 1.1f), kind == NodeKind.Ore ? new Color(.45f, .4f, .36f) : new Color(.16f, .1f, .22f));
                Box(root, "Crystal", new Vector3(.15f, .9f, 0f), new Vector3(.35f, .9f, .35f), kind == NodeKind.Ore ? new Color(.6f, .75f, .9f) : new Color(.55f, .15f, .85f));
            }
            node.view = root;
            node.glint = Random.value * 6f;
            nodes.Add(node);
            return node;
        }

        static void FitHeight(Transform model, float height)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y > .01f) model.localScale *= height / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var parent = model.parent ? model.parent.position : Vector3.zero;
            model.position += new Vector3(parent.x - bounds.center.x, parent.y - bounds.min.y, parent.z - bounds.center.z);
        }

                                           
        public int ShadowCount => shadows.Count;
        public int LampCount => lamps.Count;
        public int OverrunRegions { get { int n = 0; foreach (var pair in regionShadows) if (pair.Value >= rules.overrunShadows) n++; return n; } }
        public int NodesLeft(NodeKind kind) { int n = 0; foreach (var node in nodes) if (!node.mined && node.kind == kind) n++; return n; }
        public bool TryNearestNode(NodeKind kind, Vector3 from, out Vector3 at)
        {
            at = Vector3.zero; float best = float.MaxValue; bool found = false;
            foreach (var node in nodes)
            {
                if (node.mined || node.kind != kind) continue;
                float d = Query.Flat(node.position - from).sqrMagnitude;
                if (d < best) { best = d; at = node.position; found = true; }
            }
            return found;
        }

        Node NearestNode(Vector3 p, float range)
        {
            Node best = null; float bestDistance = range;
            foreach (var node in nodes)
            {
                if (node.mined) continue;
                float d = Query.Flat(node.position - p).magnitude;
                if (d < bestDistance) { best = node; bestDistance = d; }
            }
            return best;
        }

                                                                                                                                                   
        void TickMining(float dt, bool dusk)
        {
            var p = gm.player;
            if (!p || !p.IsAlive) { mining = null; return; }
            var node = NearestNode(p.Position, rules.mineRange);
            if (node == null || node.kind == NodeKind.Core) { if (mining != null) { mining = null; mineProgress = 0f; } if (Stage != CampaignStage.Night) prompt = ""; return; }
            bool lit = dusk || InLampLight(node.position);
            string what = node.kind == NodeKind.Ore ? ArenaText.Pair($"alien alloy +{node.amount}",$"alien alloy +{node.amount}") : ArenaText.Pair($"dark matter +{node.amount}",$"dark matter +{node.amount}");
            if (!lit)
            {
                prompt = ArenaText.Pair($"Too dark: place a lamp (F) before mining {what}",$"Too dark: place a lamp (F) before mining {what}");
                mining = null; mineProgress = 0f;
                return;
            }
            if (!EHeld) { prompt = ArenaText.Pair($"Hold E to mine {what}",$"Hold E to mine {what}"); mining = null; mineProgress = 0f; return; }
            if (mining != node) { mining = node; mineProgress = 0f; }
            mineProgress += dt / Mathf.Max(.05f, rules.mineSeconds);
            prompt = ArenaText.Pair($"Mining {Mathf.RoundToInt(mineProgress * 100f)}%",$"Mining {Mathf.RoundToInt(mineProgress * 100f)}%");
            if (Random.value < dt * 12f) Fx.Pop(node.position + Vector3.up * Random.Range(.4f, 1.2f), node.kind == NodeKind.Ore ? new Color(.7f, .85f, 1f) : new Color(.7f, .3f, 1f), .5f);
            if (mineProgress < 1f) return;
            CompleteMining(node);
        }

        void CompleteMining(Node node)
        {
            node.mined = true; mining = null; mineProgress = 0f;
            if (node.kind == NodeKind.Ore) { Gain(new CampaignCost(node.amount)); if (Stage is CampaignStage.Night or CampaignStage.Dusk) nightAlloy += node.amount; }
            else { Gain(new CampaignCost(0, 0, node.amount)); nightDark += node.amount; }
            Fx.Burst(node.position + Vector3.up, Vector3.up, node.kind == NodeKind.Ore ? new Color(.7f, .85f, 1f) : new Color(.7f, .3f, 1f), 12, 7f, 360f, .35f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PickupEnergy, node.position, node.kind == NodeKind.Ore ? 1.1f : .8f);
            if (node.view) Destroy(node.view.gameObject);
            OnTutorialMined(node.kind);
        }

                              

        void SpawnShadows()
        {
            regionShadows.Clear();
            int total = 0;
            if (Day == 0)
            {
                                                                                                     
                for (int i = 0; i < rules.tutorialShadows; i++)
                {
                    float a = (i + .5f) * Mathf.PI * 2f / Mathf.Max(1, rules.tutorialShadows);
                    AddShadow(FindOpen(BaseCentre + new Vector3(Mathf.Cos(a) * 34f, 0f, Mathf.Sin(a) * 24f), .6f), false);
                }
                return;
            }
            var order = new List<int>(regionKills.Keys);
            order.Sort((a, b) => regionKills[b].CompareTo(regionKills[a]));
            foreach (int region in order)
            {
                int count = Mathf.Min(rules.maxShadowsPerRegion, Mathf.FloorToInt(regionKills[region] / Mathf.Max(1f, rules.killsPerShadow)));
                count = Mathf.Min(count, rules.maxShadows - total);
                if (count <= 0) continue;
                regionShadows[region] = count;
                total += count;
                var centre = RegionCentre(region, out var half);
                bool overrun = count >= rules.overrunShadows;
                for (int i = 0; i < count; i++)
                {
                    Vector3 at = centre + new Vector3(Random.Range(-half.x * .8f, half.x * .8f), 0f, Random.Range(-half.y * .8f, half.y * .8f));
                    if (InBaseLight(at)) at = BaseCentre + Query.Flat(at - BaseCentre).normalized * (rules.baseLightRadius + 3f);
                    var s = AddShadow(at, overrun);
                    s.region = region;
                }
            }
        }

        Shadow AddShadow(Vector3 at, bool overrun)
        {
            var s = new Shadow { position = at, home = at, wander = at, overrun = overrun, region = RegionOf(at), bob = Random.value * 6f };
            var root = new GameObject("Shadow").transform;
            root.SetParent(worldRoot, false);
            root.position = at;
            var looks = art && Art.Enabled ? art.shadowPrefabs : null;
            if (looks != null && looks.Length > 0 && looks[Random.Range(0, looks.Length)] is { } look && look)
            {
                var model = Instantiate(look, root);
                StripColliders(model);
                FitHeight(model.transform, Random.Range(1.8f, 2.2f));
            }
            else
            {
                                                                                                                                      
                                                                                                          
                int lookIndex = Random.Range(0, 3);
                Vector3 size = lookIndex switch { 0 => new Vector3(.75f, 1.9f, .75f), 1 => new Vector3(1.15f, 1.3f, 1.15f), _ => new Vector3(.6f, 1.2f, .6f) };
                Color tint = lookIndex switch { 0 => new Color(.04f, .03f, .07f), 1 => new Color(.09f, .03f, .12f), _ => new Color(.02f, .05f, .09f) };
                var r = Box(root, "Body", new Vector3(0f, size.y * .5f, 0f), size, tint);
                Box(root, "EyeL", new Vector3(-.15f, size.y - .3f, size.z * .5f + .01f), new Vector3(.12f, .06f, .05f), new Color(.75f, .3f, 1f));
                Box(root, "EyeR", new Vector3(.15f, size.y - .3f, size.z * .5f + .01f), new Vector3(.12f, .06f, .05f), new Color(.75f, .3f, 1f));
            }
            s.view = root;
            shadows.Add(s);
            return s;
        }

        void TickShadows(float dt)
        {
            var p = gm.player;
            if (!p) return;
            Vector3 pp = Query.Flat(p.Position);
            bool playerLit = InLampLight(pp);
            foreach (var s in shadows)
            {
                Vector3 target = s.wander;
                float speed = s.overrun ? rules.overrunShadowSpeed : rules.shadowSpeed;
                float toPlayer = Query.Flat(pp - s.position).magnitude;
                Lamp prey = null;
                if (s.overrun)
                {
                    float best = rules.shadowSense * 2f;
                    foreach (var lamp in lamps)
                    {
                        if (lamp.region != s.region && !Overrun(lamp.region)) continue;
                        float d = Query.Flat(lamp.position - s.position).magnitude;
                        if (d < best) { best = d; prey = lamp; }
                    }
                }
                bool hunting = toPlayer < rules.shadowSense && (s.overrun || !playerLit) && !InBaseLight(pp);
                if (hunting) target = pp;
                else if (prey != null) target = prey.position;
                else if (Time.time >= s.wanderAt || Query.Flat(s.wander - s.position).magnitude < .5f)
                {
                    s.wanderAt = Time.time + Random.Range(2f, 4f);
                    s.wander = s.home + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
                    target = s.wander;
                }
                Vector3 step = Query.Flat(target - s.position);
                if (step.magnitude > .05f) step = step.normalized * Mathf.Min(step.magnitude, speed * dt);
                Vector3 next = s.position + step;
                                                                                                                                    
                if (InBaseLight(next) || !s.overrun && InLampLight(next)) next = s.position - step * .5f;
                if (WorldMap.Active) next = WorldMap.Active.ClampPoint(next, .5f);
                s.position = next;
                if (s.view)
                {
                    s.bob += dt * 3f;
                    s.view.position = s.position + Vector3.up * (.15f + .1f * Mathf.Sin(s.bob));
                    if (step.sqrMagnitude > 1e-5f) s.view.rotation = Quaternion.Slerp(s.view.rotation, Quaternion.LookRotation(step.normalized), dt * 8f);
                }
                if (prey != null && Query.Flat(prey.position - s.position).magnitude < 1.2f) prey.hp -= dt;
                if (toPlayer < .9f + p.Radius && Time.time >= s.hitAt && (s.overrun || !playerLit) && !InBaseLight(pp))
                {
                    s.hitAt = Time.time + rules.shadowHitInterval;
                    HitPlayerAtNight(s);
                }
            }
        }

                                                                                                                                              
                                                                                                                     
        void HitPlayerAtNight(Shadow s)
        {
            var p = gm.player;
            if (!p.IsAlive || knockedOut) return;
            float hit = Mathf.Max(1f, p.maxHp * rules.shadowHitShare);
            gm.hud?.FlashDamage();
            gm.Shake(.18f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PlayerHit, p.Position, .7f);
            p.ReceiveDamage(new DamageInfo { amount = hit, source = DamageSource.Hit, knockDir = Query.Flat(p.Position - s.position).normalized });
        }

                                                                                                                                       
                                                                     
        public bool CatchesPlayerDeath() => Running && (knockedOut || Stage is CampaignStage.Dusk or CampaignStage.Night);

        bool CaughtNightDeath()
        {
            if (knockedOut) return true;
            if (!gm.player || gm.player.IsAlive) return false;
            StartCoroutine(NightDeath());
            return true;
        }

                                                                                                                                         
                                                                                                                                     
        IEnumerator NightDeath()
        {
            knockedOut = true;
            mining = null; mineProgress = 0f;
            var lost = LoseHaul(rules.nightDeathLoss);
            CampaignEvents.RaiseNightDeath(lost, Day);
            ui.Flash(ArenaText.Pair(                $"You fell in the dark. Everything mined or picked up tonight is lost: {lost.alloy} alloy, {lost.energy} energy, {lost.dark} dark matter",                $"You fell in the dark. Everything mined or picked up tonight is lost: {lost.alloy} alloy, {lost.energy} energy, {lost.dark} dark matter"), 5f);
            Say(Line.K01);
            float until = Time.realtimeSinceStartup + rules.nightDeathSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;
            var p = gm.player;
            if (p) p.ResetForSpawn();                                                                      
            knockedOut = false;
            if (Stage is CampaignStage.Dusk) Stage = CampaignStage.Night;                                    
            EndNight(false);
        }

        float whisperClock;
        void TickWhispers(float dt)
        {
                                                                                                            
            if (!nightWhispers || shadows.Count == 0) return;
            var p = gm.player;
            float near = 0f;
            foreach (var s in shadows) near += Mathf.Clamp01(1f - Query.Flat(s.position - p.Position).magnitude / 18f);
            whisperClock -= dt * Mathf.Min(3f, near);
            if (whisperClock > 0f) return;
            whisperClock = 2.5f;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShadeSpawn, p.Position + Random.onUnitSphere * 4f, Random.Range(.55f, .75f), Mathf.Clamp01(.35f + near * .2f));
        }

                                           

        void UpdateDarkness()
        {
            if (!darkness) return;
            enhancementPulse = Mathf.Max(0f, enhancementPulse - Time.unscaledDeltaTime);
            bool show = darknessLevel > .001f && Stage is CampaignStage.Dusk or CampaignStage.Night or CampaignStage.Base;
            darkness.SetActive(show || enhancementPulse > 0f);
            if (!show && enhancementPulse <= 0f) return;
            darkness.BeginFrame(darknessLevel, Mathf.Clamp01(enhancementPulse) * .35f);
            foreach (var lamp in lamps) darkness.AddLight(lamp.position, rules.lampRadius, Mathf.Clamp01(.55f + .45f * lamp.hp / rules.lampHp));
            darkness.AddLight(BaseCentre, rules.baseLightRadius, 1f);
            if (gm.player) darkness.AddLight(gm.player.Position, rules.playerGlowRadius, .7f);
            foreach (var node in nodes)
            {
                if (node.mined) continue;
                node.glint += Time.unscaledDeltaTime;
                float pulse = .25f + .2f * Mathf.Sin(node.glint * (node.kind == NodeKind.Ore ? 2.3f : 1.6f));
                darkness.AddLight(node.position, node.kind == NodeKind.Ore ? 1f : 1.4f, node.kind == NodeKind.Core ? .9f : pulse);
            }
                                                           
            foreach (var s in shadows) darkness.AddLight(s.position, .55f, .28f);
            if (regionDarkness == null || regionDarkness.Length != RegionCount) regionDarkness = new float[RegionCount];
            var extra = regionDarkness;
            for (int r = 0; r < extra.Length; r++) extra[r] = rules.crowdedDarkness * Mathf.Clamp01(ShadowsIn(r) / (float)rules.overrunShadows);
            darkness.SetRegions(rules.regions.x, rules.regions.y, extra, WorldMap.Active ? WorldMap.Active.Bounds : new Bounds(Vector3.zero, new Vector3(180f, 20f, 120f)));
            darkness.EndFrame();
        }
    }
}
