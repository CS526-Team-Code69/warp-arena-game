using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The story's hints round the weapons (V2Campaign.Tutorial.cs). Day 0's firing step counts its shots in the goal, "(1/3)".
    /// A hand weapon taken from an afternoon crate is tried out with a hint of a later day: while the weapon is not the hand in use it
    /// asks for the right button, then for two shots of it, and it goes once they are fired; like every hint after Day 0 it shows only
    /// its circle and its tip and leaves the screen undimmed. The afternoon is opened with the campaign's own call for checks, so no
    /// morning is played, and the crate is opened with the campaign's stand-in for the E key.</summary>
    [TestFixture]
    public class LateHintTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        // Where the player stands to open the crate, from the crate (as CampaignCrateOfferTest): on it first, then a step to each side.
        static readonly Vector3[] Spots = { Vector3.zero, new(1.2f, 0f, 0f), new(-1.2f, 0f, 0f), new(0f, 0f, 1.2f), new(0f, 0f, -1.2f) };

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task DayZeroFiringStep_EachShot_CountsInTheGoal()
        {
            var (game, campaign) = await StartStory();
            await Until(() => campaign.TutorialStepNow == TutorialStep.Move && campaign.TutorialStepForced, 15f, "the walking step is taught");
            campaign.DebugPassTutorialStep();
            await Until(() => campaign.TutorialStepNow == TutorialStep.Fire && campaign.TutorialStepForced, 20f, "the firing step is taught");
            var goal = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Objective");
            int need = campaign.rules.tutorialShots;
            string Words(int done) => ArenaText.Pair($"用小手枪开 {need} 枪，朝哪打都行（{done}/{need}）", $"Fire the pistol {need} times, in any direction ({done}/{need})");
            await Until(() => goal.text.Contains(Words(0)), 3f, "the goal starts with none of the shots fired");

            var pistol = game.runWeapons.Active;
            Assume.That(pistol != null && pistol.form == WeaponForm.Pistol, Is.True, "the pistol is in hand");
            for (int shot = 1; shot <= need; shot++)
            {
                await Until(() => pistol.cooldown <= 0f && pistol.reload <= 0f, 3f, $"the pistol is ready for shot {shot}");
                game.runWeapons.HandleInput(true, true, false);
                await Until(() => goal.text.Contains(Words(shot)), 3f, $"shot {shot} is counted in the goal");
                game.runWeapons.HandleInput(false, false, true);
            }

            await Until(() => campaign.TutorialStepNow != TutorialStep.Fire, 20f, "the last shot passes the step");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task AfternoonCrateWeapon_TheHintAsksForTheRightButtonThenTwoShots_AndLeavesTheScreenUndimmed()
        {
            var (game, campaign) = await StartStory();
            campaign.DebugBeginAfternoon(1);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            Assume.That(campaign.TryNearestCrate(game.player.Position, out var crateAt), Is.True, "the afternoon put its crate on the ore trail");
            game.player.ProtectFor(600f);

            float opening = Time.realtimeSinceStartup + 20f;
            for (int frame = 0; !campaign.CratePanelOpen; frame++)
            {
                if (Time.realtimeSinceStartup > opening)
                    Assert.Fail($"the crate never opened (stage {campaign.Stage}, phase {game.RunPhase}, crates on the map {campaign.CratesOnMap})");
                SkipHoleIntro();
                if (frame % 20 == 0) campaign.DebugPlacePlayer(crateAt + Spots[frame / 20 % Spots.Length]);
                campaign.debugPressE = true;
                await Awaitable.NextFrameAsync();
            }
            campaign.debugPressE = false;
            var forms = campaign.CrateOfferForms;
            int pick = Array.FindIndex(forms, form => !RunWeapon.IsAutoForm(form));
            Assume.That(pick, Is.GreaterThanOrEqualTo(0), "the crate offers a hand weapon");
            Assert.That(campaign.LateHintNow, Is.EqualTo("None"), "no hint while the crate's panel is open");

            campaign.DebugTakeCrate(pick);
            float closing = Time.realtimeSinceStartup + 10f;
            while (game.RunPhase == ArenaUiState.WeaponIntro)
            {
                if (Time.realtimeSinceStartup > closing) Assert.Fail("the weapon card never closed");
                game.ExecuteUiCommand(ArenaUiCommand.DismissWeaponIntro, -1);
                await Awaitable.NextFrameAsync();
            }
            var weapons = game.runWeapons;
            int hand = Array.FindIndex(weapons.Hand, w => w != null && w.form == forms[pick]);
            Assume.That(hand, Is.GreaterThanOrEqualTo(0), "the crate's weapon is in a hand");
            var weapon = weapons.Hand[hand];
            Assume.That(weapons.ActiveIndex, Is.Not.EqualTo(hand), "the new weapon went into the free hand, the pistol's hand still in use");

            await Until(() => campaign.LateHintNow == "TryWeapon", 5f, "once the card is closed the try-weapon hint comes up");
            Assert.That(campaign.TryWeaponAsksSwitch, Is.True, "the new weapon is not the hand in use: the right button first");
            Assert.That(SpotlightDims(), Is.False, "a hint after Day 0 shows its circle and tip without dimming the screen");

            weapons.Switch();
            await Until(() => !campaign.TryWeaponAsksSwitch, 3f, "switched to the new weapon, the hint asks for its shots");
            Assert.That(campaign.LateHintNow, Is.EqualTo("TryWeapon"), "the same hint goes on");

            // A shot sets the weapon's cooldown to its full interval, and the hint watches for that jump. The crate's weapon is any hand
            // anomaly weapon, and some fire only on release or after a charge, so the test stands in for two shots by raising the cooldown.
            for (int shot = 1; shot <= 2; shot++)
            {
                Assert.That(campaign.LateHintNow, Is.EqualTo("TryWeapon"), $"still up before shot {shot}");
                weapon.cooldown += 5f;
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
            }

            await Until(() => campaign.LateHintNow == "None", 3f, "two shots of the new weapon end the hint");
        }

        /// <summary>Whether the campaign's spotlight dims the screen round its circle (V2Spotlight keeps it in a private field).</summary>
        static bool SpotlightDims()
        {
            var spotlight = Object.FindAnyObjectByType<V2Spotlight>(FindObjectsInactive.Include);
            Assume.That(spotlight, Is.Not.Null, "the campaign's spotlight is made");
            var dim = typeof(V2Spotlight).GetField("dim", BindingFlags.NonPublic | BindingFlags.Instance);
            Assume.That(dim, Is.Not.Null, "V2Spotlight.dim");
            return (bool)dim.GetValue(spotlight);
        }

        // Should the player's place bring the black hole into sight, its first-sight cutscene is ended at once.
        static void SkipHoleIntro()
        {
            var map = WorldMap.Active;
            if (map && map.HoleIntroPlaying) map.SkipHoleIntro();
        }

        static async Task<(GameManager, V2Campaign)> StartStory()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
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
            return (game, campaign);
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
