using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class WeaponGeneralRulesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

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
            Assert.That(WeaponGuide.Backfire(WeaponForm.ArcCoil, 1), Does.Contain("Stowed"), "its weapon card says what stowing does");

            try
            {

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
                Assert.That(game.UiSnapshot.handSlots[slot].detail, Does.Contain($"Charge {shown}%"), "its slot shows the charge it has left");

                tuning.capacitorStowedDrainSeconds = .5f;
                await Until(() => capacitor.chargeFullTime <= 0f, 5f, "the full timer runs all the way back");
                Assert.That(capacitor.charge, Is.Zero, "the charge stops at 0");
                Assert.That(game.player.Hp, Is.EqualTo(hp), "nothing went off");
                Assert.That(game.UiSnapshot.handSlots[slot].detail, Does.Not.Contain("Charge"), "an empty stowed Capacitor shows no charge");
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
            Assert.That(game.UiSnapshot.handSlots[slot].detail, Does.Not.Contain("Charge"), "in hand the charge bar shows the charge, not the slot");

            float hp = game.player.Hp;
            capacitor.charge = max;
            float fullAt = Time.time;
            await Until(() => game.player.Hp < hp, 3f, "the full Capacitor discharges into you");
            Assert.That(Time.time - fullAt, Is.LessThan(4f - left + .5f), "with what was left of its timer");
            Assert.That(hp - game.player.Hp, Is.EqualTo(tuning.capacitorSelfBlast * game.progression.Stats.DamageTakenMultiplier).Within(.01f),
                "the self-discharge at its base value");
            Assert.That(capacitor.charge, Is.Zero, "the discharge empties it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task WeaponCards_StateTheElementAndUseTheNewWords_InBothLanguages()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var game = GameManager.Instance;
            Assume.That(ArenaText.IsChinese, Is.True, "the Chinese edition starts in Chinese");
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await Awaitable.NextFrameAsync();

            var pick = Object.FindAnyObjectByType<V2Hud>().screens.First(s => s && s.state == ArenaUiState.Loadout);
            Assert.That(pick.body.text, Does.Contain("attack and trait").And.Not.Contain("Backfire"), "the opening pick's text");
            foreach (var choice in game.UiSnapshot.loadoutChoices)
                Assert.That(choice.body, Does.Contain("attack and trait").And.Not.Contain("Backfire"), "each starting form's card");

            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            var rifle = game.runWeapons.Hand[0];
            Assume.That(rifle != null && rifle.form == WeaponForm.Rifle && rifle.element == ElementKind.None, Is.True, "the Rifle has no element");
            string body = game.UiSnapshot.weaponIntroBody;
            Assert.That(body, Does.Contain("How it attacks").And.Contain("Trait"), "the card's headings are the doc's words");
            Assert.That(body, Does.Contain("Element: None"), "document 5.2: the card states the element, none included");
            Assert.That(body, Does.Not.Contain("Backfire").And.Not.Contain("How it fires").And.Not.Contain("anomaly"), "no old words");

            game.runWeapons.Offers[0] = new RunWeapon { form = WeaponForm.ScatterCannon, variant = 0, mk = 1 };
            game.runWeapons.Offers[1] = new RunWeapon { form = WeaponForm.Rifle, variant = 0, mk = 2, upgradeOf = rifle };
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                var offers = game.UiSnapshot.armoryOffers;
                Assert.That(offers[0].body, Does.Contain("pairing unrevealed").And.Not.Contain("Backfire"), "a new weapon's pairing is unrevealed");
                Assert.That(offers[1].body, Does.Contain("Its attack and trait stay the same").And.Not.Contain("Backfire"), "a stronger version keeps its pairing (owner 2026-10-28)");

                locale.localeCode = "en";
                locale.SelectLocale();
                await Until(() => ArenaText.Ready && !ArenaText.IsChinese, 12f, "the English locale");
                var english = game.UiSnapshot;
                Assert.That(english.weaponIntroBody, Does.Contain("How it attacks").And.Contain("Trait").And.Contain("Element: None"), "the English card");
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
