using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed partial class V2Campaign
    {

        const float QuestSpawnSpreadDegrees = 70f;

        const float QuestFightAfter = .5f;

        WorldMap questMap;
        bool holeSeen, questAfternoon;
        float questSpawnClock, questIdleClock;
        readonly List<Enemy> questMonsters = new();
        string sideQuestText;

        public int HoleQuestMonsters => questMonsters.Count;
        public bool HoleSeen => holeSeen;

        void BeginHoleQuest(WorldMap map)
        {
            EndHoleQuest();
            questMap = map;
            holeSeen = questAfternoon = false;
            questSpawnClock = Mathf.Max(.5f, rules.holeQuestSpawnSeconds);
            questIdleClock = 0f;
            map.campaignTick = TickHoleQuest;
            map.campaignHoleIntroAllowed = HoleIntroAllowed;
            map.campaignPortalAllowed = HoleGateAllowed;
            map.campaignConfirmPortal = AskHiddenGate;
            map.HoleIntroFinished += OnHoleIntroFinished;
        }

        void EndHoleQuest()
        {
            if (questMap) questMap.ClearCampaignHooks();
            questMap = null;
            questMonsters.Clear();
            SetSideQuestText(null);
        }

        bool HoleIntroAllowed() => Running && Stage is (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk) && InPlay;

        bool HoleGateAllowed() => Running && Stage is (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk) && InPlay;

        bool InPlay => gm && gm.player && gm.player.IsAlive && Time.timeScale > 0f && gm.RunPhase is (ArenaUiState.WaveActive or ArenaUiState.Night);

        void OnHoleIntroFinished()
        {
            holeSeen = true;
            questSpawnClock = Mathf.Max(.5f, rules.holeQuestSpawnSeconds);
        }

        void AskHiddenGate(BlackHole hole, System.Action yes, System.Action no)
        {
            if (!ui) { yes?.Invoke(); return; }
            bool morning = Stage is CampaignStage.Day or CampaignStage.Lull;
            string body = ArenaText.Pair("A very powerful enemy may be waiting in there. Do you want to go in?","A very powerful enemy may be waiting in there. Do you want to go in?") + "\n\n"
                + (morning ? ArenaText.Pair("Dying in there ends the run.","Dying in there ends the run.")
                    : ArenaText.Pair("Dying in there wakes you at the base with nothing lost, and the afternoon goes on.","Dying in there wakes you at the base with nothing lost, and the afternoon goes on."));
            ui.Confirm(ArenaText.Pair("THE HIDDEN GATE","THE HIDDEN GATE"), body, yes, no);
        }

        void TickHoleQuest()
        {
            var map = questMap;
            if (!Running || !map || !map.CampaignMapApplied || !gm) return;
            var p = gm.player;

            if (map.TrialActive)
            {
                if (p && !p.IsAlive && CatchesPlayerDeath()) gm.CampaignAbortVoidTrial();
                TickHoleGuide(map);                                     
                return;
            }

            bool evening = Stage == CampaignStage.Night || Stage == CampaignStage.Base && baseVisitFrom == CampaignStage.Night;
            bool afternoon = Stage == CampaignStage.Dusk || Stage == CampaignStage.Base && baseVisitFrom == CampaignStage.Dusk;
            if (questAfternoon && !afternoon) SinkQuestMonsters();
            questAfternoon = afternoon;

            map.SetCampaignHoleHidden(evening || Day <= 0);
            TickHoleGuide(map);                                                          

            questMonsters.RemoveAll(e => !e || !e.IsAlive || !e.gameObject.activeInHierarchy);
            var hole = map.CampaignHole;
            if (!hole || hole.Phase == HolePhase.Gone || map.TrialWon) { SetSideQuestText(null); return; }

            bool daylight = Stage is CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk;
            bool shown = !map.CampaignHoleHidden;

            if (!holeSeen && (map.HoleIntroDone || shown && daylight && hole.Phase != HolePhase.Active && OnScreen(hole.Centre))) holeSeen = true;

            string text = null;
            if (holeSeen)
            {
                text = hole.Phase == HolePhase.Active
                    ? ArenaText.Pair($"Lure monsters into the black hole {hole.Feeds}/{hole.feedsToOpen}",$"Lure monsters into the black hole {hole.Feeds}/{hole.feedsToOpen}")
                    : ArenaText.Pair("The hidden gate is open: stand on it and press E","The hidden gate is open: stand on it and press E");
                if (!shown) text += ArenaText.Pair(" (the black hole is gone at night)"," (the black hole is gone at night)");
            }
            SetSideQuestText(text);

            bool morning = Stage is CampaignStage.Day or CampaignStage.Lull;
            bool waveOpen = !morning || gm.waves && gm.waves.Phase == WavePhase.WaveActive && !gm.waves.TimeExpired;
            bool near = p && Query.Flat(p.Position - hole.Centre).magnitude <= rules.holeQuestRadius;
            if (holeSeen && shown && daylight && hole.Phase == HolePhase.Active && map.CanFeed && near && waveOpen && InPlay)
            {
                questSpawnClock -= Time.deltaTime;
                if (questSpawnClock <= 0f)
                {
                    questSpawnClock = Mathf.Max(.5f, rules.holeQuestSpawnSeconds);
                    if (questMonsters.Count < rules.holeQuestMaxAlive) SpawnQuestMonster(p, hole);
                }
            }

            if (Stage == CampaignStage.Dusk && questMonsters.Count > 0 && gm.RunPhase == ArenaUiState.Night && Time.timeScale > 0f)
            {
                questIdleClock += Time.unscaledDeltaTime;
                if (questIdleClock >= QuestFightAfter) { questIdleClock = 0f; gm.CampaignSetPhase(ArenaUiState.WaveActive); }
            }
            else questIdleClock = 0f;
        }

        void SpawnQuestMonster(PlayerController p, BlackHole hole)
        {
            var map = questMap;
            Vector3 away = Query.Flat(p.Position - hole.Centre);
            float bearing = away.sqrMagnitude > .01f ? Mathf.Atan2(away.z, away.x) : Random.value * Mathf.PI * 2f;
            float distance = Mathf.Max(2f, rules.holeQuestSpawnDistance);
            const float body = .6f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float spread = attempt < 8 ? QuestSpawnSpreadDegrees : 180f;
                float a = bearing + Random.Range(-spread, spread) * Mathf.Deg2Rad;
                Vector3 at = Query.Flat(p.Position) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * distance;
                if (!map.Contains(at, body + 1f)) continue;
                at = FindOpen(at, body, 3f);
                if (!map.IsWalkable(at, body) || Query.Flat(at - hole.Centre).magnitude < hole.GravityRadius + 2f) continue;
                var enemy = gm.waves.SpawnCampaign(EnemyKind.Grunt, at);
                if (!enemy) return;                                                                        
                enemy.Raider = false;                                                    
                enemy.SuppressRewards = true;
                questMonsters.Add(enemy);
                return;
            }
        }

        void SinkQuestMonsters()
        {
            foreach (var enemy in questMonsters)
                if (enemy && enemy.IsAlive) { enemy.SuppressRewards = true; enemy.OnEnterFallZone(Vector3.zero); }
            questMonsters.Clear();
        }

        bool OnScreen(Vector3 point) => SkillWorldAPI.IsOnScreen?.Invoke(point) ?? false;

        void SetSideQuestText(string text)
        {
            if (sideQuestText == text) return;
            sideQuestText = text;
            if (ui) ui.SetSideQuest(text);
        }
    }
}
