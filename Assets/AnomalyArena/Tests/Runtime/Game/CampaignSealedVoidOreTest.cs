using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Void ore before and after the Nash Prophet (V2Campaign.Guide.cs, V2Campaign.Void.cs). While it stands, a few sealed pieces
    /// lie on the line from the base to the black hole, outside its pull, and show with the hole; each afternoon also lays the day's count
    /// of sealed pieces at random across the map, new ones each afternoon; nothing sealed can be mined. Once the Prophet is beaten every
    /// sealed piece goes, and each afternoon's void alloy lies at random across the map, not on the hole's line. The afternoon is opened by
    /// the campaign's own call for checks, so the test need not play a morning.</summary>
    [TestFixture]
    public class CampaignSealedVoidOreTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        // Further than this many cells from the line between the base and the hole counts as off the hole's way.
        const float OffTheWay = 6f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task SealedOre_GuidesToTheHoleAndLiesAcrossTheMap_ThenTheVoidAlloyLiesAcrossTheMapOnceTheProphetFalls()
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
            int count = campaign.rules.voidOreNodes;
            Assume.That(count, Is.GreaterThan(0), "the rules lay void ore");

            // The hole's guide: a few sealed pieces along the line, each further toward the hole.
            var spots = campaign.SealedOre.ToList();
            Vector3 toHole = Query.Flat(hole.Centre - V2Campaign.BaseCentre);
            float distance = toHole.magnitude;
            Vector3 dir = toHole / distance;
            float OffLine(Vector3 at)
            {
                Vector3 rel = Query.Flat(at - V2Campaign.BaseCentre);
                return (rel - dir * Vector3.Dot(rel, dir)).magnitude;
            }
            Assert.That(spots.Count, Is.InRange(3, 7), $"a few pieces lie on the line (the hole is {distance:0} cells from the base)");
            float last = 0f;
            foreach (var spot in spots)
            {
                float along = Vector3.Dot(Query.Flat(spot - V2Campaign.BaseCentre), dir);
                Assert.That(OffLine(spot), Is.LessThanOrEqualTo(1.7f), $"{spot} lies on the line to the hole");
                Assert.That(along, Is.GreaterThan(last), "each piece lies further toward the hole than the one before");
                Assert.That(Query.Flat(spot - hole.Centre).magnitude, Is.GreaterThan(hole.GravityRadius + 1f), $"{spot} lies outside the hole's pull");
                Assert.That(map.IsWalkable(spot, 1f), Is.True, $"{spot} lies on open ground");
                last = along;
            }
            Assert.That(campaign.SealedOreShown, Is.EqualTo(!map.CampaignHoleHidden), "the line's pieces show exactly when the hole does");

            // Sealed pieces across the map, new ones each afternoon; none can be mined.
            var pieces = campaign.SealedMapOre.ToList();
            Assert.That(pieces.Count, Is.EqualTo(count), "the afternoon lays the day's count of sealed pieces across the map");
            Assert.That(pieces.Count(p => OffLine(p) > OffTheWay), Is.GreaterThan(0), "they lie across the map, not only on the hole's way");
            foreach (var p in pieces) Assert.That(map.IsWalkable(p, 1f), Is.True, $"{p} lies on open ground");
            Assert.That(campaign.NodesLeft(V2Campaign.NodeKind.VoidAlloy), Is.Zero, "while the Prophet stands nothing can be mined as void alloy");
            campaign.DebugAfternoonVoidOre();
            var next = campaign.SealedMapOre.ToList();
            Assert.That(next.Count, Is.EqualTo(count), "the next afternoon lays the count again");
            Assert.That(next.SequenceEqual(pieces), Is.False, "and in new places: yesterday's pieces go");
            Assert.That(campaign.NodesLeft(V2Campaign.NodeKind.VoidAlloy), Is.Zero, "still nothing to mine");

            // The Prophet beaten: every sealed piece goes, and the void alloy lies across the map.
            campaign.DebugBeatProphet();
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(campaign.SealedOre.Count, Is.Zero, "with the Prophet beaten the line's sealed pieces go");
            Assert.That(campaign.SealedMapOre.Count, Is.Zero, "and so do the sealed pieces across the map");
            Assert.That(campaign.SealedOreShown, Is.False, "nothing sealed shows any more");
            campaign.DebugAfternoonVoidOre();
            var nodes = campaign.NodePositions(V2Campaign.NodeKind.VoidAlloy);
            Assert.That(nodes.Count, Is.EqualTo(count), "the afternoon's void alloy: the day's count of nodes");
            Assert.That(nodes.Count(n => OffLine(n) > OffTheWay), Is.GreaterThan(0), "lying across the map, not all on the hole's way");
            foreach (var n in nodes) Assert.That(map.IsWalkable(n, 1f), Is.True, $"{n} lies on open ground");
            Assert.That(campaign.SealedMapOre.Count, Is.Zero, "and no sealed piece comes back");
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
