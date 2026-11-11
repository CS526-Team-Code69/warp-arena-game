using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>What a weapon crate offers is recorded as its panel opens (V2Campaign.Crates.cs): one crate_offer record for each weapon
    /// on the panel, "&lt;crate kind&gt;:&lt;weapon form&gt;" with its place, 1 to 3, on the campaign's day. The crate's own record follows when
    /// the player decides and names one of the weapons offered, so the sheet can work out how often a weapon was taken when it was on
    /// offer. The afternoon is opened by the campaign's own call for checks, so the test need not play a morning, and the crate is opened
    /// with the campaign's stand-in for the E key. The network is replaced by RunAnalytics.Transport, so nothing leaves the machine.</summary>
    [TestFixture]
    public class CampaignCrateOfferTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        // Where the player stands to open the crate, from the crate: on it first, then a step to each side. Ore is kept further from
        // a crate than mining reaches, so on the crate no ore takes the E; the other spots are there in case that one is not open ground.
        static readonly Vector3[] Spots = { Vector3.zero, new(1.2f, 0f, 0f), new(-1.2f, 0f, 0f), new(0f, 0f, 1.2f), new(0f, 0f, -1.2f) };
        readonly List<string> sent = new();
        readonly List<(string kind, string detail, float value, int day)> raised = new();

        [SetUp]
        public void SetUp()
        {
            sent.Clear();
            raised.Clear();
            RunAnalytics.Transport = sent.Add;
            CampaignEvents.Milestone += Note;
        }

        [TearDown]
        public void TearDown()
        {
            CampaignEvents.Milestone -= Note;
            RunAnalytics.Transport = null;
            Time.timeScale = 1f;
        }

        void Note(string kind, string detail, float value, int day)
        {
            if (kind == CampaignEvents.MilestoneCrate || kind == CampaignEvents.MilestoneCrateOffer) raised.Add((kind, detail, value, day));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task Crate_PanelOpens_RecordsEachWeaponOnOffer_ThenTheOneTaken()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
            // The rows are sent only while a run is open. GameManager opens it once the starting weapon's card is closed; what comes
            // before that belongs to the campaign's opening and changes with it, so the test opens the run itself.
            ListenToCampaign();
            RunAnalytics.RunStarted(game);

            campaign.DebugBeginAfternoon(1);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            Assume.That(campaign.TryNearestCrate(game.player.Position, out var crateAt), Is.True, "the afternoon put its crate on the ore trail");
            int cratesBefore = campaign.CratesOnMap;
            Assert.That(raised, Is.Empty, "a crate lying on the map has offered nothing yet");
            Assert.That(campaign.CrateOfferForms, Is.Empty, "no panel is open");

            var map = WorldMap.Active;
            float opening = Time.realtimeSinceStartup + 20f;
            for (int frame = 0; !campaign.CratePanelOpen; frame++)
            {
                if (Time.realtimeSinceStartup > opening)
                    Assert.Fail($"the crate never opened (stage {campaign.Stage}, phase {game.RunPhase}, crates on the map {campaign.CratesOnMap})");
                // Should the walk to the crate bring the black hole into sight, its first-sight cutscene is ended at once.
                if (map && map.HoleIntroPlaying) map.SkipHoleIntro();
                if (frame % 20 == 0) campaign.DebugPlacePlayer(crateAt + Spots[frame / 20 % Spots.Length]);
                campaign.debugPressE = true;
                await Awaitable.NextFrameAsync();
            }
            campaign.debugPressE = false;

            var forms = campaign.CrateOfferForms;
            Assert.That(forms.Length, Is.InRange(1, RunWeapons.ArmoryOfferCount), "the panel shows one to three weapons");
            Assert.That(forms, Is.Unique, "each a different weapon");
            Assert.That(raised.Select(r => r.kind), Has.All.EqualTo(CampaignEvents.MilestoneCrateOffer), "only offers so far: nothing is taken yet");
            Assert.That(raised.Select(r => (r.detail, r.value, r.day)), Is.EqualTo(forms.Select((form, i) => ("Afternoon:" + form, (float)(i + 1), 1))),
                "one record for each weapon on the panel: the crate kind and the weapon's form, its place, the campaign's day");
            Assert.That(campaign.Day, Is.EqualTo(1), "the day the records carry is the campaign's");

            for (int i = 0; i < 5; i++) await Awaitable.NextFrameAsync();
            Assert.That(raised, Has.Count.EqualTo(forms.Length), "an open panel records its weapons once, not every frame");

            // The last weapon on the panel is taken, so its place is not 1 unless the panel holds a single weapon.
            int pick = forms.Length - 1;
            campaign.DebugTakeCrate(pick);
            Assert.That(campaign.CratePanelOpen, Is.False, "the panel is closed");
            Assert.That(raised, Has.Count.EqualTo(forms.Length + 1), "taking a weapon records the crate's own row and no further offer");
            Assert.That(raised[raised.Count - 1], Is.EqualTo((CampaignEvents.MilestoneCrate, "Afternoon:" + forms[pick], (float)(pick + 1), 1)),
                "the crate's own record names the weapon taken, with the place its offer had");
            Assert.That(campaign.CratesOnMap, Is.EqualTo(cratesBefore - 1), "the crate is gone, so its panel cannot open and offer again");

            var rows = sent.Select(json => JsonUtility.FromJson<RunAnalytics.V2Event>(json))
                .Where(e => e.@event == "v2_event" && (e.kind == CampaignEvents.MilestoneCrate || e.kind == CampaignEvents.MilestoneCrateOffer))
                .ToList();
            Assert.That(rows.Select(r => (r.kind, r.detail, r.value)), Is.EqualTo(raised.Select(r => (r.kind, r.detail, (double)r.value))),
                "each record is one v2_event row, in the same order");
            Assert.That(rows.Select(r => (r.run_id, r.day, r.stage)).Distinct().Count(), Is.EqualTo(1),
                "the offers and the crate's own row share the run, the day they count in and the stage");
            Assert.That(rows[0].stage, Is.EqualTo("dusk"), "an afternoon crate is opened in the afternoon");
        }

        // RunAnalytics listens to the campaign's record points from the start of a play; the test makes sure of it, as RunAnalyticsTest does.
        static void ListenToCampaign()
        {
            var listen = typeof(RunAnalytics).GetMethod("ListenToCampaign", BindingFlags.NonPublic | BindingFlags.Static);
            Assume.That(listen, Is.Not.Null, "RunAnalytics.ListenToCampaign");
            listen.Invoke(null, null);
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
