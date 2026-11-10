using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The campaign screen layer's part for Day 0's forced tutorial and the weapon crates (2026-11-10): the spotlight (V2Spotlight) sits
    /// under the HUD layer, so the goal, its marker, the prompt and the subtitles stay readable over the dim; the crate's pick-one-of-three
    /// (V2CratePanel) covers everything while it is open. Both are made the first time they are needed.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        V2Spotlight spotlight;
        V2CratePanel cratePanel;

        V2Spotlight SpotlightView
        {
            get
            {
                if (spotlight) return spotlight;
                spotlight = V2Spotlight.Create(root);
                // Just under the HUD layer (the goal, the marker, the prompt), above the game world and the HUD canvas below this one.
                spotlight.transform.SetSiblingIndex(hud ? hud.GetSiblingIndex() : 0);
                return spotlight;
            }
        }

        /// <summary>Spotlights what at returns (asked every frame) with a bright circle of worldRadius cells and the tip "[key] phrase";
        /// dim false shows only the circle's edge and the tip.</summary>
        public void ShowSpotlight(Func<Vector3> at, float worldRadius, string key, string phrase, bool dim = true) =>
            SpotlightView.Show(at, worldRadius, key, phrase, dim, campaign && campaign.rules ? campaign.rules.spotlightDim : .75f);

        /// <summary>The taught step is done: the circle flashes green and the dim fades out.</summary>
        public void PassSpotlight() { if (spotlight) spotlight.Pass(); }

        public void HideSpotlight() { if (spotlight) spotlight.Hide(); }

        /// <summary>The pass flash is still playing (the next step waits for it).</summary>
        public bool SpotlightBusy => spotlight && spotlight.Busy;

        /// <summary>Opens a weapon crate's pick-one-of-three over everything (V2CratePanel.Show).</summary>
        public void ShowCrate(string heading, string sub, IReadOnlyList<V2CratePanel.Option> options, bool canLeave, Action<int> onPick, Action onLeave)
        {
            if (!cratePanel) cratePanel = V2CratePanel.Create(root);
            cratePanel.Show(heading, sub, options, canLeave, onPick, onLeave);
        }

        public void CloseCrate() { if (cratePanel) cratePanel.Close(); }

        public bool CrateOpen => cratePanel && cratePanel.Open;

        /// <summary>A subtitle is on screen now (a line said later waits for it, V2Campaign.SayWhenQuiet).</summary>
        public bool SubtitleShowing => Time.unscaledTime <= subtitleUntil;

        /// <summary>Shows text as the prompt for this frame, over the campaign's own prompt: for a thing whose part is ticked after the
        /// campaign's frame (an afternoon weapon crate). The campaign's prompt comes back once this stops being called.</summary>
        public void OverridePrompt(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            promptOverride = text;
            promptOverrideFrame = Time.frameCount;
        }
    }
}
