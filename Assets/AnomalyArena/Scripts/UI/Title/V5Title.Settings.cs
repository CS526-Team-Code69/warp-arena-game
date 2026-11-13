using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    public sealed partial class V5Title
    {
        // ───── The settings: the volume in ten steps, the art mode (on the title only, as the settings panel), the language, the keys in
        // two columns with ArenaKeys' rules and the settings panel's words, and Reset. The pause screen keeps its own panel
        // (ArenaSettingsPanel). ─────

        const float SetRowH = 30f, SetRowGap = 6f, SetLabelW = 48f, ControlLeft = 60f, ToggleW = 213f, ToggleH = 28.5f;
        const float KeyRowH = 30f * Css, KeyRowGap = 5f * Css, KeyColumnGap = 22f * Css, KeyCapW = 48f * Css, KeyCapH = 26f * Css;
        const float MessageSeconds = 3.2f;
        const int LeftKeys = 6;

        sealed class ToggleView
        {
            public RectTransform root;
            public Image plate, thumb;
            public Outline edge;
            public readonly Button[] options = new Button[2];
            public readonly Text[] labels = new Text[2];
            public int shown = -1;
        }

        sealed class KeyRow
        {
            public RectTransform root;
            public Text name, label;
            public V5Plate key;
            public Image line;
        }

        V5Block volumeRow, artRow, languageRow, keysHead, keyGrid, fixedBlock;
        Text volumeLabel, artLabel, languageLabel, keysTitle, keysHint, fixedNote;
        RawImage speaker, settingsRule;
        Slider volume;
        Image volumeTrack, volumeKnob;
        readonly Image[] volumeCells = new Image[10];
        ToggleView artToggle, languageToggle;
        readonly KeyRow[] keyRows = new KeyRow[ArenaKeys.Count];
        int listening = -1, stoppedFrame = -1, shownVolume = -1;
        string message = "";
        float messageUntil = -1f;
        bool volumeMoved;

        static bool ArtSwitchable => GameManager.Instance && GameManager.Instance.RunPhase == ArenaUiState.Title && !Art.IllustratedOnly && !Art.WhiteboxOnly;

        // ───── Open to tests, as ArenaSettingsPanel's entry points: device keys never reach the game in the batch test runner ─────

        /// <summary>A key row of the title's settings waits for a key.</summary>
        public static bool IsListening(out GameAction action)
        {
            action = default;
            if (!active || active.listening < 0) return false;
            action = (GameAction)active.listening;
            return true;
        }

        /// <summary>What a key pressed while a row waits does, the key given by its name in the keyboard's own list ("G", "Tab").</summary>
        public static KeyBindResult TakeKeyByName(string keyName)
        {
            if (!active || active.listening < 0 || string.IsNullOrEmpty(keyName) || char.IsDigit(keyName[0])
                || !Enum.TryParse(keyName, out Key key) || !Enum.IsDefined(typeof(Key), key)) return KeyBindResult.Refused;
            return active.Take(key);
        }

        /// <summary>The title's settings hold the game's keys while a row waits, and on the frame the waiting stops, so a key pressed for a
        /// binding does nothing else (ArenaSettingsPanel.HoldsInput asks here).</summary>
        public static bool HoldsKeys => active && (active.listening >= 0 || active.stoppedFrame == Time.frameCount);

        // ───── Building ─────

        void BuildSettings()
        {
            var page = pageRoots[SettingsTab];
            volumeRow = Block(page, "Volume", 0f, 0f, InnerW, SetRowH);
            volumeLabel = RowLabel(volumeRow.transform);
            speaker = V5TitleKit.Raw(volumeRow.transform, "Speaker", V5TitleKit.Speaker, V5TitleKit.Ink2);
            V5TitleKit.Place(speaker.rectTransform, ControlLeft, (SetRowH - 19.5f) * .5f, 19.5f, 19.5f);
            volume = MakeVolume(volumeRow.transform, ControlLeft + 19.5f + 9f, InnerW - ControlLeft - 28.5f);
            artRow = Block(page, "Art", 0f, SetRowH + SetRowGap, InnerW, SetRowH);
            artLabel = RowLabel(artRow.transform);
            artToggle = MakeToggle(artRow.transform, "Illustrated", "Whitebox", option => ChooseArt(option == 0));
            languageRow = Block(page, "Language", 0f, 2f * (SetRowH + SetRowGap), InnerW, SetRowH);
            languageLabel = RowLabel(languageRow.transform);
            languageToggle = MakeToggle(languageRow.transform, "Chinese", "English", option => ChooseLanguage(option == 0));
            float ruleTop = 3f * SetRowH + 2f * SetRowGap + 9f;
            settingsRule = V5TitleKit.Raw(page, "Rule", null, V5TitleKit.Rgb(163, 172, 190, .18f));
            V5TitleKit.Place(settingsRule.rectTransform, 0f, ruleTop, InnerW, 1f);
            keysHead = Block(page, "KeysHead", 0f, ruleTop + 10f, InnerW, 16.5f);
            keysTitle = V5TitleKit.Label(keysHead.transform, "Title", 13, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            V5TitleKit.Place(keysTitle.rectTransform, 0f, 0f, 60f, 16.5f);
            keysHint = V5TitleKit.Label(keysHead.transform, "Hint", 10, V5TitleKit.Ink3, TextAnchor.MiddleRight);
            V5TitleKit.Place(keysHint.rectTransform, 66f, 0f, InnerW - 66f, 16.5f);
            float gridTop = ruleTop + 10f + 16.5f + 7.5f, gridH = LeftKeys * KeyRowH + (LeftKeys - 1) * KeyRowGap;
            keyGrid = Block(page, "Keys", 0f, gridTop, InnerW, gridH);
            float columnW = (InnerW - KeyColumnGap) * .5f;
            for (int i = 0; i < keyRows.Length; i++)
            {
                var action = (GameAction)i;
                int column = i < LeftKeys ? 0 : 1, line = column == 0 ? i : i - LeftKeys;
                var row = new KeyRow { root = V5TitleKit.Rect(keyGrid.transform, "Key" + action) };
                V5TitleKit.Place(row.root, column * (columnW + KeyColumnGap), line * (KeyRowH + KeyRowGap), columnW, KeyRowH);
                row.name = V5TitleKit.Label(row.root, "Name", 11, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
                V5TitleKit.Place(row.name.rectTransform, 3f, 0f, columnW - 3f - KeyCapW - 6f, KeyRowH);
                row.key = V5Plate.Make(row.root, "Key", V5Plate.Look.Key, 4f, () => Listen(action));
                row.key.HoverPitch = 1.05f - line * .02f;
                row.label = V5TitleKit.Label(row.key.transform, "Label", 10, KeyInk, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(row.label.rectTransform);
                row.line = V5TitleKit.Picture(row.root, "Line", null, V5TitleKit.Rgb(163, 172, 190, .12f));
                V5TitleKit.Place(row.line.rectTransform, 0f, KeyRowH - 1f, columnW, 1f);
                keyRows[i] = row;
            }
            float noteTop = gridTop + gridH + 7.5f;
            fixedBlock = Block(page, "Fixed", 0f, noteTop, InnerW, BodyH - noteTop);
            fixedNote = V5TitleKit.Label(fixedBlock.transform, "Note", 10, V5TitleKit.Ink3, TextAnchor.UpperLeft);
            V5TitleKit.Wrap(fixedNote);
            fixedNote.lineSpacing = 1.05f;
            V5TitleKit.Stretch(fixedNote.rectTransform);
        }

        static Text RowLabel(Transform row)
        {
            var label = V5TitleKit.Label(row, "Label", 13, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            V5TitleKit.Place(label.rectTransform, 0f, 0f, SetLabelW, SetRowH);
            return label;
        }

        /// <summary>Ten steps on a bar of ten cells, a knob over it, no number: a click or a drag sets it, and every step ticks.</summary>
        Slider MakeVolume(Transform parent, float left, float width)
        {
            var root = V5TitleKit.Rect(parent, "Slider");
            V5TitleKit.Place(root, left, 0f, width, SetRowH);
            var slider = root.gameObject.AddComponent<Slider>();
            volumeTrack = V5TitleKit.Picture(root, "Track", V5TitleKit.Piece("bar-track"), Color.white, V5TitleKit.PieceDensity);
            volumeTrack.raycastTarget = true;
            V5TitleKit.Place(volumeTrack.rectTransform, 0f, (SetRowH - 12f) * .5f, width, 12f);
            float cellW = (width - 3f - 9f * 1.5f) / 10f;
            for (int c = 0; c < 10; c++)
            {
                volumeCells[c] = V5TitleKit.Picture(volumeTrack.transform, "Cell" + (c + 1), V5TitleKit.Piece("bar-fill"), V5TitleKit.OrangeHi, V5TitleKit.PieceDensity);
                V5TitleKit.Place(volumeCells[c].rectTransform, 1.5f + c * (cellW + 1.5f), 1.5f, cellW, 9f);
            }
            var area = V5TitleKit.Rect(root, "Handle Slide Area");
            V5TitleKit.Stretch(area);
            area.offsetMin = new Vector2(6.75f, 0f);
            area.offsetMax = new Vector2(-6.75f, 0f);
            volumeKnob = V5TitleKit.Picture(area, "Knob", V5TitleKit.Piece("plate"), Color.white, V5TitleKit.PlateDensity(5f));
            volumeKnob.raycastTarget = true;
            var knob = volumeKnob.rectTransform;
            knob.anchorMin = Vector2.zero;
            knob.anchorMax = new Vector2(0f, 1f);
            knob.sizeDelta = new Vector2(13.5f, -4.5f);
            slider.transition = Selectable.Transition.None;
            slider.targetGraphic = volumeKnob;
            slider.handleRect = knob;
            slider.fillRect = null;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 10f;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(OnVolume);
            return slider;
        }

        ToggleView MakeToggle(Transform row, string first, string second, Action<int> choose)
        {
            var view = new ToggleView { root = V5TitleKit.Rect(row, "Toggle") };
            V5TitleKit.Place(view.root, ControlLeft, (SetRowH - ToggleH) * .5f, ToggleW, ToggleH);
            view.plate = V5TitleKit.Picture(view.root, "Plate", V5TitleKit.Piece("plate"), Color.white, V5TitleKit.PlateDensity(9f));
            V5TitleKit.Stretch(view.plate.rectTransform);
            view.thumb = V5TitleKit.Picture(view.root, "Thumb", V5TitleKit.Piece("button-go"), Color.white, V5TitleKit.PlateDensity(7f));
            for (int i = 0; i < 2; i++)
            {
                int option = i;
                var hit = V5TitleKit.Picture(view.root, i == 0 ? first : second, null, Color.clear);
                hit.raycastTarget = true;
                V5TitleKit.Place(hit.rectTransform, i * ToggleW * .5f, 0f, ToggleW * .5f, ToggleH);
                var button = hit.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = hit;
                button.onClick.AddListener(() => choose(option));
                view.options[i] = button;
                view.labels[i] = V5TitleKit.Label(hit.transform, "Label", 13, V5TitleKit.Ink2, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(view.labels[i].rectTransform, 3f);
            }
            PlaceThumb(view, 0f);
            return view;
        }

        static void PlaceThumb(ToggleView view, float at)
        {
            float w = (ToggleW - 6f) * .5f;
            V5TitleKit.Place(view.thumb.rectTransform, 3f + at * w, 3f, w, ToggleH - 6f);
        }

        // ───── Showing ─────

        void RenderSettings()
        {
            bool on = Art.Enabled;
            volumeLabel.text = ArenaText.Pair("音量", "Volume");
            artLabel.text = ArenaText.Pair("画面", "Art mode");
            languageLabel.text = ArenaText.Pair("语言", "Language");
            foreach (var label in new[] { volumeLabel, artLabel, languageLabel }) V5TitleKit.FitWidth(label, 13, 9);
            V5TitleKit.Dress(volumeTrack, on ? V5TitleKit.Piece("bar-track") : null, on ? Color.white : V5TitleKit.Rgb(10, 13, 21), V5TitleKit.PieceDensity);
            V5TitleKit.Dress(volumeKnob, on ? V5TitleKit.Piece("plate") : null, on ? Color.white : V5TitleKit.Ink, V5TitleKit.PlateDensity(5f));
            shownVolume = -1;
            ShowVolume(Mathf.RoundToInt(ArenaVolume.Master * 10f));
            DressToggle(artToggle, on);
            DressToggle(languageToggle, on);
            artToggle.labels[0].text = ArenaText.Pair("插画", "Illustrated");
            artToggle.labels[1].text = ArenaText.Pair("白盒", "Whitebox");
            // Each language by its own name, in either language.
            languageToggle.labels[0].text = ArenaText.Pair("中文", "中文");
            languageToggle.labels[1].text = ArenaText.Pair("English", "English");
            bool switchable = ArtSwitchable;
            foreach (var option in artToggle.options) option.interactable = switchable;
            artToggle.shown = languageToggle.shown = -1;
            FollowToggle(artToggle, Art.Enabled ? 0 : 1);
            FollowToggle(languageToggle, ArenaText.IsChinese ? 0 : 1);
            keysTitle.text = ArenaText.Pair("按键", "Keys");
            RenderKeys();
            fixedNote.text = ArenaSettingsPanel.FixedNote;
            ArenaTextFit.Refit(fixedNote, 10, 8);
            ShowFoot(FootUse.Reset);
            resetLabel.text = ArenaText.Pair("恢复默认按键", "Reset Keys");
            V5TitleKit.FitWidth(resetLabel, 17, 12);
        }

        void DressToggle(ToggleView view, bool on)
        {
            V5TitleKit.Dress(view.plate, on ? V5TitleKit.Piece("plate") : null, on ? Color.white : WhiteboxSkin.ButtonFill, V5TitleKit.PlateDensity(9f));
            V5TitleKit.Dress(view.thumb, on ? V5TitleKit.Piece("button-go") : null, on ? Color.white : V5TitleKit.Orange, V5TitleKit.PlateDensity(7f));
            if (!on && !view.edge)
            {
                view.edge = view.plate.gameObject.AddComponent<Outline>();
                view.edge.useGraphicAlpha = false;
                view.edge.effectColor = WhiteboxSkin.Edge;
                view.edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (view.edge) view.edge.enabled = !on;
        }

        void FollowToggle(ToggleView view, int value)
        {
            if (view.shown == value) return;
            int from = view.shown;
            view.shown = value;
            for (int i = 0; i < 2; i++)
            {
                view.labels[i].color = i == value ? Art.Enabled ? V5TitleKit.Dark : Color.white : V5TitleKit.Ink2;
                V5TitleKit.FitWidth(view.labels[i], 13, 9);
            }
            if (from < 0)
            {
                PlaceThumb(view, value);
                return;
            }
            Animate(view, .2f, 0f, t => V5TitleKit.Bezier(t, .3f, .9f, .3f, 1f), p => PlaceThumb(view, Mathf.Lerp(from, value, p)));
        }

        void RenderKeys()
        {
            bool on = Art.Enabled;
            float columnW = (InnerW - KeyColumnGap) * .5f;
            for (int i = 0; i < keyRows.Length; i++)
            {
                var row = keyRows[i];
                bool waiting = listening == i;
                row.name.text = ArenaSettingsPanel.ActionName((GameAction)i);
                V5TitleKit.FitWidth(row.name, 11, 8);
                row.key.Waiting = waiting;
                row.label.text = waiting ? ArenaText.Pair("按一个键…", "Press a key…") : ArenaKeys.Name((GameAction)i);
                row.label.fontSize = waiting ? 9 : 10;
                row.label.color = waiting ? V5TitleKit.OrangeHi : on ? KeyInk : V5TitleKit.Ink;
                float w = Mathf.Max(KeyCapW, row.label.preferredWidth + 9f);
                row.key.Place(columnW - 1.5f - w, (KeyRowH - KeyCapH) * .5f, w, KeyCapH);
            }
            RenderKeysHint();
        }

        void RenderKeysHint()
        {
            bool shown = message.Length > 0 && Time.unscaledTime <= messageUntil;
            keysHint.text = shown ? message : ArenaSettingsPanel.KeysHint(listening >= 0);
            keysHint.color = shown ? V5TitleKit.OrangeHi : V5TitleKit.Ink3;
            V5TitleKit.FitWidth(keysHint, 10, 7);
        }

        void ShowVolume(int step)
        {
            step = Mathf.Clamp(step, 0, 10);
            if (step == shownVolume) return;
            shownVolume = step;
            volume.SetValueWithoutNotify(step);
            bool on = Art.Enabled;
            for (int c = 0; c < 10; c++)
            {
                Show(volumeCells[c], c < step);
                V5TitleKit.Dress(volumeCells[c], on ? V5TitleKit.Piece("bar-fill") : null, V5TitleKit.OrangeHi, V5TitleKit.PieceDensity);
            }
        }

        // ───── Acting ─────

        void OnVolume(float value)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(value), 0, 10);
            ArenaVolume.Set(step / 10f);
            volumeMoved = true;
            ShowVolume(step);
            // The tick follows the value; at nothing it is silent anyway.
            V5UiSound.Play(V5UiSound.Ui.Tick, .8f + step * .05f, step == 0 ? 0f : 1.4f);
        }

        void ChooseArt(bool illustrated)
        {
            if (!ArtSwitchable || Art.Enabled == illustrated) return;
            V5UiSound.Play(V5UiSound.Ui.Toggle, illustrated ? .96f : 1.06f);
            // The command the title's old switch sent, with the menus' input lock.
            GameManager.Instance.ExecuteUiCommand(ArenaUiCommand.ToggleArt);
        }

        void ChooseLanguage(bool chinese)
        {
            if (!ArenaText.Ready || ArenaText.IsChinese == chinese) return;
            V5UiSound.Play(V5UiSound.Ui.Toggle, chinese ? .96f : 1.06f);
            ArenaLanguage.Choose(chinese ? StartupLanguage.Chinese : StartupLanguage.English);
        }

        void Listen(GameAction action)
        {
            if (listening == (int)action)
            {
                // A second click on the waiting row gives up.
                StopListening(null, true);
                V5UiSound.Play(V5UiSound.Ui.Back);
                return;
            }
            listening = (int)action;
            ArenaKeys.Suspended = true;
            message = "";
            V5UiSound.Play(V5UiSound.Ui.Press);
            // Nothing stays selected while a row waits: the key pressed next would also walk the selection.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            RenderKeys();
        }

        void ReadBindingKey(Keyboard keyboard)
        {
            foreach (var control in keyboard.allKeys)
            {
                // What the keyboard lists beside its real keys (an input method's signal, media keys) is passed over.
                if (control == null || !control.wasPressedThisFrame || ArenaKeys.WhyRefused(control.keyCode) == KeyRefusal.NotAKey) continue;
                Take(control.keyCode);
                break;
            }
        }

        KeyBindResult Take(Key key)
        {
            var action = (GameAction)listening;
            var result = ArenaKeys.Bind(action, key, out var other);
            switch (result)
            {
                case KeyBindResult.Refused:
                    // The row keeps waiting: the player presses another key, or Esc.
                    Say(ArenaSettingsPanel.RefusalMessage(key));
                    V5UiSound.Play(V5UiSound.Ui.Denied);
                    RenderKeysHint();
                    return result;
                case KeyBindResult.Unchanged:
                    StopListening(ArenaSettingsPanel.NothingChanged, true);
                    V5UiSound.Play(V5UiSound.Ui.Back);
                    return result;
                default:
                    StopListening(ArenaSettingsPanel.BoundMessage(action, result, other), true);
                    V5UiSound.Play(V5UiSound.Ui.Toggle);
                    return result;
            }
        }

        void Say(string words)
        {
            message = words ?? "";
            messageUntil = Time.unscaledTime + MessageSeconds;
        }

        void StopListening(string say, bool render)
        {
            if (listening < 0) return;
            listening = -1;
            stoppedFrame = Time.frameCount;
            ArenaKeys.Suspended = false;
            if (say != null) Say(say);
            if (render && built) RenderKeys();
        }

        void OnResetKeys()
        {
            StopListening(null, false);
            ArenaKeys.ResetToDefaults();
            Say(ArenaSettingsPanel.KeysReset);
            V5UiSound.Play(V5UiSound.Ui.Press);
            RenderKeys();
        }

        void TickSettings()
        {
            if (tab != SettingsTab) return;
            FollowToggle(artToggle, Art.Enabled ? 0 : 1);
            FollowToggle(languageToggle, ArenaText.IsChinese ? 0 : 1);
            // A drag of the volume is over: the volume is written to disk once.
            if (volumeMoved && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
            {
                volumeMoved = false;
                ArenaVolume.Save();
            }
            if (!volumeMoved) ShowVolume(Mathf.RoundToInt(ArenaVolume.Master * 10f));
            if (message.Length > 0 && Time.unscaledTime > messageUntil)
            {
                message = "";
                RenderKeysHint();
            }
        }
    }
}
