using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Afternoon and night (V2 doc section 4; the stages keep the names Dusk and Night in code, the screen says afternoon and night since
    /// 2026-11-06). Afternoon: rest and mining time, the light fades, and the day's alloy ore surfaces across the campaign map: a limited
    /// amount each day (kills drop none), most of it a trail that leads from the base toward the black hole a node or two at a time and
    /// lies closer together the nearer the hole, and a few small nodes scattered sparsely in the map's other directions. Night: the
    /// ground is dark except in lights (placed mining lamps, the base, the player's own faint glow); a lamp's light and the base's end at
    /// a ring drawn on the ground, and what stands inside the ring is lit. Weapons and skills work but hurt no shadow. Every region holds
    /// shadow monsters, more where the morning's kills fell (V2Campaign.Shadows.cs): they wait at a light's edge, hunt a player in the
    /// dark a little faster than the player walks, and a crowd of them goes into a lamp's light and puts it out, so a player who killed
    /// a lot somewhere cannot mine there. The night's nodes are dark matter (and rules.nightOreNodes ore nodes, 0 since
    /// 2026-11-06), mined only in lamp light; afternoon ore left unmined lies there too and needs the light as well.
    /// The base door opens in the afternoon and at night. With CampaignRules.openEndedEvenings (the rule since 2026-11-06) neither has a
    /// clock: the base panel's "into the night" ends the afternoon and its "wait for dawn" ends the night, and mining everything out moves
    /// nothing on. Without it both run on their clocks, which wait while the base panel is open. Dawn starts the next day at once: no dawn
    /// panel and no build phase.
    /// </summary>
    public sealed partial class V2Campaign
    {
        /// <summary>VoidAlloy (since 2026-11-10): the void alloy that grows where the black hole stood once the Nash Prophet is beaten.</summary>
        public enum NodeKind { Ore, Dark, Core, VoidAlloy }

        sealed class Node
        {
            public NodeKind kind; public Vector3 position; public int amount; public bool mined; public Transform view; public float glint;
            /// <summary>Alien energy a dark matter node carries besides its dark matter (the night's share of rules.nightEnergy).</summary>
            public int energy;
            /// <summary>Not surfaced yet (no view, cannot be mined): a later node of the ore trail.</summary>
            public bool hidden;
            /// <summary>Place on today's ore trail toward the black hole (0 nearest the base), or -1 for a node off the trail.</summary>
            public int trailIndex = -1;
        }

        sealed class Lamp
        {
            public Vector3 position; public float hp; public int region; public Transform view; public Light light; public LightWaveRing ring;
            /// <summary>Shadows at its edge or inside it now, and whether a crowd of them has gone in (it shelters no more).</summary>
            public int crowd; public bool smothered;
            /// <summary>When a crowd first stood at it (it goes in a moment later), or 0 while there is none.</summary>
            public float crowdSince;
            /// <summary>The lamp radius its ring and light were last drawn for (the ship's night light can widen it during a night).</summary>
            public float drawnRadius;
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
            regionKills.Clear(); regionKindKills.Clear(); regionShadows.Clear();
            lampStock = 0; nightAlloy = nightDark = 0; knockedOut = false;
            baseVisitFrom = CampaignStage.Off;
        }

        void BuildDarkness()
        {
            if (!darkness) darkness = V2Darkness.Create(gm);
            darkness.SetActive(false);
        }

        // ───── Regions (over the campaign map since 2026-11-06) ─────

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
            RestoreEnergyOnKill(enemy); // a point of the player's energy for each death (V2Campaign.Days.cs, since 2026-11-10)
            if (Stage is not (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Tutorial)) return;
            if (enemy.SuppressRewards || enemy.IsTrialSummon || enemy.DeathCause == DeathCause.FellIntoGap || enemy.IsBoss) return;
            int region = RegionOf(enemy.Position);
            regionKills[region] = (regionKills.TryGetValue(region, out int n) ? n : 0) + 1;
            NoteKillKind(region, enemy);
            DropCurrencies(enemy);
        }

        // ───── Afternoon ─────

        bool afternoonCombat;
        /// <summary>Combat in the afternoon (CampaignStage.Dusk), for the black hole's side quest: while on, the GameManager's phase is
        /// WaveActive (weapons fire, monsters act) instead of Night, in which monsters stand still and weapons do not fire. It turns on by
        /// itself once a monster is alive in the afternoon (TickDusk), and since 2026-11-10 near the black hole or while its timed check
        /// runs (V2Campaign.Hole.cs), and stays on until nightfall; going in and out of the base keeps it. Every afternoon starts with it
        /// off. Only the afternoon's own phase is switched: a cutscene, a question, the void trial or the pause keeps its phase and returns
        /// to the afternoon's own.</summary>
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

        /// <summary>Monsters still standing when the night falls or the day breaks (the black hole's side quest brings them by day and in
        /// the afternoon) leave without a kill or a drop: the night holds no fight, and the next morning's wave starts on its own.</summary>
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
            lampStock += rules.dailyFreeLamps; // from day 1 on lamps are crafted, from the alloy the afternoon's ore carries for them
            nightAlloy = nightDark = 0; knockedOut = false;
            BankHaul();
            SpawnAfternoonOre(Day);
            // Each afternoon's extra finds, in their own files (the partial methods compile away where a part is missing).
            AfternoonCrates();
            AfternoonVoidOre();
            darkness.SetActive(true);
            enhancementPulse = 1.4f;
            // V2 doc 4.3 (VIS-V07): the nightly strengthening has text and a visual. The numbers are the wave curve's own step from today
            // to tomorrow; nothing is added on top of the measured balance.
            ShowEnhancement();
            Say(Line.D01);
            AfternoonObjective();
        }

        void ShowEnhancement()
        {
            var set = gm.waves ? gm.waves.waveSet : null;
            if (!set || Day >= rules.days && !Overtime)
            {
                gm.hud?.Banner(ArenaText.Pair("下午 · 休整与采矿", "AFTERNOON · REST AND MINING"),
                    ArenaText.Pair("今天的合金矿露出了地表。数量有限，采完就没有了。", "Today's ore has surfaced. There is only so much; once mined, it is gone."));
                return;
            }
            int today = Mathf.Clamp(rules.WaveNumber(Day, rules.wavesPerDay - 1), 1, set.waves.Length), tomorrow = Mathf.Clamp(rules.WaveNumber(Day + 1, rules.wavesPerDay - 1), 1, set.waves.Length);
            // Since 2026-11-10 the HP follows the campaign's own reference DPS by day (CampaignRules.Days.cs), not the classic wave's.
            float hp = rules.DayReferenceDps(Day + 1) / rules.DayReferenceDps(Day) - 1f;
            float damageToday = rules.EnemyDamageOn(Day), damageTomorrow = rules.EnemyDamageOn(Day + 1);
            float damage = damageToday > 0f && damageTomorrow > 0f ? damageTomorrow / damageToday - 1f
                : set.DamageFactor(tomorrow) / set.DamageFactor(today) - 1f;
            // Only what rises is named: a "+0%" says nothing.
            int hpUp = Mathf.RoundToInt(hp * 100f), damageUp = Mathf.RoundToInt(damage * 100f);
            string zhRise = hpUp > 0 && damageUp > 0 ? $"明天的怪物生命 +{hpUp}%、伤害 +{damageUp}%。"
                : hpUp > 0 ? $"明天的怪物生命 +{hpUp}%。" : damageUp > 0 ? $"明天的怪物伤害 +{damageUp}%。" : "明天的怪物和今天一样强。";
            string enRise = hpUp > 0 && damageUp > 0 ? $"Tomorrow's monsters get HP +{hpUp}% and damage +{damageUp}%."
                : hpUp > 0 ? $"Tomorrow's monsters get HP +{hpUp}%." : damageUp > 0 ? $"Tomorrow's monsters get damage +{damageUp}%." : "Tomorrow's monsters are as strong as today's.";
            gm.hud?.Banner(ArenaText.Pair("下午 · 异星力量正在增强怪物", "AFTERNOON · ALIEN POWER STRENGTHENS THE MONSTERS"),
                ArenaText.Pair(zhRise + "合金矿数量有限，采完就没了。", enRise + " The ore is limited; once mined, it's gone."));
        }

        /// <summary>The afternoon's goal: how much of today's ore is left, with a marker on the ore trail's next node (the trail leads toward
        /// the black hole; the hole itself never gets a marker), or once the trail is mined, on the nearest ore left; none when all is mined.</summary>
        void AfternoonObjective()
        {
            if (Stage != CampaignStage.Dusk) return;
            int left = OreLeftToday;
            // Short since 2026-11-10: the goal says what to do, the keys are on the hint line and the prompts.
            string onward = EveningsOpenEnded ? ArenaText.Pair("回基地升级或入夜", "head back to the base to upgrade or start the night")
                : ArenaText.Pair("回基地升级", "head back to the base to upgrade");
            Vector3? next = TrailFrontier();
            if (next == null && left > 0 && gm.player && TryNearestNode(NodeKind.Ore, gm.player.Position, out var nearest)) next = nearest;
            Objective(left > 0 ? ArenaText.Pair($"采集合金矿（还剩 {left} 处）", $"Mine the alloy ore ({left} left)")
                : ArenaText.Pair("合金矿采完了，", "The ore is mined out; ") + onward, next);
        }

        void TickDusk(float dt)
        {
            prompt = "";
            if (CaughtNightDeath()) return;
            // A monster alive in the plain afternoon (the side quest's) turns the afternoon into a fight until nightfall.
            if (!afternoonCombat && gm.RunPhase == ArenaUiState.Night && gm.waves && gm.waves.LivingCount > 0) AfternoonCombat = true;
            stageClock += dt;
            // The light fades over duskSeconds. With open-ended evenings the afternoon is a rest and mining time in daylight
            // (a brighter afternoon): it turns only rules.afternoonLight of the way toward dusk's end and holds there; the dark
            // comes with the night the player goes into (BeginNight).
            float t = Mathf.Clamp01(stageClock / Mathf.Max(.1f, rules.duskSeconds)) * (EveningsOpenEnded ? rules.afternoonLight : 1f);
            darknessLevel = rules.duskDarkness * t;
            LerpLighting(t);
            TickPickups(dt);
            TickMining(dt, true);
            TickLampPlacement();
            if (TickBaseDoor()) return; // in the base the afternoon waits
            if (!EveningsOpenEnded && stageClock >= stageLength) BeginNight();
        }

        // The night's own light, as shares of the map's night ambient and night sun. It is what shows wherever the darkness is cut away
        // (in a lamp's light, in the base's): high enough that ground there reads as lit ground with nothing painted over it. Everything
        // else is dark because the darkness image covers it, not because the scene is unlit.
        const float NightAmbientShare = 1.15f, NightSunShare = .85f;

        void LerpLighting(float t)
        {
            var biome = WorldMap.Active ? WorldMap.Active.biome : null;
            if (!biome) return;
            RenderSettings.ambientLight = Color.Lerp(biome.dayAmbient, biome.ambient * NightAmbientShare, t);
            var sun = RenderSettings.sun;
            if (!sun) foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (light.type == LightType.Directional && light.enabled) { sun = light; break; }
            if (!sun) return;
            sun.color = Color.Lerp(biome.daySun, Color.Lerp(biome.nightSun, new Color(1f, .55f, .35f), (1f - t) * .6f), t);
            sun.intensity = Mathf.Lerp(biome.daySunIntensity, biome.nightSunIntensity * NightSunShare, t);
        }

        void ApplyDaylight()
        {
            var biome = WorldMap.Active ? WorldMap.Active.biome : null;
            if (biome) biome.ApplyLighting(false);
            darknessLevel = 0f;
            darkness?.SetActive(false);
            SetBaseLights(false);
        }

        // ───── Night ─────

        void BeginNight()
        {
            Stage = CampaignStage.Night; stageClock = 0f; stageLength = rules.NightLength(Day);
            afternoonCombat = false;
            if (gm.RunPhase != ArenaUiState.Night) gm.CampaignSetPhase(ArenaUiState.Night);
            CampaignEvents.RaisePhase("night", Day);
            DismissStragglers();
            LerpLighting(1f);
            darknessLevel = rules.nightDarkness;
            SetBaseLights(true);
            SpawnShadows();
            SpawnNightOre(Day, SpawnDarkClusters());
            Say(Line.S02);
            nightWhispers = true;
            NightObjective();
        }

        /// <summary>The night's goal (from day 1; the tutorial night has its own), with a marker on the nearest dark matter left (the night's
        /// clusters lie in regions that touch, PlaceDarkClusters). Once it is all mined the marker points to the base door.</summary>
        void NightObjective()
        {
            if (Stage != CampaignStage.Night || Day <= 0) return;
            Vector3 from = gm.player ? gm.player.Position : DoorPoint;
            if (TryNearestNode(NodeKind.Dark, from, out var dark))
                Objective(ArenaText.Pair("在灯光里采集暗物质", "Mine dark matter in the lamplight"), dark);
            else
                Objective(ArenaText.Pair("暗物质采完了，回基地等待天亮", "The dark matter is mined out. Wait for dawn at the base"), DoorPoint);
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
            if (TickBaseDoor()) return; // in the base the night waits
            TickWhispers(dt);
            if (Day == 0) TickTutorialNight(dt);
            if (!EveningsOpenEnded && stageClock >= stageLength) EndNight(false);
        }

        /// <summary>Dawn: the night is over (the player chose to wait for dawn at the base, the dark knocked the player out, or without
        /// open-ended evenings its time ran out). Since 2026-11-06 there is no dawn panel and no build phase: the player stands at the base
        /// door and the next day begins at once. Every node not mined is gone: nothing of a day's ore or dark matter carries over. After the
        /// last planned day with the ship ready, the ignition starts instead; without it the days go on.</summary>
        public void EndNight(bool waitedAtBase)
        {
            if (Stage != CampaignStage.Night && Stage != CampaignStage.Base) return;
            if (Stage == CampaignStage.Base) ui.HideBase();
            baseVisitFrom = CampaignStage.Off;
            CampaignEvents.RaisePhase("dawn", Day);
            CampaignEvents.RaiseDawn(Day, new CampaignCost(alloy, energy, dark), new CampaignCost(EarnedAlloy, EarnedEnergy, EarnedDark));
            ClearNight(false);
            DismissStragglers(); // a death in the afternoon skips the night: the side quest's monsters may still stand
            ApplyDaylight();
            nightWhispers = false;
            Objective("", null);
            PlacePlayer(DoorPoint);
            if (Day >= rules.days && CanIgnite) { Ignite(); return; }
            if (Day >= rules.days)
            {
                // The last planned day is over and the ship cannot leave yet: the days go on (the last day's wave again) until it can.
                string missing = !engineFixed ? ArenaText.Pair("引擎还没修好", "The engine isn't repaired")
                    : !armorFixed ? ArenaText.Pair("装甲还没修好", "The armor isn't repaired")
                    : ArenaText.Pair("燃料还没满", "The tank isn't full");
                ui.Flash(ArenaText.Pair($"{missing}，飞船飞不起来，得再撑一天。", $"{missing}, so the ship can't leave yet. Hold out one more day."), 3.5f);
            }
            SaveStoryPhase(SavePhase.Morning); // the morning's save, before the new day's one-shot effects (V2Campaign.Save.cs)
            Stage = CampaignStage.Build; // StartNextWave begins a new day from this stage
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

        /// <summary>E at the base door, in the afternoon or at night, goes into the base (without open-ended evenings that clock waits while
        /// the panel is open). True when the base opened this frame (the caller ends its tick there).</summary>
        bool TickBaseDoor()
        {
            var p = gm.player;
            if (!p || mining != null || Query.Flat(p.Position - DoorPoint).magnitude >= 3.2f) return false;
            prompt = ArenaText.Pair($"按 {ArenaKeys.InteractKey} 进入基地", $"Press {ArenaKeys.InteractKey} to enter the base");
            if (!EPressed) return false;
            OpenBase();
            return true;
        }

        string NightHint()
        {
            if (Stage == CampaignStage.Dusk)
                return EveningsOpenEnded ? ArenaText.Pair("下午不会自己结束，想入夜时就回基地。", "The afternoon won't end on its own. Go back to the base when you're ready for the night.")
                    : ArenaText.Pair($"今天采到：合金 +{nightAlloy}", $"Mined today: alloy +{nightAlloy}");
            // At night the note names what a death would cost: everything gained since nightfall or the last base visit (the afternoon is kept).
            var risk = TonightsHaul;
            // Named only while there is something to lose.
            string atRisk = risk.alloy + risk.energy + risk.dark <= 0 ? ""
                : ArenaText.Pair($"现在倒下，会丢掉身上的合金 {risk.alloy}、能量 {risk.energy}、暗物质 {risk.dark}，回到基地就能存下。", $" If you fall now, you lose the {risk.alloy} alloy, {risk.energy} energy and {risk.dark} dark matter you carry; reach the base to keep them.");
            return ArenaText.Pair("武器伤不到影子。", "Weapons can't hurt shadows.") + atRisk
                + (EveningsOpenEnded ? ArenaText.Pair("想结束今晚，就回基地等待天亮。", " When you're done for the night, wait for dawn at the base.")
                    : ArenaText.Pair($"今晚已采到合金 {nightAlloy}、暗物质 {nightDark}。", $" Tonight so far: {nightAlloy} alloy, {nightDark} dark matter."));
        }

        // ───── Lamps ─────

        void TickLampPlacement()
        {
            if (!FPressed || WorldMap.Active && WorldMap.Active.TrialActive) return;
            var p = gm.player;
            if (!p || !p.IsAlive) return;
            if (lampStock <= 0) { ui.Flash(ArenaText.Pair("矿灯用完了。可以在基地的飞船发射区用合金制造。", "You're out of lamps. Craft more with alloy at the base's Ship Launch Bay.")); return; }
            Vector3 at = Query.Flat(p.Position + p.AimDirection.normalized * .8f);
            if (WorldMap.Active && (WorldMap.Active.IsWall(at, .4f) || !WorldMap.Active.Contains(at, .5f))) at = Query.Flat(p.Position);
            PlaceLamp(at);
            lampStock--;
        }

        /// <summary>A lamp's light radius: rules.lampRadius, larger by the ship's night light (lampRadiusScale, since 2026-11-10). Its light,
        /// its glow in the darkness, the mining it allows and the shadows it scares all follow it.</summary>
        float LampRadius => rules.lampRadius * Mathf.Max(.1f, lampRadiusScale);

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
            else if (!Art.Enabled) Whitebox.Build("lamp", root, "LampWhitebox"); // the whitebox table's lamp (2026-11-10)
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
            lamp.light.type = LightType.Point; lamp.light.range = LampRadius * 1.15f; lamp.light.intensity = LampLightFull; lamp.light.color = new Color(1f, .9f, .7f);
            lamp.view = root;
            lamps.Add(lamp);
            lamp.ring = LightWaveRing.Create(root, "LampEdge");
            lamp.ring.sparkleRate = 0f;
            DrawLampEdge(lamp);
            Fx.Ring(at, LampRadius, new Color(1f, .9f, .6f), .45f);
        }

        /// <summary>The ring on the ground at the edge of a lamp's light: what stands inside it is lit, can be mined, and is out of the
        /// shadows' reach. It turns violet once a crowd of shadows has gone in.</summary>
        // A lamp's own light only warms the ground round it (the darkness cut away is what shows its reach): full and worn down.
        const float LampLightFull = 1.5f, LampLightWorn = .5f;
        // The line at a light's edge: thin and faint, so it marks where the light ends without glowing; a crowd's warning shows stronger.
        const float LightEdgeWidth = .16f, LightEdgeAlpha = .5f, LightEdgeGlow = .7f;

        void DrawLampEdge(Lamp lamp)
        {
            bool crowded = lamp.smothered || lamp.crowdSince > 0f;
            lamp.drawnRadius = LampRadius;
            if (lamp.light) lamp.light.range = LampRadius * 1.15f;
            if (lamp.ring) lamp.ring.Draw(lamp.position, LampRadius, crowded ? LightEdgeWidth * 2.2f : LightEdgeWidth,
                crowded ? new Color(.75f, .3f, 1f, .9f) : new Color(1f, .9f, .7f, LightEdgeAlpha), crowded ? 1.1f : LightEdgeGlow);
        }

        void TickLamps(float dt)
        {
            for (int i = lamps.Count - 1; i >= 0; i--)
            {
                var lamp = lamps[i];
                if (!Mathf.Approximately(lamp.drawnRadius, LampRadius)) DrawLampEdge(lamp);
                // A lamp with a crowd at it flickers hard: before they go in, and while they wear it down.
                bool crowded = lamp.smothered || lamp.crowdSince > 0f;
                if (lamp.light) lamp.light.intensity = Mathf.Lerp(LampLightWorn, LampLightFull, lamp.hp / rules.lampHp) * (1f + (crowded ? .45f : .05f) * Mathf.Sin(Time.time * (crowded ? 31f : 9f) + i));
                if (lamp.hp > 0f) continue;
                Fx.Burst(lamp.position + Vector3.up, Vector3.zero, new Color(1f, .8f, .4f), 10, 6f, 360f, .3f);
                if (lamp.view) Destroy(lamp.view.gameObject);
                lamps.RemoveAt(i);
                Say(Line.S03);
            }
        }

        /// <summary>The ground point lies in a lamp's light or the base's: inside the ring drawn on the ground, where a node can be
        /// mined. A lamp a crowd of shadows has gone into is covered and lights nothing. The player's own glow does not count: it neither
        /// holds shadows back nor lets the player mine.</summary>
        bool InLampLight(Vector3 p)
        {
            if (InBaseLight(p)) return true;
            float radius = LampRadius;
            foreach (var lamp in lamps)
                if (!lamp.smothered && Query.Flat(p - lamp.position).sqrMagnitude <= radius * radius) return true;
            return false;
        }
        bool InBaseLight(Vector3 p) => Query.Flat(p - BaseCentre).magnitude < rules.baseLightRadius;

        // ───── Nodes and mining ─────

        // Nodes keep this far apart and this far inside the campaign map's edge; the ore trail's gaps average at least TrailMinSpacing (they
        // shrink toward the hole, TrailShare) and each node lies at least half of it further along than the one before, its first node at
        // least TrailBaseGap cells beyond the base's walls, its last HoleKeepOut cells short of the black hole's centre.
        const float NodeSpacing = 2.6f, NodeEdgeMargin = 6f, TrailMinSpacing = 5f, TrailBaseGap = 3f, TrailSideSpread = 3.5f, HoleKeepOut = 7f, TrailRingGap = 3f;

        /// <summary>How far short of the black hole's centre the trail ends: outside its pull ring with TrailRingGap cells to spare, and
        /// never nearer than HoleKeepOut.</summary>
        float TrailHoleKeepOut
        {
            get
            {
                var map = WorldMap.Active;
                float ring = map && map.CampaignHole ? map.CampaignHole.GravityRadius : 0f;
                return Mathf.Max(HoleKeepOut, ring + TrailRingGap);
            }
        }

        /// <summary>Since 2026-11-06 every afternoon the day's alloy ore surfaces on the campaign map (kills drop none): rules.OreNodes(day)
        /// nodes, so a day's alloy has a ceiling. rules.trailNodes of them lie on a trail from the base toward the black hole, its farthest
        /// node trailReach + trailReachPerDay x (day - 1) cells out and never past the hole, closer together the nearer the hole
        /// (rules.trailEndGap). rules.trailShown of them are up at once, and mining one brings up the next, always further toward the hole
        /// (newly seen trail ore points the way; the hole itself has no marker). The rest lie at random on open ground across the map. On
        /// top of these, rules.scatterNodes nodes lie sparsely in the map's other directions (SpawnScatteredOre). Since 2026-11-10 the
        /// day's alloy is the spec's table (rules.afternoonAlloy, times the ship's afternoonOreScale) shared evenly by all of today's
        /// nodes, the remainder one each to the nodes nearest the black hole (ShareAfternoonAlloy). Ore not mined stays into the night
        /// (where it needs lamp light) and is gone at dawn: nothing carries over to the next day.</summary>
        void SpawnAfternoonOre(int day)
        {
            int count = rules.OreNodes(day), shown = Mathf.Max(1, rules.trailShown), first = nodes.Count;
            var trail = PlanTrail(Mathf.Min(rules.trailNodes, count), day);
            for (int k = 0; k < trail.Count; k++) AddNode(NodeKind.Ore, trail[k], 0, k >= shown).trailIndex = k;
            // A trail cut short (a short trail, ground it could not use) leaves its share to the random nodes: the day's count stays.
            for (int i = trail.Count; i < count; i++)
                if (TryNodePoint(out var at)) AddNode(NodeKind.Ore, at, 0);
            SpawnScatteredOre();
            // The day's alloy (with the ship's ore bonus) and the alloy for its lamps (rules.dayLampsToCraft).
            ShareAfternoonAlloy(first, CampaignRules.AfternoonAlloyTotal(rules.afternoonAlloy, day, afternoonOreScale) + rules.LampAlloyOn(day));
        }

        /// <summary>The day's alloy over the ore nodes placed from index first on: evenly, the remainder one each to the nodes nearest the
        /// black hole (or where it stood). A node left with nothing (more nodes than alloy) is taken away again.</summary>
        void ShareAfternoonAlloy(int first, int total)
        {
            var today = new List<Node>();
            for (int i = first; i < nodes.Count; i++) if (nodes[i].kind == NodeKind.Ore) today.Add(nodes[i]);
            if (today.Count == 0) return;
            if (trailTarget is Vector3 hole)
                today.Sort((a, b) => Query.Flat(a.position - hole).sqrMagnitude.CompareTo(Query.Flat(b.position - hole).sqrMagnitude));
            var shares = CampaignRules.Split(total, today.Count);
            for (int i = 0; i < today.Count; i++)
            {
                today[i].amount = shares[i];
                if (shares[i] > 0) continue;
                if (today[i].view) Destroy(today[i].view.gameObject);
                nodes.Remove(today[i]);
            }
        }

        // Where the ore trail heads: the black hole, and once the hidden boss has turned the hole into ground, the place where it stood, so
        // the day's ore keeps lying along one line out from the base (the economy ledger counts the afternoon's walk that way). Set in Begin.
        Vector3? trailTarget;

        /// <summary>Today's trail points from the base toward the black hole (or where it stood), nearest first: out to today's reach with
        /// the gaps shrinking toward the hole (TrailShare), a little to either side by turns, each on open ground and each further toward
        /// the hole than the one before. Empty when the map never had a black hole.</summary>
        List<Vector3> PlanTrail(int count, int day)
        {
            var points = new List<Vector3>();
            if (HoleCentre is Vector3 standing) trailTarget = standing;
            if (count <= 0 || trailTarget is not Vector3 hole) return points;
            Vector3 toHole = Query.Flat(hole - BaseCentre);
            float holeDistance = toHole.magnitude;
            float start = Mathf.Max(rules.nodeBaseClearance, BaseHalf + TrailBaseGap);
            float reach = Mathf.Min(rules.trailReach + rules.trailReachPerDay * Mathf.Max(0, day - 1), holeDistance - TrailHoleKeepOut);
            if (holeDistance < 1f || reach < start) return points;
            Vector3 dir = toHole / holeDistance, side = new(-dir.z, 0f, dir.x);
            // A short trail (day 1) holds fewer nodes, so its gaps average at least TrailMinSpacing; they shrink toward the hole (TrailShare),
            // so the last is shorter, and a node that would lie less than half of it beyond the one before is left out (below).
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

        /// <summary>Where node k of a trail of count nodes lies between its first (0) and its last (1). The gaps shrink evenly from the
        /// first to the last, which is rules.trailEndGap of the first, so the ore lies closer together the nearer the black hole (more of
        /// it in view near the hole). With trailEndGap 1 the nodes are evenly spaced.</summary>
        float TrailShare(int k, int count)
        {
            int gaps = count - 1;
            if (gaps <= 0) return 1f;
            float end = Mathf.Clamp(rules.trailEndGap, .05f, 1f);
            // Gap i (from 0) is 1 + (end - 1) x i / (gaps - 1); Sum(n) adds up the first n of them.
            float Sum(int n) => gaps == 1 ? n : n + (end - 1f) * n * (n - 1) * .5f / (gaps - 1);
            return Sum(Mathf.Clamp(k, 0, gaps)) / Sum(gaps);
        }

        /// <summary>Moves a trail point onto usable ground nearby, sideways first, then back toward the base; false when none is close.</summary>
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

        /// <summary>The next ore of today's trail that has surfaced and is not mined (the one nearest the base), or null.</summary>
        Vector3? TrailFrontier()
        {
            Node best = null;
            foreach (var node in nodes)
                if (node.trailIndex >= 0 && !node.hidden && !node.mined && (best == null || node.trailIndex < best.trailIndex)) best = node;
            return best != null ? best.position : (Vector3?)null;
        }

        /// <summary>Mining a trail node brings up the next hidden one, further out toward the black hole, so rules.trailShown stay up
        /// until the trail runs out.</summary>
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
            ui.Flash(ArenaText.Pair("更远处又露出了一处合金矿", "More ore has surfaced further out"), 2f);
        }

        /// <summary>Ore nodes of today not mined yet, the trail's hidden ones included: what is left of the day's alloy.</summary>
        int OreLeftToday { get { int n = 0; foreach (var node in nodes) if (!node.mined && node.kind == NodeKind.Ore) n++; return n; } }

        // A night ore node lies this far from its cluster's centre: beside the dark matter, inside the light of a lamp set at the cluster.
        const float NightOreFromCluster = 4f;

        /// <summary>The night's few ore nodes (rules.nightOreNodes of rules.OreYield(day) alloy; the night's nodes are mostly dark matter):
        /// each beside one of tonight's dark matter clusters by turns, inside the light of a lamp set there (as on the tutorial night), or at
        /// random on open ground when no cluster has room.</summary>
        void SpawnNightOre(int day, List<Vector3> clusters)
        {
            if (day <= 0) return; // the tutorial night places its own
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

        /// <summary>Tonight's dark matter: rules.darkClusters clusters of rules.nodesPerCluster nodes, at most one per region of the campaign
        /// map, on open ground clear of the base. Since 2026-11-10 the night's dark matter is the table's (rules.nightDark; a night past it
        /// takes its last entry), shared evenly by tonight's nodes (ShareNightDark). Returns the clusters' centres (none on the tutorial
        /// night, which places its own).</summary>
        List<Vector3> SpawnDarkClusters()
        {
            int first = nodes.Count;
            var centres = PlaceDarkClusters();
            ShareNightDark(first, CampaignRules.ByDay(rules.nightDark, Day));
            return centres;
        }

        /// <summary>The night's dark matter over the dark matter nodes placed from index first on: evenly, the remainder one each to the
        /// first nodes. A node left with nothing (more nodes than dark matter) is taken away again.</summary>
        void ShareNightDark(int first, int total)
        {
            var tonight = new List<Node>();
            for (int i = first; i < nodes.Count; i++) if (nodes[i].kind == NodeKind.Dark) tonight.Add(nodes[i]);
            var shares = CampaignRules.Split(total, tonight.Count);
            for (int i = 0; i < tonight.Count; i++)
            {
                tonight[i].amount = shares[i];
                if (shares[i] > 0) continue;
                if (tonight[i].view) Destroy(tonight[i].view.gameObject);
                nodes.Remove(tonight[i]);
            }
            // Since 2026-11-10 the night's dark matter also carries the night's alien energy (rules.nightEnergy), shared the same way by
            // the nodes left.
            tonight.RemoveAll(node => node.amount <= 0);
            var energyShares = CampaignRules.Split(rules.NightEnergy(Day), tonight.Count);
            for (int i = 0; i < tonight.Count; i++) tonight[i].energy = energyShares[i];
        }

        List<Vector3> PlaceDarkClusters()
        {
            var centres = new List<Vector3>();
            if (Day == 0) return centres;
            var regionsLeft = new List<int>();
            for (int r = 0; r < RegionCount; r++) regionsLeft.Add(r);
            int cols = Mathf.Max(1, rules.regions.x);
            var taken = new List<int>();
            while (centres.Count < rules.darkClusters && regionsLeft.Count > 0)
            {
                // The night's clusters lie in regions that touch, so that after one is mined the next is a short walk on. Two regions
                // picked from the whole map could lie at its two ends, and the goal's marker then pointed right across it.
                int pick = Random.Range(0, regionsLeft.Count);
                if (taken.Count > 0)
                {
                    var touching = new List<int>();
                    for (int i = 0; i < regionsLeft.Count; i++)
                        foreach (int other in taken)
                            if (CampaignRules.RegionsTouch(regionsLeft[i], other, cols)) { touching.Add(i); break; }
                    if (touching.Count > 0) pick = touching[Random.Range(0, touching.Count)];
                }
                int region = regionsLeft[pick]; regionsLeft.RemoveAt(pick);
                var centre = RegionCentre(region, out var half);
                // A region whose ground cannot take a cluster gives its turn to another.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector3 at = FindOpen(centre + new Vector3(Random.Range(-half.x * .7f, half.x * .7f), 0f, Random.Range(-half.y * .7f, half.y * .7f)), 1.2f, 6f);
                    // Dark matter lies outside the base's light: a lamp has to be carried to it.
                    if (!OreSpotOk(at, 1.2f) || Query.Flat(at - BaseCentre).magnitude < rules.baseLightRadius + 4f) continue;
                    AddCluster(at);
                    centres.Add(at);
                    taken.Add(region);
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

        /// <summary>A random point for a node on open ground of the campaign map, clear of the base and of other nodes.</summary>
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

        // Scattered nodes keep at least this many degrees off the line from the base to the black hole: that way belongs to the trail,
        // so the other directions get them.
        const float ScatterOffTrailDegrees = 50f;

        /// <summary>The afternoon's few scattered nodes (since 2026-11-06): rules.scatterNodes nodes, shown from the start, on open ground
        /// between nodeBaseClearance and rules.scatterReach cells from the base and at least ScatterOffTrailDegrees off the line to the black
        /// hole (or where it stood). The map's other directions hold a little ore too, while the hole's way stays the dense one. The goal's
        /// marker points at the trail first and reaches them only once the trail is mined (AfternoonObjective). Since 2026-11-10 their alloy
        /// is their share of the day's (ShareAfternoonAlloy; rules.scatterYield is no longer read).</summary>
        void SpawnScatteredOre()
        {
            Vector3 away = trailTarget is Vector3 target ? Query.Flat(target - BaseCentre) : Vector3.zero;
            away = away.sqrMagnitude > 1f ? away.normalized : Vector3.zero; // no black hole ever: any direction will do
            float near = rules.nodeBaseClearance, far = Mathf.Max(near + 4f, rules.scatterReach);
            float maxCos = Mathf.Cos(ScatterOffTrailDegrees * Mathf.Deg2Rad);
            for (int i = 0; i < rules.scatterNodes; i++)
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    // Uniform over the ring's area, so the nodes are no thicker near the base than further out.
                    float r = Mathf.Sqrt(Random.Range(near * near, far * far)), a = Random.value * Mathf.PI * 2f;
                    var bearing = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    if (Vector3.Dot(bearing, away) > maxCos) continue;
                    Vector3 at = BaseCentre + bearing * r;
                    if (!OreSpotOk(at, 1.2f)) continue;
                    AddNode(NodeKind.Ore, at, 0);
                    break;
                }
        }

        /// <summary>Ground a node may stand on: inside the campaign map, walkable (no terrain, hole or gravity ring), no quicksand, at least
        /// nodeBaseClearance from the base centre and clear of the nodes already out.</summary>
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
            // Void alloy has no illustrated model yet: its whitebox look in both arts.
            if (kind == NodeKind.VoidAlloy) Whitebox.Build("voidalloy", root);
            else if (prefab)
            {
                var model = Instantiate(prefab, root);
                StripColliders(model);
                FitFootprint(model.transform, kind == NodeKind.Dark ? 1.5f : 1.8f);
            }
            // The whitebox (2026-11-10): the whitebox table's core, ore and dark matter node; the old boxes stay for the illustrated art
            // without a model (the core has none).
            else if (!Art.Enabled) Whitebox.Build(kind switch { NodeKind.Core => "core", NodeKind.Dark => "dark", _ => "ore" }, root);
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

        // ───── For automated checks ─────
        public int ShadowCount => shadows.Count;
        public int LampCount => lamps.Count;
        public int OverrunRegions { get { int n = 0; foreach (var pair in regionShadows) if (pair.Value >= rules.overrunShadows) n++; return n; } }
        /// <summary>Nodes of a kind that can be mined now (surfaced and not mined).</summary>
        public int NodesLeft(NodeKind kind) { int n = 0; foreach (var node in nodes) if (!node.mined && !node.hidden && node.kind == kind) n++; return n; }
        /// <summary>How near the goal's marker the player must be for the goal to say "right here": on a node that can be mined, the mine
        /// range (E does nothing farther out); anywhere else 3 cells.</summary>
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

        /// <summary>Hold E next to a node to mine it. At night the node must stand in lamp or base light; in the afternoon any node can be mined.</summary>
        void TickMining(float dt, bool dusk)
        {
            var p = gm.player;
            if (!p || !p.IsAlive) { mining = null; return; }
            var node = NearestNode(p.Position, rules.mineRange);
            if (node == null || node.kind == NodeKind.Core) { if (mining != null) { mining = null; mineProgress = 0f; } if (Stage != CampaignStage.Night) prompt = ""; return; }
            bool lit = dusk || InLampLight(node.position);
            string what = node.kind switch
            {
                NodeKind.Ore => ArenaText.Pair($"异星合金（+{node.amount}）", $"alien alloy (+{node.amount})"),
                NodeKind.VoidAlloy => ArenaText.Pair($"虚空合金（+{node.amount}）", $"void alloy (+{node.amount})"),
                _ when node.energy > 0 => ArenaText.Pair($"暗物质（+{node.amount}，能量 +{node.energy}）", $"dark matter (+{node.amount}, energy +{node.energy})"),
                _ => ArenaText.Pair($"暗物质（+{node.amount}）", $"dark matter (+{node.amount})")
            };
            if (!lit)
            {
                prompt = lampStock > 0
                    ? ArenaText.Pair($"太黑了，先按 {ArenaKeys.LampKey} 放一盏矿灯", $"It's too dark. Press {ArenaKeys.LampKey} to set down a lamp first")
                    : ArenaText.Pair($"太黑了，手里也没有矿灯。回基地，在飞船发射区造一盏（{rules.lampCost} 合金）",
                        $"It's too dark, and you have no lamps left. Craft one at the base's Ship Launch Bay ({rules.lampCost} alloy)");
                mining = null; mineProgress = 0f;
                return;
            }
            if (!EHeld) { prompt = ArenaText.Pair($"按住 {ArenaKeys.InteractKey} 采集{what}", $"Hold {ArenaKeys.InteractKey} to mine {what}"); mining = null; mineProgress = 0f; return; }
            if (mining != node) { mining = node; mineProgress = 0f; }
            mineProgress += dt / Mathf.Max(.05f, rules.mineSeconds);
            prompt = ""; // the mining bar shows the progress (V2CampaignUi.Feedback.cs)
            if (Random.value < dt * 12f) Fx.Pop(node.position + Vector3.up * Random.Range(.4f, 1.2f), node.kind == NodeKind.Ore ? new Color(.7f, .85f, 1f) : new Color(.7f, .3f, 1f), .5f);
            if (mineProgress < 1f) return;
            CompleteMining(node);
        }

        void CompleteMining(Node node)
        {
            node.mined = true; mining = null; mineProgress = 0f;
            if (node.kind == NodeKind.Ore)
            {
                Gain(new CampaignCost(node.amount));
                if (Stage is CampaignStage.Night or CampaignStage.Dusk) nightAlloy += node.amount;
                ui.ShowGain(1, node.amount, node.position);
            }
            else if (node.kind == NodeKind.VoidAlloy) MinedVoidAlloy(node.amount); // V2Campaign.Void.cs
            else
            {
                Gain(new CampaignCost(0, node.energy, node.amount));
                nightDark += node.amount;
                ui.ShowGain(2, node.amount, node.position);
                if (node.energy > 0) ui.ShowGain(0, node.energy, node.position + Vector3.up * .9f);
            }
            Fx.Burst(node.position + Vector3.up, Vector3.up, node.kind == NodeKind.Ore ? new Color(.7f, .85f, 1f) : new Color(.7f, .3f, 1f), 12, 7f, 360f, .35f);
            if (node.view) Destroy(node.view.gameObject);
            OnTutorialMined(node.kind);
            if (node.trailIndex >= 0) SurfaceNextTrailNode(node.trailIndex);
            // The goal's count and marker move on to what is left (each returns at once outside its own stage).
            AfternoonObjective();
            NightObjective();
        }

        // ───── Shadows (how they rise and move: V2Campaign.Shadows.cs) ─────

        /// <summary>A shadow's touch costs a share of the player's max HP; the touch that empties it kills (since 2026-11-06 the night kills:
        /// the player falls, loses tonight's haul and wakes at the base for the next day, see NightDeath).</summary>
        void HitPlayerAtNight(Shadow s)
        {
            var p = gm.player;
            if (!p.IsAlive || knockedOut) return;
            // A tutorial night shadow's touch takes a share of a normal one (rules.tutorialShadowHitScale).
            float hit = Mathf.Max(1f, p.maxHp * rules.shadowHitShare * (s.tutorial ? rules.tutorialShadowHitScale : 1f));
            gm.hud?.FlashDamage();
            gm.Shake(.18f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PlayerHit, p.Position, .7f);
            p.ReceiveDamage(new DamageInfo { amount = hit, source = DamageSource.Hit, knockDir = Query.Flat(p.Position - s.position).normalized });
        }

        /// <summary>GameManager asks before a dead player loses the run: a death in the afternoon or at night is the campaign's (the rule since
        /// 2026-11-06), a death in the morning still ends the run.</summary>
        public bool CatchesPlayerDeath() => Running && (knockedOut || Stage is CampaignStage.Dusk or CampaignStage.Night);

        bool CaughtNightDeath()
        {
            if (knockedOut) return true;
            if (!gm.player || gm.player.IsAlive) return false;
            StartCoroutine(NightDeath());
            return true;
        }

        /// <summary>Died in the afternoon or at night. At night the player lies in the dark while the death animation plays, everything
        /// mined or picked up tonight is lost (the base's stock, the rooms and the ship stay), and the player wakes at the base for the dawn.
        /// In the afternoon nothing is lost: after the same pause the player
        /// gets up at the base door and the afternoon goes on.</summary>
        IEnumerator NightDeath()
        {
            knockedOut = true;
            mining = null; mineProgress = 0f;
            // Since 2026-11-06 the afternoon can hold a fight (the black hole's side quest, the void trial behind its gate).
            bool afternoon = Stage == CampaignStage.Dusk;
            // The afternoon's haul is never at risk (only what comes in after nightfall is tonight's haul, see Gain), so this loses
            // nothing in the afternoon.
            var lost = LoseHaul(rules.nightDeathLoss);
            // Only a death at night is a night death (2026-11-10): a fall in the afternoon raised the same event, and the analytics then
            // marked the day's night_death although the player never died at night. The sheet has no column for afternoon falls yet;
            // one comes with the next change of the analytics sheet (its Apps Script has to be deployed again).
            if (!afternoon) CampaignEvents.RaiseNightDeath(lost, Day);
            ui.Flash(afternoon
                ? ArenaText.Pair("你在下午倒下了。在基地醒来，什么都没丢，下午还在继续。",
                    "You fell in the afternoon. You wake at the base with nothing lost, and the afternoon goes on.")
                : ArenaText.Pair($"你在黑暗中倒下了。今晚采到、捡到的全部丢失：合金 {lost.alloy}、能量 {lost.energy}、暗物质 {lost.dark}",
                    $"You fell in the dark. Everything mined or picked up tonight is lost: {lost.alloy} alloy, {lost.energy} energy, {lost.dark} dark matter"), 5f);
            if (!afternoon) Say(Line.K01); // its line says tonight's haul is lost, which a fall in the afternoon does not do
            float until = Time.realtimeSinceStartup + rules.nightDeathSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;
            var p = gm.player;
            if (p) p.ResetForSpawn(); // full HP, standing again (the death pose resets with the new spawn)
            knockedOut = false;
            if (afternoon && Stage == CampaignStage.Dusk)
            {
                PlacePlayer(DoorPoint);
                AfternoonObjective();
                yield break;
            }
            if (Stage is CampaignStage.Dusk) Stage = CampaignStage.Night; // EndNight takes it from the night
            EndNight(false);
        }

        float whisperClock;
        void TickWhispers(float dt)
        {
            // Danger is heard: the closer and the more shadows, the more often their whisper plays.
            if (!nightWhispers || shadows.Count == 0) return;
            var p = gm.player;
            float near = 0f;
            foreach (var s in shadows) near += Mathf.Clamp01(1f - Query.Flat(s.position - p.Position).magnitude / 18f);
            whisperClock -= dt * Mathf.Min(3f, near);
            if (whisperClock > 0f) return;
            whisperClock = 2.5f;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShadeSpawn, p.Position + Random.onUnitSphere * 4f, Random.Range(.55f, .75f), Mathf.Clamp01(.35f + near * .2f));
        }

        // ───── The darkness overlay ─────

        // Only lights this close to the player are drawn: on the large map the overlay's light budget goes to what can be on screen.
        const float DarknessLightReach = 40f;

        void UpdateDarkness()
        {
            if (!darkness) return;
            enhancementPulse = Mathf.Max(0f, enhancementPulse - Time.unscaledDeltaTime);
            bool show = darknessLevel > .001f && Stage is CampaignStage.Dusk or CampaignStage.Night or CampaignStage.Base;
            darkness.SetActive(show || enhancementPulse > 0f);
            if (!show && enhancementPulse <= 0f) return;
            darkness.BeginFrame(darknessLevel, Mathf.Clamp01(enhancementPulse) * .35f);
            // A lamp's light and the base's are flat discs that end at their ring; a lamp a crowd wears down dims.
            foreach (var lamp in lamps) darkness.AddLight(lamp.position, LampRadius, Mathf.Clamp01(.55f + .45f * lamp.hp / rules.lampHp), true);
            darkness.AddLight(BaseCentre, rules.baseLightRadius, 1f, true);
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
            // The shadows' eyes glint faintly in the dark; one that has noticed the player shows more of itself.
            foreach (var s in shadows)
                if (Query.Flat(s.position - eye).sqrMagnitude <= DarknessLightReach * DarknessLightReach) darkness.AddLight(s.position, s.alert ? 1.4f : .55f, s.alert ? .55f : .28f);
            if (regionDarkness == null || regionDarkness.Length != RegionCount) regionDarkness = new float[RegionCount];
            var extra = regionDarkness;
            for (int r = 0; r < extra.Length; r++) extra[r] = rules.crowdedDarkness * Mathf.Clamp01(ShadowsIn(r) / (float)rules.overrunShadows);
            darkness.SetRegions(rules.regions.x, rules.regions.y, extra, CampaignBounds);
            darkness.EndFrame();
        }
    }
}
