using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// The story's morning as it is played, for the two faults that left a morning unfinished: a raider that reached the edge of a
    /// wall's margin stood there for good (here the ship's end, which every run has beside the base), and the screen-edge marker of a
    /// monster far toward the bottom of the screen stood at the opposite edge. Both tests read only what the game shows, so they fail
    /// on the game as it was.
    /// </summary>
    [TestFixture]
    public class StuckMonsterStoryTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [TestCase(EnemyKind.Brute, 12f, 22f), TestCase(EnemyKind.Grunt, 12f, 22f), TestCase(EnemyKind.Brute, -13f, 21f), TestCase(EnemyKind.Stomper, 16f, 17f)]
        [LoadScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task Raider_SentPastTheShipsEnd_KeepsWalkingAndStrikesTheBase(EnemyKind kind, float x, float z)
        {
            var (game, campaign) = await StartStory();
            campaign.DebugBeginNight(0, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            campaign.EndNight(false);
            await Until(() => campaign.Stage == CampaignStage.Day && game.IsCombatActive, 30f, "day 1's morning");
            game.player.Invulnerable = true;
            var data = game.waves.DefinitionOf(kind);
            Assume.That(data, Is.Not.Null, $"the roster has a {kind}");

            // North of the ship, to one side: the way to the base's north wall leads round the ship's end.
            Vector3 start = V2Campaign.FindOpen(V2Campaign.BaseCentre + new Vector3(x, 0f, z), data.radius + .3f);
            var raider = game.waves.SpawnCampaign(kind, start);
            Assume.That(raider && raider.Raider, Is.True, "a raider was set down");
            Assume.That(Query.Flat(game.player.Position - start).magnitude, Is.GreaterThan(Enemy.RaidAggroRadius + 6f), "the player is out of its sight");

            Time.timeScale = 3f;
            Vector3 anchor = raider.Position, stoodAt = anchor;
            float clock = 0f, anchorAt = 0f, longestStill = 0f;
            while (clock < 45f && raider.IsAlive && raider.AttackSequence == 0)
            {
                game.player.Heal(game.player.maxHp);
                if (Query.Flat(raider.Position - anchor).magnitude > .5f) { anchor = raider.Position; anchorAt = clock; }
                else if (clock - anchorAt > longestStill) { longestStill = clock - anchorAt; stoodAt = raider.Position; }
                clock += game.WorldDeltaTime;
                await Awaitable.NextFrameAsync();
            }
            Assert.That(raider.IsAlive, Is.True, "nothing killed the raider meanwhile");
            Assert.That(raider.AttackSequence, Is.GreaterThan(0),
                $"the {kind} set down at {start} reaches the base and strikes it within 45 seconds; it stands at {raider.Position}, and stood still for {longestStill:0.0} s at {stoodAt}");
            Assert.That(longestStill, Is.LessThan(4f), $"on its way the {kind} never stood still for long (it did at {stoodAt})");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Marker_OfAMonsterFarBelowTheScreen_StandsOnItsSide()
        {
            var (game, campaign) = await StartStory();
            var directions = Object.FindAnyObjectByType<V2EnemyDirections>(FindObjectsInactive.Include);
            Assume.That(directions && directions.cameraSource && directions.cameraSource.orthographic, Is.True, "the markers follow the game's orthographic camera");
            var cam = directions.cameraSource;

            game.player.Invulnerable = true;
            campaign.DebugPlacePlayer(new Vector3(30f, 0f, 30f));
            // Toward the bottom of the screen on the ground: against the camera's heading.
            Vector3 down = -Query.Flat(cam.transform.forward).normalized;
            Vector3 at = V2Campaign.FindOpen(game.player.Position + down * 64f, .7f);
            var monster = game.waves.SpawnCampaign(EnemyKind.Grunt, at);
            Assume.That(monster, Is.Not.Null, "a monster was set down");
            monster.enabled = false; // it stands where it is
            // The view settles on the player; the markers refresh ten times a second.
            for (float until = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < until;) await Awaitable.NextFrameAsync();

            Vector3 seen = monster.Position - cam.transform.position;
            var truth = new Vector2(Vector3.Dot(seen, cam.transform.right), Vector3.Dot(seen, cam.transform.up));
            Assume.That(Vector3.Dot(seen, cam.transform.forward), Is.LessThan(0f), "the monster stands behind the camera's plane");
            Vector2 middle = new(Screen.width * .5f, Screen.height * .5f), shown = Vector2.zero;
            int markers = 0;
            foreach (var view in directions.indicators)
            {
                if (view == null || !view.root || !view.root.gameObject.activeInHierarchy) continue;
                markers++;
                shown = RectTransformUtility.WorldToScreenPoint(null, view.root.position) - middle;
            }
            Assert.That(markers, Is.EqualTo(1), "one monster off the screen, one marker");
            Assert.That(Vector2.Angle(shown, truth), Is.LessThan(45f),
                $"the monster lies {Mathf.Atan2(truth.y, truth.x) * Mathf.Rad2Deg:0} degrees from the screen's middle; its marker stands at {Mathf.Atan2(shown.y, shown.x) * Mathf.Rad2Deg:0}");
        }

        static async Task<(GameManager, V2Campaign)> StartStory()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 20f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 30f, "Day 0's walk begins");
            return (game, campaign);
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for " + operation + ".");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
