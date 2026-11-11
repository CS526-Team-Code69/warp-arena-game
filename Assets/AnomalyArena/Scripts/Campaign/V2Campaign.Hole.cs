using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalyArena
{
    /// <summary>
    /// The black hole's side quest on the campaign map (since 2026-11-06; a timed damage check since 2026-11-10). The campaign's one black
    /// hole (V2Campaign.Map.cs) stands out by day and in the afternoon and is gone at night. It has no marker at the screen's edge: the
    /// afternoon's ore trail, purple shards on the ground and its hum lead to it (V2Campaign.Guide.cs). The first time the player sees it
    /// by day or in the afternoon, the classic first-sight cutscene plays (WorldMap.TickHoleIntro: three monsters spiral in).
    /// Then the hole is fed first (V2Campaign.HoleFeed.cs): the side quest reads "lure monsters into the black hole", the campaign keeps
    /// its own count of what the hole swallows (BlackHole.countsFeeds stays off, so the hole never opens by feeding alone), and with
    /// rules.holeFeedGoal swallowed the hole wakes for the rest of the run. Until then it takes no hits.
    /// The side quest then reads "break the black hole in time". The first hit the player lands on the hole (any weapon, blast or burn
    /// finds it: BlackHoleTarget) starts rules.holeCheckSeconds, in which its damage must reach the day's requirement (by default half the
    /// Nash Prophet's HP, HoleRequirement). From rules.holeCounterDelay on the Prophet answers through the hole with its own moves, void
    /// pillars round the player and its equilibrium ring out of the hole by turns (Enemy.HoleEcho.cs), more often as the damage grows.
    /// While the check runs the hole shows its damage share and the seconds left (its world label and a bar of blocks over it), and in
    /// the afternoon near the hole the afternoon turns into a fight so the weapons fire. Reached: the hole collapses into the hidden gate,
    /// which stays open until the player goes through; E on it asks first, and yes enters the void trial against the Nash Prophet, whose
    /// win pays the void currencies (V2Campaign.Void.cs) and turns the hole into ground. Not reached in time, or the player falls meanwhile
    /// (in the afternoon they wake at the base; by day a fall ends the run as before): the side quest fails for the day, the hole is put
    /// away until the next morning and its damage starts from zero then.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // How the check shows over the hole (presentation, not balance numbers): a bar of blocks this wide, its damage row and its time
        // row this tall, floating this far above the hole's world label along the view's up; the pillars' column height and width share.
        const float CheckBarWidth = 6f, CheckBarRow = .42f, CheckTimeRow = .14f, CheckBarAboveLabel = 1.6f;
        const float PillarColumnHeight = 9f, PillarColumnShare = .4f;
        // The ring's shots start this far outside the hole's kill core: inside it a shot is swallowed at once (EnemyBolt).
        const float HoleRingMuzzle = .6f;
        static readonly Color CheckBarBack = new(.07f, .03f, .11f), CheckBarDamage = new(.72f, .38f, 1f), CheckBarTime = new(.86f, .86f, .92f);
        static readonly Color PillarFillColour = new(.64f, .3f, 1f);

        WorldMap questMap;
        bool holeSeen, holeFailedToday, holeCheckAnnounced;
        int holeCheckDay = -1;
        readonly HoleCheck holeCheck = new();
        BlackHoleTarget holeTarget;
        Transform holeCheckView, checkBar, checkDamage, checkTime;
        int labelPercent = -1, labelSeconds = -1;
        string sideQuestText;

        /// <summary>For automated checks: whether the player has met the black hole, and the day's damage check as it stands.</summary>
        public bool HoleSeen => holeSeen;
        public HoleCheckState CheckState => holeCheck.State;
        public float CheckDealt => holeCheck.Dealt;
        public float CheckRequired => holeCheck.Required;
        public float CheckSecondsLeft => holeCheck.SecondsLeft;
        public bool HoleFailedToday => holeFailedToday;
        /// <summary>The body the player's attacks hit on the black hole (null once the campaign's map is gone).</summary>
        public BlackHoleTarget HoleTarget => holeTarget;

        /// <summary>SetupCampaignMap: the side quest takes over the map's campaign hooks, and the hole gets its body and its check's view.</summary>
        void BeginHoleQuest(WorldMap map)
        {
            EndHoleQuest();
            questMap = map;
            holeSeen = false;
            ResetHoleCheck();
            ResetHoleFeeding();
            holeCheckDay = -1;
            map.HoleFed -= OnHoleFed;
            map.HoleFed += OnHoleFed;
            map.campaignTick = TickHoleQuest;
            map.campaignHoleIntroAllowed = HoleIntroAllowed;
            map.campaignPortalAllowed = HoleGateAllowed;
            map.campaignConfirmPortal = AskHiddenGate;
            map.HoleIntroFinished += OnHoleIntroFinished;
            map.VoidTrialWon -= OnVoidTrialWon;
            map.VoidTrialWon += OnVoidTrialWon;
            var hole = map.CampaignHole;
            if (!hole) return;
            hole.countsFeeds = false;
            hole.campaignLabel = "";
            ApplyCampaignHolePull(hole);
            var playerStatus = gm && gm.player ? ElementStatus.For(gm.player) : null;
            holeTarget = BlackHoleTarget.Create(hole.transform, hole.KillRadiusNominal, playerStatus ? playerStatus.Rules : null);
            holeTarget.DamageTaken += OnHoleDamaged;
            BuildHoleCheckView();
            V2BlackHolePresentation.AttackProgress = HoleAttackProgress; // the whitebox hole's cubes light up with the check
        }

        /// <summary>For the whitebox hole's cubes (V2BlackHolePresentation): while the check runs, the share of the required damage dealt;
        /// below 0 at any other time and for any other hole.</summary>
        float HoleAttackProgress(BlackHole hole) =>
            questMap && hole && hole == questMap.CampaignHole && holeCheck.State == HoleCheckState.Running ? holeCheck.Progress : -1f;

        /// <summary>RestoreClassicMap: the side quest lets go of the map, the hole counts its feeds again as in the classic run, and the
        /// body, the view and the line under the goal go.</summary>
        void EndHoleQuest()
        {
            if (questMap)
            {
                questMap.ClearCampaignHooks();
                questMap.VoidTrialWon -= OnVoidTrialWon;
                questMap.HoleFed -= OnHoleFed;
                var hole = questMap.CampaignHole;
                if (hole) { hole.countsFeeds = true; hole.campaignLabel = null; }
                RestoreClassicHolePull(hole);
                StopHoleAnswers(questMap);
            }
            if (holeTarget)
            {
                holeTarget.DamageTaken -= OnHoleDamaged;
                Destroy(holeTarget.gameObject);
            }
            holeTarget = null;
            if (holeCheckView) Destroy(holeCheckView.gameObject);
            holeCheckView = checkBar = checkDamage = checkTime = null;
            questMap = null;
            ResetHoleCheck();
            ResetHoleFeeding();
            SetSideQuestText(null);
            if (ui) ui.SetGoalsHidden(false);
            V2BlackHolePresentation.AttackProgress = null;
        }

        /// <summary>The cutscene may start: by day or in the afternoon, in play (no panel, story or question open), the player up.</summary>
        bool HoleIntroAllowed() => Running && Stage is (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk) && InPlay;

        /// <summary>E may take the hidden gate: by day or in the afternoon, in play. CanEnterPortal adds the player's own state.</summary>
        bool HoleGateAllowed() => Running && Stage is (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk) && InPlay;

        bool InPlay => gm && gm.player && gm.player.IsAlive && Time.timeScale > 0f && gm.RunPhase is (ArenaUiState.WaveActive or ArenaUiState.Night);

        void OnHoleIntroFinished()
        {
            holeSeen = true;
            // Fed first: the side quest's line says how (V2Campaign.HoleFeed.cs); the flash only names what was just seen.
            if (!HoleAwake) ui.Flash(ArenaText.Pair("黑洞会吞掉靠近它的怪物", "The black hole swallows the monsters that come near it"), 3f);
            else ui.Flash(ArenaText.Pair($"黑洞里有东西。攻击它：第一次打中后计时 {Mathf.RoundToInt(rules.holeCheckSeconds)} 秒，打出足够的伤害就能把它激怒。",
                $"Something lies within the black hole. Attack it: from the first hit you have {Mathf.RoundToInt(rules.holeCheckSeconds)} s to deal enough damage to enrage it."), 4.5f);
        }

        /// <summary>E on the hidden gate: the question before the void trial. A no leaves the gate open; a yes enters on the next frame
        /// in play (WorldMap.TickCampaignWorld).</summary>
        void AskHiddenGate(BlackHole hole, System.Action yes, System.Action no)
        {
            if (!ui) { yes?.Invoke(); return; }
            bool morning = Stage is CampaignStage.Day or CampaignStage.Lull;
            string body = ArenaText.Pair("接下来可能遇到非常强力的敌人，确定要进入吗？", "A very powerful enemy may be waiting in there. Do you want to go in?") + "\n\n"
                + (morning ? ArenaText.Pair("在里面倒下会结束本局。", "Dying in there ends the run.")
                    : ArenaText.Pair("在里面倒下会回到基地醒来，什么都不会丢，下午继续。", "Dying in there wakes you at the base with nothing lost, and the afternoon goes on."));
            ui.Confirm(ArenaText.Pair("虚空传送门", "THE VOID PORTAL"), body, yes, no);
        }

        /// <summary>The void trial is won (WorldMap.CompleteTrial): with the player standing, the gift of the void (V2Campaign.Void.cs),
        /// and the void alloy will grow where the hole stood.</summary>
        void OnVoidTrialWon()
        {
            var map = questMap;
            if (!Running || !map || !map.TrialWon || !gm || !gm.player || !gm.player.IsAlive) return;
            var hole = map.CampaignHole;
            OnProphetBeaten(hole ? hole.Centre : trailTarget ?? BaseCentre);
        }

        /// <summary>Every frame while the campaign's map is in place (WorldMap.TickCampaignWorld, also while paused or in the trial).</summary>
        void TickHoleQuest()
        {
            var map = questMap;
            if (!Running || !map || !map.CampaignMapApplied || !gm) return;
            var p = gm.player;
            TickVoid(); // the void currencies' wallet and the void-enchanted weapons (V2Campaign.Void.cs)
            if (ui) ui.SetGoalsHidden(map.TrialActive); // the goal and the side quest speak of the map the trial has left

            // A fall in the void trial at a time the campaign catches deaths (the afternoon): the trial ends without a win, so the
            // campaign's death rule wakes the player at the base (a death in the morning ends the run as in the classic one).
            if (map.TrialActive)
            {
                if (p && !p.IsAlive && CatchesPlayerDeath()) gm.CampaignAbortVoidTrial();
                ShowHoleTarget(false);
                ShowCheckBar(null);
                DrawHolePillars(map); // the trial's own pillars are its marks' (V2ProphetTrialMarks); none of the map's show meanwhile
                TickHoleGuide(map); // the hum falls silent in the trial
                return;
            }

            // Each day the check starts over: a hole put away after a failed check comes back the next morning, its damage from zero.
            if (holeCheckDay != Day)
            {
                holeCheckDay = Day;
                ResetHoleCheck();
                StopHoleAnswers(map);
            }

            // Night (and Day 0: the tutorial never leads there, and a fall into it on the tutorial's walk would end the run) puts the hole
            // away, and so does a failed check for the rest of its day (it follows once a trial is over).
            bool evening = Stage == CampaignStage.Night || Stage == CampaignStage.Base && baseVisitFrom == CampaignStage.Night;
            map.SetCampaignHoleHidden(evening || Day <= 0 || holeFailedToday);
            TickHoleGuide(map); // its shards and its hum follow it (V2Campaign.Guide.cs)

            var hole = map.CampaignHole;
            if (!hole || hole.Phase == HolePhase.Gone || map.TrialWon)
            {
                SetSideQuestText(null);
                ShowHoleTarget(false);
                ShowCheckBar(null);
                DrawHolePillars(map);
                return;
            }

            bool daylight = Stage is CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk;
            bool shown = !map.CampaignHoleHidden;
            // Met already open (the check passed before the cutscene could play): no cutscene, the quest line shows the gate at once.
            if (!holeSeen && (map.HoleIntroDone || shown && daylight && hole.Phase != HolePhase.Active && OnScreen(hole.Centre))) holeSeen = true;

            // The check counts while the hole is out (by day, in the afternoon or during a base visit from it), still a hole, and the
            // day's check is not over; it takes hits only by day or in the afternoon, outside the cutscene.
            bool holeOut = shown && hole.Phase == HolePhase.Active && Day > 0;
            // Fed first (V2Campaign.HoleFeed.cs): until it is awake the hole has no check and takes no hits.
            TickHoleFeeding(map, hole, p, holeOut && daylight);
            bool counting = holeOut && HoleAwake && !holeFailedToday && holeCheck.State is HoleCheckState.Idle or HoleCheckState.Running;
            if (holeCheck.State == HoleCheckState.Passed && hole.Phase == HolePhase.Active) PassHoleCheck(map, hole);
            else if (holeCheck.State == HoleCheckState.Running) TickHoleCheck(map, hole, p, holeOut);
            ShowHoleTarget(counting && daylight && !map.HoleIntroPlaying && holeCheck.State is HoleCheckState.Idle or HoleCheckState.Running);
            TickHoleAnswers(map, p);

            // The afternoon holds no fight until something calls for one (its own phase stops the weapons): near the hole, or while its
            // check runs, it turns into one so the hole can be attacked, and stays one until nightfall.
            if (Stage == CampaignStage.Dusk && counting && !AfternoonCombat && InPlay && p)
            {
                bool near = Query.Flat(p.Position - hole.Centre).magnitude <= rules.holeFightRadius;
                if (near || holeCheck.State == HoleCheckState.Running) AfternoonCombat = true;
            }

            SetSideQuestText(HoleQuestLine(hole, shown));
            UpdateHoleLabel(hole);
            ShowCheckBar(holeCheck.State == HoleCheckState.Running && shown ? hole : null);
        }

        /// <summary>A hit on the hole's body (BlackHoleTarget.DamageTaken): only the player's own damage counts, hits, blasts and burns
        /// alike. The first starts the clock with the day's requirement; the frame's tick settles a pass (the hit may land in the middle
        /// of a weapon's sweep over every body).</summary>
        void OnHoleDamaged(DamageInfo info, float amount)
        {
            var map = questMap;
            if (!Running || !map || amount <= 0f || info.dealer == null || info.dealer.Owner is not PlayerController) return;
            var hole = map.CampaignHole;
            if (!hole || hole.Phase != HolePhase.Active || holeFailedToday || !HoleAwake) return;
            bool starting = holeCheck.State == HoleCheckState.Idle;
            if (starting) ApplyCheckTiming();
            holeCheck.Hit(amount, starting ? HoleRequirement() : holeCheck.Required, rules.holeCheckSeconds);
            if (starting) CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneHoleCheck, CampaignEvents.HoleCheckStart, holeCheck.Progress, Day);
        }

        /// <summary>The running check, each frame: a fall or the hole put away fails it; in play its clock runs and the Prophet's answers
        /// come due. The clock waits while the game holds still (a question, the base panel, the pause, a cutscene).</summary>
        void TickHoleCheck(WorldMap map, BlackHole hole, PlayerController p, bool holeOut)
        {
            if (!holeCheckAnnounced)
            {
                holeCheckAnnounced = true;
                ui.Flash(ArenaText.Pair($"计时开始：{Mathf.RoundToInt(holeCheck.Seconds)} 秒内打出足够的伤害", $"The clock is running: deal enough damage within {Mathf.RoundToInt(holeCheck.Seconds)} s"), 2.5f);
                V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, hole.Centre);
            }
            if (!p || !p.IsAlive || !holeOut)
            {
                FailHoleCheck(map, false);
                CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneHoleCheck, !p || !p.IsAlive ? CampaignEvents.HoleCheckFailedDeath : CampaignEvents.HoleCheckFailedNight, holeCheck.Progress, Day);
                return;
            }
            if (!InPlay || Stage is not (CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk)) return;
            var move = holeCheck.Tick(Time.deltaTime);
            if (holeCheck.State == HoleCheckState.Failed)
            {
                FailHoleCheck(map, true);
                CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneHoleCheck, CampaignEvents.HoleCheckFailedTimeout, holeCheck.Progress, Day);
                return;
            }
            if (move == HoleCounterMove.Pillars) AnswerWithPillars(map, hole, p);
            else if (move == HoleCounterMove.Ring && map.hollow) map.hollow.EchoRing(hole.Centre, hole.KillRadiusNominal + HoleRingMuzzle, p);
        }

        /// <summary>The damage reached the requirement in time: the Prophet's answers stop and the hole collapses into the hidden gate.</summary>
        void PassHoleCheck(WorldMap map, BlackHole hole)
        {
            StopHoleAnswers(map);
            hole.campaignLabel = "";
            hole.OpenGate(gm.waves ? gm.waves.WaveIndex + 1 : 0);
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneHoleCheck, CampaignEvents.HoleCheckPassed, holeCheck.Progress, Day);
            ui.Flash(ArenaText.Pair("里面的东西被激怒了：黑洞变成了一座传送门", "What lies within is enraged: the black hole turns into a portal"), 3f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PortalOpen, hole.Centre);
        }

        /// <summary>The day's check fails (the time ran out, the player fell, or the hole was put away): the answers stop and the hole is
        /// put away until the next morning. Only the time running out says so in a flash; a fall has its own.</summary>
        void FailHoleCheck(WorldMap map, bool timeUp)
        {
            holeCheck.Fail();
            holeFailedToday = true;
            StopHoleAnswers(map);
            var hole = map.CampaignHole;
            if (hole)
            {
                hole.campaignLabel = "";
                Fx.Ring(hole.Centre, hole.GravityRadius, PillarFillColour, .6f);
            }
            if (timeUp) ui.Flash(ArenaText.Pair("今天没能激怒它，明天再来", "It was not enraged today. Come back tomorrow."), 3.5f);
        }

        void ResetHoleCheck()
        {
            holeCheck.Reset();
            ApplyCheckTiming();
            holeFailedToday = holeCheckAnnounced = false;
            labelPercent = labelSeconds = -1;
        }

        void ApplyCheckTiming()
        {
            if (!rules) return;
            holeCheck.CounterDelay = rules.holeCounterDelay;
            holeCheck.IntervalStart = rules.holeCounterIntervalStart;
            holeCheck.IntervalEnd = rules.holeCounterIntervalEnd;
        }

        /// <summary>The damage the day's check asks for: rules.holeCheckDamage, or by default rules.holeCheckProphetShare of the Nash
        /// Prophet's HP as the void trial gives it (since 2026-11-10 the campaign's own, rules.prophetHp, through the same rule as
        /// Enemy.CampaignBudget: V2Campaign.Days.cs).</summary>
        float HoleRequirement() =>
            CampaignRules.HoleCheckRequirement(rules.holeCheckDamage, rules.holeCheckProphetShare, rules.BodyHp(EnemyKind.Hollow, Day, false, 0f, 1f));

        /// <summary>The Prophet's pillars: the first on the player, the others round them with gaps to step through, more of them as the
        /// damage grows.</summary>
        void AnswerWithPillars(WorldMap map, BlackHole hole, PlayerController p)
        {
            var prophet = map.hollow;
            if (!prophet) return;
            int count = CampaignRules.HolePillarCount(rules.holePillarsMin, rules.holePillarsMax, holeCheck.Progress);
            var spots = CampaignRules.HolePillarSpots(p.Position, count, rules.holePillarRadius * rules.holePillarSpread, Random.value * 360f);
            prophet.EchoPillars(spots, rules.holePillarRadius, rules.holePillarWarning, rules.holePillarLife, hole.Centre);
        }

        /// <summary>Its pillars age in play (a standing one kills a player inside it) and are drawn on the ground.</summary>
        void TickHoleAnswers(WorldMap map, PlayerController p)
        {
            var prophet = map.hollow;
            if (prophet && prophet.VoidPillars.Count > 0 && InPlay) prophet.TickEcho(gm.WorldDeltaTime, p);
            DrawHolePillars(map);
        }

        /// <summary>The answers end with the check: its pillars go (shots in flight fly on). The trial's own pillars are never touched.</summary>
        void StopHoleAnswers(WorldMap map)
        {
            if (map && map.hollow && !map.TrialActive) map.hollow.ClearEcho();
            DrawHolePillars(map);
        }

        void ShowHoleTarget(bool on)
        {
            if (holeTarget) holeTarget.Show(on);
        }

        /// <summary>The line under the goal: nothing before the hole is met; then the timed check, how the day went, or the open gate.</summary>
        string HoleQuestLine(BlackHole hole, bool shown)
        {
            if (!holeSeen) return null;
            // The open portal needs no instruction (2026-11-10): the line only says it is open; standing on it shows the E (V2WorldLabels).
            bool portal = hole.Phase != HolePhase.Active;
            string text = portal ? ArenaText.Pair("虚空传送门已打开", "The void portal is open")
                : !HoleAwake ? FeedQuestLine()
                : holeFailedToday ? ArenaText.Pair("限时击破黑洞：今天没能激怒它，明天再来", "Break the black hole in time: not enraged today, come back tomorrow")
                : ArenaText.Pair("限时击破黑洞", "Break the black hole in time");
            if (!shown && !holeFailedToday)
                text += portal ? ArenaText.Pair("（夜里不出现）", " (gone at night)") : ArenaText.Pair("（黑洞夜里不出现）", " (the black hole is gone at night)");
            return text;
        }

        /// <summary>The hole's world label (V2WorldLabels reads BlackHole.campaignLabel): the damage share and the seconds left while the
        /// check runs, nothing otherwise (no count of swallowed monsters any more). Written only when a shown number changes.</summary>
        void UpdateHoleLabel(BlackHole hole)
        {
            if (holeCheck.State != HoleCheckState.Running)
            {
                labelPercent = labelSeconds = -1;
                // While it is fed the hole shows its count; awake and at rest, nothing.
                string resting = HoleAwake ? "" : FeedLabel();
                if (hole.campaignLabel != resting) hole.campaignLabel = resting;
                return;
            }
            int percent = Mathf.FloorToInt(holeCheck.Progress * 100f), seconds = Mathf.CeilToInt(holeCheck.SecondsLeft);
            if (percent == labelPercent && seconds == labelSeconds) return;
            labelPercent = percent; labelSeconds = seconds;
            hole.campaignLabel = ArenaText.Pair($"伤害 {percent}% · 剩 {seconds} 秒", $"DAMAGE {percent}% · {seconds} S LEFT");
        }

        // ───── The check's view: a bar of blocks over the hole, the pillars on the ground ─────

        void BuildHoleCheckView()
        {
            holeCheckView = new GameObject("V2 Hole Check").transform;
            checkBar = new GameObject("Bar").transform;
            checkBar.SetParent(holeCheckView, false);
            float height = CheckBarRow + CheckTimeRow + .3f;
            Flat(Box(checkBar, "Back", Vector3.zero, new Vector3(CheckBarWidth + .24f, height, .04f), CheckBarBack));
            checkDamage = Flat(Box(checkBar, "Damage", new Vector3(0f, height * .5f - .1f - CheckBarRow * .5f, -.04f), new Vector3(CheckBarWidth, CheckBarRow, .04f), CheckBarDamage)).transform;
            checkTime = Flat(Box(checkBar, "Time", new Vector3(0f, -height * .5f + .1f + CheckTimeRow * .5f, -.04f), new Vector3(CheckBarWidth, CheckTimeRow, .04f), CheckBarTime)).transform;
            checkBar.gameObject.SetActive(false);
        }

        /// <summary>The bar over hole while its check runs (null hides it): the damage share on top, the time left under it, turned to
        /// the view and floating above the hole's world label.</summary>
        void ShowCheckBar(BlackHole hole)
        {
            if (!checkBar) return;
            bool on = hole;
            if (checkBar.gameObject.activeSelf != on) checkBar.gameObject.SetActive(on);
            if (!on) return;
            var cam = gm.cam;
            Vector3 up = cam ? cam.transform.up : Vector3.up;
            checkBar.position = hole.Centre + up * (hole.size * .5f + 1f + CheckBarAboveLabel);
            if (cam) checkBar.rotation = cam.transform.rotation;
            SetBarShare(checkDamage, holeCheck.Progress);
            SetBarShare(checkTime, holeCheck.Seconds > 0f ? holeCheck.SecondsLeft / holeCheck.Seconds : 0f);
        }

        static void SetBarShare(Transform part, float share)
        {
            share = Mathf.Clamp01(share);
            part.gameObject.SetActive(share > .001f);
            var scale = part.localScale;
            part.localScale = new Vector3(CheckBarWidth * Mathf.Max(.001f, share), scale.y, scale.z);
            var at = part.localPosition;
            part.localPosition = new Vector3(-CheckBarWidth * .5f + CheckBarWidth * share * .5f, at.y, at.z);
        }

        // The Prophet's answered pillars are drawn by one V2VoidPillarFx under the check's view: made with the first pillars, gone
        // with that view when the quest lets go of the map.
        V2VoidPillarFx holePillars;

        /// <summary>The Prophet's answered pillars, outside the trial: each one's circle, clock, strike and standing column
        /// (V2VoidPillarFx), with the campaign's own column height and core width. A pillar that is over thins out. In the trial
        /// none of the map's show (the trial's own are its marks'): they go at once.</summary>
        void DrawHolePillars(WorldMap map)
        {
            var pillars = map && !map.TrialActive && map.hollow ? map.hollow.VoidPillars : null;
            if (!holePillars)
            {
                if (pillars == null || pillars.Count == 0 || !holeCheckView) return;
                holePillars = V2VoidPillarFx.Create(holeCheckView, "HolePillars");
                holePillars.columnHeight = PillarColumnHeight;
                holePillars.coreShare = PillarColumnShare;
            }

            if (pillars == null) holePillars.Clear();
            else holePillars.Draw(pillars);
        }

        static Renderer Flat(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
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
