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

        /// <summary>Since 2026-11-24 a raider the player hits from beyond raiderAggroRadius comes for the player, and goes back to the base
        /// once raiderProvokeSeconds have passed with the player still far away.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Raider_HitFromAfar_ComesForThePlayer_ThenGoesBackToTheBase()
        {
            var (game, campaign) = await StartStory();
            campaign.DebugBeginNight(0, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            campaign.EndNight(false);
            await Until(() => campaign.Stage == CampaignStage.Day && game.IsCombatActive, 30f, "day 1's morning");
            game.player.Invulnerable = true;
            Assume.That(Enemy.RaidProvokeSeconds, Is.GreaterThan(0f), "the rules turn a raider the player hits");

            // South-east of the base, the raider further east: it walks west to the base, away from nothing it could strike.
            campaign.DebugPlacePlayer(V2Campaign.FindOpen(V2Campaign.BaseCentre + new Vector3(30f, 0f, -25f), .8f));
            Vector3 start = V2Campaign.FindOpen(game.player.Position + new Vector3(Enemy.RaidAggroRadius + 14f, 0f, 0f), 1f);
            var raider = game.waves.SpawnCampaign(EnemyKind.Brute, start);
            Assume.That(raider && raider.Raider, Is.True, "a raider was set down");
            Assume.That(Query.Flat(game.player.Position - raider.Position).magnitude, Is.GreaterThan(Enemy.RaidAggroRadius + Enemy.RaidAggroRelease + 2f), "the player is out of its sight");
            float settle = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < settle) await Awaitable.NextFrameAsync();
            Assert.That(raider.RaidTurnedOnPlayer, Is.False, "left alone, it raids the base");

            // A hit makes the monster flinch, and it thinks again once the flinch is over.
            float hitAt = Time.time;
            Assert.That(raider.ReceiveDamage(new DamageInfo { amount = .5f, dealer = game.player, source = DamageSource.Hit }), Is.True, "the hit lands");
            float turning = Time.realtimeSinceStartup + 3f;
            while (raider.IsAlive && !raider.RaidTurnedOnPlayer && Time.realtimeSinceStartup < turning) await Awaitable.FixedUpdateAsync();
            Assume.That(raider.IsAlive, Is.True, "the hit did not kill it");
            Assert.That(raider.RaidTurnedOnPlayer, Is.True, "hit from afar, it comes for the player");

            // The player goes far to the north: once the time since the hit is up, the raider goes back to the base.
            campaign.DebugPlacePlayer(V2Campaign.FindOpen(V2Campaign.BaseCentre + new Vector3(30f, 0f, 40f), .8f));
            Time.timeScale = 3f;
            while (raider.IsAlive && raider.RaidTurnedOnPlayer && Time.time - hitAt < Enemy.RaidProvokeSeconds + 4f) await Awaitable.NextFrameAsync();
            float released = Time.time - hitAt;
            Assert.That(raider.RaidTurnedOnPlayer, Is.False, $"with the player far away {Enemy.RaidProvokeSeconds} s after the hit, it goes back to the base (still chasing {released:0.0} s after it)");
            Assert.That(released, Is.GreaterThan(Enemy.RaidProvokeSeconds - .5f), $"but not before its time was up (it went back {released:0.0} s after the hit)");
        }

        /// <summary>Since 2026-11-24 the first morning's goal says what its countdown is, and once the countdown is up the goal says no
        /// more monsters are coming and counts those left.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Morning_OnceItsTimeIsUp_TheGoalAsksForTheMonstersLeft()
        {
            var (game, campaign) = await StartStory();
            float seconds = campaign.rules.morningSeconds;
            try
            {
                campaign.rules.morningSeconds = 6f;
                campaign.DebugBeginNight(0, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
                campaign.EndNight(false);
                await Until(() => campaign.Stage == CampaignStage.Day && game.IsCombatActive, 30f, "day 1's morning");
                game.player.Invulnerable = true;
                Assert.That(campaign.ObjectiveText, Does.Contain(ArenaText.Pair("倒计时结束前", "Until the countdown ends")), "the first morning says what its countdown is");

                var monster = game.waves.SpawnCampaign(EnemyKind.Brute, V2Campaign.FindOpen(V2Campaign.BaseCentre + new Vector3(40f, 0f, 0f), 1f));
                Assume.That(monster, Is.Not.Null, "a monster stands to be cleared");
                Time.timeScale = 3f;
                await Until(() => game.waves.TimeExpired, 40f, "the morning's countdown to run out");
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                Assume.That(game.waves.LivingCount, Is.GreaterThan(0), "monsters are left");
                Assert.That(campaign.ObjectiveText, Does.Contain(ArenaText.Pair("不会再来新的怪物了", "No more monsters are coming")), "once the time is up the goal says no more are coming");
                Assert.That(campaign.ObjectiveText, Does.Contain(game.waves.LivingCount.ToString()), "and counts those left");
            }
            finally { campaign.rules.morningSeconds = seconds; }
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
