using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The story's wallet in the fifth pass (the mock-up's .wallet, top right; the names from ui-f/resources.html): the old currency row
    /// restyled where it stands, each currency a ringed medal, its name and its number in its own colour (ResourceColours: alloy copper,
    /// alien energy teal-green, dark matter violet), alloy first, and, from the first night on, the lamps ("×3" in gold beside a lamp
    /// with an amber glow). The void currencies stand in a second plate under it, their medals ringed and lit
    /// in violet. The old lamp counter becomes the mock-up's lamp box above the key hints (bottom right): the lamp, "×2" in gold, the key
    /// that sets one down and its words. A number that changes swells once.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // In mock-up pixels: the wallet right 24 and top 18, 58 tall (padding 8 / 14 / 8 / 10, corners cut at 10); each currency a 42-pixel
        // medal, 7 to its 23-pixel number and 8 after it, 4 between currencies. The void currencies 6 under it in a plate 50 tall with
        // 34-pixel medals and 19-pixel numbers. The lamp box 26 from the right and 82 over the foot, 84 tall (padding 12 / 20 / 12 / 14,
        // corners cut at 12): a 60-pixel medal, 14 to the 36-pixel count, 14 to the keycap, 8 to the words.
        const float WalletRightPx = 24f, WalletTopPx = 18f, WalletHeightPx = 58f, WalletMedalPx = 42f, WalletNumberPx = 23f;
        const float VoidGapPx = 6f, VoidHeightPx = 50f, VoidMedalPx = 34f, VoidNumberPx = 19f;
        const float LampRightPx = 26f, LampBottomPx = 82f, LampHeightPx = 84f, LampMedalPx = 60f, LampCountPx = 36f;
        static readonly string[] WalletMedalNames = { "EnergyMedal", "AlloyMedal", "DarkMedal", "LampMedal" };
        static readonly string[] WalletNameIds = { "EnergyName", "AlloyName", "DarkName" };
        static readonly string[] VoidNames = { "VoidEnergy", "VoidAlloy" };
        // The wallet's order on the screen, by the currency index (0 alien energy, 1 alloy, 2 dark matter): alloy, alien energy, dark
        // matter. Each name stands 6 mock-up pixels before its number, in 16-pixel letters.
        static readonly int[] WalletOrder = { 1, 0, 2 };
        const float WalletNamePx = 16f, WalletNameGapPx = 6f;

        RectTransform storyWallet;
        readonly V5HudMedal[] storyWalletMedals = new V5HudMedal[4];
        readonly Text[] storyWalletNames = new Text[3];
        readonly int[] storyWalletShown = { -1, -1, -1, -1 };
        Text storyLampAmount;
        V5HudMedal[] storyVoidMedals;
        Text[] storyVoidAmounts;
        int storyVoidEnergy = -1, storyVoidAlloy = -1;
        Font storyVoidFont;
        V5HudMedal storyLampMedal;
        V5HudKey storyLampKey;
        Text storyLampHint;
        int storyLampStock = -1;
        readonly Dictionary<(Font, int, int), float> storyDigitWidths = new();

        /// <summary>A wallet number's colour (energy, alloy, dark matter): each currency's own (ResourceColours).</summary>
        static Color WalletColour(int which) => ResourceColours.Of(which);

        /// <summary>A currency's name in the wallet (energy, alloy, dark matter).</summary>
        static string WalletName(int which) => which switch
        {
            0 => ArenaText.Pair("异星能量", "Alien energy"),
            1 => ArenaText.Pair("合金", "Alloy"),
            _ => ArenaText.Pair("暗物质", "Dark matter")
        };

        /// <summary>The wallet cell a gain flies into (the fifth pass's medal, the old row's icon): energy, alloy or dark matter.</summary>
        RectTransform WalletCell(int which)
        {
            if (which < 0 || which > 2) return null;
            if (V5HudKit.On && storyBuilt) return storyWalletMedals[which] ? storyWalletMedals[which].Rect : null;
            return currencyIcons != null && currencyIcons[which] ? currencyIcons[which].rectTransform : null;
        }

        void BuildStoryWallet()
        {
            storyWallet = (RectTransform)currencyLabels[0].transform.parent;
            if (storyWallet.TryGetComponent<Image>(out var back)) back.enabled = false;
            var plate = V5HudKit.Plate(storyWallet, "Plate", V5HudKit.PlateLook.Plain, 10f, V5HudKit.StoryCss);
            plate.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(plate.Rect);
            foreach (var icon in currencyIcons) icon.enabled = false;
            currencyText.text = "";
            storyWallet.anchoredPosition = new Vector2(-Sp(WalletRightPx), -Sp(WalletTopPx));
            for (int i = 0; i < 4; i++) storyWalletMedals[i] = V5HudKit.Medal(storyWallet, WalletMedalNames[i], Sp(WalletMedalPx));
            storyWalletMedals[3].SetGlow(V5StoryKit.Amber);
            for (int i = 0; i < 3; i++) StyleNumber(currencyLabels[i], WalletNumberPx, WalletColour(i));
            for (int i = 0; i < 3; i++) storyWalletNames[i] = V5StoryKit.Label(storyWallet, WalletNameIds[i], WalletNamePx, V5TitleKit.Ink2, TextAnchor.MiddleCenter);
            storyLampAmount = V5StoryKit.Label(storyWallet, "LampAmount", WalletNumberPx, V5TitleKit.Gold, TextAnchor.MiddleCenter, true);
        }

        static void StyleNumber(Text label, float mockPixels, Color colour)
        {
            label.fontSize = V5StoryKit.FontSize(mockPixels);
            label.fontStyle = FontStyle.Bold;
            label.color = colour;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void RefreshStoryWallet()
        {
            var c = campaign;
            var art = c.art;
            storyWalletMedals[0].SetIcons(art ? art.energy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            storyWalletMedals[1].SetIcons(art ? art.alloy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Alloy));
            storyWalletMedals[2].SetIcons(art ? art.dark : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Dark));
            storyWalletMedals[3].SetIcons(art ? art.lamp : null, V5StoryKit.LampIcon);
            int[] values = { c.Energy, c.Alloy, c.Dark };
            for (int i = 0; i < 3; i++)
            {
                // A number gained pulses when its icon lands (ShowGain's flight); one that changes otherwise (spent at the base) pulses here.
                if (storyWalletShown[i] >= 0 && values[i] != storyWalletShown[i] && currencyPulse[i] <= 0f && !Flying(i)) currencyPulse[i] = 1f;
                storyWalletShown[i] = values[i];
                StoryText(storyWalletNames[i], WalletName(i));
            }
            if (storyWalletShown[3] >= 0 && c.LampStock != storyWalletShown[3]) PulseOnce(storyLampAmount.transform);
            storyWalletShown[3] = c.LampStock;
            StoryText(storyLampAmount, "×" + c.LampStock);
            // The lamps join the wallet with the first night (the tutorial night gives the first ones), never before.
            LayOutStoryWallet(c.LampStock > 0 || c.Day > 0 || c.Stage is CampaignStage.Dusk or CampaignStage.Night);
        }

        void LayOutStoryWallet(bool lamps)
        {
            float height = Sp(WalletHeightPx), medal = Sp(WalletMedalPx), x = Sp(10f);
            for (int slot = 0; slot < 4; slot++)
            {
                int i = slot < 3 ? WalletOrder[slot] : 3;
                var label = i < 3 ? currencyLabels[i] : storyLampAmount;
                bool shown = i < 3 || lamps;
                SetShown(storyWalletMedals[i], shown);
                SetShown(label, shown);
                if (!shown) continue;
                if (slot > 0) x += Sp(4f);
                PlaceCentre(storyWalletMedals[i].Rect, new Vector2(0f, .5f), new Vector2(x + medal * .5f, 0f), Vector2.one * medal);
                x += medal + Sp(7f);
                if (i < 3)
                {
                    float name = storyWalletNames[i].preferredWidth;
                    PlaceSpan(storyWalletNames[i], x, name, height);
                    x += name + Sp(WalletNameGapPx);
                }
                // Never narrower than three digits (the lamps: one), so the row does not shift as numbers grow and shrink.
                float w = Mathf.Max(label.preferredWidth, DigitsWidth(label, i < 3 ? "000" : "×0"));
                PlaceSpan(label, x, w, height);
                x += w + Sp(8f);
            }
            storyWallet.sizeDelta = new Vector2(x + Sp(14f), height);
        }

        float DigitsWidth(Text label, string sample)
        {
            var key = (label.font, label.fontSize, sample.Length);
            if (!storyDigitWidths.TryGetValue(key, out float width)) storyDigitWidths[key] = width = V5StoryKit.WidthOf(label, sample);
            return width;
        }

        // ───── The void currencies ─────

        /// <summary>BuildVoidWallet's row restyled: its plate, a violet-ringed medal and a violet number for each void currency.</summary>
        void RestyleStoryVoidWallet()
        {
            if (voidWallet.TryGetComponent<Image>(out var back)) back.enabled = false;
            var bar = voidWallet.Find("Bar");
            if (bar) bar.gameObject.SetActive(false);
            voidWalletText.enabled = false;
            var plate = V5HudKit.Plate(voidWallet, "Plate", V5HudKit.PlateLook.Plain, 10f, V5HudKit.StoryCss);
            plate.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(plate.Rect);
            voidWallet.anchoredPosition = new Vector2(-Sp(WalletRightPx), -Sp(WalletTopPx + WalletHeightPx + VoidGapPx));
            storyVoidMedals = new V5HudMedal[2];
            storyVoidAmounts = new Text[2];
            for (int i = 0; i < 2; i++)
            {
                storyVoidMedals[i] = V5HudKit.Medal(voidWallet, VoidNames[i] + "Medal", Sp(VoidMedalPx));
                storyVoidMedals[i].SetRing(V5HudKit.Violet);
                storyVoidMedals[i].SetGlow(V5StoryKit.VioletGlow);
                storyVoidAmounts[i] = V5StoryKit.Label(voidWallet, VoidNames[i] + "Amount", VoidNumberPx, V5HudKit.VioletHi, TextAnchor.MiddleCenter, true);
            }
            storyVoidEnergy = storyVoidAlloy = -1;
            storyVoidFont = null;
        }

        /// <summary>Every frame the void wallet shows (ShowVoidWallet): its icons follow the art switch, a changed amount pulses once and the
        /// plate fits its numbers.</summary>
        void StoryVoidWallet(int energy, int alloy)
        {
            if (storyVoidMedals == null) return;
            var art = campaign ? campaign.art : null;
            // The void's own currencies have no pictures of their own: the alien energy's and alloy's, ringed and lit in violet.
            storyVoidMedals[0].SetIcons(art ? art.energy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            storyVoidMedals[1].SetIcons(art ? art.alloy : null, WhiteboxIcons.Look("voidalloy"));
            bool changed = storyVoidFont != storyVoidAmounts[0].font;
            if (energy != storyVoidEnergy)
            {
                if (storyVoidEnergy >= 0) PulseOnce(storyVoidAmounts[0].transform);
                storyVoidEnergy = energy;
                storyVoidAmounts[0].text = energy.ToString();
                changed = true;
            }
            if (alloy != storyVoidAlloy)
            {
                if (storyVoidAlloy >= 0) PulseOnce(storyVoidAmounts[1].transform);
                storyVoidAlloy = alloy;
                storyVoidAmounts[1].text = alloy.ToString();
                changed = true;
            }
            if (!changed) return;
            storyVoidFont = storyVoidAmounts[0].font;
            float height = Sp(VoidHeightPx), medal = Sp(VoidMedalPx), x = Sp(10f);
            for (int i = 0; i < 2; i++)
            {
                if (i > 0) x += Sp(4f);
                PlaceCentre(storyVoidMedals[i].Rect, new Vector2(0f, .5f), new Vector2(x + medal * .5f, 0f), Vector2.one * medal);
                x += medal + Sp(7f);
                float w = Mathf.Max(storyVoidAmounts[i].preferredWidth, DigitsWidth(storyVoidAmounts[i], "00"));
                PlaceSpan(storyVoidAmounts[i], x, w, height);
                x += w + Sp(8f);
            }
            voidWallet.sizeDelta = new Vector2(x + Sp(14f), height);
        }

        // ───── The lamp box ─────

        /// <summary>How far in from the screen's right edge the lamp box reaches, in screen pixels, as it was last laid out (0 before it
        /// ever was): the battle HUD keeps its dock clear of it (V5BattleHud.DockHalf).</summary>
        internal float StoryLampReach => storyLampStock >= 0 && lampBox && canvas ? (Sp(LampRightPx) + lampBox.sizeDelta.x) * canvas.scaleFactor : 0f;

        void BuildStoryLampBox()
        {
            if (lampBox.TryGetComponent<Image>(out var back)) back.enabled = false;
            var plate = V5HudKit.Plate(lampBox, "Plate", V5HudKit.PlateLook.Plain, 12f, V5HudKit.StoryCss);
            plate.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(plate.Rect);
            lampIcon.enabled = false;
            var cap = lampBox.Find("Key");
            if (cap) cap.gameObject.SetActive(false); // the old cap keeps its key's name, hidden; the keycap below shows it
            lampBox.anchorMin = lampBox.anchorMax = lampBox.pivot = new Vector2(1f, 0f);
            lampBox.anchoredPosition = new Vector2(-Sp(LampRightPx), Sp(LampBottomPx));
            storyLampMedal = V5HudKit.Medal(lampBox, "LampMedal", Sp(LampMedalPx));
            storyLampMedal.SetGlow(V5StoryKit.Amber);
            StyleNumber(lampCount, LampCountPx, V5TitleKit.Gold);
            storyLampKey = V5HudKit.Key(lampBox, "KeyCap", ArenaKeys.LampKey, false, V5HudKit.StoryCss);
            storyLampHint = V5StoryKit.Label(lampBox, "Hint", 16f, V5TitleKit.Ink2, TextAnchor.MiddleCenter);
        }

        /// <summary>RefreshLampCounter while the box shows (the afternoon and the night): the lamps carried (orange once none are left),
        /// the key that sets one down as the player has it (ArenaKeys) and its words; the box fits them.</summary>
        void RefreshStoryLamps()
        {
            var art = campaign.art;
            storyLampMedal.SetIcons(art ? art.lamp : null, V5StoryKit.LampIcon);
            int left = campaign.LampStock;
            if (storyLampStock >= 0 && left != storyLampStock) PulseOnce(lampCount.transform);
            storyLampStock = left;
            StoryText(lampCount, "×" + left);
            lampCount.color = left > 0 ? V5TitleKit.Gold : V5TitleKit.OrangeHi;
            if (lampKey.text != ArenaKeys.LampKey) lampKey.text = ArenaKeys.LampKey;
            storyLampKey.SetText(ArenaKeys.LampKey);
            StoryText(storyLampHint, ArenaText.Pair("放下矿灯", "Set down a lamp"));
            float height = Sp(LampHeightPx), medal = Sp(LampMedalPx), x = Sp(14f);
            PlaceCentre(storyLampMedal.Rect, new Vector2(0f, .5f), new Vector2(x + medal * .5f, 0f), Vector2.one * medal);
            x += medal + Sp(14f);
            float count = lampCount.preferredWidth;
            PlaceSpan(lampCount, x, count, height);
            x += count + Sp(14f);
            var key = storyLampKey.Rect;
            key.anchorMin = key.anchorMax = new Vector2(0f, .5f);
            key.pivot = new Vector2(0f, .5f);
            key.anchoredPosition = new Vector2(x, 0f);
            x += key.sizeDelta.x + Sp(8f);
            float words = storyLampHint.preferredWidth;
            PlaceSpan(storyLampHint, x, words, height);
            x += words + Sp(20f);
            lampBox.sizeDelta = new Vector2(x, height);
        }
    }

    /// <summary>The story's three currencies' colours wherever they are shown (the wallet's numbers, the mining bar, the names over the
    /// nodes, the icon that flies to the wallet, the sparks of mining): alloy copper, alien energy teal-green, dark matter a light violet
    /// (on dark ground; violet is kept for dark matter and the void).</summary>
    public static class ResourceColours
    {
        public static readonly Color Alloy = V5TitleKit.Rgb(0xE3, 0x9A, 0x55), Energy = V5TitleKit.Rgb(0x3F, 0xE0, 0xB0), Dark = V5TitleKit.Rgb(0xB4, 0x8C, 0xFF);

        /// <summary>By the campaign's currency index (V2CampaignUi.ShowGain's): 0 alien energy, 1 alloy, 2 dark matter.</summary>
        public static Color Of(int which) => which switch { 0 => Energy, 1 => Alloy, _ => Dark };
    }
}
