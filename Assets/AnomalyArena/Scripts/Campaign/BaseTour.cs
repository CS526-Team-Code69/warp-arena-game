using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The guided base tour's steps in their order (V2CampaignUi.BaseTour.cs): learn a skill, raise the weapon Mk, open the rocket
    /// page, head out. None: the tour has not begun; Done: it is over.</summary>
    public enum BaseTourStep { None, Skill, WeaponMk, Rocket, Leave, Done }

    /// <summary>The side of the highlighted part that the base tour's text plate stands on.</summary>
    public enum TourPlateSide { Right, Left, Above, Below }

    /// <summary>The guide of Day 1's first base visit (V2CampaignUi.BaseTour.cs): its one step, the rocket page's button. None: not begun;
    /// Done: over.</summary>
    public enum BuyGuideStep { None, Rocket, Done }

    /// <summary>What Day 1's guide leads to: the rocket page, where the mining lamps are made (Lamp), or nothing.</summary>
    public enum BuyGuideAim { None, Lamp }

    /// <summary>What Day 1's guide reads to tell whether its step is done: whether the base page is open and shows the rocket page.</summary>
    public struct BuyGuideState
    {
        public bool baseOpen, rocketShown;
    }

    /// <summary>What the base tour reads to tell whether a step is left out or done: whether a skill can be learned now and one was learned
    /// since the tour began, whether the weapon Mk can be raised now and was raised since, whether the rocket page shows, and whether the
    /// base page is open.</summary>
    public struct BaseTourState
    {
        public bool skillBuyable, skillLearned, mkBuyable, mkRaised, rocketShown, baseOpen;
    }

    /// <summary>
    /// The rules of the guided base tour on the arrival at the end of Day 0, apart from the scene so they can be tested: the steps in their
    /// order, what each says, when each is done, which are left out, and where the text plate stands beside the highlighted part. On the
    /// workshop the tour has a skill learned (any of the four first skills, 5 alien energy) and the weapon Mk raised (5 alloy), both paid by
    /// what Day 0 gives; then the rocket page is opened from its button, and the tour ends at the way out. A step the stock cannot pay for
    /// now is left out, and so is a step done already when it would begin.
    /// </summary>
    public static class BaseTour
    {
        /// <summary>The step after this one in the tour's order (Done stays Done).</summary>
        public static BaseTourStep After(BaseTourStep step) => step >= BaseTourStep.Done ? BaseTourStep.Done : step + 1;

        /// <summary>What a step says, in Chinese and in English (nothing for None and Done).</summary>
        public static (string chinese, string english) Words(BaseTourStep step) => step switch
        {
            BaseTourStep.Skill => ("先学一个技能。明天上午的第一仗就能用上。", "Learn a skill first. You can use it in tomorrow morning's first fight."),
            BaseTourStep.WeaponMk => ("合金用来升级武器型号，所有异常武器一起变强。", "Alloy upgrades the weapon Mk, and all your anomaly weapons grow stronger together."),
            BaseTourStep.Rocket => ("回家要做的事都在这里：修好三个部件，加满燃料，就能点火。", "Everything you need to get home is here: repair the three parts, fill the fuel, and you can ignite."),
            BaseTourStep.Leave => ("准备好了就出发。夜里记得放矿灯。", "Head out when you're ready. Remember to set down your mining lamps at night."),
            _ => ("", "")
        };

        /// <summary>What a step says in the language shown.</summary>
        public static string Text(BaseTourStep step)
        {
            var (chinese, english) = Words(step);
            return ArenaText.Pair(chinese, english);
        }

        /// <summary>A step left out: a skill that cannot be learned now, the weapon Mk that cannot be raised now (the stock does not pay
        /// for it).</summary>
        public static bool Skipped(BaseTourStep step, in BaseTourState s) => step switch
        {
            BaseTourStep.Skill => !s.skillBuyable,
            BaseTourStep.WeaponMk => !s.mkBuyable,
            _ => false
        };

        /// <summary>A step is done: a skill learned, the weapon Mk raised, the rocket page shown, the base page left.</summary>
        public static bool Passed(BaseTourStep step, in BaseTourState s) => step switch
        {
            BaseTourStep.Skill => s.skillLearned,
            BaseTourStep.WeaponMk => s.mkRaised,
            BaseTourStep.Rocket => s.rocketShown,
            BaseTourStep.Leave => !s.baseOpen,
            _ => true
        };

        /// <summary>The first step from `from` on that still has something to show, neither left out nor done already; Done when none is
        /// left.</summary>
        public static BaseTourStep FirstToRun(BaseTourStep from, in BaseTourState s)
        {
            for (var step = from < BaseTourStep.Skill ? BaseTourStep.Skill : from; step < BaseTourStep.Done; step++)
                if (!Skipped(step, s) && !Passed(step, s)) return step;
            return BaseTourStep.Done;
        }

        // ───── The text plate (canvas units from the screen's bottom-left corner) ─────

        /// <summary>The side the plate stands on beside the highlighted part (hole: the part with its ring): right or left, whichever
        /// has more room, while that room holds the plate at minWidth; otherwise above or below, whichever has more room.</summary>
        public static TourPlateSide PlateSide(Vector2 area, Rect hole, float minWidth, float gap, float margin)
        {
            float right = area.x - margin - hole.xMax - gap, left = hole.xMin - gap - margin;
            if (Mathf.Max(right, left) >= minWidth) return right >= left ? TourPlateSide.Right : TourPlateSide.Left;
            float above = area.y - margin - hole.yMax - gap, below = hole.yMin - gap - margin;
            return above >= below ? TourPlateSide.Above : TourPlateSide.Below;
        }

        /// <summary>How wide the plate is on that side: maxWidth, or the room the side leaves when that is less, never under minWidth and
        /// never wider than the screen inside its margins.</summary>
        public static float PlateWidth(TourPlateSide side, Vector2 area, Rect hole, float minWidth, float maxWidth, float gap, float margin)
        {
            float screen = Mathf.Max(0f, area.x - 2f * margin);
            float room = side switch
            {
                TourPlateSide.Right => area.x - margin - hole.xMax - gap,
                TourPlateSide.Left => hole.xMin - gap - margin,
                _ => screen
            };
            return Mathf.Clamp(room, Mathf.Min(minWidth, screen), Mathf.Min(maxWidth, screen));
        }

        /// <summary>Where a plate of this size stands: gap beside the hole on that side, level with the hole's middle (centred on it when
        /// above or below), and kept margin inside the screen.</summary>
        public static Rect PlatePlace(TourPlateSide side, Vector2 area, Rect hole, Vector2 size, float gap, float margin)
        {
            float x, y;
            switch (side)
            {
                case TourPlateSide.Right:
                    x = hole.xMax + gap;
                    y = hole.center.y - size.y * .5f;
                    break;
                case TourPlateSide.Left:
                    x = hole.xMin - gap - size.x;
                    y = hole.center.y - size.y * .5f;
                    break;
                case TourPlateSide.Above:
                    x = hole.center.x - size.x * .5f;
                    y = hole.yMax + gap;
                    break;
                default:
                    x = hole.center.x - size.x * .5f;
                    y = hole.yMin - gap - size.y;
                    break;
            }
            x = Mathf.Clamp(x, margin, Mathf.Max(margin, area.x - margin - size.x));
            y = Mathf.Clamp(y, margin, Mathf.Max(margin, area.y - margin - size.y));
            return new Rect(x, y, size.x, size.y);
        }
    }

    /// <summary>
    /// The rules of the guide of Day 1's first base visit, apart from the scene so they can be tested: one step, the workshop's button to
    /// the rocket page, where the mining lamps the night needs are made. It uses the base tour's plate and highlight
    /// (V2CampaignUi.BaseTour.cs). A visit that shows the rocket page already needs no guide. Esc skips it.
    /// </summary>
    public static class BuyGuide
    {
        /// <summary>What the guide leads to on a visit: the rocket page on Day 1 (V2Campaign.TickBuyGuide starts it on the day's first
        /// visit), nothing on any other day. The other values are what the caller has at hand; the guide no longer depends on them.</summary>
        public static BuyGuideAim Aim(int day, int lampStock, int lampsWanted, bool lampBuyable, bool studyBuyable) =>
            day == 1 ? BuyGuideAim.Lamp : BuyGuideAim.None;

        /// <summary>The step after this one in the guide's order (Done stays Done).</summary>
        public static BuyGuideStep After(BuyGuideStep step) => step >= BuyGuideStep.Done ? BuyGuideStep.Done : step + 1;

        /// <summary>The step is done: the rocket page shows.</summary>
        public static bool Passed(BuyGuideStep step, in BuyGuideState s) => step switch
        {
            BuyGuideStep.Rocket => s.rocketShown,
            _ => true
        };

        /// <summary>The first step from `from` on that still has something to show; Done when none is left, or when the base page is closed.</summary>
        public static BuyGuideStep FirstToRun(BuyGuideStep from, in BuyGuideState s)
        {
            if (!s.baseOpen) return BuyGuideStep.Done;
            for (var step = from < BuyGuideStep.Rocket ? BuyGuideStep.Rocket : from; step < BuyGuideStep.Done; step++)
                if (!Passed(step, s)) return step;
            return BuyGuideStep.Done;
        }

        /// <summary>What a step says, in Chinese and in English; nothing for None and Done.</summary>
        public static (string chinese, string english) Words(BuyGuideStep step) => step switch
        {
            BuyGuideStep.Rocket => ("夜里要用的矿灯在火箭这边造。", "The mining lamps you need at night are made on the rocket page."),
            _ => ("", "")
        };

        /// <summary>What a step says in the language shown.</summary>
        public static string Text(BuyGuideStep step)
        {
            var (chinese, english) = Words(step);
            return ArenaText.Pair(chinese, english);
        }
    }
}
