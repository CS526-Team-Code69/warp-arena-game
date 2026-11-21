using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Holding the left button fires a weapon again each time it is ready (RunWeapons.RepeatsWhileHeld). The single shots whose trait
    /// does not punish a careless shot repeat at their own cooldown, so a held button reaches the pace of clicking at that cooldown and
    /// no more; the six whose careless shots hurt you, and the Capacitor, still fire once per click.
    /// </summary>
    public class HoldToFireTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        [Test]
        public void RepeatsWhileHeld_SafeSingleShotsRepeat_CarelessHurtsAndTheCapacitorDoNot()
        {
            static bool Repeats(WeaponForm form, int variant) => RunWeapons.RepeatsWhileHeld(new RunWeapon { form = form, variant = variant });
            Assert.That(Repeats(WeaponForm.Pistol, 0), Is.True, "the pistol");
            Assert.That(Repeats(WeaponForm.Rifle, 1), Is.True, "the rifle's hold fire");
            Assert.That(Repeats(WeaponForm.Minigun, 0), Is.True, "the minigun");
            Assert.That(Repeats(WeaponForm.ScatterCannon, 0), Is.True, "the spread shot");
            Assert.That(Repeats(WeaponForm.Flamethrower, 1), Is.True, "the lobbed fireball");
            Assert.That(Repeats(WeaponForm.CryoCannon, 0), Is.True, "the three-way shards");
            Assert.That(Repeats(WeaponForm.SporeLauncher, 0), Is.True, "the lobbed cloud");
            Assert.That(Repeats(WeaponForm.Knife, 1), Is.True, "the hook");
            Assert.That(Repeats(WeaponForm.RocketLauncher, 0), Is.True, "the delayed strike");

            Assert.That(Repeats(WeaponForm.Knife, 0), Is.False, "the thrown blade flies back to where it was thrown");
            Assert.That(Repeats(WeaponForm.RocketLauncher, 1), Is.False, "the human rocket flies you into walls");
            Assert.That(Repeats(WeaponForm.SwarmLauncher, 0), Is.False, "spare homing missiles turn back on you");
            Assert.That(Repeats(WeaponForm.SwarmLauncher, 1), Is.False, "the chain of blasts hurts you");
            Assert.That(Repeats(WeaponForm.Railgun, 1), Is.False, "a heavy shot that kills nothing shocks you");
            Assert.That(Repeats(WeaponForm.ArcCoil, 0), Is.False, "chain lightning with too few enemies near shocks you");
            Assert.That(Repeats(WeaponForm.ArcCoil, 1), Is.False, "the Capacitor stores its charge for one release");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task HeldButton_ThreeWayShards_FireAgainAtTheirCooldown()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            weapons.ClearWaveEffects();
            var shards = new RunWeapon { form = WeaponForm.CryoCannon, variant = 0, revealed = true, mk = 1, element = ElementKind.Frost, builtInElement = true, axes = UpgradeAxis.Infusion };
            shards.rounds = weapons.ClipSize(shards.form, shards.variant);
            weapons.Hand[weapons.ActiveIndex] = shards;
            float cycle = weapons.CycleSeconds(shards);
            Assume.That(cycle, Is.GreaterThan(.2f), "a single shot with a cooldown long enough to count frames against");

            // Held for two and a half cooldowns: a shot at once, then one after each full cooldown.
            int shots = 0;
            float last = shards.cooldown;
            weapons.HandleInput(true, true, false);
            if (shards.cooldown > last + .001f) shots++;
            last = shards.cooldown;
            float end = Time.time + cycle * 2.5f;
            while (Time.time < end)
            {
                await Awaitable.NextFrameAsync();
                weapons.HandleInput(false, true, false);
                if (shards.cooldown > last + .001f) shots++;
                last = shards.cooldown;
            }
            weapons.HandleInput(false, false, true);

            Assert.That(shots, Is.EqualTo(3), $"held for {cycle * 2.5f:0.00} s with a {cycle:0.00} s cooldown");
        }
    }
}
