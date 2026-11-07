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
                                                                                                              
            public bool hidden;
                                                                                                                                         
            public int trailIndex = -1;
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
            var b = CampaignBounds;
            int cols = Mathf.Max(1, rules.regions.x), rows = Mathf.Max(1, rules.regions.y);
            int cx = Mathf.Clamp(Mathf.FloorToInt((p.x - b.min.x) / b.size.x * cols), 0, cols - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((p.z - b.min.z) / b.size.z * rows), 0, rows - 1);
            return cz * cols + cx;
        }

        Vector3 RegionCentre(int region, out Vector2 half)
        {
            var b = CampaignBounds;
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

                                

        bool afternoonCombat;
                                                                                                                                        
                                                                                                                                           
                                                                                                                                   
                                                                                                                                   
                                                                                                                                       
        internal bool AfternoonCombat
        {
            get => afternoonCombat;
            set
            {
                if (afternoonCombat == value) return;
                afternoonCombat = value;
                if (Running && Stage == CampaignStage.Dusk && gm.RunPhase is ArenaUiState.Night or ArenaUiState.WaveActive) gm.CampaignSetPhase(DuskPhase);
            }
        }
        ArenaUiState DuskPhase => afternoonCombat ? ArenaUiState.WaveActive : ArenaUiState.Night;

                                                                                                                                          
                                                                                                                                             
        void DismissStragglers()
        {
            if (!gm.waves) return;
            var standing = new List<Enemy>();
            foreach (var enemy in gm.waves.Alive) if (enemy && enemy.IsAlive && !enemy.IsBoss) standing.Add(enemy);
            foreach (var enemy in standing)
            {
                enemy.SuppressRewards = true;
                raidSpots.Remove(enemy);
                enemy.DiscardWithoutReward();
            }
        }

        void BeginDusk()
        {
            Stage = CampaignStage.Dusk; stageClock = 0f; stageLength = rules.duskSeconds;
            CampaignEvents.RaisePhase("dusk", Day);
            afternoonCombat = false;
            gm.CampaignSetPhase(ArenaUiState.Night);
            NightlyBaseUpkeep();
            lampStock += rules.freeLampsPerNight;
            nightAlloy = nightDark = 0; knockedOut = false;
            BankHaul();
            SpawnAfternoonOre(Day);
            darkness.SetActive(true);
            enhancementPulse = 1.4f;
                                                                                                                                          
                                                                            
            ShowEnhancement();
            Say(Line.D01);
            AfternoonObjective();
        }

        void ShowEnhancement()
        {
            var set = gm.waves ? gm.waves.waveSet : null;
            if (!set || !set.encounterBalance || Day >= rules.days && !Overtime)
            {
                gm.hud?.Banner(ArenaText.Pair("AFTERNOON · REST AND MINING","AFTERNOON · REST AND MINING"),
                    ArenaText.Pair("Today's ore has surfaced. There is only so much; once mined, it is gone.","Today's ore has surfaced. There is only so much; once mined, it is gone."));
                return;
            }
            int today = Mathf.Clamp(rules.WaveNumber(Day, rules.wavesPerDay - 1), 1, set.waves.Length), tomorrow = Mathf.Clamp(rules.WaveNumber(Day + 1, rules.wavesPerDay - 1), 1, set.waves.Length);
            float hp = set.encounterBalance.DpsAt(tomorrow) / set.encounterBalance.DpsAt(today) - 1f;
            float damage = set.DamageFactor(tomorrow) / set.DamageFactor(today) - 1f;
            gm.hud?.Banner(ArenaText.Pair("AFTERNOON · ALIEN POWER STRENGTHENS THE MONSTERS","AFTERNOON · ALIEN POWER STRENGTHENS THE MONSTERS"),
                ArenaText.Pair(                    $"Tomorrow's monsters: HP +{hp * 100f:0}%, damage +{damage * 100f:0}%. Today's ore has surfaced; there is only so much.",                    $"Tomorrow's monsters: HP +{hp * 100f:0}%, damage +{damage * 100f:0}%. Today's ore has surfaced; there is only so much."));
        }

                                                                                                                                              
                                                                                                                                                        
        void AfternoonObjective()
        {
            if (Stage != CampaignStage.Dusk) return;
            int left = OreLeftToday;
            string onward = EveningsOpenEnded ? ArenaText.Pair("E at the base door to upgrade or go into the night","E at the base door to upgrade or go into the night")
                : ArenaText.Pair("E at the base door to upgrade","E at the base door to upgrade");
            Vector3? next = TrailFrontier();
            if (next == null && left > 0 && gm.player && TryNearestNode(NodeKind.Ore, gm.player.Position, out var nearest)) next = nearest;
            Objective(left > 0 ? ArenaText.Pair($"Afternoon: hold E to mine alloy ore ({left} left today); ",$"Afternoon: hold E to mine alloy ore ({left} left today); ") + onward
                : ArenaText.Pair("Afternoon: today's ore is mined out; ","Afternoon: today's ore is mined out; ") + onward, next);
        }

        void TickDusk(float dt)
        {
            prompt = "";
            if (CaughtNightDeath()) return;
                                                                                                                          
            if (!afternoonCombat && gm.RunPhase == ArenaUiState.Night && gm.waves && gm.waves.LivingCount > 0) AfternoonCombat = true;
            stageClock += dt;
                                                                                                                                    
                                                                                                                                           
                                                                      
            float t = Mathf.Clamp01(stageClock / Mathf.Max(.1f, rules.duskSeconds)) * (EveningsOpenEnded ? rules.afternoonLight : 1f);
            darknessLevel = rules.duskDarkness * t;
            LerpLighting(t);
            TickPickups(dt);
            TickMining(dt, true);
            TickLampPlacement();
            if (TickBaseDoor()) return;                                   
            if (!EveningsOpenEnded && stageClock >= stageLength) BeginNight();
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
            afternoonCombat = false;
            if (gm.RunPhase != ArenaUiState.Night) gm.CampaignSetPhase(ArenaUiState.Night);
            CampaignEvents.RaisePhase("night", Day);
            DismissStragglers();
            LerpLighting(1f);
            darknessLevel = rules.nightDarkness;
            if (baseLight) baseLight.intensity = 2.2f;
            SpawnShadows();
            SpawnNightOre(Day, SpawnDarkClusters());
            Say(Line.S02);
            nightWhispers = true;
            NightObjective();
        }

                                                                                                                                               
                                                                                                                                    
        void NightObjective()
        {
            if (Stage != CampaignStage.Night || Day <= 0) return;
            Vector3 from = gm.player ? gm.player.Position : DoorPoint;
            if (TryNearestNode(NodeKind.Dark, from, out var dark))
                Objective(ArenaText.Pair("Night: F sets a lamp, hold E in its light for dark matter; E at the base door to repair, upgrade or wait for dawn","Night: F sets a lamp, hold E in its light for dark matter; E at the base door to repair, upgrade or wait for dawn"), dark);
            else
                Objective(EveningsOpenEnded ? ArenaText.Pair("Night: tonight's dark matter is mined out; E at the base door, then Wait for dawn starts the next day","Night: tonight's dark matter is mined out; E at the base door, then Wait for dawn starts the next day")
                    : ArenaText.Pair("Night: tonight's dark matter is mined out; E at the base door to repair, upgrade or wait for dawn","Night: tonight's dark matter is mined out; E at the base door to repair, upgrade or wait for dawn"), DoorPoint);
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
            if (!EveningsOpenEnded && stageClock >= stageLength) EndNight(false);
        }

                                                                                                                                        
                                                                                                                                            
                                                                                                                                             
                                                                                                                   
        public void EndNight(bool waitedAtBase)
        {
            if (Stage != CampaignStage.Night && Stage != CampaignStage.Base) return;
            if (Stage == CampaignStage.Base) ui.HideBase();
            baseVisitFrom = CampaignStage.Off;
            CampaignEvents.RaisePhase("dawn", Day);
            CampaignEvents.RaiseDawn(Day, new CampaignCost(alloy, energy, dark), new CampaignCost(EarnedAlloy, EarnedEnergy, EarnedDark));
            ClearNight(false);
            DismissStragglers();                                                                                       
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
            bool afternoon = Stage == CampaignStage.Dusk;
            prompt = EveningsOpenEnded
                ? afternoon ? ArenaText.Pair("E: enter the base to upgrade, repair or go into the night","E: enter the base to upgrade, repair or go into the night")
                    : ArenaText.Pair("E: enter the base to repair, upgrade or wait for dawn","E: enter the base to repair, upgrade or wait for dawn")
                : afternoon ? ArenaText.Pair("E: enter the base to upgrade or repair (the afternoon waits)","E: enter the base to upgrade or repair (the afternoon waits)")
                    : ArenaText.Pair("E: enter the base to upgrade or repair (the night waits)","E: enter the base to upgrade or repair (the night waits)");
            if (!EPressed) return false;
            OpenBase();
            return true;
        }

        string NightHint()
        {
            string lampText = ArenaText.Pair($"LAMPS ×{lampStock} (F TO PLACE)",$"LAMPS ×{lampStock} (F TO PLACE)");
            if (Stage == CampaignStage.Dusk)
            {
                int left = OreLeftToday;
                return Dot(ArenaText.Pair("IN THE AFTERNOON ORE NEEDS NO LAMP","IN THE AFTERNOON ORE NEEDS NO LAMP"),
                    left > 0 ? ArenaText.Pair($"{left} ORE NODES LEFT TODAY",$"{left} ORE NODES LEFT TODAY") : ArenaText.Pair("TODAY'S ORE IS MINED OUT","TODAY'S ORE IS MINED OUT"),
                    lampText,
                    EveningsOpenEnded ? ArenaText.Pair("NO CLOCK · CHOOSE INTO THE NIGHT AT THE BASE DOOR (E)","NO CLOCK · CHOOSE INTO THE NIGHT AT THE BASE DOOR (E)") : ArenaText.Pair($"MINED TODAY: ALLOY +{nightAlloy}",$"MINED TODAY: ALLOY +{nightAlloy}"));
            }
                                                                                                                                                 
            var risk = TonightsHaul;
            string atRisk = ArenaText.Pair($"A DEATH LOSES: ALLOY {risk.alloy} · ENERGY {risk.energy} · DARK MATTER {risk.dark} (THE BASE KEEPS IT)",$"A DEATH LOSES: ALLOY {risk.alloy} · ENERGY {risk.energy} · DARK MATTER {risk.dark} (THE BASE KEEPS IT)");
            return Dot(ArenaText.Pair("NO FIGHTING AT NIGHT · HOLD E IN LAMP LIGHT TO MINE","NO FIGHTING AT NIGHT · HOLD E IN LAMP LIGHT TO MINE"), lampText, atRisk,
                EveningsOpenEnded ? ArenaText.Pair("WAIT FOR DAWN AT THE BASE (E)","WAIT FOR DAWN AT THE BASE (E)")
                    : ArenaText.Pair($"TONIGHT: ALLOY +{nightAlloy} · DARK MATTER +{nightDark}",$"TONIGHT: ALLOY +{nightAlloy} · DARK MATTER +{nightDark}"));
        }

                            

        void TickLampPlacement()
        {
            if (!FPressed || WorldMap.Active && WorldMap.Active.TrialActive) return;
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

                                       

                                                                                                                                             
                                                                                                                                          
                                                                                                                         
        const float NodeSpacing = 2.6f, NodeEdgeMargin = 6f, TrailMinSpacing = 5f, TrailBaseGap = 3f, TrailSideSpread = 3.5f, HoleKeepOut = 7f;

                                                                                                                                             
                                                                                                                                            
                                                                                                                                          
                                                                                                                                       
                                                                                                                                        
                                                                                                                             
                                                                                                                                         
                                      
        void SpawnAfternoonOre(int day)
        {
            int total = rules.OreNodes(day), yield = rules.OreYield(day), shown = Mathf.Max(1, rules.trailShown);
            var trail = PlanTrail(Mathf.Min(rules.trailNodes, total), day);
            for (int k = 0; k < trail.Count; k++) AddNode(NodeKind.Ore, trail[k], yield, k >= shown).trailIndex = k;
                                                                                                                                      
            for (int i = trail.Count; i < total; i++)
                if (TryNodePoint(out var at)) AddNode(NodeKind.Ore, at, yield);
            SpawnScatteredOre();
        }

                                                                                                                                            
                                                                                                                                              
        Vector3? trailTarget;

                                                                                                                                           
                                                                                                                                          
                                                                                              
        List<Vector3> PlanTrail(int count, int day)
        {
            var points = new List<Vector3>();
            if (HoleCentre is Vector3 standing) trailTarget = standing;
            if (count <= 0 || trailTarget is not Vector3 hole) return points;
            Vector3 toHole = Query.Flat(hole - BaseCentre);
            float holeDistance = toHole.magnitude;
            float start = Mathf.Max(rules.nodeBaseClearance, BaseHalf + TrailBaseGap);
            float reach = Mathf.Min(rules.trailReach + rules.trailReachPerDay * Mathf.Max(0, day - 1), holeDistance - HoleKeepOut);
            if (holeDistance < 1f || reach < start) return points;
            Vector3 dir = toHole / holeDistance, side = new(-dir.z, 0f, dir.x);
                                                                                                                                               
                                                                                                                                
            count = Mathf.Min(count, Mathf.FloorToInt((reach - start) / TrailMinSpacing) + 1);
            float last = float.MinValue;
            for (int k = 0; k < count; k++)
            {
                float along = count == 1 ? reach : Mathf.Lerp(start, reach, TrailShare(k, count));
                Vector3 p = BaseCentre + dir * along + side * ((k % 2 == 0 ? 1f : -1f) * Random.Range(.35f, 1f) * TrailSideSpread);
                if (!TrailPoint(ref p, dir, side, points)) continue;
                float progress = Vector3.Dot(p - BaseCentre, dir);
                if (progress < last + TrailMinSpacing * .5f) continue;
                last = progress;
                points.Add(p);
            }
            return points;
        }

                                                                                                                                        
                                                                                                                                          
                                                                                                
        float TrailShare(int k, int count)
        {
            int gaps = count - 1;
            if (gaps <= 0) return 1f;
            float end = Mathf.Clamp(rules.trailEndGap, .05f, 1f);
                                                                                                    
            float Sum(int n) => gaps == 1 ? n : n + (end - 1f) * n * (n - 1) * .5f / (gaps - 1);
            return Sum(Mathf.Clamp(k, 0, gaps)) / Sum(gaps);
        }

                                                                                                                                                  
        bool TrailPoint(ref Vector3 p, Vector3 dir, Vector3 side, List<Vector3> planned)
        {
            if (TrailSpotOk(p, planned)) return true;
            for (float r = 1f; r <= 8f; r += 1f)
            {
                if (TrailSpotOk(p + side * r, planned)) { p += side * r; return true; }
                if (TrailSpotOk(p - side * r, planned)) { p -= side * r; return true; }
                if (TrailSpotOk(p - dir * r, planned)) { p -= dir * r; return true; }
            }
            return false;
        }

        bool TrailSpotOk(Vector3 at, List<Vector3> planned)
        {
            if (!OreSpotOk(at, 1.2f)) return false;
            foreach (var other in planned) if (Query.Flat(other - at).sqrMagnitude < NodeSpacing * NodeSpacing) return false;
            return true;
        }

                                                                                                                                    
        Vector3? TrailFrontier()
        {
            Node best = null;
            foreach (var node in nodes)
                if (node.trailIndex >= 0 && !node.hidden && !node.mined && (best == null || node.trailIndex < best.trailIndex)) best = node;
            return best != null ? best.position : (Vector3?)null;
        }

                                                                                                                                      
                                               
        void SurfaceNextTrailNode(int after)
        {
            Node next = null;
            foreach (var node in nodes)
                if (node.hidden && node.trailIndex > after && (next == null || node.trailIndex < next.trailIndex)) next = node;
            if (next == null) return;
            next.hidden = false;
            BuildNodeView(next);
            Fx.Ring(next.position, 2.4f, new Color(.6f, .85f, 1f), .6f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PickupEnergy, next.position, .7f, .7f);
            ui.Flash(ArenaText.Pair("More ore has surfaced further out","More ore has surfaced further out"), 2f);
        }

                                                                                                                                   
        int OreLeftToday { get { int n = 0; foreach (var node in nodes) if (!node.mined && node.kind == NodeKind.Ore) n++; return n; } }

                                                                                                                                           
        const float NightOreFromCluster = 4f;

                                                                                                                                            
                                                                                                                                              
                                                                     
        void SpawnNightOre(int day, List<Vector3> clusters)
        {
            if (day <= 0) return;                                     
            int yield = rules.OreYield(day);
            for (int i = 0; i < rules.nightOreNodes; i++)
            {
                bool placed = false;
                if (clusters.Count > 0)
                {
                    Vector3 centre = clusters[i % clusters.Count];
                    float a = Random.value * Mathf.PI * 2f;
                    for (int attempt = 0; attempt < 8 && !placed; attempt++, a += Mathf.PI / 4f)
                    {
                        Vector3 at = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * NightOreFromCluster;
                        if (!OreSpotOk(at, 1f)) continue;
                        AddNode(NodeKind.Ore, at, yield);
                        placed = true;
                    }
                }
                if (!placed && TryNodePoint(out var free)) AddNode(NodeKind.Ore, free, yield);
            }
        }

                                                                                                                                              
                                                                                                                                              
        List<Vector3> SpawnDarkClusters()
        {
            var centres = new List<Vector3>();
            if (Day == 0) return centres;
            var regionsLeft = new List<int>();
            for (int r = 0; r < RegionCount; r++) regionsLeft.Add(r);
            while (centres.Count < rules.darkClusters && regionsLeft.Count > 0)
            {
                int pick = Random.Range(0, regionsLeft.Count);
                int region = regionsLeft[pick]; regionsLeft.RemoveAt(pick);
                var centre = RegionCentre(region, out var half);
                                                                                         
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector3 at = FindOpen(centre + new Vector3(Random.Range(-half.x * .7f, half.x * .7f), 0f, Random.Range(-half.y * .7f, half.y * .7f)), 1.2f, 6f);
                    if (!OreSpotOk(at, 1.2f)) continue;
                    AddCluster(at);
                    centres.Add(at);
                    break;
                }
            }
            return centres;
        }

        void AddCluster(Vector3 centre)
        {
            for (int n = 0; n < rules.nodesPerCluster; n++)
            {
                float a = n * Mathf.PI * 2f / rules.nodesPerCluster + Random.Range(-.3f, .3f);
                Vector3 at = FindOpen(centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.1f, .8f, 4f);
                if (WorldMap.Active && !WorldMap.Active.IsWalkable(at, .8f)) at = centre;
                AddNode(NodeKind.Dark, at, rules.darkPerNode);
            }
        }

                                                                                                                                  
        bool TryNodePoint(out Vector3 at)
        {
            Vector2 half = MapHalfSize;
            float hx = Mathf.Max(1f, half.x - NodeEdgeMargin), hz = Mathf.Max(1f, half.y - NodeEdgeMargin);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                at = new Vector3(Random.Range(-hx, hx), 0f, Random.Range(-hz, hz));
                if (OreSpotOk(at, 1.2f)) return true;
            }
            at = Vector3.zero;
            return false;
        }

                                                                                                                                       
                                            
        const float ScatterOffTrailDegrees = 50f;

                                                                                                                                             
                                                                                                                                              
                                                                                                                                            
                                                                                                                                                    
        void SpawnScatteredOre()
        {
            Vector3 away = trailTarget is Vector3 target ? Query.Flat(target - BaseCentre) : Vector3.zero;
            away = away.sqrMagnitude > 1f ? away.normalized : Vector3.zero;                                             
            float near = rules.nodeBaseClearance, far = Mathf.Max(near + 4f, rules.scatterReach);
            float maxCos = Mathf.Cos(ScatterOffTrailDegrees * Mathf.Deg2Rad);
            for (int i = 0; i < rules.scatterNodes; i++)
                for (int attempt = 0; attempt < 40; attempt++)
                {
                                                                                                                
                    float r = Mathf.Sqrt(Random.Range(near * near, far * far)), a = Random.value * Mathf.PI * 2f;
                    var bearing = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    if (Vector3.Dot(bearing, away) > maxCos) continue;
                    Vector3 at = BaseCentre + bearing * r;
                    if (!OreSpotOk(at, 1.2f)) continue;
                    AddNode(NodeKind.Ore, at, rules.scatterYield);
                    break;
                }
        }

                                                                                                                                             
                                                                                                
        bool OreSpotOk(Vector3 at, float radius)
        {
            Vector2 half = MapHalfSize;
            if (Mathf.Abs(at.x) > half.x - 2f || Mathf.Abs(at.z) > half.y - 2f) return false;
            if (Query.Flat(at - BaseCentre).magnitude < rules.nodeBaseClearance) return false;
            var map = WorldMap.Active;
            if (map && (!map.IsWalkable(at, radius) || Quicksand.SpeedFactor(at) < .99f)) return false;
            foreach (var node in nodes) if (Query.Flat(node.position - at).sqrMagnitude < NodeSpacing * NodeSpacing) return false;
            return true;
        }

        Node AddNode(NodeKind kind, Vector3 at, int amount, bool hidden = false)
        {
            var node = new Node { kind = kind, position = at, amount = amount, hidden = hidden, glint = Random.value * 6f };
            nodes.Add(node);
            if (!hidden) BuildNodeView(node);
            return node;
        }

        void BuildNodeView(Node node)
        {
            var kind = node.kind;
            var root = new GameObject(kind + "Node").transform;
            root.SetParent(worldRoot, false);
            root.position = node.position;
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
                                                                                              
        public int NodesLeft(NodeKind kind) { int n = 0; foreach (var node in nodes) if (!node.mined && !node.hidden && node.kind == kind) n++; return n; }
                                                                                                                                          
                                                                                
        public float MarkerReach(Vector3 marker)
        {
            var node = NearestNode(marker, .5f);
            return node != null && node.kind != NodeKind.Core ? Mathf.Min(3f, rules.mineRange) : 3f;
        }

        public bool TryNearestNode(NodeKind kind, Vector3 from, out Vector3 at)
        {
            at = Vector3.zero; float best = float.MaxValue; bool found = false;
            foreach (var node in nodes)
            {
                if (node.mined || node.hidden || node.kind != kind) continue;
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
                if (node.mined || node.hidden) continue;
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
            if (node.trailIndex >= 0) SurfaceNextTrailNode(node.trailIndex);
                                                                                                                
            AfternoonObjective();
            NightObjective();
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
                                                                                                                             
            bool afternoon = Stage == CampaignStage.Dusk;
            ui.Flash(afternoon
                ? ArenaText.Pair(                    "You fell in the afternoon. You wake at the base with nothing lost, and the afternoon goes on.",                    "You fell in the afternoon. You wake at the base with nothing lost, and the afternoon goes on.")
                : ArenaText.Pair(                    $"You fell in the dark. Everything mined or picked up tonight is lost: {lost.alloy} alloy, {lost.energy} energy, {lost.dark} dark matter",                    $"You fell in the dark. Everything mined or picked up tonight is lost: {lost.alloy} alloy, {lost.energy} energy, {lost.dark} dark matter"), 5f);
            if (!afternoon) Say(Line.K01);                                                                                   
            float until = Time.realtimeSinceStartup + rules.nightDeathSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;
            var p = gm.player;
            if (p) p.ResetForSpawn();                                                                      
            knockedOut = false;
            if (afternoon && Stage == CampaignStage.Dusk)
            {
                PlacePlayer(DoorPoint);
                AfternoonObjective();
                yield break;
            }
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

                                           

                                                                                                                                     
        const float DarknessLightReach = 40f;

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
            Vector3 eye = gm.player ? Query.Flat(gm.player.Position) : BaseCentre;
            if (gm.player) darkness.AddLight(gm.player.Position, rules.playerGlowRadius, .7f);
            foreach (var node in nodes)
            {
                if (node.mined || node.hidden) continue;
                node.glint += Time.unscaledDeltaTime;
                if (Query.Flat(node.position - eye).sqrMagnitude > DarknessLightReach * DarknessLightReach) continue;
                float pulse = .25f + .2f * Mathf.Sin(node.glint * (node.kind == NodeKind.Ore ? 2.3f : 1.6f));
                darkness.AddLight(node.position, node.kind == NodeKind.Ore ? 1f : 1.4f, node.kind == NodeKind.Core ? .9f : pulse);
            }
                                                           
            foreach (var s in shadows)
                if (Query.Flat(s.position - eye).sqrMagnitude <= DarknessLightReach * DarknessLightReach) darkness.AddLight(s.position, .55f, .28f);
            if (regionDarkness == null || regionDarkness.Length != RegionCount) regionDarkness = new float[RegionCount];
            var extra = regionDarkness;
            for (int r = 0; r < extra.Length; r++) extra[r] = rules.crowdedDarkness * Mathf.Clamp01(ShadowsIn(r) / (float)rules.overrunShadows);
            darkness.SetRegions(rules.regions.x, rules.regions.y, extra, CampaignBounds);
            darkness.EndFrame();
        }
    }
}
