using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>General weapon rules of design document section 5: right-click does not switch to an empty
    /// hand slot (5.4); a stowed Capacitor loses its charge and runs its full-charge timer back instead of pausing (5.4); the weapon card
    /// always states the element, and the cards use the doc's words, pairing (搭配) and trait (特性), in both languages (5.2, 5.6).</summary>
    [TestFixture]
    public class WeaponGeneralRulesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        /// <summary>A hand weapon in the active slot, with no element so the self-discharge is not blurred by element payoffs.</summary>
        static RunWeapon Equip(GameManager game, WeaponForm form, int variant)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        // ───── 5.4 Switching ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Switch_OtherHandSlotEmpty_RightClickDoesNothing()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            int slot = weapons.ActiveIndex;
            var held = Equip(game, WeaponForm.Rifle, 0);
            weapons.Hand[1 - slot] = null;

            weapons.Switch();

            Assert.That(weapons.ActiveIndex, Is.EqualTo(slot), "with the other hand slot empty, right-click does not switch to bare hands");
            Assert.That(weapons.Active, Is.SameAs(held));

            var second = new RunWeapon { form = WeaponForm.Knife, variant = 0, revealed = true };
            weapons.Hand[1 - slot] = second;
            weapons.Switch();
            Assert.That(weapons.Active, Is.SameAs(second), "a weapon in the other slot is switched to as before");

            // An empty slot in hand (only reachable by a replacement) still switches to the weapon in the other one.
            weapons.Hand[1 - slot] = null;
            weapons.Hand[slot] = held;
            weapons.Switch();
            Assert.That(weapons.Active, Is.SameAs(held), "from an empty slot the switch goes to the weapon");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StowedCapacitor_DrainsEvenlyAndRunsItsFullTimerBack_NeverDischargingIntoYou()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var capacitor = Equip(game, WeaponForm.ArcCoil, 1);
            int slot = weapons.ActiveIndex;
            weapons.Hand[1 - slot] = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true };
            Assume.That(tuning.capacitorStowedDrainSeconds, Is.EqualTo(10f).Within(.001f), "the decided drain: a full charge in 10 s (3 a second at 30)");
            float max = tuning.capacitorMax, perSecond = max / tuning.capacitorStowedDrainSeconds, hp = game.player.Hp;
            float drainSeconds = tuning.capacitorStowedDrainSeconds;
            Assert.That(WeaponGuide.Backfire(WeaponForm.ArcCoil, 1), Does.Contain("收起来"), "its weapon card says what stowing does");

            try
            {
                // Full and 0.1 s from discharging into you when it is put away.
                capacitor.charge = max; capacitor.chargeFullTime = 3.9f;
                weapons.Switch();
                Assume.That(weapons.Active, Is.Not.SameAs(capacitor), "the Capacitor is stowed");
                float stowedAt = Time.time;

                await Seconds(1f);

                float away = Time.time - stowedAt, ranBack = 3.9f - capacitor.chargeFullTime;
                Assert.That(game.player.Hp, Is.EqualTo(hp), "stowed, it never discharges into you");
                Assert.That(ranBack, Is.EqualTo(away).Within(.25f), "its full timer runs back one second a second");
                Assert.That(max - capacitor.charge, Is.EqualTo(ranBack * perSecond).Within(.05f), "it loses its charge evenly, a full charge in 10 s");
                string shown = Mathf.Ceil(Mathf.Clamp01(capacitor.charge / max) * 100f).ToString("0");
                Assert.That(game.UiSnapshot.handSlots[slot].detail, Does.Contain($"电量 {shown}%"), "its slot shows the charge it has left");

                // Run it down faster: the charge and the timer stop at 0, still without a discharge.
                tuning.capacitorStowedDrainSeconds = .5f;
                await Until(() => capacitor.chargeFullTime <= 0f, 5f, "the full timer runs all the way back");
                Assert.That(capacitor.charge, Is.Zero, "the charge stops at 0");
                Assert.That(game.player.Hp, Is.EqualTo(hp), "nothing went off");
                Assert.That(game.UiSnapshot.handSlots[slot].detail, Does.Not.Contain("电量"), "an empty stowed Capacitor shows no charge");
            }
            finally { tuning.capacitorStowedDrainSeconds = drainSeconds; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StowedCapacitor_QuickSwitchOutAndBack_KeepsWhatWasLeftOfItsFullTimer()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var capacitor = Equip(game, WeaponForm.ArcCoil, 1);
            int slot = weapons.ActiveIndex;
            weapons.Hand[1 - slot] = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true };
            float max = tuning.capacitorMax;
            capacitor.charge = max; capacitor.chargeFullTime = 3.4f;

            weapons.Switch();
            await Seconds(.2f);
            weapons.Switch();

            float left = capacitor.chargeFullTime;
            Assert.That(weapons.Active, Is.SameAs(capacitor), "back in hand");
            Assert.That(left, Is.InRange(3.4f - .6f, 3.4f - .1f), "a quick switch only takes off the time it was away");
            Assert.That(capacitor.charge, Is.LessThan(max), "it lost some charge meanwhile");
            Assert.That(game.UiSnapshot.handSlots[slot].detail, Does.Not.Contain("电量"), "in hand the charge bar shows the charge, not the slot");

            // Full again in hand (walking refills it): it goes off once the timer it kept reaches 4 s, not after a fresh 4 s.
            float hp = game.player.Hp;
            capacitor.charge = max;
            float fullAt = Time.time;
            await Until(() => game.player.Hp < hp, 3f, "the full Capacitor discharges into you");
            Assert.That(Time.time - fullAt, Is.LessThan(4f - left + .5f), "with what was left of its timer");
            Assert.That(hp - game.player.Hp, Is.EqualTo(tuning.capacitorSelfBlast * game.progression.Stats.DamageTakenMultiplier).Within(.01f),
                "the self-discharge at its base value");
            Assert.That(capacitor.charge, Is.Zero, "the discharge empties it");
        }

        // ───── 5.2 Weapon card, 5.6 armory: the doc's words ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task WeaponCards_GoStraightToTheAttackAndUseTheNewWords_InBothLanguages()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var game = GameManager.Instance;
            Assume.That(ArenaText.IsChinese, Is.True, "the Chinese edition starts in Chinese");
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await Awaitable.NextFrameAsync();

            // The opening pick (R1, R2): the attack and the trait are unknown until taken.
            var pick = Object.FindAnyObjectByType<V2Hud>().screens.First(s => s && s.state == ArenaUiState.Loadout);
            Assert.That(pick.body.text, Does.Contain("攻击方式和特性").And.Not.Contain("反噬"), "the opening pick's text");
            foreach (var choice in game.UiSnapshot.loadoutChoices)
                Assert.That(choice.body, Does.Contain("攻击方式和特性").And.Not.Contain("反噬"), "each starting form's card");

            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            var rifle = game.runWeapons.Hand[0];
            Assume.That(rifle != null && rifle.form == WeaponForm.Rifle && rifle.element == ElementKind.None, Is.True, "the Rifle has no element");
            string body = game.UiSnapshot.weaponIntroBody;
            Assert.That(body, Does.Contain("攻击方式").And.Contain("特性"), "the card's headings are the doc's words");
            Assert.That(body, Does.Not.Contain("属性："), "the card goes straight to the attack: hand or auto and the element are on the Alpha's card only");
            Assert.That(body, Does.Not.Contain("反噬").And.Not.Contain("射击方式").And.Not.Contain("异常"), "no old words");

            // Armory: a new weapon and a stronger version of the held Rifle.
            game.runWeapons.Offers[0] = new RunWeapon { form = WeaponForm.ScatterCannon, variant = 0, mk = 1 };
            game.runWeapons.Offers[1] = new RunWeapon { form = WeaponForm.Rifle, variant = 0, mk = 2, upgradeOf = rifle };
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                var offers = game.UiSnapshot.armoryOffers;
                Assert.That(offers[0].body, Does.Contain("搭配未揭晓").And.Not.Contain("反噬"), "a new weapon's pairing is unrevealed");
                Assert.That(offers[1].body, Does.Contain("攻击方式和特性不变").And.Not.Contain("反噬"), "a stronger version keeps its pairing (2026-10-28)");

                locale.localeCode = "en";
                locale.SelectLocale();
                await Until(() => ArenaText.Ready && !ArenaText.IsChinese, 12f, "the English locale");
                var english = game.UiSnapshot;
                Assert.That(english.weaponIntroBody, Does.Contain("How it attacks").And.Contain("Trait").And.Not.Contain("Element:"), "the English card");
                Assert.That(english.weaponIntroHint, Does.Contain("Space").And.Contain("Enter"), "the English hint names the keys");
                string all = english.weaponIntroBody + english.loadoutChoices[0].body + english.armoryOffers[0].body + english.armoryOffers[1].body;
                Assert.That(all, Does.Contain("pairing").And.Not.Contain("ackfire").And.Not.Contain("nomal"), "no old English words");
            }
            finally
            {
                game.runWeapons.Offers = new RunWeapon[RunWeapons.ArmoryOfferCount];
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                await Until(() => ArenaText.Ready && ArenaText.IsChinese, 12f, "back to Chinese");
                Object.Destroy(locale.gameObject);
            }
        }

        [TestCase(BuildEdition.Edition.EnglishAlpha, true, TestName = "The English Alpha's weapon card states hand or auto and the element")]
        [TestCase(BuildEdition.Edition.EnglishBeta, false, TestName = "The English Beta's weapon card goes straight to the attack")]
        [TestCase(BuildEdition.Edition.Full, false, TestName = "The whole game's weapon card goes straight to the attack")]
        public void WeaponCardLead_ByEdition_StatesHandOrAutoAndTheElementOnTheAlphaOnly(BuildEdition.Edition edition, bool states)
        {
            string lead = GameManager.WeaponCardLead(false, ElementKind.None, false, edition);

            Assert.That(lead.Contains(ArenaTerms.Element(ElementKind.None)), Is.EqualTo(states));
        }

        [Test]
        public void WeaponCardLead_AnAnomalyWeaponInTheStory_KeepsItsNote_ThenABlankLine()
        {
            string lead = GameManager.WeaponCardLead(false, ElementKind.None, true, BuildEdition.Edition.Full);

            Assert.That(lead, Does.Contain("c9a6ff").And.EndWith("\n\n"), "the anomaly note, a blank line before the attack");
            Assert.That(lead, Does.Not.Contain(ArenaTerms.Element(ElementKind.None)), "no element line");
        }

        [Test]
        public void WeaponCardLead_ThePistolOutsideTheAlpha_IsEmpty() =>
            Assert.That(GameManager.WeaponCardLead(false, ElementKind.None, false, BuildEdition.Edition.Full), Is.Empty);

        // ───── Helpers ─────

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.NextFrameAsync();
        }
    }
}
