using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class CampaignMusicTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

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
