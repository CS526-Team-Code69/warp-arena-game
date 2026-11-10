using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Music by stage (CampaignMusic, 2026-11-06): a V2 campaign run plays its own tracks, loaded from StreamingAssets/Music,
    /// in place of the combat music. The tracks are local files that are not in the repository, so the test is skipped without them.</summary>
    [TestFixture]
    public class CampaignMusicTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task CampaignStart_PlaysTheDayTrack_AndSilencesTheCombatMusic()
        {
            if (!File.Exists(Path.Combine(Application.streamingAssetsPath, "Music", "day.mp3"))) Assert.Ignore("no day.mp3 in StreamingAssets/Music");
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            await Until(() => CampaignMusic.Instance && CampaignMusic.Instance.Loaded(CampaignMusic.Track.Day), 10f, "day.mp3 is loaded");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => CampaignMusic.Instance.Current == CampaignMusic.Track.Day, 5f, "the day track plays once the campaign starts");
            Assert.That(CampaignMusic.Overrides, Is.True, "the campaign's track has the music channel");
            for (int i = 0; i < 10; i++) await Awaitable.NextFrameAsync();
            var audio = V3WorldAudio.Instance;
            Assume.That(audio && audio.musicSource, Is.True, "the scene has its music channel");
            Assert.That(audio.musicSource.isPlaying && audio.musicSource.volume > 0f, Is.False, "the combat music stays silent meanwhile");
        }

        /// <summary>camera-P45 (2026-11-10): a campaign run lost by day goes to its result screen with the ending playing, not silence.
        /// V2Campaign.Finish turns the stage Off once the run is over, which used to hand the music back to the silent combat track.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task CampaignLostByDay_ResultScreen_PlaysTheEnding()
        {
            string music = Path.Combine(Application.streamingAssetsPath, "Music");
            if (!File.Exists(Path.Combine(music, "day.mp3"))) Assert.Ignore("no day.mp3 in StreamingAssets/Music");
            if (!File.Exists(Path.Combine(music, "ending.mp3")) && !File.Exists(Path.Combine(music, "title.mp3")))
                Assert.Ignore("neither ending.mp3 nor title.mp3 (which the ending borrows) in StreamingAssets/Music");
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            await Until(() => CampaignMusic.Instance && CampaignMusic.Instance.Loaded(CampaignMusic.Track.Day) && CampaignMusic.Instance.Loaded(CampaignMusic.Track.Ending),
                10f, "day.mp3 and the ending's track are loaded");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            // Day 0's walk: a death there is not the campaign's to catch (V2Campaign.CatchesPlayerDeath), so it ends the run as a death in
            // the morning does.
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
            await Until(() => CampaignMusic.Instance.Current == CampaignMusic.Track.Day, 5f, "the day track plays on the walk");
            Assume.That(campaign.CatchesPlayerDeath(), Is.False, "a death now ends the run");
            // Wall damage is not an attack, so hit protection does not block it.
            Assert.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player dies");
            await Until(() => game.RunPhase == ArenaUiState.Result && game.State == GameState.Lost, 5f, "the run ends on its result screen");
            await Until(() => campaign.Stage == CampaignStage.Off, 5f, "the campaign has finished");

            await Until(() => CampaignMusic.Instance.Current == CampaignMusic.Track.Ending, 5f, "the result screen plays the ending");
            for (int i = 0; i < 10; i++) await Awaitable.NextFrameAsync();
            Assert.That(CampaignMusic.Instance.Current, Is.EqualTo(CampaignMusic.Track.Ending), "the ending stays on the result screen");
            Assert.That(CampaignMusic.Overrides, Is.True, "the ending has the music channel, not the silent combat track");
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
