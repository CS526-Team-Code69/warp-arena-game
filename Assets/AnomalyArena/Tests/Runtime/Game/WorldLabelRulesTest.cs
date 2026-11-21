using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The world labels' rules that need no scene (V2WorldLabels): when every enemy's kind is named, and which kinds a first sight names and
    /// introduces; where an edge notice sits and which way it points; and that the 2.5D view's orthographic camera does not turn a far
    /// place on its own side round to the opposite edge.
    /// </summary>
    [TestFixture]
    public class WorldLabelRulesTest
    {
        // The notice area of a test: its middle is (600, 400).
        static readonly Rect Area = new(100f, 200f, 1000f, 400f);

        GameObject cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (cameraObject) Object.DestroyImmediate(cameraObject);
        }

        [TestCase(EnemyKind.Grunt, ExpectedResult = false, TestName = "The Grunt, every player's first monster, gets no first-sight notice")]
        [TestCase(EnemyKind.Hollow, ExpectedResult = false, TestName = "The Nash Prophet, named by its own plate, gets no first-sight notice")]
        [TestCase(EnemyKind.Tyrant, ExpectedResult = false, TestName = "The Tyrant King, named by its own plate, gets no first-sight notice")]
        [TestCase(EnemyKind.Spitter, ExpectedResult = true, TestName = "The Spitter is named and introduced at first sight")]
        [TestCase(EnemyKind.Shade, ExpectedResult = true, TestName = "The Shade is named and introduced at first sight")]
        public bool IntroducesOnFirstSight_EveryKindButTheGruntAndTheBosses(EnemyKind kind) => V2WorldLabels.IntroducesOnFirstSight(kind);

        [Test]
        public void FirstMeeting_AKindIntroduced_IsNamedForItsStretch_TheGruntAndAKindNotMetAreNot()
        {
            V2WorldLabels.ForgetFirstSights();
            try
            {
                V2WorldLabels.FirstMeeting(EnemyKind.Spitter);
                V2WorldLabels.FirstMeeting(EnemyKind.Grunt);

                Assert.That(V2WorldLabels.NamedForFirstSight(EnemyKind.Spitter), Is.True, "named over its bar while the stretch it was met in lasts");
                Assert.That(V2WorldLabels.NamedForFirstSight(EnemyKind.Grunt), Is.False, "the Grunt is never named for a first sight");
                Assert.That(V2WorldLabels.NamedForFirstSight(EnemyKind.Warden), Is.False, "nor is a kind not met");
            }
            finally
            {
                V2WorldLabels.ForgetFirstSights();
            }
        }

        [Test]
        public void FirstSightNotice_TheGruntAndTheBosses_HaveNone()
        {
            Assert.That(V2WorldLabels.FirstSightNotice(EnemyKind.Grunt), Is.Empty);
            Assert.That(V2WorldLabels.FirstSightNotice(EnemyKind.Hollow), Is.Empty);
            Assert.That(V2WorldLabels.FirstSightNotice(EnemyKind.Tyrant), Is.Empty);
        }

        [Test]
        public void HoleFirstFed_QueuesItsBannerOnceARun_AndAgainAfterTheRunIsForgotten()
        {
            Assume.That(V5HudKit.On, Is.True, "the whole game's pages (the English Alpha shows no such banner)");
            V2WorldLabels.ForgetFirstSights();
            try
            {
                Assert.That(V2WorldLabels.HoleFirstFed(), Is.True, "the first monster the hole swallows brings the banner");
                Assert.That(V2WorldLabels.HoleFirstFed(), Is.False, "the second does not");
                Assert.That(V2WorldLabels.HoleFirstFed(), Is.False, "nor any later one");
                V2WorldLabels.ForgetFirstSights();
                Assert.That(V2WorldLabels.HoleFirstFed(), Is.True, "a new run (its labels forgotten) has it again");
            }
            finally
            {
                V2WorldLabels.ForgetFirstSights();
            }
        }

        [TestCase(true, ExpectedResult = true, TestName = "Whitebox names every enemy, on every page")]
        [TestCase(false, ExpectedResult = false, TestName = "The art keeps names to specials, elites and bosses")]
        public bool OrdinaryNamesShown_InWhiteboxOnly(bool whitebox) => V2WorldLabels.OrdinaryNamesShown(whitebox);

        [Test]
        public void EdgePoint_FarToTheRight_SitsOnTheRightSide()
        {
            var at = V2WorldLabels.EdgePoint(new Vector2(5000f, 400f), Area, out bool onSide);

            Assert.That(onSide, Is.True, "it leaves through the right side");
            Assert.That(at.x, Is.EqualTo(Area.xMax).Within(.01f));
            Assert.That(at.y, Is.EqualTo(400f).Within(.01f));
        }

        [Test]
        public void EdgePoint_MostlyUp_SitsOnTheTopOnTheLineToThePlace()
        {
            // Up and a little to the right: clamping each axis on its own put this in the top right corner with a sideways arrow.
            var at = V2WorldLabels.EdgePoint(new Vector2(900f, 2400f), Area, out bool onSide);

            Assert.That(onSide, Is.False, "it leaves through the top");
            Assert.That(at.y, Is.EqualTo(Area.yMax).Within(.01f));
            Assert.That(at.x, Is.EqualTo(630f).Within(.01f), "on the line from the middle to the place");
        }

        [Test]
        public void EdgePoint_DownAndToTheRight_LeavesThroughTheBottom()
        {
            var at = V2WorldLabels.EdgePoint(new Vector2(900f, -2000f), Area, out bool onSide);

            Assert.That(onSide, Is.False, "it leaves through the bottom");
            Assert.That(at.y, Is.EqualTo(Area.yMin).Within(.01f));
            Assert.That(at.x, Is.GreaterThan(Area.center.x), "on the right half, toward the place");
        }

        [Test]
        public void EdgePoint_InsideTheArea_IsThePlaceItself()
        {
            var at = V2WorldLabels.EdgePoint(new Vector2(700f, 450f), Area, out _);

            Assert.That(at.x, Is.EqualTo(700f).Within(.01f));
            Assert.That(at.y, Is.EqualTo(450f).Within(.01f));
        }

        [Test]
        public void FacingScreenPoint_OrthographicView_AFarPlaceOnTheCamerasSide_StaysBelowTheMiddle()
        {
            var cam = MakeOrthographicView();
            // 84 m from the player toward the camera along the ground: the bottom of the screen, behind the camera's plane.
            Vector3 place = Query.Flat(-cam.transform.forward).normalized * 84f;
            Assume.That(cam.WorldToScreenPoint(place).z, Is.LessThan(0f), "the place lies behind the camera's plane");

            Vector2 point = V2WorldLabels.FacingScreenPoint(cam, place);

            Assert.That(point.y, Is.LessThan(cam.pixelHeight * .5f), "below the middle of the screen, where it lies on the ground");
        }

        [Test]
        public void FacingScreenPoint_OrthographicView_AFarPlaceAhead_StaysAboveTheMiddle()
        {
            var cam = MakeOrthographicView();
            Vector3 place = Query.Flat(cam.transform.forward).normalized * 84f;

            Vector2 point = V2WorldLabels.FacingScreenPoint(cam, place);

            Assert.That(point.y, Is.GreaterThan(cam.pixelHeight * .5f), "above the middle of the screen");
        }

        /// <summary>The game's 2.5D view (CameraRig: pitch 50, yaw 45, orthographic, size 10), 30 m from a player at the origin.</summary>
        Camera MakeOrthographicView()
        {
            cameraObject = new GameObject("WorldLabelRulesTest camera");
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.transform.rotation = Quaternion.Euler(50f, 45f, 0f);
            cam.transform.position = Vector3.up - cam.transform.forward * 30f;
            return cam;
        }
    }
}
