using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The void pillars' view (V2VoidPillarFx), checked from what its renderers draw, in whichever look the run is in: the circle's edge
    /// is the deadly radius from the first frame of the warning to the last frame the pillar stands; the ring from the middle has grown
    /// as far as the warning has run; no column stands before the pillar comes down, and then its veil stands on the deadly radius;
    /// a pillar that is over loses its edge at once and thins out to nothing; views are kept and shown again; a younger pillar keeps
    /// its own view when the list shifts. And in the void trial the Prophet's own pillars are drawn through it.
    /// </summary>
    [TestFixture]
    public class VoidPillarFxTest
    {
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";
        const float Radius = 2.5f, Warning = 1.2f, Life = 1.5f;

        V2VoidPillarFx fx;
        readonly List<Enemy.VoidPillar> pillars = new();

        [SetUp]
        public void SetUp()
        {
            pillars.Clear();
            fx = V2VoidPillarFx.Create(null, "VoidPillarFxTest");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (!fx) return;
            if (Application.isPlaying) Object.Destroy(fx.gameObject);
            else Object.DestroyImmediate(fx.gameObject);
        }

        [Test]
        public void Warning_TheEdgeIsTheDeadlyRadius_TheRingFromTheMiddleHasGrownAsFarAsTheWarningHasRun_NoColumnStands()
        {
            pillars.Add(Pillar(3f, -2f, Warning * .5f));

            fx.Draw(pillars, 0f);

            Assert.That(fx.TryRead(pillars[0].centre, out var half), Is.True, "the pillar has a view");
            Assert.That(half.EdgeShown, Is.True, "the circle shows while it warns");
            Assert.That(half.EdgeRadius, Is.EqualTo(Radius).Within(1e-3f), "its outer edge is the deadly radius");
            Assert.That(half.ClockRadius, Is.EqualTo(Radius * .5f).Within(1e-3f), "half the warning gone: the ring from the middle is half way out");
            Assert.That(half.ColumnShown, Is.False, "no column before the pillar comes down");
            Assert.That(half.CoreRadius, Is.LessThan(Radius * .1f), "only a thread stands over a warning circle");

            pillars[0] = Pillar(3f, -2f, Warning * .9f);
            fx.Draw(pillars, .1f);

            Assert.That(fx.TryRead(pillars[0].centre, out var late), Is.True);
            Assert.That(late.EdgeRadius, Is.EqualTo(Radius).Within(1e-3f), "the edge never moves");
            Assert.That(late.ClockRadius, Is.EqualTo(Radius * .9f).Within(1e-3f), "the ring has nearly reached it");
            Assert.That(late.ColumnShown, Is.False);
            Assert.That(fx.ViewCount, Is.EqualTo(1), "the same view followed the pillar");
        }

        [Test]
        public void Standing_TheVeilStandsOnTheDeadlyRadius_WithTheCoreInsideIt()
        {
            pillars.Add(Pillar(0f, 0f, Warning + .5f));

            fx.Draw(pillars, 0f);

            Assert.That(fx.TryRead(Vector3.zero, out var standing), Is.True, "the pillar has a view");
            Assert.That(standing.ColumnShown, Is.True, "a standing pillar has its column");
            Assert.That(standing.ColumnRadius, Is.EqualTo(Radius).Within(1e-3f), "the column's veil stands on the deadly radius");
            Assert.That(standing.EdgeShown, Is.True);
            Assert.That(standing.EdgeRadius, Is.EqualTo(Radius).Within(1e-3f), "and so does the circle's edge");
            Assert.That(standing.CoreRadius, Is.GreaterThan(0f).And.LessThan(Radius), "the bright core is inside the veil");
        }

        [Test]
        public void LastMomentsOfItsLife_ThePillarStillShowsItsFullWidth()
        {
            pillars.Add(Pillar(0f, 0f, Warning + Life - .05f));

            fx.Draw(pillars, 0f);

            Assert.That(fx.TryRead(Vector3.zero, out var ending), Is.True);
            Assert.That(ending.EdgeRadius, Is.EqualTo(Radius).Within(1e-3f), "it kills until the end, so its edge stays");
            Assert.That(ending.ColumnRadius, Is.EqualTo(Radius).Within(1e-3f), "and its column keeps its width");
        }

        [Test]
        public void PillarOver_ItsEdgeGoesAtOnce_TheColumnThinsOut_ThenNothingIsDrawn()
        {
            pillars.Add(Pillar(0f, 0f, Warning + .5f));
            fx.Draw(pillars, 0f);
            Assume.That(fx.TryRead(Vector3.zero, out var standing), Is.True);
            pillars.Clear();

            fx.Draw(pillars, fx.fadeSeconds * .5f);

            Assert.That(fx.TryRead(Vector3.zero, out var thinning), Is.True, "its view is still there");
            Assert.That(thinning.ThinningOut, Is.True, "thinning out");
            Assert.That(thinning.EdgeShown, Is.False, "the deadly edge goes the moment the pillar does");
            Assert.That(thinning.ClockShown, Is.False, "and the ring inside it");
            Assert.That(thinning.CoreRadius, Is.GreaterThan(0f).And.LessThan(standing.CoreRadius * .5f), "the core has thinned");

            fx.Draw(pillars, fx.fadeSeconds);

            Assert.That(fx.ShownCount, Is.Zero, "nothing is left once it has thinned out");
            Assert.That(Drawing(fx), Is.Zero, "and no renderer still draws");
        }

        [Test]
        public void LaterPillars_AreShownByTheSameViews_MoreAreMadeOnlyForMorePillars()
        {
            for (int i = 0; i < 3; i++) pillars.Add(Pillar(i * 6f, 0f, .2f));
            fx.Draw(pillars, 0f);
            Assert.That(fx.ViewCount, Is.EqualTo(3), "a view for each pillar");
            Assert.That(fx.ShownCount, Is.EqualTo(3));
            pillars.Clear();
            fx.Draw(pillars, fx.fadeSeconds * 2f);
            Assume.That(fx.ShownCount, Is.Zero, "the first pillars are gone");

            for (int i = 0; i < 3; i++) pillars.Add(Pillar(40f + i * 6f, 9f, .1f));
            fx.Draw(pillars, 0f);

            Assert.That(fx.ViewCount, Is.EqualTo(3), "the three views are shown again for the next three pillars");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(fx.TryRead(pillars[i].centre, out var moved), Is.True, "each stands where its new pillar is");
                Assert.That(moved.EdgeRadius, Is.EqualTo(Radius).Within(1e-3f));
            }

            pillars.Add(Pillar(-20f, 0f, .1f));
            pillars.Add(Pillar(-30f, 0f, .1f));
            fx.Draw(pillars, 0f);

            Assert.That(fx.ViewCount, Is.EqualTo(5), "two more pillars, two more views");
        }

        [Test]
        public void OlderPillarEnds_TheListShifts_TheYoungerPillarKeepsItsOwnView()
        {
            pillars.Add(Pillar(0f, 0f, Warning + 1f));
            pillars.Add(Pillar(8f, 0f, Warning * .25f));
            fx.Draw(pillars, 0f);

            pillars.RemoveAt(0);
            pillars[0] = Pillar(8f, 0f, Warning * .5f);
            fx.Draw(pillars, .05f);

            Assert.That(fx.TryRead(new Vector3(8f, 0f, 0f), out var younger), Is.True);
            Assert.That(younger.ThinningOut, Is.False, "the younger pillar is still followed");
            Assert.That(younger.ColumnShown, Is.False, "it still warns: the older pillar's column did not pass to it");
            Assert.That(younger.ClockRadius, Is.EqualTo(Radius * .5f).Within(1e-3f));
            Assert.That(fx.TryRead(Vector3.zero, out var older), Is.True);
            Assert.That(older.ThinningOut, Is.True, "the older pillar's view thins out where it stood");
        }

        [Test]
        public void Clear_HidesEverythingAtOnce()
        {
            pillars.Add(Pillar(0f, 0f, Warning + .5f));
            pillars.Add(Pillar(8f, 0f, .3f));
            fx.Draw(pillars, 0f);
            Assume.That(Drawing(fx), Is.GreaterThan(0), "the pillars are drawn");

            fx.Clear();

            Assert.That(fx.ShownCount, Is.Zero);
            Assert.That(Drawing(fx), Is.Zero, "no renderer still draws");
        }

        /// <summary>In the void trial (the enemy lab shows the illustrated art, so this also runs the V5 layer's part): the Prophet's own
        /// pillars are drawn through the view, at their radius while they warn and while they stand, and leave nothing after their life.</summary>
        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task Trial_TheProphetsPillars_AreDrawnAtTheirRadiusFromWarningToStanding_AndLeaveNothingAfterTheirLife()
        {
            var (game, world, hollow) = await NashProphetTest.StartTrial();
            var b = game.waves.bosses;
            NashProphetTest.ToPhase(hollow, 2, b);
            await NashProphetTest.Until(() => hollow.BossPhase == 2, 2f, "phase 2");
            NashProphetTest.Place(game.player, Query.Flat(world.trialPlatform.position) + Vector3.back * 5f);
            NashProphetTest.Force(hollow, Enemy.VoidPillarMove);
            await NashProphetTest.Until(() => hollow.VoidPillars.Count > 0, b.nashPillarWindup + 2f, "the pillar circles");
            hollow.HoldAttacks(60f);
            var marks = hollow.GetComponent<V2ProphetTrialMarks>();
            Assume.That(marks, Is.Not.Null, "the Prophet carries its trial marks");
            await NashProphetTest.Until(() => marks.Pillars && marks.Pillars.ShownCount >= hollow.VoidPillars.Count, 1f, "a view for every pillar");
            var view = marks.Pillars;
            var cast = new List<Enemy.VoidPillar>(hollow.VoidPillars);
            Assume.That(cast.TrueForAll(p => !p.Standing), Is.True, "the circles still warn");

            foreach (var pillar in cast)
            {
                Assert.That(view.TryRead(pillar.centre, out var warning), Is.True, "every pillar has its view");
                Assert.That(warning.EdgeRadius, Is.EqualTo(pillar.radius).Within(1e-3f), "a warning circle's edge is the deadly radius");
                Assert.That(warning.ColumnShown, Is.False, "no column before the pillar comes down");
            }

            await NashProphetTest.Until(() => hollow.VoidPillars.Count == cast.Count && AllStanding(hollow), b.nashPillarWarning + 1f, "the pillars stand");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            foreach (var pillar in cast)
            {
                Assert.That(view.TryRead(pillar.centre, out var standing), Is.True);
                Assert.That(standing.ColumnShown, Is.True, "a standing pillar has its column");
                Assert.That(standing.ColumnRadius, Is.EqualTo(pillar.radius).Within(1e-3f), "its veil stands on the deadly radius");
                Assert.That(standing.EdgeRadius, Is.EqualTo(pillar.radius).Within(1e-3f), "and so does its circle's edge");
            }

            await NashProphetTest.Until(() => hollow.VoidPillars.Count == 0, b.nashPillarLife + 1f, "the pillars' life is over");
            await NashProphetTest.Until(() => view.ShownCount == 0, view.fadeSeconds + 1f, "the views have thinned out");
            Assert.That(Drawing(view), Is.Zero, "nothing is left drawn on the platform");
        }

        static bool AllStanding(Enemy hollow)
        {
            foreach (var pillar in hollow.VoidPillars)
                if (!pillar.Standing) return false;
            return true;
        }

        static Enemy.VoidPillar Pillar(float x, float z, float age) =>
            new() { centre = new Vector3(x, 0f, z), radius = Radius, age = age, warning = Warning, life = Life };

        /// <summary>Renderers of the view that draw now.</summary>
        static int Drawing(V2VoidPillarFx view)
        {
            int count = 0;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) count++;
            return count;
        }
    }
}
