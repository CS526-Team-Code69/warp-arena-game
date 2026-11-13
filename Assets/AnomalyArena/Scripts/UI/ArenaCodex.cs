using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The collection: the weapons, skills and monsters the player has met, kept from one run to the next (ArenaCodexProgress). A
    /// button in the title's bottom-right corner and one in the pause screen's row open it over the whole screen; Back or Esc closes
    /// it. An entry not met yet shows only "???". Retired weapons (WeaponRoster.IsRetired) are never offered, so they are left out.
    /// V2Hud adds it on every page but the Alpha's (BuildEdition), whose title offers only Start Game and the tutorial.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaCodex : MonoBehaviour
    {
        /// <summary>The name of its button on the title and in the pause screen's row (V2Hud.PauseButtons).</summary>
        public const string ButtonName = "Codex";

        enum Page { Weapons, Skills, Monsters }

        static ArenaCodex shown;
        static int closedFrame = -1;

        // The colours of a page tab: the page on show, the others.
        static readonly Color TabOn = new(.22f, .38f, .54f, 1f), TabOff = new(.12f, .19f, .27f, 1f);

        // Space kept clear round the page, in canvas units.
        const float Margin = 50f;

        V2Hud hud;
        GameObject root, overlay;
        RectTransform list, viewportRect;
        Text title, count;
        Font font;
        bool chinese;
        Page page;
        readonly Dictionary<Page, Image> tabs = new();
        // Every label whose words do not depend on the page, with its words, so a change of language writes them again.
        readonly List<(Text label, Func<string> words)> fixedLabels = new();

        public bool IsOpen => root && root.activeSelf;

        /// <summary>The collection is open, or was closed this frame: the Esc that closed it must not also resume a paused run or
        /// leave the title's map pick (as ArenaSettingsPanel.HoldsInput).</summary>
        public static bool HoldsInput => shown && shown.IsOpen || Time.frameCount == closedFrame;

        public void Initialize(V2Hud owner)
        {
            hud = owner;
            font = ArenaText.Font ? ArenaText.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            chinese = ArenaText.IsChinese;
            AddMenuButtons();
            BuildOverlay();
        }

        void Update()
        {
            // The language changed (the title's Language option): the fixed labels take its words and its font, and an open page is
            // written again.
            var current = ArenaText.Font;
            if (current && (current != font || ArenaText.IsChinese != chinese))
            {
                font = current;
                chinese = ArenaText.IsChinese;
                Relabel();
                if (IsOpen) Show(page);
            }
            if (IsOpen && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        void AddMenuButtons()
        {
            if (hud.screens == null) return;
            foreach (var screen in hud.screens)
            {
                if (!screen || screen.state is not (ArenaUiState.Title or ArenaUiState.Paused)) continue;
                var content = screen.transform.Find("Content");
                if (!content || content.Find(ButtonName)) continue;
                bool onTitle = screen.state == ArenaUiState.Title;
                // A copy of a button of the same screen, so it looks like its neighbours; its own click and words replace the copied ones.
                if (content.Find(onTitle ? "StartRun" : "RestartRun") is not RectTransform source) continue;
                var copy = Instantiate(source.gameObject, content);
                copy.name = ButtonName;
                foreach (var relay in copy.GetComponents<ArenaUiButton>()) Destroy(relay);
                foreach (var localizer in copy.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true)) Destroy(localizer);
                var button = copy.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(Open);
                var savedLabel = copy.GetComponentInChildren<Text>(true);
                if (savedLabel) savedLabel.gameObject.SetActive(false);
                var label = Label("CodexLabel", copy.transform, savedLabel ? savedLabel.fontSize : 20, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                if (savedLabel) label.color = savedLabel.color;
                label.raycastTarget = false;
                label.transform.SetAsLastSibling();
                // The pause screen's row is in capitals; the title's secondary buttons (Settings, Language) are not.
                Fixed(label, onTitle ? () => ArenaText.Pair("图鉴", "Collection") : () => ArenaText.Pair("图鉴", "COLLECTION"));
                if (onTitle)
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
            // A canvas of its own over the title, the HUD and the edge notices (under the settings panel, 29000, and the tutorial's
            // pages, 30000), scaled as the settings panel's.
            root = new GameObject("CodexCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 28000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 810f);
            scaler.matchWidthOrHeight = .5f;

            overlay = Node("CodexOverlay", root.transform, typeof(Image));
            Stretch((RectTransform)overlay.transform);
            overlay.GetComponent<Image>().color = new Color(.025f, .035f, .055f, 1f);

            var header = Node("Header", overlay.transform, typeof(Image));
            var headerRect = (RectTransform)header.transform;
            headerRect.anchorMin = new Vector2(0f, 1f); headerRect.anchorMax = new Vector2(1f, 1f); headerRect.pivot = new Vector2(.5f, 1f);
            headerRect.offsetMin = new Vector2(Margin, -36f - 90f); headerRect.offsetMax = new Vector2(-Margin, -36f);
            header.GetComponent<Image>().color = new Color(.08f, .12f, .18f, 1f);
            title = Label("Title", header.transform, 34, TextAnchor.MiddleLeft);
            Place(title.rectTransform, 28, 8, 640, 74);
            count = Label("Count", header.transform, 20, TextAnchor.MiddleRight);
            PlaceFromRight(count.rectTransform, 190, 8, 330, 74);
            AddButton(header.transform, "Close", () => ArenaText.Pair("返回 [Esc]", "Back [Esc]"), new Rect(14, 14, 160, 62), Close, true);

            tabs[Page.Weapons] = AddButton(overlay.transform, "Weapons", () => ArenaText.Pair("武器", "Weapons"), new Rect(Margin, 146, 230, 58), () => Show(Page.Weapons));
            tabs[Page.Skills] = AddButton(overlay.transform, "Skills", () => ArenaText.Pair("技能", "Skills"), new Rect(Margin + 246, 146, 230, 58), () => Show(Page.Skills));
            tabs[Page.Monsters] = AddButton(overlay.transform, "Monsters", () => ArenaText.Pair("怪物", "Monsters"), new Rect(Margin + 492, 146, 230, 58), () => Show(Page.Monsters));

            var viewport = Node("Viewport", overlay.transform, typeof(Image), typeof(Mask), typeof(ScrollRect));
            viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(Margin, 36f); viewportRect.offsetMax = new Vector2(-Margin, -220f);
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
            root.SetActive(false);
        }

        void OnDestroy()
        {
            if (root) Destroy(root);
        }

        public void Open()
        {
            if (!root) return;
            shown = this;
            root.SetActive(true);
            Canvas.ForceUpdateCanvases(); // the list's width is known before its entries are written
            Show(page);
            V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            closedFrame = Time.frameCount;
            V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
        }

        void Show(Page selected)
        {
            page = selected;
            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            foreach (var (tab, image) in tabs) if (image) image.color = tab == page ? TabOn : TabOff;
            int found = 0, total = 0;
            var gm = GameManager.Instance;
            switch (page)
            {
                case Page.Weapons:
                    title.text = ArenaText.Pair("武器图鉴", "Weapons");
                    foreach (var form in ListedWeapons())
                    {
                        bool met = ArenaCodexProgress.WeaponDiscovered(form);
                        total++; if (met) found++;
                        AddEntry(met, ArenaTerms.WeaponForm(form), met ? WeaponDetail(form) : "",
                            Art.Enabled ? SpriteAt(hud.art ? hud.art.weaponSprites : null, (int)form) : WhiteboxIcons.Weapon((int)form));
                    }
                    break;
                case Page.Skills:
                    title.text = ArenaText.Pair("技能图鉴", "Skills");
                    var catalog = gm && gm.skills ? gm.skills.Catalog : null;
                    foreach (SkillId id in Enum.GetValues(typeof(SkillId)))
                    {
                        var data = catalog ? catalog.Find(id) : null;
                        if (catalog && !data) continue; // not in this game's catalogue
                        bool met = ArenaCodexProgress.SkillDiscovered(id);
                        total++; if (met) found++;
                        AddEntry(met, ArenaTerms.Skill(id), met && data ? data.Description(1) : "",
                            Art.Enabled ? SpriteAt(hud.art ? hud.art.skillIcons : null, (int)id) : WhiteboxIcons.Skill((int)id));
                    }
                    break;
                default:
                    title.text = ArenaText.Pair("怪物图鉴", "Monsters");
                    var roster = gm && gm.waves ? gm.waves.roster : null;
                    foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                    {
                        if (roster != null && !InRoster(roster, kind)) continue;
                        bool met = ArenaCodexProgress.EnemyDiscovered(kind);
                        total++; if (met) found++;
                        AddEntry(met, ArenaText.Text("enemy." + kind), met ? $"<color=#9ad8ff>{Role(kind)}</color>\n{MonsterNote(kind)}" : "",
                            Art.Enabled ? EnemySprite(kind) : null, !Art.Enabled ? Whitebox.Colour(WhiteboxCharacter.LookIdFor(kind)) : (Color?)null);
                    }
                    break;
            }
            count.text = ArenaText.Pair($"已发现 {found} / {total}", $"Found {found} of {total}");
            list.anchoredPosition = Vector2.zero;
        }

        /// <summary>The weapons the collection lists: every form but the retired ones, which are never offered.</summary>
        public static IEnumerable<WeaponForm> ListedWeapons()
        {
            foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
                if (!WeaponRoster.IsRetired(form)) yield return form;
        }

        /// <summary>How a weapon met so far fights: the pistol's one way; for an anomaly weapon, each of its two anomalies that has
        /// turned up (RunWeapons.Reveal keeps them), by name.</summary>
        static string WeaponDetail(WeaponForm form)
        {
            if (!RunWeapon.IsAnomalousForm(form)) return WeaponGuide.How(form, 0);
            var lines = new List<string>();
            int missing = 0;
            for (int variant = 0; variant < 2; variant++)
                if (ArenaCodexProgress.AnomalyRevealed(form, variant))
                    lines.Add($"<b>{ArenaTerms.WeaponAnomaly(form, variant)}</b>{ArenaText.Pair("：", ": ")}{WeaponGuide.How(form, variant)}");
                else missing++;
            if (missing == 1) lines.Add(ArenaText.Pair("它还有一种异常形态没有出现过。", "One of its anomalies has not turned up yet."));
            if (missing == 2) lines.Add(ArenaText.Pair("它的两种异常形态都还没有出现过。", "Neither of its anomalies has turned up yet."));
            return string.Join("\n", lines);
        }

        static string Role(EnemyKind kind) =>
            EnemyRoles.IsBoss(kind) ? ArenaText.Pair("首领", "Boss")
            : EnemyRoles.IsSupport(kind) ? ArenaText.Pair("辅助", "Support")
            : EnemyRoles.IsSpecial(kind) ? ArenaText.Pair("特殊", "Special")
            : EnemyRoles.IsMelee(kind) ? ArenaText.Pair("近战", "Melee")
            : ArenaText.Pair("远程", "Ranged");

        /// <summary>What a monster does, in a sentence or two.</summary>
        static string MonsterNote(EnemyKind kind) => kind switch
        {
            EnemyKind.Grunt => ArenaText.Pair("最常见的怪，走到跟前出拳。单只不难对付，成群围上来时要留好退路。",
                "The most common monster. It walks up and punches. One is easy; a crowd can box you in, so keep a way out."),
            EnemyKind.Brute => ArenaText.Pair("块头大、走得慢，比小怪打得疼，也更耐打。",
                "Big and slow. It hits harder than a Grunt and takes longer to bring down."),
            EnemyKind.Skitter => ArenaText.Pair("跑得最快的小个子，伤害不高，却总是第一个贴上来。",
                "Small and quick. It does little damage, but it is always the first to reach you."),
            EnemyKind.Splitter => ArenaText.Pair("被打死时会裂成两只小的，小的也会接着追你。",
                "When it dies it splits into two smaller ones, and they keep coming."),
            EnemyKind.Spitter => ArenaText.Pair("隔着一段距离吐毒刺，每被打中一次，中毒就多一层。",
                "It spits poison darts from a distance. Every hit adds another layer of poison."),
            EnemyKind.Warden => ArenaText.Pair("自己从不出手，专给身边的怪加护盾。先把它打掉，其他怪会好打得多。",
                "It never attacks; it shields the monsters around it. Take it down first and the rest get much easier."),
            EnemyKind.Chanter => ArenaText.Pair("站在后面吟唱，让身边的怪打得更狠。",
                "It stays back and chants, making the monsters around it hit harder."),
            EnemyKind.Rammer => ArenaText.Pair("先压低身子蓄力，然后沿直线猛冲过来。看到它蓄力就往旁边躲。",
                "It crouches to build up, then charges in a straight line. When you see it crouch, step aside."),
            EnemyKind.Bloater => ArenaText.Pair("靠近你或者被打死时都会爆炸，炸完还会留下一片辐射区，站在里面会持续掉血。",
                "It explodes when it gets close or when it dies, and leaves a patch of radiation that hurts while you stand in it."),
            EnemyKind.Spire => ArenaText.Pair("扎在原地不动，朝一片地面发动攻击。离开它标出的那片地面就安全了。",
                "It never moves and strikes a patch of ground. Step out of the marked patch and you are safe."),
            EnemyKind.Stomper => ArenaText.Pair("重重地跺向地面，被震到会僵住一小会儿。",
                "It stamps the ground hard; caught in the blast, you freeze for a moment."),
            EnemyKind.Shade => ArenaText.Pair("隐着身摸过来，一出手就很重。",
                "It creeps up unseen, and when it strikes, it strikes hard."),
            EnemyKind.Hollow => ArenaText.Pair("虚空试炼尽头的首领。",
                "The boss waiting at the end of the Void Trial."),
            EnemyKind.Tyrant => ArenaText.Pair("挡在回家路上的最后一个首领。",
                "The last boss, standing between you and the way home."),
            _ => ""
        };

        static bool InRoster(EnemyDefinition[] roster, EnemyKind kind)
        {
            foreach (var candidate in roster) if (candidate && candidate.kind == kind) return true;
            return false;
        }

        void AddEntry(bool found, string entryName, string detail, Sprite sprite, Color? blockColour = null)
        {
            var row = Node("Entry", list, typeof(Image), typeof(LayoutElement));
            row.GetComponent<Image>().color = found ? new Color(.09f, .13f, .18f, 1f) : new Color(.055f, .065f, .08f, 1f);
            var icon = Node("Icon", row.transform, typeof(Image)).GetComponent<Image>();
            Place(icon.rectTransform, 18, 14, 88, 88); icon.preserveAspect = true; icon.sprite = sprite;
            icon.color = found && blockColour.HasValue ? blockColour.Value : found && sprite ? Color.white : new Color(.12f, .14f, .17f, 1f);
            var name = Label("Name", row.transform, 24, TextAnchor.UpperLeft); Place(name.rectTransform, 128, 17, 310, 40);
            // Not met yet: nothing about it shows.
            name.text = found ? entryName : "???";
            name.color = found ? new Color(.9f, .78f, .42f) : new Color(.42f, .46f, .52f);
            // The words run to the list's right edge, whatever the screen's width.
            float detailWidth = Mathf.Max(240f, (viewportRect ? viewportRect.rect.width : 1340f) - 36f - 450f - 24f);
            var body = Label("Detail", row.transform, 18, TextAnchor.UpperLeft); Place(body.rectTransform, 450, 18, detailWidth, 82);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.text = found ? detail : "";
            body.color = new Color(.82f, .86f, .9f);
            // A longer entry (two anomalies, a long skill) grows to fit its words.
            row.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(116f, body.preferredHeight + 36f);
        }

        Sprite EnemySprite(EnemyKind kind)
        {
            if (!hud.art || hud.art.enemyImages == null) return null;
            foreach (var entry in hud.art.enemyImages)
                if (string.Equals(entry.id, kind.ToString(), StringComparison.OrdinalIgnoreCase)) return entry.sprite;
            return null;
        }

        static Sprite SpriteAt(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;

        void Fixed(Text label, Func<string> words)
        {
            fixedLabels.Add((label, words));
            label.text = words();
        }

        void Relabel()
        {
            foreach (var (label, words) in fixedLabels)
                if (label) { label.font = font; label.text = words(); }
            if (title) title.font = font;
            if (count) count.font = font;
        }

        Text Label(string name, Transform parent, int size, TextAnchor anchor)
        {
            var label = Node(name, parent, typeof(Text)).GetComponent<Text>();
            label.font = font; label.fontSize = size; label.alignment = anchor; label.color = Color.white;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        /// <summary>A button and its label; rect.x is from the parent's left edge, or from its right edge when fromRight.</summary>
        Image AddButton(Transform parent, string name, Func<string> words, Rect rect, Action action, bool fromRight = false)
        {
            var go = Node(name, parent, typeof(Image), typeof(Button));
            var image = go.GetComponent<Image>();
            image.color = TabOff;
            go.GetComponent<Button>().onClick.AddListener(() => action());
            var label = Label(name + "Label", parent, 20, TextAnchor.MiddleCenter);
            foreach (var placed in new[] { (RectTransform)go.transform, label.rectTransform })
                if (fromRight) PlaceFromRight(placed, rect.x, rect.y, rect.width, rect.height);
                else Place(placed, rect.x, rect.y, rect.width, rect.height);
            label.raycastTarget = false;
            label.transform.SetAsLastSibling();
            Fixed(label, words);
            return image;
        }

        static GameObject Node(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components); go.transform.SetParent(parent, false); return go;
        }

        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        static void PlaceFromRight(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-right, -top); rect.sizeDelta = new Vector2(width, height);
        }

        static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(left, -top); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
