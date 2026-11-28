using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The guided base tour on Day 0's arrival, the rules that need no scene: its four steps in order (a skill, the weapon Mk, the
    /// rocket page, the way out), what each says, when each is done, which are left out, and where its text plate stands beside the
    /// highlighted part; Day 1's one-step guide to the rocket page; and that what Day 0 gives pays for the tour's two purchases.</summary>
    [TestFixture]
    public class BaseTourTest
    {
        /// <summary>The workshop as the arrival opens it: a skill and the weapon Mk both paid for, nothing bought yet, the base page open.</summary>
        static BaseTourState Arrival() => new()
        {
            skillBuyable = true,
            mkBuyable = true,
            baseOpen = true
        };

        [Test]
        public void After_WalksTheFourStepsInTheirOrder()
        {
            var expected = new[] { BaseTourStep.Skill, BaseTourStep.WeaponMk, BaseTourStep.Rocket, BaseTourStep.Leave, BaseTourStep.Done };
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
            for (var step = BaseTourStep.Skill; step < BaseTourStep.Done; step++)
            {
                var (chinese, english) = BaseTour.Words(step);
                Assert.That(chinese, Is.Not.Empty, step.ToString());
                Assert.That(english, Is.Not.Empty, step.ToString());
            }
            Assert.That(BaseTour.Words(BaseTourStep.None).chinese, Is.Empty);
            Assert.That(BaseTour.Words(BaseTourStep.Done).english, Is.Empty);
        }

        [TestCase(BaseTourStep.Skill, "先学一个技能。明天上午的第一仗就能用上。")]
        [TestCase(BaseTourStep.WeaponMk, "合金用来升级武器型号，所有异常武器一起变强。")]
        [TestCase(BaseTourStep.Rocket, "修好三个部件并加满燃料后才能点火。点火会引来最终决战，先做好准备。")]
        [TestCase(BaseTourStep.Leave, "准备好了就出发。夜里记得放矿灯。")]
        public void Words_EachStep_IsItsOneSentence(BaseTourStep step, string expected)
        {
            var (chinese, _) = BaseTour.Words(step);

            Assert.That(chinese, Is.EqualTo(expected));
        }

        [Test]
        public void FirstToRun_OnArrival_IsTheSkill()
        {
            Assert.That(BaseTour.FirstToRun(BaseTourStep.None, Arrival()), Is.EqualTo(BaseTourStep.Skill));
        }

        [Test]
        public void Passed_EachStep_OnceWhatItAsksForHasHappened()
        {
            var s = Arrival();
            Assert.That(BaseTour.Passed(BaseTourStep.Skill, s), Is.False);
            s.skillLearned = true;
            Assert.That(BaseTour.Passed(BaseTourStep.Skill, s), Is.True, "a skill learned");
            Assert.That(BaseTour.Passed(BaseTourStep.WeaponMk, s), Is.False);
            s.mkRaised = true;
            Assert.That(BaseTour.Passed(BaseTourStep.WeaponMk, s), Is.True, "the weapon Mk raised");
            Assert.That(BaseTour.Passed(BaseTourStep.Rocket, s), Is.False);
            s.rocketShown = true;
            Assert.That(BaseTour.Passed(BaseTourStep.Rocket, s), Is.True, "the rocket page shown");
            Assert.That(BaseTour.Passed(BaseTourStep.Leave, s), Is.False);
            s.baseOpen = false;
            Assert.That(BaseTour.Passed(BaseTourStep.Leave, s), Is.True, "the base left");
        }

        [Test]
        public void FirstToRun_ASkillThatCannotBeLearned_GoesOnToTheWeaponMk()
        {
            var s = Arrival();
            s.skillBuyable = false;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.Skill, s), Is.EqualTo(BaseTourStep.WeaponMk));
        }

        [Test]
        public void FirstToRun_AWeaponMkThatCannotBeRaised_GoesOnToTheRocketPage()
        {
            var s = Arrival();
            s.skillLearned = true;
            s.mkBuyable = false;
            Assert.That(BaseTour.FirstToRun(BaseTour.After(BaseTourStep.Skill), s), Is.EqualTo(BaseTourStep.Rocket));
        }

        [Test]
        public void FirstToRun_TheRocketPageShownAlready_GoesOnToTheWayOut()
        {
            var s = Arrival();
            s.rocketShown = true;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.Rocket, s), Is.EqualTo(BaseTourStep.Leave));
        }

        [Test]
        public void FirstToRun_TheBasePageClosed_EndsTheTour()
        {
            var s = Arrival();
            s.baseOpen = false;
            Assert.That(BaseTour.FirstToRun(BaseTourStep.Leave, s), Is.EqualTo(BaseTourStep.Done));
            Assert.That(BaseTour.FirstToRun(BaseTourStep.Done, s), Is.EqualTo(BaseTourStep.Done));
        }

        // ───── What Day 0 gives pays for the tour ─────

        [Test]
        public void DayZero_GivesWhatTheTourSkillAndWeaponMkCost()
        {
#if UNITY_EDITOR
            var rules = UnityEditor.AssetDatabase.LoadAssetAtPath<CampaignRules>("Assets/AnomalyArena/Data/Campaign/V2CampaignRules.asset");
            Assert.That(rules, Is.Not.Null, "the story's rules");
            var mk = rules.MkPrice(1);
            Assert.That(mk.HasValue, Is.True, "Mk 1 can be raised");
            Assert.That((rules.RarityEnergy(CardRarity.Common), mk.Value.alloy), Is.EqualTo((5, 5)), "a first skill costs 5 alien energy and Mk 2 five alloy");
            Assert.That(rules.dayZeroEnergy, Is.GreaterThanOrEqualTo(rules.RarityEnergy(CardRarity.Common)), "Day 0's alien energy pays for a skill");
            Assert.That(rules.dayZeroAlloy, Is.GreaterThanOrEqualTo(mk.Value.alloy), "Day 0's alloy pays for Mk 2");
            Assert.That(rules.OpenDay(V2Campaign.StudyFeature.Skills), Is.Zero, "the skills are open on Day 0");
#else
            Assert.Ignore("reads the rules asset in the editor");
#endif
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
            // The rocket page's button at the top right: the plate stands left of it and is pushed down inside the top edge.
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

        // ───── Day 1's guide to the rocket page ─────

        [TestCase(0, BuyGuideAim.None, TestName = "BuyGuideAim_Day0_IsNothing")]
        [TestCase(1, BuyGuideAim.Lamp, TestName = "BuyGuideAim_Day1_IsTheRocketPage")]
        [TestCase(2, BuyGuideAim.None, TestName = "BuyGuideAim_Day2_IsNothing")]
        public void BuyGuideAim_IsTheRocketPageOnDay1Only(int day, BuyGuideAim expected)
        {
            Assert.That(BuyGuide.Aim(day, 0, 3, true, true), Is.EqualTo(expected));
            Assert.That(BuyGuide.Aim(day, 3, 3, false, false), Is.EqualTo(expected), "whatever the lamps and the stock");
        }

        [Test]
        public void BuyGuide_OnTheWorkshop_PointsAtTheRocketButton_AndEndsOnTheRocketPage()
        {
            var s = new BuyGuideState { baseOpen = true };
            Assert.That(BuyGuide.FirstToRun(BuyGuideStep.None, s), Is.EqualTo(BuyGuideStep.Rocket));
            s.rocketShown = true;
            Assert.That(BuyGuide.Passed(BuyGuideStep.Rocket, s), Is.True, "the rocket page opened");
            Assert.That(BuyGuide.FirstToRun(BuyGuide.After(BuyGuideStep.Rocket), s), Is.EqualTo(BuyGuideStep.Done));
        }

        [Test]
        public void BuyGuide_TheRocketPageShownAlready_NeedsNoGuide()
        {
            var s = new BuyGuideState { baseOpen = true, rocketShown = true };
            Assert.That(BuyGuide.FirstToRun(BuyGuideStep.None, s), Is.EqualTo(BuyGuideStep.Done));
        }

        [Test]
        public void BuyGuide_TheBasePageClosed_EndsIt()
        {
            var s = new BuyGuideState { baseOpen = false };
            Assert.That(BuyGuide.FirstToRun(BuyGuideStep.Rocket, s), Is.EqualTo(BuyGuideStep.Done));
        }

        [Test]
        public void BuyGuideWords_ItsStepIsItsOneSentence_AndNothingOutsideIt()
        {
            var (chinese, english) = BuyGuide.Words(BuyGuideStep.Rocket);
            Assert.That(chinese, Is.EqualTo("夜里要用的矿灯在火箭这边造。"));
            Assert.That(english, Is.Not.Empty);
            Assert.That(BuyGuide.Words(BuyGuideStep.None).chinese, Is.Empty);
            Assert.That(BuyGuide.Words(BuyGuideStep.Done).english, Is.Empty);
        }
    }
}
