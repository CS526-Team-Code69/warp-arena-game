using System;

namespace AnomalyArena
{
    /// <summary>
    /// The question before the ignition. Once the ignition begins the run cannot go back: the base is not visited again, the final battle
    /// follows at once, and the only save from then on is that battle's (SaveStoryPhase), which a lost battle is retried from. So every
    /// step into the ignition that is the player's own asks first, and says so (AskIgnition): the ignition button of the base's pages
    /// on any day (AskIgnite), "Wait for dawn" when the dawn it brings starts the ignition (WaitForDawn, DawnIgnites), and the dawn
    /// after a fall in the dark on such a night (AskBeforeDawnIgnites). The answer that ignites starts the ignition. The answer that
    /// waits leaves the run where it was: the base page stays open, and after a fall the night goes on with the player at the base
    /// door. Ignite itself asks nothing: the question's answer calls it, and so do the checks and, where the evenings have a clock, the
    /// night that runs out.
    /// </summary>
    public sealed partial class V2Campaign
    {
        /// <summary>The dawn after this night starts the ignition instead of another day: the last planned day is over and the ship is
        /// ready (EndNight).</summary>
        public bool DawnIgnites => Running && rules && Day >= rules.days && CanIgnite;

        /// <summary>The ignition button of the base's pages: the question first, the ignition on the answer that ignites. Nothing while
        /// the ship cannot ignite.</summary>
        public void AskIgnite()
        {
            if (!Running || !CanIgnite) return;
            AskIgnition(Ignite, null);
        }

        /// <summary>After a fall in the dark (NightDeath), the player on their feet again: when the dawn would start the ignition, the
        /// question comes first. The answer that ignites brings that dawn; the other leaves the night going on, the player at the base
        /// door. False when the dawn brings an ordinary day: nothing was asked, and the caller brings the dawn.</summary>
        bool AskBeforeDawnIgnites()
        {
            if (!DawnIgnites || !ui) return false;
            PlacePlayer(DoorPoint);
            AskIgnition(() => EndNight(false), NightObjective);
            return true;
        }

        /// <summary>The question over the run (V2CampaignUi.Confirm): its title says there is no way back, its body what follows the
        /// ignition and what a lost final battle is retried from, and its two answers are the two actions. A campaign without its screen
        /// layer ignites unasked.</summary>
        void AskIgnition(Action ignite, Action wait)
        {
            if (!ui)
            {
                ignite?.Invoke();
                return;
            }
            ui.Confirm(IgniteAskTitle, IgniteAskBody, ignite, wait, IgniteAskYes, IgniteAskNo);
        }

        /// <summary>The question's words, in the language shown.</summary>
        public static string IgniteAskTitle => ArenaText.Pair("点火后就回不来了", "No way back after ignition");
        public static string IgniteAskBody => ArenaText.Pair(
            "飞船一点火，最终决战马上开始，不能再回基地升级和加固。\n\n如果决战失败，只能从决战开头重来，回不到点火之前。现在点火吗？",
            "Once the ship ignites, the final battle begins at once, and you cannot return to the base to upgrade or fortify.\n\n"
            + "If you lose, you can only retry that battle. Ignite now?");
        public static string IgniteAskYes => ArenaText.Pair("点火", "Ignite");
        public static string IgniteAskNo => ArenaText.Pair("先不点火", "Not yet");
    }
}
