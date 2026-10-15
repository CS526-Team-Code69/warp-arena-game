using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(650)]
    public sealed class V3TitleBackdrop : MonoBehaviour
    {
        [Header("Saved title-only graphics")]
        public UnityEngine.UI.RawImage background;
        public UnityEngine.UI.Image logo;
        public UnityEngine.UI.Image warpArcs;
        public UnityEngine.UI.Image[] softGlows;
        public UnityEngine.UI.Image[] dust;
        public UnityEngine.UI.Button initialSelection;
        [Header("Lightweight background motion without changing game time.")]
        [Tooltip("Disabling motion retains the static title and saved art objects.")] public bool animate = true;
        [Range(0f, .025f), Tooltip("Small offset amplitude for the background texture coordinates.")] public float parallaxAmount = .007f;
        [Range(0f, .45f), Tooltip("Brighten the existing background on the title screen; restore its color on exit.")] public float backgroundLift = .18f;
        [Tooltip("Rotation speed of background distortion per second.")] public float arcDegreesPerSecond = 1.6f;
        [Tooltip("Ambient particle drift speed in reference Canvas units per second.")] public float dustRiseSpeed = 9f;
        [Range(0f, .02f), Tooltip("Pulse amount for decorative light strips.")] public float logoBreath = .005f;
        Rect backgroundUv;
        Color backgroundColor;
        Color[] dustColor, glowColor;
        Vector2[] dustOrigin;
        Vector3 logoScale;
        Quaternion arcRotation;
        Vector2 pointer;
        float started;
        bool captured, selectionPending;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            started = Time.unscaledTime; pointer = Vector2.zero; selectionPending = true;
            if (background) { backgroundUv = background.uvRect; backgroundColor = background.color; }
            if (logo) logoScale = logo.rectTransform.localScale;
            if (warpArcs) arcRotation = warpArcs.rectTransform.localRotation;
            int count = dust?.Length ?? 0;
            dustOrigin = new Vector2[count]; dustColor = new Color[count];
            for (int i = 0; i < count; i++)
                if (dust[i]) { dustOrigin[i] = dust[i].rectTransform.anchoredPosition; dustColor[i] = dust[i].color; }
            count = softGlows?.Length ?? 0; glowColor = new Color[count];
            for (int i = 0; i < count; i++) if (softGlows[i]) glowColor[i] = softGlows[i].color;
            captured = true;
        }
        void LateUpdate()
        {
            if (!Application.isPlaying || !captured) return;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (selectionPending && ArenaText.Ready && events && initialSelection && initialSelection.IsInteractable())
            {
                var selected = events.currentSelectedGameObject;
                if (!selected || !selected.transform.IsChildOf(transform)) events.SetSelectedGameObject(initialSelection.gameObject);
                selectionPending = false;
            }
            if (!animate) return;
            float time = Time.unscaledTime - started;
            Vector2 target = Vector2.zero;
            if (Mouse.current != null && Screen.width > 0 && Screen.height > 0)
            {
                var position = Mouse.current.position.ReadValue();
                target = new Vector2(Mathf.Clamp(position.x / Screen.width - .5f, -.5f, .5f), Mathf.Clamp(position.y / Screen.height - .5f, -.5f, .5f));
            }
            pointer = Vector2.Lerp(pointer, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2f));
            if (background)
            {
                float margin = Mathf.Clamp(parallaxAmount, 0f, .025f);
                background.uvRect = new Rect(backgroundUv.x + margin + pointer.x * margin, backgroundUv.y + margin + pointer.y * margin,
                    Mathf.Max(.01f, backgroundUv.width - margin * 2f), Mathf.Max(.01f, backgroundUv.height - margin * 2f));
                background.color = Color.Lerp(backgroundColor, new Color(1f, 1f, 1f, backgroundColor.a), backgroundLift);
            }
            if (logo) logo.rectTransform.localScale = logoScale * (1f + Mathf.Sin(time * .55f) * logoBreath);
            if (warpArcs) warpArcs.rectTransform.localRotation = arcRotation * Quaternion.Euler(0f, 0f, time * arcDegreesPerSecond);
            for (int i = 0; i < (softGlows?.Length ?? 0); i++)
            {
                if (!softGlows[i]) continue;
                var tint = glowColor[i]; tint.a *= .83f + .17f * Mathf.Sin(time * .37f + i * 2.4f); softGlows[i].color = tint;
            }
            for (int i = 0; i < (dust?.Length ?? 0); i++)
            {
                if (!dust[i]) continue;
                var rect = dust[i].rectTransform;
                float height = rect.parent is RectTransform parent ? Mathf.Max(200f, parent.rect.height) : 900f;
                float speed = dustRiseSpeed * (.7f + (i % 5) * .13f);
                float y = Mathf.Repeat(dustOrigin[i].y + height * .5f + time * speed, height) - height * .5f;
                rect.anchoredPosition = new Vector2(dustOrigin[i].x + Mathf.Sin(time * .13f + i * 1.4f) * (9f + i % 4 * 4f), y);
                var tint = dustColor[i];
                float edge = Mathf.Clamp01((height * .5f - Mathf.Abs(y)) / 80f);
                tint.a *= edge * (.72f + .28f * Mathf.Sin(time * .48f + i)); dust[i].color = tint;
            }
        }
        void OnDisable()
        {
            if (!captured) return;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events && events.currentSelectedGameObject && events.currentSelectedGameObject.transform.IsChildOf(transform)) events.SetSelectedGameObject(null);
            if (background) { background.uvRect = backgroundUv; background.color = backgroundColor; }
            if (logo) logo.rectTransform.localScale = logoScale;
            if (warpArcs) warpArcs.rectTransform.localRotation = arcRotation;
            for (int i = 0; i < (dust?.Length ?? 0); i++) if (dust[i]) { dust[i].rectTransform.anchoredPosition = dustOrigin[i]; dust[i].color = dustColor[i]; }
            for (int i = 0; i < (softGlows?.Length ?? 0); i++) if (softGlows[i]) softGlows[i].color = glowColor[i];
            captured = false;
        }
    }
}
