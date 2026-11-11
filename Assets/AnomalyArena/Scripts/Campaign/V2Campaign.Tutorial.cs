using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Day 0 as a forced spotlight tutorial (implementation spec 2026-11-10, section A; the V2 doc asks that every control be taught with a
    /// dark screen and a spotlight, moving on only once the player has done it and seen it work). From the crash site west of the base the
    /// player is taught, one step at a time: 1 walk 4 cells toward the marker, 2 fire three pistol shots anywhere, 3 hold E at the glowing
    /// ore, 4 open the weapon crate with E and take one of three (it goes into the main hand, the pistol stays in the other), 5 sprint once,
    /// 6 kill the first four monsters, 7 pick up the alien energy they drop. On the way home lie a second ore (not forced, to practise), a
    /// second weapon crate (not forced; its weapon replaces the pistol) and purple-black void crystals that cannot be mined. Then the base
    /// as before (Arrival, V2Campaign.Story.cs). The tutorial night's two steps, "F sets a lamp" and "mine dark matter in its light", are
    /// forced the same way.
    /// A step at a thing (the ore, the crate, the dark matter) is walked to with the goal's marker first and turns into its spotlight once
    /// the player is close (CampaignRules.tutorialTeachReach). While a spotlight is up only that step's input works (SpotlightTutorial,
    /// PlayerController.InputGate); between steps everything taught so far works. Time never stops, and no monster is out before step 6.
    /// When a step is done its circle flashes green with a sound and the dim fades; the next step starts once that is over.
    /// </summary>
    public sealed partial class V2Campaign
    {
        TutorialStep tutStep, tutLastDone;
        // The step's spotlight is up and holds every other input back.
        bool tutForced;
        // What comes after the step just passed: it starts once the spotlight's green flash and fade are over.
        System.Action tutNext;
        Vector3 tutStart, tutMarker, tutCluster;
        Node tutOre, tutSecondOre;
        Crate tutFirstCrate, tutSecondCrate;
        readonly List<Enemy> tutGrunts = new();
        string tutorialHint = "";
        bool tutPickupSaid, tutHomeSaid, voidLineSaid, secondCrateHint;
        int tutShots;
        float tutCooldown, tutSprinted;
        float tutStepAt; // when the step being taught began (Time.unscaledTime), for the tutorial_step analytics row
        // The way home after the first fight: past the second ore, past the second crate, to the door.
        enum HomeLeg { Ore, Crate, Door }
        HomeLeg homeLeg;
        readonly List<Transform> voidCrystals = new();
        Vector3 voidCrystalsAt;

        /// <summary>For automated checks: the tutorial step being taught or walked to, and whether its spotlight holds the rest back.</summary>
        public TutorialStep TutorialStepNow => tutStep;
        public bool TutorialStepForced => tutForced;

        void BeginTutorial()
        {
            Stage = CampaignStage.Tutorial;
            tutStep = tutLastDone = TutorialStep.None;
            tutForced = false; tutNext = null;
            tutPickupSaid = tutHomeSaid = voidLineSaid = secondCrateHint = changedWeaponSaid = false;
            cratesOpened = 0; switchTaught = skillTaught = skillTeachPending = false; lateHint = LateHint.None;
            tutGrunts.Clear(); voidCrystals.Clear(); crates.Clear();
            tutOre = tutSecondOre = null; tutFirstCrate = tutSecondCrate = null;
            PlayerController.InputGate = TutorialLets;
            if (!crateLoopRunning) { crateLoopRunning = true; StartCoroutine(CrateLoop()); }
            // Crash debris at the start: a few dark scorched slabs (whitebox stand-in for the crash site props).
            for (int i = 0; i < 5; i++)
                Box(worldRoot, "CrashDebris", CrashSite + new Vector3(Random.Range(-4f, 3f), .2f, Random.Range(-5f, 5f)), new Vector3(Random.Range(.8f, 2f), .4f, Random.Range(.6f, 1.6f)), new Color(.18f, .16f, .15f))
                    .transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            Say(Line.T01);
            TeachMove();
        }

        /// <summary>PlayerController.InputGate while a campaign runs: on Day 0 (its walk and its night) only what the tutorial lets through.</summary>
        bool TutorialLets(PlayerAct act)
        {
            if (!this ||
                !Running ||
                Active != this ||
                Day != 0 ||
                Stage is not (CampaignStage.Tutorial or CampaignStage.Night))
            {
                return true;
            }

            // This step now teaches secondary fire.
            if (tutForced && tutStep == TutorialStep.Switch)
            {
                if (act == PlayerAct.Fire) return true;
                if (act == PlayerAct.Switch) return false;
            }

            return SpotlightTutorial.Lets(
                act,
                tutForced ? tutStep : TutorialStep.None,
                tutLastDone);
        }

        Vector3 PlayerPoint() => gm && gm.player ? gm.player.Position : BaseCentre;
        static float Apart(Vector3 a, Vector3 b) => Query.Flat(a - b).magnitude;

        // ───── Teaching a step ─────

        /// <summary>Puts a step's spotlight up: the bright circle on what at returns, the tip "[key] phrase", every other input held back.</summary>
        void Teach(TutorialStep step, System.Func<Vector3> at, float radius, string key, string phrase)
        {
            if (step != tutStep) tutStepAt = Time.unscaledTime;
            tutStep = step; tutForced = true;
            ui.ShowSpotlight(at, radius, key, phrase);
            tutorialHint = WorkingKeys();
        }

        /// <summary>A step to walk to first (no spotlight yet): the goal and its marker.</summary>
        void Approach(TutorialStep step, string goal, Vector3? marker)
        {
            if (step != tutStep) tutStepAt = Time.unscaledTime;
            tutStep = step; tutForced = false;
            ui.HideSpotlight();
            Objective(goal, marker);
            tutorialHint = WorkingKeys();
        }

        /// <summary>The step is done: the circle flashes green with a sound and fades; then next starts (RunNextStep).</summary>
        void PassStep(System.Action next)
        {
            tutLastDone = tutStep; tutForced = false;
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneTutorialStep, tutStep.ToString(), Time.unscaledTime - tutStepAt, Day);
            ui.PassSpotlight();
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, PlayerPoint(), 1.3f, .75f);
            tutNext = next;
            tutorialHint = WorkingKeys();
        }

        void RunNextStep()
        {
            if (tutNext == null || ui.SpotlightBusy) return;
            var next = tutNext;
            tutNext = null;
            next();
        }

        /// <summary>The HUD's hint line on Day 0: between steps, the keys learned so far. While a step's spotlight is up the line is
        /// left to the weapon's name, because the spotlight's tip already shows that step's key (since 2026-11-10).</summary>
        string WorkingKeys()
        {
            if (tutForced) return "";
            var works = SpotlightTutorial.Taught(tutLastDone);
            // In sentence case since 2026-11-10 (the note stands in the goal panel, not on the HUD's capital-letter line); the right button
            // joins once it was taught (the second crate opened), though it works from the first fight on.
            return Dot((works & PlayerAct.Move) != 0 ? ArenaText.Pair("WASD 移动", "WASD move") : "",
                (works & PlayerAct.Fire) != 0 ? ArenaText.Pair("左键开火", "Left click fire") : "",
                (works & PlayerAct.Sprint) != 0 ? ArenaText.Pair("Shift 冲刺", "Shift sprint") : "",
                (works & PlayerAct.Switch) != 0 && switchTaught ? ArenaText.Pair("右键换手", "Right click fire") : "");
        }

        static string MouseKey => ArenaText.Pair("左键", "LMB");

        // ───── Day 0's steps ─────

        void TeachMove()
        {
            tutStart = Query.Flat(PlayerPoint());
            tutMarker = FindOpen(CrashSite + new Vector3(14f, 0f, 0f), 1f);
            Objective(ArenaText.Pair($"朝蓝色标记走 {rules.tutorialMoveMeters:0} 米", $"Walk {rules.tutorialMoveMeters:0} m toward the blue marker"), tutMarker);
            Teach(TutorialStep.Move, PlayerPoint, 3f, "WASD", ArenaText.Pair("向标记移动", "Walk to marker"));
        }

        void TeachFire()
        {
            tutShots = 0;
            tutCooldown = gm.runWeapons && gm.runWeapons.Active != null ? gm.runWeapons.Active.cooldown : 0f;
            Objective(ArenaText.Pair($"用小手枪开 {rules.tutorialShots} 枪，朝哪打都行", $"Fire the pistol {rules.tutorialShots} times, anywhere"), null);
            Teach(TutorialStep.Fire, PlayerPoint, 3f, MouseKey, ArenaText.Pair("按住开火", "Hold to fire"));
        }

        void ApproachOre()
        {
            Vector3 corner = FindOpen(TutorialCorner, 1.4f);
            tutOre = AddNode(NodeKind.Ore, corner, DayZeroOre(0)); // Day 0's alloy (V2Campaign.Days.cs, since 2026-11-10)
            if (tutOre.view) tutOre.view.localScale *= 1.4f;
            Approach(TutorialStep.Mine, ArenaText.Pair("去角落里发光的地方看看", "Check the glow in the corner"), corner);
            Say(Line.T07);
        }

        void ApproachCrate()
        {
            Vector3 at = FindOpen(Vector3.Lerp(TutorialCorner, DoorPoint, .2f), 1.2f);
            tutFirstCrate = PlaceCrate(at, CrateKind.TutorialFirst);
            Approach(TutorialStep.Crate, ArenaText.Pair("前面有个武器箱，过去看看", "There is a weapon crate ahead: take a look"), at);
            SayWhenQuiet(Line.T08, () => Stage == CampaignStage.Tutorial);
        }

        void TeachSprint()
        {
            tutSprinted = 0f;
            Objective(ArenaText.Pair("边走边冲刺一次", "Sprint once while walking"), null);
            Teach(TutorialStep.Sprint, PlayerPoint, 3.5f, "Shift", ArenaText.Pair("冲刺一次", "Sprint once"));
        }

        void TeachFight()
        {
            Vector3 from = PlayerPoint(), way = Query.Flat(DoorPoint - from);
            Vector3 ahead = way.sqrMagnitude > 1f ? way.normalized : Vector3.right;
            SpawnTutorialGrunts(from + ahead * rules.tutorialFightDistance);
            Objective(ArenaText.Pair($"用刚拿到的武器消灭 {tutGrunts.Count} 只怪物", $"Defeat the {tutGrunts.Count} monsters with your new weapon"), null);
            SayWhenQuiet(Line.T04, () => Stage == CampaignStage.Tutorial);
            Teach(TutorialStep.Fight, PlayerPoint, 9f, MouseKey, ArenaText.Pair("消灭怪物", "Defeat the monsters"));
        }

        void TeachCollect()
        {
            Objective(ArenaText.Pair("走过去，捡起怪物掉落的异星能量", "Walk over the alien energy the monsters dropped"), null);
            Teach(TutorialStep.Collect, NearestPickup, 2.6f, "WASD", ArenaText.Pair("捡起能量", "Collect the energy"));
        }

        Vector3 NearestPickup()
        {
            Vector3 from = PlayerPoint(), best = from;
            float bestDistance = float.MaxValue;
            foreach (var pickup in pickups)
            {
                float d = Query.Flat(pickup.position - from).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = pickup.position; }
            }
            return best;
        }

        /// <summary>After the first fight the way home holds a second ore, a second weapon crate and the void crystals, in that order along
        /// the line to the base door, a little to either side of it.</summary>
        void BeginHomeward()
        {
            Vector3 from = Query.Flat(PlayerPoint()), way = Query.Flat(DoorPoint - from);
            Vector3 dir = way.sqrMagnitude > 1f ? way.normalized : Vector3.right, side = new(-dir.z, 0f, dir.x);
            tutSecondOre = AddNode(NodeKind.Ore, FindOpen(from + way * .35f + side * 2.5f, 1.2f), DayZeroOre(1));
            tutSecondCrate = PlaceCrate(FindOpen(from + way * .55f - side * 2f, 1.2f), CrateKind.TutorialSecond);
            PlaceVoidCrystals(FindOpen(from + way * .74f + side * 4f, 1.2f));
            tutStep = TutorialStep.SecondCrate; tutForced = false;
            ui.HideSpotlight();
            homeLeg = HomeLeg.Ore;
            HomeObjective();
        }

        void HomeObjective()
        {
            switch (homeLeg)
            {
                case HomeLeg.Ore:
                    Objective(ArenaText.Pair("回基地。路边还有一处合金矿，可以自己采", "Head to the base. There is more ore by the way: mine it yourself"), tutSecondOre?.position);
                    break;
                case HomeLeg.Crate:
                    Objective(ArenaText.Pair("路边有第二个武器箱：选中的会换下小手枪，也可以不拿", "A second weapon crate by the way: your pick replaces the pistol, or leave it"), tutSecondCrate?.position);
                    break;
                default:
                    Objective(ArenaText.Pair("回到基地门口", "Reach the base door"), DoorPoint);
                    break;
            }
        }

        void TickTutorial(float dt)
        {
            prompt = "";
            TickPickups(dt);
            var p = gm.player;
            if (!p || !p.IsAlive) return;
            RunNextStep();
            Vector3 pp = p.Position;
            switch (tutStep)
            {
                case TutorialStep.Move:
                    if (tutForced && SpotlightTutorial.MovedToward(tutStart, pp, tutMarker) >= rules.tutorialMoveMeters) PassStep(TeachFire);
                    break;
                case TutorialStep.Fire:
                    if (!tutForced) break;
                    var weapon = gm.runWeapons ? gm.runWeapons.Active : null;
                    float cooldown = weapon != null ? weapon.cooldown : 0f;
                    if (weapon != null && SpotlightTutorial.Fired(tutCooldown, cooldown)) tutShots++;
                    tutCooldown = cooldown;
                    if (tutShots >= rules.tutorialShots) PassStep(ApproachOre);
                    break;
                case TutorialStep.Mine:
                    if (tutOre == null || tutOre.mined) break;
                    // "Something's glowing in that corner": the ore sparkles until it is mined.
                    if (Random.value < dt * 8f)
                        Fx.Pop(tutOre.position + new Vector3(Random.Range(-.6f, .6f), Random.Range(.6f, 1.8f), Random.Range(-.6f, .6f)), new Color(.6f, .9f, 1f), Random.Range(.5f, .9f));
                    if (tutForced) TickMining(dt, true);
                    else if (Apart(pp, tutOre.position) < rules.tutorialTeachReach)
                    {
                        Vector3 ore = tutOre.position;
                        Objective(ArenaText.Pair("站到矿石旁，把它采完", "Stand by the ore and mine it out"), ore);
                        Teach(TutorialStep.Mine, () => ore, 2.6f, "E", ArenaText.Pair("按住采矿", "Hold to mine"));
                    }
                    break;
                case TutorialStep.Crate:
                    if (tutFirstCrate == null || crateOpen != null) break;
                    if (!tutForced)
                    {
                        if (Apart(pp, tutFirstCrate.position) >= rules.tutorialTeachReach) break;
                        Vector3 at = tutFirstCrate.position;
                        Objective(ArenaText.Pair("站到武器箱旁，打开它", "Stand by the crate and open it"), at);
                        Teach(TutorialStep.Crate, () => at, 2.6f, "E", ArenaText.Pair("打开武器箱", "Open the crate"));
                    }
                    else if (CrateUse(tutFirstCrate, out string text))
                    {
                        var crate = tutFirstCrate;
                        tutFirstCrate = null;
                        OpenCrate(crate, _ => PassStep(TeachSprint));
                    }
                    else prompt = text;
                    break;
                case TutorialStep.Sprint:
                    if (!tutForced) break;
                    if (p.stamina.Sprinting) tutSprinted += dt;
                    if (tutSprinted >= rules.tutorialSprintSeconds) PassStep(TeachFight);
                    break;
                case TutorialStep.Fight:
                    tutGrunts.RemoveAll(e => !e || !e.IsAlive);
                    if (tutForced && tutGrunts.Count == 0) PassStep(TeachCollect);
                    break;
                case TutorialStep.Collect:
                    if (tutForced && pickups.Count == 0) PassStep(BeginHomeward);
                    break;
                case TutorialStep.Switch:
                {
                if (!tutForced) break;

                var weapons = gm.runWeapons;

                if (!weapons ||
                    weapons.Hand == null ||
                    weapons.Hand.Length < 2 ||
                    weapons.Hand[1] == null)
                {
                    break;
                }

                var secondary = weapons.Hand[1];
                float secondaryCooldown = secondary.cooldown;

                bool fired = SpotlightTutorial.Fired(
                    tutCooldown,
                    secondaryCooldown);

                tutCooldown = secondaryCooldown;

                if (fired)
                    PassStep(ResumeHomeward);

                break;
            }
                case TutorialStep.SecondCrate:
                    if (TickHomeward(dt, pp)) return;
                    break;
            }
            TickVoidCrystals(pp);
        }

        /// <summary>The way home: the second ore and the second crate may be used or passed by; the door ends Day 0's walk. True once the
        /// walk is over (the arrival has begun).</summary>
        bool TickHomeward(float dt, Vector3 pp)
        {
            TickMining(dt, true);
            float home = Apart(pp, DoorPoint);
            // A thing counts as passed once the player is clearly nearer the door than it is.
            bool Passed(Vector3 thing) => home < Apart(thing, DoorPoint) - 3f;
            if (homeLeg == HomeLeg.Ore && (tutSecondOre == null || tutSecondOre.mined || Passed(tutSecondOre.position))) { homeLeg = HomeLeg.Crate; HomeObjective(); }
            var crate = tutSecondCrate;
            if (crate != null && crateOpen == null)
            {
                // Not forced: near the crate its key shows in a circle without the dim.
                bool near = Apart(pp, crate.position) < rules.tutorialTeachReach;
                if (near != secondCrateHint)
                {
                    secondCrateHint = near;
                    Vector3 at = crate.position;
                    if (near) ui.ShowSpotlight(() => at, 2.6f, "E", ArenaText.Pair("打开武器箱", "Open the crate"), false);
                    else ui.HideSpotlight();
                }
                if (CrateUse(crate, out string text))
                {
                    tutSecondCrate = null;
                    secondCrateHint = false;
                    ui.HideSpotlight();
                    OpenCrate(crate, _ => { homeLeg = HomeLeg.Door; HomeObjective(); });
                }
                else if (!string.IsNullOrEmpty(text)) prompt = text;
            }
            if (homeLeg == HomeLeg.Crate && (tutSecondCrate == null && crateOpen == null || crate != null && Passed(crate.position)))
            {
                homeLeg = HomeLeg.Door;
                if (secondCrateHint) { secondCrateHint = false; ui.HideSpotlight(); }
                HomeObjective();
            }
            if (!tutHomeSaid && Apart(pp, BaseCentre) < 32f) { tutHomeSaid = true; SayWhenQuiet(Line.T06, () => Stage == CampaignStage.Tutorial); }
            if (home >= 3.2f || crateOpen != null) return false;
            tutStep = TutorialStep.Done;
            StartCoroutine(Arrival());
            return true;
        }

        void SpawnTutorialGrunts(Vector3 centre)
        {
            tutGrunts.Clear();
            int count = rules.tutorialFightGrunts;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = FindOpen(centre + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * 3.5f, .6f);
                var enemy = gm.waves.SpawnCampaign(EnemyKind.Grunt, at);
                if (enemy) tutGrunts.Add(enemy);
            }
        }

        /// <summary>CompleteMining's report (V2Campaign.Night.cs): the taught ore on Day 0, the tutorial night's dark matter.</summary>
        void OnTutorialMined(NodeKind kind)
        {
            if (Stage == CampaignStage.Tutorial && tutStep == TutorialStep.Mine && tutForced && kind == NodeKind.Ore)
            {
                Say(Line.T03);
                PassStep(ApproachCrate);
            }
            if (Day == 0 && Stage == CampaignStage.Night && kind == NodeKind.Dark)
            {
                tutDarkMined++;
                if (tutStep == TutorialStep.DarkMatter && tutForced)
                    PassStep(() =>
                    {
                        tutStep = TutorialStep.Done;
                        if (tutNightStep < 2) Objective(ArenaText.Pair("把这一片暗物质采完", "Mine the rest of this dark matter"), null);
                    });
                if (tutDarkMined >= rules.nodesPerCluster && tutNightStep < 2) { tutNightStep = 2; Say(Line.N03); TutorialDawnObjective(); }
            }
        }

        void OnTutorialPickup(bool energyShard)
        {
            if (Stage == CampaignStage.Tutorial && energyShard && !tutPickupSaid) { tutPickupSaid = true; Say(Line.T05); }
        }

        /// <summary>The walk is over (Arrival): what was only for the way goes (the void crystals, a crate left on the way, the spotlight).</summary>
        void EndDayZeroWalk()
        {
            tutorialHint = "";
            ui.HideSpotlight();
            foreach (var crystal in voidCrystals) if (crystal) Destroy(crystal.gameObject);
            voidCrystals.Clear();
            RemoveCrates(CrateKind.TutorialFirst);
            RemoveCrates(CrateKind.TutorialSecond);
            tutFirstCrate = tutSecondCrate = null;
        }

        // ───── Taught after the first fight (2026-11-10): the right button and the first skill ─────

        enum LateHint { None, Switch, Skill }

        // A hint of a later day stays this long at most; then it goes by itself.
        const float LateHintSeconds = 12f;
        // Crates whose panel was opened in this run: the right button is taught at the second one.
        int cratesOpened;
        bool switchTaught, skillTaught, skillTeachPending;
        int switchFrom, skillSlot;
        float skillCooldownSeen, lateHintUntil;
        LateHint lateHint;

        /// <summary>For automated checks: the hint of a later day that is up now ("None", "Switch" or "Skill").</summary>
        public string LateHintNow => lateHint.ToString();

        /// <summary>Called as a crate's panel is done with (V2Campaign.Crates.cs). The right button is taught the second time a crate is
        /// opened, once both hands hold a weapon: on Day 0's walk as a forced step (only the right button works until it is pressed), on
        /// a later day as the circle and the tip alone, which hold nothing back.</summary>
        void MaybeTeachSwitch()
        {
            if (switchTaught || cratesOpened < 2 || !Running) return;

            var weapons = gm ? gm.runWeapons : null;

            if (!weapons ||
                weapons.Hand == null ||
                weapons.Hand.Length < 2 ||
                weapons.Hand[0] == null ||
                weapons.Hand[1] == null)
            {
                return;
            }

            switchTaught = true;
            // Record weapon 2's cooldown before teaching its use.
            tutCooldown = weapons.Hand[1].cooldown;

            string key = ArenaText.Pair("右键", "RMB");
            string phrase = ArenaText.Pair(
                "使用副武器，无需切换",
                "Use your secondary weapon");

            if (Day == 0 && Stage == CampaignStage.Tutorial)
            {
                Objective(
                    ArenaText.Pair(
                        "你获得了副武器！点击鼠标右键使用。",
                        "You picked up a secondary weapon! Right-click to fire."),
                    null);

                Teach(
                    TutorialStep.Switch,
                    PlayerPoint,
                    3.2f,
                    key,
                    phrase);
            }
            else
            {
                StartLateHint(LateHint.Switch, key, phrase);
            }
        }

        /// <summary>The forced right-button step is done: the way home goes on.</summary>
        void ResumeHomeward()
        {
            tutStep = TutorialStep.SecondCrate; tutForced = false;
            HomeObjective();
        }

        void StartLateHint(LateHint hint, string key, string phrase)
        {
            lateHint = hint;
            lateHintUntil = Time.unscaledTime + LateHintSeconds;
            ui.ShowSpotlight(PlayerPoint, 3.2f, key, phrase);
        }

        /// <summary>Each frame (CrateLoop). The first skill is taught the first time the player is out in a fight after learning one
        /// (skills cast only in a fight): the circle and the tip with the skill's own key. A hint of a later day holds no input back; it
        /// goes with a green flash once its key has done its work, or by itself after LateHintSeconds.</summary>
        void TickLateTeaching()
        {
            if (!Running || !gm) return;
            var weapons = gm.runWeapons;
            var skills = gm.skills;
            if (!skillTaught && !skillTeachPending && skills && skills.OwnedCount > 0) skillTeachPending = true;
            bool inFight = Stage is CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk && gm.RunPhase == ArenaUiState.WaveActive
                && gm.CanUseCombatInput && !(WorldMap.Active && WorldMap.Active.TrialActive);
            if (skillTeachPending && lateHint == LateHint.None && !tutForced && inFight)
            {
                skillSlot = System.Array.FindIndex(skills.Slots, slot => !slot.Empty);
                skillTeachPending = false; skillTaught = true;
                if (skillSlot >= 0)
                {
                    skillCooldownSeen = skills.Slots[skillSlot].CooldownLeft;
                    StartLateHint(LateHint.Skill, (skillSlot + 1).ToString(), ArenaText.Pair("释放技能", "Cast your skill"));
                }
            }
            if (lateHint == LateHint.None) return;
            bool done;
            if (lateHint == LateHint.Switch) done = weapons && weapons.ActiveIndex != switchFrom;
            else
            {
                float cooldown = skills && skillSlot >= 0 && skillSlot < skills.Slots.Length ? skills.Slots[skillSlot].CooldownLeft : 0f;
                done = SpotlightTutorial.Fired(skillCooldownSeen, cooldown);
                skillCooldownSeen = cooldown;
            }
            if (done)
            {
                lateHint = LateHint.None;
                ui.PassSpotlight();
                V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, PlayerPoint(), 1.3f, .75f);
            }
            else if (Time.unscaledTime >= lateHintUntil || Stage is CampaignStage.Base or CampaignStage.Story or CampaignStage.Off)
            {
                lateHint = LateHint.None;
                ui.HideSpotlight();
            }
        }


        // ───── The void crystals by the way home ─────

        /// <summary>Purple-black crystals of void matter (whitebox look "voidore"): they cannot be mined; E only says so.</summary>
        void PlaceVoidCrystals(Vector3 at)
        {
            voidCrystalsAt = at;
            for (int i = 0; i < rules.tutorialVoidCrystals; i++)
            {
                var root = new GameObject("VoidCrystal").transform;
                root.SetParent(worldRoot, false);
                float a = i * 2.3f;
                root.position = at + (i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.2f);
                root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                root.localScale = Vector3.one * (i == 0 ? 1f : .7f);
                Whitebox.Build("voidore", root);
                voidCrystals.Add(root);
            }
        }

        void TickVoidCrystals(Vector3 pp)
        {
            if (voidCrystals.Count == 0) return;
            if (Random.value < Time.deltaTime * 3f)
                Fx.Pop(voidCrystalsAt + new Vector3(Random.Range(-1f, 1f), Random.Range(.4f, 1.4f), Random.Range(-1f, 1f)), new Color(.42f, .12f, .58f), Random.Range(.4f, .7f));
            if (Apart(pp, voidCrystalsAt) > rules.crateReach + 1f) return;
            prompt = ArenaText.Pair("紫黑色的晶体（按 E）", "A purple-black crystal (E)");
            if (!EPressed) return;
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneVoidCrystal, "", 0f, Day);
            ui.Flash(ArenaText.Pair("无法采集", "Cannot be mined"), 2f);
            V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
            if (!voidLineSaid) { voidLineSaid = true; Say(Line.T09); }
        }

        // ───── Tutorial night ─────

        int tutNightStep, tutDarkMined;

        void BeginTutorialNight()
        {
            Stage = CampaignStage.Night;
            CampaignEvents.RaisePhase("night", Day);
            gm.CampaignSetPhase(ArenaUiState.Night);
            PlacePlayer(DoorPoint);
            lampStock += rules.freeLampsPerNight;
            nightAlloy = nightDark = 0; knockedOut = false;
            stageClock = 0f; stageLength = rules.NightLength(0);
            LerpLighting(1f);
            darknessLevel = rules.nightDarkness;
            darkness.SetActive(true);
            if (baseLight) baseLight.intensity = 2.2f;
            Vector3 cluster = FindOpen(BaseCentre + new Vector3(18f, 0f, -9f), 1.2f);
            tutCluster = cluster;
            AddCluster(cluster);
            // The tutorial's ore lies beside the cluster, inside the one lamp's light (the first night teaches one lamp, not two). Off
            // since 2026-11-10 (rules.dayZeroNightOre), so Day 0 gives rules.dayZeroAlloy in all.
            if (rules.dayZeroNightOre)
                for (int i = 0; i < rules.tutorialNightOre; i++)
                    AddNode(NodeKind.Ore, FindOpen(cluster + new Vector3(2.6f + 1.4f * i, 0f, 1.8f - 3.6f * i), 1f, 2.5f), rules.tutorialNightOreAlloy);
            SpawnShadows();
            tutNightStep = 0; tutDarkMined = 0;
            // Everything is taught by now: only the night's two forced steps hold anything back.
            tutNext = null; tutLastDone = TutorialStep.Done;
            Approach(TutorialStep.Lamp, ArenaText.Pair("去发紫光的暗物质那里", "Go to the purple dark matter"), cluster);
            Say(Line.S02);
            nightWhispers = true;
            StartCoroutine(TutorialNightLine());
        }

        IEnumerator TutorialNightLine()
        {
            yield return new WaitForSecondsRealtime(4.5f);
            if (Stage == CampaignStage.Night && Day == 0) Say(Line.N01);
        }

        /// <summary>The tutorial night, each frame (TickNight): walk to the dark matter, then two forced steps, F for a lamp beside it and
        /// E in its light; the rest of the cluster and the way back to the base door are free.</summary>
        void TickTutorialNight(float dt)
        {
            RunNextStep();
            switch (tutStep)
            {
                case TutorialStep.Lamp when !tutForced:
                    if (Apart(PlayerPoint(), tutCluster) >= rules.tutorialTeachReach + 1f) break;
                    Objective(ArenaText.Pair("在暗物质旁放下一盏矿灯", "Set a lamp down by the dark matter"), tutCluster);
                    Teach(TutorialStep.Lamp, () => tutCluster, 4.5f, "F", ArenaText.Pair("放下矿灯", "Place a lamp"));
                    break;
                case TutorialStep.Lamp:
                    // Dark matter already mined out (under a lamp set on the way) has nothing left to light: the step is done too.
                    if (ClusterLit() || NodesLeft(NodeKind.Dark) == 0) { Say(Line.N02); PassStep(TeachDarkMatter); }
                    // The step must be doable: with no lamp left and none lighting the dark matter, the base sends one more.
                    else if (lampStock <= 0)
                    {
                        lampStock++;
                        ui.Flash(ArenaText.Pair("基地补给了一盏矿灯：放在暗物质旁边", "The base sends one more lamp: set it by the dark matter"), 3f);
                    }
                    break;
            }
            // Since 2026-11-06 the night has no clock: a tutorial night that runs past tutorialNightSeconds once its forced steps are done
            // (the dark matter left alone) points the player back to the base door, where waiting for dawn starts day 1.
            if (EveningsOpenEnded && tutNightStep < 2 && tutStep == TutorialStep.Done && stageClock >= rules.tutorialNightSeconds)
            {
                tutNightStep = 2;
                TutorialDawnObjective();
            }
        }

        void TeachDarkMatter()
        {
            if (NodesLeft(NodeKind.Dark) == 0)
            {
                tutStep = TutorialStep.Done;
                if (tutNightStep < 2) { tutNightStep = 2; TutorialDawnObjective(); }
                return;
            }
            Objective(ArenaText.Pair("站在灯光里采集暗物质", "Mine the dark matter while standing in the light"), null);
            Teach(TutorialStep.DarkMatter, () => tutCluster, 4.5f, "E", ArenaText.Pair("按住采集", "Hold to mine"));
        }

        /// <summary>A lamp lights some dark matter of the tutorial's cluster.</summary>
        bool ClusterLit()
        {
            foreach (var node in nodes)
                if (node.kind == NodeKind.Dark && !node.mined && !node.hidden && InLampLight(node.position) && !InBaseLight(node.position)) return true;
            return false;
        }

        /// <summary>The tutorial night's last goal: back to the base door, then "wait for dawn" in the base panel starts day 1.</summary>
        void TutorialDawnObjective() => Objective(ArenaText.Pair("回基地，选“等待天亮”开始第 1 天", "Return to the base and choose Wait for dawn to start day 1"), DoorPoint);

        /// <summary>Day 0 is over (CrateLoop sees day 1): a spotlight still up goes and the inputs are all free.</summary>
        void ForgetTutorial()
        {
            if (tutStep == TutorialStep.Done && !tutForced && tutNext == null) return;
            tutStep = TutorialStep.Done; tutForced = false; tutNext = null;
            ui.HideSpotlight();
        }

        // ───── For automated checks ─────

        /// <summary>Passes the step being taught when an automated check cannot give its input (a walk, shots, a sprint).</summary>
        public void DebugPassTutorialStep()
        {
            if (!tutForced) return;
            System.Action next = tutStep switch
            {
                TutorialStep.Move => TeachFire,
                TutorialStep.Fire => ApproachOre,
                TutorialStep.Sprint => TeachFight,
                TutorialStep.Switch => ResumeHomeward,
                _ => null
            };
            if (next != null) PassStep(next);
        }
    }
}
