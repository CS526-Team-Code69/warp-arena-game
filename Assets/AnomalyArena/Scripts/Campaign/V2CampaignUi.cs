using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{

    public sealed class V2CampaignUi : MonoBehaviour
    {
        V2Campaign campaign;
        GameManager gm;
        Font font;
        Canvas canvas;
        RectTransform root, hud, baseScreen, titleCard, cgScreen, baseBar;
        Image cgVignette;
        Text currencyText, baseText, objectiveText, promptText, flashText, subtitleSpeaker, subtitleLine, titleText, titleSub, cgSkip, markerText;
        Image baseHpFill, baseShieldFill, whiteOut, markerIcon, subtitleBack, flashBack;

        Image markerPulse, objectiveBack; RectTransform markerArrow; string objectiveBody;

        Image sideQuestBack; Text sideQuestText; string sideQuestBody;
        RectTransform confirmScreen; Text confirmTitle, confirmBody, confirmHint, confirmYesLabel, confirmNoLabel;
        Action confirmYes, confirmNo; float confirmOpenedAt; bool confirmHoldsRun; ArenaUiState confirmReturnPhase;
        readonly Image[] sidePips = new Image[4];
        Image[] currencyIcons;
        Text[] currencyLabels;
        RawImage cgPicture;
        float flashUntil, subtitleUntil, titleUntil, titleLength, whiteOutClock, whiteOutLength, refreshAt;
        Vector3? marker;

        CampaignRoom tab = CampaignRoom.Control;
        Text baseHeading, baseCurrencies, roomTitle, roomBody;
        RawImage roomPicture;
        RectTransform tabColumn, actionColumn, leaveRow;
        readonly List<GameObject> actionButtons = new();
        int panelVersion = -1;

        int cgShot; float cgClock, cgLength; Texture cgTexture, cgFrontTexture;
        RawImage cgForeground;
        public bool CgSkipped { get; private set; }

        static readonly Color Panel = new(.04f, .07f, .12f, .94f), PanelEdge = new(.3f, .55f, .85f, .55f), Soft = new(.86f, .9f, .96f), Faint = new(.62f, .72f, .85f);
        static readonly Color Accent = new(.45f, .85f, 1f), Warn = new(1f, .45f, .3f), Good = new(.5f, 1f, .6f), SystemColour = new(.5f, .9f, 1f), PlayerColour = new(1f, .86f, .55f);
        static readonly Color SideQuestColour = new(.78f, .55f, 1f);

        const float ObjectiveTop = -202f, ObjectiveMinHeight = 78f, SideQuestGap = 8f, SideQuestMinHeight = 48f;

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

            Place(sidePips[0].rectTransform, new Vector2(-228f, 40f)); Place(sidePips[2].rectTransform, new Vector2(-228f, 14f));
            Place(sidePips[3].rectTransform, new Vector2(-241f, 27f)); Place(sidePips[1].rectTransform, new Vector2(-215f, 27f));

            objectiveBack = Box("ObjectiveBack", hud, new Vector2(0f, 1f), new Vector2(14f, ObjectiveTop), new Vector2(620f, ObjectiveMinHeight), new Vector2(0f, 1f), new Color(0f, .02f, .06f, .78f)).GetComponent<Image>();
            EdgeBar(objectiveBack.rectTransform, Accent);
            objectiveBack.gameObject.SetActive(false);
            objectiveText = Label("Objective", hud, "", 24, Color.white, TextAnchor.UpperLeft);
            objectiveText.fontStyle = FontStyle.Bold;
            Anchor(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, ObjectiveTop - 8f), new Vector2(596f, 66f), new Vector2(0f, 1f));
            Shadow(objectiveText);

            sideQuestBack = Box("SideQuestBack", hud, new Vector2(0f, 1f), new Vector2(14f, ObjectiveTop - ObjectiveMinHeight - SideQuestGap), new Vector2(620f, SideQuestMinHeight), new Vector2(0f, 1f), new Color(0f, .02f, .06f, .78f)).GetComponent<Image>();
            EdgeBar(sideQuestBack.rectTransform, SideQuestColour);
            sideQuestText = Label("SideQuest", sideQuestBack.rectTransform, "", 22, Color.white, TextAnchor.MiddleLeft);
            sideQuestText.fontStyle = FontStyle.Bold;
            Stretch(sideQuestText.rectTransform, 16f, 6f, 8f, 6f);
            Shadow(sideQuestText);
            sideQuestBack.gameObject.SetActive(false);

            promptText = Label("Prompt", hud, "", 20, Color.white, TextAnchor.MiddleCenter);
            Anchor(promptText.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 236f), new Vector2(900f, 36f), new Vector2(.5f, .5f));
            Shadow(promptText);

            markerPulse = Box("MarkerPulse", hud, new Vector2(0f, 0f), Vector2.zero, new Vector2(36f, 36f), new Vector2(.5f, .5f), Accent).GetComponent<Image>();
            markerPulse.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            markerIcon = Box("Marker", hud, new Vector2(0f, 0f), Vector2.zero, new Vector2(36f, 36f), new Vector2(.5f, .5f), Accent).GetComponent<Image>();
            markerIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Box("MarkerCore", markerIcon.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(14f, 14f), new Vector2(.5f, .5f), Color.white);

            markerArrow = MakeRect("MarkerArrow", hud, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40f, 40f));
            for (int i = 0; i < 2; i++)
                Box("ArrowBar" + i, markerArrow, new Vector2(.5f, .5f), new Vector2(14f, 0f), new Vector2(30f, 9f), new Vector2(1f, .5f), Accent).localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -40f : 40f);
            markerArrow.gameObject.SetActive(false);
            markerText = Label("MarkerText", hud, "", 20, Color.white, TextAnchor.MiddleCenter);
            markerText.fontStyle = FontStyle.Bold;

            markerText.rectTransform.anchorMin = markerText.rectTransform.anchorMax = Vector2.zero;
            markerText.rectTransform.sizeDelta = new Vector2(160f, 28f);
            Shadow(markerText);

            subtitleBack = Box("Subtitle", root, new Vector2(.5f, 0f), new Vector2(0f, 318f), new Vector2(980f, 70f), new Vector2(.5f, .5f), new Color(0f, 0f, 0f, .55f)).GetComponent<Image>();
            subtitleSpeaker = Label("Speaker", subtitleBack.rectTransform, "", 15, SystemColour, TextAnchor.UpperCenter);
            Stretch(subtitleSpeaker.rectTransform, 12f, 4f, 12f, 46f);
            subtitleLine = Label("Line", subtitleBack.rectTransform, "", 21, Color.white, TextAnchor.MiddleCenter);
            Stretch(subtitleLine.rectTransform, 16f, 22f, 16f, 4f);
            subtitleBack.gameObject.SetActive(false);

            flashBack = Box("Flash", root, new Vector2(.5f, .5f), new Vector2(0f, 150f), new Vector2(1100f, 64f), new Vector2(.5f, .5f), new Color(0f, 0f, 0f, .7f)).GetComponent<Image>();
            flashText = Label("Text", flashBack.rectTransform, "", 22, Color.white, TextAnchor.MiddleCenter);
            Stretch(flashText.rectTransform, 18f, 4f, 18f, 4f);
            Shadow(flashText);
            flashBack.gameObject.SetActive(false);

            BuildBasePanel();

            flashBack.transform.SetAsLastSibling();

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

            cgVignette = new GameObject("Vignette", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            cgVignette.rectTransform.SetParent(cgScreen, false);
            Stretch(cgVignette.rectTransform, 0f, 0f, 0f, 0f);
            cgVignette.sprite = V2Hud.EdgeVignette();
            cgVignette.color = new Color(0f, 0f, 0f, .92f);
            cgVignette.raycastTarget = false;
            cgSkip = Label("Skip", cgScreen, "", 15, Faint, TextAnchor.LowerRight);
            Stretch(cgSkip.rectTransform, 0f, 0f, 24f, 16f);
            cgScreen.GetComponent<Image>().raycastTarget = true;                                            
            var skip = cgScreen.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(() => CgSkipped = true);
            cgScreen.gameObject.SetActive(false);

            subtitleBack.transform.SetAsLastSibling();

            whiteOut = Box("WhiteOut", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
            StretchFull(whiteOut.rectTransform);
            whiteOut.raycastTarget = false;
            BuildConfirm();
            HideAll();
        }

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

        void EdgeBar(RectTransform backing, Color colour)
        {
            var bar = Box("Bar", backing, new Vector2(0f, .5f), Vector2.zero, new Vector2(6f, 0f), new Vector2(0f, .5f), colour);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f);
            bar.sizeDelta = new Vector2(6f, 0f); bar.anchoredPosition = Vector2.zero;
        }

        public void HideAll()
        {
            if (hud) hud.gameObject.SetActive(false);
            if (baseScreen) baseScreen.gameObject.SetActive(false);
            if (titleCard) titleCard.gameObject.SetActive(false);
            if (cgScreen) cgScreen.gameObject.SetActive(false);
            if (subtitleBack) subtitleBack.gameObject.SetActive(false);
            if (flashBack) flashBack.gameObject.SetActive(false);
            flashUntil = 0f;

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
            subtitleUntil = Time.unscaledTime + seconds;
            subtitleBack.gameObject.SetActive(true);
        }

        public void ClearSubtitle() { subtitleUntil = 0f; if (subtitleBack) subtitleBack.gameObject.SetActive(false); }

        public void Flash(string text, float seconds = 2.4f)
        {
            ApplyFont();
            flashText.text = text;

            flashBack.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(flashText.preferredWidth + 48f, 320f, 1100f), 64f);
            flashUntil = Time.unscaledTime + seconds;
            flashBack.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
        public void ClearFlash() => flashUntil = 0f;

        public void SetSideQuest(string text)
        {
            if (string.IsNullOrEmpty(text)) text = null;
            if (text == sideQuestBody) return;                                                                   
            ApplyFont();
            sideQuestBody = text;
            PlaceSideQuest();
        }

        void PlaceSideQuest()
        {
            if (!sideQuestBack) return;
            bool any = !string.IsNullOrEmpty(sideQuestBody);
            sideQuestBack.gameObject.SetActive(any);
            if (!any) return;
            sideQuestText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(SideQuestColour)}>{ArenaText.Pair("SIDE QUEST: ","SIDE QUEST: ")}</color>{sideQuestBody}";
            var rect = sideQuestBack.rectTransform;
            rect.sizeDelta = new Vector2(620f, Mathf.Max(SideQuestMinHeight, sideQuestText.preferredHeight + 12f));
            float top = objectiveBack.gameObject.activeSelf ? ObjectiveTop - objectiveBack.rectTransform.sizeDelta.y - SideQuestGap : ObjectiveTop;
            rect.anchoredPosition = new Vector2(14f, top);
        }

        public void Confirm(string title, string body, Action onYes, Action onNo = null)
        {
            ApplyFont();
            if (ConfirmOpen) CloseConfirm(false);
            confirmTitle.text = title ?? "";
            confirmBody.text = body ?? "";
            confirmYesLabel.text = ArenaText.Pair("OK","OK");
            confirmNoLabel.text = ArenaText.Pair("Cancel","Cancel");
            confirmHint.text = ArenaText.Pair("Click OK · Esc: Cancel","Click OK · Esc: Cancel");
            confirmYes = onYes; confirmNo = onNo;
            confirmOpenedAt = Time.unscaledTime;

            confirmHoldsRun = gm && gm.State == GameState.Playing && (gm.CanPlayerMove || gm.BuildTimeFlows);
            if (confirmHoldsRun) { confirmReturnPhase = gm.RunPhase; gm.CampaignSetPhase(ArenaUiState.Story); }
            confirmScreen.gameObject.SetActive(true);
            confirmScreen.SetAsLastSibling();
        }

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

                if (gm && gm.State == GameState.Playing && gm.RunPhase == ArenaUiState.Story) gm.CampaignSetPhase(confirmReturnPhase);
            }
            V2Audio.Instance?.PlayUI(yes ? ArenaUiSound.Confirm : ArenaUiSound.Cancel);
            answer?.Invoke();
        }

        void TickConfirm()
        {
            var keys = Keyboard.current;

            if (keys == null || Time.unscaledTime - confirmOpenedAt < .2f) return;
            if (keys.escapeKey.wasPressedThisFrame) CloseConfirm(false);
        }

        public void SetObjective(string text, Vector3? point) { objectiveBody = text; marker = point; WriteObjective(null); }

        void WriteObjective(string way)
        {
            bool any = !string.IsNullOrEmpty(objectiveBody);
            objectiveBack.gameObject.SetActive(any);
            objectiveText.text = !any ? "" : $"<color=#{ColorUtility.ToHtmlStringRGB(Accent)}>{ArenaText.Pair("GOAL: ","GOAL: ")}</color>{objectiveBody}" + (string.IsNullOrEmpty(way) ? "" : "\n" + way);
            if (any) objectiveBack.rectTransform.sizeDelta = new Vector2(620f, Mathf.Max(ObjectiveMinHeight, objectiveText.preferredHeight + 16f));
            PlaceSideQuest();
        }

        static string ScreenWay(Vector2 d)
        {
            int sector = (int)(Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 22.5f, 360f) / 45f);
            return sector switch
            {
                0 => ArenaText.Pair("right","right"), 1 => ArenaText.Pair("up-right","up-right"), 2 => ArenaText.Pair("up","up"), 3 => ArenaText.Pair("up-left","up-left"),
                4 => ArenaText.Pair("left","left"), 5 => ArenaText.Pair("down-left","down-left"), 6 => ArenaText.Pair("down","down"), _ => ArenaText.Pair("down-right","down-right")
            };
        }

        public void WhiteOut(float seconds) { whiteOutClock = 0f; whiteOutLength = Mathf.Max(.1f, seconds); }

        public void SkipCg() => CgSkipped = true;
        public bool BaseOpen => baseScreen && baseScreen.gameObject.activeSelf;
        public void BeginCg() { CgSkipped = false; cgScreen.gameObject.SetActive(true); cgSkip.text = ArenaText.Pair("Click or Esc to skip","Click or Esc to skip"); }
        public void EndCg()
        {
            cgScreen.gameObject.SetActive(false);
            cgFrontTexture = null;
            cgForeground.texture = null;
        }
        public void CgShot(Texture picture, float seconds, int shot) => CgShot(picture, null, seconds, shot);

        public void CgShot(Texture picture, Texture front, float seconds, int shot)
        {
            cgTexture = picture; cgFrontTexture = picture ? front : null; cgShot = shot; cgClock = 0f; cgLength = Mathf.Max(.1f, seconds);
            float aspect = picture && picture.height > 0 ? Mathf.Clamp(picture.width / (float)picture.height, 1.2f, 1.9f) : 1.5f;
            var size = new Vector2(720f * aspect, 720f);
            cgPicture.rectTransform.sizeDelta = size;
            cgForeground.rectTransform.sizeDelta = size;
            cgPicture.texture = picture;
            cgPicture.color = picture ? Color.white : new Color(0f, 0f, 0f, 0f);
            cgForeground.texture = cgFrontTexture;
            cgForeground.color = new Color(1f, 1f, 1f, 0f);
        }

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

        void Update()
        {
            ApplyFont();

            if (ConfirmOpen) TickConfirm();
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
            if (subtitleBack.gameObject.activeSelf && now > subtitleUntil) subtitleBack.gameObject.SetActive(false);

            subtitleBack.rectTransform.anchoredPosition = new Vector2(0f, baseScreen.gameObject.activeSelf ? 38f : 318f);
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

            float zoom = Mathf.Lerp(.94f, .82f, t);
            Vector2 from = cgShot % 2 == 0 ? new Vector2(.2f, .5f) : new Vector2(.8f, .3f);
            Vector2 to = cgShot % 2 == 0 ? new Vector2(.7f, .4f) : new Vector2(.3f, .6f);
            Vector2 d = Vector2.Lerp(from, to, t);
            cgPicture.uvRect = new Rect((1f - zoom) * d.x, (1f - zoom) * d.y, zoom, zoom);
            float fade = Mathf.Clamp01(Mathf.Min(cgClock / .6f, (cgLength - cgClock) / .6f));

            var tint = new Color(fade, .93f * fade, .82f * fade, 1f);
            if (cgTexture) cgPicture.color = tint;
            if (cgFrontTexture)
            {

                float zoomFront = Mathf.Lerp(.935f, .76f, t);
                cgForeground.uvRect = new Rect((1f - zoomFront) * d.x, (1f - zoomFront) * d.y, zoomFront, zoomFront);
                cgForeground.color = tint;
            }
        }

        public void Refresh()
        {
            if (!campaign || !gm) return;
            var stage = campaign.Stage;
            bool paused = gm.RunPhase == ArenaUiState.Paused;
            bool showHud = !paused && stage is CampaignStage.Tutorial or CampaignStage.Day or CampaignStage.Lull or CampaignStage.Dusk or CampaignStage.Night or CampaignStage.FinalBoss or CampaignStage.Core
                && gm.RunPhase is not (ArenaUiState.LevelUp or ArenaUiState.WeaponIntro or ArenaUiState.Result or ArenaUiState.Draft);                                                        
            hud.gameObject.SetActive(showHud);
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + .1f; RefreshTexts(); }
            if (showHud) PlaceMarker();
            if (baseScreen.gameObject.activeSelf && Time.unscaledTime >= panelRefreshAt) { panelRefreshAt = Time.unscaledTime + .25f; RefreshBasePanel(false); }
        }
        float panelRefreshAt;

        void RefreshTexts()
        {
            var c = campaign;
            var art = c.art;
            if (art)
            {
                if (currencyIcons[0].sprite != art.energy) { currencyIcons[0].sprite = art.energy; currencyIcons[1].sprite = art.alloy; currencyIcons[2].sprite = art.dark; }
            }
            bool icons = art && art.energy && art.alloy && art.dark;
            foreach (var icon in currencyIcons) icon.enabled = icons;
            currencyLabels[0].text = $"{c.Energy}"; currencyLabels[1].text = $"{c.Alloy}"; currencyLabels[2].text = $"{c.Dark}";
            foreach (var label in currencyLabels) label.enabled = icons;
            currencyText.text = icons ? "" : ArenaText.Pair($"Energy {c.Energy}   Alloy {c.Alloy}   Dark {c.Dark}",$"Energy {c.Energy}   Alloy {c.Alloy}   Dark {c.Dark}");
            baseText.text = ArenaText.Pair($"BASE {Mathf.CeilToInt(c.BaseHp)}/{Mathf.CeilToInt(c.BaseMaxHp)}",$"BASE {Mathf.CeilToInt(c.BaseHp)}/{Mathf.CeilToInt(c.BaseMaxHp)}")
                + (c.ShieldLevel > 0 ? ArenaText.Pair($" · SHIELD {Mathf.CeilToInt(c.BaseShield)}",$" · SHIELD {Mathf.CeilToInt(c.BaseShield)}") : "")
                + ArenaText.Pair($" · SHIP E{(c.EngineFixed ? "✓" : "✗")} A{(c.ArmorFixed ? "✓" : "✗")} F{c.Fuel}/{c.rules.fuelUnits}",$" · SHIP E{(c.EngineFixed ? "✓" : "✗")} A{(c.ArmorFixed ? "✓" : "✗")} F{c.Fuel}/{c.rules.fuelUnits}");
            SetFill(baseHpFill, c.BaseHp / Mathf.Max(1f, c.BaseMaxHp));
            SetFill(baseShieldFill, c.BaseShield / Mathf.Max(1f, c.BaseMaxHp));
            for (int side = 0; side < 4; side++)
            {
                var room = c.Room(V2Campaign.SideRoom[side]);
                sidePips[side].color = room.damaged ? Warn : room.level <= 0 ? new Color(.3f, .3f, .32f) : V2Campaign.RoomColour(V2Campaign.SideRoom[side]);
            }
            promptText.text = c.PromptText;
            baseBar.gameObject.SetActive(c.Stage is not (CampaignStage.FinalBoss or CampaignStage.Core or CampaignStage.Tutorial));
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

            Vector2 clamped = new(Mathf.Clamp(p.x, 60f, rect.width - 60f), Mathf.Clamp(p.y, rect.height * .19f, rect.height * .8f));

            if (PanelsCorner(scale) is Vector2 corner && clamped.x < corner.x + 30f && clamped.y > corner.y - 50f) clamped.y = corner.y - 50f;

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
            WriteObjective(distance < campaign.MarkerReach(marker.Value) ? ArenaText.Pair("right here","right here")
                : ArenaText.Pair($"{distance:0} m to the {ScreenWay(dir)} (follow the blue marker)",$"{distance:0} m to the {ScreenWay(dir)} (follow the blue marker)"));
        }

        static readonly Vector3[] panelCorners = new Vector3[4];

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

        void BuildBasePanel()
        {
            baseScreen = Box("BasePanel", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, .01f, .03f, .78f));
            StretchFull(baseScreen);
            var panel = Box("Panel", baseScreen, new Vector2(.5f, .5f), new Vector2(0f, 30f), new Vector2(1300f, 760f), new Vector2(.5f, .5f), Panel);
            Outline(panel.gameObject);
            baseHeading = Label("Heading", panel, "", 30, Color.white, TextAnchor.UpperLeft);
            Anchor(baseHeading.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -22f), new Vector2(760f, 44f), new Vector2(0f, 1f));
            baseCurrencies = Label("Currencies", panel, "", 19, Soft, TextAnchor.UpperRight);
            Anchor(baseCurrencies.rectTransform, new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(560f, 40f), new Vector2(1f, 1f));
            tabColumn = MakeRect("Tabs", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -84f), new Vector2(230f, 560f));
            tabColumn.pivot = new Vector2(0f, 1f);
            roomPicture = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            roomPicture.rectTransform.SetParent(panel, false);
            Anchor(roomPicture.rectTransform, new Vector2(0f, 1f), new Vector2(284f, -84f), new Vector2(560f, 315f), new Vector2(0f, 1f));
            roomTitle = Label("RoomTitle", panel, "", 24, Accent, TextAnchor.UpperLeft);
            Anchor(roomTitle.rectTransform, new Vector2(0f, 1f), new Vector2(284f, -410f), new Vector2(560f, 36f), new Vector2(0f, 1f));
            roomBody = Label("RoomBody", panel, "", 17, Soft, TextAnchor.UpperLeft);
            Anchor(roomBody.rectTransform, new Vector2(0f, 1f), new Vector2(284f, -450f), new Vector2(560f, 215f), new Vector2(0f, 1f));
            roomBody.supportRichText = true;
            actionColumn = MakeRect("Actions", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -84f), new Vector2(380f, 600f));
            actionColumn.pivot = new Vector2(1f, 1f);
            leaveRow = MakeRect("Leave", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 24f), new Vector2(0f, 64f));
            leaveRow.pivot = new Vector2(.5f, 0f);
            baseScreen.gameObject.SetActive(false);
        }

        public void ShowBase()
        {
            ApplyFont();
            panelVersion = -1;
            tab = CampaignRoom.Control;
            baseScreen.gameObject.SetActive(true);
            RefreshBasePanel(true);
        }

        public void HideBase() { baseScreen.gameObject.SetActive(false); }

        public void ResumeBase()
        {
            ApplyFont();
            panelVersion = -1;
            baseScreen.gameObject.SetActive(true);
            RefreshBasePanel(true);
        }

        public void ShowTab(CampaignRoom room)
        {
            if (!BaseOpen) return;
            tab = room;
            RefreshBasePanel(true);
        }

        void Act(Func<bool> action, string failure)
        {
            if (!action() && !string.IsNullOrEmpty(failure)) Flash(failure);
            RefreshBasePanel(true);
        }

        void RefreshBasePanel(bool rebuild)
        {
            var c = campaign;
            var r = c.rules;
            baseHeading.text = c.BaseAfterTutorial ? ArenaText.Pair("BASE · FIRST VISIT","BASE · FIRST VISIT")
                : c.BaseAtDawn ? ArenaText.Pair($"BASE · DAWN AFTER NIGHT {c.Day}",$"BASE · DAWN AFTER NIGHT {c.Day}")
                : c.BaseFromDusk ? ArenaText.Pair($"BASE · DAY {c.Day}, AFTERNOON",$"BASE · DAY {c.Day}, AFTERNOON")
                : ArenaText.Pair($"BASE · NIGHT {c.Day}",$"BASE · NIGHT {c.Day}");
            baseCurrencies.text = ArenaText.Pair($"Alien energy {c.Energy}   Alien alloy {c.Alloy}   Dark matter {c.Dark}",$"Alien energy {c.Energy}   Alien alloy {c.Alloy}   Dark matter {c.Dark}");
            int version = Hash(c);
            if (!rebuild && version == panelVersion) return;
            panelVersion = version;

            foreach (Transform child in tabColumn) Destroy(child.gameObject);
            for (int i = 0; i < 4; i++)
            {
                var room = (CampaignRoom)i;
                var state = c.Room(room);
                string level = room == CampaignRoom.Control ? ArenaText.Pair($"Hull {c.Durability} · Shield {c.ShieldLevel}",$"Hull {c.Durability} · Shield {c.ShieldLevel}")
                    : state.level <= 0 ? ArenaText.Pair("not built","not built") : ArenaText.Pair($"level {state.level} / 3",$"level {state.level} / 3");
                string text = $"{V2Campaign.RoomName(room)}\n<size=14>{level}{(state.damaged ? ArenaText.Pair(" · <color=#ff7a5a>BROKEN</color>"," · <color=#ff7a5a>BROKEN</color>") : "")}</size>";
                var b = MakeButton(tabColumn, text, new Vector2(0f, -i * 86f), new Vector2(230f, 76f), () => { tab = room; RefreshBasePanel(true); }, room == tab);
                ((RectTransform)b.transform).anchorMin = ((RectTransform)b.transform).anchorMax = new Vector2(0f, 1f);
                ((RectTransform)b.transform).pivot = new Vector2(0f, 1f);
                var sprite = RoomIcon(room);
                if (sprite)
                {
                    var icon = Icon((RectTransform)b.transform, new Vector2(0f, .5f), new Vector2(8f, 0f), 60f);
                    icon.sprite = sprite;
                    var label = b.GetComponentInChildren<Text>();
                    Stretch(label.rectTransform, 74f, 4f, 8f, 4f);
                    label.alignment = TextAnchor.MiddleLeft;
                }
            }

            roomPicture.texture = Picture(tab);
            roomPicture.color = roomPicture.texture ? Color.white : new Color(.12f, .16f, .22f, 1f);
            var current = c.Room(tab);
            roomTitle.text = V2Campaign.RoomName(tab) + (current.damaged ? ArenaText.Pair(" (BROKEN)"," (BROKEN)") : "");
            roomBody.text = RoomText(tab);

            foreach (var go in actionButtons) Destroy(go);
            actionButtons.Clear();
            var actions = new List<(string label, Func<bool> act, string fail, bool enabled)>();
            if (current.damaged)
                actions.Add((ArenaText.Pair($"Repair{(tab == CampaignRoom.Control ? " (by hand)" : " (by central control)")} · {c.RepairCost(tab)} alloy",$"Repair{(tab == CampaignRoom.Control ? " (by hand)" : " (by central control)")} · {c.RepairCost(tab)} alloy"),
                    () => c.TryRepairRoom(tab), tab != CampaignRoom.Control && !c.ControlWorks ? ArenaText.Pair("Repair Central Control first","Repair Central Control first") : ArenaText.Pair("Not enough alloy","Not enough alloy"), true));
            switch (tab)
            {
                case CampaignRoom.Control:
                    if (c.DurabilityCost is { } dc) actions.Add((ArenaText.Pair($"Hull to level {c.Durability + 1} (base HP {Mathf.RoundToInt(r.BaseMaxHp(c.Durability + 1))}) · {CostText(dc)}",$"Hull to level {c.Durability + 1} (base HP {Mathf.RoundToInt(r.BaseMaxHp(c.Durability + 1))}) · {CostText(dc)}"), c.TryUpgradeDurability, Why(dc), c.ControlWorks));
                    if (c.ShieldCost is { } sc) actions.Add((c.ShieldLevel == 0 ? ArenaText.Pair($"Unlock the base shield · {CostText(sc)}",$"Unlock the base shield · {CostText(sc)}") : ArenaText.Pair($"Shield to level {c.ShieldLevel + 1} · {CostText(sc)}",$"Shield to level {c.ShieldLevel + 1} · {CostText(sc)}"), c.TryUpgradeShield, Why(sc), c.ControlWorks));
                    if (c.BaseHp < c.BaseMaxHp - .5f) actions.Add((ArenaText.Pair($"Repair the hull (to full) · {c.HullRepairCost} alloy",$"Repair the hull (to full) · {c.HullRepairCost} alloy"), c.TryRepairHull, ArenaText.Pair("Not enough alloy, or Central Control is broken","Not enough alloy, or Central Control is broken"), c.ControlWorks));
                    break;
                case CampaignRoom.Ship:
                    if (c.NextLevelCost(tab) is { } shipCost) actions.Add((UpgradeLabel(tab, shipCost), () => c.TryUpgradeRoom(tab), UpgradeFail(tab, shipCost), c.ControlWorks && !current.damaged));
                    if (!c.EngineFixed) actions.Add((ArenaText.Pair($"Repair the engine · {CostText(r.engineRepair)}",$"Repair the engine · {CostText(r.engineRepair)}"), c.TryRepairEngine, current.level < 1 ? ArenaText.Pair("Needs Ship Launch Bay level 1","Needs Ship Launch Bay level 1") : Why(r.engineRepair), c.CanRepairEngine));
                    if (!c.ArmorFixed) actions.Add((ArenaText.Pair($"Repair the armor · {CostText(r.armorRepair)}",$"Repair the armor · {CostText(r.armorRepair)}"), c.TryRepairArmor, current.level < 2 ? ArenaText.Pair("Needs Ship Launch Bay level 2","Needs Ship Launch Bay level 2") : Why(r.armorRepair), c.CanRepairArmor));
                    if (!c.FuelFull) actions.Add((ArenaText.Pair($"Synthesize antimatter fuel +1 ({c.Fuel}/{r.fuelUnits}) · {r.darkPerFuelUnit} dark matter",$"Synthesize antimatter fuel +1 ({c.Fuel}/{r.fuelUnits}) · {r.darkPerFuelUnit} dark matter"), c.TrySynthesiseFuel, current.level < 3 ? ArenaText.Pair("Needs Ship Launch Bay level 3","Needs Ship Launch Bay level 3") : ArenaText.Pair("Not enough dark matter","Not enough dark matter"), c.CanSynthesiseFuel));
                    actions.Add((ArenaText.Pair($"Craft a mining lamp ({c.LampStock} held) · {r.lampCost} alloy",$"Craft a mining lamp ({c.LampStock} held) · {r.lampCost} alloy"), c.TryCraftLamp, current.damaged ? ArenaText.Pair("The Ship Launch Bay is broken","The Ship Launch Bay is broken") : ArenaText.Pair("Not enough alloy","Not enough alloy"), c.LampsCraftable));
                    if (c.CanIgnite) actions.Add((ArenaText.Pair("Start the ignition sequence","Start the ignition sequence"), () => { c.Ignite(); return true; }, "", true));
                    break;
                default:
                    if (c.NextLevelCost(tab) is { } cost) actions.Add((UpgradeLabel(tab, cost), () => c.TryUpgradeRoom(tab), UpgradeFail(tab, cost), c.ControlWorks && !current.damaged));

                    string broken = ArenaText.Pair("This room is broken: repair it first","This room is broken: repair it first");
                    string Short(CampaignCost price) => current.damaged ? broken : c.CanAfford(price) ? null : Why(price);
                    if (tab == CampaignRoom.Armament)
                    {

                        if (current.level >= 1 && c.PlayerLevelCost is { } lc)
                            actions.Add((ArenaText.Pair(                                    $"Armor upgrade to level {gm.progression.Level + 1} (max HP +{Mathf.RoundToInt(gm.progression.Rules.maxHpPerLevel)}, max energy +{Mathf.RoundToInt(gm.progression.Rules.maxEnergyPerLevel)}) · {CostText(lc)}",                                    $"Armor upgrade to level {gm.progression.Level + 1} (max HP +{Mathf.RoundToInt(gm.progression.Rules.maxHpPerLevel)}, max energy +{Mathf.RoundToInt(gm.progression.Rules.maxEnergyPerLevel)}) · {CostText(lc)}"),
                                c.TryBuyPlayerLevel, current.damaged ? broken : Why(lc), !current.damaged));
                        if (current.level >= 1)
                        {

                            var boostPrice = c.WeaponDamageCost;
                            bool boostable = c.HoldsAnomalyWeapon;
                            actions.Add((boostable ? ArenaText.Pair($"Boost weapon damage: a higher Mk, the weapon in hand first · {CostText(boostPrice)}",$"Boost weapon damage: a higher Mk, the weapon in hand first · {CostText(boostPrice)}")
                                    : ArenaText.Pair("Boost weapon damage: the pistol cannot be boosted; unlock an anomaly weapon first","Boost weapon damage: the pistol cannot be boosted; unlock an anomaly weapon first"),
                                c.TryBuyWeaponDamage, Short(boostPrice), !current.damaged && boostable));
                        }

                        string anomaly = ArenaText.Pair("\nAn anomaly weapon: how it attacks and its trait show once bought","\nAn anomaly weapon: how it attacks and its trait show once bought");
                        if (current.level >= 2)
                        {
                            var weaponPrice = c.NewWeaponCost;
                            bool full = c.WeaponSlotsFull;
                            actions.Add((full ? ArenaText.Pair("Research a new weapon: every slot is taken","Research a new weapon: every slot is taken")
                                    : (c.NewWeaponReplacesPistol ? ArenaText.Pair($"Research a new weapon (a hand weapon replaces the pistol) · {CostText(weaponPrice)}",$"Research a new weapon (a hand weapon replaces the pistol) · {CostText(weaponPrice)}")
                                        : ArenaText.Pair($"Research a new weapon for a free slot · {CostText(weaponPrice)}",$"Research a new weapon for a free slot · {CostText(weaponPrice)}")) + anomaly,
                                c.TryBuyNewWeapon, Short(weaponPrice), !current.damaged && !full));
                        }
                        if (current.level >= 2 && c.RocketForSale)
                        {
                            var rocketPrice = c.RocketCost;
                            var gone = c.RocketReplaces;
                            actions.Add((gone != null
                                    ? (gone.form == WeaponForm.Pistol
                                        ? ArenaText.Pair($"Unlock the rocket launcher: it replaces the {gone.FormName} · {CostText(rocketPrice)}",$"Unlock the rocket launcher: it replaces the {gone.FormName} · {CostText(rocketPrice)}")
                                        : ArenaText.Pair($"Unlock the rocket launcher: it replaces the {gone.FormName} not in hand · {CostText(rocketPrice)}",$"Unlock the rocket launcher: it replaces the {gone.FormName} not in hand · {CostText(rocketPrice)}")) + anomaly
                                    : ArenaText.Pair($"Unlock the rocket launcher for the free hand · {CostText(rocketPrice)}",$"Unlock the rocket launcher for the free hand · {CostText(rocketPrice)}") + anomaly,
                                c.TryBuyRocketLauncher, Short(rocketPrice), !current.damaged));
                        }
                    }
                    else if (tab == CampaignRoom.Energy && current.level >= 1)
                        actions.Add(UpgradeAction(CampaignUpgrade.Stat, ArenaText.Pair("one random stat rises","one random stat rises"), current, Short));
                    if (tab == CampaignRoom.Armament && current.level >= 2)
                        actions.Add(UpgradeAction(CampaignUpgrade.Tame, ArenaText.Pair("a weapon gains its Tame","a weapon gains its Tame"), current, Short));
                    if (tab == CampaignRoom.Armament && current.level >= 3)
                        actions.Add(UpgradeAction(CampaignUpgrade.Twist, ArenaText.Pair("a weapon gains its Twist","a weapon gains its Twist"), current, Short));
                    if (tab == CampaignRoom.Energy && current.level >= 2)
                        actions.Add(UpgradeAction(CampaignUpgrade.Skill, ArenaText.Pair("a new skill or a skill level","a new skill or a skill level"), current, Short));
                    if (tab == CampaignRoom.Energy && current.level >= 3)
                        actions.Add(UpgradeAction(CampaignUpgrade.Infusion, ArenaText.Pair("a weapon takes an element","a weapon takes an element"), current, Short));
                    break;
            }
            for (int i = 0; i < actions.Count; i++)
            {
                var (label, act, fail, enabled) = actions[i];
                var b = MakeButton(actionColumn, label, new Vector2(0f, -i * 78f), new Vector2(380f, 68f), () => Act(act, fail), false);
                var rect = (RectTransform)b.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
                b.interactable = enabled;
                actionButtons.Add(b.gameObject);
            }

            foreach (Transform child in leaveRow) Destroy(child.gameObject);
            var leaves = new List<(string label, Action act)>();
            if (c.NightTimeLeft(out float seconds))
            {

                string left = c.EveningsOpenEnded ? "" : ArenaText.Pair($" ({Mathf.CeilToInt(seconds)} s left)",$" ({Mathf.CeilToInt(seconds)} s left)");
                string dawn = c.Day >= r.days ? (c.CanIgnite ? ArenaText.Pair(": start the ignition",": start the ignition") : ArenaText.Pair($": the ship cannot leave yet, start day {c.Day + 1}",$": the ship cannot leave yet, start day {c.Day + 1}"))
                    : ArenaText.Pair($": start day {c.Day + 1}",$": start day {c.Day + 1}");
                if (c.BaseFromDusk)
                {
                    leaves.Add((ArenaText.Pair($"Back to the afternoon{left}",$"Back to the afternoon{left}"), c.LeaveBase));
                    leaves.Add((ArenaText.Pair("Into the night","Into the night"), c.EnterNight));
                    leaves.Add((ArenaText.Pair("Skip the night, wait for dawn","Skip the night, wait for dawn") + dawn, c.WaitForDawn));
                }
                else
                {
                    leaves.Add((ArenaText.Pair($"Back into the night{left}",$"Back into the night{left}"), c.LeaveBase));
                    leaves.Add((ArenaText.Pair("Wait for dawn","Wait for dawn") + dawn, c.WaitForDawn));
                }
            }
            else if (c.Day == 0 && !c.BaseAtDawn) leaves.Add((ArenaText.Pair("Head out (night falls)","Head out (night falls)"), c.LeaveBase));
            else if (c.Day >= r.days && c.CanIgnite) leaves.Add((ArenaText.Pair("Start the ignition sequence","Start the ignition sequence"), c.Ignite));
            else leaves.Add((c.Day >= r.days ? ArenaText.Pair($"Tank not full · start day {c.Day + 1}",$"Tank not full · start day {c.Day + 1}") : ArenaText.Pair($"Head out: start day {c.Day + 1}",$"Head out: start day {c.Day + 1}"), c.LeaveBase));
            for (int i = 0; i < leaves.Count; i++)
            {
                var (label, act) = leaves[i];
                var b = MakeButton(leaveRow, label, new Vector2((i - (leaves.Count - 1) * .5f) * 420f, 0f), new Vector2(400f, 58f), () => act(), true);
                var rect = (RectTransform)b.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f); rect.pivot = new Vector2(.5f, 0f);
            }
        }

        (string label, Func<bool> act, string fail, bool enabled) UpgradeAction(CampaignUpgrade kind, string effect, V2Campaign.RoomState room, Func<CampaignCost, string> shortOf)
        {
            var price = campaign.UpgradeCost(kind);
            string name = V2Campaign.UpgradeName(kind);
            return (ArenaText.Pair($"{name}: {effect} · {CostText(price)}",$"{name}: {effect} · {CostText(price)}"), () => campaign.TryBuyUpgrade(kind), shortOf(price), !room.damaged);
        }

        int Hash(V2Campaign c)
        {
            int h = (int)tab * 7919 + c.Alloy * 31 + c.Energy * 131 + c.Dark * 521 + c.Durability * 3 + c.ShieldLevel * 5 + c.Fuel * 17 + c.LampStock * 23 + (c.EngineFixed ? 1 : 0) + (c.ArmorFixed ? 2 : 0);
            for (int i = 0; i < 4; i++) h = h * 13 + c.Room((CampaignRoom)i).level * 2 + (c.Room((CampaignRoom)i).damaged ? 1 : 0);
            h = h * 7 + Mathf.CeilToInt(c.BaseHp) + (c.NightTimeLeft(out float s) ? (float.IsInfinity(s) ? 1 : Mathf.CeilToInt(s) * 1009) : 0);

            h = h * 3 + (c.HoldsAnomalyWeapon ? 1 : 0) + (c.NewWeaponReplacesPistol ? 2 : 0);
            if (gm && gm.runWeapons) foreach (var w in gm.runWeapons.Hand) h = h * 5 + (w == null ? 0 : 1 + (int)w.form);
            return h;
        }

        string UpgradeLabel(CampaignRoom room, CampaignCost cost)
        {
            int next = campaign.Room(room).level + 1;
            return next == 1 ? ArenaText.Pair($"Build the {V2Campaign.RoomName(room)} · {CostText(cost)}",$"Build the {V2Campaign.RoomName(room)} · {CostText(cost)}")
                : ArenaText.Pair($"Upgrade to level {next} (its new feature shows once built) · {CostText(cost)}",$"Upgrade to level {next} (its new feature shows once built) · {CostText(cost)}");
        }

        string UpgradeFail(CampaignRoom room, CampaignCost cost) => !campaign.ControlWorks ? ArenaText.Pair("Central Control is broken: repair it by hand first","Central Control is broken: repair it by hand first")
            : campaign.Room(room).damaged ? ArenaText.Pair("Repair this room first","Repair this room first") : Why(cost);

        string Why(CampaignCost cost)
        {
            var c = campaign;
            var missing = new List<string>();
            if (c.Alloy < cost.alloy) missing.Add(ArenaText.Pair($"{cost.alloy - c.Alloy} alloy short",$"{cost.alloy - c.Alloy} alloy short"));
            if (c.Energy < cost.energy) missing.Add(ArenaText.Pair($"{cost.energy - c.Energy} energy short",$"{cost.energy - c.Energy} energy short"));
            if (c.Dark < cost.dark) missing.Add(ArenaText.Pair($"{cost.dark - c.Dark} dark matter short",$"{cost.dark - c.Dark} dark matter short"));
            return missing.Count > 0 ? string.Join(ArenaText.Pair(", ",", "), missing) : ArenaText.Pair("Not possible now","Not possible now");
        }

        static string CostText(CampaignCost cost)
        {
            var parts = new List<string>();
            if (cost.alloy > 0) parts.Add(ArenaText.Pair($"{cost.alloy} alloy",$"{cost.alloy} alloy"));
            if (cost.energy > 0) parts.Add(ArenaText.Pair($"{cost.energy} energy",$"{cost.energy} energy"));
            if (cost.dark > 0) parts.Add(ArenaText.Pair($"{cost.dark} dark matter",$"{cost.dark} dark matter"));
            return parts.Count == 0 ? ArenaText.Pair("free","free") : string.Join(" + ", parts);
        }

        Sprite RoomIcon(CampaignRoom room)
        {
            var art = campaign.art;
            if (!art) return null;
            return room switch { CampaignRoom.Control => art.control, CampaignRoom.Armament => art.armament, CampaignRoom.Energy => art.energyLab, _ => art.shipBay };
        }

        Texture Picture(CampaignRoom room)
        {
            if (room == CampaignRoom.Ship && campaign.ShipViewTexture) return campaign.ShipViewTexture;
            var art = campaign.art;
            if (!art || !Art.Enabled) return null;
            int level = campaign.Room(room).level;
            return room switch
            {
                CampaignRoom.Control => art.roomControl,
                CampaignRoom.Armament => LevelPicture(art.roomArmamentLevels, level, art.roomArmament),
                CampaignRoom.Energy => LevelPicture(art.roomEnergyLevels, level, art.roomEnergy),
                _ => campaign.ArmorFixed && art.shipIntactPicture ? art.shipIntactPicture : art.shipWreckPicture
            };
        }

        static Texture LevelPicture(Texture2D[] levels, int level, Texture2D first)
        {
            int i = level - 2;
            return levels != null && i >= 0 && i < levels.Length && levels[i] ? levels[i] : first;
        }

        string RoomText(CampaignRoom room)
        {
            var c = campaign;
            var r = c.rules;
            string Done(bool on) => on ? "<color=#7dff9a>●</color> " : "<color=#58606c>○</color> ";
            string Hidden(int level) => Done(false) + ArenaText.Pair($"Level {level}: ??? (shows at level {level})",$"Level {level}: ??? (shows at level {level})");
            int lv = c.Room(room).level;
            switch (room)
            {
                case CampaignRoom.Control:
                    return string.Join("\n", new[]
                    {
                        ArenaText.Pair($"Base HP {Mathf.CeilToInt(c.BaseHp)} / {Mathf.CeilToInt(c.BaseMaxHp)} (hull level {c.Durability} / {r.MaxDurability})",$"Base HP {Mathf.CeilToInt(c.BaseHp)} / {Mathf.CeilToInt(c.BaseMaxHp)} (hull level {c.Durability} / {r.MaxDurability})"),
                        c.ShieldLevel > 0 ? ArenaText.Pair($"Shield {Mathf.CeilToInt(c.BaseShield)} / {Mathf.CeilToInt(c.BaseMaxShield)} (level {c.ShieldLevel} / {r.MaxShield}, refills each night; by day press E at the door to top it up with energy)",$"Shield {Mathf.CeilToInt(c.BaseShield)} / {Mathf.CeilToInt(c.BaseMaxShield)} (level {c.ShieldLevel} / {r.MaxShield}, refills each night; by day press E at the door to top it up with energy)")
                            : ArenaText.Pair("Shield locked (unlock it with alien energy)","Shield locked (unlock it with alien energy)"),
                        ArenaText.Pair("Central Control builds, upgrades and repairs the other three rooms; when it breaks, repair it by hand first.","Central Control builds, upgrades and repairs the other three rooms; when it breaks, repair it by hand first."),
                        ArenaText.Pair("Each side of the base holds one room: north Ship Launch Bay, east Armament Bay, south (door) Central Control, west Energy Lab. A side hit too hard breaks its room.","Each side of the base holds one room: north Ship Launch Bay, east Armament Bay, south (door) Central Control, west Energy Lab. A side hit too hard breaks its room."),
                        ArenaText.Pair($"Each night the base repairs {r.nightSelfRepair * 100f:0}% of its HP by itself.",$"Each night the base repairs {r.nightSelfRepair * 100f:0}% of its HP by itself.")
                    });
                case CampaignRoom.Armament:
                    return string.Join("\n", new[]
                    {
                        lv >= 1 ? Done(true) + ArenaText.Pair("Level 1: armor upgrades (each adds max HP and max energy); weapon damage boosts (a higher Mk)","Level 1: armor upgrades (each adds max HP and max energy); weapon damage boosts (a higher Mk)") : Hidden(1),
                        lv >= 2 ? Done(true) + ArenaText.Pair("Level 2: research new anomaly weapons, unlock the rocket launcher; tame weapons","Level 2: research new anomaly weapons, unlock the rocket launcher; tame weapons") : Hidden(2),
                        lv >= 3 ? Done(true) + ArenaText.Pair("Level 3: weapon twists","Level 3: weapon twists") : Hidden(3),
                        lv >= 1 ? ArenaText.Pair($"\nArmor level {gm.progression.Level}: max HP {Mathf.RoundToInt(gm.player.maxHp)}, max energy {Mathf.RoundToInt(gm.progression.Stats.maxEnergy)}. Kills drop energy and no XP; what you buy here takes effect at once.",$"\nArmor level {gm.progression.Level}: max HP {Mathf.RoundToInt(gm.player.maxHp)}, max energy {Mathf.RoundToInt(gm.progression.Stats.maxEnergy)}. Kills drop energy and no XP; what you buy here takes effect at once.")
                            : ArenaText.Pair("\nUntil it is built: no armor upgrades and no weapon boosts.","\nUntil it is built: no armor upgrades and no weapon boosts.")
                    })

                        + (lv >= 2 ? "\n" + ArenaText.Pair(                            "Anomaly weapons: every weapon bought here carries an anomaly; how it attacks and its trait show once bought. The starting pistol is no anomaly weapon and cannot be boosted.",                            "Anomaly weapons: every weapon bought here carries an anomaly; how it attacks and its trait show once bought. The starting pistol is no anomaly weapon and cannot be boosted.") : "");
                case CampaignRoom.Energy:
                    return string.Join("\n", new[]
                    {
                        lv >= 1 ? Done(true) + ArenaText.Pair("Level 1: upgrade the body (one random stat rises)","Level 1: upgrade the body (one random stat rises)") : Hidden(1),
                        lv >= 2 ? Done(true) + ArenaText.Pair("Level 2: unlock alien skills (a new skill or a level for one you know)","Level 2: unlock alien skills (a new skill or a level for one you know)") : Hidden(2),
                        lv >= 3 ? Done(true) + ArenaText.Pair("Level 3: weapon infusion (a weapon takes an element)","Level 3: weapon infusion (a weapon takes an element)") : Hidden(3),
                        ArenaText.Pair("\nWhat you buy here takes effect at once, one random result each time.","\nWhat you buy here takes effect at once, one random result each time.")
                    });
                default:
                    return string.Join("\n", new[]
                    {
                        Done(true) + ArenaText.Pair($"Mining lamps: {r.freeLampsPerNight} handed out each night, more at {r.lampCost} alloy each",$"Mining lamps: {r.freeLampsPerNight} handed out each night, more at {r.lampCost} alloy each"),
                        lv >= 1 ? Done(c.EngineFixed) + ArenaText.Pair($"Level 1: repair the ship's engine{(c.EngineFixed ? " (done)" : "")}",$"Level 1: repair the ship's engine{(c.EngineFixed ? " (done)" : "")}") : Hidden(1),
                        lv >= 2 ? Done(c.ArmorFixed) + ArenaText.Pair($"Level 2: repair the ship's armor{(c.ArmorFixed ? " (done)" : "")}",$"Level 2: repair the ship's armor{(c.ArmorFixed ? " (done)" : "")}") : Hidden(2),
                        lv >= 3 ? Done(c.FuelFull) + ArenaText.Pair($"Level 3: synthesize antimatter fuel ({c.Fuel} / {r.fuelUnits}, {r.darkPerFuelUnit} dark matter each)",$"Level 3: synthesize antimatter fuel ({c.Fuel} / {r.fuelUnits}, {r.darkPerFuelUnit} dark matter each)") : Hidden(3),
                        ArenaText.Pair($"\nMain goal: fix the engine and armor and fill the tank to ignite and go home (the ignition is forced after day {r.days}).",$"\nMain goal: fix the engine and armor and fill the tank to ignite and go home (the ignition is forced after day {r.days}).")
                    });
            }
        }

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
