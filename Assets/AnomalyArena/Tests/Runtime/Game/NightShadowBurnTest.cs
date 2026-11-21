using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Light burns a night shadow (V2Campaign.Shadows.cs): a shadow caught in a lamp's light leaves it by the nearest edge at
    /// least as fast as it hunts, keeps what the light took once it is out in the dark, and dissolves once the light has taken
    /// rules.shadowLightSeconds from it in all. Day 1's night is opened by hand (V2Campaign.DebugBeginNight), so the test does not play a
    /// day; one shadow is raised far from the base (DebugAddShadow) and lamps are set down on it one after another (DebugPlaceLamp).
    /// Shadows gathering at the lamp the player stands in: the goal panel's note names them from rules.lampCrowdNotice on, and a full
    /// crowd waits rules.lampCrowdWarning seconds at the edge before it goes in.</summary>
    [TestFixture]
    public class NightShadowBurnTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task ShadowCaughtInALampsLight_LeavesByTheNearestEdge_AndDissolvesAfterTheLightSecondsInAll()
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
            campaign.DebugBeginNight(1, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Night), "day 1's night has begun");

            var rules = campaign.rules;
            // A new run's lamps light rules.lampRadius (the ship's night light, which widens them, is not built yet).
            float radius = rules.lampRadius, lightSeconds = rules.shadowLightSeconds;
            float leaveSeconds = radius / rules.ShadowChaseSpeed(game.player.moveSpeed);
            // Well clear of the base's light, where the player stands at the base door.
            var shadow = campaign.DebugAddShadow(V2Campaign.BaseCentre + new Vector3(rules.baseLightRadius + 14f, 0f, 0f));
            await Awaitable.NextFrameAsync();
            var (there, position, burned) = shadow();
            Assume.That(there && burned == 0f, Is.True, "the shadow stands in the dark, untouched by light");

            int lamps = 0;
            float taken = 0f;
            while (there)
            {
                if (lamps >= 12) Assert.Fail($"twelve lamps were set down on the shadow and it is still there, with {burned:0.00} s of light taken");
                Assert.That(burned, Is.LessThan(lightSeconds), "a shadow still there has taken less than the light seconds");
                Vector3 lamp = Query.Flat(position);
                campaign.DebugPlaceLamp(lamp);
                lamps++;
                float setAt = Time.time;
                // It leaves the new light straight out by the nearest edge, at least as fast as it hunts.
                while (true)
                {
                    await Awaitable.NextFrameAsync();
                    (there, position, burned) = shadow();
                    if (!there || Query.Flat(position - lamp).magnitude >= radius - .1f) break;
                    if (Time.time - setAt > leaveSeconds + .5f)
                        Assert.Fail($"after {Time.time - setAt:0.00} s the shadow still stands {Query.Flat(position - lamp).magnitude:0.0} cells inside a lamp's light of radius {radius:0.0}");
                }
                if (!there) break;
                Assert.That(burned, Is.GreaterThan(taken), $"lamp {lamps}: the light took something from the shadow");
                Assert.That(burned - taken, Is.LessThanOrEqualTo(leaveSeconds + .2f), $"lamp {lamps}: it burned only while it made for the edge");
                taken = burned;

                // Out in the dark for a while: the light's toll stays.
                for (int i = 0; i < 20; i++) await Awaitable.NextFrameAsync();
                (there, position, burned) = shadow();
                if (!there) break;
                Assert.That(burned, Is.GreaterThanOrEqualTo(taken), $"after lamp {lamps}: nothing comes back in the dark");
                taken = burned;
            }

            Assert.That(burned, Is.GreaterThanOrEqualTo(lightSeconds), "it dissolved only once the light had taken the light seconds from it");
            Assert.That(lamps, Is.GreaterThan(1), "one lamp's light alone does not burn away a shadow that leaves it at once");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task ShadowsGatheringAtThePlayersLamp_TheNoteCountsThemFromThree_AndAFullCrowdWaitsTheWarningBeforeGoingIn()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            // A copy of the rules without the night's ambient shadows, so the only shadows are the test's own.
            var original = campaign.rules;
            var rulesCopy = UnityEngine.Object.Instantiate(original);
            rulesCopy.ambientShadows = 0f;
            rulesCopy.ambientShadowsPerDay = 0f;
            campaign.rules = rulesCopy;
            try
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!game.CampaignRunning)
                {
                    if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                    game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                    await Awaitable.NextFrameAsync();
                }
                await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
                campaign.DebugBeginNight(1, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
                Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Night), "day 1's night has begun");
                game.player.ProtectFor(600f);

                var rules = campaign.rules;
                int full = rules.overrunShadows;
                Assume.That(rules.lampCrowdNotice, Is.EqualTo(3).And.LessThan(full), "the note names three of a crowd of five");
                // The player well clear of the base's light (on the nearest open ground) and a lamp where the player stands; the shadows
                // rise just outside the lamp's edge, all round it.
                campaign.DebugPlacePlayer(V2Campaign.BaseCentre + new Vector3(rules.baseLightRadius + 16f, 0f, 0f));
                await Awaitable.NextFrameAsync();
                Vector3 lamp = Query.Flat(game.player.Position);
                campaign.DebugPlaceLamp(lamp);
                await Awaitable.NextFrameAsync();
                Assume.That(campaign.DebugLampState(0, out int none, out _) && none == 0, Is.True, "one lamp, no shadow at it");
                float edge = rules.lampRadius + 1f;
                int raised = 0;
                void Raise(int count)
                {
                    for (int i = 0; i < count; i++, raised++)
                    {
                        float a = raised * Mathf.PI * 2f / full;
                        campaign.DebugAddShadow(lamp + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * edge);
                    }
                }
                string Note(int gathered) => ArenaText.Pair($"灯外聚了 {gathered}/{full} 只影子，聚满会冲进来灭灯。",
                    $"{gathered} of {full} shadows have gathered at this lamp. At {full} they rush in and put it out.");

                Raise(2);
                await Until(() => campaign.DebugLampState(0, out int c, out _) && c == 2, 3f, "two shadows gather at the lamp");
                Assert.That(campaign.CurrentNote(), Does.Not.Contain(Note(2)), "two are not named yet");

                Raise(1);
                await Until(() => campaign.DebugLampState(0, out int c, out _) && c == 3, 3f, "a third gathers");
                Assert.That(campaign.CurrentNote(), Is.EqualTo(Note(3)), "three of five are named, with what a full crowd does");

                Raise(full - 3);
                await Until(() => campaign.DebugLampState(0, out int c, out _) && c >= full, 3f, "a full crowd gathers");
                float crowdAt = Time.time;
                Assert.That(campaign.CurrentNote(), Is.EqualTo(Note(full)), "the full crowd is named while it waits at the edge");
                // Half a second of slack for frames the test and the campaign see apart; the old warning of 1.5 s stays well outside it.
                while (Time.time - crowdAt < rules.lampCrowdWarning - .5f)
                {
                    await Awaitable.NextFrameAsync();
                    Assert.That(campaign.DebugLampState(0, out _, out bool early) && !early, Is.True,
                        $"{Time.time - crowdAt:0.00} s after the crowd gathered it still waits at the edge ({rules.lampCrowdWarning} s of warning)");
                }
                await Until(() => campaign.DebugLampState(0, out _, out bool goneIn) && goneIn, 2f, "the crowd goes in once its warning is over");
                Assert.That(Time.time - crowdAt, Is.GreaterThanOrEqualTo(rules.lampCrowdWarning - .5f), "not before the warning was over");
                Assert.That(campaign.CurrentNote(), Does.Not.Contain(Note(full)), "a lamp the crowd went into shelters no more: the note stops counting");
            }
            finally
            {
                campaign.rules = original;
                UnityEngine.Object.DestroyImmediate(rulesCopy);
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
