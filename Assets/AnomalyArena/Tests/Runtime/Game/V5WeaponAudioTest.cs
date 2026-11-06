using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class V5WeaponAudioTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChargedRifle_PressStartsTheLoop_ItsPitchFollowsTheCharge_ReleaseCutsItInTheSameFrame()
        {
            var (game, audio, weapons) = await Ready();
            var rifle = Equip(game, WeaponForm.Rifle, 0);

            weapons.HandleInput(true, true, false);
            audio.Tick();
            bool startedOnPress = audio.LoopLive(V5Sfx.LoopChargeRifle);
            var source = audio.LoopSource(V5Sfx.LoopChargeRifle);
            bool playing = source && source.isPlaying;
            float lowPitch = source ? source.pitch : 0f;
            rifle.charge = .9f;
            audio.Tick();
            float highPitch = source ? source.pitch : 0f, held = source ? source.volume : 0f;

            weapons.HandleInput(false, false, true);
            audio.Tick();
            bool liveAfterRelease = audio.LoopLive(V5Sfx.LoopChargeRifle);
            float cutTo = source ? source.volume : 1f;
            audio.Tick();

            Assert.That(startedOnPress && playing, Is.True, "the press starts the loop in the same frame");
            Assert.That(highPitch, Is.GreaterThan(lowPitch), "its pitch follows the charge");
            Assert.That(liveAfterRelease, Is.False, "the release ends it in the same frame");
            Assert.That(cutTo, Is.LessThan(held * .5f), "and it is already down by more than half in that frame");
            Assert.That(source.isPlaying, Is.False, "and stopped one frame later");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChargedRifle_HeldAtFull_RingsOnce_ThenOnceMoreOnTheNextCharge()
        {
            var (game, audio, weapons) = await Ready();
            var rifle = Equip(game, WeaponForm.Rifle, 0);
            int before = audio.Plays(V5Sfx.ChargeFull);

            weapons.HandleInput(true, true, false);
            audio.Tick();
            int notYet = audio.Plays(V5Sfx.ChargeFull) - before;
            rifle.charge = 1f;
            audio.Tick();

            await RealSeconds(.25f);
            audio.Tick();
            int whileHeld = audio.Plays(V5Sfx.ChargeFull) - before;
            weapons.HandleInput(false, false, true);
            audio.Tick();
            rifle.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            rifle.charge = 1f;
            audio.Tick();
            int nextCharge = audio.Plays(V5Sfx.ChargeFull) - before;

            Assert.That(notYet, Is.EqualTo(0), "an empty charge does not ring");
            Assert.That(whileHeld, Is.EqualTo(1), "full rings once however long it is held");
            Assert.That(nextCharge, Is.EqualTo(2), "and once more on the next full charge");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChargedRifle_SwitchingWeapon_CutsTheLoopAndPlaysTheCancel()
        {
            var (game, audio, weapons) = await Ready();
            var rifle = Equip(game, WeaponForm.Rifle, 0);
            weapons.Hand[1 - weapons.ActiveIndex] = new RunWeapon { form = WeaponForm.Knife, variant = 1, revealed = true };
            int cancels = audio.Plays(V5Sfx.ChargeCancel), shots = audio.Plays(V5Sfx.RifleShot);
            weapons.HandleInput(true, true, false);
            audio.Tick();
            Assume.That(audio.LoopLive(V5Sfx.LoopChargeRifle), Is.True, "the rifle is charging");

            weapons.Switch();
            audio.Tick();

            Assert.That(audio.LoopLive(V5Sfx.LoopChargeRifle), Is.False, "the switch ends the charge sound in the same frame");
            Assert.That(audio.Plays(V5Sfx.ChargeCancel), Is.EqualTo(cancels + 1), "the cancel is heard");
            Assert.That(audio.Plays(V5Sfx.RifleShot), Is.EqualTo(shots), "and no shot");
            Assert.That(rifle.charging, Is.False, "the weapon itself was cancelled");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChargedRifle_MoreCharge_MoreWeightInTheShot()
        {
            var (game, audio, weapons) = await Ready();
            var rifle = Equip(game, WeaponForm.Rifle, 0);

            weapons.HandleInput(true, true, false);
            rifle.charge = .25f;
            weapons.HandleInput(false, false, true);
            float light = audio.LastGain(V5Sfx.RifleHeavy), lightPitch = audio.LastPitch(V5Sfx.RifleShot);
            await RealSeconds(.15f);
            rifle.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            rifle.charge = 1f;
            weapons.HandleInput(false, false, true);
            float heavy = audio.LastGain(V5Sfx.RifleHeavy), heavyPitch = audio.LastPitch(V5Sfx.RifleShot);

            Assert.That(heavy, Is.GreaterThan(light * 2f), "a full charge carries more of the heavy layer");
            Assert.That(heavyPitch, Is.LessThan(lightPitch), "and the shot sits lower");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Lance_ChargesWithItsOwnLoop_AndItStopsWhenItFiresItself()
        {
            var (game, audio, weapons) = await Ready();
            var lance = Equip(game, WeaponForm.Railgun, 0);
            int shots = audio.Plays(V5Sfx.RailShot), full = audio.Plays(V5Sfx.ChargeFull);

            weapons.HandleInput(true, true, false);
            audio.Tick();
            bool own = audio.LoopLive(V5Sfx.LoopChargeRail), notRifle = !audio.LoopLive(V5Sfx.LoopChargeRifle);
            await Until(() => !lance.charging, 3f, "the Lance fires itself at full charge");
            audio.Tick();

            Assert.That(own && notRifle, Is.True, "it has its own charge sound");
            Assert.That(audio.LoopLive(V5Sfx.LoopChargeRail), Is.False, "the sound ends with the charge");
            Assert.That(audio.Plays(V5Sfx.RailShot), Is.EqualTo(shots + 1), "and its shot is heard");
            Assert.That(audio.Plays(V5Sfx.ChargeFull), Is.EqualTo(full), "it never holds at full, so it does not ring");
        }

        [TestCase(WeaponForm.Flamethrower, 0, V5Sfx.LoopFlame)]
        [TestCase(WeaponForm.CryoCannon, 1, V5Sfx.LoopBlizzard)]
        [TestCase(WeaponForm.Minigun, 0, V5Sfx.LoopMinigun)]
        [TestCase(WeaponForm.Minigun, 1, V5Sfx.LoopMinigun)]
        [LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Trigger_LoopLastsExactlyAsLongAsItIsDown(WeaponForm form, int variant, V5Sfx loop)
        {
            var (game, audio, weapons) = await Ready();
            Equip(game, form, variant);

            weapons.HandleInput(true, true, false);
            audio.Tick();
            bool pressed = audio.LoopLive(loop);
            weapons.HandleInput(false, true, false);
            audio.Tick();
            bool held = audio.LoopLive(loop);
            weapons.HandleInput(false, false, true);
            audio.Tick();
            bool released = audio.LoopLive(loop);

            Assert.That(pressed, Is.True, "it starts on the frame of the press");
            Assert.That(held, Is.True, "it runs while held");
            Assert.That(released, Is.False, "it ends on the frame of the release");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SpinUp_PitchClimbsWithTheSpin_AndTheBarrelsRunDownOnRelease()
        {
            var (game, audio, weapons) = await Ready();
            var gun = Equip(game, WeaponForm.Minigun, 0);
            int spindowns = audio.Plays(V5Sfx.MinigunSpindown);

            weapons.HandleInput(true, true, false);
            audio.Tick();
            float low = audio.LoopSource(V5Sfx.LoopMinigun).pitch;
            gun.heldTime = 3f;
            audio.Tick();
            float high = audio.LoopSource(V5Sfx.LoopMinigun).pitch;
            weapons.HandleInput(false, false, true);
            audio.Tick();

            Assert.That(high, Is.GreaterThan(low), "the pitch climbs with the spin");
            Assert.That(audio.Plays(V5Sfx.MinigunSpindown), Is.EqualTo(spindowns + 1), "the release runs the barrels down");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReverseShot_EmptyClip_ReloadIsHeard_PressingFireClicks_TheEndIsHeard()
        {
            var (game, audio, weapons) = await Ready();
            var rifle = Equip(game, WeaponForm.Rifle, 1);
            audio.Tick();
            int starts = audio.Plays(V5Sfx.ReloadClip), done = audio.Plays(V5Sfx.ReloadDone), clicks = audio.Plays(V5Sfx.DryClick);

            rifle.reload = .5f;
            audio.Tick();
            int started = audio.Plays(V5Sfx.ReloadClip) - starts;
            weapons.HandleInput(true, true, false);
            int clicked = audio.Plays(V5Sfx.DryClick) - clicks;
            weapons.HandleInput(false, false, true);
            rifle.reload = 0f;
            audio.Tick();

            Assert.That(started, Is.EqualTo(1), "the reload is heard when it starts");
            Assert.That(clicked, Is.EqualTo(1), "pressing fire with an empty clip clicks");
            Assert.That(audio.Plays(V5Sfx.ReloadDone), Is.EqualTo(done + 1), "the end of the reload is heard");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ElementHit_AThrongOfHits_IsHeldDown_ATrickleIsNotAHit()
        {
            var (game, audio, _) = await Ready();
            await RealSeconds(.25f);
            int fires = audio.Plays(V5Sfx.ElementFire), frosts = audio.Plays(V5Sfx.ElementFrost);
            Vector3 at = game.player.Position;

            for (int i = 0; i < 12; i++) V5WeaponAudio.ElementHit(ElementKind.Fire, at);
            int firesAtOnce = audio.Plays(V5Sfx.ElementFire) - fires;
            V5WeaponAudio.ElementHit(ElementKind.Frost, at);
            int otherInTheSameMoment = audio.Plays(V5Sfx.ElementFrost) - frosts;
            await RealSeconds(.2f);
            V5WeaponAudio.ElementHit(ElementKind.Frost, at, .05f);
            int trickle = audio.Plays(V5Sfx.ElementFrost) - frosts;
            V5WeaponAudio.ElementHit(ElementKind.Frost, at, 1f);
            int hit = audio.Plays(V5Sfx.ElementFrost) - frosts;

            Assert.That(firesAtOnce, Is.EqualTo(1), "twelve hits in a moment are one sound");
            Assert.That(otherInTheSameMoment, Is.EqualTo(0), "the elements wait a little for each other");
            Assert.That(trickle, Is.EqualTo(0), "a per-frame trickle of stacks is no hit");
            Assert.That(hit, Is.EqualTo(1), "a real hit is heard");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ElementHit_IsQuieterThanTheRifleShot()
        {
            var (game, audio, _) = await Ready();
            await RealSeconds(.25f);
            Vector3 at = game.player.Position;

            audio.Play(V5Sfx.RifleShot);
            float rifle = audio.LastGain(V5Sfx.RifleShot) * Loudest(audio.ClipOf(V5Sfx.RifleShot));
            await RealSeconds(.2f);
            foreach (ElementKind element in Enum.GetValues(typeof(ElementKind)))
            {
                if (element == ElementKind.None) continue;
                var sfx = (V5Sfx)((int)V5Sfx.ElementFire + (int)element - 1);
                await RealSeconds(.2f);
                V5WeaponAudio.ElementHit(element, at);
                Assert.That(audio.LastGain(sfx) * Loudest(audio.ClipOf(sfx)), Is.LessThan(rifle), element + " sits under the rifle");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task EveryEntry_HasItsClip_AndABlowStartsAtOnce()
        {
            await Until(() => V5WeaponAudio.Instance, 12f, "the sound layer starts with the scene");
            var audio = V5WeaponAudio.Instance;
            foreach (V5Sfx sfx in Enum.GetValues(typeof(V5Sfx)))
            {
                var clip = audio.ClipOf(sfx);
                Assert.That(clip, Is.Not.Null, sfx + " has its file");
                Assert.That(clip.channels, Is.EqualTo(1), sfx + " is mono");

                if (sfx is V5Sfx.EnergyRise or V5Sfx.ScepterRaise || sfx.ToString().StartsWith("Loop")) continue;
                Assert.That(StartMilliseconds(clip), Is.LessThanOrEqualTo(10f), sfx + " reaches a tenth of its peak within 10 ms");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SavedBanks_HitsAndShots_PlayTheClipsBuiltAtRuntime_MusicKeepsItsOwn()
        {
            await Until(() => V5WeaponAudio.Instance && V3WorldAudio.Instance && V2Audio.Instance, 12f, "the banks start with the scene");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();

            string Swing() { foreach (var s in V3WorldAudio.Instance.bank) if (s != null && s.cue == ArenaWorldCue.NpcSwing) return s.clip.name; return ""; }
            string Music() { foreach (var s in V3WorldAudio.Instance.bank) if (s != null && s.cue == ArenaWorldCue.CombatMusic) return s.clip.name; return ""; }
            string Impact() { foreach (var e in V2Audio.Instance.clips) if (e != null && e.cue == ArenaSoundCue.Impact) return e.clip.name; return ""; }

            Assert.That(Swing(), Is.EqualTo("bank-npc-swing"), "an enemy's swing");
            Assert.That(Impact(), Is.EqualTo("bank-weapon-impact"), "a weapon's impact");
            Assert.That(Music(), Is.EqualTo("combat-music"), "the music stays the saved clip");
        }

        static float[] Samples(AudioClip clip)
        {
            clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            return data;
        }

        static float Loudest(AudioClip clip)
        {
            var data = Samples(clip);
            int window = Mathf.Max(1, clip.frequency / 50);
            float best = 0f, sum = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i] * data[i];
                if (i >= window) sum -= data[i - window] * data[i - window];
                best = Mathf.Max(best, sum);
            }
            return Mathf.Sqrt(best / window);
        }

        static float StartMilliseconds(AudioClip clip)
        {
            var data = Samples(clip);
            float peak = 0f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < data.Length; i++)
                if (Mathf.Abs(data[i]) > peak * .1f) return i * 1000f / clip.frequency;
            return float.PositiveInfinity;
        }

        static async Task<(GameManager game, V5WeaponAudio audio, RunWeapons weapons)> Ready()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            await Until(() => game.CanUseCombatInput, 3f, "combat input opens");
            var audio = V5WeaponAudio.Instance;
            Assume.That(audio, Is.Not.Null, "V2Audio adds the sound layer");
            return (game, audio, game.runWeapons);
        }

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
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

        static async Task RealSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
        }
    }
}
