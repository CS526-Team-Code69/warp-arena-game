using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>The bottom row's feedback: the hand slot in use is marked and the buttons follow it, an attack flashes its own weapon's
    /// slot, and a cast flashes the skill's slot, runs a light round it and turns its cooldown.</summary>
    [TestFixture]
    public class SlotFeedbackTest
    {
        const string SceneCn = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void RestoreClock()
        {
            Time.timeScale = 1f;
            TestStartLanguage.Reset();
        }

        // ───── The hand in use ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task Chinese_TheHandInUse_IsMarked_AndTheMarkAndTheButtonsFollowTheSwap() => await HandInUseIsMarked();

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task English_TheHandInUse_IsMarked_AndTheMarkAndTheButtonsFollowTheSwap()
        {
            // One scene serves both languages since 2026-11-10: the English start is asked for before the scene loads.
            TestStartLanguage.Use(StartupLanguage.English);
            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            await HandInUseIsMarked();
        }

        static async Task HandInUseIsMarked()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>();
            var weapons = game.runWeapons;
            int held = weapons.ActiveIndex, other = 1 - held;
            weapons.Hand[other] = null;
            await Refreshed(hud);
            AssertInUse(hud, held);
            Assert.That(game.UiSnapshot.hint, Is.EqualTo(weapons.Active.Label), "the saved line over the charge bar names the weapon in hand");
            if (V5HudKit.On)
            {
                // The fifth pass names it on its slot only: the hint plate at the top has nothing to show, and the saved line is not drawn.
                var snapshot = game.UiSnapshot;
                Assert.That(snapshot.hintLead + snapshot.hintKey + snapshot.hintTail, Is.Empty, "the hint plate has no prompt while the hint only names the weapon");
                Assert.That(hud.gameplayRoot.transform.Find("V5Hint").gameObject.activeSelf, Is.False, "so it does not show");
                Assert.That(hud.hintLabel.enabled, Is.False, "and the saved line over the charge bar is not drawn");
            }
            Assert.That(Key(hud, other), Is.EqualTo(ArenaText.Pair($"手持{other + 1}", $"HAND {other + 1}")), "right click does nothing with the other slot empty, so it names no button");

            weapons.Hand[other] = new RunWeapon { form = WeaponForm.Knife, variant = 0, revealed = true };
            await Refreshed(hud);
            Assert.That(Key(hud, other), Is.EqualTo(ArenaText.Pair($"手持{other + 1} [右键切换]", $"HAND {other + 1} [RMB SWITCH]")));

            weapons.Switch();
            await Refreshed(hud);
            Assert.That(weapons.ActiveIndex, Is.EqualTo(other));
            AssertInUse(hud, other);
            Assert.That(game.UiSnapshot.hint, Is.EqualTo(weapons.Active.Label), "and follows the swap, whichever weapon was revealed last");
            Assert.That(Key(hud, held), Is.EqualTo(ArenaText.Pair($"手持{held + 1} [右键切换]", $"HAND {held + 1} [RMB SWITCH]")), "the buttons move with the swap");

            var dimmed = hud.handSlots[held].GetComponent<CanvasGroup>();
            var lit = hud.handSlots[other].GetComponent<CanvasGroup>();
            await Until(() => dimmed.alpha < .7f && lit.alpha > .95f, 2f, "the slot that is not in use dims and the one in use comes back");
        }

        static void AssertInUse(V2Hud hud, int index)
        {
            var inUse = hud.handSlots[index].feedback;
            var other = hud.handSlots[1 - index].feedback;
            Assert.That(inUse.IsInUse && inUse.TagShown && inUse.EdgeAlpha > .99f, Is.True, $"hand {index + 1} has the tag and the lit edge");
            Assert.That(other.IsInUse || other.TagShown, Is.False, $"hand {2 - index} has neither");
            Assert.That(Key(hud, index), Is.EqualTo(ArenaText.Pair($"手持{index + 1} [左键]", $"HAND {index + 1} [LMB]")), "the left button uses the slot in hand");
            var label = hud.handSlots[index].transform.Find("InUseTag/Label").GetComponent<Text>();
            Assert.That(label.text, Is.EqualTo(ArenaText.Pair("使用中", "IN USE")));
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task AnEmptyHand_HasNoMark()
        {
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready, 12f, "scene and localization initialize");
            var game = GameManager.Instance;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (game.RunPhase != ArenaUiState.SupplyDrop)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Stuck in {game.RunPhase}.");
                game.ExecuteUiCommand(ArenaUiCommand.Start);
                await Awaitable.NextFrameAsync();
            }
            var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>();
            await Refreshed(hud);
            foreach (var slot in hud.handSlots) Assert.That(slot.feedback.IsInUse || slot.feedback.TagShown, Is.False, "no weapon yet, nothing is in use");
            Assert.That(Key(hud, 0), Is.EqualTo(ArenaText.Pair("手持1 [左键]", "HAND 1 [LMB]")), "the first weapon will go here");
            Assert.That(Key(hud, 1), Is.EqualTo(ArenaText.Pair("手持2", "HAND 2")));
        }

        // ───── Attacks ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task AnAttack_FlashesItsWeaponsSlot_AndChargingDoesNot()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>();
            var weapons = game.runWeapons;
            var held = hud.handSlots[weapons.ActiveIndex].feedback;
            var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, rounds = weapons.ClipSize(WeaponForm.Rifle, 0) };
            weapons.Hand[weapons.ActiveIndex] = rifle;
            await Refreshed(hud);
            await Until(() => held.Flash <= 0f, 2f, "the slot's first light goes out");

            weapons.HandleInput(true, true, false);
            Assert.That(rifle.charging, Is.True);
            await Awaitable.NextFrameAsync();
            Assert.That(held.Flash, Is.Zero, "charging is not an attack yet");
            weapons.HandleInput(false, false, true);
            await Awaitable.NextFrameAsync();
            Assert.That(held.Flash, Is.GreaterThan(.5f), "the shot leaving flashes the slot, with no wait for the next refresh");
            Assert.That(hud.handSlots[1 - weapons.ActiveIndex].feedback.Flash, Is.Zero, "and only its own slot");
            await Until(() => held.Flash <= 0f, 2f, "the flash ends");

            var auto = hud.autoSlots[0].feedback;
            weapons.Auto[0] = new RunWeapon { form = WeaponForm.OrbitBlades, variant = 0, revealed = true };
            await Refreshed(hud);
            Assert.That(auto.Flash, Is.Zero, "a blade that has hit nothing has not attacked");
            float deadline = Time.realtimeSinceStartup + 4f;
            while (auto.Flash <= 0f)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("The blade's hit never flashed its slot.");
                V3CombatSmokeTest.Teleport(targets[0], Query.Flat(weapons.GetOrbitPosition(0, 0)));
                Physics.SyncTransforms();
                await Awaitable.NextFrameAsync();
            }
            Assert.That(held.Flash, Is.Zero, "the auto weapon flashes its own slot, not the hand's");
        }

        // ───── Casts ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task ACast_FlashesTurnsAndCoolsItsSlot_AndARefusedOneDoesNot()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>();
            var skills = game.skills;
            Assert.That(skills.Learn(SkillId.FrostNova, 0, 2), Is.True);
            int slot = Array.FindIndex(skills.Slots, s => !s.Empty && s.Id == SkillId.FrostNova);
            var feedback = hud.skillSlots[slot].feedback;
            game.progression.GainEnergy(1000f);
            await Refreshed(hud);
            Assert.That(feedback.Flash, Is.Zero);
            Assert.That(feedback.Spin, Is.EqualTo(-1f));
            Assert.That(feedback.ClockShown, Is.Zero);

            Assert.That(skills.TryCast(slot), Is.True);
            await Awaitable.NextFrameAsync();
            Assert.That(feedback.Flash, Is.GreaterThan(.5f), "the cast flashes the slot at once");
            Assert.That(feedback.Spin, Is.GreaterThanOrEqualTo(0f), "and starts the light round its edge");
            // The HUD writes its numbers at 10 Hz: from the first frame the clock keeps up with the cooldown, it does not fall away and jump back at the next write.
            float watchUntil = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < watchUntil)
            {
                Assert.That(feedback.ClockShown, Is.GreaterThanOrEqualTo(skills.Slots[slot].CooldownLeft / skills.Slots[slot].Cooldown - .01f), "the clock keeps up with the cooldown");
                await Awaitable.NextFrameAsync();
            }
            await Until(() => feedback.Spin >= 1f, 2f, "the light goes all the way round");
            await Until(() => feedback.Spin < 0f && feedback.Flash <= 0f, 2f, "the light and the flash end");

            await Refreshed(hud);
            Assert.That(feedback.ClockShown, Is.GreaterThan(0f), "the cooldown clock keeps turning after them");
            skills.Slots[slot].CooldownLeft = 0f;
            await Refreshed(hud);
            await Until(() => feedback.ClockShown <= 0f, 3f, "the clock runs out with the cooldown");

            Assert.That(game.progression.TrySpendEnergy(game.progression.Energy), Is.True);
            Assert.That(skills.TryCast(slot), Is.False, "no energy");
            await Awaitable.NextFrameAsync();
            Assert.That(feedback.Flash, Is.Zero, "a refused cast does not flash");
            Assert.That(feedback.Spin, Is.EqualTo(-1f), "or turn");
        }

        // ───── Helpers ─────

        static string Key(V2Hud hud, int index) => hud.handSlots[index].transform.Find("SlotKey").GetComponent<Text>().text;

        /// <summary>Draws the current run into the HUD and gives the feedback its frame.</summary>
        static async Task Refreshed(V2Hud hud)
        {
            hud.RefreshView();
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
        }

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
