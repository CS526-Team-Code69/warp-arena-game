using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>"Skip Day 0" in the pause menu (V2Campaign.DayZeroSkip.cs) in a running story. A browser that has not finished Day 0 before
    /// is not offered it, on the walk or at night, and Day 0's night ending into day 1 remembers that it was finished. Where it was: the
    /// pause screen shows the skip's button and its sentence; a skip on the walk opens the first crate's pick, whose weapon goes into the
    /// main hand with the pistol in the other, then the base opens with its tour and exactly Day 0's alloy, alien energy and dark matter,
    /// and leaving the base starts day 1's morning at once, with its save in the run's slot and the lamps a finished Day 0 leaves. A skip
    /// on Day 0's night starts day 1's dawn the same way. Each skip is one tutorial_step record, "Skip". The saves and the saved settings
    /// are kept in memory; Day 0's night is begun with the campaign's own call for checks.</summary>
    [TestFixture]
    public class Day0SkipTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const int Slot = 2;
        readonly List<string> stepsRecorded = new();

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaSaves.InMemoryForTests && ArenaPrefs.MemoryForTests, Is.True, "the saves and the saved settings are kept in memory during the tests");
            ArenaSaves.ResetForTests();
            stepsRecorded.Clear();
            CampaignEvents.Milestone += Note;
        }

        [TearDown]
        public void TearDown()
        {
            CampaignEvents.Milestone -= Note;
            ArenaSaves.ResetForTests();
            Time.timeScale = 1f;
        }

        void Note(string kind, string detail, float value, int day)
        {
            if (kind == CampaignEvents.MilestoneTutorialStep) stepsRecorded.Add(detail);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task Day0NotFinishedBefore_TheSkipIsNotOffered_AndDay0sNightEndingRemembersIt()
        {
            Assume.That(V2Campaign.DayZeroFinishedBefore, Is.False, "the reset forgets a Day 0 finished before");
            var game = await ReadyScene();
            var campaign = await StartStory(game);
            Assume.That(V5HudKit.On, Is.True, "the whole game's pages");
            Assert.That(campaign.DayZeroSkipOffered, Is.False, "the walk offers no skip");
            var (skip, _) = await Pause(game);
            Assert.That(skip.gameObject.activeSelf, Is.False, "the pause screen shows no skip");
            await CommandUntil(game, ArenaUiCommand.Resume, () => game.RunPhase != ArenaUiState.Paused);

            campaign.DebugBeginTutorialNight();
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((0, CampaignStage.Night)), "Day 0's night has begun");
            Assert.That(campaign.DayZeroSkipOffered, Is.False, "nor does the night");

            // Waiting for dawn ends Day 0's night into day 1: from then on this browser counts Day 0 as finished, though day 1 offers no skip.
            campaign.EndNight(true);
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Day)), "the dawn began day 1's morning");
            game.player.ProtectFor(600f);
            Assert.That(V2Campaign.DayZeroFinishedBefore, Is.True, "Day 0 is remembered as finished");
            Assert.That(campaign.DayZeroSkipOffered, Is.False, "day 1 offers no skip");
            Assert.That(stepsRecorded, Has.No.Member("Skip"), "nothing was skipped");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(150000)]
        public async Task SkipOnTheWalk_OpensTheFirstCrate_ThenTheBaseWithItsTour_AndLeavingItStartsDay1sMorning()
        {
            V2Campaign.SetDayZeroFinishedForTests(true);
            ArenaSaves.Bind(SaveMode.Story, Slot);
            var game = await ReadyScene();
            var campaign = await StartStory(game);
            Assume.That(V5HudKit.On, Is.True, "the whole game's pages");
            var rules = campaign.rules;
            var weapons = game.runWeapons;
            Assume.That(weapons.Hand[weapons.ActiveIndex]?.form, Is.EqualTo(WeaponForm.Pistol), "the walk begins with the pistol in hand");
            Assert.That(campaign.DayZeroSkipOffered, Is.True, "the walk offers the skip");

            var (skip, paused) = await Pause(game);
            Assert.That(skip.gameObject.activeSelf, Is.True, "the pause screen shows the skip");
            Assert.That(skip.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo(ArenaText.Pair("跳过第 0 天", "SKIP DAY 0")), "labelled");
            Assert.That(paused.body.text, Does.Contain(campaign.DayZeroSkipWords), "the paused line says what it does");
            skip.onClick.Invoke();

            Assert.That(game.RunPhase, Is.Not.EqualTo(ArenaUiState.Paused), "the pause closed");
            Assert.That(stepsRecorded, Is.EqualTo(new[] { "Skip" }), "the skip is one tutorial_step record");
            Assert.That((campaign.Stage, campaign.TutorialStepNow, campaign.TutorialStepForced), Is.EqualTo((CampaignStage.Story, TutorialStep.Done, false)),
                "the walk is over, no step forced");
            Assert.That((campaign.CratesOnMap, campaign.NodesLeft(V2Campaign.NodeKind.Ore), campaign.PickupCount, campaign.VoidCrystalsPlaced),
                Is.EqualTo((0, 0, 0, (Vector3?)null)), "nothing of the walk is left on the map");
            Assert.That((campaign.Alloy, campaign.Energy, campaign.Dark), Is.EqualTo((rules.dayZeroAlloy, rules.dayZeroEnergy, rules.nodesPerCluster * rules.darkPerNode)),
                "the wallet holds what Day 0 gives");
            Assert.That(campaign.CratePanelOpen, Is.True, "the first crate's pick is open");
            var forms = campaign.CrateOfferForms;
            Assert.That(forms.Length, Is.InRange(1, RunWeapons.ArmoryOfferCount), "one to three weapons");
            Assert.That(forms.Select(RunWeapon.IsAutoForm), Is.All.False, "hand weapons only, as Day 0's crates hold");

            campaign.DebugTakeCrate(0);
            await CloseWeaponCard(game);
            Assert.That(weapons.Hand[weapons.ActiveIndex]?.form, Is.EqualTo(forms[0]), "the weapon taken is in the main hand");
            Assert.That(weapons.Hand[1 - weapons.ActiveIndex]?.form, Is.EqualTo(WeaponForm.Pistol), "and the pistol in the other");

            await Until(() => campaign.Stage == CampaignStage.Base, 45f, "the arrival opens the base");
            Assert.That(campaign.BaseAfterTutorial, Is.True, "the arrival's visit");
            Assert.That(Vector3.Distance(game.player.Position, campaign.DoorPoint), Is.LessThan(2.5f), "the player stands at the base door");
            Assert.That((campaign.Alloy, campaign.Energy, campaign.Dark), Is.EqualTo((rules.dayZeroAlloy, rules.dayZeroEnergy, rules.nodesPerCluster * rules.darkPerNode)),
                "the arrival gives nothing twice");
            await Until(() => campaign.Ui.BaseTourStepNow != BaseTourStep.None, 25f, "the base tour begins");
            Assert.That(campaign.Ui.BaseTourStepNow, Is.Not.EqualTo(BaseTourStep.Done), "the tour runs");
            Assert.That(ArenaSaves.List(SaveMode.Story)[Slot].empty, Is.True, "Day 0 writes no save");
            var exits = campaign.BaseExits();
            Assert.That(exits.Select(e => e.label), Is.EqualTo(new[] { ArenaText.Pair("出发（进入第 1 天）", "Head out (day 1 begins)") }),
                "one way out, into day 1");

            exits[0].go();
            Assert.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Day)), "leaving the base starts day 1's morning at once");
            game.player.ProtectFor(600f);
            Assert.That(campaign.LampStock, Is.EqualTo(Mathf.Max(0, rules.freeLampsPerNight - 1)), "with the lamps a finished Day 0 leaves");
            Assert.That(Vector3.Distance(game.player.Position, campaign.DoorPoint), Is.LessThan(2.5f), "at the base door");
            var morning = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((morning.empty, morning.day, morning.phase), Is.EqualTo((false, 1, SavePhase.Morning)), "day 1's morning save is in the run's slot");
            Assert.That(V2Campaign.DayZeroFinishedBefore, Is.True, "Day 0 is still remembered as finished");
            Assert.That(campaign.DayZeroSkipOffered, Is.False, "day 1 offers no skip");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task SkipOnDay0sNight_StartsDay1sDawn()
        {
            V2Campaign.SetDayZeroFinishedForTests(true);
            var game = await ReadyScene();
            var campaign = await StartStory(game);
            Assume.That(V5HudKit.On, Is.True, "the whole game's pages");
            var rules = campaign.rules;
            campaign.DebugBeginTutorialNight();
            Assume.That((campaign.Day, campaign.Stage, campaign.TutorialStepNow), Is.EqualTo((0, CampaignStage.Night, TutorialStep.Lamp)),
                "Day 0's night, at its lamp step");
            Assume.That(campaign.NodesLeft(V2Campaign.NodeKind.Dark), Is.EqualTo(rules.nodesPerCluster), "the night's dark matter is out");
            int lamps = campaign.LampStock;
            Assert.That(campaign.DayZeroSkipOffered, Is.True, "the night offers the skip");

            var (skip, paused) = await Pause(game);
            Assert.That(skip.gameObject.activeSelf, Is.True, "the pause screen shows the skip");
            Assert.That(paused.body.text, Does.Contain(campaign.DayZeroSkipWords), "the paused line says what it does");
            skip.onClick.Invoke();

            Assert.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Day)), "day 1's dawn begins at once");
            game.player.ProtectFor(600f);
            Assert.That(stepsRecorded.Count(step => step == "Skip"), Is.EqualTo(1), "the skip is one tutorial_step record");
            Assert.That((campaign.Alloy, campaign.Energy, campaign.Dark), Is.EqualTo((rules.dayZeroAlloy, rules.dayZeroEnergy, rules.nodesPerCluster * rules.darkPerNode)),
                "the wallet holds what Day 0 gives");
            Assert.That(campaign.LampStock, Is.EqualTo(Mathf.Max(lamps, rules.freeLampsPerNight - 1)), "the night's lamps not set down are kept");
            Assert.That((campaign.NodesLeft(V2Campaign.NodeKind.Dark), campaign.ShadowCount, campaign.LampCount), Is.EqualTo((0, 0, 0)),
                "the tutorial night's dark matter, shadows and lamps are gone");
            Assert.That((campaign.TutorialStepNow, campaign.TutorialStepForced), Is.EqualTo((TutorialStep.Done, false)), "no step of the tutorial is left");
            Assert.That(Vector3.Distance(game.player.Position, campaign.DoorPoint), Is.LessThan(2.5f), "the player stands at the base door");
            Assert.That(V2Campaign.DayZeroFinishedBefore, Is.True, "Day 0 is still remembered as finished");
        }

        // ───── Helpers (as CampaignBaseRedesignTest's) ─────

        /// <summary>The pause screen opened, with its skip button and its screen.</summary>
        static async Task<(UnityEngine.UI.Button skip, V2UiScreen screen)> Pause(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            var hud = Object.FindAnyObjectByType<V2Hud>();
            var paused = hud.screens.First(screen => screen && screen.state == ArenaUiState.Paused);
            var skip = paused.transform.Find("Content/" + V2Hud.PauseSkipDayZeroButton);
            Assert.That(skip, Is.Not.Null, "the whole game's pause screen has the skip's button");
            return (skip.GetComponent<UnityEngine.UI.Button>(), paused);
        }

        /// <summary>The card of the weapon just taken is closed.</summary>
        static async Task CloseWeaponCard(GameManager game)
        {
            await Until(() => game.RunPhase == ArenaUiState.WeaponIntro, 5f, "the weapon's card");
            float closing = Time.realtimeSinceStartup + 10f;
            while (game.RunPhase == ArenaUiState.WeaponIntro)
            {
                if (Time.realtimeSinceStartup > closing) Assert.Fail("the weapon card never closed");
                game.ExecuteUiCommand(ArenaUiCommand.DismissWeaponIntro, -1);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>The story mode from the title, up to Day 0's walk.</summary>
        static async Task<V2Campaign> StartStory(GameManager game)
        {
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            await CommandUntil(game, ArenaUiCommand.StartCampaign, () => game.CampaignRunning);
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
            return campaign;
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
