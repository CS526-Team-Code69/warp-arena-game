using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The rocket page's view of the ship (V5RocketPage.cs, work/art-plans/style-1112/ui-f/rocket.html): the ship's live picture
    /// (V2Campaign.ShipViewTexture, the camera that swings slowly to and fro north of the pad and shows each part as it is bought) in a
    /// teal plate with cut corners, and over it a numbered pin for each of the three parts, 1 the engine, 2 the hull, 3 the fuel tank:
    /// steel blue with a green check beside its name once done, orange until then. The pins follow the parts on the moving picture (each
    /// part's place on the ship seen through the view's camera); with the art on and no live picture, the crashed ship cut out stands in
    /// the plate with the pins where the mock-up has them. In whitebox the plate is flat with the thin edge and the pins are flat discs.
    /// Built only with the fifth pass's pages (V5HudKit.On).
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The view (mock-up pixels): 1180 x 466 at the columns' top from the left margin, its corners cut at 20; the pins' dots 46 across,
        // their tags 38 tall and 10 beside them, kept 30 inside the view.
        const float R5ViewW = 1180f, R5ViewH = 466f, R5ViewCut = 20f, R5PinDot = 46f, R5TagH = 38f, R5TagGap = 10f, R5PinMargin = 30f;
        // Each part's place on the ship, in the ship's own frame (V2Campaign.ShipCentre, not turned): the engine at the stern, the top of
        // the hull in its middle, the fuel tank low on its south side.
        static readonly Vector3[] R5PinAnchors = { new(-6.2f, 1.6f, 0f), new(.6f, 2.6f, 0f), new(-2.8f, .6f, -2.4f) };
        // Without the live picture: the dots' centres on the cut-out ship, as fractions of its picture from the top-left corner (the
        // mock-up's), and the picture's place in the view (its middle across, its top, its height).
        static readonly Vector2[] R5PinOnCutout = { new(.886f, .234f), new(.399f, .369f), new(.566f, .558f) };
        const float R5CutoutMiddle = 585f, R5CutoutTop = 24f, R5CutoutH = 420f;

        static readonly Color R5DotDone = V5TitleKit.Steel, R5InkDone = V5TitleKit.Rgb(11, 26, 42), R5InkToDo = V5TitleKit.Rgb(42, 14, 2),
            R5DotBack = V5TitleKit.Rgb(11, 18, 34);

        sealed class R5Pin
        {
            public RectTransform root, tag;
            public Image glow, back, dot, ring, tagBack;
            public Text number, name;
        }

        RectTransform r5View, r5Window;
        V5HudPlate r5ViewPlate;
        Image r5WindowShape, r5Cutout;
        RawImage r5Live;
        Camera r5ShipCamera;
        readonly R5Pin[] r5Pins = new R5Pin[3];

        // ───── Building (BuildRocket5) ─────

        void BuildRocket5View(RectTransform parent)
        {
            r5View = V5TitleKit.Rect(parent, "ShipView");
            B5Rect(r5View, B5Margin, B5Top, R5ViewW, R5ViewH);
            r5ViewPlate = V5HudKit.Plate(r5View, "Plate", V5HudKit.PlateLook.Teal, R5ViewCut, B5Css);
            V5TitleKit.Stretch(r5ViewPlate.Rect);
            // The window: the plate's inside, its corners cut with the art on, that clips the picture and the pins.
            r5WindowShape = V5TitleKit.Picture(r5View, "Window", V5TitleKit.Chamfer, Color.white, V5HudKit.ChamferDensity(R5ViewCut - 1f, B5Css));
            V5TitleKit.Stretch(r5WindowShape.rectTransform, Bp(3f));
            r5WindowShape.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            r5Window = r5WindowShape.rectTransform;
            r5Live = V5TitleKit.Raw(r5Window, "Live", null, Color.white);
            V5TitleKit.Stretch(r5Live.rectTransform);
            r5Cutout = V5TitleKit.Picture(r5Window, "Cutout", V5TitleKit.Piece("ship-cutout"), Color.white);
            r5Cutout.preserveAspect = true;
            for (int k = 0; k < 3; k++)
            {
                var pin = r5Pins[k] = new R5Pin();
                pin.root = V5TitleKit.Rect(r5Window, "Pin" + (k + 1));
                pin.root.anchorMin = pin.root.anchorMax = Vector2.zero;
                pin.root.pivot = new Vector2(.5f, .5f);
                pin.root.sizeDelta = Vector2.one * Bp(R5PinDot);
                pin.glow = V5TitleKit.Picture(pin.root, "Glow", V5HudKit.Glow, Color.clear);
                V5TitleKit.Centre(pin.glow.rectTransform, Vector2.one * Bp(R5PinDot * 1.9f));
                pin.ring = V5TitleKit.Picture(pin.root, "Ring", V5HudKit.RingLine, Color.white);
                V5TitleKit.Centre(pin.ring.rectTransform, Vector2.one * Bp(R5PinDot + 12f));
                pin.back = V5TitleKit.Picture(pin.root, "Gap", V5TitleKit.Piece("disc"), R5DotBack);
                V5TitleKit.Centre(pin.back.rectTransform, Vector2.one * Bp(R5PinDot + 6f));
                pin.dot = V5TitleKit.Picture(pin.root, "Dot", V5TitleKit.Piece("disc"), Color.white);
                V5TitleKit.Centre(pin.dot.rectTransform, Vector2.one * Bp(R5PinDot));
                pin.number = V5StoryKit.Label(pin.root, "Number", 26f, R5InkDone, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(pin.number.rectTransform);
                StoryText(pin.number, (k + 1).ToString());
                pin.tag = V5TitleKit.Rect(pin.root, "Tag");
                pin.tagBack = V5TitleKit.Picture(pin.tag, "Back", V5TitleKit.Chamfer, B5ChipBack, V5HudKit.ChamferDensity(8f, B5Css));
                V5TitleKit.Stretch(pin.tagBack.rectTransform);
                pin.name = V5StoryKit.Label(pin.tag, "Name", 22f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            }
        }

        // ───── Refreshing (RefreshRocket5) and following the picture (TickRocket5) ─────

        /// <summary>The view from the campaign's values now: the live picture (or the cut-out ship) and the pins' looks and names.</summary>
        void RefreshRocket5View(V2Campaign c, bool art, IReadOnlyList<V2Campaign.ShipPart> parts)
        {
            // The window follows the plate's shape: cut corners with the art on, a plain rectangle in whitebox.
            V5TitleKit.Dress(r5WindowShape, art ? V5TitleKit.Chamfer : null, Color.white, V5HudKit.ChamferDensity(R5ViewCut - 1f, B5Css));
            var live = c.ShipViewTexture;
            bool cutout = !live && art && r5Cutout.sprite;
            SetShown(r5Live, live);
            SetShown(r5Cutout, cutout);
            if (live)
            {
                r5Live.texture = live;
                var size = r5Window.rect.size;
                r5Live.uvRect = R5Cover(live.width / Mathf.Max(1f, live.height), size.x / Mathf.Max(1f, size.y));
            }
            if (cutout)
            {
                var shape = V5TitleKit.PieceSize("ship-cutout");
                float w = R5CutoutH * shape.x / Mathf.Max(1f, shape.y);
                B5Rect(r5Cutout.rectTransform, R5CutoutMiddle - w * .5f, R5CutoutTop, w, R5CutoutH);
            }
            for (int k = 0; k < 3; k++)
            {
                var pin = r5Pins[k];
                bool done = k < parts.Count && parts[k].done;
                SetShown(pin.root, live || cutout);
                var colour = done ? R5DotDone : V5TitleKit.Orange;
                pin.dot.color = colour;
                pin.ring.color = new Color(colour.r, colour.g, colour.b, .65f);
                pin.glow.color = art ? new Color(colour.r, colour.g, colour.b, .45f) : Color.clear;
                pin.back.color = art ? R5DotBack : WhiteboxSkin.PlateFill;
                pin.number.color = done ? R5InkDone : R5InkToDo;
                string name = k < parts.Count ? parts[k].name : "";
                StoryText(pin.name, done ? $"{name} <color=#{HexOf(B5Ok)}>✓</color>" : name);
                pin.name.fontSize = V5StoryKit.FontSize(22f);
                float tagW = B5W(pin.name, pin.name.text) + 28f;
                pin.tag.sizeDelta = new Vector2(Bp(tagW), Bp(R5TagH));
                B5PlaceLeft(pin.name, Bp(14f), Bp(tagW - 28f), Bp(R5TagH));
                B5Flat(pin.tagBack, art, V5TitleKit.Chamfer, B5ChipBack, V5HudKit.ChamferDensity(8f, B5Css), WhiteboxSkin.PlateFill, WhiteboxSkin.Edge);
                B5Edge(pin.dot, !art, WhiteboxSkin.Edge);
            }
            TickRocket5();
        }

        /// <summary>Every frame the rocket page shows: each pin on its part of the moving picture, seen through the view's camera; on the
        /// cut-out ship where the mock-up has it. A pin keeps inside the view, its tag right of its dot unless the dot is near the right edge.</summary>
        void TickRocket5()
        {
            if (!r5View || !r5View.gameObject.activeInHierarchy || !campaign) return;
            var size = r5Window.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            var live = r5Live.gameObject.activeSelf ? campaign.ShipViewTexture : null;
            var view = live ? ShipCamera5(live) : null;
            float margin = Bp(R5PinMargin);
            for (int k = 0; k < 3; k++)
            {
                var pin = r5Pins[k];
                if (!pin.root.gameObject.activeSelf) continue;
                Vector2 at;
                if (view)
                {
                    var point = view.WorldToViewportPoint(V2Campaign.ShipCentre + R5PinAnchors[k]);
                    var uv = r5Live.uvRect;
                    at = new Vector2((point.x - uv.x) / Mathf.Max(1e-4f, uv.width) * size.x, (point.y - uv.y) / Mathf.Max(1e-4f, uv.height) * size.y);
                }
                else
                {
                    // Where the cut-out ship stands in the window (RefreshRocket5View places it there), its left and top from the
                    // window's top-left corner.
                    var shape = V5TitleKit.PieceSize("ship-cutout");
                    float w = Bp(R5CutoutH * shape.x / Mathf.Max(1f, shape.y)), h = Bp(R5CutoutH), left = Bp(R5CutoutMiddle) - w * .5f, top = Bp(R5CutoutTop);
                    var spot = R5PinOnCutout[k];
                    at = new Vector2(left + spot.x * w, size.y - (top + spot.y * h));
                }
                at = new Vector2(Mathf.Clamp(at.x, margin, size.x - margin), Mathf.Clamp(at.y, margin, size.y - margin));
                pin.root.anchoredPosition = at;
                bool tagLeft = at.x > size.x * .72f;
                var tag = pin.tag;
                tag.anchorMin = tag.anchorMax = new Vector2(tagLeft ? 0f : 1f, .5f);
                tag.pivot = new Vector2(tagLeft ? 1f : 0f, .5f);
                tag.anchoredPosition = new Vector2((tagLeft ? -1f : 1f) * Bp(R5TagGap + 6f), 0f);
            }
        }

        /// <summary>The camera that draws the ship's live picture: the one whose target it is (found once while it draws).</summary>
        Camera ShipCamera5(Texture target)
        {
            if (r5ShipCamera && r5ShipCamera.targetTexture == target && r5ShipCamera.isActiveAndEnabled) return r5ShipCamera;
            r5ShipCamera = null;
            foreach (var camera in Camera.allCameras)
                if (camera && camera.targetTexture == target)
                {
                    r5ShipCamera = camera;
                    break;
                }
            return r5ShipCamera;
        }

        /// <summary>The part of a picture of this shape (width over height) that fills a box of the other shape: cut at the edges, never
        /// stretched.</summary>
        static Rect R5Cover(float shape, float box)
        {
            if (shape <= 0f || box <= 0f) return new Rect(0f, 0f, 1f, 1f);
            if (shape < box)
            {
                float height = shape / box;
                return new Rect(0f, (1f - height) * .5f, 1f, height);
            }
            float width = box / shape;
            return new Rect((1f - width) * .5f, 0f, width, 1f);
        }
    }
}
