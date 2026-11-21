using System.Collections;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// "Skip Day 0" in the pause menu (V2Hud): offered on Day 0's walk and its night, on the whole game's pages (V5HudKit.On), and only
    /// in a browser where a story run has gone from Day 0 into day 1 before (DayZeroFinishedBefore, kept in the saved settings).
    /// On the walk, what was only for the way goes without rewards (the spotlight and its forced step, the walk's monsters, its ore, its
    /// crates, the void crystals, the energy left on the ground, the hint line), the player stands at the base door and the wallet is made
    /// up to what Day 0 gives (DayZeroMakeUp). If the first crate's weapon was not taken yet, the first crate's pick opens as on the walk
    /// (its weapon into the main hand, the pistol into the other); the second crate is not given. Then the arrival and the base tour as
    /// usual (Arrival). Leaving the base after a skip skips the memory and the tutorial night as well: day 1's dawn begins at once through
    /// EndNight (the morning's save, the morning at the base door), with the lamps a finished Day 0 leaves for day 1.
    /// At night the wallet is made up the same way, the tutorial night is cleared and day 1's dawn begins.
    /// It is not offered while a crate's panel or a question is open, while the player lies fallen or in the void trial; the pause itself
    /// does not open over the arrival, the base, the memory or a weapon card. A fall on Day 0 is caught as before, and Day 0 writes no save.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // The saved setting (ArenaPrefs; the tests keep theirs in memory): a story run in this browser has gone from Day 0 into day 1.
        const string DayZeroFinishedKey = "WarpArena.DayZeroFinished";
        // The lamps the tutorial night's forced step sets down: a finished Day 0 leaves its free lamps less these for day 1.
        const int TutorialNightLamps = 1;
        // At most this many seconds the arrival after a skipped walk waits for the line about the crate's weapon (T10) to be said.
        const float SkipLineWait = 8f;
        // The tutorial_step row of a skip: its detail (the other rows name the step passed), its value the seconds of the step skipped in.
        const string SkipStepName = "Skip";

        // Day 0 was skipped in this run: leaving the base after the arrival goes straight to day 1's dawn.
        bool dayZeroSkipped;

        /// <summary>A story run in this browser has gone from Day 0 into day 1 before (EndNight remembers it), so the pause menu may offer
        /// to skip Day 0.</summary>
        public static bool DayZeroFinishedBefore => ArenaPrefs.GetString(DayZeroFinishedKey) == "1";

        /// <summary>Day 0's night ends into day 1's dawn (EndNight): this browser remembers that Day 0 was finished.</summary>
        static void RememberDayZeroFinished()
        {
            if (DayZeroFinishedBefore) return;
            ArenaPrefs.SetString(DayZeroFinishedKey, "1");
            ArenaPrefs.Save();
        }

        /// <summary>The pause menu offers "Skip Day 0" now: Day 0's walk or its night, on the whole game's pages, in a browser that has
        /// finished Day 0 before, not skipped already, with no crate's panel or question open, the player standing, and not in the void
        /// trial.</summary>
        public bool DayZeroSkipOffered => Running && Active == this && Day == 0 && !dayZeroSkipped && V5HudKit.On && DayZeroFinishedBefore
            && Stage is (CampaignStage.Tutorial or CampaignStage.Night) && crateOpen == null && !knockedOut
            && gm && gm.player && gm.player.IsAlive && !(ui && ui.ConfirmOpen) && !(WorldMap.Active && WorldMap.Active.TrialActive);

        /// <summary>The sentence the paused line adds while the skip is offered: what it does at this moment (empty when it is not offered).</summary>
        public string DayZeroSkipWords => !DayZeroSkipOffered ? ""
            : Stage == CampaignStage.Night
                ? ArenaText.Pair("跳过第 0 天后，今晚的教学到此结束，直接进入第 1 天，第 0 天的合金、异星能量和暗物质会补齐。",
                    "Skipping Day 0 ends tonight's lesson and starts day 1 at once, with Day 0's alloy, alien energy and dark matter made up.")
            : cratesOpened == 0
                ? ArenaText.Pair("跳过第 0 天后直接回基地：第一个武器箱照样可选，第 0 天的合金、异星能量和暗物质会补齐，今晚的教学也一并跳过。",
                    "Skipping Day 0 goes straight to the base: you still pick the first crate, Day 0's alloy, alien energy and dark matter are made up, and tonight's lesson is skipped.")
                : ArenaText.Pair("跳过第 0 天后直接回基地：第 0 天的合金、异星能量和暗物质会补齐，今晚的教学也一并跳过。",
                    "Skipping Day 0 goes straight to the base: Day 0's alloy, alien energy and dark matter are made up, and tonight's lesson is skipped.");

        /// <summary>The pause menu's "Skip Day 0", once the pause has closed (V2Hud). Nothing while it is not offered.</summary>
        public void SkipDayZero()
        {
            if (!DayZeroSkipOffered) return;
            dayZeroSkipped = true;
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneTutorialStep, SkipStepName, Time.unscaledTime - tutStepAt, Day);
            // Whatever was being said on the way stops with it.
            if (voiceSource) voiceSource.Stop();
            speechUntil = 0f;
            ui.ClearSubtitle();
            if (Stage == CampaignStage.Night)
            {
                SkipToDayOne();
                return;
            }
            ClearDayZeroWalk();
            Gain(DayZeroMakeUp());
            Stage = CampaignStage.Story; // nothing of the walk ticks any more
            PlacePlayer(DoorPoint);
            if (cratesOpened == 0 && gm.runWeapons)
            {
                // The first crate's pick, as on the walk (Day 0's hand weapons, no way to leave it, the pick into the main hand), from a
                // crate that stands nowhere on the map. Once it is taken, or found empty, the arrival follows.
                OpenCrate(new Crate { kind = CrateKind.TutorialFirst, position = Query.Flat(PlayerPoint()) }, _ => StartCoroutine(ArriveAfterSkip()));
                return;
            }
            StartCoroutine(Arrival());
        }

        /// <summary>After the first crate's pick on a skipped walk: the run holds still while the line about the weapon (T10) is said,
        /// then the arrival.</summary>
        IEnumerator ArriveAfterSkip()
        {
            gm.CampaignSetPhase(ArenaUiState.Story, 1f);
            float until = Time.realtimeSinceStartup + SkipLineWait;
            while (SpeechBusy && Time.realtimeSinceStartup < until) yield return null;
            yield return Arrival();
        }

        /// <summary>What Day 0's walk put out goes without rewards: the spotlight and its forced step, the walk's monsters, its ore, its
        /// crates, the void crystals, the energy left on the ground, the goal and the hint line.</summary>
        void ClearDayZeroWalk()
        {
            tutStep = tutLastDone = TutorialStep.Done;
            tutForced = false; tutNext = null;
            tutGrunts.Clear();
            tutOre = tutSecondOre = null;
            DismissStragglers(); // the walk's monsters leave without a kill or a drop
            ClearNight(false);   // the walk's ore, and any mining under way
            ClearPickups();      // the energy on the ground: DayZeroMakeUp counts only what was picked up
            EndDayZeroWalk();    // the spotlight, the crates, the void crystals, the hint line
            Objective("", null);
            prompt = "";
        }

        /// <summary>Day 0's night is skipped (from the pause menu at night, or on leaving the base after a skipped walk): the wallet made
        /// up, what was mined tonight kept as at the base door, the lamps a finished Day 0 leaves, and day 1's dawn through EndNight.</summary>
        void SkipToDayOne()
        {
            Gain(DayZeroMakeUp());
            BankHaul();
            lampStock = Mathf.Max(lampStock, DayZeroLampsLeft);
            ForgetTutorial();
            ui.HideSpotlight();
            Stage = CampaignStage.Night; // EndNight takes it from the night, as WaitForDawn does
            EndNight(true);
        }

        /// <summary>What Day 0 gives by day 1's dawn, less what the run has earned so far: the alloy and the alien energy the arrival tops
        /// Day 0 up to (rules.dayZeroAlloy, dayZeroEnergy), and the dark matter of the tutorial night's cluster (rules.nodesPerCluster
        /// nodes of darkPerNode). It is measured against what was earned rather than what is held, so nothing is given twice and nothing
        /// bought at the base comes back.</summary>
        CampaignCost DayZeroMakeUp() => new(Mathf.Max(0, rules.dayZeroAlloy - EarnedAlloy), Mathf.Max(0, rules.dayZeroEnergy - EarnedEnergy),
            Mathf.Max(0, rules.nodesPerCluster * rules.darkPerNode - EarnedDark));

        /// <summary>The lamps a finished Day 0 leaves for day 1: the tutorial night's free lamps less the one its forced step sets down.</summary>
        int DayZeroLampsLeft => Mathf.Max(0, rules.freeLampsPerNight - TutorialNightLamps);

        // ───── For automated checks ─────

        /// <summary>Day 0's walk ends at once and the tutorial night begins, as it would after the arrival and the memory.</summary>
        public void DebugBeginTutorialNight()
        {
            if (!Running || Day != 0 || Stage != CampaignStage.Tutorial) return;
            ClearDayZeroWalk();
            BeginTutorialNight();
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>Tests only: whether this browser counts as having finished Day 0 before, in the tests' own settings (nothing while the
        /// settings are the real ones).</summary>
        public static void SetDayZeroFinishedForTests(bool finished)
        {
            if (!ArenaPrefs.MemoryForTests) return;
            if (finished) ArenaPrefs.SetString(DayZeroFinishedKey, "1");
            else ArenaPrefs.Delete(DayZeroFinishedKey);
        }
#endif
    }
}
