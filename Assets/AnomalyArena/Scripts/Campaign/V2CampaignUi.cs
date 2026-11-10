using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The V2 campaign's own screen layer, built at run time like the tutorial pages (no scene change): currencies, the base bar and the
    /// objective over the HUD; subtitles; the black title card, the memory CG and the white-out; and the base panel (D1-C: a 2D
    /// panel, one tab per room with its picture; the ship tab shows the ship's state). A room's next level never says what it unlocks
    /// (V2 doc: the new feature is seen only once unlocked).
    /// </summary>
    public sealed partial class V2CampaignUi : MonoBehaviour
    {
        V2Campaign campaign;
        GameManager gm;
        Font font;
        Canvas canvas;
        RectTransform root, hud, baseScreen, titleCard, cgScreen, baseBar;
        Image cgVignette;
        Text currencyText, baseText, objectiveText, flashText, subtitleSpeaker, subtitleLine, titleText, titleSub, cgSkip, markerText;
        Image baseHpFill, baseShieldFill, whiteOut, markerIcon, flashBack;
        // The subtitle has no plate behind it since 2026-11-10: its two texts stand on this rect, the speaker over the line.
        RectTransform subtitleRoot;
        // Since 2026-11-06 the guide is stronger (a bigger marker with a pulse, an arrow at the screen's edge when the goal is off screen)
        // and the top-left goal sits on a dark backing with the way and the distance.
        Image markerPulse, objectiveBack; RectTransform markerArrow; string objectiveBody;
        // Since 2026-11-06: a side-quest line under the goal in the goal's style, and a yes / no question that holds the run still.
        Image sideQuestBack; Text sideQuestText; string sideQuestBody;
        RectTransform confirmScreen; Text confirmTitle, confirmBody, confirmHint, confirmYesLabel, confirmNoLabel;
        Action confirmYes, confirmNo; float confirmOpenedAt; bool confirmHoldsRun; ArenaUiState confirmReturnPhase;
        readonly Image[] sidePips = new Image[4];
        Image[] currencyIcons;
        Text[] currencyLabels;
        RawImage cgPicture;
        float flashUntil, subtitleUntil, titleUntil, titleLength, whiteOutClock, whiteOutLength, refreshAt;
        Vector3? marker;
        // cg
        int cgShot; float cgClock, cgLength; Texture cgTexture, cgFrontTexture;
        RawImage cgForeground;
        public bool CgSkipped { get; private set; }

        static readonly Color Panel = new(.04f, .07f, .12f, .94f), PanelEdge = new(.3f, .55f, .85f, .55f), Soft = new(.86f, .9f, .96f), Faint = new(.62f, .72f, .85f);
        static readonly Color Accent = new(.45f, .85f, 1f), Warn = new(1f, .45f, .3f), Good = new(.5f, 1f, .6f), SystemColour = new(.5f, .9f, 1f), PlayerColour = new(1f, .86f, .55f);
        static readonly Color SideQuestColour = new(.78f, .55f, 1f);
        // The goal's backing: its top edge, the height it keeps for one or two lines, and the gap to the side-quest line under it.
        const float ObjectiveTop = -202f, ObjectiveMinHeight = 78f, SideQuestGap = 8f, SideQuestMinHeight = 48f, ObjectiveLeft = 14f, ObjectiveWidth = 620f;
        // 2026-11-10 (v2-P52, three overlaps with the HUD's own texts): the goal panels step down under the HUD's banner while it shows,
        // this far below it and at this speed (canvas units a second); the base bar sits at least this far below the HUD's batch line
        // ("N alive · M pending"), and never higher than its old place.
        const float BannerGap = 8f, GoalStepSpeed = 1600f, BaseBarTop = 112f, BaseBarGap = 4f;
        // The subtitle (2026-11-10: larger, no plate): its width, its bottom edge over the slots and the mining bar, its bottom edge in
        // the strip the base panel keeps free, the two sizes and the gap between the line and the speaker over it.
        const float SubtitleWidth = 1240f, SubtitleBottom = 250f, BaseSubtitleBottom = 104f, SubtitleSpeakerGap = 8f;
        const int SubtitleLineSize = 32, SubtitleSpeakerSize = 22;
        // How far the goal panels stand below their place now (0 without a banner over them).
        float goalDrop;

        public static V2CampaignUi Create(V2Campaign campaign)
        {
            var go = new GameObject("V2CampaignUi", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var ui = go.AddComponent<V2CampaignUi>();
            ui.campaign = campaign;
            ui.gm = GameManager.Instance;
            ui.Build();
            return ui;
        }

        void Build()
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            int hudOrder = gm && gm.hud && gm.hud.v2Hud && gm.hud.v2Hud.localizedCanvas ? gm.hud.v2Hud.localizedCanvas.sortingOrder : 0;
            canvas.sortingOrder = hudOrder + 5;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;
            root = (RectTransform)transform;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            hud = MakeRect("Hud", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Currencies, top right.
            var currency = Box("Currencies", hud, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(430f, 44f), new Vector2(1f, 1f), Panel);
            currencyIcons = new Image[3];
            currencyLabels = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                currencyIcons[i] = Icon(currency, new Vector2(0f, .5f), new Vector2(12f + i * 140f, 0f), 34f);
                currencyLabels[i] = Label("Amount" + i, currency, "", 19, Soft, TextAnchor.MiddleLeft);
                Anchor(currencyLabels[i].rectTransform, new Vector2(0f, .5f), new Vector2(50f + i * 140f, 0f), new Vector2(88f, 36f), new Vector2(0f, .5f));
            }
            currencyText = Label("Text", currency, "", 17, Soft, TextAnchor.MiddleCenter);
            Stretch(currencyText.rectTransform, 8f, 4f, 8f, 4f);
            // Base bar, top centre under the wave label.
            var bar = baseBar = Box("BaseBar", hud, new Vector2(.5f, 1f), new Vector2(0f, -112f), new Vector2(480f, 54f), new Vector2(.5f, 1f), Panel);
            baseText = Label("Text", bar, "", 15, Soft, TextAnchor.UpperCenter);
            Stretch(baseText.rectTransform, 8f, 2f, 8f, 26f);
            var track = Box("Track", bar, new Vector2(.5f, 0f), new Vector2(0f, 10f), new Vector2(400f, 12f), new Vector2(.5f, 0f), new Color(.1f, .12f, .16f, 1f));
            baseHpFill = Fill(track, new Color(.95f, .6f, .25f));
            baseShieldFill = Fill(track, new Color(.45f, .85f, 1f, .85f));
            for (int i = 0; i < 4; i++)
            {
                sidePips[i] = Box("Side" + i, bar, new Vector2(.5f, 0f), new Vector2(-232f + (i % 2) * 0f, 0f), new Vector2(12f, 12f), new Vector2(.5f, .5f), Color.white).GetComponent<Image>();
            }
            // Side pips sit like a compass at the bar's left: north on top, south at the bottom.
            Place(sidePips[0].rectTransform, new Vector2(-228f, 40f)); Place(sidePips[2].rectTransform, new Vector2(-228f, 14f));
            Place(sidePips[3].rectTransform, new Vector2(-241f, 27f)); Place(sidePips[1].rectTransform, new Vector2(-215f, 27f));
            // Objective, left.
            objectiveBack = Box("ObjectiveBack", hud, new Vector2(0f, 1f), new Vector2(14f, ObjectiveTop), new Vector2(620f, ObjectiveMinHeight), new Vector2(0f, 1f), new Color(0f, .02f, .06f, .78f)).GetComponent<Image>();
            EdgeBar(objectiveBack.rectTransform, Accent);
            objectiveBack.gameObject.SetActive(false);
            objectiveText = Label("Objective", hud, "", 24, Color.white, TextAnchor.UpperLeft);
            Anchor(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, ObjectiveTop - 8f), new Vector2(596f, 66f), new Vector2(0f, 1f));
            Shadow(objectiveText);
            // Side quest, under the goal in the same style (since 2026-11-06: the black hole's side quest).
            sideQuestBack = Box("SideQuestBack", hud, new Vector2(0f, 1f), new Vector2(14f, ObjectiveTop - ObjectiveMinHeight - SideQuestGap), new Vector2(620f, SideQuestMinHeight), new Vector2(0f, 1f), new Color(0f, .02f, .06f, .78f)).GetComponent<Image>();
            EdgeBar(sideQuestBack.rectTransform, SideQuestColour);
            sideQuestText = Label("SideQuest", sideQuestBack.rectTransform, "", 22, Color.white, TextAnchor.MiddleLeft);
            sideQuestText.fontStyle = FontStyle.Bold;
            Stretch(sideQuestText.rectTransform, 16f, 6f, 8f, 6f);
            Shadow(sideQuestText);
            sideQuestBack.gameObject.SetActive(false);
            // No prompt line at the bottom centre since 2026-11-10: the prompt stands in the goal panel (V2CampaignUi.Feedback.cs),
            // which also builds the mining bar, the lamp counter and the texts of what was just gained.
            BuildFeedback();
            // Objective marker (screen point of the objective, clamped to the edges).
            markerPulse = Box("MarkerPulse", hud, new Vector2(0f, 0f), Vector2.zero, new Vector2(36f, 36f), new Vector2(.5f, .5f), Accent).GetComponent<Image>();
            markerPulse.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            markerIcon = Box("Marker", hud, new Vector2(0f, 0f), Vector2.zero, new Vector2(36f, 36f), new Vector2(.5f, .5f), Accent).GetComponent<Image>();
            markerIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Box("MarkerCore", markerIcon.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(14f, 14f), new Vector2(.5f, .5f), Color.white);
            // The edge arrow: a chevron whose tip points along +x, turned toward the goal.
            markerArrow = MakeRect("MarkerArrow", hud, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40f, 40f));
            for (int i = 0; i < 2; i++)
                Box("ArrowBar" + i, markerArrow, new Vector2(.5f, .5f), new Vector2(14f, 0f), new Vector2(30f, 9f), new Vector2(1f, .5f), Accent).localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -40f : 40f);
            markerArrow.gameObject.SetActive(false);
            markerText = Label("MarkerText", hud, "", 20, Color.white, TextAnchor.MiddleCenter);
            markerText.fontStyle = FontStyle.Bold;
            // Placed in the same frame as the marker (from the bottom-left corner), so its distance sits under the marker.
            markerText.rectTransform.anchorMin = markerText.rectTransform.anchorMax = Vector2.zero;
            markerText.rectTransform.sizeDelta = new Vector2(160f, 28f);
            Shadow(markerText);

            // Subtitle, bottom centre above the slots and the mining bar: large, no plate behind it, the speaker over the line.
            subtitleRoot = MakeRect("Subtitle", root, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, SubtitleBottom), new Vector2(SubtitleWidth, 10f));
            subtitleRoot.pivot = new Vector2(.5f, 0f);
            subtitleLine = Label("Line", subtitleRoot, "", SubtitleLineSize, Color.white, TextAnchor.LowerCenter);
            subtitleLine.lineSpacing = 1.12f;
            Anchor(subtitleLine.rectTransform, new Vector2(.5f, 0f), Vector2.zero, new Vector2(SubtitleWidth, 48f), new Vector2(.5f, 0f));
            Edge(subtitleLine);
            subtitleSpeaker = Label("Speaker", subtitleRoot, "", SubtitleSpeakerSize, SystemColour, TextAnchor.LowerCenter);
            subtitleSpeaker.fontStyle = FontStyle.Bold;
            Anchor(subtitleSpeaker.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 56f), new Vector2(SubtitleWidth, 32f), new Vector2(.5f, 0f));
            Edge(subtitleSpeaker);
            subtitleRoot.gameObject.SetActive(false);
            // Flash, centre, on a dark plate.
            flashBack = Box("Flash", root, new Vector2(.5f, .5f), new Vector2(0f, 150f), new Vector2(1100f, 64f), new Vector2(.5f, .5f), new Color(0f, 0f, 0f, .7f)).GetComponent<Image>();
            flashText = Label("Text", flashBack.rectTransform, "", 22, Color.white, TextAnchor.MiddleCenter);
            Stretch(flashText.rectTransform, 18f, 4f, 18f, 4f);
            Shadow(flashText);
            flashBack.gameObject.SetActive(false);

            BuildBasePanel();
            // The flash shows over the base panel: the purchases there say what they gave (the title card, the CG and the white-out,
            // made after this, still cover it).
            flashBack.transform.SetAsLastSibling();

            // Title card and CG cover everything.
            titleCard = Box("TitleCard", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Color.black);
            StretchFull(titleCard);
            titleText = Label("Title", titleCard, "", 64, Color.white, TextAnchor.MiddleCenter);
            Stretch(titleText.rectTransform, 0f, 380f, 0f, 380f);
            titleSub = Label("Sub", titleCard, "", 22, Faint, TextAnchor.MiddleCenter);
            Stretch(titleSub.rectTransform, 0f, 470f, 0f, 300f);
            titleCard.gameObject.SetActive(false);

            cgScreen = Box("Cg", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Color.black);
            StretchFull(cgScreen);
            cgPicture = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            cgPicture.rectTransform.SetParent(cgScreen, false);
            Anchor(cgPicture.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 30f), new Vector2(1080f, 720f), new Vector2(.5f, .5f));
            cgPicture.raycastTarget = false;
            cgForeground = new GameObject("Foreground", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            cgForeground.rectTransform.SetParent(cgScreen, false);
            Anchor(cgForeground.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 30f), new Vector2(1080f, 720f), new Vector2(.5f, .5f));
            cgForeground.raycastTarget = false;
            cgForeground.color = new Color(1f, 1f, 1f, 0f);
            // A memory: the pictures fade warm and pale with dark edges.
            cgVignette = new GameObject("Vignette", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            cgVignette.rectTransform.SetParent(cgScreen, false);
            Stretch(cgVignette.rectTransform, 0f, 0f, 0f, 0f);
            cgVignette.sprite = V2Hud.EdgeVignette();
            cgVignette.color = new Color(0f, 0f, 0f, .92f);
            cgVignette.raycastTarget = false;
            cgSkip = Label("Skip", cgScreen, "", 15, Faint, TextAnchor.LowerRight);
            Stretch(cgSkip.rectTransform, 0f, 0f, 24f, 16f);
            cgScreen.GetComponent<Image>().raycastTarget = true; // the whole CG screen takes the skip click
            var skip = cgScreen.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(() => CgSkipped = true);
            cgScreen.gameObject.SetActive(false);
            // Subtitles stay above the CG.
            subtitleRoot.SetAsLastSibling();

            whiteOut = Box("WhiteOut", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
            StretchFull(whiteOut.rectTransform);
            whiteOut.raycastTarget = false;
            BuildConfirm();
            HideAll();
        }

        /// <summary>The yes / no question (since 2026-11-06): a dimmed screen that takes every click, so none reaches the game or the panels
        /// under it, and a panel in the middle with a title, the question, OK and Cancel, and the keys that answer it.</summary>
        void BuildConfirm()
        {
            confirmScreen = Box("Confirm", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, .01f, .03f, .7f));
            StretchFull(confirmScreen);
            confirmScreen.GetComponent<Image>().raycastTarget = true;
            var panel = Box("Panel", confirmScreen, new Vector2(.5f, .5f), new Vector2(0f, 20f), new Vector2(720f, 340f), new Vector2(.5f, .5f), Panel);
            Outline(panel.gameObject);
            confirmTitle = Label("Title", panel, "", 30, Accent, TextAnchor.UpperCenter);
            confirmTitle.fontStyle = FontStyle.Bold;
            Stretch(confirmTitle.rectTransform, 32f, 26f, 32f, 270f);
            confirmBody = Label("Body", panel, "", 20, Soft, TextAnchor.UpperCenter);
            Stretch(confirmBody.rectTransform, 44f, 86f, 44f, 116f);
            var yes = MakeButton(panel, "", new Vector2(-140f, -96f), new Vector2(240f, 58f), () => CloseConfirm(true), true);
            var no = MakeButton(panel, "", new Vector2(140f, -96f), new Vector2(240f, 58f), () => CloseConfirm(false), false);
            confirmYesLabel = yes.GetComponentInChildren<Text>();
            confirmNoLabel = no.GetComponentInChildren<Text>();
            confirmHint = Label("Keys", panel, "", 15, Faint, TextAnchor.LowerCenter);
            Stretch(confirmHint.rectTransform, 20f, 300f, 20f, 12f);
            confirmScreen.gameObject.SetActive(false);
        }

        /// <summary>A thin accent bar down the left edge of a backing, as tall as the backing.</summary>
        void EdgeBar(RectTransform backing, Color colour)
        {
            var bar = Box("Bar", backing, new Vector2(0f, .5f), Vector2.zero, new Vector2(6f, 0f), new Vector2(0f, .5f), colour);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f);
            bar.sizeDelta = new Vector2(6f, 0f); bar.anchoredPosition = Vector2.zero;
        }

        // ───── Public calls ─────

        public void HideAll()
        {
            if (hud) hud.gameObject.SetActive(false);
            if (baseScreen) baseScreen.gameObject.SetActive(false);
            if (titleCard) titleCard.gameObject.SetActive(false);
            if (cgScreen) cgScreen.gameObject.SetActive(false);
            if (subtitleRoot) subtitleRoot.gameObject.SetActive(false);
            subtitleUntil = 0f; // Update shows a subtitle again while its time lasts
            if (flashBack) flashBack.gameObject.SetActive(false);
            flashUntil = 0f;
            // The run is over or not begun: an open question goes away unanswered and the side quest is cleared.
            if (confirmScreen) confirmScreen.gameObject.SetActive(false);
            confirmYes = confirmNo = null; confirmHoldsRun = false;
            sideQuestBody = null;
            if (sideQuestBack) sideQuestBack.gameObject.SetActive(false);
        }

        public void ShowTitleCard(string title, string sub, float seconds)
        {
            ApplyFont();
            titleText.text = title; titleSub.text = sub;
            titleUntil = Time.unscaledTime + seconds; titleLength = seconds;
            titleCard.gameObject.SetActive(true);
        }

        public void Subtitle(string speaker, string line, bool system, float seconds)
        {
            ApplyFont();
            subtitleSpeaker.text = speaker; subtitleSpeaker.color = system ? SystemColour : PlayerColour;
            subtitleLine.text = line;
            // The line's box is as tall as its text (one line or several), and the speaker stands over it.
            float height = Mathf.Max(SubtitleLineSize * 1.3f, subtitleLine.preferredHeight);
            subtitleLine.rectTransform.sizeDelta = new Vector2(SubtitleWidth, height);
            subtitleSpeaker.rectTransform.anchoredPosition = new Vector2(0f, height + SubtitleSpeakerGap);
            subtitleUntil = Time.unscaledTime + seconds;
            subtitleRoot.gameObject.SetActive(true);
        }

        public void ClearSubtitle() { subtitleUntil = 0f; if (subtitleRoot) subtitleRoot.gameObject.SetActive(false); }

        public void Flash(string text, float seconds = 2.4f)
        {
            ApplyFont();
            flashText.text = text;
            // The plate fits the line (a long one wraps inside 1100).
            flashBack.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(flashText.preferredWidth + 48f, 320f, 1100f), 64f);
            flashUntil = Time.unscaledTime + seconds;
            flashBack.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
        public void ClearFlash() => flashUntil = 0f;

        /// <summary>A side-quest line under the goal, in the goal's style with a violet bar and "Side quest:" before it (null or empty hides
        /// it). Since 2026-11-06: the black hole's side quest.</summary>
        public void SetSideQuest(string text)
        {
            if (string.IsNullOrEmpty(text)) text = null;
            if (text == sideQuestBody) return; // called every frame with the same line, nothing to lay out again
            ApplyFont();
            sideQuestBody = text;
            PlaceSideQuest();
        }

        /// <summary>The side-quest line right under the goal's backing (in the goal's place while there is no goal), as tall as its text.</summary>
        void PlaceSideQuest()
        {
            if (!sideQuestBack) return;
            bool any = !string.IsNullOrEmpty(sideQuestBody);
            sideQuestBack.gameObject.SetActive(any);
            if (!any) return;
            sideQuestText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(SideQuestColour)}>{ArenaText.Pair("支线：", "SIDE QUEST: ")}</color>{sideQuestBody}";
            var rect = sideQuestBack.rectTransform;
            rect.sizeDelta = new Vector2(ObjectiveWidth, Mathf.Max(SideQuestMinHeight, sideQuestText.preferredHeight + 12f));
            rect.anchoredPosition = new Vector2(ObjectiveLeft, SideQuestTop - goalDrop);
        }

        /// <summary>The side-quest line's top edge at its own place: right under the goal's backing, or in the goal's place without a goal.</summary>
        float SideQuestTop => objectiveBack.gameObject.activeSelf ? ObjectiveTop - objectiveBack.rectTransform.sizeDelta.y - SideQuestGap : ObjectiveTop;

        /// <summary>
        /// The goal and the side-quest line under it step down out of the way of the HUD's banner (V2Hud.bannerLabel: a day's or the
        /// afternoon's two lines at the top centre, about 2 s), which is wide enough to reach the top-left goal and lay over it (v2-P52);
        /// once the banner is gone they come back up. Placed every frame the campaign's HUD shows.
        /// </summary>
        void PlaceGoalPanels(float scale)
        {
            float wanted = 0f;
            float panels = PanelsHeight();
            if (panels > 0f && BannerRect(scale) is Rect banner)
            {
                // The panels at their own place, in the banner's frame (canvas units from the bottom-left).
                float top = root.rect.height + ObjectiveTop, bottom = top - panels;
                if (banner.xMin < ObjectiveLeft + ObjectiveWidth && banner.xMax > ObjectiveLeft && banner.yMin < top && banner.yMax > bottom)
                    wanted = top - banner.yMin + BannerGap;
            }
            if (Mathf.Approximately(goalDrop, wanted)) return;
            goalDrop = Mathf.MoveTowards(goalDrop, wanted, GoalStepSpeed * Time.unscaledDeltaTime);
            objectiveBack.rectTransform.anchoredPosition = new Vector2(ObjectiveLeft, ObjectiveTop - goalDrop);
            objectiveText.rectTransform.anchoredPosition = new Vector2(ObjectiveLeft + 16f, ObjectiveTop - 8f - goalDrop);
            if (sideQuestBack.gameObject.activeSelf) sideQuestBack.rectTransform.anchoredPosition = new Vector2(ObjectiveLeft, SideQuestTop - goalDrop);
        }

        /// <summary>How far down from ObjectiveTop the goal panels shown reach (0 while neither shows).</summary>
        float PanelsHeight()
        {
            float reach = 0f;
            if (objectiveBack.gameObject.activeSelf) reach = objectiveBack.rectTransform.sizeDelta.y;
            if (sideQuestBack.gameObject.activeSelf) reach = Mathf.Max(reach, ObjectiveTop - SideQuestTop + sideQuestBack.rectTransform.sizeDelta.y);
            return reach;
        }

        /// <summary>The HUD banner's text on screen, in this canvas's units from the bottom-left (scale: canvas units per screen pixel);
        /// null while it shows nothing. The width is the text's own where it is narrower than its box, placed by the box's alignment; the
        /// height is the text's where it overflows its box.</summary>
        Rect? BannerRect(float scale)
        {
            var label = gm && gm.hud && gm.hud.v2Hud ? gm.hud.v2Hud.bannerLabel : null;
            if (!label || !label.isActiveAndEnabled || string.IsNullOrEmpty(label.text)) return null;
            var rect = label.rectTransform;
            rect.GetWorldCorners(panelCorners);
            float toCanvas = rect.lossyScale.x * scale; // the label's units to this canvas's
            Rect box = Rect.MinMaxRect(panelCorners[0].x * scale, panelCorners[0].y * scale, panelCorners[2].x * scale, panelCorners[2].y * scale);
            float width = Mathf.Min(box.width, label.preferredWidth * toCanvas), height = Mathf.Max(box.height, label.preferredHeight * toCanvas);
            int column = (int)label.alignment % 3, row = (int)label.alignment / 3; // TextAnchor: Upper, Middle, Lower rows of Left, Center, Right
            float xMin = column == 0 ? box.xMin : column == 1 ? box.center.x - width * .5f : box.xMax - width;
            float yMax = row == 0 ? box.yMax : row == 1 ? box.center.y + height * .5f : box.yMin + height;
            return new Rect(xMin, yMax - height, width, height);
        }

        /// <summary>The base bar sits under the HUD's wave lines at the top centre, its top just below V2Hud's batch line ("N alive · M
        /// pending"), which it used to cover by half (v2-P52). The line's box is read at every refresh, so the bar follows the HUD's layout.</summary>
        void PlaceBaseBar()
        {
            float top = BaseBarTop;
            var batch = gm && gm.hud && gm.hud.v2Hud ? gm.hud.v2Hud.batchLabel : null;
            if (batch)
            {
                float scale = root.lossyScale.y > 0f ? 1f / root.lossyScale.y : 1f;
                batch.rectTransform.GetWorldCorners(panelCorners);
                top = Mathf.Max(top, root.rect.height - panelCorners[0].y * scale + BaseBarGap); // its bottom edge, from the screen's top
            }
            var at = baseBar.anchoredPosition;
            if (!Mathf.Approximately(at.y, -top)) baseBar.anchoredPosition = new Vector2(at.x, -top);
        }

        /// <summary>A yes / no question over the run: a title, the question, OK and Cancel (a click on OK answers yes, Esc or Cancel no;
        /// Enter does nothing here, only the weapon card reads Space and Enter, see SourceKeysTest). While it shows the
        /// run holds still (the GameManager's Story phase: no clock, no combat, no pause key) and no click or key reaches the game; once
        /// answered the run's phase comes back first, then onYes or onNo runs. A second question replaces an open one, which counts as no.</summary>
        public void Confirm(string title, string body, Action onYes, Action onNo = null)
        {
            ApplyFont();
            if (ConfirmOpen) CloseConfirm(false);
            confirmTitle.text = title ?? "";
            confirmBody.text = body ?? "";
            confirmYesLabel.text = ArenaText.Pair("确定", "OK");
            confirmNoLabel.text = ArenaText.Pair("取消", "Cancel");
            confirmHint.text = ArenaText.Pair("点击确定 · Esc 取消", "Click OK · Esc: Cancel");
            confirmYes = onYes; confirmNo = onNo;
            confirmOpenedAt = Time.unscaledTime;
            // Only a running world is held (a question over the base panel or a frozen screen leaves that phase as it is).
            confirmHoldsRun = gm && gm.State == GameState.Playing && (gm.CanPlayerMove || gm.BuildTimeFlows);
            if (confirmHoldsRun) { confirmReturnPhase = gm.RunPhase; gm.CampaignSetPhase(ArenaUiState.Story); }
            confirmScreen.gameObject.SetActive(true);
            confirmScreen.SetAsLastSibling();
        }

        /// <summary>A yes / no question is showing (the campaign reads no key meanwhile).</summary>
        public bool ConfirmOpen => confirmScreen && confirmScreen.gameObject.activeSelf;

        void CloseConfirm(bool yes)
        {
            if (!ConfirmOpen) return;
            confirmScreen.gameObject.SetActive(false);
            var answer = yes ? confirmYes : confirmNo;
            confirmYes = confirmNo = null;
            if (confirmHoldsRun)
            {
                confirmHoldsRun = false;
                // The run's phase comes back unless something else took over meanwhile (the run ended).
                if (gm && gm.State == GameState.Playing && gm.RunPhase == ArenaUiState.Story) gm.CampaignSetPhase(confirmReturnPhase);
            }
            V2Audio.Instance?.PlayUI(yes ? ArenaUiSound.Confirm : ArenaUiSound.Cancel);
            answer?.Invoke();
        }

        /// <summary>For automated checks: answers the open question as a click on OK or Cancel would.</summary>
        public void DebugAnswerConfirm(bool yes) => CloseConfirm(yes);

        void TickConfirm()
        {
            var keys = Keyboard.current;
            // The key or click that asked the question does not answer it.
            if (keys == null || Time.unscaledTime - confirmOpenedAt < .2f) return;
            if (keys.escapeKey.wasPressedThisFrame) CloseConfirm(false);
        }

        public void SetObjective(string text, Vector3? point) { objectiveBody = text; marker = point; WriteObjective(null); }

        /// <summary>The goal's second line: which way on screen and how far (while a marker shows). The panel's text is put together in
        /// ComposeGoal (V2CampaignUi.Feedback.cs).</summary>
        void WriteObjective(string way)
        {
            objectiveWay = way;
            ComposeGoal();
        }

        /// <summary>A screen direction in words (WASD follows the screen): right, up-right, up and so on.</summary>
        static string ScreenWay(Vector2 d)
        {
            int sector = (int)(Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 22.5f, 360f) / 45f);
            return sector switch
            {
                0 => ArenaText.Pair("右方", "right"), 1 => ArenaText.Pair("右上方", "up-right"), 2 => ArenaText.Pair("上方", "up"), 3 => ArenaText.Pair("左上方", "up-left"),
                4 => ArenaText.Pair("左方", "left"), 5 => ArenaText.Pair("左下方", "down-left"), 6 => ArenaText.Pair("下方", "down"), _ => ArenaText.Pair("右下方", "down-right")
            };
        }

        public void WhiteOut(float seconds) { whiteOutClock = 0f; whiteOutLength = Mathf.Max(.1f, seconds); }

        public void SkipCg() => CgSkipped = true;
        public bool BaseOpen => baseScreen && baseScreen.gameObject.activeSelf;
        public void BeginCg() { CgSkipped = false; cgScreen.gameObject.SetActive(true); cgSkip.text = ArenaText.Pair("点击或按 Esc 跳过", "Click or Esc to skip"); }
        public void EndCg()
        {
            cgScreen.gameObject.SetActive(false);
            cgFrontTexture = null;
            cgForeground.texture = null;
        }
        public void CgShot(Texture picture, float seconds, int shot) => CgShot(picture, null, seconds, shot);

        /// <summary>One shot of a CG: a background and an optional transparent foreground drawn over it with a little parallax, both sized
        /// to the picture's own shape (16:9 for the CG01 illustrations).</summary>
        public void CgShot(Texture picture, Texture front, float seconds, int shot)
        {
            cgTexture = picture; cgFrontTexture = picture ? front : null; cgShot = shot; cgClock = 0f; cgLength = Mathf.Max(.1f, seconds);
            cgBlocksShot = -1; // the whitebox's blocks are laid out again for this shot (V2CampaignUi.Whitebox.cs)
            float aspect = picture && picture.height > 0 ? Mathf.Clamp(picture.width / (float)picture.height, 1.2f, 1.9f) : 1.5f;
            var size = new Vector2(720f * aspect, 720f);
            cgPicture.rectTransform.sizeDelta = size;
            cgForeground.rectTransform.sizeDelta = size;
            cgPicture.texture = picture;
            cgPicture.color = picture ? Color.white : new Color(0f, 0f, 0f, 0f);
            cgForeground.texture = cgFrontTexture;
            cgForeground.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>The title's Start opens the V2 campaign. Since 2026-11-06 there is no button for the classic 15-wave run (it stays
        /// in the project, not on the title); the tests still start it with ArenaUiCommand.Start.</summary>
        public void RouteTitleStart()
        {
            var v2 = gm && gm.hud ? gm.hud.v2Hud : null;
            if (!v2 || v2.screens == null) return;
            foreach (var screen in v2.screens)
            {
                if (!screen || screen.state != ArenaUiState.Title) continue;
                var start = screen.transform.Find("Content/StartRun");
                if (!start || !start.TryGetComponent<ArenaUiButton>(out var button)) return;
                button.command = ArenaUiCommand.StartCampaign;
                return;
            }
        }

        // ───── Frame ─────

        void Update()
        {
            ApplyFont();
            // An open question takes Esc for itself: Esc then neither leaves the base nor skips a CG.
            if (ConfirmOpen) TickConfirm();
            else if (StudyOpen) TickStudy(); // the study window (V2CampaignUi.Study.cs) takes the keys, Esc included, while it is open
            else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (cgScreen.gameObject.activeSelf) CgSkipped = true;
                else if (baseScreen.gameObject.activeSelf && campaign.Stage == CampaignStage.Base && !campaign.BaseAtDawn && campaign.NightTimeLeft(out _)) campaign.LeaveBase();
            }
            campaign.ShowShipView(BaseOpen && tab == CampaignRoom.Ship);
            float now = Time.unscaledTime;
            if (titleCard.gameObject.activeSelf)
            {
                float left = titleUntil - now;
                float a = Mathf.Clamp01(Mathf.Min(1f - left / titleLength + .6f, left / .8f));
                titleText.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                titleSub.color = new Color(Faint.r, Faint.g, Faint.b, Mathf.Clamp01(a));
                if (left <= 0f) titleCard.gameObject.SetActive(false);
            }
            // A weapon card (a weapon just bought at the base, GameManager.CampaignShowWeaponCard) fills the middle of the screen: a line
            // said meanwhile waits under it instead of covering the card's lower half (v2-P52), and shows again once the card is closed
            // if its time is not up.
            bool subtitleShown = now <= subtitleUntil && !(gm && gm.RunPhase == ArenaUiState.WeaponIntro);
            if (subtitleRoot.gameObject.activeSelf != subtitleShown) subtitleRoot.gameObject.SetActive(subtitleShown);
            // Over the base panel the subtitle stands in the strip the panel keeps free for it, above its ways out.
            subtitleRoot.anchoredPosition = new Vector2(0f, baseScreen.gameObject.activeSelf ? BaseSubtitleBottom : SubtitleBottom);
            flashBack.gameObject.SetActive(now < flashUntil && !string.IsNullOrEmpty(flashText.text));
            if (whiteOutLength > 0f)
            {
                whiteOutClock += Time.unscaledDeltaTime;
                float t = whiteOutClock / whiteOutLength;
                whiteOut.color = new Color(1f, 1f, 1f, t < 1f ? t : Mathf.Clamp01(2f - t));
                if (t >= 2f) whiteOutLength = 0f;
            }
            if (cgScreen.gameObject.activeSelf) TickCg();
        }

        void TickCg()
        {
            cgClock += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(cgClock / cgLength);
            // A slow push-in that drifts a different way on each shot (Ken Burns), fading in and out.
            float zoom = Mathf.Lerp(.94f, .82f, t);
            Vector2 from = cgShot % 2 == 0 ? new Vector2(.2f, .5f) : new Vector2(.8f, .3f);
            Vector2 to = cgShot % 2 == 0 ? new Vector2(.7f, .4f) : new Vector2(.3f, .6f);
            Vector2 d = Vector2.Lerp(from, to, t);
            cgPicture.uvRect = new Rect((1f - zoom) * d.x, (1f - zoom) * d.y, zoom, zoom);
            float fade = Mathf.Clamp01(Mathf.Min(cgClock / .6f, (cgLength - cgClock) / .6f));
            // Fading darkens both layers toward the black screen instead of making them see-through: a half-transparent foreground
            // would let the background show through the figure as a ghost.
            var tint = new Color(fade, .93f * fade, .82f * fade, 1f);
            if (cgTexture) cgPicture.color = tint;
            if (cgFrontTexture)
            {
                // The foreground starts lined up with its background and pushes in faster: a touch of depth between the layers.
                float zoomFront = Mathf.Lerp(.935f, .76f, t);
                cgForeground.uvRect = new Rect((1f - zoomFront) * d.x, (1f - zoomFront) * d.y, zoomFront, zoomFront);
                cgForeground.color = tint;
            }
            // Whitebox (2026-11-10): flat blocks in place of the pictures, with the same push-in and fade.
            TickWhiteboxCg(t, d, fade);
        }

        /// <summary>The campaign calls this each frame: what the HUD shows follows the stage.</summary>
        public void Refresh()
        {
            if (!campaign || !gm) return;
            var stage = campaign.Stage;
            bool paused = gm.RunPhase == ArenaUiState.Paused;
            bool showHud = !paused && stage is CampaignStage.Tutorial or CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk or CampaignStage.Night or CampaignStage.FinalBoss or CampaignStage.Core
                && gm.RunPhase is not (ArenaUiState.LevelUp or ArenaUiState.WeaponIntro or ArenaUiState.Result or ArenaUiState.Draft) // Draft: the greater void reward after the hidden boss
                && !CrateOpen; // a crate's panel covers the middle of the screen: the goal would show beside it
            hud.gameObject.SetActive(showHud);
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .1f; RefreshTexts(); }
            if (showHud)
            {
                PlaceGoalPanels(root.lossyScale.x > 0f ? 1f / root.lossyScale.x : 1f);
                PlaceMarker();
                ComposeGoal(); // the prompt of this frame, and a note whose time is up
            }
            TickFeedback(showHud);
            if (baseScreen.gameObject.activeSelf && Time.unscaledTime >= panelRefreshAt) { panelRefreshAt = Time.unscaledTime + .25f; RefreshBasePanel(false); }
        }
        float panelRefreshAt;

        void RefreshTexts()
        {
            var c = campaign;
            // The three currencies' icons: the art's, or the whitebox's shapes with the art off (V2CampaignUi.Whitebox.cs).
            bool icons = true;
            for (int i = 0; i < currencyIcons.Length; i++)
            {
                var sprite = CurrencySprite(i);
                icons &= (bool)sprite;
                if (sprite && currencyIcons[i].sprite != sprite) currencyIcons[i].sprite = sprite;
            }
            foreach (var icon in currencyIcons) icon.enabled = icons;
            currencyLabels[0].text = $"{c.Energy}"; currencyLabels[1].text = $"{c.Alloy}"; currencyLabels[2].text = $"{c.Dark}";
            foreach (var label in currencyLabels) label.enabled = icons;
            currencyText.text = icons ? "" : ArenaText.Pair($"能量 {c.Energy}　合金 {c.Alloy}　暗物质 {c.Dark}", $"Energy {c.Energy}   Alloy {c.Alloy}   Dark {c.Dark}");
            baseText.text = ArenaText.Pair($"基地 {Mathf.CeilToInt(c.BaseHp)}/{Mathf.CeilToInt(c.BaseMaxHp)}", $"BASE {Mathf.CeilToInt(c.BaseHp)}/{Mathf.CeilToInt(c.BaseMaxHp)}")
                + (c.ShieldLevel > 0 ? ArenaText.Pair($" · 护盾 {Mathf.CeilToInt(c.BaseShield)}", $" · SHIELD {Mathf.CeilToInt(c.BaseShield)}") : "")
                + ArenaText.Pair($" · 飞船 引擎{(c.EngineFixed ? "✓" : "×")} 装甲{(c.ArmorFixed ? "✓" : "×")} 燃料{c.Fuel}/{c.FuelUnits}", $" · SHIP E{(c.EngineFixed ? "✓" : "×")} A{(c.ArmorFixed ? "✓" : "×")} F{c.Fuel}/{c.FuelUnits}");
            SetFill(baseHpFill, c.BaseHp / Mathf.Max(1f, c.BaseMaxHp));
            SetFill(baseShieldFill, c.BaseShield / Mathf.Max(1f, c.BaseMaxHp));
            for (int side = 0; side < 4; side++)
            {
                var room = c.Room(V2Campaign.SideRoom[side]);
                sidePips[side].color = room.damaged ? Warn : room.level <= 0 ? new Color(.3f, .3f, .32f) : V2Campaign.RoomColour(V2Campaign.SideRoom[side]);
            }
            promptBody = c.PromptText ?? "";
            RefreshNote();
            RefreshLampCounter();
            baseBar.gameObject.SetActive(c.Stage is not (CampaignStage.FinalBoss or CampaignStage.Core or CampaignStage.Tutorial));
            PlaceBaseBar();
        }

        void PlaceMarker()
        {
            bool inTrial = WorldMap.Active && WorldMap.Active.TrialActive;
            bool show = marker.HasValue && gm.cam && gm.player && !inTrial;
            markerIcon.gameObject.SetActive(show); markerText.gameObject.SetActive(show); markerPulse.gameObject.SetActive(show);
            if (!show) { markerArrow.gameObject.SetActive(false); if (inTrial && marker.HasValue) WriteObjective(null); return; }
            Vector3 world = marker.Value + Vector3.up * 1.5f;
            Vector3 sp = gm.cam.WorldToScreenPoint(world);
            var rect = root.rect;
            float scale = root.lossyScale.x > 0f ? 1f / root.lossyScale.x : 1f;
            Vector2 p = new(sp.x * scale, sp.y * scale);
            if (sp.z < 0f) p = new Vector2(rect.width - p.x, rect.height - p.y);
            // Kept clear of the weapon and skill bar at the bottom and the day and base bars at the top (since 2026-11-10 the base bar sits
            // lower, under the HUD's batch line: the marker stays a little below wherever it is).
            float highest = rect.height * .8f;
            if (baseBar.gameObject.activeInHierarchy) highest = Mathf.Min(highest, rect.height + baseBar.anchoredPosition.y - baseBar.sizeDelta.y - 24f);
            Vector2 clamped = new(Mathf.Clamp(p.x, 60f, rect.width - 60f), Mathf.Clamp(p.y, rect.height * .19f, Mathf.Max(rect.height * .19f, highest)));
            // The goal panels fill the top-left corner: a marker that would sit on them (a goal off screen up and to the left, or one
            // under them) moves down below them, so it covers neither their text nor its own distance; its arrow still points at the goal.
            if (PanelsCorner(scale) is Vector2 corner && clamped.x < corner.x + 30f && clamped.y > corner.y - 50f) clamped.y = corner.y - 50f;
            // Off screen: the arrow sits at the edge pointing out toward the goal, the marker just inside it.
            bool offScreen = (clamped - p).sqrMagnitude > 1f;
            Vector3 playerPoint = gm.cam.WorldToScreenPoint(gm.player.Position);
            Vector2 from = new(playerPoint.x * scale, playerPoint.y * scale);
            Vector2 dir = (p - from).sqrMagnitude > 1f ? (p - from).normalized : Vector2.up;
            Vector2 icon = clamped;
            markerArrow.gameObject.SetActive(offScreen);
            if (offScreen)
            {
                Vector2 edge = (p - new Vector2(rect.width * .5f, rect.height * .5f)).normalized;
                markerArrow.anchoredPosition = clamped;
                markerArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(edge.y, edge.x) * Mathf.Rad2Deg);
                icon = clamped - edge * 46f;
            }
            markerIcon.rectTransform.anchoredPosition = icon;
            markerPulse.rectTransform.anchoredPosition = icon;
            markerText.rectTransform.anchoredPosition = icon + new Vector2(0f, -38f);
            float distance = Query.Flat(marker.Value - gm.player.Position).magnitude;
            markerText.text = $"{distance:0} m";
            float wave = Mathf.Sin(Time.unscaledTime * 6f), ring = Mathf.Repeat(Time.unscaledTime * 1.1f, 1f);
            markerIcon.color = new Color(Accent.r, Accent.g, Accent.b, .85f + .15f * wave);
            markerIcon.rectTransform.localScale = Vector3.one * (1f + .12f * wave);
            markerPulse.rectTransform.localScale = Vector3.one * (1f + 1.8f * ring);
            markerPulse.color = new Color(Accent.r, Accent.g, Accent.b, .7f * (1f - ring));
            WriteObjective(distance < campaign.MarkerReach(marker.Value) ? ArenaText.Pair("就在这里", "right here")
                : ArenaText.Pair($"{ScreenWay(dir)} · {distance:0} 米", $"{distance:0} m · {ScreenWay(dir)}"));
        }

        static readonly Vector3[] panelCorners = new Vector3[4];

        /// <summary>How far the goal panels shown in the top-left corner (the goal, and the side quest under it) reach, in the marker's frame
        /// (canvas units from the screen's bottom-left): x is their right edge, y their bottom edge; none while neither shows.</summary>
        Vector2? PanelsCorner(float scale)
        {
            Vector2? reach = null;
            Reach(objectiveBack, ref reach, scale);
            Reach(sideQuestBack, ref reach, scale);
            return reach;
        }

        static void Reach(Image panel, ref Vector2? reach, float scale)
        {
            if (!panel || !panel.gameObject.activeInHierarchy) return;
            panel.rectTransform.GetWorldCorners(panelCorners);
            var r = new Vector2(panelCorners[2].x * scale, panelCorners[0].y * scale);
            reach = reach is Vector2 v ? new Vector2(Mathf.Max(v.x, r.x), Mathf.Min(v.y, r.y)) : r;
        }

        // ───── Small builders ─────

        void ApplyFont()
        {
            var f = ArenaText.Font;
            if (!f || f == font) return;
            font = f;
            foreach (var t in GetComponentsInChildren<Text>(true)) t.font = font;
        }

        RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color colour)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = rect.GetComponent<Image>();
            image.color = colour; image.raycastTarget = false;
            return rect;
        }

        Image Icon(RectTransform parent, Vector2 anchor, Vector2 position, float size)
        {
            var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = anchor;
            image.rectTransform.pivot = new Vector2(0f, .5f);
            image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = new Vector2(size, size);
            image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        Image Fill(RectTransform track, Color colour)
        {
            var image = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(track, false);
            image.rectTransform.anchorMin = new Vector2(0f, 0f); image.rectTransform.anchorMax = new Vector2(1f, 1f);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.color = colour; image.raycastTarget = false;
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal;
            image.sprite = WhiteSprite();
            return image;
        }

        static Sprite white;
        static Sprite WhiteSprite()
        {
            if (white) return white;
            var texture = Texture2D.whiteTexture;
            white = Sprite.Create(texture, new UnityEngine.Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            return white;
        }

        static void SetFill(Image fill, float value) { if (fill) fill.fillAmount = Mathf.Clamp01(value); }

        Text Label(string name, Transform parent, string text, int size, Color colour, TextAnchor anchor)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            label.font = font; label.fontSize = size; label.color = colour; label.alignment = anchor; label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true; label.raycastTarget = false;
            return label;
        }

        static void Shadow(Text label)
        {
            var shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .85f); shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>A text that stands on the game world with no plate behind it: a dark outline and a soft drop shadow keep it readable
        /// on light ground (the whitebox's floor) as on dark.</summary>
        static void Edge(Text label)
        {
            var outline = label.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, .92f); outline.effectDistance = new Vector2(2f, -2f);
            var shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .7f); shadow.effectDistance = new Vector2(0f, -4f);
        }

        static void Outline(GameObject go)
        {
            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = PanelEdge; outline.effectDistance = new Vector2(2f, -2f);
        }

        Button MakeButton(RectTransform parent, string text, Vector2 position, Vector2 size, Action onClick, bool primary)
        {
            var rect = Box("Button", parent, new Vector2(.5f, .5f), position, size, new Vector2(.5f, .5f), primary ? new Color(.12f, .32f, .5f, 1f) : new Color(.1f, .15f, .22f, 1f));
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Outline(rect.gameObject);
            var button = rect.gameObject.AddComponent<Button>();
            var colours = button.colors;
            colours.highlightedColor = new Color(1.25f, 1.25f, 1.3f); colours.pressedColor = new Color(.8f, .8f, .85f); colours.disabledColor = new Color(.55f, .55f, .6f, .7f);
            button.colors = colours;
            button.onClick.AddListener(() => onClick());
            var label = Label("Label", rect, text, 17, Color.white, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 10f, 4f, 10f, 4f);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 11; label.resizeTextMaxSize = 18;
            return button;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        static void Place(RectTransform rect, Vector2 position) => rect.anchoredPosition = position;

        static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        static void StretchFull(RectTransform rect) => Stretch(rect, 0f, 0f, 0f, 0f);
    }
}
