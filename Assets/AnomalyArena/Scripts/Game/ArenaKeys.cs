using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>What the player does with a key they may choose themselves, in the order the settings list shows them. They are saved
    /// by name, so one added later leaves the keys saved for the others alone.</summary>
    public enum GameAction { MoveUp, MoveLeft, MoveDown, MoveRight, Sprint, Interact, Skill1, Skill2, Skill3, Lamp, CardPick }

    /// <summary>How giving an action a key ended: it took the key, it took it and the action that held it took the old one, it had
    /// that key already, or the key cannot be used.</summary>
    public enum KeyBindResult { Bound, Swapped, Unchanged, Refused }

    /// <summary>Why a key cannot be given to an action.</summary>
    public enum KeyRefusal { None, MenuKey, SystemKey, NotAKey }

    /// <summary>
    /// The keys of the game's own actions in one place: moving, sprinting, interacting (mining, doors, crates, portals), the three
    /// skills (which also pick the first, second and third option on a panel), setting a lamp down and opening a card pick kept for
    /// later. Every script that reads one of these from the keyboard asks here, and every text that names one asks here for the key's
    /// name, so a key changed in the settings is the key the game reads and the key it shows. The defaults are the keys the game always
    /// had: W A S D, Shift, E, 1 2 3, F, T.
    ///
    /// Fixed, and not here: the mouse (aim, attack on the left button, swap hands on the right), because the pointer also works the
    /// menus and a weapon needs the button's press, hold and release; and the menus' keys Esc, Tab, R, Space and Enter. Those act on
    /// screens where the keys above act too (R rerolls on the panel where a skill key picks a card), so an action cannot take one of
    /// them either. A key the browser or the system takes is refused as well (Ctrl, Alt, the Windows key, F1 to F12, the lock keys):
    /// held with a letter, Ctrl closes a browser tab.
    ///
    /// Two actions never share a key and none is ever left without one: an action given a key that another holds swaps keys with it.
    /// Refusing instead would make a player free a key first and bind twice; with a swap one press does it and both rows show the change.
    /// The keys are saved with the other choices of the settings (ArenaPrefs) and read back at the next start.
    /// </summary>
    public static class ArenaKeys
    {
        /// <summary>The saved keys: "MoveUp=W;MoveLeft=A;…", actions and keys by name.</summary>
        public const string SavedKey = "WarpArena.Keys";

        static readonly Key[] Defaults =
        {
            Key.W, Key.A, Key.S, Key.D, Key.LeftShift, Key.E, Key.Digit1, Key.Digit2, Key.Digit3, Key.F, Key.T,
        };

        // The menus' own keys. They are written by name: SourceKeysTest lets no script but GameManagerFlow.cs name the weapon card's
        // keys in code, and a name here reads none of them.
        static readonly HashSet<Key> MenuKeys = Named("Escape", "Tab", "R", "Space", "Enter", "NumpadEnter");

        static readonly HashSet<Key> SystemKeys = new()
        {
            Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftMeta, Key.RightMeta, Key.ContextMenu,
            Key.PrintScreen, Key.ScrollLock, Key.NumLock, Key.CapsLock, Key.Pause,
            Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,
        };

        static Key[] bound;
        // The keys' names as shown, worked out once for each set of keys: the prompts ask for them every frame.
        static string[] names;

        /// <summary>How many actions there are.</summary>
        public static int Count => Defaults.Length;

        /// <summary>Goes up each time a key changes (never below 0): a view that wrote a key's name compares it to know when to write
        /// it again.</summary>
        public static int Revision { get; private set; }

        /// <summary>Raised after a key changed, a reset included.</summary>
        public static event Action Changed;

        /// <summary>While on, no action reads as held or pressed: the settings panel is open, and a key pressed there (for a binding,
        /// or just held) must not also act in the game behind it.</summary>
        public static bool Suspended { get; set; }

        // The editor keeps statics from one play to the next (domain reload is off): every play reads the saved keys afresh.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            bound = null;
            names = null;
            Changed = null;
            Suspended = false;
            Revision = 0;
        }

        static Key[] Map => bound ??= Parse(ArenaPrefs.GetString(SavedKey, ""));

        // ───── Reading ─────

        /// <summary>The key an action is on.</summary>
        public static Key KeyOf(GameAction action) => Map[(int)action];

        /// <summary>The action's key is down. A key on Shift answers to either Shift, as sprinting always did.</summary>
        public static bool Held(GameAction action)
        {
            var keyboard = Keyboard.current;
            if (Suspended || keyboard == null) return false;
            var key = KeyOf(action);
            return keyboard[key].isPressed || key == Key.LeftShift && keyboard[Key.RightShift].isPressed;
        }

        /// <summary>The action's key went down this frame.</summary>
        public static bool Pressed(GameAction action)
        {
            var keyboard = Keyboard.current;
            if (Suspended || keyboard == null) return false;
            var key = KeyOf(action);
            return keyboard[key].wasPressedThisFrame || key == Key.LeftShift && keyboard[Key.RightShift].wasPressedThisFrame;
        }

        /// <summary>The key to read for what the game knows by an action's default key: the key the player put that action on.
        /// A key that is no action's default (a fixed menu key) is read as it is. The menu key handling keeps its table of default
        /// keys and asks here which key to look at (GameManagerFlow).</summary>
        public static Key Physical(Key shown)
        {
            int index = Array.IndexOf(Defaults, shown);
            return index < 0 ? shown : Map[index];
        }

        // ───── Names ─────

        /// <summary>The name of the action's key as the game shows it: "W", "Shift", "1", "↑".</summary>
        public static string Name(GameAction action)
        {
            if (names == null)
            {
                var map = Map;
                names = new string[map.Length];
                for (int i = 0; i < map.Length; i++) names[i] = Label(map[i]);
            }
            return names[(int)action];
        }

        /// <summary>The four move keys in one piece, up, left, down, right: "WASD" while each is a single letter, else "↑ ← ↓ →".</summary>
        public static string MoveKeys
        {
            get
            {
                string up = Name(GameAction.MoveUp), left = Name(GameAction.MoveLeft), down = Name(GameAction.MoveDown), right = Name(GameAction.MoveRight);
                bool letters = up.Length == 1 && left.Length == 1 && down.Length == 1 && right.Length == 1
                    && char.IsLetter(up[0]) && char.IsLetter(left[0]) && char.IsLetter(down[0]) && char.IsLetter(right[0]);
                return letters ? up + left + down + right : $"{up} {left} {down} {right}";
            }
        }

        /// <summary>The three skill keys as the texts list them: "1 / 2 / 3".</summary>
        public static string SkillKeys => SkillKeysWith(" / ");

        /// <summary>The three skill keys with something else between them: "1, 2, 3".</summary>
        public static string SkillKeysWith(string between) => Name(GameAction.Skill1) + between + Name(GameAction.Skill2) + between + Name(GameAction.Skill3);

        /// <summary>The single keys the texts name most often, by what they do.</summary>
        public static string SprintKey => Name(GameAction.Sprint);
        public static string InteractKey => Name(GameAction.Interact);
        public static string LampKey => Name(GameAction.Lamp);
        public static string CardPickKey => Name(GameAction.CardPick);

        /// <summary>A key's name as the game shows it, the same in both languages (what is printed on the key, or a short word).</summary>
        public static string Label(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return key.ToString();
            if (key >= Key.Digit1 && key <= Key.Digit0) return key.ToString().Substring("Digit".Length);
            if (key >= Key.Numpad0 && key <= Key.Numpad9) return "Num " + key.ToString().Substring("Numpad".Length);
            return key switch
            {
                Key.LeftShift or Key.RightShift => "Shift",
                // Never an action's key (WhyRefused); named for the line that says so.
                Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                Key.LeftAlt or Key.RightAlt => "Alt",
                Key.LeftMeta or Key.RightMeta => "Win / Cmd",
                Key.UpArrow => "↑",
                Key.DownArrow => "↓",
                Key.LeftArrow => "←",
                Key.RightArrow => "→",
                Key.Backquote => "`",
                Key.Quote => "'",
                Key.Semicolon => ";",
                Key.Comma => ",",
                Key.Period => ".",
                Key.Slash => "/",
                Key.Backslash => "\\",
                Key.LeftBracket => "[",
                Key.RightBracket => "]",
                Key.Minus => "-",
                Key.Equals => "=",
                Key.PageUp => "PgUp",
                Key.PageDown => "PgDn",
                Key.Insert => "Ins",
                Key.Delete => "Del",
                Key.NumpadDivide => "Num /",
                Key.NumpadMultiply => "Num *",
                Key.NumpadPlus => "Num +",
                Key.NumpadMinus => "Num -",
                Key.NumpadPeriod => "Num .",
                Key.NumpadEquals => "Num =",
                Key.Escape => "Esc",
                _ => key.ToString(),
            };
        }

        // ───── Changing ─────

        /// <summary>Why this key cannot be given to an action; None when it can.</summary>
        public static KeyRefusal WhyRefused(Key key)
        {
            // Keys past the keyboard's own (the function keys from F13 on, the media keys) are not offered.
            if ((int)key < 1 || (int)key > Keyboard.KeyCount) return KeyRefusal.NotAKey;
            if (MenuKeys.Contains(key)) return KeyRefusal.MenuKey;
            return SystemKeys.Contains(key) ? KeyRefusal.SystemKey : KeyRefusal.None;
        }

        /// <summary>Gives the action this key. A key another action holds is swapped with it (that action is swappedWith); a fixed or
        /// unsafe key is refused and nothing changes. The new keys are saved at once.</summary>
        public static KeyBindResult Bind(GameAction action, Key key, out GameAction swappedWith)
        {
            swappedWith = action;
            // Either Shift is the one Shift.
            if (key == Key.RightShift) key = Key.LeftShift;
            if (WhyRefused(key) != KeyRefusal.None) return KeyBindResult.Refused;
            var map = Map;
            int index = (int)action;
            if (map[index] == key) return KeyBindResult.Unchanged;
            int other = Array.IndexOf(map, key);
            if (other >= 0)
            {
                map[other] = map[index];
                swappedWith = (GameAction)other;
            }
            map[index] = key;
            Save();
            return other >= 0 ? KeyBindResult.Swapped : KeyBindResult.Bound;
        }

        /// <summary>Every action back on the key the game always had.</summary>
        public static void ResetToDefaults()
        {
            bound = (Key[])Defaults.Clone();
            Save();
        }

        /// <summary>Forgets the keys in memory and reads the saved ones again, as the next start does.</summary>
        public static void Reload()
        {
            bound = null;
            Note();
        }

        static void Save()
        {
            ArenaPrefs.SetString(SavedKey, Serialize(Map));
            ArenaPrefs.Save();
            Note();
        }

        static void Note()
        {
            names = null;
            Revision++;
            Changed?.Invoke();
        }

        // ───── By name: for the saved text, and for callers that do not know the keyboard's types (the tests) ─────

        /// <summary>The name of the action's key in the keyboard's own list ("W", "LeftShift", "Digit1"), as it is saved.</summary>
        public static string KeyNameOf(GameAction action) => KeyOf(action).ToString();

        /// <summary>The name, in the keyboard's own list, of the key the game always had for this action.</summary>
        public static string DefaultKeyNameOf(GameAction action) => Defaults[(int)action].ToString();

        /// <summary>Bind with the key given by its name in the keyboard's own list; an unknown name is refused.</summary>
        public static KeyBindResult BindByName(GameAction action, string keyName, out GameAction swappedWith)
        {
            swappedWith = action;
            return TryKey(keyName, out var key) ? Bind(action, key, out swappedWith) : KeyBindResult.Refused;
        }

        /// <summary>Whether a key of this name could be given to an action (a known key that is neither fixed nor unsafe).</summary>
        public static bool CanBindByName(string keyName) => TryKey(keyName, out var key) && WhyRefused(key) == KeyRefusal.None;

        /// <summary>The saved text of the keys as they are now.</summary>
        public static string SavedText => Serialize(Map);

        static bool TryKey(string name, out Key key)
        {
            key = Key.None;
            // A number is not a name: Enum.TryParse would take "5" for the key numbered 5.
            return !string.IsNullOrEmpty(name) && !char.IsDigit(name[0]) && name[0] != '-' && Enum.TryParse(name, out key) && Enum.IsDefined(typeof(Key), key);
        }

        static HashSet<Key> Named(params string[] names)
        {
            var keys = new HashSet<Key>();
            foreach (var name in names)
                if (TryKey(name, out var key)) keys.Add(key);
            return keys;
        }

        static string Serialize(Key[] map)
        {
            var text = new StringBuilder();
            for (int i = 0; i < map.Length; i++)
            {
                if (i > 0) text.Append(';');
                text.Append((GameAction)i).Append('=').Append(map[i]);
            }
            return text.ToString();
        }

        /// <summary>The keys a saved text gives: the defaults, with each action the text names on the key it names. An entry that names
        /// no action or no usable key is skipped. A text that would put two actions on one key is dropped whole and the defaults stand:
        /// which of the two should keep the key cannot be told from the text.</summary>
        static Key[] Parse(string saved)
        {
            var map = (Key[])Defaults.Clone();
            if (string.IsNullOrEmpty(saved)) return map;
            foreach (var entry in saved.Split(';'))
            {
                int equals = entry.IndexOf('=');
                if (equals <= 0) continue;
                string actionName = entry.Substring(0, equals).Trim();
                int index = -1;
                for (int i = 0; i < map.Length && index < 0; i++)
                    if (((GameAction)i).ToString() == actionName) index = i;
                if (index < 0 || !TryKey(entry.Substring(equals + 1).Trim(), out var key)) continue;
                if (key == Key.RightShift) key = Key.LeftShift;
                if (WhyRefused(key) == KeyRefusal.None) map[index] = key;
            }
            for (int i = 0; i < map.Length; i++)
                for (int j = i + 1; j < map.Length; j++)
                    if (map[i] == map[j]) return (Key[])Defaults.Clone();
            return map;
        }
    }
}
