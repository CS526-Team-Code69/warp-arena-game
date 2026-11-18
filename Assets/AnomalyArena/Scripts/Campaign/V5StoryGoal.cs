using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The goal plate in the fifth pass (the mock-up's .goal, top left under the vitals): the old goal panel (ObjectiveBack) restyled
    /// where it stands, its name kept. An orange triangle and "目标 · OBJECTIVE" head it; the goal's first sentence is the title in the
    /// cream paint and the rest stands under it with the way and the distance, the prompt of the thing stood at (orange: act on it) and
    /// the campaign's short note. Without a goal the head reads "提示 · NOTE". The side quest's strip (the old SideQuestBack, moved into the
    /// plate) closes the plate: a violet wash with a violet bar on its left, the void's medal and "支线" in violet before its text. The old
    /// goal text is still written, hidden. The plate steps down under the HUD's banner as the old panel did (PlaceGoalPanels).
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // In mock-up pixels: left 24, top 124, 340 wide, corners cut at 11, padding 12 / 16 / 11 / 18; the head 14 tall (the triangle
        // 11 x 13, 8 to its 12-pixel spaced capitals), the title 7 under it (20 pixels), the body 4 under that (14 pixels). The side
        // quest's strip 11 in from the plate's sides and foot, padding 6 / 10, a 3-pixel bar, a 26-pixel medal 9 before its 14-pixel text.
        const float GoalLeftPx = 24f, GoalTopPx = 124f, GoalWidthPx = 340f, GoalPadTopPx = 12f, GoalPadRightPx = 16f, GoalPadBottomPx = 11f, GoalPadLeftPx = 18f;
        const float GoalHeadPx = 14f, GoalTitleGapPx = 7f, GoalBodyGapPx = 4f, SideInsetPx = 11f, SidePadXPx = 10f, SidePadYPx = 6f, SideMedalPx = 26f;

        Image storyGoalPointer;
        Text storyGoalCap, storyGoalTitle, storyGoalBody;
        V5HudMedal storySideMedal;
        string storyGoal = "", storyWay = "", storyPrompt = "", storyNote = "";
        bool? storyGoalChinese;

        // The plate's call for attention (TickGoalFlash): a new goal, or a new stage of the side quest, flashes the plate three times
        // (orange for a goal, violet for the side quest, whose strip glows too), nudges it larger for a moment and plays the panel's
        // sound. A line whose only change is a number (3/10 to 4/10) and a switch of language do not call for it.
        Image storyGoalFlash;
        string storyGoalKey, storySideKey;
        float goalFlashAt = -10f;
        Color goalFlashColour;
        bool goalFlashSide;
        const float GoalFlashSeconds = 1.6f, GoalFlashPulses = 3f, GoalFlashPeak = .42f, GoalFlashGrow = .05f, GoalFlashGrowSeconds = .3f;
        const float GoalCutPx = 11f; // the plate's corner cut, which its flash keeps

        /// <summary>The goal panel's left edge, top edge (as its anchored position, from the screen's top) and width: the fifth pass's
        /// plate, or the old panel's.</summary>
        float GoalLeft => V5HudKit.On ? Sp(GoalLeftPx) : ObjectiveLeft;

        float GoalTop => V5HudKit.On ? -Sp(GoalTopPx) : ObjectiveTop;

        float GoalWidth => V5HudKit.On ? Sp(GoalWidthPx) : ObjectiveWidth;

        void BuildStoryGoal()
        {
            var back = objectiveBack.rectTransform;
            objectiveBack.enabled = false;
            var bar = back.Find("Bar");
            if (bar) bar.gameObject.SetActive(false);
            var plate = V5HudKit.Plate(back, "Plate", V5HudKit.PlateLook.Plain, GoalCutPx, V5HudKit.StoryCss);
            plate.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(plate.Rect);
            storyGoalFlash = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            storyGoalFlash.transform.SetParent(back, false);
            storyGoalFlash.transform.SetSiblingIndex(1); // over the plate, under the words
            V5TitleKit.Stretch(storyGoalFlash.rectTransform);
            storyGoalFlash.raycastTarget = false;
            storyGoalFlash.color = Color.clear;
            // A triangle that points up, turned to point right: 13 across its base, 11 deep.
            storyGoalPointer = V5HudKit.Arrow(back, "Pointer", new Vector2(Sp(13f), Sp(11f)), V5TitleKit.Orange, -90f);
            storyGoalCap = V5StoryKit.Label(back, "Cap", 12f, V5TitleKit.OrangeHi, TextAnchor.MiddleLeft, true);
            storyGoalTitle = V5StoryKit.Label(back, "Title", 20f, Color.white, TextAnchor.UpperLeft, true);
            StoryWrap(storyGoalTitle, .9f);
            V5TitleKit.Painted(storyGoalTitle);
            storyGoalBody = V5StoryKit.Label(back, "Body", 14f, V5TitleKit.Ink2, TextAnchor.UpperLeft);
            StoryWrap(storyGoalBody, 1f);
            objectiveText.enabled = false;

            // The side quest's strip inside the plate, at its foot.
            var side = sideQuestBack.rectTransform;
            side.SetParent(back, false);
            sideQuestBack.color = V5StoryKit.VioletWash;
            if (side.Find("Bar") is RectTransform edge)
            {
                edge.sizeDelta = new Vector2(Sp(3f), 0f);
                if (edge.TryGetComponent<Image>(out var line)) line.color = V5HudKit.Violet;
            }
            storySideMedal = V5HudKit.Medal(side, "Medal", Sp(SideMedalPx));
            PlaceCentre(storySideMedal.Rect, new Vector2(0f, .5f), new Vector2(Sp(SidePadXPx + SideMedalPx * .5f), 0f), Vector2.one * Sp(SideMedalPx));
            sideQuestText.fontSize = V5StoryKit.FontSize(14f);
            sideQuestText.fontStyle = FontStyle.Bold;
            sideQuestText.color = V5TitleKit.Ink;
            sideQuestText.alignment = TextAnchor.MiddleLeft;
            Stretch(sideQuestText.rectTransform, Sp(SidePadXPx + SideMedalPx + 9f), Sp(SidePadYPx), Sp(SidePadXPx), Sp(SidePadYPx));
            back.anchoredPosition = new Vector2(GoalLeft, GoalTop);
        }

        static void StoryWrap(Text label, float spacing)
        {
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = spacing;
        }

        /// <summary>The void's icon (the void element's), on the side quest's medal: the art set's with the art on, the whitebox's otherwise.</summary>
        Sprite StoryVoidIcon
        {
            get
            {
                var set = gm && gm.hud && gm.hud.v2Hud ? gm.hud.v2Hud.art : null;
                int i = (int)ElementKind.Void - 1;
                return set && set.elementIcons != null && i < set.elementIcons.Length ? set.elementIcons[i] : null;
            }
        }

        /// <summary>Lays the plate out from the goal's parts (ComposeGoal keeps them) and the side quest's text: the head, the title and the
        /// body while there is a goal, a prompt or a note; the strip while there is a side quest; hidden with neither.</summary>
        void LayOutStoryGoal()
        {
            if (!storyGoalTitle) return;
            bool sameLanguage = storyGoalChinese == ArenaText.IsChinese;
            storyGoalChinese = ArenaText.IsChinese;
            var back = objectiveBack.rectTransform;
            SplitWords(storyGoal, true, out string title, out string rest);
            var body = new StringBuilder();
            void Add(string text, Color? colour)
            {
                if (string.IsNullOrEmpty(text)) return;
                if (body.Length > 0) body.Append('\n');
                if (colour is Color c) body.Append("<b><color=#").Append(HexOf(c)).Append('>').Append(text).Append("</color></b>");
                else body.Append(text);
            }
            Add(rest, null);
            if (title.Length > 0) Add(storyWay, null);
            Add(storyPrompt, V5TitleKit.OrangeHi);
            Add(storyNote, null);
            bool main = title.Length > 0 || body.Length > 0, side = !string.IsNullOrEmpty(sideQuestBody);
            string goalKey = WordsOf(storyGoal), sideKey = side ? WordsOf(sideQuestBody) : "";
            if (sameLanguage && goalKey.Length > 0 && goalKey != storyGoalKey) StartGoalFlash(V5TitleKit.OrangeHi, false);
            else if (sameLanguage && sideKey.Length > 0 && sideKey != storySideKey) StartGoalFlash(V5HudKit.VioletHi, true);
            storyGoalKey = goalKey;
            storySideKey = sideKey;
            float width = Sp(GoalWidthPx), left = Sp(GoalPadLeftPx), inner = width - Sp(GoalPadLeftPx + GoalPadRightPx), y = 0f;
            SetShown(storyGoalPointer, main);
            SetShown(storyGoalCap, main);
            SetShown(storyGoalTitle, main && title.Length > 0);
            SetShown(storyGoalBody, main && body.Length > 0);
            if (main)
            {
                y = Sp(GoalPadTopPx);
                string cap = title.Length > 0 ? ArenaText.Pair("目标 · OBJECTIVE", "OBJECTIVE") : ArenaText.Pair("提示 · NOTE", "NOTE");
                StoryText(storyGoalCap, V5TitleKit.Spaced(cap));
                V5TitleKit.Place(storyGoalCap.rectTransform, left + Sp(11f + 8f), y, inner - Sp(19f), Sp(GoalHeadPx));
                PlaceCentre(storyGoalPointer.rectTransform, new Vector2(0f, 1f), new Vector2(left + Sp(5.5f), -(y + Sp(GoalHeadPx * .5f))), new Vector2(Sp(13f), Sp(11f)));
                y += Sp(GoalHeadPx);
                if (title.Length > 0)
                {
                    y += Sp(GoalTitleGapPx);
                    y += StoryBlock(storyGoalTitle, title, left, y, inner);
                }
                if (body.Length > 0)
                {
                    y += Sp(title.Length > 0 ? GoalBodyGapPx : GoalTitleGapPx);
                    y += StoryBlock(storyGoalBody, body.ToString(), left, y, inner);
                }
                y += Sp(GoalPadBottomPx);
            }
            sideQuestBack.gameObject.SetActive(side);
            if (side)
            {
                if (!main) y = Sp(SideInsetPx);
                sideQuestText.text = $"<color=#{HexOf(V5HudKit.VioletHi)}>{ArenaText.Pair("支线", "Side quest")}</color>  {sideQuestBody}";
                storySideMedal.SetIcons(StoryVoidIcon, WhiteboxIcons.Element((int)ElementKind.Void - 1));
                var strip = sideQuestBack.rectTransform;
                float stripWidth = width - Sp(SideInsetPx * 2f);
                V5TitleKit.Place(strip, Sp(SideInsetPx), y, stripWidth, Sp(SideMedalPx + SidePadYPx * 2f));
                float height = Mathf.Max(Sp(SideMedalPx + SidePadYPx * 2f), sideQuestText.preferredHeight + Sp(SidePadYPx * 2f));
                strip.sizeDelta = new Vector2(stripWidth, height);
                y += height + Sp(SideInsetPx);
            }
            bool any = main || side;
            objectiveBack.gameObject.SetActive(any);
            if (!any) return;
            back.sizeDelta = new Vector2(width, y);
            back.anchoredPosition = new Vector2(GoalLeft, GoalTop - goalDrop);
        }

        /// <summary>A line without its digits, so a count that only moves (3/10, then 4/10) reads as the same line.</summary>
        static string WordsOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var words = new StringBuilder(text.Length);
            foreach (char c in text)
                if (!char.IsDigit(c)) words.Append(c);
            return words.ToString();
        }

        void StartGoalFlash(Color colour, bool side)
        {
            goalFlashAt = Time.unscaledTime;
            goalFlashColour = colour;
            goalFlashSide = side;
            // A white shape of the plate's outline (the cut-corner mask with the art on, a flat rectangle in whitebox), tinted by the
            // flash's colour: the plate's own picture is dark, and a dark picture stays dark whatever colour it is tinted.
            bool art = Art.Enabled;
            V5TitleKit.Dress(storyGoalFlash, art ? V5TitleKit.Chamfer : null, Color.clear, art ? V5HudKit.ChamferDensity(GoalCutPx, V5HudKit.StoryCss) : 0f);
            V5UiSound.Play(V5UiSound.Ui.Panel);
        }

        /// <summary>Each frame (Update): the flash's three pulses, each weaker than the last, the plate's nudge at the start and, for the
        /// side quest, its strip's glow; everything back as it was once the flash is over.</summary>
        void TickGoalFlash()
        {
            if (!storyGoalFlash) return;
            var back = objectiveBack.rectTransform;
            float t = Time.unscaledTime - goalFlashAt;
            if (t < 0f || t > GoalFlashSeconds)
            {
                if (storyGoalFlash.color.a <= 0f) return;
                storyGoalFlash.color = Color.clear;
                back.localScale = Vector3.one;
                if (goalFlashSide) sideQuestBack.color = V5StoryKit.VioletWash;
                return;
            }
            float pulse = Mathf.Sin(Mathf.PI * GoalFlashPulses * t / GoalFlashSeconds);
            float a = GoalFlashPeak * pulse * pulse * (.45f + .55f * (1f - t / GoalFlashSeconds));
            storyGoalFlash.color = new Color(goalFlashColour.r, goalFlashColour.g, goalFlashColour.b, Mathf.Max(.001f, a));
            float grow = t < GoalFlashGrowSeconds ? GoalFlashGrow * Mathf.Sin(Mathf.PI * t / GoalFlashGrowSeconds) : 0f;
            back.localScale = Vector3.one * (1f + grow);
            if (goalFlashSide) sideQuestBack.color = Color.Lerp(V5StoryKit.VioletWash, V5HudKit.VioletHi, a);
        }

        /// <summary>A wrapped label from (left, top) of the plate, as wide as given and as tall as its text; its height.</summary>
        static float StoryBlock(Text label, string text, float left, float top, float width)
        {
            StoryText(label, text);
            V5TitleKit.Place(label.rectTransform, left, top, width, 10f);
            float height = Mathf.Ceil(label.preferredHeight);
            label.rectTransform.sizeDelta = new Vector2(width, height);
            return height;
        }
    }
}
