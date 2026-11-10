using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The V2 campaign's starting Pistol (since 2026-11-06): a hand weapon with plain shots only, known from the start, weaker than
    /// the Rifle, never in the classic opening pick, the air drop or the armory, and never upgraded (no Mk, Infusion, Twist or Tame). Every
    /// weapon form, the Pistol included, has a name, an attack name and an icon, and the hand holds the Pistol's picture or the whitebox box.</summary>
    [TestFixture]
    public class PistolTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const float TargetHp = 100f;
        bool savedArt;

        [SetUp]
        public void KeepArt() => savedArt = Art.Enabled;

        [TearDown]
        public void Restore()
        {
            Art.Enabled = savedArt;
            Time.timeScale = 1f;
        }

        // ───── Without a scene ─────

        [Test]
        public void PistolForm_IsAHandWeaponWithoutAnomaly_AndTheRosterKeepsItOutOfItsList()
        {
            foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
            {
                bool auto = form is WeaponForm.OrbitBlades or WeaponForm.SentryDrone or WeaponForm.ShieldRing;
                Assert.That(new RunWeapon { form = form }.IsAuto, Is.EqualTo(auto), $"{form}: auto weapons by name, so the Pistol after them is a hand weapon");
                Assert.That(new RunWeapon { form = form }.IsAnomalous, Is.EqualTo(form != WeaponForm.Pistol), $"{form}: every weapon but the Pistol is an anomaly weapon");
                for (int variant = 0; variant < 2; variant++)
                    Assert.That(WeaponRoster.AnomalyName(form, variant), Is.Not.Empty, $"{form} {variant}: an English attack name");
            }

            var roster = ScriptableObject.CreateInstance<WeaponRoster>();
            try
            {
                roster.SetDefaults();
                Assert.That(roster.definitions.Select(d => d.form), Has.No.Member(WeaponForm.Pistol), "the armory's list never holds the Pistol");
                var pistol = roster.Get(WeaponForm.Pistol);
                var expected = WeaponRoster.DefaultPistol();
                Assert.That(pistol, Is.Not.Null, "Get still finds the Pistol's numbers");
                Assert.That(pistol.form, Is.EqualTo(WeaponForm.Pistol));
                Assert.That(new[] { pistol.damageA, pistol.cooldownA, pistol.rangeA }, Is.EqualTo(new[] { expected.damageA, expected.cooldownA, expected.rangeA }), "its defaults");
                Assert.That(new[] { pistol.damageB, pistol.cooldownB, pistol.rangeB }, Is.EqualTo(new[] { pistol.damageA, pistol.cooldownA, pistol.rangeA }), "no variant difference");
            }
            finally { Object.DestroyImmediate(roster); }
        }

        // ───── In the Chinese scene ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Pistol_PressAndHold_FiresPlainRoundsAtItsInterval_EachStoppingAtTheFirstEnemy()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            int reveals = 0;
            void CountReveal(RunWeapon _) => reveals++;
            weapons.Revealed += CountReveal;
            try
            {
                weapons.ClearWaveEffects();
                weapons.GiveToHand(weapons.ActiveIndex, WeaponForm.Pistol);
                var pistol = weapons.Active;
                Assert.That(pistol.form, Is.EqualTo(WeaponForm.Pistol));
                Assert.That(pistol.revealed && pistol.variant == 0 && pistol.mk == 1, Is.True, "known at once, one variant, Mk 1");
                Assert.That(pistol.Label, Is.EqualTo(ArenaTerms.WeaponForm(WeaponForm.Pistol)).And.Not.Contain("?"), "its name alone");
                var definition = weapons.roster.Get(WeaponForm.Pistol);
                Assert.That(definition, Is.Not.Null, "the saved roster gives the Pistol its numbers");
                float hit = definition.damageA * weapons.crowdDamageMultiplier; // no starting-form factor and no Mk

                weapons.HandleInput(true, true, false);
                var round = AttackPool.Instance.actors.FirstOrDefault(a => a && a.gameObject.activeSelf && a.weapon == pistol);
                Assert.That(round, Is.Not.Null, "a press fires one round at once");
                Assert.That(Rounds(pistol), Is.EqualTo(1), "one round");
                Assert.That(round.mode, Is.EqualTo(AttackMode.Bolt));
                Assert.That(round.pierce || round.ricochets > 0 || round.knock > 0f, Is.False, "a plain round: no pierce, no bounce, no push");
                Assert.That(round.speed, Is.EqualTo(weapons.pistolRoundSpeed).Within(.001f), "at the Pistol's round speed");
                Assert.That(pistol.charging, Is.False, "no charge");
                Assert.That(pistol.cooldown, Is.EqualTo(definition.cooldownA).Within(.001f), "its own interval, with no starting-form factor");

                await Until(() => targets[0].Hp < TargetHp, 3f, "the round reaches the first enemy");
                await Until(() => !round.gameObject.activeSelf, 3f, "the round stops there");
                Assert.That(targets[0].Hp, Is.EqualTo(TargetHp - hit).Within(.001f), "one plain hit");
                Assert.That(targets[1].Hp, Is.EqualTo(TargetHp), "the enemy behind it is untouched");

                // Held down, it waits for its interval, then fires again without a new press.
                pistol.cooldown = 1f;
                weapons.HandleInput(false, true, false);
                Assert.That(Rounds(pistol), Is.Zero, "nothing while the interval runs");
                pistol.cooldown = 0f;
                weapons.HandleInput(false, true, false);
                Assert.That(Rounds(pistol), Is.EqualTo(1), "holding fires the next round");
                Assert.That(pistol.reload, Is.Zero, "no clip to reload");
                Assert.That(reveals, Is.Zero, "no reveal toast");

                // Weaker than the Rifle: a smaller hit and less damage a second than a full Charged Shot with its charge.
                var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true };
                float rifleCycle = weapons.CycleSeconds(rifle) + weapons.StartingFormCycleFactor(WeaponForm.Rifle); // a 1 s charge, scaled like the cooldown
                Assert.That(weapons.HitDamage(pistol), Is.LessThan(weapons.HitDamage(rifle)), "a smaller hit than a full Charged Shot");
                Assert.That(weapons.TheoreticalDps(pistol), Is.LessThan(weapons.HitDamage(rifle) / rifleCycle), "and less damage a second");
            }
            finally { weapons.Revealed -= CountReveal; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ClassicRun_NeverOffersThePistol_AndNoUpgradeEverPicksIt()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await Awaitable.NextFrameAsync();
            Assert.That(game.UiSnapshot.loadoutChoices.Select(c => c.formIndex),
                Is.EquivalentTo(new[] { (int)WeaponForm.RocketLauncher, (int)WeaponForm.Rifle, (int)WeaponForm.Knife }), "the opening pick is the three starting forms");

            var weapons = game.runWeapons;
            Assume.That(RunWeapons.AxisAllowed == null && RunWeapons.ArmoryRule == null, Is.True, "a classic run, with no campaign rules left behind");
            weapons.ResetRun();
            var pistol = new RunWeapon { form = WeaponForm.Pistol, revealed = true };
            weapons.Hand[0] = pistol;
            Assert.That(weapons.RollUpgradeCard(), Is.Null, "with only the Pistol in hand there is nothing to upgrade");
            var knife = new RunWeapon { form = WeaponForm.Knife, variant = 0, revealed = true };
            weapons.Hand[1] = knife;
            for (int i = 0; i < 40; i++)
            {
                var card = weapons.RollUpgradeCard();
                Assert.That(card, Is.Not.Null, "the knife can still be upgraded");
                Assert.That(((WeaponUpgradeOffer)card.Payload).Weapon, Is.SameAs(knife), "no Infusion, Twist or Tame ever picks the Pistol");
            }
            for (int wave = 1; wave <= 14; wave++)
                for (int roll = 0; roll < 6; roll++)
                {
                    weapons.RollArmory(wave);
                    foreach (var offer in weapons.Offers.Where(o => o != null))
                    {
                        Assert.That(offer.form, Is.Not.EqualTo(WeaponForm.Pistol), $"wave {wave}: the air drop never brings a Pistol");
                        Assert.That(offer.upgradeOf, Is.Not.SameAs(pistol), $"wave {wave}: nor a stronger Pistol");
                    }
                }
            Assert.That(pistol.mk == 1 && pistol.axes == UpgradeAxis.None, Is.True, "the Pistol stays as it was");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task EveryWeaponForm_HasAnIconAndNames_AndTheHandHoldsThePistolsPictureOrItsWhiteboxShape()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var hud = Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            Assume.That(hud && hud.art, Is.True, "the HUD has the V2 art set");
            var forms = (WeaponForm[])Enum.GetValues(typeof(WeaponForm));
            Assert.That(hud.art.weaponSprites, Has.Length.EqualTo(forms.Length), "one icon per form");
            foreach (var form in forms)
            {
                Assert.That(hud.art.weaponSprites[(int)form], Is.Not.Null, $"{form}: an icon");
                Assert.That(ArenaTerms.WeaponForm(form), Is.Not.Empty, $"{form}: a name");
                for (int variant = 0; variant < 2; variant++)
                    Assert.That(ArenaTerms.WeaponAnomaly(form, variant), Is.Not.Empty, $"{form} {variant}: an attack name");
            }
            Assert.That(ArenaTerms.WeaponForm(WeaponForm.Pistol), Is.EqualTo(ArenaText.Pair("小手枪", "Pistol")));
            for (int variant = 0; variant < 2; variant++)
            {
                Assert.That(WeaponGuide.How(WeaponForm.Pistol, variant), Does.Contain(ArenaText.Pair("普通射击", "Plain shots")), "how it attacks");
                Assert.That(WeaponGuide.Backfire(WeaponForm.Pistol, variant), Does.StartWith(ArenaText.Pair("无", "None")), "the trait line says there is none instead of staying empty");
            }

            var weapons = game.runWeapons;
            weapons.ClearWaveEffects();
            weapons.GiveToHand(weapons.ActiveIndex, WeaponForm.Pistol);
            var held = game.player.GetComponent<V2WeaponPresentation>();
            Assume.That(held && held.illustration && held.whitebox, Is.True, "the player holds its weapon through V2WeaponPresentation");
            Art.Enabled = true;
            await Frames(2);
            Assert.That(held.illustration.sprite, Is.SameAs(hud.art.weaponSprites[(int)WeaponForm.Pistol]), "the illustrated hand holds the Pistol's picture");
            Assert.That(held.illustration.enabled, Is.True, "and shows it");
            Art.Enabled = false;
            await Frames(2);
            // Since 2026-11-10 every form in use has its own whitebox shape from the table of looks; the saved box is only for the
            // retired forms.
            var shape = held.transform.Find("Whitebox " + WeaponForm.Pistol);
            Assert.That(shape && shape.GetComponent<WhiteboxLook>().Visible, Is.True, "the whitebox hand holds the Pistol's own whitebox shape");
            Assert.That(held.whitebox.enabled, Is.False, "in place of the saved box");
            Assert.That(held.illustration.enabled, Is.False, "and no picture");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PistolShot_IsTheRifleShot_WithoutTheChargedLayerOrAReload()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            await Until(() => game.CanUseCombatInput, 3f, "combat input opens");
            var audio = V5WeaponAudio.Instance;
            Assume.That(audio, Is.Not.Null, "V2Audio adds the sound layer");
            var weapons = game.runWeapons;
            weapons.ClearWaveEffects();
            weapons.GiveToHand(weapons.ActiveIndex, WeaponForm.Pistol);
            int shots = audio.Plays(V5Sfx.RifleShot), heavy = audio.Plays(V5Sfx.RifleHeavy), clips = audio.Plays(V5Sfx.ReloadClip);

            weapons.HandleInput(true, true, false);
            audio.Tick();

            Assert.That(audio.Plays(V5Sfx.RifleShot), Is.EqualTo(shots + 1), "the Rifle's shot is heard");
            Assert.That(audio.Plays(V5Sfx.RifleHeavy), Is.EqualTo(heavy), "without the charged layer");
            Assert.That(audio.Plays(V5Sfx.ReloadClip), Is.EqualTo(clips), "and no reload");
        }

        static int Rounds(RunWeapon weapon) => AttackPool.Instance.actors.Count(a => a && a.gameObject.activeSelf && a.weapon == weapon);

        static async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
