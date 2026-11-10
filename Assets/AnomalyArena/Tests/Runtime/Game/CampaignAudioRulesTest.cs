using System.Collections.Generic;
using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>
    /// The campaign sound effects' rules apart from the scene (CampaignAudio, 2026-11-10): which file each sound is read from, which
    /// six loop, and how loud each loop wants to be for a given moment of the campaign (LoopLevel), including the seam's cross-fade.
    /// </summary>
    [TestFixture]
    public class CampaignAudioRulesTest
    {
        static readonly CampaignAudio.LoopRanges Ranges = CampaignAudio.LoopRanges.Default;

        static CampaignAudio.LoopInputs At(CampaignStage stage, int day = 1)
        {
            var s = CampaignAudio.LoopInputs.Far;
            s.stage = stage;
            s.day = day;
            s.overrunAt = 3;
            return s;
        }

        [Test]
        public void FileName_IsTheValueInLowerCaseWords_AndEveryFileIsDifferent()
        {
            Assert.That(CampaignAudio.FileName(CampaignSfx.NightFall), Is.EqualTo("SFX-night-fall"));
            Assert.That(CampaignAudio.FileName(CampaignSfx.OrePickupE), Is.EqualTo("SFX-ore-pickup-e"));
            Assert.That(CampaignAudio.FileName(CampaignSfx.MemoryCgTransition), Is.EqualTo("SFX-memory-cg-transition"));
            Assert.That(CampaignAudio.FileName(CampaignSfx.TutorialCrashAmbience), Is.EqualTo("SFX-tutorial-crash-ambience"));
            var names = new HashSet<string>();
            int loops = 0;
            foreach (CampaignSfx sfx in System.Enum.GetValues(typeof(CampaignSfx)))
            {
                Assert.That(names.Add(CampaignAudio.FileName(sfx)), Is.True, $"{sfx} shares its file name");
                if (CampaignAudio.Loops(sfx)) loops++;
                Assert.That(CampaignAudio.DefaultGain(sfx), Is.InRange(.05f, 1f), $"{sfx} default gain");
            }
            Assert.That(names.Count, Is.EqualTo(51), "one value per sound of the 2026-11-10 set");
            Assert.That(loops, Is.EqualTo(6), "darkness, shadow approach, fuel fill, night ambience, damaged base, crash site");
        }

        [Test]
        public void Night_DarknessAndNightAmbienceTakeTurns_AndNeitherPlaysByDay()
        {
            var dark = At(CampaignStage.Night);
            dark.inDarkness = true;
            var lit = At(CampaignStage.Night);

            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.DarknessLoop, dark, Ranges), Is.EqualTo(1f));
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.NightAmbienceLoop, dark, Ranges), Is.EqualTo(0f), "out of the light: only the darkness");
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.DarknessLoop, lit, Ranges), Is.EqualTo(0f));
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.NightAmbienceLoop, lit, Ranges), Is.EqualTo(Ranges.calmNight).Within(1e-5f), "in the light: the calm night");
            foreach (var stage in new[] { CampaignStage.Day, CampaignStage.Dusk, CampaignStage.Base, CampaignStage.Story })
            {
                var s = At(stage);
                s.inDarkness = true;
                Assert.That(CampaignAudio.LoopLevel(CampaignSfx.DarknessLoop, s, Ranges), Is.EqualTo(0f), $"{stage}: no darkness loop");
                Assert.That(CampaignAudio.LoopLevel(CampaignSfx.NightAmbienceLoop, s, Ranges), Is.EqualTo(0f), $"{stage}: no night ambience");
            }
        }

        [Test]
        public void NightAmbience_RisesWithTheShadowsInTheRegion_UpToFullAtOverrun()
        {
            var s = At(CampaignStage.Night);
            s.shadowsHere = 0;
            float calm = CampaignAudio.LoopLevel(CampaignSfx.NightAmbienceLoop, s, Ranges);
            s.shadowsHere = 1;
            float one = CampaignAudio.LoopLevel(CampaignSfx.NightAmbienceLoop, s, Ranges);
            s.shadowsHere = 3;
            float overrun = CampaignAudio.LoopLevel(CampaignSfx.NightAmbienceLoop, s, Ranges);
            s.shadowsHere = 9;
            float crowded = CampaignAudio.LoopLevel(CampaignSfx.NightAmbienceLoop, s, Ranges);

            Assert.That(one, Is.GreaterThan(calm));
            Assert.That(one, Is.EqualTo(Ranges.calmNight + (1f - Ranges.calmNight) / 3f).Within(1e-5f), "a third of the way at one shadow of three");
            Assert.That(overrun, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(crowded, Is.EqualTo(1f).Within(1e-5f), "never above full");
        }

        [Test]
        public void ShadowApproach_FullWithinNear_SilentFromFar_SquareCurveBetween_AndOnlyAtNight()
        {
            var s = At(CampaignStage.Night);
            float near = Ranges.shadowNear, far = Ranges.shadowFar;

            s.nearestShadow = near * .5f;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.ShadowApproachLoop, s, Ranges), Is.EqualTo(1f));
            s.nearestShadow = (near + far) * .5f;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.ShadowApproachLoop, s, Ranges), Is.EqualTo(.25f).Within(1e-5f), "halfway: a quarter");
            s.nearestShadow = far;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.ShadowApproachLoop, s, Ranges), Is.EqualTo(0f));
            s.nearestShadow = float.PositiveInfinity;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.ShadowApproachLoop, s, Ranges), Is.EqualTo(0f), "no shadow, no sound");
            var dusk = At(CampaignStage.Dusk);
            dusk.nearestShadow = 0f;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.ShadowApproachLoop, dusk, Ranges), Is.EqualTo(0f), "not in the afternoon");
        }

        [Test]
        public void CrashSiteAmbience_OnlyOnDayZeroFromTheTitleCardToTheWalksEnd()
        {
            foreach (var stage in new[] { CampaignStage.TitleCard, CampaignStage.Loadout, CampaignStage.Tutorial })
            {
                var s = At(stage, 0);
                s.toCrash = 0f;
                Assert.That(CampaignAudio.LoopLevel(CampaignSfx.TutorialCrashAmbience, s, Ranges), Is.EqualTo(1f), $"{stage} at the crash site");
                s.toCrash = Ranges.crashFar + 1f;
                Assert.That(CampaignAudio.LoopLevel(CampaignSfx.TutorialCrashAmbience, s, Ranges), Is.EqualTo(0f), $"{stage} far from it");
            }
            var night0 = At(CampaignStage.Night, 0);
            night0.toCrash = 0f;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.TutorialCrashAmbience, night0, Ranges), Is.EqualTo(0f), "not on the tutorial night");
            var day1 = At(CampaignStage.Tutorial, 1);
            day1.toCrash = 0f;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.TutorialCrashAmbience, day1, Ranges), Is.EqualTo(0f), "not after Day 0");
        }

        [Test]
        public void DamagedBaseAndFuel_FollowTheirState()
        {
            var s = At(CampaignStage.Day);
            s.toBase = 0f;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.BaseDamagedLoop, s, Ranges), Is.EqualTo(0f), "no broken room");
            s.roomBroken = true;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.BaseDamagedLoop, s, Ranges), Is.EqualTo(1f));
            s.toBase = Ranges.baseFar;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.BaseDamagedLoop, s, Ranges), Is.EqualTo(0f), "far from the base");
            var story = At(CampaignStage.Story);
            story.roomBroken = true; story.toBase = 0f;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.BaseDamagedLoop, story, Ranges), Is.EqualTo(0f), "not in a story scene");

            var fuel = At(CampaignStage.Base);
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.FuelFillLoop, fuel, Ranges), Is.EqualTo(0f));
            fuel.fueling = true;
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.FuelFillLoop, fuel, Ranges), Is.EqualTo(1f));
            Assert.That(CampaignAudio.LoopLevel(CampaignSfx.NightFall, fuel, Ranges), Is.EqualTo(0f), "a one-shot is never a loop");
        }

        [Test]
        public void Seam_IsAnEqualPowerCrossFade()
        {
            foreach (float t in new[] { 0f, .25f, .5f, .75f, 1f })
            {
                float a = CampaignAudio.SeamIn(t), b = CampaignAudio.SeamOut(t);
                Assert.That(a * a + b * b, Is.EqualTo(1f).Within(1e-5f), $"power at {t}");
            }
            Assert.That(CampaignAudio.SeamIn(0f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(CampaignAudio.SeamIn(1f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(CampaignAudio.SeamIn(2f), Is.EqualTo(1f).Within(1e-6f), "clamped past the seam's end");
        }
    }
}
