using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The guided base tour on Day 0's arrival, the rules that need no scene: its steps in order, what each says, when each is
    /// done, which are left out, and where its text plate stands beside the highlighted part; and the same for the guide to a first
    /// purchase on a later visit (BuyGuide).</summary>
    [TestFixture]
    public class BaseTourTest
    {
        /// <summary>The page as the arrival opens it: Central Control's tab, both rooms unbuilt and paid for exactly, the study window shut.</summary>
        static BaseTourState Arrival() => new()
        {
            tab = CampaignRoom.Control,
            armamentBuildable = true,
            energyBuildable = true,
            baseOpen = true
        };

        [Test]
        public void After_WalksTheStepsInTheirOrder()
        {
            var expected = new[]
            {
                BaseTourStep.Control, BaseTourStep.ArmamentZone, BaseTourStep.BuildArmament, BaseTourStep.OpenStudy, BaseTourStep.CloseStudy,
                BaseTourStep.EnergyZone, BaseTourStep.BuildEnergy, BaseTourStep.ShipZone, BaseTourStep.Leave, BaseTourStep.Done
            };
            var step = BaseTourStep.None;
            foreach (var next in expected)
            {
                step = BaseTour.After(step);
                Assert.That(step, Is.EqualTo(next));
            }
            Assert.That(BaseTour.After(BaseTourStep.Done), Is.EqualTo(BaseTourStep.Done), "the end stays the end");
        }

        [Test]
        public void Words_EveryStepShownSaysSomethingInBothLanguages_AndNothingOutsideTheTour()
        {
            for (var step = BaseTourStep.Control; step < BaseTourStep.Done; step++)
            {
                var (chinese, english) = BaseTour.Words(step);
                Assert.That(chinese, Is.Not.Empty, step.ToString());
                Assert.That(english, Is.Not.Empty, step.ToString());
            }
            Assert.That(BaseTour.Words(BaseTourStep.None).chinese, Is.Empty);
            Assert.That(BaseTour.Words(BaseTourStep.Done).english, Is.Empty);
        }

        [Test]
        public void FirstToRun_OnArrival_IsCentralControl()
        {
            Assert.That(BaseTour.FirstToRun(BaseTourStep.None, Arrival()), Is.EqualTo(BaseTourStep.Control));
        }

        [Test]
        public void Passed_CentralControl_TakesTheClickOnItsZone_WithItsTabStillShown()
        {
            var s = Arrival();
            Assert.That(BaseTour.Passed(BaseTourStep.Control, s), Is.False, "its tab shows from the start: only the click passes it");
            s.controlClicked = true;
            Assert.That(BaseTour.Passed(BaseTourStep.Control, s), Is.True);
            s.tab = CampaignRoom.Armament;
            Assert.That(BaseTour.Passed(BaseTourStep.Control, s), Is.False);
        }

        [Test]
        public void Passed_EachLaterStep_OnceWhatItAsksForHasHappened()
        {
            var s = Arrival();
            s.tab = CampaignRoom.Armament;
            Assert.That(BaseTour.Passed(BaseTourStep.ArmamentZone, s), Is.True, "the Armament Bay's tab");
            Assert.That(BaseTour.Passed(BaseTourStep.BuildArmament, s), Is.False);
            s.armamentLevel = 1;
            Assert.That(BaseTour.Passed(BaseTourStep.BuildArmament, s), Is.True, "the bay built");
            Assert.That(BaseTour.Passed(BaseTourStep.OpenStudy, s), Is.False);
            s.studyOpen = true;
            Assert.That(BaseTour.Passed(BaseTourStep.OpenStudy, s), Is.True, "the study window open");
            Assert.That(BaseTour.Passed(BaseTourStep.CloseStudy, s), Is.False);
            s.studyOpen = false;
            Assert.That(BaseTour.Passed(BaseTourStep.CloseStudy, s), Is.True, "the study window shut again");
            s.tab = CampaignRoom.Energy;
            Assert.That(BaseTour.Passed(BaseTourStep.EnergyZone, s), Is.True, "the Energy Lab's tab");
            Assert.That(BaseTour.Passed(BaseTourStep.BuildEnergy, s), Is.False);
            s.energyLevel = 1;
            Assert.That(BaseTour.Passed(BaseTourStep.BuildEnergy, s), Is.True, "the lab built");
            Assert.That(BaseTour.Passed(BaseTourStep.ShipZone, s), Is.False);
            s.tab = CampaignRoom.Ship;
            Assert.That(BaseTour.Passed(BaseTourStep.ShipZone, s), Is.True, "the launch bay's tab");
            Assert.That(BaseTour.Passed(BaseTourStep.Leave, s), Is.False);
            s.baseOpen = false;
            Assert.That(BaseTour.Passed(BaseTourStep.Leave, s), Is.True, "the base page left");
        }

        [Test]
        public void FirstToRun_AnArmamentBayThatCannotBeBuilt_LeavesOutItsBuildAndItsStudy()
        {
            // The alloy went on the hull first: the Armament Bay's build and both of its study steps are left out.
            var s = Arrival();
            s.tab = CampaignRoom.Armament;
            s.armamentBuildable = false;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.BuildArmament, s), Is.EqualTo(BaseTourStep.EnergyZone));
        }

        [Test]
        public void FirstToRun_AnArmamentBayBuiltAlready_IsNotBuiltAgain_ButItsStudyIsShown()
        {
            var s = Arrival();
            s.tab = CampaignRoom.Armament;
            s.armamentLevel = 1;
            s.armamentBuildable = false;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.BuildArmament, s), Is.EqualTo(BaseTourStep.OpenStudy));
        }

        [Test]
        public void FirstToRun_TheStudyWindowShut_LeavesOutItsClosing()
        {
            var s = Arrival();
            s.tab = CampaignRoom.Armament;
            s.armamentLevel = 1;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.CloseStudy, s), Is.EqualTo(BaseTourStep.EnergyZone));
        }

        [Test]
        public void FirstToRun_AnEnergyLabThatCannotBeBuilt_GoesOnToTheLaunchBay()
        {
            var s = Arrival();
            s.tab = CampaignRoom.Energy;
            s.energyBuildable = false;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.BuildEnergy, s), Is.EqualTo(BaseTourStep.ShipZone));
        }

        [Test]
        public void FirstToRun_AZoneWhoseTabShowsAlready_IsNotAskedFor()
        {
            var s = Arrival();
            s.tab = CampaignRoom.Ship;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.ShipZone, s), Is.EqualTo(BaseTourStep.Leave));
        }

        [Test]
        public void FirstToRun_TheBasePageClosed_EndsTheTour()
        {
            var s = Arrival();
            s.baseOpen = false;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.Leave, s), Is.EqualTo(BaseTourStep.Done));
            Assert.That(BaseTour.FirstToRun(BaseTourStep.Done, s), Is.EqualTo(BaseTourStep.Done));
        }

        // The plate on the 1600 x 900 reference screen: 18 from the ring, 16 inside the screen's edges, 300 to 560 wide.
        static readonly Vector2 Area = new(1600f, 900f);
        const float Gap = 18f, Margin = 16f, MinWidth = 300f, MaxWidth = 560f;

        [Test]
        public void Plate_BesideAZoneAtTheLeft_StandsRightOfIt_LevelWithIt()
        {
            var hole = Rect.MinMaxRect(14f, 640f, 395f, 760f);
            var side = BaseTour.PlateSide(Area, hole, MinWidth, Gap, Margin);
            Assert.That(side, Is.EqualTo(TourPlateSide.Right));
            Assert.That(BaseTour.PlateWidth(side, Area, hole, MinWidth, MaxWidth, Gap, Margin), Is.EqualTo(MaxWidth));
            var place = BaseTour.PlatePlace(side, Area, hole, new Vector2(560f, 160f), Gap, Margin);
            Assert.That(place.xMin, Is.EqualTo(395f + Gap).Within(1e-3f));
            Assert.That(place.center.y, Is.EqualTo(hole.center.y).Within(1e-3f));
        }

        [Test]
        public void Plate_BesideARowAtTheRight_StandsLeftOfIt()
        {
            var hole = Rect.MinMaxRect(1080f, 600f, 1590f, 720f);
            var side = BaseTour.PlateSide(Area, hole, MinWidth, Gap, Margin);
            Assert.That(side, Is.EqualTo(TourPlateSide.Left));
            var place = BaseTour.PlatePlace(side, Area, hole, new Vector2(560f, 160f), Gap, Margin);
            Assert.That(place.xMax, Is.EqualTo(1080f - Gap).Within(1e-3f));
        }

        [Test]
        public void Plate_BesideAButtonInACorner_IsKeptInsideTheScreen()
        {
            // The study window's close button at the top right: the plate stands left of it and is pushed down inside the top edge.
            var hole = Rect.MinMaxRect(1410f, 790f, 1595f, 895f);
            var side = BaseTour.PlateSide(Area, hole, MinWidth, Gap, Margin);
            var place = BaseTour.PlatePlace(side, Area, hole, new Vector2(560f, 220f), Gap, Margin);
            Assert.That(side, Is.EqualTo(TourPlateSide.Left));
            Assert.That(place.yMax, Is.EqualTo(900f - Margin).Within(1e-3f));
            Assert.That(place.xMin, Is.GreaterThanOrEqualTo(Margin));
        }

        [Test]
        public void Plate_WithNoRoomOnEitherSide_GoesAboveOrBelowWhereThereIsMoreRoom()
        {
            var hole = Rect.MinMaxRect(100f, 200f, 1500f, 320f);
            var side = BaseTour.PlateSide(Area, hole, MinWidth, Gap, Margin);
            Assert.That(side, Is.EqualTo(TourPlateSide.Above));
            var place = BaseTour.PlatePlace(side, Area, hole, new Vector2(560f, 160f), Gap, Margin);
            Assert.That(place.yMin, Is.EqualTo(320f + Gap).Within(1e-3f));
            Assert.That(place.center.x, Is.EqualTo(hole.center.x).Within(1e-3f));
            Assert.That(BaseTour.PlateSide(Area, Rect.MinMaxRect(100f, 600f, 1500f, 700f), MinWidth, Gap, Margin), Is.EqualTo(TourPlateSide.Below));
        }

        [Test]
        public void PlateWidth_NarrowsToTheRoomLeft_ButNeverUnderTheLeast()
        {
            // Right of the box 1600 - 16 - 1150 - 18 = 416 is left, more than the 366 on its left.
            var hole = Rect.MinMaxRect(400f, 400f, 1150f, 500f);
            Assert.That(BaseTour.PlateSide(Area, hole, MinWidth, Gap, Margin), Is.EqualTo(TourPlateSide.Right));
            Assert.That(BaseTour.PlateWidth(TourPlateSide.Right, Area, hole, MinWidth, MaxWidth, Gap, Margin), Is.EqualTo(416f).Within(1e-3f));
            Assert.That(BaseTour.PlateWidth(TourPlateSide.Left, Area, Rect.MinMaxRect(250f, 0f, 260f, 10f), MinWidth, MaxWidth, Gap, Margin), Is.EqualTo(MinWidth));
        }

        // ───── The guide to a first purchase on a later visit ─────

        /// <summary>A visit's page as it opens: Central Control's tab, the study window shut, nothing bought yet.</summary>
        static BuyGuideState Visit(BuyGuideAim aim, CampaignRoom room) => new()
        {
            aim = aim,
            room = room,
            tab = CampaignRoom.Control,
            baseOpen = true
        };

        [TestCase(0, 0, 3, true, true, BuyGuideAim.None, TestName = "BuyGuideAim_Day0_IsNothing")]
        [TestCase(1, 0, 3, true, true, BuyGuideAim.Lamp, TestName = "BuyGuideAim_FewerLampsThanTheDaysAndOneBuyable_IsALamp")]
        [TestCase(1, 3, 3, true, true, BuyGuideAim.Study, TestName = "BuyGuideAim_EnoughLamps_IsAStudyItem")]
        [TestCase(1, 0, 3, false, true, BuyGuideAim.Study, TestName = "BuyGuideAim_NoLampBuyable_IsAStudyItem")]
        [TestCase(2, 0, 3, false, false, BuyGuideAim.None, TestName = "BuyGuideAim_NothingBuyable_IsNothing")]
        public void BuyGuideAim_FollowsTheLampsAndTheStock(int day, int lampStock, int lampsWanted, bool lampBuyable, bool studyBuyable, BuyGuideAim expected)
        {
            Assert.That(BuyGuide.Aim(day, lampStock, lampsWanted, lampBuyable, studyBuyable), Is.EqualTo(expected));
        }

        [Test]
        public void BuyGuide_ToALamp_ShowsTheLaunchBayThenItsLampRow_AndEndsOnThePurchase()
        {
            var s = Visit(BuyGuideAim.Lamp, CampaignRoom.Ship);
            Assert.That(BuyGuide.FirstToRun(BuyGuideStep.None, s), Is.EqualTo(BuyGuideStep.Zone));
            s.tab = CampaignRoom.Ship;
            Assert.That(BuyGuide.FirstToRun(BuyGuide.After(BuyGuideStep.Zone), s), Is.EqualTo(BuyGuideStep.Lamp));
            s.bought = true;
            Assert.That(BuyGuide.Passed(BuyGuideStep.Lamp, s), Is.True, "the lamp made");
            Assert.That(BuyGuide.FirstToRun(BuyGuide.After(BuyGuideStep.Lamp), s), Is.EqualTo(BuyGuideStep.Done), "the study's steps are left out");
        }

        [Test]
        public void BuyGuide_ToAStudyItem_ShowsTheRoomThenItsStudyButtonThenTheBuyButton()
        {
            var s = Visit(BuyGuideAim.Study, CampaignRoom.Energy);
            Assert.That(BuyGuide.FirstToRun(BuyGuideStep.None, s), Is.EqualTo(BuyGuideStep.Zone));
            s.tab = CampaignRoom.Energy;
            Assert.That(BuyGuide.FirstToRun(BuyGuide.After(BuyGuideStep.Zone), s), Is.EqualTo(BuyGuideStep.OpenStudy), "the lamp row is left out");
            s.studyOpen = true;
            Assert.That(BuyGuide.FirstToRun(BuyGuide.After(BuyGuideStep.OpenStudy), s), Is.EqualTo(BuyGuideStep.Buy));
            s.bought = true;
            Assert.That(BuyGuide.FirstToRun(BuyGuide.After(BuyGuideStep.Buy), s), Is.EqualTo(BuyGuideStep.Done));
        }

        [Test]
        public void BuyGuide_TheRoomsTabShownAlready_IsNotAskedFor()
        {
            var s = Visit(BuyGuideAim.Study, CampaignRoom.Armament);
            s.tab = CampaignRoom.Armament;
            Assert.That(BuyGuide.FirstToRun(BuyGuideStep.None, s), Is.EqualTo(BuyGuideStep.OpenStudy));
        }

        [Test]
        public void BuyGuide_TheBasePageClosed_EndsIt()
        {
            var s = Visit(BuyGuideAim.Lamp, CampaignRoom.Ship);
            s.baseOpen = false;
            Assert.That(BuyGuide.FirstToRun(BuyGuideStep.Lamp, s), Is.EqualTo(BuyGuideStep.Done));
        }

        [Test]
        public void BuyGuideWords_EveryStepItCanShowSaysSomethingInBothLanguages_AndNothingOutsideIt()
        {
            var shown = new[]
            {
                (BuyGuideStep.Zone, BuyGuideAim.Lamp, CampaignRoom.Ship), (BuyGuideStep.Lamp, BuyGuideAim.Lamp, CampaignRoom.Ship),
                (BuyGuideStep.Zone, BuyGuideAim.Study, CampaignRoom.Armament), (BuyGuideStep.Zone, BuyGuideAim.Study, CampaignRoom.Energy),
                (BuyGuideStep.OpenStudy, BuyGuideAim.Study, CampaignRoom.Armament), (BuyGuideStep.OpenStudy, BuyGuideAim.Study, CampaignRoom.Energy),
                (BuyGuideStep.Buy, BuyGuideAim.Study, CampaignRoom.Armament)
            };
            foreach (var (step, aim, room) in shown)
            {
                var (chinese, english) = BuyGuide.Words(step, aim, room);
                Assert.That(chinese, Is.Not.Empty, $"{step} {aim} {room}");
                Assert.That(english, Is.Not.Empty, $"{step} {aim} {room}");
            }
            Assert.That(BuyGuide.Words(BuyGuideStep.None, BuyGuideAim.Lamp, CampaignRoom.Ship).chinese, Is.Empty);
            Assert.That(BuyGuide.Words(BuyGuideStep.Done, BuyGuideAim.Study, CampaignRoom.Armament).english, Is.Empty);
        }

        [Test]
        public void BuyGuideWords_TheEnergyLabsItemsAreBoughtWithAlienEnergy_TheArmamentBaysWithAlloy()
        {
            Assert.That(BuyGuide.Words(BuyGuideStep.OpenStudy, BuyGuideAim.Study, CampaignRoom.Energy).chinese, Does.Contain("异星能量"));
            Assert.That(BuyGuide.Words(BuyGuideStep.OpenStudy, BuyGuideAim.Study, CampaignRoom.Energy).english, Does.Contain("alien energy"));
            Assert.That(BuyGuide.Words(BuyGuideStep.OpenStudy, BuyGuideAim.Study, CampaignRoom.Armament).chinese, Does.Contain("合金"));
            Assert.That(BuyGuide.Words(BuyGuideStep.OpenStudy, BuyGuideAim.Study, CampaignRoom.Armament).english, Does.Contain("alloy"));
        }
    }
}
