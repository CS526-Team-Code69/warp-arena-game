using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>Animates only saved title graphics. Restores the shared background before another screen uses it.</summary>
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
        [Header("轻量背景动态；不改变游戏时间")]
        [Tooltip("关闭后保留静态标题，不删除已保存的素材物件。")] public bool animate = true;
        [Range(0f, .025f), Tooltip("原背景纹理坐标的细微偏移幅度。")] public float parallaxAmount = .007f;
        [Range(0f, .45f), Tooltip("仅开始页略微提亮现有背景；离开恢复原色。")] public float backgroundLift = .18f;
        [Tooltip("背景扭曲弧每秒旋转角度。")] public float arcDegreesPerSecond = 1.6f;
        [Tooltip("保存的尘点漂浮速度，参考Canvas单位每秒。")] public float dustRiseSpeed = 9f;
        [Range(0f, .02f), Tooltip("标题缓慢呼吸的比例幅度。")] public float logoBreath = .005f;
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
        // ───── Whitebox (2026-11-10: the interface's pictures become plain shapes and colours) ─────
        // The title art itself is covered by V2Hud's WhiteboxBackdrop (the background is V2Hud.themeBackground). Here the drawn logo gives
        // way to the game's name in plain text over the logo's own box, and the decorations over the art (the warp arcs, the soft glows,
        // the dust) are hidden; switching back shows them as they were saved.
        const string Wordmark = "WARP ARENA";
        const int WordmarkLargest = 150, WordmarkSmallest = 40;
        UnityEngine.UI.Text wordmark;
        bool? shownArt;

        void ApplyArt()
        {
            bool art = Art.Enabled;
            if (!art && !wordmark && logo) wordmark = MakeWordmark();
            if (wordmark && !wordmark.font) wordmark.font = WordmarkFont();
            if (wordmark) FollowLogo();
            if (shownArt == art) return;
            shownArt = art;
            if (logo) logo.enabled = art;
            if (wordmark) wordmark.gameObject.SetActive(!art);
            if (warpArcs) warpArcs.enabled = art;
            foreach (var glow in softGlows ?? System.Array.Empty<UnityEngine.UI.Image>()) if (glow) glow.enabled = art;
            foreach (var mote in dust ?? System.Array.Empty<UnityEngine.UI.Image>()) if (mote) mote.enabled = art;
        }

        /// <summary>The wordmark stays over the logo's box, which the whole game's title moves onto its grid and sizes by the language
        /// (V5Title): left-aligned when the box is anchored at its left, centred otherwise.</summary>
        void FollowLogo()
        {
            if (!logo) return;
            var box = logo.rectTransform;
            var rect = wordmark.rectTransform;
            if (rect.parent != box.parent)
            {
                rect.SetParent(box.parent, false);
                rect.SetSiblingIndex(box.GetSiblingIndex() + 1);
            }
            if (rect.anchorMin != box.anchorMin) rect.anchorMin = box.anchorMin;
            if (rect.anchorMax != box.anchorMax) rect.anchorMax = box.anchorMax;
            if (rect.pivot != box.pivot) rect.pivot = box.pivot;
            if (rect.anchoredPosition != box.anchoredPosition) rect.anchoredPosition = box.anchoredPosition;
            if (rect.sizeDelta != box.sizeDelta) rect.sizeDelta = box.sizeDelta;
            var anchor = box.pivot.x < .25f ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            if (wordmark.alignment != anchor) wordmark.alignment = anchor;
        }

        /// <summary>The wordmark's font is Unity's built-in one, not the game's. Its letters are several hundred pixels tall; in the
        /// game's font they grew that font's texture four times in one frame on a fresh start in whitebox (512 x 512 to 1024 x 2048)
        /// and the key hints under the title kept glyph positions of a texture that was gone (2026-11-10, see FontAtlasGuard). The
        /// built-in font has a texture of its own, so the wordmark leaves the texts of the game's font alone. The game's font only
        /// stands in where the built-in one is missing.</summary>
        static Font WordmarkFont()
        {
            var builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return builtIn ? builtIn : ArenaText.Font;
        }

        UnityEngine.UI.Text MakeWordmark()
        {
            var box = logo.rectTransform;
            var text = new GameObject("WhiteboxWordmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            var rect = text.rectTransform;
            rect.SetParent(box.parent, false);
            rect.SetSiblingIndex(box.GetSiblingIndex() + 1);
            rect.anchorMin = box.anchorMin; rect.anchorMax = box.anchorMax; rect.pivot = box.pivot;
            rect.anchoredPosition = box.anchoredPosition; rect.sizeDelta = box.sizeDelta;
            text.font = WordmarkFont();
            text.text = Wordmark; // the game's name is English in every language (V2Hud's title)
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.92f, .96f, 1f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = WordmarkSmallest;
            text.resizeTextMaxSize = WordmarkLargest;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            var shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .6f);
            shadow.effectDistance = new Vector2(2f, -3f);
            return text;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !captured) return;
            ApplyArt();
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
