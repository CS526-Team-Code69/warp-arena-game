using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// One card of the fifth pass's pick screens (V5PickKit). Its own image takes the clicks and draws nothing; the crate lid's frame
    /// (V5PickFrame) is a child, so a glow can sit behind it: soft, in the card's colour (a rarity) with the art on. The chosen card (the
    /// one a go button or a click takes) is lifted, glows orange and has an orange pointer over it. A band of light crosses the card once
    /// as the pointer arrives and a ring of light spreads from where it is pressed, both inside the frame and only with the art on; the
    /// card sinks a little while pressed. Its screen places what it shows (the picture, the name, the chips, the rows) from the card's
    /// top-left corner; Inner* give the frame's dark inside.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5PickCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        // The lights (as the title's plates, work/art-plans/style-1112/ui-e/fx.js).
        const float SweepSeconds = .62f, RingSeconds = .56f, SweepLean = -18f;
        // The mock-up's lift of the chosen card, its pointer and the press, in mock-up pixels; how fast the lift follows.
        const float LiftPx = 14f, PointerW = 28f, PointerH = 18f, PointerAbove = 26f, SinkPx = 2f, LiftSpeed = 8f;
        static readonly Color SweepTint = new(1f, .94f, .86f, .3f), WarmRing = new(1f, .84f, .67f, .8f);
        static readonly Color ChosenGlow = V5TitleKit.Rgb(242, 106, 43, .95f), HoverGlow = V5TitleKit.Rgb(242, 106, 43, .35f);

        float css = V5HudKit.HudCss;
        Image hit, glow, pointer, sweep, ring;
        V5PickFrame frame;
        RectTransform lights;
        Vector2 rest, size;
        float lift, sink, sweepAge = -1f, ringAge = -1f, tintGlow;
        bool hovered, pressed, chosen, live = true, dim;
        bool? shownArt;
        Color tint = Color.white;
        Vector2 ringAt;

        /// <summary>The pointer arrived over the card, or left it; the left button clicked it (only while it is live).</summary>
        public event Action<V5PickCard> Entered, Exited, Clicked;
        /// <summary>The card plays the hover sound itself; off where the card's own button already plays one.</summary>
        public bool HoverSound { get; set; } = true;
        public float HoverPitch { get; set; } = 1f;
        public RectTransform Rect => (RectTransform)transform;
        public bool Hovered => hovered;
        public Vector2 Size => size;
        public V5PickFrame Frame => frame;

        /// <summary>The frame's dark inside, from the card's top-left corner.</summary>
        public float InnerLeft => size.x * V5PickKit.FrameLeft;
        public float InnerTop => size.y * V5PickKit.FrameTop;
        public float InnerWidth => size.x * (1f - V5PickKit.FrameLeft - V5PickKit.FrameRight);
        public float InnerHeight => size.y * (1f - V5PickKit.FrameTop - V5PickKit.FrameBottom);

        /// <summary>The card look on an object that has its own image (a saved choice card: its click stays as it was).</summary>
        public static V5PickCard Dress(GameObject card, float css)
        {
            if (!card.TryGetComponent<V5PickCard>(out var view)) view = card.AddComponent<V5PickCard>();
            view.Setup(css);
            return view;
        }

        /// <summary>A new card under parent.</summary>
        public static V5PickCard Make(Transform parent, string name, float css)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return Dress(go, css);
        }

        void Setup(float scale)
        {
            css = scale;
            if (frame) return;
            if (!TryGetComponent(out hit)) hit = gameObject.AddComponent<Image>();
            hit.sprite = null;
            hit.type = Image.Type.Simple;
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var rect = Rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            glow = V5TitleKit.Picture(transform, "V5Glow", V5PickKit.CardGlow, Color.clear, V5PickKit.CardGlowDensity);
            glow.transform.SetSiblingIndex(0);
            var frameImage = V5TitleKit.Picture(transform, "V5Frame", null, Color.white);
            frameImage.transform.SetSiblingIndex(1);
            frame = frameImage.gameObject.AddComponent<V5PickFrame>();
            lights = V5TitleKit.Rect(transform, "V5Lights");
            lights.gameObject.AddComponent<RectMask2D>();
            sweep = V5TitleKit.Picture(lights, "Sweep", V5TitleKit.Piece("sweep"), Color.clear);
            var band = sweep.rectTransform;
            band.anchorMin = band.anchorMax = new Vector2(0f, .5f);
            band.pivot = new Vector2(.5f, .5f);
            band.localRotation = Quaternion.Euler(0f, 0f, SweepLean);
            ring = V5TitleKit.Picture(lights, "Ring", V5TitleKit.Piece("ripple"), Color.clear);
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = ring.rectTransform.pivot = new Vector2(.5f, .5f);
            sweep.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
            lights.gameObject.SetActive(false);
            pointer = V5HudKit.Arrow(transform, "V5Pointer", new Vector2(PointerW, PointerH) * css, V5TitleKit.Orange, 180f);
            pointer.rectTransform.anchorMin = pointer.rectTransform.anchorMax = new Vector2(.5f, 1f);
            pointer.rectTransform.pivot = new Vector2(.5f, .5f);
            pointer.gameObject.SetActive(false);
            Dress();
        }

        /// <summary>Takes the card's look off again (its pieces and itself); the object keeps its own image, which takes the clicks.</summary>
        public void Remove()
        {
            foreach (var piece in new Component[] { glow, frame, lights, pointer })
                if (piece) Destroy(piece.gameObject);
            if (hit) hit.color = Color.white;
            Destroy(this);
        }

        /// <summary>The card's size; the frame, the glow, the lights and the pointer follow it.</summary>
        public void SetSize(Vector2 value)
        {
            size = value;
            Rect.sizeDelta = value;
            V5TitleKit.Stretch(frame.Image.rectTransform);
            V5TitleKit.Stretch(glow.rectTransform, -V5PickKit.CardGlowOutset);
            V5PickKit.Place(lights, InnerLeft, InnerTop, InnerWidth, InnerHeight);
            pointer.rectTransform.anchoredPosition = new Vector2(0f, PointerAbove * css - PointerH * css * .5f);
            Apply();
        }

        /// <summary>Where the card rests: its top-left corner, from its parent's top-left corner. The lift and the press move it from there.</summary>
        public void SetRest(float left, float top)
        {
            rest = new Vector2(left + size.x * .5f, -(top + size.y * .5f));
            Apply();
        }

        /// <summary>Puts the lifted card back where it rests at once (a screen that opens shows its cards resting).</summary>
        public void Settle()
        {
            lift = chosen ? LiftPx * css : 0f;
            sink = 0f;
            Apply();
        }

        /// <summary>The chosen card: lifted, an orange glow and an orange pointer (the whitebox edge turns orange).</summary>
        public bool Chosen
        {
            get => chosen;
            set
            {
                if (chosen == value) return;
                chosen = value;
                Dress();
            }
        }

        /// <summary>Whether the card answers the pointer (a card that cannot be taken, or keys still locked after the window opened).</summary>
        public bool Live
        {
            get => live;
            set
            {
                if (live == value) return;
                live = value;
                if (!live) Release();
            }
        }

        /// <summary>A card that cannot be taken is darker.</summary>
        public bool Dim
        {
            get => dim;
            set
            {
                if (dim == value) return;
                dim = value;
                Dress();
            }
        }

        /// <summary>The card's colour: the whitebox edge, and with the art on a soft glow of this strength (0 for none).</summary>
        public void SetTint(Color colour, float glowStrength)
        {
            if (tint == colour && Mathf.Approximately(tintGlow, glowStrength)) return;
            tint = colour;
            tintGlow = glowStrength;
            Dress();
        }

        void Dress()
        {
            if (!frame) return;
            bool art = Art.Enabled;
            shownArt = art;
            var edge = tint;
            edge.a = .9f;
            frame.SetEdge(edge);
            frame.SetState(chosen, hovered && live, dim);
            Color halo = Color.clear;
            if (art && chosen) halo = ChosenGlow;
            else if (art && hovered && live) halo = HoverGlow;
            else if (art && tintGlow > 0f) halo = new Color(tint.r, tint.g, tint.b, tintGlow);
            if (glow.color != halo) glow.color = halo;
            V5PickKit.Show(pointer, chosen);
            if (!art) StopLights();
        }

        void Apply()
        {
            var at = rest + new Vector2(0f, lift - sink);
            if (Rect.anchoredPosition != at) Rect.anchoredPosition = at;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!live || hovered) return;
            hovered = true;
            Dress();
            StartSweep();
            if (HoverSound) V5UiSound.Play(V5UiSound.Ui.Hover, HoverPitch);
            Entered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
            if (!hovered) return;
            hovered = false;
            Dress();
            Exited?.Invoke(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!live || eventData.button != PointerEventData.InputButton.Left) return;
            pressed = true;
            sink = SinkPx * css;
            Apply();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(lights, eventData.position, eventData.pressEventCamera, out var local)) StartRing(local);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!live || eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke(this);
        }

        void Release()
        {
            if (!pressed) return;
            pressed = false;
            sink = 0f;
            Apply();
        }

        void OnDisable()
        {
            Release();
            hovered = false;
            StopLights();
            lift = 0f;
            Apply();
            if (frame) Dress();
        }

        // ───── The lights, inside the frame ─────

        void StartSweep()
        {
            if (!Art.Enabled || !isActiveAndEnabled) return;
            Lights();
            sweepAge = 0f;
            sweep.gameObject.SetActive(true);
            DrawSweep();
        }

        void StartRing(Vector2 local)
        {
            if (!Art.Enabled || !isActiveAndEnabled) return;
            Lights();
            ringAge = 0f;
            ringAt = local - lights.rect.center;
            ring.gameObject.SetActive(true);
            DrawRing();
        }

        void Lights()
        {
            if (!lights.gameObject.activeSelf) lights.gameObject.SetActive(true);
            lights.SetAsLastSibling();
            pointer.transform.SetAsLastSibling();
        }

        void StopLights()
        {
            sweepAge = ringAge = -1f;
            if (lights && lights.gameObject.activeSelf) lights.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (shownArt != Art.Enabled) Dress();
            float dt = Time.unscaledDeltaTime;
            float target = chosen ? LiftPx * css : 0f;
            if (!Mathf.Approximately(lift, target))
            {
                lift = Mathf.MoveTowards(lift, target, Mathf.Max(LiftPx * css * LiftSpeed * dt, .01f));
                Apply();
            }
            if (sweepAge < 0f && ringAge < 0f) return;
            if (sweepAge >= 0f)
            {
                sweepAge += dt;
                DrawSweep();
            }
            if (ringAge >= 0f)
            {
                ringAge += dt;
                DrawRing();
            }
            if (sweepAge < 0f && ringAge < 0f) StopLights();
        }

        void DrawSweep()
        {
            float t = sweepAge / SweepSeconds;
            if (t >= 1f)
            {
                sweepAge = -1f;
                sweep.gameObject.SetActive(false);
                return;
            }
            float p = V5TitleKit.Bezier(t, .3f, .6f, .3f, 1f);
            var area = lights.rect.size;
            float width = area.x * .46f;
            var band = sweep.rectTransform;
            band.sizeDelta = new Vector2(width, area.y * 1.4f + width);
            band.anchoredPosition = new Vector2(Mathf.Lerp(-1.1f, 2.3f, p) * width + width * .5f, 0f);
            float alpha = p < .2f ? p / .2f : p < .75f ? 1f : (1f - p) / .25f;
            var colour = SweepTint;
            colour.a *= Mathf.Clamp01(alpha);
            sweep.color = colour;
        }

        void DrawRing()
        {
            float t = ringAge / RingSeconds;
            if (t >= 1f)
            {
                ringAge = -1f;
                ring.gameObject.SetActive(false);
                return;
            }
            float p = V5TitleKit.Bezier(t, .15f, .7f, .3f, 1f);
            var area = lights.rect.size;
            float diameter = Mathf.Sqrt(area.x * area.x + area.y * area.y) * 1.15f * Mathf.Lerp(.06f, 1f, p);
            var rect = ring.rectTransform;
            rect.sizeDelta = new Vector2(diameter, diameter);
            rect.anchoredPosition = ringAt;
            float alpha = p < .45f ? Mathf.Lerp(1f, .8f, p / .45f) : Mathf.Lerp(.8f, 0f, (p - .45f) / .55f);
            var colour = WarmRing;
            colour.a *= alpha;
            ring.color = colour;
        }
    }
}
