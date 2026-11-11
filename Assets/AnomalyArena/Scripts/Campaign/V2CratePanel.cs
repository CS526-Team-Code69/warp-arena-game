using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// A weapon crate's pick-one-of-three (implementation spec 2026-11-10, sections A and B), on the campaign's own canvas: a dimmed
    /// screen that takes every click, a heading, a line on where the weapon goes, three weapons side by side (picture, name in its rarity
    /// colour, hand or auto and Mk, rarity, where it goes) and, when the crate may be left, a button for that. 1 / 2 / 3 or a click picks,
    /// Esc leaves it. Its words never call anything a card: the campaign has no cards. The run holds still behind it (V2Campaign.Crates.cs).
    /// </summary>
    public sealed class V2CratePanel : MonoBehaviour
    {
        /// <summary>One weapon as the panel shows it; every text arrives localized.</summary>
        public sealed class Option
        {
            public string title, kind, body, badge;
            public Color tint = Color.white;
            public int formIndex = -1;
        }

        sealed class View
        {
            public RectTransform root; public Button button; public Outline edge;
            public Text number, title, kind, badge, body; public Image icon;
            public int formIndex = -1;
        }

        // Keys and clicks wait this long after the panel opens: the E that opened it or a held fire button must not pick at once.
        const float InputDelay = .25f;
        static readonly Color Plate = new(.04f, .07f, .12f, .96f), Soft = new(.86f, .9f, .96f), Faint = new(.62f, .72f, .85f), Accent = new(.45f, .85f, 1f);

        RectTransform screen;
        Text heading, sub, hint, skipLabel;
        Button skip;
        readonly View[] views = new View[RunWeapons.ArmoryOfferCount];
        Action<int> pick;
        Action leave;
        int count;
        bool mayLeave, iconsIllustrated;
        float openedAt;

        public bool Open => screen && screen.gameObject.activeSelf;

        public static V2CratePanel Create(RectTransform parent)
        {
            var rect = new GameObject("CratePanel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var panel = rect.gameObject.AddComponent<V2CratePanel>();
            panel.Build(rect);
            return panel;
        }

        void Build(RectTransform rect)
        {
            screen = rect;
            var back = rect.GetComponent<Image>();
            back.color = new Color(0f, .01f, .03f, .82f);
            back.raycastTarget = true; // no click reaches the game or the panels under it
            var panel = Box("Panel", rect, new Vector2(.5f, .5f), new Vector2(0f, 10f), new Vector2(1180f, 600f), Plate);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.3f, .55f, .85f, .55f); outline.effectDistance = new Vector2(2f, -2f);
            heading = Label("Heading", panel, 32, Color.white, TextAnchor.UpperCenter);
            heading.fontStyle = FontStyle.Bold;
            Place(heading.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -24f), new Vector2(1100f, 44f), new Vector2(.5f, 1f));
            sub = Label("Sub", panel, 19, Soft, TextAnchor.UpperCenter);
            Place(sub.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -74f), new Vector2(1100f, 52f), new Vector2(.5f, 1f));
            for (int i = 0; i < views.Length; i++) views[i] = BuildView(panel, i);
            hint = Label("Keys", panel, 16, Faint, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 24f), new Vector2(1100f, 28f), new Vector2(.5f, 0f));
            var skipRect = Box("Leave", panel, new Vector2(.5f, 0f), new Vector2(0f, 62f), new Vector2(240f, 48f), new Color(.1f, .15f, .22f, 1f));
            skipRect.GetComponent<Image>().raycastTarget = true;
            var skipEdge = skipRect.gameObject.AddComponent<Outline>();
            skipEdge.effectColor = new Color(.3f, .55f, .85f, .55f); skipEdge.effectDistance = new Vector2(2f, -2f);
            skip = skipRect.gameObject.AddComponent<Button>();
            skip.onClick.AddListener(Leave);
            skipLabel = Label("Label", skipRect, 18, Color.white, TextAnchor.MiddleCenter);
            Fill(skipLabel.rectTransform);
            screen.gameObject.SetActive(false);
        }

        View BuildView(RectTransform panel, int i)
        {
            var v = new View();
            v.root = Box("Weapon" + (i + 1), panel, new Vector2(.5f, 1f), new Vector2((i - 1) * 372f, -138f), new Vector2(350f, 340f), new Color(.07f, .11f, .18f, 1f));
            v.root.pivot = new Vector2(.5f, 1f);
            v.root.GetComponent<Image>().raycastTarget = true;
            v.edge = v.root.gameObject.AddComponent<Outline>();
            v.edge.effectDistance = new Vector2(3f, -3f);
            v.button = v.root.gameObject.AddComponent<Button>();
            var colours = v.button.colors;
            colours.highlightedColor = new Color(1.3f, 1.3f, 1.35f); colours.pressedColor = new Color(.8f, .8f, .85f); colours.disabledColor = new Color(.85f, .85f, .9f, 1f);
            v.button.colors = colours;
            int index = i;
            v.button.onClick.AddListener(() => Pick(index));
            v.number = Label("Key", v.root, 18, Faint, TextAnchor.UpperLeft);
            Place(v.number.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(40f, 26f), new Vector2(0f, 1f));
            v.icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            v.icon.rectTransform.SetParent(v.root, false);
            Place(v.icon.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -16f), new Vector2(120f, 120f), new Vector2(.5f, 1f));
            v.icon.preserveAspect = true; v.icon.raycastTarget = false;
            v.title = Label("Title", v.root, 26, Color.white, TextAnchor.UpperCenter);
            v.title.fontStyle = FontStyle.Bold;
            Place(v.title.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -142f), new Vector2(320f, 34f), new Vector2(.5f, 1f));
            v.kind = Label("Kind", v.root, 22, Accent, TextAnchor.UpperCenter);
            Place(v.kind.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -180f), new Vector2(320f, 30f), new Vector2(.5f, 1f));
            v.body = Label("Body", v.root, 40, Faint, TextAnchor.MiddleCenter);
            v.body.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(v.body.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -214f), new Vector2(316f, 76f), new Vector2(.5f, 1f));
            v.badge = Label("Badge", v.root, 17, Color.white, TextAnchor.LowerCenter);
            Place(v.badge.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 10f), new Vector2(320f, 24f), new Vector2(.5f, 0f));
            return v;
        }

        /// <summary>Opens the panel on these weapons (at most three). onPick gets the index picked; onLeave runs when the crate is left
        /// (only when mayLeave). The panel closes itself before either runs.</summary>
        public void Show(string headingText, string subText, IReadOnlyList<Option> options, bool canLeave, Action<int> onPick, Action onLeave)
        {
            ApplyFont();
            pick = onPick; leave = onLeave; mayLeave = canLeave;
            count = Mathf.Min(options?.Count ?? 0, views.Length);
            heading.text = headingText ?? "";
            sub.text = subText ?? "";
            var art = GameManager.Instance && GameManager.Instance.hud && GameManager.Instance.hud.v2Hud ? GameManager.Instance.hud.v2Hud.art : null;
            for (int i = 0; i < views.Length; i++)
            {
                var v = views[i];
                bool used = i < count && options[i] != null;
                v.root.gameObject.SetActive(used);
                if (!used) continue;
                var o = options[i];
                // Two weapons sit side by side under the middle; one sits in the middle.
                float step = 372f, first = -(count - 1) * step * .5f;
                v.root.anchoredPosition = new Vector2(first + i * step, -138f);
                // The key that takes this weapon: the skill key of the same place, as the player has it (ArenaKeys).
                v.number.text = ArenaKeys.Name(GameAction.Skill1 + i);
                v.title.text = o.title ?? "";
                v.title.color = o.tint;
                v.kind.text = o.kind ?? "";
                v.body.text = o.body ?? "";
                v.badge.text = o.badge ?? "";
                v.badge.color = Soft; // what the pick would push out, not a rarity since 2026-11-10
                v.edge.effectColor = new Color(o.tint.r, o.tint.g, o.tint.b, .9f);
                v.formIndex = o.formIndex;
                V2Hud.SetWeaponIcon(v.icon, art, o.formIndex);
                v.button.interactable = false;
            }
            iconsIllustrated = Art.Enabled;
            skip.gameObject.SetActive(canLeave);
            skipLabel.text = ArenaText.Pair("不拿", "LEAVE IT");
            hint.text = canLeave ? ArenaText.Pair($"按 {ArenaKeys.SkillKeys} 或点击选择 · Esc 不拿", $"{ArenaKeys.SkillKeys} or click to take one · Esc to leave it")
                : ArenaText.Pair($"按 {ArenaKeys.SkillKeys} 或点击选择", $"{ArenaKeys.SkillKeys} or click to take one");
            openedAt = Time.unscaledTime;
            screen.gameObject.SetActive(true);
            screen.SetAsLastSibling();
        }

        public void Close()
        {
            if (screen) screen.gameObject.SetActive(false);
            pick = null; leave = null;
        }

        /// <summary>The weapons' pictures follow the art switch while the panel is open (2026-11-10: whitebox shapes with the art off).</summary>
        void FollowArtSwitch()
        {
            if (iconsIllustrated == Art.Enabled) return;
            iconsIllustrated = Art.Enabled;
            var gm = GameManager.Instance;
            var art = gm && gm.hud && gm.hud.v2Hud ? gm.hud.v2Hud.art : null;
            for (int i = 0; i < count; i++) V2Hud.SetWeaponIcon(views[i].icon, art, views[i].formIndex);
        }

        void Update()
        {
            if (!Open) return;
            FollowArtSwitch();
            bool ready = Time.unscaledTime - openedAt >= InputDelay;
            for (int i = 0; i < count; i++) if (views[i].button.interactable != ready) views[i].button.interactable = ready;
            if (skip.interactable != ready) skip.interactable = ready;
            var keys = Keyboard.current;
            if (!ready || keys == null) return;
            if (ArenaKeys.Pressed(GameAction.Skill1) || keys.numpad1Key.wasPressedThisFrame) Pick(0);
            else if (ArenaKeys.Pressed(GameAction.Skill2) || keys.numpad2Key.wasPressedThisFrame) Pick(1);
            else if (ArenaKeys.Pressed(GameAction.Skill3) || keys.numpad3Key.wasPressedThisFrame) Pick(2);
            else if (keys.escapeKey.wasPressedThisFrame && mayLeave) Leave();
        }

        void Pick(int index)
        {
            if (!Open || index < 0 || index >= count || Time.unscaledTime - openedAt < InputDelay) return;
            var then = pick;
            Close();
            then?.Invoke(index);
        }

        void Leave()
        {
            if (!Open || !mayLeave || Time.unscaledTime - openedAt < InputDelay) return;
            var then = leave;
            Close();
            then?.Invoke();
        }

        // ───── Building blocks ─────

        void ApplyFont()
        {
            var font = ArenaText.Font;
            if (!font) return;
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = font;
        }

        static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, anchor, position, size, new Vector2(.5f, .5f));
            var image = rect.GetComponent<Image>();
            image.color = colour; image.raycastTarget = false;
            return rect;
        }

        static Text Label(string name, Transform parent, int size, Color colour, TextAnchor anchor)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            label.font = ArenaText.Font ? ArenaText.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size; label.color = colour; label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true; label.raycastTarget = false;
            return label;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f); rect.offsetMax = new Vector2(-8f, -4f);
        }
    }
}
