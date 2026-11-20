using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The guided base tour's steps in their order (V2CampaignUi.BaseTour.cs). None: the tour has not begun; Done: it is over.</summary>
    public enum BaseTourStep { None, Control, ArmamentZone, BuildArmament, OpenStudy, CloseStudy, EnergyZone, BuildEnergy, ShipZone, Leave, Done }

    /// <summary>The side of the highlighted part that the base tour's text plate stands on.</summary>
    public enum TourPlateSide { Right, Left, Above, Below }

    /// <summary>What the base tour reads to tell whether a step is left out or done: the base page's open tab, the levels of the two rooms
    /// the tour builds and whether each can be built now, whether the study window and the base page are open, and whether Central
    /// Control's zone was clicked since the step began.</summary>
    public struct BaseTourState
    {
        public CampaignRoom tab;
        public int armamentLevel, energyLevel;
        public bool armamentBuildable, energyBuildable, studyOpen, baseOpen, controlClicked;
    }

    /// <summary>
    /// The rules of the guided base tour on the arrival at the end of Day 0, apart from the scene so they can be tested: the steps in their
    /// order, what each says, when each is done, which are left out, and where the text plate stands beside the highlighted part. The tour
    /// shows Central Control and the Armament Bay, builds the bay with the alloy Day 0 gives (exactly its price), opens and closes its
    /// study window, shows the Energy Lab and builds it with Day 0's energy, shows the Ship Launch Bay and ends at the way out. A room that
    /// cannot be built now (or is built already) is left out, and so are the study window's two steps while the Armament Bay is not
    /// built, so a build that is left out takes its study with it. A step already done when it would begin (its tab shown already) is not
    /// asked for.
    /// </summary>
    public static class BaseTour
    {
        /// <summary>The step after this one in the tour's order (Done stays Done).</summary>
        public static BaseTourStep After(BaseTourStep step) => step >= BaseTourStep.Done ? BaseTourStep.Done : step + 1;

        /// <summary>What a step says, in Chinese and in English (nothing for None and Done).</summary>
        public static (string chinese, string english) Words(BaseTourStep step) => step switch
        {
            BaseTourStep.Control => ("这里是总控区，管理基地本身：加固外壁、建造护盾、修复被打坏的功能区。白天怪物会冲击基地，外壁越坚固，基地越不容易被攻破。",
                "This is Central Control, which runs the base itself: it strengthens the outer wall, builds the shield and repairs damaged rooms. Monsters attack the base by day, and the stronger the wall, the longer the base holds."),
            BaseTourStep.ArmamentZone => ("武装区负责你的武器和护甲。它现在还没有建成，建成后才会开放具体的功能。点这里看看。",
                "The Armament Bay looks after your weapons and armor. It hasn't been built yet, and its functions open once it is. Click here to take a look."),
            BaseTourStep.BuildArmament => ("你手上的合金正好够建造武装区。点这里，把它建起来。",
                "You have exactly enough alloy to build the Armament Bay. Click here to build it."),
            BaseTourStep.OpenStudy => ("武装区建好了。它的强化都在“研习”里购买，点这里打开看看。",
                "The Armament Bay is built. Its upgrades are bought under Study; click here to open it."),
            BaseTourStep.CloseStudy => ("提升武器等级，会让所有异常武器的伤害一起提高；强化护甲，会提高你的生命和能量上限。武装区升到更高等级后，还会开放新的项目。合金已经用完了，先关掉，攒够了再来。",
                "Raising the weapon level makes every anomaly weapon hit harder, and strengthening your armor raises your maximum health and energy. Higher levels of the Armament Bay open new options. Your alloy is spent for now, so close this and come back when you have saved more."),
            BaseTourStep.EnergyZone => ("能量研究区用异星能量做研究，第一项研究能强化身体的各项属性。研究区的等级越高，能研究的东西就越多。",
                "The Energy Lab turns alien energy into research; its first line strengthens your body's attributes. The higher its level, the more there is to research."),
            BaseTourStep.BuildEnergy => ("你手上的能量正好够建造能量研究区。点这里建造。",
                "You have exactly enough energy to build the Energy Lab. Click here to build it."),
            BaseTourStep.ShipZone => ("飞船发射区负责修理飞船、为返航做准备，升级它是回家的必经之路。夜里照明用的矿灯，也在这里制造。",
                "The Ship Launch Bay repairs the ship and prepares it for the journey home, so upgrading it is the only way back. The mining lamps you need at night are made here too."),
            BaseTourStep.Leave => ("基地就介绍到这里。准备好以后就出发，夜里记得用矿灯照明。",
                "That's the whole base. Head out when you're ready, and remember to light your way with mining lamps at night."),
            _ => ("", "")
        };

        /// <summary>What a step says in the language shown.</summary>
        public static string Text(BaseTourStep step)
        {
            var (chinese, english) = Words(step);
            return ArenaText.Pair(chinese, english);
        }

        /// <summary>The plate's small second line: how to go on.</summary>
        public static string Hint => ArenaText.Pair("点击高亮处继续", "Click the highlighted spot to continue");

        /// <summary>A step left out: a room the tour would build that is built already or cannot be built now (Central Control broken, the
        /// room broken, or the stock short of its price); opening the study window while the Armament Bay is not built; closing it while
        /// it is not open.</summary>
        public static bool Skipped(BaseTourStep step, in BaseTourState s) => step switch
        {
            BaseTourStep.BuildArmament => s.armamentLevel >= 1 || !s.armamentBuildable,
            BaseTourStep.OpenStudy => s.armamentLevel < 1,
            BaseTourStep.CloseStudy => !s.studyOpen,
            BaseTourStep.BuildEnergy => s.energyLevel >= 1 || !s.energyBuildable,
            _ => false
        };

        /// <summary>A step is done: Central Control's zone clicked (its tab still shown), a zone's tab shown, a room built, the study window
        /// opened, then closed again, and the base page left.</summary>
        public static bool Passed(BaseTourStep step, in BaseTourState s) => step switch
        {
            BaseTourStep.Control => s.controlClicked && s.tab == CampaignRoom.Control,
            BaseTourStep.ArmamentZone => s.tab == CampaignRoom.Armament,
            BaseTourStep.BuildArmament => s.armamentLevel >= 1,
            BaseTourStep.OpenStudy => s.studyOpen,
            BaseTourStep.CloseStudy => !s.studyOpen,
            BaseTourStep.EnergyZone => s.tab == CampaignRoom.Energy,
            BaseTourStep.BuildEnergy => s.energyLevel >= 1,
            BaseTourStep.ShipZone => s.tab == CampaignRoom.Ship,
            BaseTourStep.Leave => !s.baseOpen,
            _ => true
        };

        /// <summary>The first step from `from` on that still has something to show, neither left out nor done already; Done when none is
        /// left.</summary>
        public static BaseTourStep FirstToRun(BaseTourStep from, in BaseTourState s)
        {
            for (var step = from < BaseTourStep.Control ? BaseTourStep.Control : from; step < BaseTourStep.Done; step++)
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
}
