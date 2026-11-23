using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The story's final battle is over when the Tyrant falls (V2Campaign.EndFinalFight, TickCore): the crowd it had called
    /// leaves the map, the ones its red crosses bring after its fall leave as well, and nothing hurts the player on the way to the dark
    /// matter core, whose E then starts the ending. The final battle is reached through the campaign's own calls: day 7's afternoon
    /// opened for checks, the ship's parts and fuel bought, the ignition started and its cutscene skipped.</summary>
    [TestFixture]
    public class FinalBattleAftermathTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(240000)]
        public async Task WhenTheTyrantFalls_ItsCrowdLeaves_AndNothingHurtsThePlayerOnTheWayToTheCore()
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

            // The last planned day's afternoon, the ship made ready, and the ignition.
            campaign.DebugBeginAfternoon(campaign.rules.days);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            campaign.DebugGrant(2000, 0, 2000);
            foreach (V2Campaign.ShipPartKind part in Enum.GetValues(typeof(V2Campaign.ShipPartKind)))
                Assume.That(campaign.TryBuyShipPart(part), Is.True, $"the ship's {part} is bought");
            Assume.That(campaign.TryAddFuel(), Is.True, "the fuel goes in");
            Assume.That(campaign.CanIgnite, Is.True, "the ship can ignite");
            campaign.Ignite();
            deadline = Time.realtimeSinceStartup + 120f;
            while (campaign.Stage != CampaignStage.FinalBoss)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The final battle never began: {campaign.Stage}, {game.RunPhase}.");
                if (campaign.Ui) campaign.Ui.SkipCg();
                await Awaitable.NextFrameAsync();
            }
            await Until(() => game.waves.tyrant && game.waves.tyrant.gameObject.activeInHierarchy && game.waves.tyrant.IsAlive, 15f, "the Tyrant arrives");
            var tyrant = game.waves.tyrant;
            game.player.ProtectFor(60f);

            // A crowd about the player, as its scepter calls one; then its red crosses still on the ground as it falls.
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = V2Campaign.FindOpen(game.player.Position + Quaternion.Euler(0f, 90f * i + 45f, 0f) * Vector3.forward * 7f, 1f);
                var minion = game.waves.SpawnLab(EnemyKind.Grunt, at, false, ElementKind.None);
                if (minion) minion.SuppressRewards = true;
            }
            await Until(() => Minions(game) >= 2, 10f, "the Tyrant's crowd stands");
            game.waves.SpawnTyrantSummons(6, game.player.Position + Vector3.back * 10f);
            deadline = Time.realtimeSinceStartup + 40f;
            while (campaign.Stage == CampaignStage.FinalBoss)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The Tyrant did not fall: {tyrant.Hp}/{tyrant.maxHp}, phase {tyrant.BossPhase}.");
                if (tyrant.IsAlive) tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp, source = DamageSource.Poison, ignoreShield = true });
                await Awaitable.NextFrameAsync();
            }
            Assert.That(campaign.Stage, Is.EqualTo(CampaignStage.Core), "the Tyrant's fall leaves the dark matter core");
            Assert.That(game.State, Is.EqualTo(GameState.Playing), "the run goes on to the core");

            // From here nothing stands for more than the frame it appears in, and the player loses no HP.
            float hp = game.player.Hp;
            int most = 0;
            float watch = Time.realtimeSinceStartup + 4f;
            await Awaitable.NextFrameAsync();
            while (Time.realtimeSinceStartup < watch)
            {
                await Awaitable.NextFrameAsync();
                most = Mathf.Max(most, Minions(game));
                Assert.That(game.State, Is.EqualTo(GameState.Playing), "the run is not lost after the Tyrant fell");
            }
            Assert.That(Minions(game), Is.Zero, "none of the Tyrant's crowd is left on the map");
            Assert.That(most, Is.LessThanOrEqualTo(6), "the ones its red crosses bring leave as they come (never a second crowd standing)");
            Assert.That(game.player.IsAlive && game.player.Hp >= hp, Is.True, $"the player lost no HP after the Tyrant fell ({hp} -> {game.player.Hp})");
            Assert.That(game.player.Protected, Is.True, "no attack lands on the player until the core is taken");
        }

        static int Minions(GameManager game) => game.waves.Alive.Count(e => e && e.IsAlive && !e.IsBoss && e.gameObject.activeInHierarchy);

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
