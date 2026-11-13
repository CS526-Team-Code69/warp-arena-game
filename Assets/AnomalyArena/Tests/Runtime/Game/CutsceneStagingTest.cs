using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The story cutscenes staged in whitebox (V2Cutscene): the shot lists follow the storyboard (M1 to M9, L1 to L5, E1 to E3, in order,
    /// each of four to seven seconds, the memory under fifty in all, every beat inside its shot); every shot builds, plays to its end and
    /// keeps its main figure in the picture at its first and its last frame, with everything it made on the stage's layer, the only one
    /// the cutscene camera draws; and once the cutscene is over nothing of the stage is left to draw and its picture is let go.
    /// </summary>
    [TestFixture]
    public class CutsceneStagingTest
    {
        V2Cutscene stage;

        static IEnumerable<CutsceneShot> AllShots => V2Cutscene.Memory.Concat(V2Cutscene.Ignition).Concat(V2Cutscene.Ending);

        static IEnumerable<string> ShotIds => AllShots.Select(shot => shot.id).ToArray();

        [TearDown]
        public void TearDown()
        {
            if (!stage) return;
            if (Application.isPlaying) Object.Destroy(stage.gameObject);
            else Object.DestroyImmediate(stage.gameObject);
        }

        [Test]
        public void ShotLists_FollowTheStoryboard_TheMemoryUnderFiftySeconds()
        {
            Assert.That(V2Cutscene.Memory.Select(shot => shot.id), Is.EqualTo(new[] { "M1", "M2", "M3", "M4", "M5", "M6", "M7", "M8", "M9" }));
            Assert.That(V2Cutscene.Ignition.Select(shot => shot.id), Is.EqualTo(new[] { "L1", "L2", "L3", "L4", "L5" }));
            Assert.That(V2Cutscene.Ending.Select(shot => shot.id), Is.EqualTo(new[] { "E1", "E2", "E3" }));
            Assert.That(V2Cutscene.Memory.Sum(shot => shot.seconds), Is.LessThanOrEqualTo(50f), "the memory lasts under fifty seconds");
            foreach (var shot in AllShots)
            {
                Assert.That(shot.seconds, Is.InRange(4f, 7f), $"{shot.id} lasts four to seven seconds");
                Assert.That(shot.stage, Is.Not.Null, $"{shot.id} is staged");
                foreach (var beat in shot.beats) Assert.That(beat.at, Is.InRange(0f, shot.seconds), $"{shot.id}: every beat falls inside the shot");
            }
        }

        [TestCaseSource(nameof(ShotIds)), CreateScene]
        public void EveryShot_PlaysToItsEnd_ItsFigureInThePicture_AllOnTheStageLayer(string id)
        {
            var shot = AllShots.First(each => each.id == id);
            stage = V2Cutscene.Create("CutsceneStagingTest");

            stage.Show(shot);

            Assert.That(stage.Picture, Is.Not.Null, "the camera draws into a picture");
            Assert.That(stage.View.cullingMask, Is.EqualTo(1 << V2Cutscene.StageLayer), "the camera draws the stage's layer only");
            AssertInPicture(id, "at its first frame");
            for (float t = 0f; t < shot.seconds; t += .1f) stage.Advance(.1f);
            AssertInPicture(id, "at its last frame");
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, "the shot has a set");
            Assert.That(renderers.Where(r => r.gameObject.layer != V2Cutscene.StageLayer).Select(r => r.name), Is.Empty, "everything is on the stage's layer");
        }

        [Test, CreateScene]
        public void End_NothingOfTheStageIsLeftToDraw_ThePictureIsLetGo()
        {
            stage = V2Cutscene.Create("CutsceneStagingTest");
            stage.Show(V2Cutscene.Memory[0]);
            stage.Advance(1f);
            Assume.That(stage.LiveCubes, Is.GreaterThan(0), "the burning towers have cubes in the air");

            stage.End();

            Assert.That(stage.Picture, Is.Null, "the picture is let go");
            Assert.That(stage.Shot, Is.Null, "no shot is on the stage");
            Assert.That(stage.View.enabled, Is.False, "the camera stops");
            Assert.That(stage.GetComponentsInChildren<Renderer>().Where(r => r.enabled), Is.Empty, "nothing of the stage is left to draw");
        }

        void AssertInPicture(string id, string when)
        {
            Assert.That(stage.TrySubjectPoint(out var point), Is.True, $"{id} names its main figure");
            var v = stage.View.WorldToViewportPoint(point);
            Assert.That(v.z, Is.GreaterThan(0f), $"{id}: its figure is in front of the camera {when}");
            Assert.That(v.x, Is.InRange(.02f, .98f), $"{id}: its figure is in the picture across {when}");
            Assert.That(v.y, Is.InRange(.02f, .98f), $"{id}: its figure is in the picture up and down {when}");
        }
    }
}
