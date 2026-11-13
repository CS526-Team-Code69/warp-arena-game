using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The battle HUD of the fifth interface pass (work/art-plans/style-1112/ui-e/hud.html), the part both modes share, built over V2Hud's
    /// saved HUD (V2Canvas/GameplayHud) when V5HudKit.On: the vitals at the top left (the helmet portrait in the gauge ring, life and stamina
    /// in the plate beside it), the dock along the bottom (V5BattleSlot: auto, hand and skill slots in one row, scaled down together when
    /// they would leave the band between mock-up x 300 and 1594), the key hints at the bottom right, the boss plate, the banners, the
    /// statuses beside the vitals, the charge bar over the hands and the hint plate under the top centre (V5BattleHud.Hint.cs); in the
    /// open mode also the wave plate at the top centre and the wallet at the top right (V5BattleHud.Top.cs). While the story runs (the
    /// snapshot's hideXp) the top centre and the wallet are the story layer's (V2CampaignUi), and V2Hud's own wave lines, experience
    /// header and energy stay hidden in both modes.
    /// The saved pieces it replaces are hidden, never removed, and V2Hud keeps writing their words (tests and the analytics read them).
    /// Positions are set here at run time, so the values saved in the scene for the old HUD (V2HudLayout) do not move the new one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class V5BattleHud : V5HudPiece
    {
        const float Css = V5BattleKit.Css;
        // The dock (mock-up pixels): 20 above the foot, slots 10 apart, inside the band from x 300 to x 1594 around the middle at 960.
        const float DockBottom = 20f, DockGap = 10f, DockHalfWidth = 634f;
        // Canvas units from the foot that the story's lamp box reaches (its top, about 124), with a margin for a marker's half height.
        const float StoryLampClear = 142f;
        // The story's subtitle band reaches about 158 units up (V5StoryLines): the bottom markers keep above it.
        const float StorySubtitleClear = 180f;
        // In the story the dock stops short of the lamp box at the bottom right: on a 16:9 screen the box's left edge, in English with ten
        // lamps or more, stands about 360 mock-up pixels in from the right edge. Until the story layer has laid the box out, its reach is
        // taken as LampReach. DockClear is kept between the dock and what stands at the bottom right; the dock is never narrower than
        // DockHalfLeast on either side of the middle.
        const float StoryDockHalfWidth = 590f, LampReach = 340f, DockClear = 12f, DockHalfLeast = 200f;
        // In the story the markers along the top keep below the goal plate at the top left, which reaches about 235 canvas units down
        // with a goal, a prompt, a note and the side quest.
        const float StoryGoalClear = 250f;
        // The boss plate under the top centre's plate (which ends 104 down), the banner under it.
        const float BossTop = 116f, BossW = 560f, BossH = 64f, BannerTop = 196f, BannerW = 820f, BannerH = 152f, NoticeH = 36f;
        const float HintsRight = 26f, HintsBottom = 26f, HintKeyGap = 8f, HintItemGap = 26f;
        const float BannerIn = .22f, BannerOut = .18f, BannerDrop = 12f, PulseSeconds = .28f, PulseGrow = .14f;

        V2Hud hud;
        RectTransform root;
        bool built;
        bool? shownChinese;
        bool snapshotStory;
        int shownKeys = -1;

        /// <summary>Builds the battle HUD over a V2Hud's saved one (once); null where there is nothing to build on.</summary>
        public static V5BattleHud Install(V2Hud hud)
        {
            if (!hud || !hud.gameplayRoot) return null;
            if (hud.TryGetComponent<V5BattleHud>(out var existing)) return existing;
            var battle = hud.gameObject.AddComponent<V5BattleHud>();
            battle.Build(hud);
            return battle;
        }

        void Build(V2Hud saved)
        {
            hud = saved;
            root = (RectTransform)hud.gameplayRoot.transform;
            V5BattleKit.Fallback = hud.hpLabel ? hud.hpLabel.font : hud.hintLabel ? hud.hintLabel.font : null;
            HideSavedPieces();
            BuildVitals();
            BuildWave();
            BuildWallet();
            BuildKeyHints();
            DressBoss();
            DressStatuses();
            DressChargeBar();
            BuildBanner();
            BuildHint();
            InstallSlots();
            built = true;
            LayOut();
            Redress();
        }

        /// <summary>The saved resource panel (life, stamina, energy), the experience header, the banner's own words, the boss's immunity
        /// line and the hint line over the hands: the new pieces show what they showed. Their objects stay, and V2Hud still writes their
        /// words.</summary>
        void HideSavedPieces()
        {
            var panel = root.Find("ResourcePanel") ?? (hud.hpLabel ? hud.hpLabel.transform.parent : null);
            if (panel && panel != root && panel.gameObject.activeSelf)
            {
                hiddenPanel = panel.gameObject;
                hiddenPanel.SetActive(false);
            }
            var header = hud.xpLabel ? hud.xpLabel.transform.parent : null;
            if (header && header != root && header.gameObject.activeSelf)
            {
                hiddenHeader = header.gameObject;
                hiddenHeader.SetActive(false);
            }
            if (hud.bannerLabel) hud.bannerLabel.enabled = false;
            if (hud.immunityLabel) hud.immunityLabel.enabled = false;
            if (hud.hintLabel) hud.hintLabel.enabled = false;
        }

        GameObject hiddenPanel, hiddenHeader;

        /// <summary>Takes back a build that stopped half way (V2Hud logs the fault and calls this): the saved pieces it hid show again,
        /// the pieces it made go, the slots it restyled go back to V2UiSlot, and the component goes at once, so the saved layout
        /// (V2HudLayout.Apply) places the saved HUD again and it works as it did without the fifth pass.</summary>
        public static void Abandon(V2Hud saved)
        {
            if (!saved || !saved.TryGetComponent<V5BattleHud>(out var battle)) return;
            try { battle.TakeBack(); }
            catch (System.Exception fault) { Debug.LogException(fault); }
            // At once rather than at the frame's end: the saved layout below lays the play screen out itself only once this is gone.
            DestroyImmediate(battle);
            var layout = saved.GetComponentInParent<V2HudLayout>(true);
            if (!layout) return;
            try { layout.Apply(); }
            catch (System.Exception fault) { Debug.LogException(fault); }
        }

        void TakeBack()
        {
            if (hiddenPanel) hiddenPanel.SetActive(true);
            if (hiddenHeader) hiddenHeader.SetActive(true);
            if (!hud) return;
            if (hud.bannerLabel) hud.bannerLabel.enabled = true;
            if (hud.immunityLabel) hud.immunityLabel.enabled = true;
            if (hud.hintLabel) hud.hintLabel.enabled = true;
            TakeBack(hud.handSlots);
            TakeBack(hud.autoSlots);
            TakeBack(hud.skillSlots);
            // Every piece made here is named V5...: those on the play screen and those inside the boss's bars.
            if (root) RemoveMade(root);
            if (hud.bossRoot) RemoveMade(hud.bossRoot.transform);
        }

        static void TakeBack(V2UiSlot[] slots)
        {
            if (slots == null) return;
            foreach (var slot in slots)
                if (slot && slot.TryGetComponent<V5BattleSlot>(out var v5)) v5.TakeBack();
        }

        static void RemoveMade(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith("V5", System.StringComparison.Ordinal)) Destroy(child.gameObject);
            }
        }

        void InstallSlots()
        {
            Install(hud.handSlots, V5BattleSlot.Kind.Hand);
            Install(hud.autoSlots, V5BattleSlot.Kind.Auto);
            Install(hud.skillSlots, V5BattleSlot.Kind.Skill);
        }

        static void Install(V2UiSlot[] slots, V5BattleSlot.Kind kind)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++) V5BattleSlot.Install(slots[i], kind, i);
        }

        /// <summary>Whether this image is dressed here (V2Hud leaves it out of its whitebox bars, so only one of them dresses it).</summary>
        public bool Dresses(Image image) => image && (image == hud.chargeFill || image == hud.bossHpFill || image == hud.bossShieldFill);

        /// <summary>Lays every piece out again (V2HudLayout's Apply comes here while the game runs).</summary>
        public void LayOut()
        {
            if (!built) return;
            LayVitals();
            // The key hints first: the dock keeps clear of them (DockHalf).
            LayKeyHints();
            dockKey = -1;
            LayDock();
            PlaceEdgeFloor(snapshotStory);
        }

        /// <summary>Draws the run (V2Hud.Render, ten times a second): banner is the banner's words now, toast whether they are a passing
        /// one-line notice (V2Hud.ShowBanner) rather than the run's own banner.</summary>
        public void Render(ArenaUiSnapshot s, string banner, bool toast)
        {
            if (!built || s == null) return;
            bool story = snapshotStory = s.hideXp;
            // The mid-wave card pick draws its own key row at the bottom: the key hints step aside meanwhile (V2Hud hides the dock).
            if (hints && hints.gameObject.activeSelf == (s.state == ArenaUiState.LevelUp)) hints.gameObject.SetActive(s.state != ArenaUiState.LevelUp);
            bool relabel = shownChinese != ArenaText.IsChinese || shownKeys != ArenaKeys.Revision;
            shownChinese = ArenaText.IsChinese;
            shownKeys = ArenaKeys.Revision;
            RenderVitals(s, relabel);
            RenderWave(s, !story);
            RenderWallet(s, !story);
            RenderBoss(s);
            RenderStatuses();
            if (relabel) LayKeyHints();
            // The dash's key joins the hints once the void trial is won, and stands there in a continued run that had won it; the dock
            // then keeps clear of the wider row (DockHalf).
            bool dash = GameManager.Instance && GameManager.Instance.VoidDashUnlocked;
            if (dash != dashShown)
            {
                dashShown = dash;
                LayKeyHints();
            }
            LayDock();
            PlaceEdgeFloor(story);
            RenderChargeBar(s);
            RenderHint(s);
            RenderBanner(banner, toast);
        }

        protected override void Dress(bool art)
        {
            if (!built) return;
            DressPortrait(art);
            DressBanner(art);
            DressBars(art);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!built) return;
            float dt = Time.unscaledDeltaTime;
            TickLife(dt);
            TickBanner(dt);
            TickHint(dt);
            TickPulses();
        }

        // ───── The dock ─────

        readonly List<V2UiSlot> docked = new();
        int dockKey = -1;
        float dockTop = DockBottom + 124f, handsCentre, dockHalf = -1f;
        Canvas canvas;
        V2CampaignUi storyLayer;

        /// <summary>The slots shown, in one row in the mock-up's order (the auto slots, the hands, then the skills), centred, 10 apart, their
        /// feet on one line; scaled down together when the row would leave the band (DockHalf). Laid out again whenever the slots shown
        /// or the band change.</summary>
        void LayDock()
        {
            int key = 0, bit = 0;
            docked.Clear();
            Collect(hud.autoSlots, ref key, ref bit);
            Collect(hud.handSlots, ref key, ref bit);
            Collect(hud.skillSlots, ref key, ref bit);
            float half = DockHalf(snapshotStory);
            if (key == dockKey && Mathf.Abs(half - dockHalf) < .5f) return;
            dockKey = key;
            dockHalf = half;
            float total = -DockGap, tallest = 0f;
            foreach (var slot in docked)
            {
                var size = slot.v5.Size;
                total += size.x + DockGap;
                tallest = Mathf.Max(tallest, size.y);
            }
            float k = total > half * 2f ? half * 2f / total : 1f;
            float x = -total * k * .5f, hands = 0f;
            int handCount = 0;
            foreach (var slot in docked)
            {
                var size = slot.v5.Size;
                var rect = (RectTransform)slot.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
                rect.sizeDelta = size * Css;
                float centre = x + size.x * k * .5f;
                rect.anchoredPosition = new Vector2(centre * Css, DockBottom * Css);
                rect.localScale = new Vector3(k, k, 1f);
                if (slot.v5.SlotKind == V5BattleSlot.Kind.Hand) { hands += centre; handCount++; }
                x += (size.x + DockGap) * k;
            }
            dockTop = DockBottom + (docked.Count > 0 ? tallest * k : 0f);
            handsCentre = handCount > 0 ? hands / handCount : 0f;
            PlaceChargeBar();
        }

        /// <summary>Half the dock's width in mock-up pixels: the band's (590 in the story, where the lamp box stands at the bottom right),
        /// and never more than the canvas's own half width less what stands at the bottom right (the key hints; in the story also the
        /// lamp box), so on a window narrower than 16:9 the slots shrink instead of running under them.</summary>
        float DockHalf(bool story)
        {
            float half = story ? StoryDockHalfWidth : DockHalfWidth;
            if (!canvas && root) canvas = root.GetComponentInParent<Canvas>();
            if (!canvas) return half;
            var whole = canvas.rootCanvas;
            float canvasHalf = ((RectTransform)whole.transform).rect.width * .5f / Css;
            float corner = hints ? HintsRight + hints.sizeDelta.x / Css : 0f;
            if (story) corner = Mathf.Max(corner, StoryLampReach(whole.scaleFactor));
            return Mathf.Max(DockHalfLeast, Mathf.Min(half, canvasHalf - corner - DockClear));
        }

        /// <summary>How far in from the right edge the story's lamp box reaches, in mock-up pixels of this canvas: as the story layer last
        /// laid it out (it grows with English and with ten lamps or more), and never less than LampReach, so the dock does not change
        /// when the box first shows.</summary>
        float StoryLampReach(float scale)
        {
            if (!storyLayer && GameManager.Instance && GameManager.Instance.TryGetComponent<V2Campaign>(out var campaign)) storyLayer = campaign.Ui;
            float pixels = storyLayer ? storyLayer.StoryLampReach : 0f;
            return Mathf.Max(LampReach, pixels / Mathf.Max(1e-4f, scale) / Css);
        }

        V2EnemyDirections directions;
        V2WorldLabels worldLabels;
        bool edgesSought;

        /// <summary>The screen-edge markers keep clear of the dock and the charge bar over it; while the story runs, also of its lamp box at the
        /// bottom right (the story layer's, up to about 124 units from the foot of this canvas), which the markers on the right edge
        /// would reach, and of the goal plate at the top left (the markers along the top keep below it; the open mode keeps its own top
        /// inset). Written at every draw: each of the two sets its own top inset when it wakes, which can come after this.</summary>
        void PlaceEdgeFloor(bool story)
        {
            float floor = Mathf.Max((dockTop + 44f) * Css, story ? Mathf.Max(StoryLampClear, StorySubtitleClear) : 0f);
            if (!edgesSought)
            {
                edgesSought = true;
                directions = hud.GetComponentInChildren<V2EnemyDirections>(true);
                worldLabels = hud.GetComponentInChildren<V2WorldLabels>(true);
            }
            if (directions)
            {
                directions.bottomInset = floor;
                directions.topInset = story ? Mathf.Max(V2EnemyDirections.FifthTopInset, StoryGoalClear) : V2EnemyDirections.FifthTopInset;
            }
            if (worldLabels)
            {
                worldLabels.edgeBottomInset = floor;
                worldLabels.edgeTopInset = story ? Mathf.Max(V2WorldLabels.FifthTopInset, StoryGoalClear) : V2WorldLabels.FifthTopInset;
            }
        }

        void Collect(V2UiSlot[] slots, ref int key, ref int bit)
        {
            if (slots == null) return;
            foreach (var slot in slots)
            {
                if (slot && slot.v5 && slot.gameObject.activeSelf)
                {
                    docked.Add(slot);
                    key |= 1 << bit;
                }
                bit++;
            }
        }

        // ───── The key hints (bottom right) ─────

        RectTransform hints;
        V5HudKey interactKey, pauseKey, dashKey;
        Text interactWord, pauseWord, dashWord;
        bool dashShown;

        void BuildKeyHints()
        {
            hints = V5TitleKit.Rect(root, "V5KeyHints");
            V5BattleKit.Anchor(hints, new Vector2(1f, 0f), new Vector2(-HintsRight * Css, HintsBottom * Css), new Vector2(300f, 28f) * Css);
            interactKey = V5HudKit.Key(hints, "InteractKey", ArenaKeys.InteractKey, false, Css, 28f, 15f);
            interactWord = V5BattleKit.Label(hints, "InteractWord", 16f, V5TitleKit.Ink2, TextAnchor.MiddleRight);
            pauseKey = V5HudKit.Key(hints, "PauseKey", "Esc", false, Css, 28f, 15f);
            pauseWord = V5BattleKit.Label(hints, "PauseWord", 16f, V5TitleKit.Ink2, TextAnchor.MiddleRight);
            // Space is a fixed key, never one of the actions the settings move, so its cap is named here; it shows once the dash is won.
            dashKey = V5HudKit.Key(hints, "DashKey", "Space", false, Css, 28f, 15f);
            dashWord = V5BattleKit.Label(hints, "DashWord", 16f, V5TitleKit.Ink2, TextAnchor.MiddleRight);
            dashKey.gameObject.SetActive(false);
            dashWord.gameObject.SetActive(false);
        }

        /// <summary>"E 互动" and "Esc 暂停", right to left from the corner; the interact key is the one the player has it on (ArenaKeys).
        /// Once the void dash is won, "Space 冲刺" stands left of them; nothing of it shows before.</summary>
        void LayKeyHints()
        {
            if (!hints) return;
            interactKey.SetText(ArenaKeys.InteractKey);
            V5BattleKit.Sync(interactWord);
            V5BattleKit.Sync(pauseWord);
            interactWord.text = ArenaText.Pair("互动", "Interact");
            pauseWord.text = ArenaText.Pair("暂停", "Pause");
            V5BattleKit.Sync(dashWord);
            dashWord.text = ArenaText.Pair("冲刺", "Dash");
            if (dashKey.gameObject.activeSelf != dashShown) dashKey.gameObject.SetActive(dashShown);
            if (dashWord.gameObject.activeSelf != dashShown) dashWord.gameObject.SetActive(dashShown);
            float right = 0f;
            right = PlaceHint(pauseWord, pauseKey, right);
            right = PlaceHint(interactWord, interactKey, right + HintItemGap * Css);
            if (dashShown) right = PlaceHint(dashWord, dashKey, right + HintItemGap * Css);
            hints.sizeDelta = new Vector2(right, 28f * Css);
        }

        static float PlaceHint(Text word, V5HudKey key, float right)
        {
            float wordWidth = V5BattleKit.Width(word) + 2f;
            V5BattleKit.Anchor(word.rectTransform, new Vector2(1f, .5f), new Vector2(-right, 0f), new Vector2(wordWidth, 28f * Css));
            right += wordWidth + HintKeyGap * Css;
            var rect = key.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, .5f);
            rect.anchoredPosition = new Vector2(-right, 0f);
            return right + rect.sizeDelta.x;
        }

        // ───── The boss plate ─────

        RectTransform boss;
        Text bossName, bossNumbers, bossImmune;
        Image bossHpTrack, bossShieldTrack;

        /// <summary>The saved boss bars (V2Hud.bossRoot, shown in the void trial and against the Tyrant) as one plate under the top centre:
        /// the boss's name and its immunities, its life in numbers on the right, its life bar and its shield under them.</summary>
        void DressBoss()
        {
            if (!hud.bossRoot) return;
            boss = (RectTransform)hud.bossRoot.transform;
            V5BattleKit.Anchor(boss, new Vector2(.5f, 1f), new Vector2(0f, -BossTop * Css), new Vector2(BossW, BossH) * Css);
            var plate = V5HudKit.Plate(boss, "V5Plate", V5HudKit.PlateLook.Plain, 10f, Css);
            V5TitleKit.Stretch(plate.Rect);
            plate.transform.SetAsFirstSibling();
            bossName = V5BattleKit.Label(boss, "V5Name", 18f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(bossName);
            bossImmune = V5BattleKit.Label(boss, "V5Immune", 12f, V5TitleKit.Ink3, TextAnchor.MiddleLeft, true);
            bossNumbers = V5BattleKit.Label(boss, "V5Numbers", 15f, V5TitleKit.Ink, TextAnchor.MiddleRight, true);
            V5BattleKit.Place(bossNumbers.rectTransform, BossW * .5f, 8f, BossW * .5f - 16f, 22f);
            bossHpTrack = Track(hud.bossHpFill, 36f, 12f);
            bossShieldTrack = Track(hud.bossShieldFill, 51f, 4f);
            if (hud.bossImmunityIcons != null)
                foreach (var icon in hud.bossImmunityIcons)
                    if (icon)
                    {
                        icon.transform.SetParent(boss, false);
                        icon.preserveAspect = true;
                    }
        }

        Image Track(Image fill, float top, float height)
        {
            var back = fill ? fill.transform.parent as RectTransform : null;
            if (!back || back == boss) return null;
            V5BattleKit.Place(back, 16f, top, BossW - 32f, height);
            V5TitleKit.Stretch(fill.rectTransform);
            if (!back.TryGetComponent<Image>(out var track)) return null;
            track.sprite = WhiteboxSkin.Square;
            track.type = Image.Type.Simple;
            track.color = V5HudKit.Track;
            return track;
        }

        void RenderBoss(ArenaUiSnapshot s)
        {
            if (!boss || !hud.bossRoot.activeSelf) return;
            V5BattleKit.Sync(bossName);
            V5BattleKit.Sync(bossNumbers);
            V5BattleKit.Sync(bossImmune);
            string name = s.state == ArenaUiState.FinalBoss ? ArenaText.Pair("暴虐君王", "The Tyrant King") : ArenaText.Pair("纳什先知", "The Nash Prophet");
            if (bossName.text != name) bossName.text = name;
            bossNumbers.text = $"{Mathf.CeilToInt(s.bossHp)}<size={V5BattleKit.Font(12f)}><color=#{V5BattleKit.Hex(V5TitleKit.Ink3)}> / {Mathf.CeilToInt(s.bossMaxHp)}</color></size>";
            float nameWidth = V5BattleKit.Width(bossName) / Css;
            V5BattleKit.Place(bossName.rectTransform, 16f, 8f, nameWidth + 4f, 22f);
            // The immunities after the name: a word, then the element icons V2Hud shows.
            float x = 16f + nameWidth + 14f;
            bool any = false;
            if (hud.bossImmunityIcons != null)
                foreach (var icon in hud.bossImmunityIcons)
                    if (icon && icon.gameObject.activeSelf) { any = true; break; }
            bossImmune.gameObject.SetActive(any);
            if (any)
            {
                bossImmune.text = ArenaText.Pair("免疫", "IMMUNE");
                float wordWidth = V5BattleKit.Width(bossImmune) / Css;
                V5BattleKit.Place(bossImmune.rectTransform, x, 8f, wordWidth + 2f, 22f);
                x += wordWidth + 6f;
                foreach (var icon in hud.bossImmunityIcons)
                {
                    if (!icon || !icon.gameObject.activeSelf) continue;
                    V5BattleKit.Place(icon.rectTransform, x, 9f, 20f, 20f);
                    x += 24f;
                }
            }
        }

        // ───── The statuses beside the vitals ─────

        readonly V5HudPlate[] statusPlates = new V5HudPlate[4];
        // Mock-up pixels: right of the vitals' plate (which ends at 426), on the portrait's middle.
        const float StatusLeft = 440f, StatusTop = 49f, StatusH = 30f, StatusIcon = 22f, StatusGap = 6f;

        /// <summary>The saved status icons and counts (fire, poison, frost, lightning on the player) each on a small plate, in a row beside the
        /// vitals; the line that says frozen or paralysed under them.</summary>
        void DressStatuses()
        {
            if (hud.statusIcons == null || hud.statusCounts == null) return;
            for (int i = 0; i < Mathf.Min(4, Mathf.Min(hud.statusIcons.Length, hud.statusCounts.Length)); i++)
            {
                var icon = hud.statusIcons[i];
                var count = hud.statusCounts[i];
                if (!icon || !count) continue;
                var plate = statusPlates[i] = V5HudKit.Plate(root, "V5Status" + i, V5HudKit.PlateLook.Plain, 6f, Css);
                plate.transform.SetSiblingIndex(Mathf.Min(icon.transform.GetSiblingIndex(), count.transform.GetSiblingIndex()));
                plate.gameObject.SetActive(false);
                icon.preserveAspect = true;
                count.resizeTextForBestFit = false;
                count.fontSize = V5BattleKit.Font(16f);
                count.fontStyle = FontStyle.Bold;
                count.alignment = TextAnchor.MiddleLeft;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Overflow;
                count.color = V5TitleKit.Ink;
            }
            var line = hud.statusLabel;
            if (!line) return;
            line.resizeTextForBestFit = false;
            line.fontSize = V5BattleKit.Font(14f);
            line.fontStyle = FontStyle.Bold;
            line.alignment = TextAnchor.MiddleLeft;
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.verticalOverflow = VerticalWrapMode.Overflow;
            line.color = V5TitleKit.Frost;
            V5BattleKit.Place(line.rectTransform, StatusLeft, StatusTop + StatusH + 4f, 300f, 20f);
        }

        void RenderStatuses()
        {
            float x = StatusLeft;
            for (int i = 0; i < statusPlates.Length; i++)
            {
                var plate = statusPlates[i];
                if (!plate) continue;
                var icon = hud.statusIcons[i];
                var count = hud.statusCounts[i];
                bool on = icon.gameObject.activeSelf;
                if (plate.gameObject.activeSelf != on) plate.gameObject.SetActive(on);
                if (!on) continue;
                float countWidth = (count.gameObject.activeSelf ? V5BattleKit.Width(count) : 0f) / Css;
                float width = 6f + StatusIcon + 4f + countWidth + 8f;
                V5BattleKit.Place(plate.Rect, x, StatusTop, width, StatusH);
                V5BattleKit.Place(icon.rectTransform, x + 6f, StatusTop + (StatusH - StatusIcon) * .5f, StatusIcon, StatusIcon);
                V5BattleKit.Place(count.rectTransform, x + 6f + StatusIcon + 4f, StatusTop, countWidth + 4f, StatusH);
                x += width + StatusGap;
            }
            // Frozen in the frost's colour, paralysed in the lightning's.
            if (hud.statusLabel) hud.statusLabel.color = hud.statusLabel.text == ArenaText.Text("ui.paralysed") ? V5TitleKit.Bolt : V5TitleKit.Frost;
        }

        // ───── The charge bar over the hands (the hint that stood over it is the plate at the top: V5BattleHud.Hint.cs) ─────

        RectTransform chargeBar;
        Image chargeTrack;

        void DressChargeBar()
        {
            if (hud.chargeFill && hud.chargeFill.transform.parent is RectTransform bar && bar != root)
            {
                chargeBar = bar;
                chargeTrack = bar.GetComponent<Image>();
                if (chargeTrack)
                {
                    chargeTrack.sprite = WhiteboxSkin.Square;
                    chargeTrack.type = Image.Type.Simple;
                    chargeTrack.color = V5HudKit.Track;
                }
                V5TitleKit.Stretch(hud.chargeFill.rectTransform);
            }
        }

        /// <summary>The charge bar right over the hands (clear of the hazard tab on the hand in use).</summary>
        void PlaceChargeBar()
        {
            if (chargeBar) V5BattleKit.Anchor(chargeBar, new Vector2(.5f, 0f), new Vector2(handsCentre * Css, (dockTop + 10f) * Css), new Vector2(420f, 5f) * Css);
        }

        /// <summary>The charge bar shows only while a weapon charges.</summary>
        void RenderChargeBar(ArenaUiSnapshot s)
        {
            if (chargeBar)
            {
                bool charging = s.charge01 > 0f;
                if (chargeBar.gameObject.activeSelf != charging) chargeBar.gameObject.SetActive(charging);
            }
        }

        void DressBars(bool art)
        {
            Fill(hud.chargeFill, art, V5BattleKit.ChargeTop, V5BattleKit.ChargeFoot);
            Fill(hud.bossHpFill, art, V5BattleKit.BossTop, V5BattleKit.BossFoot);
            Fill(hud.bossShieldFill, art, V5BattleKit.ShieldBand, V5TitleKit.Steel);
            if (bossHpTrack) bossHpTrack.color = V5HudKit.Track;
            if (bossShieldTrack) bossShieldTrack.color = V5HudKit.Track;
        }

        // A saved fill keeps its filled type: its picture and colour are set by hand (V5TitleKit.Dress would make it a plain image).
        static void Fill(Image fill, bool art, Color top, Color foot)
        {
            if (!fill) return;
            fill.sprite = art ? V5BattleKit.Fill(top, foot) : WhiteboxSkin.Square;
            fill.color = art ? Color.white : Color.Lerp(top, foot, .35f);
        }

        // ───── The banners ─────

        RectTransform banner, notice;
        Image bannerPicture;
        Outline bannerEdge;
        Text bannerTitle, bannerLine, noticeText;
        V5HudPlate noticePlate;
        CanvasGroup bannerGroup, noticeGroup;
        string bannerShown = "", noticeShown = "";
        float bannerFade, noticeFade;
        bool bannerWanted, noticeWanted;

        /// <summary>The run's banners (a wave, a boss, a level gained, a day) on the hull-plate banner, smaller than the story's night banner
        /// and without its lamps: the title in the cream paint, the line under it. A passing one-line notice (a weapon revealed, a skill
        /// refused) goes on a small plate in the same place instead.</summary>
        void BuildBanner()
        {
            banner = V5TitleKit.Rect(root, "V5Banner");
            V5BattleKit.Anchor(banner, new Vector2(.5f, 1f), new Vector2(0f, -BannerTop * Css), new Vector2(BannerW, BannerH) * Css);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = bannerGroup.interactable = false;
            bannerPicture = V5TitleKit.Picture(banner, "Picture", null, Color.white);
            V5TitleKit.Stretch(bannerPicture.rectTransform);
            bannerTitle = V5BattleKit.Label(banner, "Title", 40f, Color.white, TextAnchor.MiddleCenter, true);
            V5TitleKit.Painted(bannerTitle);
            bannerLine = V5BattleKit.Label(banner, "Line", 17f, V5TitleKit.Paint, TextAnchor.MiddleCenter);
            banner.gameObject.SetActive(false);

            notice = V5TitleKit.Rect(root, "V5Notice");
            V5BattleKit.Anchor(notice, new Vector2(.5f, 1f), new Vector2(0f, -BannerTop * Css), new Vector2(300f, NoticeH) * Css);
            noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
            noticeGroup.blocksRaycasts = noticeGroup.interactable = false;
            noticePlate = V5HudKit.Plate(notice, "Plate", V5HudKit.PlateLook.Plain, 8f, Css);
            V5TitleKit.Stretch(noticePlate.Rect);
            noticeText = V5BattleKit.Label(notice, "Line", 18f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(noticeText.rectTransform);
            notice.gameObject.SetActive(false);
        }

        void DressBanner(bool art)
        {
            if (!bannerPicture) return;
            V5TitleKit.Dress(bannerPicture, art ? V5TitleKit.Piece("banner") : null, art ? Color.white : WhiteboxSkin.PlateFill);
            if (!art && !bannerEdge)
            {
                bannerEdge = bannerPicture.gameObject.AddComponent<Outline>();
                bannerEdge.useGraphicAlpha = false;
                bannerEdge.effectColor = WhiteboxSkin.Edge;
                bannerEdge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (bannerEdge) bannerEdge.enabled = !art;
        }

        void RenderBanner(string text, bool toast)
        {
            text ??= "";
            bool line = toast && text.Length > 0 && text.IndexOf('\n') < 0;
            bannerWanted = text.Length > 0 && !line;
            noticeWanted = line;
            if (bannerWanted && text != bannerShown)
            {
                bannerShown = text;
                int cut = text.IndexOf('\n');
                string title = cut < 0 ? text : text.Substring(0, cut), rest = cut < 0 ? "" : text.Substring(cut + 1).Replace('\n', ' ');
                V5BattleKit.Sync(bannerTitle);
                V5BattleKit.Sync(bannerLine);
                bannerTitle.text = title;
                bannerLine.text = rest;
                bool two = rest.Length > 0;
                V5BattleKit.Place(bannerTitle.rectTransform, 60f, two ? 28f : 46f, BannerW - 120f, 60f);
                V5BattleKit.Place(bannerLine.rectTransform, 60f, 96f, BannerW - 120f, 28f);
                bannerLine.gameObject.SetActive(two);
                V5TitleKit.FitWidth(bannerTitle, V5BattleKit.Font(40f), V5BattleKit.Font(22f));
                V5TitleKit.FitWidth(bannerLine, V5BattleKit.Font(17f), V5BattleKit.Font(12f));
                if (!banner.gameObject.activeSelf) { bannerFade = 0f; banner.gameObject.SetActive(true); }
            }
            if (noticeWanted && text != noticeShown)
            {
                noticeShown = text;
                V5BattleKit.Sync(noticeText);
                noticeText.text = text;
                float width = Mathf.Min(V5BattleKit.Width(noticeText) / Css + 40f, BannerW);
                notice.sizeDelta = new Vector2(width, NoticeH) * Css;
                V5TitleKit.FitWidth(noticeText, V5BattleKit.Font(18f), V5BattleKit.Font(12f));
                if (!notice.gameObject.activeSelf) { noticeFade = 0f; notice.gameObject.SetActive(true); }
            }
        }

        /// <summary>A banner slides down a little as it comes in and fades as it goes; nothing moves while it stays.</summary>
        void TickBanner(float dt)
        {
            // bannerTop: BannerTop, or lower while the hint plate stands under a boss plate (PlaceHintPlate).
            Fade(banner, bannerGroup, bannerWanted, ref bannerFade, ref bannerShown, dt, bannerTop);
            Fade(notice, noticeGroup, noticeWanted, ref noticeFade, ref noticeShown, dt, bannerTop);
        }

        static void Fade(RectTransform part, CanvasGroup group, bool wanted, ref float shown, ref string text, float dt, float top)
        {
            if (!part || !part.gameObject.activeSelf) return;
            shown = wanted ? Mathf.Min(1f, shown + dt / BannerIn) : Mathf.Max(0f, shown - dt / BannerOut);
            group.alpha = wanted ? V5TitleKit.EaseOut(shown) : shown;
            float drop = wanted ? (1f - V5TitleKit.EaseOut(shown)) * BannerDrop : 0f;
            part.anchoredPosition = new Vector2(0f, -(top - drop) * Css);
            if (!wanted && shown <= 0f)
            {
                part.gameObject.SetActive(false);
                text = "";
            }
        }

        // ───── A number that changes pulses once ─────

        readonly List<(Transform part, float start)> pulses = new();

        void Pulse(Component part)
        {
            if (!part) return;
            pulses.RemoveAll(p => p.part == part.transform);
            pulses.Add((part.transform, Time.unscaledTime));
        }

        void TickPulses()
        {
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                var (part, start) = pulses[i];
                float t = (Time.unscaledTime - start) / PulseSeconds;
                if (!part || t >= 1f)
                {
                    if (part) part.localScale = Vector3.one;
                    pulses.RemoveAt(i);
                    continue;
                }
                part.localScale = Vector3.one * (1f + PulseGrow * Mathf.Sin(t * Mathf.PI));
            }
        }
    }
}
