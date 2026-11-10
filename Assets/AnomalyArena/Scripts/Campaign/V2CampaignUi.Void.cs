using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The void currencies on the campaign's screen (2026-11-10): a second row under the three currencies at the top right, in the void's
    /// violet, shown from the first void currency gained (V2Campaign.Void.cs calls ShowVoidWallet every frame). It is part of the HUD
    /// layer, so it hides with it (the base panel, a story scene, the pause). Built the first time it is needed.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // Under the currency row (its top at -20, 44 tall), as wide, a little lower.
        const float VoidWalletTop = -70f, VoidWalletHeight = 36f, VoidWalletWidth = 430f;
        static readonly Color VoidWalletColour = new(.78f, .55f, 1f);

        RectTransform voidWallet;
        Text voidWalletText;
        int voidWalletEnergy = -1, voidWalletAlloy = -1, voidWalletLanguage = -1;

        /// <summary>The void currencies' row: shown with these amounts, or hidden (none gained yet in this run).</summary>
        public void ShowVoidWallet(bool show, int energy, int alloy)
        {
            if (!hud) return;
            if (!show)
            {
                if (voidWallet && voidWallet.gameObject.activeSelf) voidWallet.gameObject.SetActive(false);
                return;
            }
            if (!voidWallet) BuildVoidWallet();
            // The text is written again only when an amount, the language or the text's readiness changes.
            int language = !ArenaText.Ready ? 0 : ArenaText.IsChinese ? 1 : 2;
            if (energy != voidWalletEnergy || alloy != voidWalletAlloy || language != voidWalletLanguage)
            {
                ApplyFont();
                voidWalletEnergy = energy; voidWalletAlloy = alloy; voidWalletLanguage = language;
                voidWalletText.text = ArenaText.Pair($"虚空能量 {energy}　虚空合金 {alloy}", $"Void energy {energy}   Void alloy {alloy}");
            }
            if (!voidWallet.gameObject.activeSelf) voidWallet.gameObject.SetActive(true);
        }

        void BuildVoidWallet()
        {
            voidWallet = Box("VoidCurrencies", hud, new Vector2(1f, 1f), new Vector2(-24f, VoidWalletTop), new Vector2(VoidWalletWidth, VoidWalletHeight), new Vector2(1f, 1f), Panel);
            EdgeBar(voidWallet, VoidWalletColour);
            voidWalletText = Label("Text", voidWallet, "", 17, VoidWalletColour, TextAnchor.MiddleCenter);
            Stretch(voidWalletText.rectTransform, 14f, 2f, 8f, 2f);
            voidWalletEnergy = voidWalletAlloy = -1;
        }
    }
}
