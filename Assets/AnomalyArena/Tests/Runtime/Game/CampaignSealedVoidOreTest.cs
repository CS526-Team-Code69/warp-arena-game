using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The sealed void ore on the black hole's line (V2Campaign.Guide.cs): a few pieces lie along the line from the base to the
    /// hole, outside its pull, and show with the hole; nothing there can be mined while the Nash Prophet stands. Once the Prophet is
    /// beaten the pieces go and the afternoon's void alloy grows on the same spots (V2Campaign.Void.cs AfternoonVoidOre). The afternoon is
    /// opened by the campaign's own call for checks, so the test need not play a morning.</summary>
    [TestFixture]
    public class CampaignSealedVoidOreTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task SealedOre_LiesOnTheHolesLine_ShowsWithIt_ThenTheVoidAlloyGrowsThereOnceTheProphetFalls()
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

            campaign.DebugBeginAfternoon(1);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            var map = WorldMap.Active;
            var hole = map ? map.CampaignHole : null;
            Assume.That(hole, Is.Not.Null, "the campaign's map has its black hole");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();

            var spots = campaign.SealedOre.ToList();
            Vector3 toHole = Query.Flat(hole.Centre - V2Campaign.BaseCentre);
            float distance = toHole.magnitude;
            Vector3 dir = toHole / distance;
            Assert.That(spots.Count, Is.InRange(3, 7), $"a few pieces lie on the line (the hole is {distance:0} cells from the base)");
            float last = 0f;
            foreach (var spot in spots)
            {
                Vector3 rel = Query.Flat(spot - V2Campaign.BaseCentre);
                float along = Vector3.Dot(rel, dir), off = (rel - dir * along).magnitude;
                Assert.That(off, Is.LessThanOrEqualTo(1.7f), $"{spot} lies on the line to the hole");
                Assert.That(along, Is.GreaterThan(last), "each piece lies further toward the hole than the one before");
                Assert.That(Query.Flat(spot - hole.Centre).magnitude, Is.GreaterThan(hole.GravityRadius + 1f), $"{spot} lies outside the hole's pull");
                Assert.That(map.IsWalkable(spot, 1f), Is.True, $"{spot} lies on open ground");
                last = along;
            }
            Assert.That(campaign.SealedOreShown, Is.EqualTo(!map.CampaignHoleHidden), "the pieces show exactly when the hole does");
            Assert.That(campaign.NodesLeft(V2Campaign.NodeKind.VoidAlloy), Is.Zero, "while the Prophet stands nothing on the line can be mined");

            campaign.DebugBeatProphet();
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(campaign.SealedOre.Count, Is.Zero, "with the Prophet beaten the seal is broken and the sealed pieces go");
            Assert.That(campaign.SealedOreShown, Is.False, "nothing sealed shows any more");

            campaign.DebugAfternoonVoidOre();
            int grown = campaign.NodesLeft(V2Campaign.NodeKind.VoidAlloy);
            Assert.That(grown, Is.InRange(1, spots.Count), "the afternoon's void alloy grows on the line");
            foreach (var spot in spots)
                if (campaign.TryNearestNode(V2Campaign.NodeKind.VoidAlloy, spot, out var at))
                    Assert.That(Query.Flat(at - spot).magnitude, Is.LessThanOrEqualTo(5f).Or.GreaterThan(9f),
                        "each node stands on or right beside a sealed piece's spot (the far ones belong to other spots)");
            Assert.That(spots.Count(spot => campaign.TryNearestNode(V2Campaign.NodeKind.VoidAlloy, spot, out var at) && Query.Flat(at - spot).magnitude <= 5f),
                Is.EqualTo(grown), "every node grew at a sealed piece's spot");
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
