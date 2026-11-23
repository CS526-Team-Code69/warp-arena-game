using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The base page's calls for attention (TickBase5): while the night's lamps are short and the alloy pays for one, the rocket page's
    /// lamp button breathes; while a fortification is wanted and the alloy pays for it, the workshop's fortify button does
    /// (V2Campaign.LampsWanted, FortifyWanted). Breathing is a slow swell of the button, on real time, so it moves while the game waits.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        const float NudgeSwell = .06f, NudgeSpeed = 5.2f;

        void TickNudges5()
        {
            var c = campaign;
            if (!c) return;
            float swell = 1f + NudgeSwell * (.5f + .5f * Mathf.Sin(Time.unscaledTime * NudgeSpeed));
            bool lamp = b5Screen == V2Campaign.BaseScreen.Rocket && c.LampsWanted && c.CanAfford(c.LampPrice);
            bool fortify = b5Screen == V2Campaign.BaseScreen.Workshop && c.FortifyWanted && c.FortifyPrice is CampaignCost price && c.CanAfford(price);
            Breathe(r5LampButton, lamp ? swell : 1f);
            Breathe(w5Fortify, fortify ? swell : 1f);
        }

        static void Breathe(V5Plate plate, float scale)
        {
            if (!plate) return;
            var t = plate.transform;
            if (!Mathf.Approximately(t.localScale.x, scale)) t.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
