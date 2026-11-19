using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The story cutscenes staged in whitebox (V2Cutscene): the shot lists follow the script (M11 to M20, L11 to L15, E11 to E14, in
    /// order, each of 3.5 to 10 seconds, 61, 38.5 and 30.5 seconds in all, the memory's flashbacks marked as memories, every beat inside
    /// its shot, and the 24 lines C10 to E08 said once each in their order); a line waits for the one before it and a shot outlasts its
    /// last line (V2Campaign.Schedule); every shot builds, plays to its end and keeps its main figure in the picture at its first and its
    /// last frame, with everything it made on the stage's layer, the only one the cutscene camera draws; and once the cutscene is over
    /// nothing of the stage is left to draw and its picture is let go.
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
        public void ShotLists_FollowTheScript_SixtyOneSecondsOfMemory()
        {
            Assert.That(V2Cutscene.Memory.Select(shot => shot.id), Is.EqualTo(new[] { "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20" }));
            Assert.That(V2Cutscene.Ignition.Select(shot => shot.id), Is.EqualTo(new[] { "L11", "L12", "L13", "L14", "L15" }));
            Assert.That(V2Cutscene.Ending.Select(shot => shot.id), Is.EqualTo(new[] { "E11", "E12", "E13", "E14" }));
            Assert.That(V2Cutscene.Memory.Sum(shot => shot.seconds), Is.EqualTo(61f).Within(.01f), "the memory lasts 61 seconds as written");
            Assert.That(V2Cutscene.Ignition.Sum(shot => shot.seconds), Is.EqualTo(38.5f).Within(.01f), "the failed ignition lasts 38.5 seconds");
            Assert.That(V2Cutscene.Ending.Sum(shot => shot.seconds), Is.EqualTo(30.5f).Within(.01f), "the ending lasts 30.5 seconds");
            Assert.That(AllShots.Where(shot => shot.time == CutsceneTime.Memory).Select(shot => shot.id),
                Is.EqualTo(new[] { "M12", "M13", "M14", "M15", "M16", "M17", "M19" }), "the flashbacks are memories, the rest the present");
            var lines = AllShots.SelectMany(shot => shot.beats).Where(beat => beat.line.HasValue).Select(beat => beat.line.Value);
            var script = Enumerable.Range((int)V2Campaign.Line.C10, 24).Select(i => (V2Campaign.Line)i);
            Assert.That(lines, Is.EqualTo(script), "the 24 lines C10 to E08, each once, in their order");
            foreach (var shot in AllShots)
            {
                Assert.That(shot.seconds, Is.InRange(3.5f, 10f), $"{shot.id} lasts 3.5 to 10 seconds");
                Assert.That(shot.stage, Is.Not.Null, $"{shot.id} is staged");
                foreach (var beat in shot.beats) Assert.That(beat.at, Is.InRange(0f, shot.seconds), $"{shot.id}: every beat falls inside the shot");
            }
        }

        [Test]
        public void Schedule_ALineWaitsForTheOneBefore_TheShotOutlastsItsLastLine_ItsClosingMovesWithIt()
        {
            var beats = new List<(float at, CutsceneBeat beat)>();
            var ignition = V2Cutscene.Ignition[0]; // L11: F06 at 0.4 s, F07 at 3.8 s, the engine's sound at 0
            Assert.That(V2Campaign.Schedule(ignition, _ => 2f, beats), Is.EqualTo(7.5f), "short lines: the shot keeps its seconds");
            Assert.That(beats.Where(b => b.beat.line.HasValue).Select(b => b.at), Is.EqualTo(new[] { .4f, 3.8f }), "short lines keep their moments");

            float length = V2Campaign.Schedule(ignition, _ => 5f, beats);
            float second = .4f + 5f + V2Campaign.CutsceneLineGap;
            Assert.That(beats.Where(b => b.beat.line.HasValue).Select(b => b.at), Is.EqualTo(new[] { .4f, second }).Within(1e-4f), "F07 waits for F06");
            Assert.That(length, Is.EqualTo(second + 5f + V2Campaign.CutsceneLineTail).Within(1e-4f), "the shot lasts until F07 has finished");
            Assert.That(beats.First(b => b.beat.sound.HasValue).at, Is.EqualTo(0f), "a sound keeps its moment");

            var record = V2Cutscene.Memory[0]; // M11: closes on a white-out at 6.2 s with the memory's sound
            length = V2Campaign.Schedule(record, _ => 5f, beats);
            float grown = length - record.seconds;
            Assert.That(grown, Is.GreaterThan(0f), "two long lines make the shot longer");
            Assert.That(beats.First(b => b.beat.whiteOut > 0f).at, Is.EqualTo(6.2f + grown).Within(1e-4f), "the white-out still closes the shot");
            Assert.That(beats.First(b => b.beat.sound == CampaignSfx.MemoryCgTransition).at, Is.EqualTo(6.2f + grown).Within(1e-4f), "and its sound with it");
            Assert.That(beats.First(b => b.beat.sound == CampaignSfx.RecordChime).at, Is.EqualTo(0f), "the opening sound keeps its moment");
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
            stage.Show(AllShots.First(shot => shot.id == "M12"));
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
