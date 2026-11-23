using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The base tour's spotlight (V2CampaignUi.BaseTour.cs): Day 0's spotlight (V2Spotlight) for a part of the screen instead of a point in
    /// the world. The whole screen dims except a box round the highlighted part; the box has a soft edge and a pulsing ring in Day 0's
    /// colours, and beside it stands a plate with what the step explains and, smaller under it, how to go on. A backing that draws
    /// nothing covers the screen and takes every click (this component is its raycast filter), except a click on the highlighted part
    /// while its step is open, which reaches the page under it. When the step is done the ring flashes green as Day 0's circle does and
    /// the plate fades; the box then slides over to the next part. The box follows its part every frame, so a page laid out again or a
    /// screen of another size moves it along. Sealed, it takes every click and shows nothing. It runs on unscaled time, since the base
    /// page stops the game's clock.
    /// </summary>
    public sealed class V2RectSpotlight : MonoBehaviour, ICanvasRaycastFilter
    {
        // Canvas units (1600 x 900 reference): the box's margin round the part, the width of its soft edge, how far outside the soft edge
        // the ring lies and how much farther out it moves in the green flash, and how far past the part's own edge a click still counts.
        const float BoxPad = 4f, Feather = 12f, RingOut = 4f, RingGrow = 8f, ClickSlack = 2f;
        // The plate: its gap to the ring and to the screen's edges, its widths and padding, the gap between its two lines, their sizes.
        const float PlateGap = 18f, ScreenMargin = 16f, PlateMaxWidth = 560f, PlateMinWidth = 300f, PlatePadX = 22f, PlatePadY = 16f, LineGap = 8f;
        const int TextSize = 23, HintSize = 16;
        // Seconds (unscaled): the box's slide over to the next part, and the plate's fade in or out.
        const float SlideSeconds = .35f, PlateFadeSeconds = .2f;
        // The soft edge's and the ring's pictures: white squares sliced at SliceBorder, so their corners keep their size whatever the
        // box's size; the soft edge's corners are rounded at CornerRadius, and the ring is a line about RingWidth either side of its middle.
        const int SliceSize = 64, SliceBorder = 31;
        const float CornerRadius = 14f, RingWidth = 1.6f;

        RectTransform blocker, layer, tip;
        readonly Image[] quads = new Image[4];
        Image edge, ring;
        CanvasGroup tipGroup;
        Text body, hint;
        Func<Rect?> target;
        // The box now (the part with its margin, eased over from the last part while sliding), where a slide began and where it ends,
        // and the part's own box that takes the click; all in canvas units from the screen's bottom-left corner.
        Rect box, slideFrom, slideTo, clickable;
        bool placed, found, passed;
        float dimAlpha, shownFor, plateAlpha, passClock = -1f, slideClock = -1f;
        // The page's parts the plate keeps off (Show's keepClear, TourPlateRoom), asked again every PlanSeconds and whenever the box, the
        // screen or the words change, and the plate's place they gave.
        Action<List<Rect>, List<Rect>> keepClear;
        readonly List<Rect> clearBusy = new(), clearButtons = new();
        Rect planAround, planPlate;
        Vector2 planSize;
        string planText;
        float planAt = -10f;
        int planSignature;
        const float PlanSeconds = .25f;

        /// <summary>The green flash and the plate's fade are still playing (the next step waits for them).</summary>
        public bool Busy => passClock >= 0f && passClock < V2Spotlight.FlashSeconds + PlateFadeSeconds;

        public static V2RectSpotlight Create(RectTransform parent)
        {
            var rect = new GameObject("BaseTour", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            var view = rect.gameObject.AddComponent<V2RectSpotlight>();
            view.Build(rect);
            return view;
        }

        void Build(RectTransform rect)
        {
            // The backing: clear, and the only part here that takes a click; IsRaycastLocationValid lets a click on the highlighted part
            // through to the page under it.
            var backing = new GameObject("Blocker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            blocker = backing.rectTransform;
            blocker.SetParent(rect, false);
            Stretch(blocker);
            backing.color = Color.clear;
            backing.raycastTarget = true;
            // A renderer that culls a clear mesh leaves it out of the canvas's drawing, and the raycaster skips what is not drawn.
            backing.canvasRenderer.cullTransparentMesh = false;

            layer = new GameObject("Layer", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(rect, false);
            Stretch(layer);
            for (int i = 0; i < quads.Length; i++) quads[i] = Picture("Dim" + i, null);
            edge = Picture("Edge", EdgeSprite);
            ring = Picture("Ring", RingSprite);

            tip = new GameObject("Tip", typeof(RectTransform)).GetComponent<RectTransform>();
            tip.SetParent(layer, false);
            Corner(tip);
            tipGroup = tip.gameObject.AddComponent<CanvasGroup>();
            tipGroup.blocksRaycasts = false;
            // The fifth pass's plain plate, as Day 0's tip: gunmetal with the art on, the flat plate with the thin edge in whitebox.
            V5TitleKit.Stretch(V5HudKit.Plate(tip, "Plate", V5HudKit.PlateLook.Plain, 10f, V5HudKit.StoryCss).Rect);
            body = Label("Text", TextSize, V5TitleKit.Ink, FontStyle.Normal);
            body.lineSpacing = 1.2f;
            hint = Label("Hint", HintSize, V2Spotlight.Edge, FontStyle.Bold);
            blocker.gameObject.SetActive(false);
            layer.gameObject.SetActive(false);
        }

        /// <summary>Puts the box on the part that at returns, asked every frame (its box in canvas units from the screen's bottom-left
        /// corner, or null while it is not on the screen), with the plate saying text and, smaller under it, hintText; the rest of the
        /// screen dims by dimness. A box already shown slides over to the new part; otherwise everything fades in. keepClear, when given,
        /// lists the page's parts (their words and icons, then their buttons) that the plate keeps off.</summary>
        public void Show(Func<Rect?> at, string text, string hintText, float dimness, Action<List<Rect>, List<Rect>> keepClear = null)
        {
            this.keepClear = keepClear;
            planAt = -10f;
            bool moving = target != null && placed;
            target = at;
            dimAlpha = Mathf.Clamp01(dimness);
            passed = found = false;
            passClock = -1f;
            if (moving)
            {
                slideFrom = slideTo = box;
                slideClock = 0f;
            }
            else
            {
                placed = false;
                slideClock = -1f;
                shownFor = plateAlpha = 0f;
            }
            var font = V5TitleKit.Font;
            if (font) body.font = hint.font = font;
            body.text = text ?? "";
            hint.text = hintText ?? "";
            SetShown(hint, hint.text.Length > 0);
            SetShown(blocker, true);
        }

        /// <summary>The step is done: the ring flashes green and the plate fades out; the box stays where it is until the next Show.</summary>
        public void Pass()
        {
            if (target == null || passed) return;
            passed = true;
            passClock = 0f;
        }

        /// <summary>Takes every click and shows nothing (before the tour's first step).</summary>
        public void Seal()
        {
            Clear();
            SetShown(blocker, true);
        }

        /// <summary>Everything goes at once: the dim, the plate and the backing that takes the clicks.</summary>
        public void Hide()
        {
            Clear();
            SetShown(blocker, false);
        }

        void Clear()
        {
            target = null;
            placed = found = passed = false;
            passClock = slideClock = -1f;
            SetShown(layer, false);
        }

        /// <summary>The backing's raycast filter: the backing takes a click anywhere but on the highlighted part, and there too unless the
        /// step is open (its part on the screen, not passed, the box not sliding).</summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (target == null || !placed || !found || passed || slideClock >= 0f) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(blocker, screenPoint, eventCamera, out var local)) return true;
            return !clickable.Contains(local - blocker.rect.min);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            // Where the part is now. Once passed, the box stays where its step was done.
            if (!passed)
            {
                var at = target();
                found = at.HasValue;
                if (found)
                {
                    clickable = Grow(at.Value, ClickSlack);
                    slideTo = Grow(at.Value, BoxPad);
                }
            }
            // Nothing is drawn until the part has been on the screen once (the backing still takes every click).
            if (!placed)
            {
                if (!found)
                {
                    SetShown(layer, false);
                    return;
                }
                placed = true;
                box = slideTo;
                SetShown(layer, true);
            }
            if (slideClock >= 0f)
            {
                slideClock += dt;
                box = Lerp(slideFrom, slideTo, V5TitleKit.EaseOut(Mathf.Clamp01(slideClock / SlideSeconds)));
                if (slideClock >= SlideSeconds) slideClock = -1f;
            }
            else if (found && !passed) box = slideTo;

            shownFor += dt;
            float fade = Mathf.Clamp01(shownFor / V2Spotlight.FadeInSeconds), flash = 0f;
            if (passClock >= 0f)
            {
                passClock += dt;
                if (passClock < V2Spotlight.FlashSeconds) flash = Mathf.Sin(passClock / V2Spotlight.FlashSeconds * Mathf.PI);
            }
            // The plate shows while its step is open and through the green flash; it fades out after the flash and while the box slides.
            bool plateShown = passed ? passClock < V2Spotlight.FlashSeconds : slideClock < 0f && found;
            plateAlpha = Mathf.MoveTowards(plateAlpha, plateShown ? 1f : 0f, dt / PlateFadeSeconds);
            Draw(((RectTransform)transform).rect.size, fade, flash);
        }

        void Draw(Vector2 size, float fade, float flash)
        {
            // Four plain quads dim the screen round the box's outer edge; the soft edge over the box is dark at its rim and clear Feather
            // inside it, so the part and its margin stay bright.
            Rect outer = Grow(box, Feather);
            var dark = new Color(0f, 0f, 0f, dimAlpha * fade);
            Place(quads[0].rectTransform, new Rect(0f, 0f, Mathf.Max(0f, outer.xMin), size.y));
            Place(quads[1].rectTransform, new Rect(outer.xMax, 0f, Mathf.Max(0f, size.x - outer.xMax), size.y));
            Place(quads[2].rectTransform, new Rect(outer.xMin, 0f, outer.width, Mathf.Max(0f, outer.yMin)));
            Place(quads[3].rectTransform, new Rect(outer.xMin, outer.yMax, outer.width, Mathf.Max(0f, size.y - outer.yMax)));
            foreach (var quad in quads) quad.color = dark;
            Place(edge.rectTransform, outer);
            edge.color = dark;
            // The ring lies on the soft edge's rim and pulses as Day 0's ring does; when the step is done it flashes green and moves out.
            Place(ring.rectTransform, Grow(outer, RingOut + RingGrow * flash));
            Color tint = Color.Lerp(V2Spotlight.Edge, V2Spotlight.PassGreen, flash);
            float pulse = .3f + .15f * Mathf.Sin(Time.unscaledTime * 4f);
            ring.color = new Color(tint.r, tint.g, tint.b, Mathf.Max(pulse * fade, flash));
            PlacePlate(size, Grow(outer, RingOut));
        }

        /// <summary>The plate beside the ring on the side with more room (BaseTour.PlateSide), as tall as its two lines need at its width.</summary>
        void PlacePlate(Vector2 size, Rect around)
        {
            tipGroup.alpha = plateAlpha;
            if (plateAlpha <= 0f) return;
            if (keepClear != null)
            {
                PlacePlateClear(size, around);
                return;
            }
            var side = BaseTour.PlateSide(size, around, PlateMinWidth, PlateGap, ScreenMargin);
            float width = BaseTour.PlateWidth(side, size, around, PlateMinWidth, PlateMaxWidth, PlateGap, ScreenMargin);
            float inner = width - PlatePadX * 2f;
            float bodyHeight = Fit(body, inner);
            bool hinted = hint.gameObject.activeSelf;
            float hintHeight = hinted ? Fit(hint, inner) : 0f;
            float height = PlatePadY * 2f + bodyHeight + (hinted ? LineGap + hintHeight : 0f);
            Place(tip, BaseTour.PlatePlace(side, size, around, new Vector2(width, height), PlateGap, ScreenMargin));
            body.rectTransform.anchoredPosition = new Vector2(PlatePadX, -PlatePadY);
            hint.rectTransform.anchoredPosition = new Vector2(PlatePadX, -(PlatePadY + bodyHeight + LineGap));
        }

        /// <summary>The plate where it covers the least of the page's parts (TourPlateRoom), its words laid out at its width.</summary>
        void PlacePlateClear(Vector2 size, Rect around)
        {
            bool moved = planText != body.text || planSize != size || !Near(planAround, around);
            if (moved || Time.unscaledTime - planAt >= PlanSeconds)
            {
                clearBusy.Clear();
                clearButtons.Clear();
                keepClear(clearBusy, clearButtons);
                planAt = Time.unscaledTime;
                // The page's parts are asked every PlanSeconds; the plate is placed again only when they, the box or the words changed.
                int signature = Signature(clearBusy) * 31 + Signature(clearButtons);
                if (moved || signature != planSignature)
                {
                    planSignature = signature;
                    bool hinted = hint.gameObject.activeSelf;
                    float oneLine = Mathf.Ceil(Mathf.Max(body.preferredWidth, hinted ? hint.preferredWidth : 0f)) + PlatePadX * 2f + 2f;
                    planPlate = TourPlateRoom.Place(size, around, width => PlateHeight(width, hinted), oneLine, clearBusy, clearButtons,
                        PlateMinWidth, PlateMaxWidth, PlateGap, ScreenMargin);
                    planText = body.text;
                    planSize = size;
                    planAround = around;
                }
            }
            float inner = planPlate.width - PlatePadX * 2f;
            float bodyHeight = Fit(body, inner);
            if (hint.gameObject.activeSelf) Fit(hint, inner);
            Place(tip, planPlate);
            body.rectTransform.anchoredPosition = new Vector2(PlatePadX, -PlatePadY);
            hint.rectTransform.anchoredPosition = new Vector2(PlatePadX, -(PlatePadY + bodyHeight + LineGap));
        }

        /// <summary>How tall the plate is at this width, its two lines laid out there.</summary>
        float PlateHeight(float width, bool hinted)
        {
            float inner = width - PlatePadX * 2f;
            return PlatePadY * 2f + Fit(body, inner) + (hinted ? LineGap + Fit(hint, inner) : 0f);
        }

        static bool Near(Rect a, Rect b) => (a.min - b.min).sqrMagnitude < .25f && (a.max - b.max).sqrMagnitude < .25f;

        /// <summary>A number that changes when any of the parts moves by a unit or more, or one comes or goes.</summary>
        static int Signature(List<Rect> parts)
        {
            unchecked
            {
                int h = parts.Count;
                foreach (var r in parts) h = h * 31 + Mathf.RoundToInt(r.x) * 7 + Mathf.RoundToInt(r.y) * 13 + Mathf.RoundToInt(r.width) * 17 + Mathf.RoundToInt(r.height);
                return h;
            }
        }

        /// <summary>Gives a label this width and returns how tall its text is there, the label made that tall.</summary>
        static float Fit(Text label, float width)
        {
            var rect = label.rectTransform;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            float height = Mathf.Ceil(label.preferredHeight);
            rect.sizeDelta = new Vector2(width, height);
            return height;
        }

        // ───── Building blocks ─────

        Image Picture(string name, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(layer, false);
            Corner(image.rectTransform);
            image.sprite = sprite;
            image.type = sprite ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.black;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A label in the plate, placed from the plate's top-left corner, that wraps at its width.</summary>
        Text Label(string name, int size, Color colour, FontStyle style)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rect = label.rectTransform;
            rect.SetParent(tip, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            label.font = V5TitleKit.Font;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }

        static Rect Grow(Rect rect, float by) => Rect.MinMaxRect(rect.xMin - by, rect.yMin - by, rect.xMax + by, rect.yMax + by);

        static Rect Lerp(Rect from, Rect to, float t) => Rect.MinMaxRect(Mathf.Lerp(from.xMin, to.xMin, t), Mathf.Lerp(from.yMin, to.yMin, t),
            Mathf.Lerp(from.xMax, to.xMax, t), Mathf.Lerp(from.yMax, to.yMax, t));

        static void SetShown(Component part, bool shown)
        {
            if (part && part.gameObject.activeSelf != shown) part.gameObject.SetActive(shown);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Anchored and pivoted at the bottom-left corner, so position and size are in the canvas's own units from there.</summary>
        static void Corner(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
        }

        static void Place(RectTransform rect, Rect at)
        {
            rect.anchoredPosition = at.position;
            rect.sizeDelta = at.size;
        }

        static Sprite edgeSprite, ringSprite;

        /// <summary>The soft edge: dark at its rim and clear Feather inside it, its corners rounded at CornerRadius.</summary>
        static Sprite EdgeSprite => edgeSprite ? edgeSprite : edgeSprite = Slice("BaseTourEdge", CornerRadius, depth => 1f - Mathf.SmoothStep(0f, 1f, depth / Feather));

        /// <summary>The ring: a thin line RingOut inside its rim. Drawn RingOut larger than the soft edge, the line lies on the soft edge's
        /// rim, its corners rounded as the rim's.</summary>
        static Sprite RingSprite => ringSprite ? ringSprite : ringSprite = Slice("BaseTourRing", CornerRadius + RingOut, depth => Mathf.Exp(-Mathf.Pow((depth - RingOut) / RingWidth, 2f)));

        /// <summary>A white square whose alpha follows how deep each pixel lies inside its rim, the rim's corners rounded at corner
        /// (alpha only: the colour comes from the image's tint). It is sliced so its corners keep their size, one pixel to a canvas unit.</summary>
        static Sprite Slice(string name, float corner, Func<float, float> alphaAtDepth)
        {
            var texture = new Texture2D(SliceSize, SliceSize, TextureFormat.RGBA32, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[SliceSize * SliceSize];
            float half = SliceSize * .5f, straight = half - corner;
            for (int y = 0; y < SliceSize; y++)
                for (int x = 0; x < SliceSize; x++)
                {
                    // The distance outside the rounded rim (negative inside it): the rounded square's distance field.
                    float qx = Mathf.Abs(x + .5f - half) - straight, qy = Mathf.Abs(y + .5f - half) - straight;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;
                    pixels[y * SliceSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAtDepth(-outside)) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, SliceSize, SliceSize), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * SliceBorder);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
