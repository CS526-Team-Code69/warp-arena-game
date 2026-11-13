using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The campaign screen layer while the void trial runs (2026-11-10): the goal, its marker and the side-quest line speak of the map the
    /// player has left, and they lay over the trial's own banner, so they are hidden until the trial is over. They keep their texts and
    /// places meanwhile (only their transparency changes), so whatever sets them goes on as before.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        bool goalsHidden;

        /// <summary>Hides (or shows again) the goal, its marker and the side-quest line.</summary>
        public void SetGoalsHidden(bool hidden)
        {
            if (goalsHidden == hidden || !objectiveBack) return;
            goalsHidden = hidden;
            float alpha = hidden ? 0f : 1f;
            Fade(objectiveBack, alpha);
            Fade(objectiveText, alpha);
            Fade(sideQuestBack, alpha);
            Fade(sideQuestText, alpha);
            Fade(markerText, alpha);
            Fade(markerPulse, alpha);
            Fade(markerArrow, alpha);
            // The day plate speaks of the map left behind too, and its place at the top centre is the trial's boss bar's.
            if (storyDay) Fade(storyDay, alpha);
        }

        static void Fade(Component part, float alpha)
        {
            if (!part) return;
            if (!part.TryGetComponent(out CanvasGroup group)) group = part.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.blocksRaycasts = false;
        }
    }
}
