using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// A part of the title placed from its parent's top-left corner, in canvas units, that can be slid and faded from where it rests (the
    /// window's parts coming in, a card that shakes) without losing its place.
    /// </summary>
    public class V5Block : MonoBehaviour
    {
        Vector2 rest, offset;
        float sink;
        CanvasGroup group;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Where it rests: left and top from the parent's top-left corner, and its size.</summary>
        public void Place(float left, float top, float width, float height)
        {
            var rect = Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rest = new Vector2(left, -top);
            Apply();
        }

        /// <summary>How far it is moved from where it rests (x to the right, y up).</summary>
        public Vector2 Offset
        {
            get => offset;
            set
            {
                offset = value;
                Apply();
            }
        }

        /// <summary>How far it is pressed down.</summary>
        protected float Sink
        {
            get => sink;
            set
            {
                sink = value;
                Apply();
            }
        }

        public float Alpha
        {
            get => group ? group.alpha : 1f;
            set
            {
                if (!group)
                {
                    if (value >= 1f) return;
                    group = gameObject.AddComponent<CanvasGroup>();
                }
                group.alpha = value;
            }
        }

        void Apply() => Rect.anchoredPosition = rest + offset + new Vector2(0f, -sink);
    }

    /// <summary>
    /// A plate of the title the player acts on: a menu item, a save slot, a card, a tab, a keycap, a button. With the art on it is the
    /// mock-up's gunmetal plate, its rim lighter under the pointer and orange while it is the chosen one; a band of light crosses it once as
    /// the pointer arrives, it sinks 2 mock-up pixels while pressed and a ring of light spreads from where it was pressed, both clipped to
    /// its cut corners (work/art-plans/style-1112/ui-e/fx.js). An empty save slot is a striped plate. In whitebox it is a flat rectangle with
    /// a thin edge in WhiteboxSkin's colours, orange while chosen, and the lights are left out. Its Button takes the clicks and the keyboard's
    /// navigation; the hover sound is its own (V5UiSound).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5Plate : V5Block, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public enum Look { Plate, Empty, Go, Key }

        // The lights (fx.js): how long, how bright, which way the band leans.
        const float SweepSeconds = .62f, RingSeconds = .56f, SweepLean = -18f;
        const float SinkUnits = 2f * V5TitleKit.Css, Inset = 3f * V5TitleKit.Css;
        static readonly Color SweepTint = new(1f, .94f, .86f, .36f), WarmRing = new(1f, .84f, .67f, .85f), CoolRing = new(.84f, .89f, 1f, .75f);
        // An empty slot: grey stripes, lighter under the pointer, orange while chosen, seen through a dark fill.
        static readonly Color StripeIdle = V5TitleKit.Rgb(160, 170, 190, .38f), StripeHover = V5TitleKit.Rgb(214, 222, 236, .62f),
            EmptyFill = V5TitleKit.Rgb(12, 14, 28, .78f), Rivet = V5TitleKit.Rgb(195, 202, 216);
        // A plate that does nothing yet (an unknown card, a won run's go button) is a little darker.
        static readonly Color Dimmed = new(.78f, .8f, .86f, 1f);
        // Whitebox: flat fills in WhiteboxSkin's family, and the edge.
        static readonly Color FlatHover = new(.14f, .2f, .29f, 1f), FlatChosen = new(.16f, .22f, .32f, 1f), FlatEmpty = new(.05f, .07f, .12f, .7f),
            FlatKey = new(.2f, .24f, .31f, 1f), EdgeHover = new(.55f, .75f, 1f, .9f);

        /// <summary>Set while the title selects a plate by code, so the selection is not taken for the keyboard's (no sound, no event).</summary>
        internal static bool Quiet;

        Look look = Look.Plate;
        float cut = 16f;
        bool warm, hovered, keyHover, pressed, chosen, waiting, dim;
        Image surface, tab, lift;
        Button button;
        RectTransform back, lights;
        RawImage stripes;
        Image sweep, ring;
        Outline edge;
        float sweepAge = -1f, ringAge = -1f;
        Vector2 ringAt, shownSize;
        bool? shownArt;

        /// <summary>The hover sound's pitch (the menu steps down item by item, the cards along a row).</summary>
        public float HoverPitch { get; set; } = 1f;
        public bool HoverSound { get; set; } = true;
        /// <summary>Raised when the keyboard's (or a pad's) navigation selects it, not the pointer.</summary>
        public event Action<V5Plate> KeyboardSelected;
        public Button Button => button;
        public Look Kind => look;

        /// <summary>A new plate under parent that runs click when clicked; its ring of light is warm on the menu and the go buttons.</summary>
        public static V5Plate Make(Transform parent, string name, Look look, float cutCss, Action click, bool warm = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var plate = go.AddComponent<V5Plate>();
            plate.Setup(look, cutCss, warm);
            if (click != null) plate.button.onClick.AddListener(() => click());
            return plate;
        }

        /// <summary>The same look on a button that exists already (the open mode's map pick): its image becomes the plate, its click stays.</summary>
        public static V5Plate Dress(GameObject existing, Look look, float cutCss, bool warm = false)
        {
            if (!existing.TryGetComponent<V5Plate>(out var plate)) plate = existing.AddComponent<V5Plate>();
            plate.Setup(look, cutCss, warm);
            return plate;
        }

        void Setup(Look newLook, float cutCss, bool warmRing)
        {
            surface = GetComponent<Image>();
            button = GetComponent<Button>();
            cut = cutCss;
            warm = warmRing;
            if (surface) surface.raycastTarget = true;
            if (button)
            {
                button.transition = Selectable.Transition.None;
                button.targetGraphic = surface;
            }
            SetLook(newLook);
        }

        public void SetLook(Look newLook)
        {
            look = newLook;
            if (look == Look.Empty && !back) BuildEmpty();
            Refresh();
        }

        /// <summary>The chosen one of its group: an orange rim and, with a tab, the orange and black tab over its top edge.</summary>
        public bool Chosen
        {
            get => chosen;
            set
            {
                if (chosen == value) return;
                chosen = value;
                Refresh();
            }
        }

        /// <summary>A keycap waiting for a key: dark, rimmed in orange.</summary>
        public bool Waiting
        {
            get => waiting;
            set
            {
                if (waiting == value) return;
                waiting = value;
                Refresh();
            }
        }

        /// <summary>Darker: a card not met yet, a go button that does nothing.</summary>
        public bool Dim
        {
            get => dim;
            set
            {
                if (dim == value) return;
                dim = value;
                Refresh();
            }
        }

        /// <summary>The orange and black tab over the top edge, leftCss mock-up pixels from the left, shown while the plate is chosen.</summary>
        public void AddTab(float leftCss)
        {
            if (tab) return;
            tab = V5TitleKit.Picture(transform, "Tab", V5TitleKit.Piece("hazard-tab"), Color.white);
            var rect = tab.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(leftCss * V5TitleKit.Css, 5f * V5TitleKit.Css);
            rect.sizeDelta = new Vector2(72f, 10f) * V5TitleKit.Css;
            Refresh();
        }

        void BuildEmpty()
        {
            back = V5TitleKit.Rect(transform, "Back");
            V5TitleKit.Stretch(back);
            back.SetAsFirstSibling();
            var shape = back.gameObject.AddComponent<Image>();
            V5TitleKit.Dress(shape, V5TitleKit.Chamfer, Color.white, V5TitleKit.ChamferDensity(cut));
            shape.raycastTarget = false;
            back.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            stripes = V5TitleKit.Raw(back, "Stripes", V5TitleKit.Stripes, StripeIdle);
            V5TitleKit.Stretch(stripes.rectTransform);
            var fill = V5TitleKit.Picture(back, "Fill", V5TitleKit.Chamfer, EmptyFill, V5TitleKit.ChamferDensity(cut));
            V5TitleKit.Stretch(fill.rectTransform, Inset);
            foreach (var corner in new[] { Vector2.one, Vector2.zero })
            {
                var rivet = V5TitleKit.Picture(back, "Rivet", V5TitleKit.Piece("disc"), Rivet);
                var rect = rivet.rectTransform;
                rect.anchorMin = rect.anchorMax = corner;
                rect.pivot = new Vector2(.5f, .5f);
                float from = 11f * V5TitleKit.Css;
                rect.anchoredPosition = new Vector2(corner.x > .5f ? -from : from, corner.y > .5f ? -from : from);
                rect.sizeDelta = Vector2.one * 3f * V5TitleKit.Css;
            }
        }

        void Refresh()
        {
            if (!surface) return;
            bool art = Art.Enabled;
            shownArt = art;
            shownSize = Rect.rect.size;
            if (back && back.gameObject.activeSelf != (look == Look.Empty && art)) back.gameObject.SetActive(look == Look.Empty && art);
            if (art)
            {
                if (edge && edge.enabled) edge.enabled = false;
                switch (look)
                {
                    case Look.Empty:
                        V5TitleKit.Dress(surface, null, Color.clear);
                        stripes.color = chosen ? V5TitleKit.Orange : hovered ? StripeHover : StripeIdle;
                        stripes.uvRect = new Rect(0f, 0f, shownSize.x / V5TitleKit.StripeRepeat, shownSize.y / V5TitleKit.StripeRepeat);
                        break;
                    case Look.Go:
                        V5TitleKit.Dress(surface, V5TitleKit.Piece("button-go"), dim ? Dimmed * new Color(.75f, .75f, .75f, 1f) : Color.white, V5TitleKit.PlateDensity(cut));
                        break;
                    case Look.Key:
                        V5TitleKit.Dress(surface, V5TitleKit.Piece(waiting ? "keycap-dark" : "keycap"), waiting ? new Color(1f, .62f, .45f, 1f) : Color.white, V5TitleKit.PieceDensity);
                        break;
                    default:
                        V5TitleKit.Dress(surface, V5TitleKit.Piece(chosen ? "plate-selected" : hovered ? "plate-hover" : "plate"), dim ? Dimmed : Color.white,
                            V5TitleKit.PlateDensity(cut));
                        break;
                }
            }
            else
            {
                V5TitleKit.Dress(surface, null, look switch
                {
                    Look.Empty => FlatEmpty,
                    Look.Go => dim ? WhiteboxSkin.ButtonFill : WhiteboxSkin.PrimaryFill,
                    Look.Key => FlatKey,
                    _ => chosen ? FlatChosen : hovered ? FlatHover : WhiteboxSkin.ButtonFill,
                });
                if (!edge)
                {
                    edge = surface.gameObject.AddComponent<Outline>();
                    edge.useGraphicAlpha = false;
                    edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
                }
                if (!edge.enabled) edge.enabled = true;
                var colour = chosen || waiting ? V5TitleKit.Orange : hovered ? EdgeHover : WhiteboxSkin.Edge;
                if (edge.effectColor != colour) edge.effectColor = colour;
                StopLights();
            }
            if (tab)
            {
                bool shown = chosen;
                if (tab.gameObject.activeSelf != shown) tab.gameObject.SetActive(shown);
                V5TitleKit.Dress(tab, art ? V5TitleKit.Piece("hazard-tab") : null, art ? Color.white : V5TitleKit.Orange);
            }
            bool lifted = art && hovered && (chosen || look == Look.Go) && !dim;
            if (lifted && !lift)
            {
                lift = V5TitleKit.Picture(transform, "Lift", V5TitleKit.Chamfer, Color.clear, V5TitleKit.ChamferDensity(cut));
                V5TitleKit.Stretch(lift.rectTransform);
                lift.transform.SetSiblingIndex(back ? 1 : 0);
            }
            if (lift) lift.color = new Color(1f, 1f, 1f, lifted ? .08f : 0f);
        }

        bool Live => !button || button.IsInteractable();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Live) Hover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
            if (!hovered || keyHover) return;
            hovered = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Live || eventData.button != PointerEventData.InputButton.Left) return;
            pressed = true;
            Sink = SinkUnits;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, eventData.position, eventData.pressEventCamera, out var local)) StartRing(local);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnSelect(BaseEventData eventData)
        {
            // The pointer selects what it presses, and the title selects by code: only the keyboard's navigation counts here.
            if (Quiet || Mouse.current != null && Mouse.current.leftButton.isPressed) return;
            keyHover = true;
            Hover(true);
            KeyboardSelected?.Invoke(this);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!keyHover) return;
            keyHover = false;
            hovered = false;
            Refresh();
        }

        void Hover(bool sound)
        {
            if (hovered) return;
            hovered = true;
            Refresh();
            StartSweep();
            if (sound && HoverSound) V5UiSound.Play(V5UiSound.Ui.Hover, HoverPitch);
        }

        void Release()
        {
            if (!pressed) return;
            pressed = false;
            Sink = 0f;
        }

        void OnDisable()
        {
            Release();
            hovered = keyHover = false;
            StopLights();
            if (surface) Refresh();
        }

        // ───── The lights, clipped to the plate's cut corners ─────

        void StartSweep()
        {
            if (!Art.Enabled || look == Look.Key || !isActiveAndEnabled) return;
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
            ringAt = local - Rect.rect.center;
            ring.gameObject.SetActive(true);
            DrawRing();
        }

        void Lights()
        {
            if (lights)
            {
                if (!lights.gameObject.activeSelf) lights.gameObject.SetActive(true);
                lights.SetAsLastSibling();
                return;
            }
            lights = V5TitleKit.Rect(transform, "Lights");
            V5TitleKit.Stretch(lights, Inset);
            var shape = lights.gameObject.AddComponent<Image>();
            V5TitleKit.Dress(shape, V5TitleKit.Chamfer, Color.white, V5TitleKit.ChamferDensity(cut));
            shape.raycastTarget = false;
            lights.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            sweep = V5TitleKit.Picture(lights, "Sweep", V5TitleKit.Piece("sweep"), Color.clear);
            var band = sweep.rectTransform;
            band.anchorMin = band.anchorMax = new Vector2(0f, .5f);
            band.pivot = new Vector2(.5f, .5f);
            band.localRotation = Quaternion.Euler(0f, 0f, SweepLean);
            ring = V5TitleKit.Picture(lights, "Ring", V5TitleKit.Piece("ripple"), Color.clear);
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = ring.rectTransform.pivot = new Vector2(.5f, .5f);
            sweep.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
        }

        void StopLights()
        {
            sweepAge = ringAge = -1f;
            if (lights && lights.gameObject.activeSelf) lights.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (shownArt != Art.Enabled || look == Look.Empty && Rect.rect.size != shownSize) Refresh();
            if (sweepAge < 0f && ringAge < 0f) return;
            float dt = Time.unscaledDeltaTime;
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
            if (t >= 1f || !sweep)
            {
                sweepAge = -1f;
                if (sweep) sweep.gameObject.SetActive(false);
                return;
            }
            float p = V5TitleKit.Bezier(t, .3f, .6f, .3f, 1f);
            var size = lights.rect.size;
            float width = size.x * .46f;
            var band = sweep.rectTransform;
            band.sizeDelta = new Vector2(width, size.y * 1.4f + width);
            band.anchoredPosition = new Vector2(Mathf.Lerp(-1.1f, 2.3f, p) * width + width * .5f, 0f);
            float alpha = p < .2f ? p / .2f : p < .75f ? 1f : (1f - p) / .25f;
            var colour = SweepTint;
            colour.a *= Mathf.Clamp01(alpha);
            sweep.color = colour;
        }

        void DrawRing()
        {
            float t = ringAge / RingSeconds;
            if (t >= 1f || !ring)
            {
                ringAge = -1f;
                if (ring) ring.gameObject.SetActive(false);
                return;
            }
            float p = V5TitleKit.Bezier(t, .15f, .7f, .3f, 1f);
            var size = Rect.rect.size;
            float diameter = Mathf.Sqrt(size.x * size.x + size.y * size.y) * 1.15f * Mathf.Lerp(.06f, 1f, p);
            var rect = ring.rectTransform;
            rect.sizeDelta = new Vector2(diameter, diameter);
            rect.anchoredPosition = ringAt;
            float alpha = p < .45f ? Mathf.Lerp(1f, .8f, p / .45f) : Mathf.Lerp(.8f, 0f, (p - .45f) / .55f);
            var colour = warm ? WarmRing : CoolRing;
            colour.a *= alpha;
            ring.color = colour;
        }
    }
}
