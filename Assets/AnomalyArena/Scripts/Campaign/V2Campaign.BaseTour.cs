namespace AnomalyArena
{
    /// <summary>
    /// When the guided tour of the base runs (V2CampaignUi.BaseTour.cs): once a run, on the arrival at the end of Day 0, as soon as the
    /// base system has finished talking. While it is still talking the page takes no click (V2CampaignUi.HoldBaseForTour), so a purchase
    /// cannot spend the alloy and the alien energy the tour's two steps are paid with (a skill and the weapon Mk) before the tour begins.
    /// Only on the fifth pass's pages (V5HudKit.On).
    /// </summary>
    public sealed partial class V2Campaign
    {
        // The tour has begun in this run (or could not): it never comes again.
        bool baseTourBegun;

        /// <summary>A new run (StartRun): the tour may come again.</summary>
        void ResetBaseTour() => baseTourBegun = false;

        /// <summary>Each frame of a base visit (Update).</summary>
        void TickBaseTour()
        {
            if (baseTourBegun || !ui || !BaseAfterTutorial || !V5HudKit.On) return;
            if (SpeechBusy)
            {
                if (!ui.BaseTourRunning) ui.HoldBaseForTour();
                return;
            }
            baseTourBegun = true;
            ui.StartBaseTour();
        }
    }
}
