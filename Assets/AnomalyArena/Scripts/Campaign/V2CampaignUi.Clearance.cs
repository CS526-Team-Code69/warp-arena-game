using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// What the story's plates keep clear of, and where they stand for those that keep clear of them: the base tour's plate keeps off
    /// the base page's words, buttons and icons (TourParts); the black hole's label steps below the flash (FlashOver); the screen-edge
    /// markers keep out of the goal plate (GoalPlateOnScreen); the goal marker stands above an ore's name (AboveNodeName).
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        static readonly List<Text> partTexts = new();
        static readonly List<Selectable> partButtons = new();
        static readonly List<Image> partImages = new();
        static readonly Vector3[] clearCorners = new Vector3[4];
        // A picture up to this size (square canvas units) is an icon the plate keeps off (a coin, a medal); a larger one is a backing.
        const float SmallPicture = 90f * 90f;

        /// <summary>The parts of the open base page the tour's plate keeps off (V2RectSpotlight.Show): its words, its small pictures, the
        /// purchase flash and the base system's subtitle while they show in busy, its buttons in buttons. Each is cut to the scrolling list it stands in; what a list hides
        /// or a faded group does not show is left out. Canvas units from the screen's bottom-left corner, as the tour draws.</summary>
        void TourParts(List<Rect> busy, List<Rect> buttons)
        {
            if (!baseScreen || !baseScreen.gameObject.activeInHierarchy) return;
            baseScreen.GetComponentsInChildren(false, partTexts);
            foreach (var label in partTexts)
                if (label.isActiveAndEnabled && !string.IsNullOrEmpty(label.text) && label.color.a > .05f && Shown(label) && PartBox(label.rectTransform) is Rect box)
                    busy.Add(box);
            baseScreen.GetComponentsInChildren(false, partImages);
            foreach (var image in partImages)
                if (image.isActiveAndEnabled && image.sprite && image.color.a > .05f && Shown(image) && PartBox(image.rectTransform) is Rect box
                    && box.width * box.height <= SmallPicture)
                    busy.Add(box);
            baseScreen.GetComponentsInChildren(false, partButtons);
            foreach (var button in partButtons)
                if (button.isActiveAndEnabled && Shown(button) && PartBox((RectTransform)button.transform) is Rect box)
                    buttons.Add(box);
            if (flashBack && flashBack.gameObject.activeInHierarchy && TourRect(flashBack.rectTransform) is Rect flash) busy.Add(flash);
            // A line the base system is saying, over the page's foot.
            if (subtitleRoot && subtitleRoot.gameObject.activeInHierarchy)
                foreach (var line in new[] { subtitleSpeaker, subtitleLine })
                    if (line && line.isActiveAndEnabled && !string.IsNullOrEmpty(line.text) && TourRect(line.rectTransform) is Rect said) busy.Add(said);
            partTexts.Clear();
            partImages.Clear();
            partButtons.Clear();
        }

        /// <summary>A part's box as the tour draws, cut to the scrolling list (RectMask2D or Mask) it stands in; null when the list hides it.</summary>
        Rect? PartBox(RectTransform part)
        {
            if (TourRect(part) is not Rect box) return null;
            for (var at = part.parent; at && at != baseScreen; at = at.parent)
            {
                if (!at.TryGetComponent<RectMask2D>(out _) && !at.TryGetComponent<Mask>(out _)) continue;
                if (TourRect((RectTransform)at) is not Rect mask) return null;
                float xMin = Mathf.Max(box.xMin, mask.xMin), yMin = Mathf.Max(box.yMin, mask.yMin), xMax = Mathf.Min(box.xMax, mask.xMax), yMax = Mathf.Min(box.yMax, mask.yMax);
                if (xMax <= xMin || yMax <= yMin) return null;
                box = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
            return box;
        }

        /// <summary>No group over the part has faded it out.</summary>
        static bool Shown(Component part)
        {
            for (var at = part.transform; at; at = at.parent)
                if (at.TryGetComponent<CanvasGroup>(out var group) && group.alpha < .05f) return false;
            return true;
        }

        /// <summary>The story's flash plate in screen pixels, when it shows over area: its bottom edge (the black hole's label steps down below it).</summary>
        public static bool FlashOver(Rect area, out float bottom)
        {
            bottom = 0f;
            var ui = V2Campaign.Active ? V2Campaign.Active.Ui : null;
            if (!ui || ui.FlashOnScreen() is not Rect plate || !plate.Overlaps(area)) return false;
            bottom = plate.yMin;
            return true;
        }

        /// <summary>The story's goal plate (with the side quest's strip) in screen pixels while it shows, else null: the screen-edge markers
        /// keep out of it (V2EdgeRoom).</summary>
        public static Rect? GoalPlateOnScreen()
        {
            var campaign = V2Campaign.Active;
            var ui = campaign && campaign.Running ? campaign.Ui : null;
            if (!ui || !ui.objectiveBack || !ui.objectiveBack.gameObject.activeInHierarchy) return null;
            var box = ScreenBox(ui.objectiveBack.rectTransform);
            if (!V5HudKit.On && ui.sideQuestBack && ui.sideQuestBack.gameObject.activeInHierarchy)
            {
                var side = ScreenBox(ui.sideQuestBack.rectTransform);
                box = Rect.MinMaxRect(Mathf.Min(box.xMin, side.xMin), Mathf.Min(box.yMin, side.yMin), Mathf.Max(box.xMax, side.xMax), Mathf.Max(box.yMax, side.yMax));
            }
            return box;
        }

        static Rect ScreenBox(RectTransform part)
        {
            part.GetWorldCorners(clearCorners);
            return Rect.MinMaxRect(clearCorners[0].x, clearCorners[0].y, clearCorners[2].x, clearCorners[2].y);
        }

        // The goal marker's reach round its diamond's middle (canvas units): the diamond with its pulse above, its distance under it, the
        // distance's width either side; and the gap it keeps above an ore's name.
        const float MarkerReachX = 44f, MarkerReachUp = 24f, MarkerReachDown = 54f, MarkerNameGap = 4f;

        /// <summary>The goal marker on the screen (icon: its diamond's middle, canvas units from the bottom-left; scale: canvas units per
        /// screen pixel) stands above the name of an ore near the player (V2WorldLabels) instead of over it.</summary>
        Vector2 AboveNodeName(Vector2 icon, float scale)
        {
            if (scale <= 0f) return icon;
            float pixel = 1f / scale;
            var area = Rect.MinMaxRect((icon.x - MarkerReachX) * pixel, (icon.y - MarkerReachDown) * pixel, (icon.x + MarkerReachX) * pixel,
                (icon.y + MarkerReachUp) * pixel);
            if (!V2WorldLabels.NodeNameUnder(area, out float top)) return icon;
            return new Vector2(icon.x, Mathf.Max(icon.y, top * scale + MarkerReachDown + MarkerNameGap));
        }
    }
}
