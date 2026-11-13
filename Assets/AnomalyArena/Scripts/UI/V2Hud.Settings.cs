using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The two buttons that open the settings panel (ArenaSettingsPanel: volume, art mode, keys). On the title it stands in the
    /// top-left corner, the counterpart of the Language option in the top-right one; it took the place of the art switch that stood
    /// under the entries and of the line of key hints along the bottom, which are both saved in the scene and no longer shown. On the
    /// pause screen it stands in the panel's top-right corner, beside the heading, clear of the row of four buttons under the table.
    /// </summary>
    public partial class V2Hud
    {
        /// <summary>The title's Settings button, a copy of the saved button the Language option is copied from.</summary>
        public const string TitleSettingsButton = "TitleSettings";
        /// <summary>The pause screen's Settings button, a copy of its restart button.</summary>
        public const string PauseSettingsButton = "PauseSettings";

        // The pause screen's button in the saved layout (without the menu skin, which places it itself): its size and its distance
        // from the panel's top-right corner. The saved clock label that lies there is never shown while paused.
        const float PauseSettingsWidth = 190f, PauseSettingsHeight = 46f, PauseSettingsRight = 44f, PauseSettingsTop = 36f;

        RectTransform titleSettings;
        UnityEngine.UI.Text titleSettingsLabel, pauseSettingsLabel;

        /// <summary>Made in Awake with the title's other buttons, before the menu skin dresses them by name (TitleSecondaryButtons).
        /// It mirrors the Language option: the same size, the same distance from its corner.</summary>
        void AddTitleSettingsButton()
        {
            var content = TitleContent();
            if (!content || content.Find(TitleSettingsButton)) return;
            var original = content.Find("EnglishShowcase") ?? content.Find("ToggleArt");
            if (!original) return;
            var button = CopyAsPlainButton(original, TitleSettingsButton, OpenSettings);
            if (button.TryGetComponent<ArenaLocaleButton>(out var locale)) Destroy(locale);
            button.gameObject.SetActive(true);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(LanguageWidth, LanguageHeight);
            rect.anchoredPosition = new Vector2(LanguageRight, -LanguageTop);
            // The saved rules above and under the label are as wide as the button they came from.
            foreach (Transform child in rect)
                if (child is RectTransform rule && child.name.Contains("Rule") && rule.anchorMin.x == rule.anchorMax.x)
                    rule.sizeDelta = new Vector2(LanguageWidth - 16f, rule.sizeDelta.y);
            titleSettings = rect;
            titleSettingsLabel = rect.GetComponentInChildren<UnityEngine.UI.Text>(true);
        }

        /// <summary>Made with the pause screen's tutorial button. The run stays paused under the panel, and closing it shows the pause
        /// screen again (the panel holds the game's keys meanwhile, so its Esc does not resume).</summary>
        void AddPauseSettingsButton(Transform content)
        {
            // The Alpha page offers no settings (BuildEdition): it stays silent, at the volume's default.
            if (BuildEdition.IsAlpha || content.Find(PauseSettingsButton) || content.Find("RestartRun") is not { } restart) return;
            var button = CopyAsPlainButton(restart, PauseSettingsButton, OpenSettings);
            pauseSettingsLabel = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (content is RectTransform panel)
                PlaceTopLeft((RectTransform)button.transform, panel.sizeDelta.x - PauseSettingsRight - PauseSettingsWidth, PauseSettingsTop, PauseSettingsWidth, PauseSettingsHeight);
        }

        void OpenSettings()
        {
            V2Audio.Instance?.PlayUI(ArenaUiSound.Select);
            ArenaSettingsPanel.Open(appliedFont);
        }

        /// <summary>The pause screen's button in the language showing and the pause screen's font.</summary>
        void RenderPauseSettings(Font font)
        {
            if (!pauseSettingsLabel) return;
            SetText(pauseSettingsLabel, ArenaText.Pair("设置", "SETTINGS"));
            if (font && pauseSettingsLabel.font != font) pauseSettingsLabel.font = font;
        }
    }
}
