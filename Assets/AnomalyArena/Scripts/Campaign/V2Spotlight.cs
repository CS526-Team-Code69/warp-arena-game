using System;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The forced tutorial's spotlight (implementation spec 2026-11-10, section A): the whole screen dims (CampaignRules.spotlightDim)
    /// except a round bright circle around what is being taught, which follows it every frame, and beside the circle a small tip with a
    /// key cap and a short phrase. When the step is done the circle flashes green, then the dim fades out (Pass); Busy holds until then,
    /// so the next step waits for it. A spotlight without dim shows only the circle's edge and the tip (a step that is not forced).
    /// It lives on the campaign's own canvas (V2CampaignUi.Spotlight.cs), under the goal and the marker, and shows only while the player
    /// can move: a panel, a weapon card or the pause hide it, and its flash waits for them to close.
    /// </summary>
    public sealed class V2Spotlight : MonoBehaviour
    {
        // The bright circle's texture is clear out to this share of its half size and fully dark at its edge (HoleEdge), so the square
        // drawn around the circle is the circle's radius / HoleEdge on each side.
        const float HoleEdge = .8f, HoleClear = .62f, MinRadius = 40f;
        // The pass: the green flash, then the dim fading out (seconds, unscaled).
        const float FlashSeconds = .45f, FadeSeconds = .5f, FadeInSeconds = .25f;
        static readonly Color PassGreen = new(.4f, 1f, .5f), Edge = new(.45f, .85f, 1f), TipPlate = new(.03f, .06f, .11f, .92f), Cap = new(.92f, .94f, .98f);

        RectTransform layer, tip, keyCap;
        readonly Image[] quads = new Image[4];
        RawImage hole, ring;
        Image tipBack;
        Outline tipEdge;
        Text keyLabel, phraseLabel;
        // The fifth pass's tip (V5HudKit.On): it fades as a whole, and is dressed again when the art switch changes.
        CanvasGroup tipGroup;
        bool? fifthArt;
        Func<Vector3> target;
        Vector3 lastTarget;
        float worldRadius, dimAlpha, shownFor, passClock = -1f;
        bool dim;

        /// <summary>The pass flash and fade are still playing (the next step waits for them).</summary>
        public bool Busy => passClock >= 0f;

        public static V2Spotlight Create(RectTransform parent)
        {
            var rect = new GameObject("Spotlight", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            var view = rect.gameObject.AddComponent<V2Spotlight>();
            view.Build(rect);
            return view;
        }

        void Build(RectTransform rect)
        {
            layer = new GameObject("Layer", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(rect, false);
            Stretch(layer);
            for (int i = 0; i < quads.Length; i++)
            {
                quads[i] = new GameObject("Dim" + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                quads[i].rectTransform.SetParent(layer, false);
                Corner(quads[i].rectTransform);
                quads[i].raycastTarget = false;
            }
            hole = Raw("Hole", HoleTexture());
            ring = Raw("Ring", RingTexture());

            tipBack = new GameObject("Tip", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            tip = tipBack.rectTransform;
            tip.SetParent(layer, false);
            Corner(tip);
            tipBack.color = TipPlate;
            tipBack.raycastTarget = false;
            tipEdge = tipBack.gameObject.AddComponent<Outline>();
            tipEdge.effectDistance = new Vector2(2f, -2f);
            var capImage = new GameObject("Key", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            keyCap = capImage.rectTransform;
            keyCap.SetParent(tip, false);
            keyCap.anchorMin = keyCap.anchorMax = new Vector2(0f, .5f);
            keyCap.pivot = new Vector2(0f, .5f);
            capImage.color = Cap;
            capImage.raycastTarget = false;
            var capEdge = capImage.gameObject.AddComponent<Shadow>();
            capEdge.effectColor = new Color(0f, 0f, 0f, .6f);
            capEdge.effectDistance = new Vector2(0f, -3f);
            keyLabel = Label("KeyLabel", keyCap, 20, new Color(.08f, .1f, .14f), TextAnchor.MiddleCenter);
            Stretch(keyLabel.rectTransform);
            phraseLabel = Label("Phrase", tip, 22, Color.white, TextAnchor.MiddleLeft);
            phraseLabel.rectTransform.anchorMin = phraseLabel.rectTransform.anchorMax = new Vector2(0f, .5f);
            phraseLabel.rectTransform.pivot = new Vector2(0f, .5f);
            if (V5HudKit.On) tipGroup = tip.gameObject.AddComponent<CanvasGroup>();
            layer.gameObject.SetActive(false);
        }

        /// <summary>Puts the spotlight on what at returns (asked every frame), with a bright circle worldRadius cells round it and the tip
        /// "[key] phrase". Without dim only the circle's edge and the tip show, and nothing is held back.</summary>
        public void Show(Func<Vector3> at, float radius, string key, string phrase, bool dimmed, float dimness)
        {
            target = at;
            worldRadius = Mathf.Max(.5f, radius);
            dim = dimmed;
            dimAlpha = Mathf.Clamp01(dimness);
            passClock = -1f;
            shownFor = 0f;
            var font = ArenaText.Font;
            if (font) { keyLabel.font = font; phraseLabel.font = font; }
            keyLabel.text = key ?? "";
            phraseLabel.text = phrase ?? "";
        }

        /// <summary>The step is done: the circle flashes green and the dim fades out, then the spotlight goes.</summary>
        public void Pass()
        {
            if (target == null || Busy) return;
            passClock = 0f;
        }

        public void Hide()
        {
            target = null;
            passClock = -1f;
            if (layer) layer.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            var cam = gm ? gm.cam : null;
            bool visible = target != null && cam && gm.State == GameState.Playing && gm.CanPlayerMove;
            if (layer.gameObject.activeSelf != visible) layer.gameObject.SetActive(visible);
            if (!visible) return;
            float dt = Time.unscaledDeltaTime;
            shownFor += dt;
            float fade = Mathf.Clamp01(shownFor / FadeInSeconds);
            float flash = 0f;
            if (Busy)
            {
                passClock += dt;
                flash = passClock < FlashSeconds ? Mathf.Sin(passClock / FlashSeconds * Mathf.PI) : 0f;
                fade *= passClock < FlashSeconds ? 1f : 1f - Mathf.Clamp01((passClock - FlashSeconds) / FadeSeconds);
                if (passClock >= FlashSeconds + FadeSeconds) { Hide(); return; }
            }
            lastTarget = target();
            if (tipGroup && fifthArt != Art.Enabled) DressFifth(Art.Enabled);
            Place(cam, fade, flash);
        }

        void Place(Camera cam, float fade, float flash)
        {
            var area = ((RectTransform)transform).rect;
            float scale = transform.lossyScale.x > 0f ? 1f / transform.lossyScale.x : 1f;
            Vector3 sp = cam.WorldToScreenPoint(lastTarget);
            Vector3 edge = cam.WorldToScreenPoint(lastTarget + cam.transform.right * worldRadius);
            Vector2 c = new(sp.x * scale, sp.y * scale);
            // Behind the camera: mirrored, as the goal's marker does, so the circle stays off the far side of the screen.
            if (sp.z < 0f) c = new Vector2(area.width - c.x, area.height - c.y);
            float r = Mathf.Max(MinRadius, new Vector2(edge.x - sp.x, edge.y - sp.y).magnitude * scale);
            float half = r / HoleEdge;

            Color dark = new(0f, 0f, 0f, dim ? dimAlpha * fade : 0f);
            foreach (var quad in quads) { quad.enabled = dim; quad.color = dark; }
            hole.enabled = dim;
            hole.color = dark;
            SetRect(hole.rectTransform, c, new Vector2(half * 2f, half * 2f));
            SetRect(quads[0].rectTransform, Vector2.zero, new Vector2(Mathf.Max(0f, c.x - half), area.height));
            SetRect(quads[1].rectTransform, new Vector2(c.x + half, 0f), new Vector2(Mathf.Max(0f, area.width - c.x - half), area.height));
            SetRect(quads[2].rectTransform, new Vector2(c.x - half, 0f), new Vector2(half * 2f, Mathf.Max(0f, c.y - half)));
            SetRect(quads[3].rectTransform, new Vector2(c.x - half, c.y + half), new Vector2(half * 2f, Mathf.Max(0f, area.height - c.y - half)));

            SetRect(ring.rectTransform, c, new Vector2(half * 2f, half * 2f));
            Color edgeColour = Color.Lerp(Edge, PassGreen, flash);
            float pulse = .3f + .15f * Mathf.Sin(Time.unscaledTime * 4f);
            ring.color = new Color(edgeColour.r, edgeColour.g, edgeColour.b, Mathf.Max(pulse * fade, flash));
            ring.rectTransform.localScale = Vector3.one * (1f + .12f * flash);

            // The tip: [key] phrase, to the right of the circle, or to its left where the right has no room; kept on the screen, so
            // with the circle off screen it waits at the edge on the circle's side.
            float capWidth = Mathf.Max(44f, keyLabel.preferredWidth + 22f), phraseWidth = phraseLabel.preferredWidth;
            keyCap.anchoredPosition = new Vector2(12f, 0f);
            keyCap.sizeDelta = new Vector2(capWidth, 40f);
            phraseLabel.rectTransform.anchoredPosition = new Vector2(12f + capWidth + 12f, 0f);
            phraseLabel.rectTransform.sizeDelta = new Vector2(phraseWidth + 4f, 40f);
            var size = new Vector2(12f + capWidth + 12f + phraseWidth + 18f, 58f);
            float x = c.x + r + 20f;
            if (x + size.x > area.width - 16f) x = c.x - r - 20f - size.x;
            x = Mathf.Clamp(x, 16f, Mathf.Max(16f, area.width - 16f - size.x));
            float y = Mathf.Clamp(c.y - size.y * .5f, 16f, Mathf.Max(16f, area.height - 16f - size.y));
            SetRect(tip, new Vector2(x, y), size);
            if (tipGroup)
            {
                tipGroup.alpha = Mathf.Max(fade, flash);
                return;
            }
            tipBack.color = new Color(TipPlate.r, TipPlate.g, TipPlate.b, TipPlate.a * Mathf.Max(fade, flash));
            tipEdge.effectColor = new Color(edgeColour.r, edgeColour.g, edgeColour.b, .8f * Mathf.Max(fade, flash));
            keyLabel.color = new Color(.08f, .1f, .14f, Mathf.Max(fade, flash));
            phraseLabel.color = new Color(1f, 1f, 1f, Mathf.Max(fade, flash));
        }

        /// <summary>The fifth pass's tip: the gunmetal plate and the light keycap with the art on; in whitebox the flat plate with the thin
        /// edge and a flat button. The words are cream, the key's name dark on the light cap.</summary>
        void DressFifth(bool art)
        {
            fifthArt = art;
            const float css = V5HudKit.StoryCss;
            var capImage = keyCap.GetComponent<Image>();
            V5TitleKit.Dress(tipBack, art ? V5TitleKit.Piece("plate") : null, art ? Color.white : WhiteboxSkin.PlateFill, art ? V5HudKit.PlateDensity(10f, css) : 0f);
            tipEdge.enabled = !art;
            tipEdge.effectColor = WhiteboxSkin.Edge;
            tipEdge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            V5TitleKit.Dress(capImage, art ? V5TitleKit.Piece("keycap") : null, art ? Color.white : WhiteboxSkin.ButtonFill, art ? V5HudKit.PieceDensity(css) : 0f);
            // The keycap's picture carries its own lower edge; the flat button keeps the drop shadow.
            if (capImage.TryGetComponent<Shadow>(out var capShadow)) capShadow.enabled = !art;
            keyLabel.color = art ? V5HudKit.KeyInk : V5TitleKit.Ink;
            phraseLabel.color = V5TitleKit.Ink;
        }

        // ───── Building blocks ─────

        RawImage Raw(string name, Texture texture)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(layer, false);
            Corner(image.rectTransform);
            image.rectTransform.pivot = new Vector2(.5f, .5f); // placed by its centre, and the flash grows it from there
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        static Text Label(string name, Transform parent, int size, Color colour, TextAnchor anchor)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            label.font = ArenaText.Font ? ArenaText.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size; label.fontStyle = FontStyle.Bold; label.color = colour; label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Anchored and pivoted at the bottom-left corner, so position and size are in the canvas's own units from there.</summary>
        static void Corner(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
        }

        static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Texture2D holeTexture, ringTexture;

        /// <summary>Dark everywhere but a round clear middle with a soft edge (alpha only; the colour comes from the image's tint).</summary>
        static Texture2D HoleTexture() => holeTexture ? holeTexture : holeTexture = Radial("SpotlightHole", d => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HoleClear, HoleEdge, d)));

        /// <summary>A thin ring on the circle's edge, for its outline and the green flash.</summary>
        static Texture2D RingTexture() => ringTexture ? ringTexture : ringTexture = Radial("SpotlightRing", d => Mathf.Exp(-Mathf.Pow((d - HoleEdge) / .025f, 2f)));

        static Texture2D Radial(string name, Func<float, float> alpha)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy))) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
