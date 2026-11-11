using System;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    [DisallowMultipleComponent]
    public sealed class ArenaCodex : MonoBehaviour
    {
        enum Page { Weapons, Skills, Enemies }

        V2Hud hud;
        GameObject overlay;
        RectTransform list;
        Text title, count;
        Font font;
        Page page;
        public bool IsOpen => overlay && overlay.activeSelf;

        public void Initialize(V2Hud owner)
        {
            hud = owner;
            font = FindFont();
            AddMenuButtons();
            BuildOverlay();
        }

        void Update()
        {
            if (overlay && overlay.activeSelf && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                Close();
        }

        void AddMenuButtons()
        {
            if (hud.screens == null) return;
            foreach (var screen in hud.screens)
            {
                if (!screen || screen.state is not (ArenaUiState.Title or ArenaUiState.Paused)) continue;
                var content = screen.transform.Find("Content");
                if (!content || content.Find("Codex")) continue;
                string sourceName = screen.state == ArenaUiState.Title ? "StartRun" : "RestartRun";
                if (content.Find(sourceName) is not RectTransform source) continue;
                var copy = Instantiate(source.gameObject, content);
                copy.name = "Codex";
                foreach (var relay in copy.GetComponents<ArenaUiButton>()) Destroy(relay);
                foreach (var localizer in copy.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true)) Destroy(localizer);
                var button = copy.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(Open);
                var savedLabel = copy.GetComponentInChildren<Text>(true);
                if (savedLabel) savedLabel.gameObject.SetActive(false);
                var label = Label("CodexLabel", copy.transform, savedLabel ? savedLabel.fontSize : 20, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                label.text = ArenaText.Pair("COLLECTION", "COLLECTION");
                label.raycastTarget = false;
                label.transform.SetAsLastSibling();
                if (screen.state == ArenaUiState.Title)
                {
                    var rect = (RectTransform)copy.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
                    rect.anchoredPosition = new Vector2(-30f, 30f);
                    rect.sizeDelta = new Vector2(240f, 64f);
                }
            }
        }

        void BuildOverlay()
        {
            overlay = Node("CodexOverlay", hud.localizedCanvas.transform, typeof(Image));
            Stretch((RectTransform)overlay.transform);
            overlay.GetComponent<Image>().color = new Color(.025f, .035f, .055f, .985f);

            var header = Node("Header", overlay.transform, typeof(Image));
            Place((RectTransform)header.transform, 60, 36, 1480, 96);
            header.GetComponent<Image>().color = new Color(.08f, .12f, .18f, 1f);
            title = Label("Title", header.transform, 36, TextAnchor.MiddleLeft);
            Place(title.rectTransform, 28, 8, 600, 80);
            count = Label("Count", header.transform, 20, TextAnchor.MiddleRight);
            Place(count.rectTransform, 1040, 8, 250, 80);
            AddButton(header.transform, "Close", ArenaText.Pair("BACK  [ESC]", "BACK  [ESC]"), new Rect(1310, 18, 140, 60), Close);

            AddButton(overlay.transform, "Weapons", ArenaText.Pair("WEAPONS", "WEAPONS"), new Rect(60, 148, 230, 62), () => Show(Page.Weapons));
            AddButton(overlay.transform, "Skills", ArenaText.Pair("SKILLS", "SKILLS"), new Rect(306, 148, 230, 62), () => Show(Page.Skills));
            AddButton(overlay.transform, "Enemies", ArenaText.Pair("MONSTERS", "MONSTERS"), new Rect(552, 148, 230, 62), () => Show(Page.Enemies));

            var viewport = Node("Viewport", overlay.transform, typeof(Image), typeof(Mask), typeof(ScrollRect));
            Place((RectTransform)viewport.transform, 60, 230, 1480, 610);
            viewport.GetComponent<Image>().color = new Color(.035f, .055f, .08f, 1f);
            viewport.GetComponent<Mask>().showMaskGraphic = true;
            var content = Node("Entries", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list = (RectTransform)content.transform;
            list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1); list.pivot = new Vector2(.5f, 1);
            list.anchoredPosition = Vector2.zero; list.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18); layout.spacing = 12; layout.childForceExpandHeight = false; layout.childControlHeight = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform; scroll.content = list; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            overlay.SetActive(false);
        }

        public void Open()
        {
            if (!overlay) return;
            overlay.transform.SetAsLastSibling();
            overlay.SetActive(true);
            Show(page);
            V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);
        }

        public void Close()
        {
            if (!overlay) return;
            overlay.SetActive(false);
            V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
        }

        void Show(Page selected)
        {
            page = selected;
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            int unlocked = 0, total = 0;
            switch (page)
            {
                case Page.Weapons:
                    title.text = ArenaText.Pair("WEAPON COLLECTION", "WEAPON COLLECTION");
                    foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
                    {
                        bool found = ArenaCodexProgress.WeaponDiscovered(form); if (found) unlocked++; total++;
                        var data = GameManager.Instance?.runWeapons?.roster?.Get(form);
                        string detail = data == null ? "" : $"DMG {data.damageA:0.#} / {data.damageB:0.#}   ·   CD {data.cooldownA:0.##} / {data.cooldownB:0.##}\n{WeaponRoster.AnomalyName(form, 0)}  ·  {WeaponRoster.AnomalyName(form, 1)}";
                        AddEntry(found, WeaponRoster.FormName(form), detail, Art.Enabled ? SpriteAt(hud.art?.weaponSprites, (int)form) : WhiteboxIcons.Weapon((int)form));
                    }
                    break;
                case Page.Skills:
                    title.text = ArenaText.Pair("SKILL COLLECTION", "SKILL COLLECTION");
                    var catalog = GameManager.Instance?.skills?.Catalog;
                    foreach (SkillId id in Enum.GetValues(typeof(SkillId)))
                    {
                        bool found = ArenaCodexProgress.SkillDiscovered(id); if (found) unlocked++; total++;
                        var data = catalog ? catalog.Find(id) : null;
                        string name = data ? data.displayName : id.ToString();
                        string detail = data ? $"ENERGY {data.cost:0}   ·   COOLDOWN {data.cooldown:0.#}s\n{data.Description(1)}" : "";
                        AddEntry(found, name, detail, Art.Enabled ? SpriteAt(hud.art?.skillIcons, (int)id) : WhiteboxIcons.Skill((int)id));
                    }
                    break;
                default:
                    title.text = ArenaText.Pair("MONSTER COLLECTION", "MONSTER COLLECTION");
                    var roster = GameManager.Instance?.waves?.roster;
                    foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                    {
                        bool found = ArenaCodexProgress.EnemyDiscovered(kind); if (found) unlocked++; total++;
                        EnemyDefinition data = null;
                        if (roster != null) foreach (var candidate in roster) if (candidate && candidate.kind == kind) { data = candidate; break; }
                        string name = data && !string.IsNullOrWhiteSpace(data.displayName) ? data.displayName : kind.ToString();
                        string role = EnemyRoles.IsBoss(kind) ? "BOSS" : EnemyRoles.IsSupport(kind) ? "SUPPORT" : EnemyRoles.IsSpecial(kind) ? "SPECIAL" : EnemyRoles.IsMelee(kind) ? "MELEE" : "RANGED";
                        string detail = data ? $"{role}   ·   HP {data.hp:0}   ·   DAMAGE {data.damage:0.#}   ·   SPEED {data.speed:0.#}" : role;
                        AddEntry(found, name, detail, Art.Enabled ? EnemySprite(kind) : null,
                            !Art.Enabled ? Whitebox.Colour(WhiteboxCharacter.LookIdFor(kind)) : (Color?)null);
                    }
                    break;
            }
            count.text = ArenaText.Pair($"DISCOVERED  {unlocked} / {total}", $"DISCOVERED  {unlocked} / {total}");
            list.anchoredPosition = Vector2.zero;
        }

        void AddEntry(bool found, string entryName, string detail, Sprite sprite, Color? blockColour = null)
        {
            var row = Node("Entry", list, typeof(Image), typeof(LayoutElement));
            row.GetComponent<Image>().color = found ? new Color(.09f, .13f, .18f, 1f) : new Color(.055f, .065f, .08f, 1f);
            row.GetComponent<LayoutElement>().preferredHeight = 116;
            var icon = Node("Icon", row.transform, typeof(Image)).GetComponent<Image>();
            Place(icon.rectTransform, 18, 14, 88, 88); icon.preserveAspect = true; icon.sprite = sprite;
            icon.color = found && blockColour.HasValue ? blockColour.Value : found && sprite ? Color.white : new Color(.12f, .14f, .17f, 1f);
            var name = Label("Name", row.transform, 24, TextAnchor.UpperLeft); Place(name.rectTransform, 128, 17, 310, 40);
            name.text = found ? entryName : ArenaText.Pair("UNKNOWN", "UNKNOWN"); name.color = found ? new Color(.9f, .78f, .42f) : new Color(.42f, .46f, .52f);
            var body = Label("Detail", row.transform, 18, TextAnchor.UpperLeft); Place(body.rectTransform, 450, 18, 970, 82);
            body.horizontalOverflow = HorizontalWrapMode.Wrap; body.verticalOverflow = VerticalWrapMode.Truncate;
            body.text = found ? detail : ArenaText.Pair("Encounter this entry during a run to reveal its records.", "Encounter this entry during a run to reveal its records.");
            body.color = found ? new Color(.82f, .86f, .9f) : new Color(.38f, .42f, .48f);
        }

        Sprite EnemySprite(EnemyKind kind)
        {
            if (!hud.art || hud.art.enemyImages == null) return null;
            foreach (var entry in hud.art.enemyImages)
                if (string.Equals(entry.id, kind.ToString(), StringComparison.OrdinalIgnoreCase)) return entry.sprite;
            return null;
        }

        static Sprite SpriteAt(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
        Font FindFont()
        {
            if (hud.fontLabels != null) foreach (var label in hud.fontLabels) if (label && label.font) return label.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        Text Label(string name, Transform parent, int size, TextAnchor anchor)
        {
            var label = Node(name, parent, typeof(Text)).GetComponent<Text>();
            label.font = font; label.fontSize = size; label.alignment = anchor; label.color = Color.white;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        void AddButton(Transform parent, string name, string text, Rect rect, Action action)
        {
            var go = Node(name, parent, typeof(Image), typeof(Button));
            Place((RectTransform)go.transform, rect.x, rect.y, rect.width, rect.height);
            go.GetComponent<Image>().color = new Color(.14f, .24f, .34f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => action());
            var label = Label(name + "Label", parent, 20, TextAnchor.MiddleCenter);
            Place(label.rectTransform, rect.x, rect.y, rect.width, rect.height);
            label.text = text;
            label.raycastTarget = false;
            label.transform.SetAsLastSibling();
        }

        static GameObject Node(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components); go.transform.SetParent(parent, false); return go;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(left, -top); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
