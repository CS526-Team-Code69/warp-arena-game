using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The story layer in the fifth interface pass (work/art-plans/style-1112/ui-e/hud.html), built only when V5HudKit.On: the English
    /// Alpha keeps the screens it was graded with. Its parts are in the V5Story* files: the day plate at the top centre (V5StoryDay), the
    /// wallet at the top right with the void currencies under it and the lamp box above the key hints (V5StoryWallet), the goal plate with
    /// the side quest (V5StoryGoal), the subtitles and the night banner (V5StoryLines). The old pieces keep their names and their texts:
    /// each is restyled where it stands, or hidden where the mock-up merges it into a new piece (the base bar into the day plate). This
    /// part builds them and ticks their motion, which stays restrained: a changed number swells once, the dial's arc eases to its value,
    /// the banner slides in and fades out. It also brings the goal marker, the mining bar and the flash into the same family.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // A changed number swells once and settles over this long, at most this much larger. The mining bar's centre stands this far over
        // the screen's foot: clear of a subtitle of two lines under it.
        const float StoryPulseSeconds = .35f, StoryPulseGrow = .22f, StoryMineBarY = 250f;

        sealed class StoryPulse
        {
            public Transform target;
            public float left;
        }

        readonly List<StoryPulse> storyPulses = new();
        bool storyBuilt;

        static float Sp(float mockPixels) => V5StoryKit.Px(mockPixels);

        /// <summary>The goal marker's colour: orange (go there) in the fifth pass, the old cyan otherwise.</summary>
        static Color MarkerColour => V5HudKit.On ? V5TitleKit.Orange : Accent;

        /// <summary>The mining bar's fill: steel for alloy and violet for dark matter in the fifth pass, the old colours otherwise.</summary>
        static Color MineAlloyColour => V5HudKit.On ? V5TitleKit.Steel : AlloyBar;

        static Color MineDarkColour => V5HudKit.On ? V5HudKit.Violet : DarkBar;

        void BuildStoryHud()
        {
            BuildStoryDay();
            BuildStoryWallet();
            BuildStoryLampBox();
            BuildStoryGoal();
            BuildStoryLines();
            RestyleStoryFeedback();
            RestyleStoryConfirm();
            storyBuilt = true;
        }

        /// <summary>Ten times a second with the campaign's texts (RefreshTexts): the day plate and the wallet; the goal plate again after a
        /// change of language (its head is written in the language shown).</summary>
        void RefreshStoryTexts()
        {
            if (!storyBuilt) return;
            RefreshStoryDay();
            RefreshStoryWallet();
            if (storyGoalChinese != ArenaText.IsChinese) LayOutStoryGoal();
        }

        /// <summary>Every frame the campaign runs (Refresh): the dial's arc, the night banner and the pulses.</summary>
        void TickStoryHud()
        {
            if (!storyBuilt) return;
            float dt = Time.unscaledDeltaTime;
            TickStoryDay(dt);
            TickStoryBanner(dt);
            TickStorySubtitleCover();
            foreach (var pulse in storyPulses)
            {
                if (pulse.left <= 0f) continue;
                pulse.left = Mathf.Max(0f, pulse.left - dt / StoryPulseSeconds);
                if (pulse.target) pulse.target.localScale = Vector3.one * (1f + StoryPulseGrow * Mathf.Sin(pulse.left * Mathf.PI));
            }
        }

        /// <summary>The run is over or not begun (HideAll): the banner goes and the goal plate follows its cleared side quest.</summary>
        void HideStoryHud()
        {
            if (!storyBuilt) return;
            EndNightBanner();
            storySubtitleInBanner = false;
            LayOutStoryGoal();
        }

        /// <summary>Makes a number swell once (again from the start if it is swelling already).</summary>
        void PulseOnce(Transform target)
        {
            if (!target) return;
            foreach (var pulse in storyPulses)
                if (pulse.target == target)
                {
                    pulse.left = 1f;
                    return;
                }
            storyPulses.Add(new StoryPulse { target = target, left = 1f });
        }

        /// <summary>A wallet number's pulse from what was just gained (TickFeedback's): it swells and brightens once, then settles in its
        /// own colour.</summary>
        static void StoryPulseLabel(Text label, Color rest, float left)
        {
            float wave = Mathf.Sin(left * Mathf.PI);
            label.rectTransform.localScale = Vector3.one * (1f + StoryPulseGrow * wave);
            label.color = Color.Lerp(rest, Color.white, .6f * wave);
        }

        /// <summary>The highest a goal marker may stand (canvas units from the foot): a little under the day plate.</summary>
        float StoryMarkerCeiling(float highest, float height) =>
            storyDay && storyDay.gameObject.activeInHierarchy ? Mathf.Min(highest, height + storyDay.anchoredPosition.y - storyDay.sizeDelta.y - 24f) : highest;

        void RestyleStoryFeedback()
        {
            // The goal marker and its chevron in orange with a cream core (PlaceMarker colours the marker every frame with MarkerColour).
            var core = markerIcon.transform.Find("MarkerCore");
            if (core && core.TryGetComponent<Image>(out var dot)) dot.color = V5TitleKit.Paint;
            foreach (var bar in markerArrow.GetComponentsInChildren<Image>(true)) bar.color = V5TitleKit.Orange;
            markerText.color = V5TitleKit.Ink;
            markerText.fontSize = V5StoryKit.FontSize(19f);
            // The mining bar: the mock-up's dark track with a hairline (its fill: MineAlloyColour, MineDarkColour), raised clear of the subtitle.
            mineBar.anchoredPosition = new Vector2(0f, StoryMineBarY);
            if (mineBar.TryGetComponent<Image>(out var track)) track.color = V5HudKit.Track;
            if (mineBar.TryGetComponent<UnityEngine.UI.Outline>(out var rim))
            {
                rim.effectColor = V5TitleKit.Rgb(160, 170, 190, .45f);
                rim.effectDistance = new Vector2(1f, -1f);
            }
            mineLabel.color = V5TitleKit.Ink;
            mineLabel.fontSize = V5StoryKit.FontSize(20f);
            // The flash: its line on a plate. The old dark box keeps its place and size (PlaceFlash lays it out) and draws nothing.
            flashBack.enabled = false;
            var plate = V5HudKit.Plate(flashBack.transform, "Plate", V5HudKit.PlateLook.Plain, 10f, V5HudKit.StoryCss);
            plate.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(plate.Rect);
            flashText.color = V5TitleKit.Ink;
            flashText.fontSize = 20;
        }

        /// <summary>The yes / no question over the run (BuildConfirm) in the same family: its panel on a plate with the title in the cream
        /// paint, OK on a warm plate (act on it) and Cancel on a plain one. Each button keeps its own invisible image to take the click and
        /// tints its plate as it is pointed at and pressed; the labels keep one size (no best fit: Chinese would shrink, pitfall T-015).</summary>
        void RestyleStoryConfirm()
        {
            if (!confirmTitle || !(confirmTitle.transform.parent is RectTransform panel)) return;
            if (panel.TryGetComponent<Image>(out var back)) back.enabled = false;
            if (panel.TryGetComponent<UnityEngine.UI.Outline>(out var rim)) rim.enabled = false;
            var plate = V5HudKit.Plate(panel, "Plate", V5HudKit.PlateLook.Plain, 16f, V5HudKit.StoryCss);
            plate.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(plate.Rect);
            V5TitleKit.Painted(confirmTitle);
            confirmBody.color = V5TitleKit.Ink2;
            confirmHint.color = V5TitleKit.Ink3;
            StoryConfirmButton(confirmYesLabel, V5HudKit.PlateLook.Warm, V5TitleKit.Chosen);
            StoryConfirmButton(confirmNoLabel, V5HudKit.PlateLook.Plain, V5TitleKit.Ink);
        }

        static void StoryConfirmButton(Text label, V5HudKit.PlateLook look, Color ink)
        {
            if (!label || !(label.transform.parent is RectTransform rect) || !rect.TryGetComponent<Button>(out var button)) return;
            if (rect.TryGetComponent<Image>(out var hit)) hit.color = Color.clear;
            if (rect.TryGetComponent<UnityEngine.UI.Outline>(out var rim)) rim.enabled = false;
            var face = V5HudKit.Plate(rect, "Plate", look, 10f, V5HudKit.StoryCss);
            face.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(face.Rect);
            button.targetGraphic = face.Image;
            label.resizeTextForBestFit = false;
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.color = ink;
        }

        // ───── Small builders ─────

        static void SetShown(Component part, bool shown)
        {
            if (part && part.gameObject.activeSelf != shown) part.gameObject.SetActive(shown);
        }

        static void StoryText(Text label, string text)
        {
            text ??= "";
            if (label && label.text != text) label.text = text;
        }

        /// <summary>Centres a rect on a point of its parent (anchor) with its own size.</summary>
        static void PlaceCentre(RectTransform rect, Vector2 anchor, Vector2 centre, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = centre;
            rect.sizeDelta = size;
        }

        /// <summary>A label in a row: from left (canvas units from the row's left edge) as wide as width, centred on the row's middle and
        /// scaled from its own centre when it pulses.</summary>
        static void PlaceSpan(Text label, float left, float width, float height)
        {
            label.alignment = TextAnchor.MiddleCenter;
            PlaceCentre(label.rectTransform, new Vector2(0f, .5f), new Vector2(left + width * .5f, 0f), new Vector2(width + 2f, height));
        }

        static string HexOf(Color colour) => ColorUtility.ToHtmlStringRGB(colour);

        /// <summary>Text cut at its first sentence's end (a Chinese full stop or ". ", and with semicolons also at "；" or "; "): the head
        /// without its stop, and the rest (its first letter raised in English); the whole text and nothing when there is no cut.</summary>
        static void SplitWords(string words, bool semicolons, out string head, out string rest)
        {
            words = (words ?? "").Trim();
            head = words;
            rest = "";
            int cut = -1, skip = 0;
            void Try(string mark)
            {
                int at = words.IndexOf(mark, System.StringComparison.Ordinal);
                if (at > 0 && (cut < 0 || at < cut))
                {
                    cut = at;
                    skip = mark.Length;
                }
            }
            Try("。");
            Try(". ");
            if (semicolons)
            {
                Try("；");
                Try("; ");
            }
            if (cut <= 0 || cut + skip >= words.Length)
            {
                head = words.TrimEnd('。', '.');
                return;
            }
            head = words.Substring(0, cut).Trim();
            rest = words.Substring(cut + skip).Trim();
            if (rest.Length > 0 && rest[0] >= 'a' && rest[0] <= 'z') rest = char.ToUpperInvariant(rest[0]) + rest.Substring(1);
        }
    }
}
