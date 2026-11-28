using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Day 0 as a forced spotlight tutorial (implementation spec 2026-11-10, section A; the V2 doc asks that every control be taught with a
    /// dark screen and a spotlight, moving on only once the player has done it and seen it work). From the crash site west of the base the
    /// player is taught, one step at a time: 1 walk 4 cells toward the marker, 2 fire three pistol shots anywhere, 3 hold E at the glowing
    /// ore, 4 open the weapon crate with E and take one of three (it goes into the main hand, the pistol stays in the other), then fire
    /// the new weapon twice while no monster is out, to see how it attacks, 5 sprint once,
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
        // Where the step being taught began (the player's place then): a fall on Day 0's walk wakes the player there (NightDeath).
        Vector3 tutWake;
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
            dayZeroSkipped = false;
            quietLinesWaiting = 0; speechUntil = 0f;
            tutPickupSaid = tutHomeSaid = voidLineSaid = secondCrateHint = changedWeaponSaid = false;
            cratesOpened = 0; switchTaught = skillTaught = skillTeachPending = false; lateHint = LateHint.None;
            tryWeapon = null; tryWeaponPending = false;
            dashTaught = dashTeachPending = shadowFireSaid = false;
            tutGrunts.Clear(); voidCrystals.Clear(); crates.Clear();
            tutOre = tutSecondOre = null; tutFirstCrate = tutSecondCrate = null;
            PlayerController.InputGate = TutorialLets;
            if (!crateLoopRunning) { crateLoopRunning = true; StartCoroutine(CrateLoop()); }
            // Crash debris at the start: pieces of the wreck while the art is on (CampaignArt.crashDebrisPrefabs, taken in turn), else a
            // few dark scorched slabs.
            var debris = art && Art.Enabled && art.crashDebrisPrefabs != null && art.crashDebrisPrefabs.Length > 0 ? art.crashDebrisPrefabs : null;
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = CrashSite + new Vector3(Random.Range(-4f, 3f), .2f, Random.Range(-5f, 5f));
                var piece = debris != null ? debris[i % debris.Length] : null;
                if (piece)
                {
                    var holder = new GameObject("CrashDebris").transform;
                    holder.SetParent(worldRoot, false);
                    holder.SetPositionAndRotation(new Vector3(at.x, 0f, at.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    var model = Instantiate(piece, holder);
                    StripColliders(model);
                    FitFootprint(model.transform, Random.Range(1.4f, 2.4f));
                    continue;
                }
                Box(worldRoot, "CrashDebris", at, new Vector3(Random.Range(.8f, 2f), .4f, Random.Range(.6f, 1.6f)), new Color(.18f, .16f, .15f))
                    .transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            }
            Say(Line.T01);
            TeachMove();
        }

        /// <summary>PlayerController.InputGate while a campaign runs: on Day 0 (its walk and its night) only what the tutorial lets through.</summary>
        bool TutorialLets(PlayerAct act)
        {
            if (!this || !Running || Active != this || Day != 0 || Stage is not (CampaignStage.Tutorial or CampaignStage.Night)) return true;
            return SpotlightTutorial.Lets(act, tutForced ? tutStep : TutorialStep.None, tutLastDone);
        }

        Vector3 PlayerPoint() => gm && gm.player ? gm.player.Position : BaseCentre;
        static float Apart(Vector3 a, Vector3 b) => Query.Flat(a - b).magnitude;

        // ───── Teaching a step ─────

        /// <summary>Puts a step's spotlight up: the bright circle on what at returns, the tip "[key] phrase", every other input held back.</summary>
        void Teach(TutorialStep step, System.Func<Vector3> at, float radius, string key, string phrase)
        {
            if (step != tutStep) BeginStep();
            tutStep = step; tutForced = true;
            ui.ShowSpotlight(at, radius, key, phrase);
            tutorialHint = WorkingKeys();
        }

        /// <summary>A step to walk to first (no spotlight yet): the goal and its marker.</summary>
        void Approach(TutorialStep step, string goal, Vector3? marker)
        {
            if (step != tutStep) BeginStep();
            tutStep = step; tutForced = false;
            ui.HideSpotlight();
            Objective(goal, marker);
            tutorialHint = WorkingKeys();
        }

        /// <summary>A new step begins: when (for its analytics row) and where the player stands (where a fall wakes them).</summary>
        void BeginStep()
        {
            tutStepAt = Time.unscaledTime;
            tutWake = Query.Flat(PlayerPoint());
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
            // The next step waits for the green flash to fade and for whoever is talking to finish: a step that began under a line left
            // the line trailing behind what the player was already doing.
            if (tutNext == null || ui.SpotlightBusy || SpeechBusy) return;
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
            return Dot((works & PlayerAct.Move) != 0 ? ArenaText.Pair($"{ArenaKeys.MoveKeys} 移动", $"{ArenaKeys.MoveKeys} to move") : "",
                (works & PlayerAct.Fire) != 0 ? ArenaText.Pair("左键开火", "Left click to fire") : "",
                (works & PlayerAct.Sprint) != 0 ? ArenaText.Pair($"{ArenaKeys.SprintKey} 冲刺", $"{ArenaKeys.SprintKey} to sprint") : "",
                (works & PlayerAct.Switch) != 0 && switchTaught ? ArenaText.Pair("右键换手", "Right click to switch hands") : "");
        }

        static string MouseKey => ArenaText.Pair("左键", "LMB");

        // ───── Day 0's steps ─────

        void TeachMove()
        {
            tutStart = Query.Flat(PlayerPoint());
            tutMarker = FindOpen(CrashSite + new Vector3(14f, 0f, 0f), 1f);
            Objective(ArenaText.Pair($"朝闪动的标记走 {rules.tutorialMoveMeters:0} 米", $"Walk {rules.tutorialMoveMeters:0} m toward the pulsing marker"), tutMarker);
            Teach(TutorialStep.Move, PlayerPoint, 3f, ArenaKeys.MoveKeys, ArenaText.Pair("向标记移动", "Walk to the marker"));
        }

        void TeachFire()
        {
            tutShots = 0;
            tutCooldown = gm.runWeapons && gm.runWeapons.Active != null ? gm.runWeapons.Active.cooldown : 0f;
            FireObjective();
            Teach(TutorialStep.Fire, PlayerPoint, 3f, MouseKey, ArenaText.Pair("按住开火", "Hold to fire"));
        }

        /// <summary>The firing step's goal with the shots fired so far after it, "(1/3)", written again at every shot.</summary>
        void FireObjective()
        {
            int need = rules.tutorialShots, done = Mathf.Clamp(tutShots, 0, need);
            Objective(ArenaText.Pair($"用小手枪开 {need} 枪，朝哪打都行（{done}/{need}）", $"Fire the pistol {need} times, in any direction ({done}/{need})"), null);
        }

        /// <summary>The Fire and TryWeapon steps, each frame: whether the weapon in use fired since the last look (its cooldown jumped).</summary>
        bool ActiveWeaponFired()
        {
            var weapon = gm.runWeapons ? gm.runWeapons.Active : null;
            float cooldown = weapon != null ? weapon.cooldown : 0f;
            bool fired = weapon != null && SpotlightTutorial.Fired(tutCooldown, cooldown);
            tutCooldown = cooldown;
            return fired;
        }

        void ApproachOre()
        {
            Vector3 corner = FindOpen(TutorialCorner, 1.4f);
            tutOre = AddNode(NodeKind.Ore, corner, DayZeroOre(0)); // Day 0's alloy (V2Campaign.Days.cs, since 2026-11-10)
            if (tutOre.view) tutOre.view.localScale *= 1.4f;
            Approach(TutorialStep.Mine, ArenaText.Pair("去角落里发光的地方看看", "Check out the glow in the corner"), corner);
            Say(Line.T07);
        }

        void ApproachCrate()
        {
            Vector3 at = FindOpen(Vector3.Lerp(TutorialCorner, DoorPoint, .2f), 1.2f);
            tutFirstCrate = PlaceCrate(at, CrateKind.TutorialFirst);
            Approach(TutorialStep.Crate, ArenaText.Pair("前面有个武器箱，过去看看", "There's a weapon crate ahead. Go take a look"), at);
            SayWhenQuiet(Line.T08, () => Stage == CampaignStage.Tutorial);
        }

        // The first crate's weapon is fired this many times before the sprint (the goal's words say "twice").
        const int TryWeaponUses = 2;

        /// <summary>Right after the first crate's weapon card: the new weapon fired twice while no monster is out yet, so how it attacks
        /// (and whether its trait can hurt the player) is seen in a calm moment before the first fight. Walking works too.</summary>
        void TeachTryWeapon()
        {
            tutShots = 0;
            tutCooldown = gm.runWeapons && gm.runWeapons.Active != null ? gm.runWeapons.Active.cooldown : 0f;
            Objective(ArenaText.Pair("先试两下刚拿到的武器，看清它怎么攻击", "Fire your new weapon twice and watch how it attacks"), null);
            Teach(TutorialStep.TryWeapon, PlayerPoint, 3f, MouseKey, ArenaText.Pair("试用新武器", "Try your new weapon"));
        }

        void TeachSprint()
        {
            tutSprinted = 0f;
            Objective(ArenaText.Pair("边走边冲刺一次", "Sprint once while walking"), null);
            Teach(TutorialStep.Sprint, PlayerPoint, 3.5f, ArenaKeys.SprintKey, ArenaText.Pair("冲刺一次", "Sprint once"));
        }

        /// <summary>The hero speaks first ("something's coming"); the monsters come when he has finished (StartFight).</summary>
        void TeachFight()
        {
            if (TutorialStep.Fight != tutStep) BeginStep();
            tutStep = TutorialStep.Fight; tutForced = false;
            ui.HideSpotlight();
            Objective(ArenaText.Pair("有东西过来了", "Something is coming"), null);
            Say(Line.T04);
            tutNext = StartFight; // RunNextStep holds it until the line is over
            tutorialHint = WorkingKeys();
        }

        void StartFight()
        {
            Vector3 from = PlayerPoint(), way = Query.Flat(DoorPoint - from);
            Vector3 ahead = way.sqrMagnitude > 1f ? way.normalized : Vector3.right;
            SpawnTutorialGrunts(from + ahead * rules.tutorialFightDistance);
            Objective(ArenaText.Pair($"用新武器消灭 {tutGrunts.Count} 只怪物。被你打中的怪物会转头冲向你。", $"Defeat the {tutGrunts.Count} monsters with your new weapon. Monsters you hit turn and charge at you."), null);
            Teach(TutorialStep.Fight, PlayerPoint, 9f, MouseKey, ArenaText.Pair("消灭怪物", "Defeat the monsters"));
        }

        void TeachCollect()
        {
            Objective(ArenaText.Pair("走过去，捡起怪物掉落的异星能量", "Walk over the alien energy the monsters dropped to pick it up"), null);
            Teach(TutorialStep.Collect, NearestPickup, 2.6f, ArenaKeys.MoveKeys, ArenaText.Pair("捡起异星能量", "Pick up the alien energy"));
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
            tutWake = from;
            ui.HideSpotlight();
            homeLeg = HomeLeg.Ore;
            HomeObjective();
        }

        void HomeObjective()
        {
            switch (homeLeg)
            {
                case HomeLeg.Ore:
                    Objective(ArenaText.Pair("回基地。路边还有一处合金矿，可以顺手采掉", "Head back to the base. You can mine the alloy ore by the road on the way"), tutSecondOre?.position);
                    break;
                case HomeLeg.Crate:
                    Objective(ArenaText.Pair("路边还有一个武器箱。从里面拿武器会换下小手枪，不想换也可以不拿", "There's another weapon crate by the road. Taking a weapon from it replaces your pistol, but you don't have to take one"), tutSecondCrate?.position);
                    break;
                default:
                    Objective(ArenaText.Pair("回到基地门口", "Go to the base door"), DoorPoint);
                    break;
            }
        }

        void TickTutorial(float dt)
        {
            prompt = "";
            // A fall on the walk is caught: the player wakes where the step began and the step goes on (NightDeath).
            if (CaughtNightDeath()) return;
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
                    if (ActiveWeaponFired()) { tutShots++; FireObjective(); }
                    if (tutShots >= rules.tutorialShots) PassStep(ApproachOre);
                    break;
                case TutorialStep.TryWeapon:
                    if (!tutForced) break;
                    if (ActiveWeaponFired()) tutShots++;
                    if (tutShots >= TryWeaponUses) PassStep(TeachSprint);
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
                        Objective(ArenaText.Pair("站到矿石旁边，把它采完", "Stand next to the ore and mine all of it"), ore);
                        Teach(TutorialStep.Mine, () => ore, 2.6f, ArenaKeys.InteractKey, ArenaText.Pair("按住采矿", "Hold to mine"));
                    }
                    break;
                case TutorialStep.Crate:
                    if (tutFirstCrate == null || crateOpen != null) break;
                    if (!tutForced)
                    {
                        if (Apart(pp, tutFirstCrate.position) >= rules.tutorialTeachReach) break;
                        Vector3 at = tutFirstCrate.position;
                        Objective(ArenaText.Pair("站到武器箱旁边，打开它", "Stand next to the crate and open it"), at);
                        Teach(TutorialStep.Crate, () => at, 2.6f, ArenaKeys.InteractKey, ArenaText.Pair("打开武器箱", "Open the crate"));
                    }
                    else if (CrateUse(tutFirstCrate, out string text))
                    {
                        var crate = tutFirstCrate;
                        tutFirstCrate = null;
                        // A weapon taken is tried out first; a crate with nothing new in it goes straight on to the sprint.
                        OpenCrate(crate, taken => PassStep(taken ? (System.Action)TeachTryWeapon : TeachSprint));
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
                    if (tutForced && gm.runWeapons && gm.runWeapons.ActiveIndex != switchFrom) PassStep(ResumeHomeward);
                    break;
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
                    if (near) ui.ShowSpotlight(() => at, 2.6f, ArenaKeys.InteractKey, ArenaText.Pair("打开武器箱", "Open the crate"), false);
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

        // ───── Taught after the first fight: the right button, the first skill, the void dash and an afternoon crate's new weapon ─────

        enum LateHint { None, Switch, Skill, Dash, HoleEscape, TryWeapon }

        // A hint of a later day stays this long at most, in game time (a pause or the character screen holds it); then it goes by itself.
        const float LateHintSeconds = 12f;
        // The circle a hint of a later day draws round the player, in cells.
        const float LateHintRadius = 3.2f;
        // The afternoon crate's hand weapon to be tried out: its hint is waiting (pending) or up, and asks for the right button first while
        // the weapon is not the hand in use; the weapon's shots so far and its cooldown at the last look.
        RunWeapon tryWeapon;
        bool tryWeaponPending, tryWeaponSwitching;
        int tryWeaponShots;
        float tryWeaponCooldown;
        // Crates whose panel was opened in this run: the right button is taught at the second one.
        int cratesOpened;
        bool switchTaught, skillTaught, skillTeachPending, dashTaught, dashTeachPending;
        int switchFrom, skillSlot;
        float skillCooldownSeen, lateHintUntil;
        LateHint lateHint;
        // The black hole's pull was escaped once in this run (its hint passed; reset with the hole's quest at each run's start), and when
        // the player last stepped into its ring.
        bool holeEscapeTaught;
        float holeEscapeSince;

        /// <summary>For automated checks: the hint of a later day that is up now ("None", "Switch", "Skill", "Dash", "HoleEscape" or
        /// "TryWeapon").</summary>
        public string LateHintNow => lateHint.ToString();

        /// <summary>For automated checks: the try-weapon hint is up and asks for the right button (its weapon is not the hand in use).</summary>
        public bool TryWeaponAsksSwitch => lateHint == LateHint.TryWeapon && tryWeaponSwitching;

        /// <summary>The spotlight that teaches the sprint out of the black hole's pull is up (the HUD's hint plate leaves the pull to it).</summary>
        public bool TeachingHoleEscape => lateHint == LateHint.HoleEscape;

        /// <summary>Called as a crate's panel is done with (V2Campaign.Crates.cs). The right button is taught the second time a crate is
        /// opened, once both hands hold a weapon: on Day 0's walk as a forced step (only the right button works until it is pressed), on
        /// a later day as the circle and the tip alone, without the dim, which hold nothing back.</summary>
        void MaybeTeachSwitch()
        {
            if (switchTaught || cratesOpened < 2 || !Running) return;
            var weapons = gm ? gm.runWeapons : null;
            if (!weapons || weapons.Hand.Length < 2 || weapons.Hand[0] == null || weapons.Hand[1] == null) return;
            switchTaught = true;
            switchFrom = weapons.ActiveIndex;
            string key = ArenaText.Pair("右键", "RMB"), phrase = ArenaText.Pair("换到另一只手的武器", "Switch to your other weapon");
            if (Day == 0 && Stage == CampaignStage.Tutorial)
            {
                Objective(ArenaText.Pair("换到另一只手上的武器", "Switch to the weapon in your other hand"), null);
                Teach(TutorialStep.Switch, PlayerPoint, 3.2f, key, phrase);
            }
            else StartLateHint(LateHint.Switch, key, phrase);
        }

        /// <summary>The forced right-button step is done: the way home goes on.</summary>
        void ResumeHomeward()
        {
            tutStep = TutorialStep.SecondCrate; tutForced = false;
            HomeObjective();
        }

        /// <summary>A hint of a later day: the circle round the player and the tip "[key] phrase", without the dim (only Day 0's forced
        /// steps dim the screen), so the fight or the walk round the player stays in full view.</summary>
        void StartLateHint(LateHint hint, string key, string phrase)
        {
            lateHint = hint;
            lateHintUntil = Time.time + LateHintSeconds;
            ui.ShowSpotlight(PlayerPoint, LateHintRadius, key, phrase, false);
        }

        /// <summary>A hint of a later day that is up gives way to the black hole's: it is asked for again later (the skill and the void
        /// dash at once, the right button at the next crate opened).</summary>
        void SetAsideLateHint()
        {
            switch (lateHint)
            {
                case LateHint.Skill: skillTaught = false; break;
                case LateHint.Dash: dashTeachPending = true; break;
                case LateHint.Switch: switchTaught = false; break;
                case LateHint.TryWeapon: tryWeaponPending = true; break;
            }
            lateHint = LateHint.None;
        }

        /// <summary>An afternoon crate's weapon was taken and its card dismissed (CrateLoop): a hand weapon is tried out with a hint, after
        /// the right button's when that one comes up too (MaybeTeachSwitch runs right after this). An auto weapon gets none.</summary>
        void QueueTryWeapon(RunWeapon weapon)
        {
            if (weapon == null || weapon.IsAuto) return;
            if (lateHint == LateHint.TryWeapon) { lateHint = LateHint.None; ui.HideSpotlight(); }
            tryWeapon = weapon;
            tryWeaponPending = true;
        }

        /// <summary>The try-weapon hint waits until no other hint is up (and the green flash of one just passed is over), the player is out
        /// in the afternoon in play and no monster stands within rules.tryWeaponClearance cells. It is dropped once the afternoon is over
        /// or its weapon has left the hands.</summary>
        void MaybeStartTryWeapon()
        {
            int hand = TryWeaponHand();
            if (hand < 0 || Stage is not (CampaignStage.Dusk or CampaignStage.Base or CampaignStage.Story))
            {
                tryWeaponPending = false;
                tryWeapon = null;
                return;
            }
            if (lateHint != LateHint.None || ui.SpotlightBusy || tutForced || Stage != CampaignStage.Dusk || !InPlay || !gm.CanPlayerMove
                || WorldMap.Active && WorldMap.Active.TrialActive || MonsterWithin(PlayerPoint(), rules.tryWeaponClearance)) return;
            tryWeaponPending = false;
            tryWeaponShots = 0;
            tryWeaponCooldown = tryWeapon.cooldown;
            bool inUse = gm.runWeapons.ActiveIndex == hand;
            tryWeaponSwitching = !inUse;
            StartLateHint(LateHint.TryWeapon, TryWeaponKey(inUse), TryWeaponPhrase(inUse));
        }

        /// <summary>The try-weapon hint, each frame: the right button's tip while its weapon is not the hand in use, the left button's once
        /// it is (for a full LateHintSeconds from then), and back should the player switch away again; done after TryWeaponUses shots of
        /// it. False when it was taken down without a pass: its weapon left the hands, or the player fell.</summary>
        bool TickTryWeapon(out bool done)
        {
            done = false;
            int hand = TryWeaponHand();
            if (hand < 0 || !gm.player || !gm.player.IsAlive)
            {
                lateHint = LateHint.None;
                tryWeapon = null;
                ui.HideSpotlight();
                return false;
            }
            bool inUse = gm.runWeapons.ActiveIndex == hand;
            if (inUse == tryWeaponSwitching)
            {
                tryWeaponSwitching = !inUse;
                ui.ShowSpotlight(PlayerPoint, LateHintRadius, TryWeaponKey(inUse), TryWeaponPhrase(inUse), false);
                if (inUse) lateHintUntil = Time.time + LateHintSeconds;
            }
            float cooldown = tryWeapon.cooldown;
            if (inUse && SpotlightTutorial.Fired(tryWeaponCooldown, cooldown)) tryWeaponShots++;
            tryWeaponCooldown = cooldown;
            done = tryWeaponShots >= TryWeaponUses;
            return true;
        }

        /// <summary>The hand that holds the weapon to try out, or -1 when it is in neither.</summary>
        int TryWeaponHand()
        {
            var weapons = gm ? gm.runWeapons : null;
            return tryWeapon == null || !weapons || weapons.Hand == null ? -1 : System.Array.IndexOf(weapons.Hand, tryWeapon);
        }

        static string TryWeaponKey(bool inUse) => inUse ? MouseKey : ArenaText.Pair("右键", "RMB");
        static string TryWeaponPhrase(bool inUse) => inUse ? ArenaText.Pair("试用新武器", "Try your new weapon") : ArenaText.Pair("换到新武器", "Switch to your new weapon");

        /// <summary>A living monster stands within reach cells of a point.</summary>
        bool MonsterWithin(Vector3 at, float reach)
        {
            var waves = gm ? gm.waves : null;
            if (!waves || reach <= 0f) return false;
            foreach (var enemy in waves.Alive)
                if (enemy && enemy.IsAlive && Query.Flat(enemy.Position - at).sqrMagnitude <= reach * reach) return true;
            return false;
        }

        /// <summary>The void dash was just gained (GameManagerFlow.TickVoidDashNotice): Space is taught once, as a hint of a later day,
        /// as soon as play is back on the map and nothing else is being taught; it goes once the player has dashed.</summary>
        internal void TeachVoidDash()
        {
            if (!Running || dashTaught) return;
            dashTaught = dashTeachPending = true;
        }

        /// <summary>Each frame (CrateLoop). The first skill is taught the first time the player is out in a fight after learning one
        /// (skills cast only in a fight): the circle and the tip with the key the skill is on. The void dash is taught once it was gained
        /// (TeachVoidDash), when play is back on the map. A hand weapon from an afternoon crate is tried out (QueueTryWeapon) once the
        /// hints before it are done and no monster is near. A hint of a later day holds no input back and leaves the screen undimmed; it
        /// goes with a green flash once its key has done its work, or by itself after LateHintSeconds of play. The black hole's pull is
        /// taught the first time the player stands in the campaign hole's ring (by day or in the afternoon): the sprint key and its words,
        /// ahead of any other hint. It passes once the player is out of the ring alive (a tutorial_step row "HoleEscape" with the seconds
        /// spent inside) and comes again after a fall.</summary>
        void TickLateTeaching()
        {
            if (!Running || !gm) return;
            var weapons = gm.runWeapons;
            var skills = gm.skills;
            var player = gm.player;
            if (!skillTaught && !skillTeachPending && skills && skills.OwnedCount > 0) skillTeachPending = true;
            bool inFight = Stage is CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk && gm.RunPhase == ArenaUiState.WaveActive
                && gm.CanUseCombatInput && !(WorldMap.Active && WorldMap.Active.TrialActive);
            // A hole's pull drops the player within seconds: its hint goes up at once, and a hint already up gives way to it.
            bool inPull = PlayerInHolePull(player);
            if (!holeEscapeTaught && inPull && lateHint != LateHint.HoleEscape && !tutForced && InPlay)
            {
                SetAsideLateHint();
                holeEscapeSince = Time.time;
                StartLateHint(LateHint.HoleEscape, ArenaKeys.SprintKey, ArenaText.Pair("冲刺逃出引力", "Sprint out of the pull"));
            }
            if (tryWeaponPending) MaybeStartTryWeapon();
            if (skillTeachPending && lateHint == LateHint.None && !tutForced && inFight)
            {
                skillSlot = System.Array.FindIndex(skills.Slots, slot => !slot.Empty);
                skillTeachPending = false; skillTaught = true;
                if (skillSlot >= 0)
                {
                    skillCooldownSeen = skills.Slots[skillSlot].CooldownLeft;
                    // The key the skill is on, which the settings can move.
                    StartLateHint(LateHint.Skill, ArenaKeys.Name(GameAction.Skill1 + skillSlot), ArenaText.Pair("释放技能", "Cast your skill"));
                }
            }
            // The void dash: once the player can move on the map again (not in the base, a cutscene or the void trial).
            if (dashTeachPending && lateHint == LateHint.None && !tutForced && player && gm.CanPlayerMove
                && Stage is not (CampaignStage.Base or CampaignStage.Story or CampaignStage.Off) && !(WorldMap.Active && WorldMap.Active.TrialActive))
            {
                dashTeachPending = false;
                StartLateHint(LateHint.Dash, ArenaText.Pair("空格", "Space"), ArenaText.Pair("朝光标方向虚空冲刺", "Void Dash toward the cursor"));
            }
            if (lateHint == LateHint.None) return;
            bool done;
            if (lateHint == LateHint.HoleEscape)
            {
                // Out of the ring alive, in play, passes it. A fall, or a hole that no longer pulls (put away, broken, its cutscene),
                // takes it down without a pass, so the next time in the ring shows it again.
                if (!player || !player.IsAlive || !inPull && !HolePulling)
                {
                    lateHint = LateHint.None;
                    ui.HideSpotlight();
                    return;
                }
                done = !inPull && InPlay;
                if (done)
                {
                    holeEscapeTaught = true;
                    CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneTutorialStep, nameof(LateHint.HoleEscape), Time.time - holeEscapeSince, Day);
                }
            }
            else if (lateHint == LateHint.Switch) done = weapons && weapons.ActiveIndex != switchFrom;
            else if (lateHint == LateHint.Dash) done = player && player.voidDash.CoolingDown; // a dash starts its cooldown
            else if (lateHint == LateHint.TryWeapon)
            {
                if (!TickTryWeapon(out done)) return;
            }
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
            // The pull's hint has no time limit: it stays while the player is in the ring.
            else if (lateHint != LateHint.HoleEscape && Time.time >= lateHintUntil || Stage is CampaignStage.Base or CampaignStage.Story or CampaignStage.Off)
            {
                lateHint = LateHint.None;
                ui.HideSpotlight();
            }
            if (lateHint != LateHint.TryWeapon && !tryWeaponPending) tryWeapon = null;
        }


        // ───── The void crystals by the way home ─────

        /// <summary>Purple-black crystals of void matter (CampaignArt.voidCrystalPrefab while the art is on, else the whitebox look
        /// "voidore"): they cannot be mined; E only says so. They lie low on the ground, matte and without a glow (VoidCrystal.mat), so
        /// they do not read as the night's dark matter, which stands tall on the same model and glows violet at its edges.</summary>
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
                if (art && Art.Enabled && art.voidCrystalPrefab)
                {
                    var model = Instantiate(art.voidCrystalPrefab, root);
                    StripColliders(model);
                    // Squashed to VoidCrystalSquash of its own height, then fitted to VoidCrystalHeight (the others at .7 of it). The model
                    // stands on a pale slab of sand about a fifteenth of its height, which is sunk under the ground.
                    float height = VoidCrystalHeight * root.localScale.y;
                    model.transform.localScale = new Vector3(1f, VoidCrystalSquash, 1f);
                    FitHeight(model.transform, height);
                    model.transform.position += Vector3.down * (height * VoidCrystalSlab);
                }
                else Whitebox.Build("voidore", root).localScale = new Vector3(1f, VoidCrystalSquash, 1f);
                voidCrystals.Add(root);
            }
        }

        // The void crystals' height with their slab (the first; the others are .7 of it), how much of the model's own height they keep,
        // and the slab's share of the height.
        const float VoidCrystalHeight = .75f, VoidCrystalSquash = .45f, VoidCrystalSlab = .07f;

        void TickVoidCrystals(Vector3 pp)
        {
            if (voidCrystals.Count == 0) return;
            if (Apart(pp, voidCrystalsAt) > rules.crateReach + 1f) return;
            prompt = ArenaText.Pair($"紫黑色的晶体（按 {ArenaKeys.InteractKey}）", $"A purple-black crystal ({ArenaKeys.InteractKey})");
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
            SetBaseLights(true);
            Vector3 cluster = FindOpen(BaseCentre + new Vector3(18f, 0f, -9f), 1.2f);
            tutCluster = cluster;
            AddCluster(cluster);
            // The tutorial's ore lies beside the cluster, inside the one lamp's light (the first night teaches one lamp, not two). Off
            // since 2026-11-10 (rules.dayZeroNightOre), so Day 0 gives rules.dayZeroAlloy in all.
            if (rules.dayZeroNightOre)
                for (int i = 0; i < rules.tutorialNightOre; i++)
                    AddNode(NodeKind.Ore, FindOpen(cluster + new Vector3(2.6f + 1.4f * i, 0f, 1.8f - 3.6f * i), 1f, 2.5f), rules.tutorialNightOreAlloy);
            SpawnShadows();
            tutNightStep = 0; tutDarkMined = 0; tutEdgeSaid = false;
            // Everything is taught by now: only the night's two forced steps hold anything back.
            tutNext = null; tutLastDone = TutorialStep.Done;
            Approach(TutorialStep.Lamp, ArenaText.Pair("去那片发着紫光的暗物质旁边", "Go to the glowing purple dark matter"), cluster);
            Say(Line.S02);
        }

        /// <summary>The tutorial night, each frame (TickNight): walk to the dark matter, then two forced steps, F for a lamp beside it and
        /// E in its light; the rest of the cluster and the way back to the base door are free.</summary>
        void TickTutorialNight(float dt)
        {
            RunNextStep();
            // A passed step waits for its line and its flash (RunNextStep): nothing of it is done again meanwhile.
            if (tutNext != null) return;
            switch (tutStep)
            {
                case TutorialStep.Lamp when !tutForced:
                    if (Apart(PlayerPoint(), tutCluster) >= rules.tutorialTeachReach + 1f) break;
                    Objective(ArenaText.Pair($"矿灯只能在夜里放。现在把灯放在暗物质旁；从第 1 天起，基地每天送 {rules.dailyFreeLamps} 盏。", $"You can only place lamps after dark. Set one beside the dark matter. From day 1, the base gives you {rules.dailyFreeLamps} lamps each day."), tutCluster);
                    Teach(TutorialStep.Lamp, () => tutCluster, 4.5f, ArenaKeys.LampKey, ArenaText.Pair("放下矿灯", "Set down a lamp"));
                    // The base explains the lamp here, at the dark matter, once the line said at nightfall is over (a few seconds at most).
                    SayWhenQuiet(Line.N01, () => Stage == CampaignStage.Night && Day == 0 && tutStep == TutorialStep.Lamp && tutForced, 3f);
                    break;
                case TutorialStep.Lamp:
                    // Dark matter already mined out (under a lamp set on the way) has nothing left to light: the step is done too.
                    if (ClusterLit() || NodesLeft(NodeKind.Dark) == 0) { Say(Line.N02); PassStep(TeachDarkMatter); }
                    // The step must be doable: with no lamp left and none lighting the dark matter, the base sends one more.
                    else if (lampStock <= 0)
                    {
                        lampStock++;
                        ui.Flash(ArenaText.Pair("基地又送来一盏矿灯，把它放在暗物质旁边", "The base sent you another lamp. Set it down by the dark matter"), 3f);
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
            Objective(ArenaText.Pair("站在灯光里采集暗物质", "Stand in the lamplight and mine the dark matter"), null);
            Teach(TutorialStep.DarkMatter, () => tutCluster, 4.5f, ArenaKeys.InteractKey, ArenaText.Pair("按住采集", "Hold to mine"));
        }

        /// <summary>A lamp lights some dark matter of the tutorial's cluster.</summary>
        bool ClusterLit()
        {
            foreach (var node in nodes)
                if (node.kind == NodeKind.Dark && !node.mined && !node.hidden && InLampLight(node.position) && !InBaseLight(node.position)) return true;
            return false;
        }

        /// <summary>The tutorial night's last goal: back to the base door, then "wait for dawn" in the base panel starts day 1.</summary>
        void TutorialDawnObjective() => Objective(ArenaText.Pair("回基地，选“等待天亮”开始第 1 天。随着怪物伤害提高，你的生命上限也会在清晨增长。", "Go back to the base and choose \"Wait for dawn\" to start day 1. As monsters deal more damage each day, your max health rises at dawn."), DoorPoint);

        /// <summary>Day 0 is over (CrateLoop sees day 1): a spotlight still up goes and the inputs are all free.</summary>
        void ForgetTutorial()
        {
            if (tutStep == TutorialStep.Done && !tutForced && tutNext == null) return;
            tutStep = TutorialStep.Done; tutForced = false; tutNext = null;
            ui.HideSpotlight();
        }

        // ───── For automated checks ─────

        /// <summary>Where the void crystals stand; null before the way home has placed them.</summary>
        public Vector3? VoidCrystalsPlaced => voidCrystals.Count > 0 ? voidCrystalsAt : (Vector3?)null;

        /// <summary>Passes the step being taught when an automated check cannot give its input (a walk, shots, a sprint).</summary>
        public void DebugPassTutorialStep()
        {
            if (!tutForced) return;
            System.Action next = tutStep switch
            {
                TutorialStep.Move => TeachFire,
                TutorialStep.Fire => ApproachOre,
                TutorialStep.TryWeapon => TeachSprint,
                TutorialStep.Sprint => TeachFight,
                TutorialStep.Switch => ResumeHomeward,
                _ => null
            };
            if (next != null) PassStep(next);
        }
    }
}
