using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The story's lines in the fifth pass. The subtitle (the mock-up's .sub) is restyled where it stands: one line of large bold
    /// letters on a dark backing that fades out to every side, the speaker's slanted tag before it (orange for the hero, teal for the
    /// base's system), centred just over the weapon slots; a line too long for one row wraps under itself. The night banner (the
    /// mock-up's night) carries the words said as the night falls (V2Campaign.NightFallWords): their first sentence as its title in the
    /// cream paint, widely spaced, and the rest as its line, on the hull-plate banner with a mining lamp standing beside each end. It
    /// slides in from a little higher, holds, and fades out; meanwhile that line shows in no subtitle. The day's other announcements keep
    /// the HUD's own banner, which says them at the same moment.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The subtitle in mock-up pixels: its box's top 862 from the screen's top (so its foot stands StorySubtitleBottom canvas units over
        // the foot), 27-pixel bold letters, padding 10 / 60; the tag 16-pixel letters with padding 6 / 12, 16 before the line; a line wider
        // than SubtitleLinePx wraps.
        const float StorySubtitleBottom = 136f, SubLinePx = 27f, SubPadXPx = 60f, SubPadYPx = 10f, TagFontPx = 16f, TagPadXPx = 12f, TagPadYPx = 6f, TagGapPx = 16f, SubtitleLinePx = 1320f;
        // The night banner in mock-up pixels: its top 384 from the screen's top, 1000 x 185; the title 60 pixels and 16 under it the line
        // (19 pixels), both kept inside the banner's hazard-striped ends (BannerEndsPx each); a lamp 130 wide standing 16 beside each end, 4
        // lower than the banner's top. It slides 24 down over BannerIn seconds while it fades in, and fades out over BannerOut; it stays as
        // long as the line's subtitle would, within BannerSeconds.
        const float BannerTopPx = 384f, BannerWidthPx = 1000f, BannerHeightPx = 185f, BannerTitlePx = 60f, BannerLinePx = 19f, BannerLineGapPx = 16f, BannerEndsPx = 110f;
        const float LampPostWidthPx = 130f, LampPostGapPx = 16f, LampPostDropPx = 4f, BannerSlidePx = 24f, BannerIn = .35f, BannerOut = .6f;
        static readonly Vector2 BannerSeconds = new(3.2f, 5f);

        Image storySubGlow, storySubTag;
        // The subtitle steps aside while the crate's window is open (TickStorySubtitleCover).
        CanvasGroup storySubFade;
        V2CratePanel storyCratePanel;
        float storyCrateSearch;
        bool storySubtitleInBanner;
        RectTransform storyBanner;
        CanvasGroup storyBannerFade;
        Text storyBannerTitle, storyBannerLine;
        float storyBannerClock = -1f, storyBannerLength;

        /// <summary>The subtitle's foot over the screen's foot during play: the fifth pass's place, or the old one.</summary>
        float SubtitlePlayBottom => V5HudKit.On ? StorySubtitleBottom : SubtitleBottom;

        void BuildStoryLines()
        {
            storySubGlow = V5TitleKit.Picture(subtitleRoot, "Glow", V5StoryKit.SoftOval, V5StoryKit.SubtitleBacking);
            storySubGlow.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(storySubGlow.rectTransform);
            storySubTag = V5TitleKit.Picture(subtitleRoot, "Tag", V5StoryKit.Tag, V5TitleKit.Orange, V5StoryKit.TagDensity);
            storySubTag.transform.SetSiblingIndex(subtitleSpeaker.transform.GetSiblingIndex());
            subtitleLine.fontSize = V5StoryKit.FontSize(SubLinePx);
            subtitleLine.fontStyle = FontStyle.Bold;
            subtitleLine.color = V5TitleKit.Ink;
            subtitleLine.lineSpacing = 1f;
            if (!subtitleRoot.TryGetComponent(out storySubFade)) storySubFade = subtitleRoot.gameObject.AddComponent<CanvasGroup>();
            storySubFade.blocksRaycasts = storySubFade.interactable = false;
            subtitleLine.alignment = TextAnchor.MiddleLeft;
            // The line's dark edge thins to the mock-up's soft shadow (the backing carries the contrast); the speaker on its tag has none.
            foreach (var effect in subtitleLine.GetComponents<UnityEngine.UI.Shadow>())
            {
                bool outline = effect is UnityEngine.UI.Outline;
                effect.effectColor = new Color(0f, 0f, 0f, outline ? .55f : .7f);
                effect.effectDistance = outline ? new Vector2(1f, -1f) : new Vector2(0f, -2f);
            }
            subtitleSpeaker.fontSize = V5StoryKit.FontSize(TagFontPx);
            subtitleSpeaker.fontStyle = FontStyle.Bold;
            subtitleSpeaker.color = V5TitleKit.Dark;
            subtitleSpeaker.alignment = TextAnchor.MiddleCenter;
            subtitleSpeaker.horizontalOverflow = HorizontalWrapMode.Overflow;
            foreach (var effect in subtitleSpeaker.GetComponents<UnityEngine.UI.Shadow>()) effect.enabled = false;
            BuildNightBanner();
        }

        /// <summary>Subtitle's fifth-pass layout (it has set the texts): the tag fits the speaker, the line fits its words (wrapping past
        /// SubtitleLinePx), the backing spreads round both. The words said as the night falls go to the night banner instead.</summary>
        void StorySubtitle(string line, bool system, float seconds)
        {
            if (!storySubTag) return;
            storySubtitleInBanner = !string.IsNullOrEmpty(line) && line == V2Campaign.NightFallWords;
            if (storySubtitleInBanner)
            {
                ShowNightBanner(line, seconds);
                subtitleRoot.gameObject.SetActive(false);
                return;
            }
            subtitleSpeaker.color = V5TitleKit.Dark;
            storySubTag.color = system ? V5TitleKit.Teal : V5TitleKit.Orange;
            bool tagged = !string.IsNullOrEmpty(subtitleSpeaker.text);
            SetShown(storySubTag, tagged);
            SetShown(subtitleSpeaker, tagged);
            float padX = Sp(SubPadXPx), padY = Sp(SubPadYPx);
            float tagWidth = tagged ? subtitleSpeaker.preferredWidth + Sp(TagPadXPx * 2f) : 0f, tagHeight = Sp(TagFontPx * 1.25f + TagPadYPx * 2f);
            float gap = tagged ? Sp(TagGapPx) : 0f, lineWidth = Mathf.Min(subtitleLine.preferredWidth, Sp(SubtitleLinePx));
            var lineRect = subtitleLine.rectTransform;
            lineRect.anchorMin = lineRect.anchorMax = new Vector2(0f, .5f);
            lineRect.pivot = new Vector2(0f, .5f);
            lineRect.sizeDelta = new Vector2(lineWidth + 2f, 10f);
            float lineHeight = Mathf.Max(Sp(SubLinePx * 1.3f), subtitleLine.preferredHeight);
            lineRect.sizeDelta = new Vector2(lineWidth + 2f, lineHeight);
            lineRect.anchoredPosition = new Vector2(padX + tagWidth + gap, 0f);
            subtitleRoot.sizeDelta = new Vector2(padX * 2f + tagWidth + gap + lineWidth, Mathf.Max(lineHeight, tagHeight) + padY * 2f);
            foreach (var rect in new[] { storySubTag.rectTransform, subtitleSpeaker.rectTransform })
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
                rect.pivot = new Vector2(0f, .5f);
                rect.anchoredPosition = new Vector2(padX, 0f);
                rect.sizeDelta = new Vector2(tagWidth, tagHeight);
            }
        }

        // ───── The night banner ─────

        void BuildNightBanner()
        {
            storyBanner = V5TitleKit.Rect(hud, "NightBanner");
            storyBanner.anchorMin = storyBanner.anchorMax = storyBanner.pivot = new Vector2(.5f, 1f);
            storyBanner.anchoredPosition = new Vector2(0f, -Sp(BannerTopPx));
            storyBanner.sizeDelta = new Vector2(Sp(BannerWidthPx), Sp(BannerHeightPx));
            storyBannerFade = storyBanner.gameObject.AddComponent<CanvasGroup>();
            storyBannerFade.blocksRaycasts = false;
            storyBannerFade.interactable = false;
            var postSize = V5TitleKit.PieceSize("lamp-post");
            float postHeightPx = LampPostWidthPx * postSize.y / Mathf.Max(1f, postSize.x);
            for (int side = 0; side < 2; side++)
            {
                var anchor = new Vector2(side, 1f);
                var centre = new Vector2((side == 0 ? -1f : 1f) * Sp(LampPostGapPx + LampPostWidthPx * .5f), -Sp(LampPostDropPx + postHeightPx * .5f));
                var glow = V5TitleKit.Picture(storyBanner, side == 0 ? "LampGlowLeft" : "LampGlowRight", V5HudKit.Glow, V5StoryKit.Amber);
                PlaceCentre(glow.rectTransform, anchor, centre, new Vector2(Sp(LampPostWidthPx * 1.5f), Sp(postHeightPx * 1.25f)));
                glow.gameObject.AddComponent<V5StoryArtOnly>();
                var post = V5TitleKit.Picture(storyBanner, side == 0 ? "LampLeft" : "LampRight", V5TitleKit.Piece("lamp-post"), Color.white);
                post.preserveAspect = true;
                PlaceCentre(post.rectTransform, anchor, centre, new Vector2(Sp(LampPostWidthPx), Sp(postHeightPx)));
                if (side == 1) post.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                post.gameObject.AddComponent<V5StoryArtOnly>();
            }
            var frame = V5TitleKit.Picture(storyBanner, "Frame", null, Color.white);
            V5TitleKit.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<V5StoryBannerFrame>();
            storyBannerTitle = V5StoryKit.Label(storyBanner, "Title", BannerTitlePx, Color.white, TextAnchor.MiddleCenter, true);
            V5TitleKit.Painted(storyBannerTitle);
            storyBannerLine = V5StoryKit.Label(storyBanner, "Line", BannerLinePx, V5TitleKit.Paint, TextAnchor.MiddleCenter);
            storyBanner.gameObject.SetActive(false);
        }

        void ShowNightBanner(string words, float seconds)
        {
            if (!storyBanner) return;
            SplitWords(words, false, out string title, out string line);
            float inner = Sp(BannerWidthPx - BannerEndsPx * 2f);
            storyBannerTitle.text = SpreadTitle(title);
            PlaceCentre(storyBannerTitle.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(inner, Sp(BannerTitlePx * 1.3f)));
            V5TitleKit.FitWidth(storyBannerTitle, V5StoryKit.FontSize(BannerTitlePx), V5StoryKit.FontSize(36f));
            storyBannerLine.text = line;
            SetShown(storyBannerLine, line.Length > 0);
            PlaceCentre(storyBannerLine.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(inner, Sp(BannerLinePx * 1.5f)));
            V5TitleKit.FitWidth(storyBannerLine, V5StoryKit.FontSize(BannerLinePx), V5StoryKit.FontSize(13f));
            float titleHeight = Sp(BannerTitlePx), lineHeight = line.Length > 0 ? Sp(BannerLinePx + BannerLineGapPx) : 0f, total = titleHeight + lineHeight;
            storyBannerTitle.rectTransform.anchoredPosition = new Vector2(0f, total * .5f - titleHeight * .5f);
            storyBannerLine.rectTransform.anchoredPosition = new Vector2(0f, -total * .5f + Sp(BannerLinePx * .5f));
            storyBannerClock = 0f;
            storyBannerLength = Mathf.Clamp(seconds, BannerSeconds.x, BannerSeconds.y);
            storyBannerFade.alpha = 0f;
            storyBanner.anchoredPosition = new Vector2(0f, -Sp(BannerTopPx) + Sp(BannerSlidePx));
            storyBanner.gameObject.SetActive(true);
        }

        /// <summary>A banner title widely spaced: Chinese with two spaces between its characters, other words in capitals with one.</summary>
        static string SpreadTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            bool wide = title[0] >= '⺀';
            var text = new StringBuilder(title.Length * 3);
            foreach (char c in wide ? title : title.ToUpperInvariant())
            {
                if (text.Length > 0) text.Append(wide ? "  " : " ");
                text.Append(c);
            }
            return text.ToString();
        }

        /// <summary>The crate's window covers the foot of the screen with its buttons: the subtitle steps aside while it is open (the voice
        /// goes on) and comes back as it closes.</summary>
        void TickStorySubtitleCover()
        {
            if (!storySubFade) return;
            if (!storyCratePanel && Time.unscaledTime >= storyCrateSearch)
            {
                storyCrateSearch = Time.unscaledTime + 1f;
                storyCratePanel = GetComponentInChildren<V2CratePanel>(true);
            }
            float alpha = storyCratePanel && storyCratePanel.Open ? 0f : 1f;
            if (!Mathf.Approximately(storySubFade.alpha, alpha)) storySubFade.alpha = alpha;
        }

        void TickStoryBanner(float dt)
        {
            if (storyBannerClock < 0f || !storyBanner) return;
            storyBannerClock += dt;
            float t = storyBannerClock, alpha = 1f, lift = 0f;
            if (t < BannerIn)
            {
                float k = V5TitleKit.EaseOut(t / BannerIn);
                alpha = k;
                lift = (1f - k) * Sp(BannerSlidePx);
            }
            else if (t > storyBannerLength - BannerOut) alpha = Mathf.Clamp01((storyBannerLength - t) / BannerOut);
            storyBannerFade.alpha = alpha;
            storyBanner.anchoredPosition = new Vector2(0f, -Sp(BannerTopPx) + lift);
            if (t >= storyBannerLength) EndNightBanner();
        }

        void EndNightBanner()
        {
            storyBannerClock = -1f;
            if (storyBanner && storyBanner.gameObject.activeSelf) storyBanner.gameObject.SetActive(false);
        }
    }
}
