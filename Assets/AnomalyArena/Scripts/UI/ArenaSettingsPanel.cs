using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The settings panel: the sound volume, the art mode and the keys. The title's Settings button (top left) and the pause screen's
    /// open it over whatever is showing, on a canvas of its own. A dark layer under it takes every click, so nothing behind it can be
    /// pressed; while it is open, and on the frame it closes, the game reads none of its keys (GameManager's menu keys through
    /// HoldsInput, the actions through ArenaKeys.Suspended), so the Esc that closes it does not also resume a paused run and a key
    /// pressed for a binding does nothing else.
    /// - Volume: one slider for everything that sounds (ArenaVolume).
    /// - Art mode: illustrated or whitebox, through the command the title's art switch sent (ArenaUiCommand.ToggleArt), so its rules
    ///   are the same: only on the title, never in the enemy lab, kept for the rest of the play (Art). Opened from the pause screen
    ///   the two buttons show the mode and cannot be pressed, with a line saying where it is switched.
    /// - Keys: one row for each action of ArenaKeys. A click on a row's key waits for the next key pressed: it takes that key, swaps
    ///   with the action that held it, or says why the key cannot be used and keeps waiting; Esc gives up. Reset brings the
    ///   defaults back.
    /// Esc, the Close button or a gamepad's east button closes it. With the art on it wears the menus' drawn panel and buttons, in
    /// whitebox the flat ones (WhiteboxSkin), like the tutorial's pages.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaSettingsPanel : MonoBehaviour
    {
        /// <summary>The panel's root object; its parts are under "Window/Panel" (ArtIllustrated, ArtWhitebox, Volume, Key + the action's
        /// name with its Name and its Key button, ResetKeys, Close).</summary>
        public const string RootName = "SettingsPanel";

        // Canvas units on the 1440 x 810 reference; tops are measured from the panel's top edge.
        const float PanelW = 900f, PanelH = 712f, Margin = 48f, LabelW = 170f, ControlLeft = 230f;
        const float VolumeTop = 96f, ArtTop = 152f, KeysTop = 214f, RowsTop = 252f, RowH = 44f;
        const float KeyColumnW = 392f, KeyColumnGap = 20f, KeyButtonW = 132f, KeyButtonH = 36f;
        const float MessageTop = 524f, FixedTop = 554f, BottomButtonW = 260f, BottomButtonH = 52f;
        // The left column holds the moves, the sprint and the interact key; the right one the skills, the lamp and the card pick.
        const int LeftRows = 6;

        static readonly Color Soft = new(.86f, .90f, .96f), Faint = new(.62f, .72f, .85f), Cyan = new(.50f, .86f, 1f), Waiting = new(1f, .84f, .36f);
        static readonly Color Trough = new(.10f, .15f, .22f, 1f), Backing = new(.03f, .05f, .10f, .96f);

        static ArenaSettingsPanel instance;
        static Sprite panelSprite, buttonSprite, primarySprite;
        static int closedFrame = -1;

        public static bool IsOpen => instance && instance.gameObject.activeSelf;

        /// <summary>The game ignores its keys while the panel is open, and on the frame it closes, so the closing Esc does nothing else.</summary>
        public static bool HoldsInput => IsOpen || Time.frameCount == closedFrame;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            instance = null;
            closedFrame = -1;
        }

        /// <summary>Opens the panel over whatever shows, in this font (the game's own when none is given).</summary>
        public static void Open(Font font)
        {
            if (!instance) instance = Build();
            instance.Show(font);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            closedFrame = Time.frameCount;
            instance.Hide();
        }

        /// <summary>What Esc does (and a gamepad's east button): a row waiting for a key gives up; otherwise the panel closes. Open to
        /// tests, like GameManager.PressKey: device keys never reach the game in the batch-mode test runner.</summary>
        public static void Back()
        {
            if (!IsOpen) return;
            if (instance.listening >= 0) { instance.StopListening(ArenaText.Pair("没有改动。", "Nothing changed.")); return; }
            V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
            Close();
        }

        /// <summary>The row that waits for a key, if one does.</summary>
        public static bool IsListening(out GameAction action)
        {
            action = IsOpen && instance.listening >= 0 ? (GameAction)instance.listening : default;
            return IsOpen && instance.listening >= 0;
        }

        /// <summary>What a key pressed while a row waits does, with the key given by its name in the keyboard's own list ("G",
        /// "UpArrow"). Refused (and nothing changes) while no row waits or for an unknown name. Open to tests, like Back.</summary>
        public static KeyBindResult TakeKeyByName(string keyName)
        {
            if (!IsOpen || instance.listening < 0 || !Enum.TryParse(keyName, out Key key) || !Enum.IsDefined(typeof(Key), key)) return KeyBindResult.Refused;
            return instance.Take(key);
        }

        // ───── The open panel ─────

        readonly WhiteboxSkin.Set flat = new();
        Canvas canvas;
        RectTransform window;
        Slider volume;
        Text title, volumeLabel, volumeValue, artLabel, artNote, keysHeading, keysHint, message, fixedNote, resetLabel, closeLabel;
        Text illustratedLabel, whiteboxLabel;
        Button illustrated, whitebox, close;
        Image illustratedMark, whiteboxMark;
        readonly Text[] rowNames = new Text[ArenaKeys.Count], rowKeys = new Text[ArenaKeys.Count];
        Font appliedFont;
        GameObject selectedBefore;
        string messageText = "";
        int listening = -1;
        bool volumeMoved;

        void OnEnable() => ArenaKeys.Suspended = true;

        void OnDisable() => ArenaKeys.Suspended = false;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Show(Font font)
        {
            listening = -1;
            messageText = "";
            volumeMoved = false;
            ApplyFont(font ? font : ArenaText.Font);
            volume.SetValueWithoutNotify(ArenaVolume.Master);
            // Before the first frame it is drawn in: the look that goes with the art switch.
            flat.Follow();
            var events = EventSystem.current;
            selectedBefore = events ? events.currentSelectedGameObject : null;
            gameObject.SetActive(true);
            FitWindow();
            // The selection leaves the button behind the panel; the keyboard and a gamepad start from Close.
            if (events) events.SetSelectedGameObject(close.gameObject);
            Refresh();
        }

        void Hide()
        {
            listening = -1;
            ArenaVolume.Save();
            gameObject.SetActive(false);
            var events = EventSystem.current;
            if (events) events.SetSelectedGameObject(selectedBefore && selectedBefore.activeInHierarchy ? selectedBefore : null);
            selectedBefore = null;
        }

        void Update()
        {
            flat.Follow();
            if (ArenaText.Font) ApplyFont(ArenaText.Font);
            FitWindow();
            var keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (escape || Gamepad.current is { } pad && pad.buttonEast.wasPressedThisFrame)
            {
                Back();
                if (!IsOpen) return;
            }
            else if (listening >= 0 && keyboard != null)
            {
                foreach (var control in keyboard.allKeys)
                {
                    // What the keyboard lists beside its real keys (an input method's own signal, media keys) is passed over.
                    if (control == null || !control.wasPressedThisFrame || ArenaKeys.WhyRefused(control.keyCode) == KeyRefusal.NotAKey) continue;
                    Take(control.keyCode);
                    break;
                }
            }
            // A drag of the slider is over: the volume is written to disk once, and a click sound lets the new level be heard.
            if (volumeMoved && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
            {
                volumeMoved = false;
                ArenaVolume.Save();
                V2Audio.Instance?.PlayUI(ArenaUiSound.Select);
            }
            Refresh();
        }

        /// <summary>A window too small for the panel shows it whole, scaled down, instead of cut off. The canvas' size is worked out from
        /// the screen and the scale the canvas scaler set as the panel came on: the canvas' own rectangle follows a little later.</summary>
        void FitWindow()
        {
            float unit = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            var scale = Vector3.one * Mathf.Min(1f, Screen.width / unit / (PanelW + 24f), Screen.height / unit / (PanelH + 24f));
            if (window.localScale != scale) window.localScale = scale;
        }

        // ───── Art mode ─────

        /// <summary>The title's art switch worked on the title only, and the enemy lab shows only the illustrated art.</summary>
        static bool ArtSwitchable => GameManager.Instance && GameManager.Instance.RunPhase == ArenaUiState.Title && !Art.IllustratedOnly && !Art.WhiteboxOnly;

        void ChooseArt(bool wanted)
        {
            if (!ArtSwitchable || Art.Enabled == wanted) return;
            // The command the title's own switch sent, with the menus' input lock and their sound.
            GameManager.Instance.ExecuteUiCommand(ArenaUiCommand.ToggleArt);
        }

        // ───── Keys ─────

        void Listen(GameAction action)
        {
            V2Audio.Instance?.PlayUI(ArenaUiSound.Select);
            // A second click on the waiting row gives up.
            listening = listening == (int)action ? -1 : (int)action;
            messageText = "";
            // Nothing stays selected while a row waits: the key pressed next (an arrow, a move key) would otherwise also walk the
            // selection to a neighbour, or move the slider if that was selected.
            if (listening >= 0 && EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        void StopListening(string say)
        {
            listening = -1;
            messageText = say ?? "";
        }

        KeyBindResult Take(Key key)
        {
            var action = (GameAction)listening;
            var result = ArenaKeys.Bind(action, key, out var other);
            switch (result)
            {
                case KeyBindResult.Refused:
                    // The row keeps waiting: the player presses another key, or Esc.
                    messageText = ArenaKeys.WhyRefused(key) switch
                    {
                        KeyRefusal.MenuKey => ArenaText.Pair($"{ArenaKeys.Label(key)} 是菜单的固定键，不能用。换一个键，或按 Esc 取消。", $"{ArenaKeys.Label(key)} is one of the menus' fixed keys. Press another key, or Esc to cancel."),
                        KeyRefusal.SystemKey => ArenaText.Pair($"{ArenaKeys.Label(key)} 会被浏览器或系统占用，不能用。换一个键，或按 Esc 取消。", $"{ArenaKeys.Label(key)} is taken by the browser or the system. Press another key, or Esc to cancel."),
                        _ => ArenaText.Pair("这个键不能用。换一个键，或按 Esc 取消。", "That key can't be used. Press another key, or Esc to cancel."),
                    };
                    V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
                    return result;
                case KeyBindResult.Swapped:
                    StopListening(ArenaText.Pair($"「{ActionName(action)}」和「{ActionName(other)}」交换了按键。", $"\"{ActionName(action)}\" and \"{ActionName(other)}\" swapped keys."));
                    break;
                default:
                    StopListening(ArenaText.Pair($"「{ActionName(action)}」现在是 {ArenaKeys.Name(action)}。", $"\"{ActionName(action)}\" is now {ArenaKeys.Name(action)}."));
                    break;
            }
            V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);
            return result;
        }

        void ResetKeys()
        {
            ArenaKeys.ResetToDefaults();
            StopListening(ArenaText.Pair("按键已恢复默认。", "The keys are back to their defaults."));
            V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);
        }

        /// <summary>An action's name in the list, in the language showing.</summary>
        public static string ActionName(GameAction action) => action switch
        {
            GameAction.MoveUp => ArenaText.Pair("向上移动", "Move up"),
            GameAction.MoveLeft => ArenaText.Pair("向左移动", "Move left"),
            GameAction.MoveDown => ArenaText.Pair("向下移动", "Move down"),
            GameAction.MoveRight => ArenaText.Pair("向右移动", "Move right"),
            GameAction.Sprint => ArenaText.Pair("冲刺", "Sprint"),
            GameAction.Interact => ArenaText.Pair("互动 / 采集", "Interact / Mine"),
            GameAction.Skill1 => ArenaText.Pair("技能 1 / 面板选项 1", "Skill 1 / Option 1"),
            GameAction.Skill2 => ArenaText.Pair("技能 2 / 面板选项 2", "Skill 2 / Option 2"),
            GameAction.Skill3 => ArenaText.Pair("技能 3 / 面板选项 3", "Skill 3 / Option 3"),
            GameAction.Lamp => ArenaText.Pair("放下矿灯", "Place a lamp"),
            GameAction.CardPick => ArenaText.Pair("打开待选的升级", "Open a pending pick"),
            _ => action.ToString(),
        };

        // ───── What it shows ─────

        void Refresh()
        {
            SetText(title, ArenaText.Pair("设置", "SETTINGS"));
            SetText(volumeLabel, ArenaText.Pair("音量", "Volume"));
            SetText(volumeValue, Mathf.RoundToInt(ArenaVolume.Master * 100f) + "%");

            // The Alpha page is whitebox only (BuildEdition): it has no art row at all.
            bool artRow = !Art.WhiteboxOnly;
            foreach (Component part in new Component[] { artLabel, illustrated, whitebox, artNote })
                if (part && part.gameObject.activeSelf != artRow) part.gameObject.SetActive(artRow);
            SetText(artLabel, ArenaText.Pair("画面", "Art mode"));
            SetText(illustratedLabel, ArenaText.Pair("插画", "Illustrated"));
            SetText(whiteboxLabel, ArenaText.Pair("白盒", "Whitebox"));
            bool switchable = ArtSwitchable;
            if (illustrated.interactable != switchable) illustrated.interactable = whitebox.interactable = switchable;
            Mark(illustratedMark, illustratedLabel, Art.Enabled);
            Mark(whiteboxMark, whiteboxLabel, !Art.Enabled);
            SetText(artNote, switchable ? ""
                : Art.IllustratedOnly ? ArenaText.Pair("这个场景只有插画。", "This scene is illustrated only.")
                : ArenaText.Pair("回到标题界面才能切换。", "You can only switch this on the title screen."));

            SetText(keysHeading, ArenaText.Pair("按键", "Keys"));
            SetText(keysHint, listening >= 0 ? ArenaText.Pair("按下你想用的键，按 Esc 取消", "Press the key you want, or Esc to cancel")
                : ArenaText.Pair("点右边的键，再按下想用的键", "Click a key on the right, then press the key you want"));
            for (int i = 0; i < rowNames.Length; i++)
            {
                SetText(rowNames[i], ActionName((GameAction)i));
                bool waiting = listening == i;
                SetText(rowKeys[i], waiting ? ArenaText.Pair("按一个键…", "Press a key…") : ArenaKeys.Name((GameAction)i));
                var colour = waiting ? Waiting : Cyan;
                if (rowKeys[i].color != colour) rowKeys[i].color = colour;
            }
            SetText(message, messageText);
            SetText(fixedNote, ArenaText.Pair("鼠标按键不能改：左键使用手上的武器，右键在两只手的武器之间切换。Esc、Tab、R、空格和回车是菜单专用键，不能分配给上面的动作。",
                "Mouse buttons are fixed: left click uses the weapon in your hand, right click switches hands. Esc, Tab, R, Space and Enter are reserved for menus and can't be given to the actions above."));
            SetText(resetLabel, ArenaText.Pair("恢复默认按键", "RESET KEYS"));
            SetText(closeLabel, ArenaText.Pair("关闭  [Esc]", "CLOSE  [Esc]"));
        }

        static void Mark(Image mark, Text label, bool current)
        {
            if (mark.enabled != current) mark.enabled = current;
            var colour = current ? Color.white : Faint;
            if (label.color != colour) label.color = colour;
        }

        static void SetText(Text label, string value)
        {
            if (label && label.text != (value ?? "")) label.text = value ?? "";
        }

        void ApplyFont(Font font)
        {
            if (!font || font == appliedFont) return;
            appliedFont = font;
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = font;
        }

        // ───── Building ─────

        static ArenaSettingsPanel Build()
        {
            if (!panelSprite) panelSprite = Load("V5/UI/ui-panel", 112f);
            if (!buttonSprite) buttonSprite = Load("V5/UI/ui-button", 40f);
            if (!primarySprite) primarySprite = Load("V5/UI/ui-button-primary", 40f);

            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var overlay = root.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            // Over the game's canvases and the campaign's; under the tutorial's pages, which never show with it.
            overlay.sortingOrder = 29000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 810f);
            scaler.matchWidthOrHeight = .5f;
            var panel = root.AddComponent<ArenaSettingsPanel>();
            panel.BuildParts();
            root.SetActive(false);
            return panel;
        }

        void BuildParts()
        {
            canvas = GetComponent<Canvas>();
            // Takes every click that misses the panel's own controls: nothing behind can be pressed.
            var dim = NewImage(transform, "Dim", null, new Color(0f, 0f, 0f, .74f));
            Stretch(dim.rectTransform, 0f);
            dim.raycastTarget = true;

            window = NewRect(transform, "Window");
            window.sizeDelta = new Vector2(PanelW, PanelH);
            // A solid backing inside the drawn frame's cut corners, so what is behind does not show through the text.
            var backing = NewImage(window, "Backing", null, Backing).rectTransform;
            backing.sizeDelta = new Vector2(PanelW - 56f, PanelH - 56f);
            var frame = NewImage(window, "Panel", panelSprite, panelSprite ? Color.white : WhiteboxSkin.PanelFill, Image.Type.Sliced, 2f);
            frame.raycastTarget = true;
            if (panelSprite) flat.Add(frame, WhiteboxSkin.Look.Panel);
            var body = frame.rectTransform;
            body.sizeDelta = new Vector2(PanelW, PanelH);

            title = NewText(body, "Title", 30, Soft, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(title.rectTransform, Margin, 26f, 420f, 44f);
            var rule = NewImage(body, "TitleRule", null, new Color(Cyan.r, Cyan.g, Cyan.b, .35f));
            Place(rule.rectTransform, Margin - 8f, 78f, PanelW - (Margin - 8f) * 2f, 2f);

            // Volume: its name, the slider, the level as a percentage.
            volumeLabel = RowLabel(body, "VolumeLabel", VolumeTop);
            volume = MakeSlider(body, "Volume", ControlLeft, VolumeTop + 8f, 470f, 28f);
            volume.onValueChanged.AddListener(value =>
            {
                ArenaVolume.Set(value);
                volumeMoved = true;
            });
            volumeValue = NewText(body, "VolumeValue", 20, Soft, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(volumeValue.rectTransform, ControlLeft + 470f + 16f, VolumeTop, 100f, RowH);

            // Art mode: the two modes side by side, the one showing marked; a line beside them when they cannot be pressed here.
            artLabel = RowLabel(body, "ArtLabel", ArtTop);
            illustrated = MakeButton(body, "ArtIllustrated", false, 20, () => ChooseArt(true), out illustratedLabel);
            Place((RectTransform)illustrated.transform, ControlLeft, ArtTop, 200f, RowH);
            illustratedMark = MakeMark(illustrated.transform);
            whitebox = MakeButton(body, "ArtWhitebox", false, 20, () => ChooseArt(false), out whiteboxLabel);
            Place((RectTransform)whitebox.transform, ControlLeft + 212f, ArtTop, 200f, RowH);
            whiteboxMark = MakeMark(whitebox.transform);
            artNote = NewText(body, "ArtNote", 15, Faint, FontStyle.Normal, TextAnchor.MiddleLeft);
            Place(artNote.rectTransform, ControlLeft + 428f, ArtTop, PanelW - Margin - (ControlLeft + 428f), RowH);
            Shrink(artNote, 11);

            // Keys: a heading with what to do, then the rows in two columns.
            keysHeading = NewText(body, "KeysHeading", 22, Cyan, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(keysHeading.rectTransform, Margin, KeysTop, LabelW, 30f);
            keysHint = NewText(body, "KeysHint", 15, Faint, FontStyle.Normal, TextAnchor.MiddleLeft);
            Place(keysHint.rectTransform, ControlLeft, KeysTop, PanelW - Margin - ControlLeft, 30f);
            for (int i = 0; i < rowNames.Length; i++)
            {
                var action = (GameAction)i;
                int column = i < LeftRows ? 0 : 1, line = column == 0 ? i : i - LeftRows;
                var row = NewRect(body, "Key" + action);
                Place(row, Margin + column * (KeyColumnW + KeyColumnGap), RowsTop + line * RowH, KeyColumnW, RowH);
                rowNames[i] = NewText(row, "Name", 18, Soft, FontStyle.Normal, TextAnchor.MiddleLeft);
                Place(rowNames[i].rectTransform, 0f, 0f, KeyColumnW - KeyButtonW - 12f, RowH);
                Shrink(rowNames[i], 12);
                var key = MakeButton(row, "Key", false, 18, () => Listen(action), out rowKeys[i]);
                Place((RectTransform)key.transform, KeyColumnW - KeyButtonW, (RowH - KeyButtonH) * .5f, KeyButtonW, KeyButtonH);
                rowKeys[i].color = Cyan;
            }

            // What the last key press did, then what cannot be changed at all.
            message = NewText(body, "Message", 16, Waiting, FontStyle.Normal, TextAnchor.MiddleLeft);
            Place(message.rectTransform, Margin, MessageTop, PanelW - Margin * 2f, 28f);
            Shrink(message, 11);
            fixedNote = NewText(body, "FixedNote", 14, Faint, FontStyle.Normal, TextAnchor.UpperLeft);
            Place(fixedNote.rectTransform, Margin, FixedTop, PanelW - Margin * 2f, 44f);
            Shrink(fixedNote, 10);

            var reset = MakeButton(body, "ResetKeys", false, 19, ResetKeys, out resetLabel);
            Place((RectTransform)reset.transform, Margin, PanelH - Margin - BottomButtonH, BottomButtonW, BottomButtonH);
            close = MakeButton(body, "Close", true, 19, Back, out closeLabel);
            Place((RectTransform)close.transform, PanelW - Margin - BottomButtonW, PanelH - Margin - BottomButtonH, BottomButtonW, BottomButtonH);
        }

        Text RowLabel(Transform parent, string name, float top)
        {
            var label = NewText(parent, name, 22, Cyan, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(label.rectTransform, Margin, top, LabelW, RowH);
            Shrink(label, 14);
            return label;
        }

        /// <summary>A button in the menus' look: the drawn plate with the art on, a flat one in whitebox (WhiteboxSkin follows the switch).</summary>
        Button MakeButton(Transform parent, string name, bool primary, int textSize, Action click, out Text label)
        {
            var sprite = primary ? primarySprite : buttonSprite;
            var image = NewImage(parent, name, sprite, sprite ? Color.white : primary ? WhiteboxSkin.PrimaryFill : WhiteboxSkin.ButtonFill, Image.Type.Sliced, 2f);
            image.raycastTarget = true;
            if (sprite) flat.Add(image, primary ? WhiteboxSkin.Look.PrimaryButton : WhiteboxSkin.Look.Button);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // The menus' own tints (V5MenuSkin.SkinButton).
            var colours = button.colors;
            colours.normalColor = new Color(.84f, .88f, .95f);
            colours.highlightedColor = Color.white;
            colours.selectedColor = Color.white;
            colours.pressedColor = new Color(.62f, .68f, .80f);
            colours.disabledColor = new Color(.40f, .43f, .50f, .75f);
            colours.fadeDuration = .08f;
            button.colors = colours;
            button.onClick.AddListener(() => click());
            label = NewText(image.rectTransform, "Label", textSize, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 8f);
            Shrink(label, Mathf.Max(10, textSize - 8));
            return button;
        }

        /// <summary>The line under the art mode that is showing.</summary>
        static Image MakeMark(Transform button)
        {
            var mark = NewImage(button, "Mark", null, Cyan);
            var rect = mark.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.offsetMin = new Vector2(16f, 5f);
            rect.offsetMax = new Vector2(-16f, 8f);
            return mark;
        }

        /// <summary>A slider made of plain blocks, the same in both looks: a trough that takes the pointer along its whole length, the
        /// filled part and a handle.</summary>
        static Slider MakeSlider(Transform parent, string name, float left, float top, float width, float height)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, left, top, width, height);
            var trough = NewImage(root.transform, "Background", null, Trough);
            Stretch(trough.rectTransform, 0f);
            trough.raycastTarget = true;
            var fillArea = NewRect(root.transform, "Fill Area");
            Stretch(fillArea, 4f);
            var fill = NewImage(fillArea, "Fill", null, new Color(Cyan.r, Cyan.g, Cyan.b, .85f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var handleArea = NewRect(root.transform, "Handle Slide Area");
            Stretch(handleArea, 0f);
            handleArea.offsetMin = new Vector2(9f, 0f);
            handleArea.offsetMax = new Vector2(-9f, 0f);
            var handle = NewImage(handleArea, "Handle", null, Color.white);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = Vector2.zero;
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(18f, 8f);
            var slider = root.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            return slider;
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static Image NewImage(Transform parent, string name, Sprite sprite, Color colour, Image.Type type = Image.Type.Simple, float density = 1f)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.type = sprite ? type : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = density;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        static Text NewText(Transform parent, string name, int size, Color colour, FontStyle style, TextAnchor anchor)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = colour;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        /// <summary>A text too long for its box goes down to this size before it loses anything.</summary>
        static void Shrink(Text label, int smallest)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Min(smallest, label.fontSize);
            label.resizeTextMaxSize = label.fontSize;
        }

        /// <summary>Left and top measured from the parent's top-left corner.</summary>
        static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        static Sprite Load(string path, float border)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (!texture) return null;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * border);
        }
    }
}
