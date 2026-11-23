using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The afternoon's packs for the black hole are born on the map (V2Campaign.SpawnLurePack). A pack comes out behind the
    /// player as seen from the hole, and the hole always stands in a corner of the map: with the player on the hole's far side, "behind
    /// the player" used to lie past the map's side. The pack was born there, stood outside the map's wall for good, and filled the
    /// number the rules allow alive at once, so no further pack came. The afternoon is opened by the campaign's own call for checks and
    /// the hole's first-sight cutscene is ended at once; the player is set down between the hole and the nearest side of the map.</summary>
    [TestFixture]
    public class HoleLurePackPlaceTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task PlayerBetweenTheHoleAndTheMapsSide_ThePacksAreBornOnTheMap()
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

            campaign.DebugBeginAfternoon(2);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            var map = WorldMap.Active;
            var hole = map ? map.CampaignHole : null;
            Assume.That(hole, Is.Not.Null, "the campaign's map has its black hole");
            Assume.That(campaign.HoleFeedGoal, Is.GreaterThan(1), "the rules ask for the hole to be fed");

            // The hole is met first (its packs come only after that); the cutscene is ended at once.
            campaign.DebugPlacePlayer(hole.Centre + new Vector3(-(hole.GravityRadius + 7f), 0f, 0f) * Mathf.Sign(hole.Centre.x));
            float sight = Time.realtimeSinceStartup + 20f;
            while (!campaign.HoleSeen)
            {
                if (Time.realtimeSinceStartup > sight) Assert.Fail("the black hole was never met");
                if (map.HoleIntroPlaying) map.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }

            // Between the hole and the side of the map nearest to it, three cells inside that side and outside the hole's pull.
            Bounds b = map.Bounds;
            Vector3 c = hole.Centre;
            float toSideX = b.extents.x - Mathf.Abs(c.x), toSideZ = b.extents.z - Mathf.Abs(c.z);
            Vector3 toSide = toSideX <= toSideZ ? new Vector3(Mathf.Sign(c.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(c.z));
            float toNearest = Mathf.Min(toSideX, toSideZ);
            Assume.That(toNearest - 3f, Is.GreaterThan(hole.GravityRadius + 1f).And.LessThan(campaign.rules.holeFightRadius),
                $"the hole stands near enough to a side of the map for the player to stand between the two ({toNearest:0.0} cells; pull {hole.GravityRadius:0.0}, the afternoon's fight reaches {campaign.rules.holeFightRadius:0.0})");
            Vector3 stand = V2Campaign.FindOpen(c + toSide * (toNearest - 3f), .8f, 4f);
            game.player.ProtectFor(60f);
            campaign.DebugPlacePlayer(stand);

            // Three packs' time: every monster that comes is one of the hole's (the afternoon has no others).
            var seen = new Dictionary<Enemy, Vector3>();
            float until = Time.realtimeSinceStartup + campaign.rules.holeLureInterval * 2f + 6f;
            while (Time.realtimeSinceStartup < until)
            {
                game.player.ProtectFor(5f);
                if (Query.Flat(game.player.Position - stand).magnitude > 2.5f) campaign.DebugPlacePlayer(stand);
                foreach (var enemy in game.waves.Alive)
                    if (enemy && enemy.IsAlive && enemy.gameObject.activeInHierarchy && !seen.ContainsKey(enemy)) seen[enemy] = enemy.Position;
                await Awaitable.NextFrameAsync();
            }
            Assert.That(seen.Count, Is.GreaterThan(0), $"packs came for a player standing at {P(stand)}, {Query.Flat(stand - c).magnitude:0.0} cells from the hole at {P(c)} (lured alive {campaign.HoleLureAlive})");
            var outside = seen.Where(pair => !map.Contains(pair.Value, 0f) || pair.Key && pair.Key.IsAlive && !map.Contains(pair.Key.Position, 0f))
                .Select(pair => $"{P(pair.Value)}{(pair.Key && pair.Key.IsAlive ? " now " + P(pair.Key.Position) : "")}").ToList();
            Assert.That(outside, Is.Empty,
                $"{outside.Count} of {seen.Count} of the hole's monsters were born past the map's side (the map is {b.size.x:0} x {b.size.z:0}, the player stood at {P(stand)}, the hole at {P(c)}): {string.Join(", ", outside)}");
        }

        static string P(Vector3 p) => $"({p.x:0.0}, {p.z:0.0})";

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
