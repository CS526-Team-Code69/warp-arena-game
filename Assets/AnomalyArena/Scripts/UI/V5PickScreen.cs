using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The open mode's pick screens in the fifth interface pass, in the same family as the story's weapon crate (V5PickCrate): the
    /// starting weapon, the armory, the card and level-up picks, the two replacement screens, the ready screen and the weapon card.
    /// V5MenuSkin puts it on each such saved screen when V5HudKit.On, in place of the menu look; V2Hud keeps setting the saved texts and
    /// calls Show at each refresh, so every command, key and saved object stays as it was. The look:
    /// a veil over the run, the hull-plate banner with the screen's heading, its one-line explanation under it and the build clock at the
    /// top right; the cards on the crate lid's frame (V5PickCard), each with its key, its picture, its name, its chips (tier, rarity),
    /// "攻击方式 ???" / "特性 ???" for a weapon not taken yet, its text, and "会换下" when taking it pushes a weapon out; the card under the
    /// pointer is the chosen one (lifted, orange glow, pointer), since a click or its key takes it at once here; at the foot the plain
    /// buttons on the left, the keys that work on the screen in the middle and the orange go button on the right.
    /// The saved panel picture and the rarity name under the picture are hidden (the name now sits in a chip); the saved summary shows on
    /// the ready screen only. A card's text box is at least as large as the menu look's, so every card text still fits.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5PickScreen : MonoBehaviour
    {
        enum Shape { Weapons, Cards, Ready, Intro }

        sealed class Part
        {
            public V2UiChoice choice;
            public V5PickCard card;
            public V5HudKey key;
            public V5PickChip tier, rarity;
            public Image artGlow;
            public Image[] rows;
            public Text[] rowLabels, rowMarks;
            public Text swap;
            public Color tint = Color.white;
            public bool unknown, body = true, showTier;
            public int mk = 1;
            public string swapText = "";
            public string laid;
        }

        sealed class Plate
        {
            public Button button;
            public V5Plate plate;
            public Text label;
            public RawImage hazard;
            public bool go;
        }

        const float Css = V5HudKit.HudCss;
        // The battle canvas (1440 x 810) in its own units. The banner and the line under it; the build clock at the top right.
        const float W = 1440f, H = 810f;
        const float BannerTop = 6f, BannerH = 84f, LineTop = 98f, LineH = 26f, ClockRight = 30f, ClockTop = 33f, ClockW = 320f, ClockH = 30f;
        // The cards: as large as the battle canvas allows, since the long card texts need the room, in the crate lid's proportions. The
        // chosen card's lift and pointer stay clear of the line above the row.
        const float CardW = 410f, CardGap = 20f, CardsTop = 166f;
        // The foot row: the plain buttons on the left, the keys in the middle, the go button on the right.
        const float RowTop = 706f, Side = 42f, SecondaryW = 230f, SecondaryH = 44f, GoW = 300f, GoH = 48f, RowGap = 16f;
        // Inside a card (canvas units): a margin inside the frame, the key row, the picture band, the name, the chips, the rows.
        const float InPad = 8f, KeyH = 22f, BandTop = 30f, CardsBandH = 92f, WeaponsBandH = 120f, RowH = 28f, RowGap6 = 6f;
        const int LineLargest = 17, LineSmallest = 12, HeadingLargest = 32, HeadingSmallest = 18;
        // The weapon card (WeaponIntro): the frame round the weapon on the left, the text on a plate on the right.
        const float IntroFrameLeft = 150f, IntroFrameTop = 160f, IntroFrameW = 300f, IntroTextLeft = 500f, IntroTextTop = 160f, IntroTextW = 800f,
            IntroTextH = 420f, IntroGoTop = 610f, IntroGoW = 360f, IntroGoH = 52f, IntroHintTop = 684f;
        // The ready screen: its line, the summary of the build, the go button.
        const float ReadyLineTop = 290f, ReadySummaryTop = 334f, ReadySummaryH = 110f, ReadyGoTop = 470f, ReadyGoW = 380f, ReadyGoH = 54f;

        static float CardH => CardW / V5PickKit.FrameAspect;

        V2UiScreen screen;
        RectTransform content;
        CanvasGroup group;
        Shape shape;
        Image banner, veil, introPlate;
        V5PickFrame introFrame;
        Image introGlow;
        V5PickHints hints;
        readonly List<Part> parts = new();
        readonly List<Plate> plates = new();
        readonly List<(string[] keys, string word)> hintItems = new();
        readonly List<GameObject> made = new();
        string headingShown, lineShown, hintsFor;
        float age;
        int shownCards = -1;
        bool? shownArt;

        /// <summary>The screens this look draws.</summary>
        public static bool Covers(ArenaUiState state) => state is ArenaUiState.Loadout or ArenaUiState.Armory or ArenaUiState.Draft
            or ArenaUiState.ReplaceWeapon or ArenaUiState.ReplaceSkill or ArenaUiState.Ready or ArenaUiState.WeaponIntro;

        /// <summary>Restyles a saved screen; false (and nothing left of it) when that went wrong, so the caller can give it the menu look.</summary>
        public static bool Install(V2UiScreen screen, RectTransform content)
        {
            if (!screen || !content) return false;
            var view = screen.gameObject.AddComponent<V5PickScreen>();
            if (V5PickKit.Safely(() => view.Build(screen, content)))
            {
                screen.pick = view;
                return true;
            }
            view.TakeBack();
            return false;
        }

        T Track<T>(T part) where T : Component
        {
            if (part) made.Add(part.gameObject);
            return part;
        }

        /// <summary>Takes back what was built, so the menu look that comes instead dresses the saved screen alone: the new pieces, the cards'
        /// and buttons' looks, the panel picture switched off.</summary>
        void TakeBack()
        {
            foreach (var go in made) if (go) Destroy(go);
            made.Clear();
            if (screen && screen.choices != null)
                foreach (var choice in screen.choices)
                    if (choice && choice.TryGetComponent<V5PickCard>(out var card)) card.Remove();
            if (content)
            {
                foreach (var plate in content.GetComponentsInChildren<V5Plate>(true))
                {
                    if (plate.transform.Find("Lights") is { } lights) Destroy(lights.gameObject);
                    Destroy(plate);
                }
                if (content.TryGetComponent<Image>(out var panel)) panel.enabled = true;
            }
            if (screen && screen.pick == this) screen.pick = null;
            Destroy(this);
        }

        void Build(V2UiScreen target, RectTransform box)
        {
            screen = target;
            content = box;
            shape = screen.state switch
            {
                ArenaUiState.Loadout or ArenaUiState.Armory or ArenaUiState.ReplaceWeapon => Shape.Weapons,
                ArenaUiState.Draft or ArenaUiState.ReplaceSkill => Shape.Cards,
                ArenaUiState.Ready => Shape.Ready,
                _ => Shape.Intro,
            };
            veil = screen.GetComponent<Image>();
            // The content is the whole canvas now; the saved panel picture goes (the parts stand on the veil, as in the mock-up).
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(W, H);
            if (content.TryGetComponent<Image>(out var panel)) panel.enabled = false;
            if (!content.TryGetComponent(out group)) group = content.gameObject.AddComponent<CanvasGroup>();
            banner = Track(V5PickKit.Banner(content, "V5Banner"));
            banner.transform.SetAsFirstSibling();
            V5PickKit.Place(banner.rectTransform, (W - BannerH * V5PickKit.BannerAspect) * .5f, BannerTop, BannerH * V5PickKit.BannerAspect, BannerH);
            StyleHeading();
            StyleLine();
            StyleClock();
            if (shape is Shape.Weapons or Shape.Cards && screen.choices != null)
                for (int i = 0; i < screen.choices.Length; i++)
                    if (screen.choices[i]) parts.Add(BuildPart(screen.choices[i], i));
            foreach (var button in content.GetComponentsInChildren<Button>(true))
                if (button.transform.parent == content && !button.GetComponent<V2UiChoice>()) plates.Add(DressButton(button));
            hints = Track(V5PickHints.Make(content, "V5Keys", Css));
            if (shape == Shape.Intro) BuildIntro();
            if (screen.summary)
            {
                var summary = screen.summary;
                summary.resizeTextForBestFit = false;
                summary.color = V5TitleKit.Ink3;
                summary.alignment = shape == Shape.Intro ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter;
                summary.horizontalOverflow = HorizontalWrapMode.Wrap;
                summary.verticalOverflow = VerticalWrapMode.Truncate;
                summary.fontSize = 15;
                if (shape == Shape.Intro) V5PickKit.Place(summary.rectTransform, 0f, IntroHintTop, W, 28f);
                else V5PickKit.Place(summary.rectTransform, (W - 900f) * .5f, ReadySummaryTop, 900f, ReadySummaryH);
                // The build summary shows on the ready screen; the card screens leave it out (the cards and the foot row fill the screen).
                V5PickKit.Show(summary, shape is Shape.Ready or Shape.Intro);
            }
            Follow(true);
            // Every card is laid out now, shown or not: a card's text box must have its size before the card is first shown.
            foreach (var part in parts) LayPart(part);
            LayCards();
            LayFoot();
        }

        // ───── Building ─────

        void StyleHeading()
        {
            var heading = screen.heading;
            if (!heading) return;
            heading.transform.SetSiblingIndex(banner.transform.GetSiblingIndex() + 1);
            heading.resizeTextForBestFit = false;
            heading.alignment = TextAnchor.MiddleCenter;
            heading.horizontalOverflow = HorizontalWrapMode.Overflow;
            heading.verticalOverflow = VerticalWrapMode.Overflow;
            heading.fontStyle = FontStyle.Bold;
            V5TitleKit.Painted(heading);
            var rect = banner.rectTransform;
            float ends = rect.sizeDelta.x * V5PickKit.BannerEnds;
            V5PickKit.Place(heading.rectTransform, rect.anchoredPosition.x + ends, BannerTop, rect.sizeDelta.x - ends * 2f, BannerH);
        }

        void StyleLine()
        {
            var line = screen.body;
            if (!line || shape == Shape.Intro) return;
            line.resizeTextForBestFit = false;
            line.alignment = TextAnchor.MiddleCenter;
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Overflow;
            if (shape == Shape.Ready) V5PickKit.Place(line.rectTransform, 60f, ReadyLineTop, W - 120f, 30f);
            else V5PickKit.Place(line.rectTransform, 60f, LineTop, W - 120f, LineH);
            line.fontSize = LineLargest;
        }

        void StyleClock()
        {
            var clock = screen.clock;
            if (!clock) return;
            clock.resizeTextForBestFit = false;
            clock.alignment = TextAnchor.MiddleRight;
            clock.horizontalOverflow = HorizontalWrapMode.Overflow;
            clock.fontSize = 18;
            clock.fontStyle = FontStyle.Bold;
            V5PickKit.Place(clock.rectTransform, W - ClockRight - ClockW, ClockTop, ClockW, ClockH);
        }

        Part BuildPart(V2UiChoice choice, int index)
        {
            var part = new Part { choice = choice };
            part.card = V5PickCard.Dress(choice.gameObject, 1f);
            // The card's saved button plays the hover sound (ArenaUiButton) and takes the click; the card adds the look.
            part.card.HoverSound = false;
            part.card.SetSize(new Vector2(CardW, CardH));
            if (choice.button) choice.button.transition = Selectable.Transition.None;
            var t = choice.transform;
            part.artGlow = Track(V5TitleKit.Picture(t, "V5ArtGlow", V5PickKit.ArtGlow, V5TitleKit.Rgb(255, 236, 210, .12f)));
            part.artGlow.transform.SetSiblingIndex(2);
            part.key = Track(V5HudKit.Key(t, "V5Key", ArenaKeys.Name(GameAction.Skill1 + index), true, 1f, KeyH, 13f));
            part.tier = Track(V5PickChip.Make(t, "V5Tier", 1f, 20f, 13f));
            part.rarity = Track(V5PickChip.Make(t, "V5Rarity", 1f, 20f, 13f));
            if (shape == Shape.Weapons)
            {
                part.rows = new Image[2];
                part.rowLabels = new Text[2];
                part.rowMarks = new Text[2];
                for (int r = 0; r < 2; r++)
                {
                    part.rows[r] = Track(V5TitleKit.Picture(t, r == 0 ? "V5Attack" : "V5Trait", null, V5PickKit.RowFill));
                    var edge = part.rows[r].gameObject.AddComponent<Outline>();
                    edge.useGraphicAlpha = false;
                    edge.effectColor = V5PickKit.RowEdge;
                    edge.effectDistance = new Vector2(1f, -1f);
                    part.rowLabels[r] = V5PickKit.Label(part.rows[r].transform, "Label", 15f, 1f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
                    part.rowMarks[r] = V5PickKit.Label(part.rows[r].transform, "Unknown", 16f, 1f, Color.white, TextAnchor.MiddleRight, true);
                    V5TitleKit.Painted(part.rowMarks[r]);
                    part.rowMarks[r].text = "? ? ?";
                }
            }
            part.swap = Track(V5PickKit.Label(t, "V5Swap", 14f, 1f, V5TitleKit.Ink3, TextAnchor.MiddleCenter));
            part.swap.supportRichText = true;
            if (choice.title)
            {
                choice.title.resizeTextForBestFit = false;
                choice.title.alignment = TextAnchor.MiddleCenter;
                choice.title.horizontalOverflow = HorizontalWrapMode.Overflow;
                choice.title.verticalOverflow = VerticalWrapMode.Overflow;
                choice.title.fontStyle = FontStyle.Bold;
                V5TitleKit.Painted(choice.title);
            }
            if (choice.kind)
            {
                choice.kind.resizeTextForBestFit = false;
                choice.kind.alignment = TextAnchor.MiddleLeft;
                choice.kind.horizontalOverflow = HorizontalWrapMode.Overflow;
                choice.kind.fontSize = 14;
                choice.kind.color = V5TitleKit.Ink2;
            }
            if (choice.body)
            {
                choice.body.alignment = TextAnchor.UpperLeft;
                choice.body.fontSize = 19;
                choice.bodyFontSize = 19;
            }
            // The rarity's name moves into its chip; the saved label keeps its text, hidden, above the text box (V2UiChoice grows the
            // text box down to a badge that sits under it).
            if (choice.badge)
            {
                V5PickKit.Place(choice.badge.rectTransform, 0f, 0f, 10f, 10f);
                V5PickKit.Show(choice.badge, false);
            }
            foreach (var image in new[] { choice.icon, choice.weaponIcon, choice.contentIcon })
                if (image) image.preserveAspect = true;
            choice.rarityApplied = rarity => SetRarity(part, rarity);
            SetRarity(part, CardRarity.Common);
            part.card.Settle();
            return part;
        }

        Plate DressButton(Button button)
        {
            bool go = button == screen.startNext || button.name == "ContinueButton";
            var plate = new Plate { button = button, go = go };
            plate.plate = V5Plate.Dress(button.gameObject, go ? V5Plate.Look.Go : V5Plate.Look.Plate, 10f, go);
            // A saved command button plays the hover sound itself (ArenaUiButton).
            plate.plate.HoverSound = !button.GetComponent<ArenaUiButton>();
            plate.label = button.GetComponentInChildren<Text>(true);
            if (go) plate.hazard = Track(V5PickKit.Hazard(button.transform, 1f, GoH));
            if (plate.label)
            {
                plate.label.resizeTextForBestFit = false;
                plate.label.alignment = TextAnchor.MiddleCenter;
                plate.label.fontSize = go ? 20 : 17;
                plate.label.fontStyle = FontStyle.Bold;
                plate.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return plate;
        }

        void BuildIntro()
        {
            // The weapon just taken is a reward: its frame glows gold (whitebox: a gold edge).
            introGlow = Track(V5TitleKit.Picture(content, "V5IntroGlow", V5PickKit.CardGlow, V5TitleKit.Rgb(246, 188, 76, .5f), V5PickKit.CardGlowDensity));
            var frameImage = Track(V5TitleKit.Picture(content, "V5IntroFrame", null, Color.white));
            introFrame = frameImage.gameObject.AddComponent<V5PickFrame>();
            introFrame.SetEdge(V5TitleKit.Gold);
            float frameH = IntroFrameW / V5PickKit.FrameAspect;
            V5PickKit.Place(frameImage.rectTransform, IntroFrameLeft, IntroFrameTop, IntroFrameW, frameH);
            V5PickKit.Place(introGlow.rectTransform, IntroFrameLeft - V5PickKit.CardGlowOutset, IntroFrameTop - V5PickKit.CardGlowOutset,
                IntroFrameW + V5PickKit.CardGlowOutset * 2f, frameH + V5PickKit.CardGlowOutset * 2f);
            introPlate = Track(V5HudKit.Plate(content, "V5IntroText", V5HudKit.PlateLook.Plain, 14f, Css)).Image;
            V5PickKit.Place(introPlate.rectTransform, IntroTextLeft - 24f, IntroTextTop - 20f, IntroTextW + 48f, IntroTextH + 40f);
            int at = banner.transform.GetSiblingIndex() + 1;
            introGlow.transform.SetSiblingIndex(at);
            frameImage.transform.SetSiblingIndex(at + 1);
            introPlate.transform.SetSiblingIndex(at + 2);
            if (screen.icon)
            {
                screen.icon.preserveAspect = true;
                float w = IntroFrameW * .74f, h = w * .5f;
                V5PickKit.Place(screen.icon.rectTransform, IntroFrameLeft + (IntroFrameW - w) * .5f, IntroFrameTop + (frameH - h) * .45f, w, h);
                screen.icon.transform.SetAsLastSibling();
            }
            if (screen.body)
            {
                screen.body.alignment = TextAnchor.UpperLeft;
                screen.body.horizontalOverflow = HorizontalWrapMode.Wrap;
                screen.body.verticalOverflow = VerticalWrapMode.Truncate;
                V5PickKit.Place(screen.body.rectTransform, IntroTextLeft, IntroTextTop, IntroTextW, IntroTextH);
                screen.body.transform.SetAsLastSibling();
            }
        }

        // ───── Each refresh (V2Hud.RenderScreen) ─────

        /// <summary>V2Hud calls this at each refresh of the screen, after it set the saved texts and cards: the cards' chips, rows and
        /// lines, the keys that work now, the buttons' looks.</summary>
        public void Show(ArenaUiSnapshot s)
        {
            if (s == null) return;
            V5PickKit.Safely(() => Render(s));
        }

        void Render(ArenaUiSnapshot s)
        {
            var gm = GameManager.Instance;
            var weapons = gm ? gm.runWeapons : null;
            for (int i = 0; i < parts.Count; i++) RenderPart(parts[i], i, s, weapons);
            LayCards();
            RenderHints(s, gm);
            LayFoot();
        }

        void RenderPart(Part part, int i, ArenaUiSnapshot s, RunWeapons weapons)
        {
            var choice = part.choice;
            if (!choice || !choice.gameObject.activeSelf) return;
            part.unknown = false;
            part.body = true;
            part.showTier = false;
            part.swapText = "";
            switch (s.state)
            {
                case ArenaUiState.Loadout:
                    // A starting weapon's attack and trait show only once it is taken: the rows say so, the explanation says why.
                    part.unknown = true;
                    part.body = false;
                    break;
                case ArenaUiState.Armory:
                    var offer = weapons && weapons.Offers != null && i < weapons.Offers.Length ? weapons.Offers[i] : null;
                    if (offer == null) break;
                    part.showTier = true;
                    part.mk = Mathf.Max(1, offer.mk);
                    part.unknown = offer.upgradeOf == null && !offer.revealed;
                    // A new weapon that finds no free slot replaces one the player chooses next.
                    if (offer.upgradeOf == null && SlotsFull(weapons, offer))
                        part.swapText = ArenaText.Pair("会换下一把武器", "Replaces a weapon you hold");
                    break;
                case ArenaUiState.ReplaceWeapon:
                    part.swapText = ArenaText.Pair("换下它", "Replace it");
                    break;
                case ArenaUiState.ReplaceSkill:
                    part.swapText = "";
                    break;
            }
            var key = ArenaKeys.Name(GameAction.Skill1 + Mathf.Min(i, 2));
            part.key.SetText(key);
            if (part.showTier) part.tier.Set(V5PickKit.Tier(part.mk), V5PickKit.TierColour(part.mk), V5BattleKit.TierInk);
            V5PickKit.Show(part.tier, part.showTier);
            string badge = choice.badge ? choice.badge.text : "";
            part.rarity.Set(badge, part.tint, V5BattleKit.TierInk);
            V5PickKit.Show(part.rarity, !string.IsNullOrEmpty(badge));
            if (part.rows != null)
            {
                part.rowLabels[0].text = ArenaText.Pair("攻击方式", "Attack");
                part.rowLabels[1].text = ArenaText.Pair("特性", "Trait");
                for (int r = 0; r < 2; r++) V5PickKit.Show(part.rows[r], part.unknown);
            }
            V5PickKit.SetText(part.swap, part.swapText);
            V5PickKit.Show(part.swap, part.swapText.Length > 0);
            V5PickKit.Show(choice.body, part.body);
            bool live = !choice.button || choice.button.interactable;
            part.card.Live = live;
            part.card.Dim = !live;
        }

        static bool SlotsFull(RunWeapons weapons, RunWeapon offer)
        {
            var slots = offer.IsAuto ? weapons.Auto : weapons.Hand;
            if (slots == null) return false;
            foreach (var slot in slots) if (slot == null) return false;
            return true;
        }

        void RenderHints(ArenaUiSnapshot s, GameManager gm)
        {
            bool canPause = gm && (gm.CanPlayerMove || gm.BuildTimeFlows);
            int shown = 0;
            foreach (var part in parts) if (part.choice && part.choice.gameObject.activeSelf) shown++;
            // What the hints depend on; they are built again only when it changes.
            string state = $"{s.state}|{s.isGreaterReward}|{s.rerolls > 0}|{canPause}|{shown}|{ArenaText.IsChinese}|{ArenaKeys.Revision}";
            if (state == hintsFor) return;
            hintsFor = state;
            hintItems.Clear();
            string choose = ArenaText.Pair("选择", "Choose"), replace = ArenaText.Pair("换下", "Replace");
            switch (s.state)
            {
                case ArenaUiState.Loadout:
                    hintItems.Add((Numbers(3), choose));
                    break;
                case ArenaUiState.Armory:
                    hintItems.Add((Numbers(3), choose));
                    hintItems.Add((new[] { "Tab" }, ArenaText.Pair("收起面板", "Put away")));
                    if (canPause) hintItems.Add((new[] { "Esc" }, ArenaText.Pair("暂停", "Pause")));
                    break;
                case ArenaUiState.Draft:
                    hintItems.Add((Numbers(3), choose));
                    if (!s.isGreaterReward && s.rerolls > 0) hintItems.Add((new[] { "R" }, ArenaText.Pair("重抽", "Reroll")));
                    if (!s.isGreaterReward) hintItems.Add((new[] { "Tab" }, ArenaText.Pair("收起面板", "Put away")));
                    if (canPause) hintItems.Add((new[] { "Esc" }, ArenaText.Pair("暂停", "Pause")));
                    break;
                case ArenaUiState.LevelUp:
                    hintItems.Add((Numbers(3), choose));
                    if (s.rerolls > 0) hintItems.Add((new[] { "R" }, ArenaText.Pair("重抽", "Reroll")));
                    hintItems.Add((new[] { "Esc" }, ArenaText.Pair("稍后再选", "Pick later")));
                    break;
                case ArenaUiState.ReplaceWeapon:
                case ArenaUiState.ReplaceSkill:
                    hintItems.Add((Numbers(Mathf.Max(1, shown)), replace));
                    if (canPause) hintItems.Add((new[] { "Esc" }, ArenaText.Pair("暂停", "Pause")));
                    break;
                case ArenaUiState.Ready:
                    hintItems.Add((new[] { "Tab" }, ArenaText.Pair("收起面板", "Put away")));
                    if (canPause) hintItems.Add((new[] { "Esc" }, ArenaText.Pair("暂停", "Pause")));
                    break;
            }
            hints.Set(hintItems);
        }

        static string[] Numbers(int count)
        {
            var keys = new string[Mathf.Clamp(count, 1, 3)];
            for (int i = 0; i < keys.Length; i++) keys[i] = ArenaKeys.Name(GameAction.Skill1 + i);
            return keys;
        }

        void SetRarity(Part part, CardRarity rarity)
        {
            var tint = V5PickKit.RarityColour(rarity);
            part.tint = rarity == CardRarity.Common ? V5BattleKit.TierOne : tint;
            part.card.SetTint(rarity == CardRarity.Common ? WhiteboxSkin.Edge : tint, V5PickKit.RarityGlow(rarity));
            // ArenaRarity's black cannot be read on a dark card, so a void card's name is pale violet.
            if (rarity == CardRarity.Void && part.choice.title) part.choice.title.color = ArenaRarity.VoidInk;
            if (part.rarity) part.rarity.Set(part.choice.badge ? part.choice.badge.text : "", part.tint, V5BattleKit.TierInk);
        }

        // ───── Layout ─────

        void LayCards()
        {
            if (parts.Count == 0) return;
            int shown = 0;
            foreach (var part in parts) if (part.choice && part.choice.gameObject.activeSelf) shown++;
            float x = (W - shown * CardW - Mathf.Max(0, shown - 1) * CardGap) * .5f;
            foreach (var part in parts)
            {
                if (!part.choice || !part.choice.gameObject.activeSelf) continue;
                part.card.SetRest(x, CardsTop);
                x += CardW + CardGap;
                LayPart(part);
            }
            if (shown != shownCards)
            {
                shownCards = shown;
                foreach (var part in parts) part.card.Settle();
            }
        }

        void LayPart(Part part)
        {
            var choice = part.choice;
            var card = part.card;
            float x0 = card.InnerLeft + InPad, y0 = card.InnerTop + 4f, w = card.InnerWidth - InPad * 2f, h = card.InnerHeight - 8f;
            bool weapon = choice.weaponIcon && choice.weaponIcon.gameObject.activeSelf;
            bool glyph = choice.contentIcon && choice.contentIcon.gameObject.activeSelf;
            bool element = choice.icon && choice.icon.gameObject.activeSelf;
            // Laid out again only when what the card shows changes.
            string signature = $"{weapon}{glyph}{element}{part.unknown}{part.body}{part.showTier}{part.swapText.Length > 0}{part.key.Rect.sizeDelta.x:0}"
                + $"{(part.rarity.gameObject.activeSelf ? part.rarity.Width : 0f):0}{(part.showTier ? part.tier.Width : 0f):0}{(choice.title ? choice.title.text : "")}";
            if (signature == part.laid) return;
            part.laid = signature;
            float keyW = part.key.Rect.sizeDelta.x;
            V5PickKit.Place(part.key.Rect, x0, y0, keyW, KeyH);
            bool cards = shape == Shape.Cards;
            float rarityW = part.rarity.gameObject.activeSelf ? part.rarity.Width : 0f;
            if (choice.kind) V5PickKit.Place(choice.kind.rectTransform, x0 + keyW + 8f, y0, w - keyW - 8f - (cards ? rarityW + 8f : 0f), KeyH);
            float bandTop = y0 + BandTop, bandH = cards ? CardsBandH : WeaponsBandH;
            V5PickKit.Place(part.artGlow.rectTransform, x0 - 10f, bandTop - 10f, w + 20f, bandH + 20f);
            const float small = 28f;
            if (element) V5PickKit.Place(choice.icon.rectTransform, x0 + w - small, bandTop, small, small);
            if (weapon)
            {
                float pw = cards ? 236f : 272f, ph = cards ? 88f : 116f;
                V5PickKit.Place(choice.weaponIcon.rectTransform, x0 + (w - pw) * .5f, bandTop + (bandH - ph) * .5f, pw, ph);
                if (glyph) V5PickKit.Place(choice.contentIcon.rectTransform, x0 + w - small * (element ? 2f : 1f) - (element ? 6f : 0f), bandTop, small, small);
            }
            else if (glyph)
            {
                float g = bandH - 4f;
                V5PickKit.Place(choice.contentIcon.rectTransform, x0 + (w - g) * .5f, bandTop + 2f, g, g);
            }
            // The pictures stand in front of the art glow, the small marks in front of the picture.
            if (choice.weaponIcon) choice.weaponIcon.transform.SetSiblingIndex(3);
            if (choice.contentIcon && !weapon) choice.contentIcon.transform.SetSiblingIndex(3);
            if (choice.icon) choice.icon.transform.SetAsLastSibling();
            if (weapon && glyph) choice.contentIcon.transform.SetAsLastSibling();
            float y = bandTop + bandH + 6f;
            if (choice.title)
            {
                float titleH = cards ? 30f : 34f;
                V5PickKit.Place(choice.title.rectTransform, x0, y, w, titleH);
                V5TitleKit.FitWidth(choice.title, cards ? 24 : 26, cards ? 15 : 17);
                y += titleH + 4f;
            }
            if (cards)
            {
                V5PickKit.Place(part.rarity.Rect, x0 + w - rarityW, y0 + 1f, rarityW, part.rarity.Height);
                // The text box: at least as large as the menu look's (352 x 166), so every card text still fits at the smallest size.
                if (choice.body) V5PickKit.Place(choice.body.rectTransform, x0 + 2f, y + 2f, w - 4f, y0 + h - y - 2f);
                if (choice.body) choice.body.transform.SetAsLastSibling();
                part.swap.transform.SetAsLastSibling();
                return;
            }
            float tierW = part.showTier ? part.tier.Width : 0f, gap = part.showTier && rarityW > 0f ? 8f : 0f;
            float chipsLeft = x0 + (w - tierW - gap - rarityW) * .5f;
            if (part.showTier) V5PickKit.Place(part.tier.Rect, chipsLeft, y, tierW, part.tier.Height);
            if (rarityW > 0f) V5PickKit.Place(part.rarity.Rect, chipsLeft + tierW + gap, y, rarityW, part.rarity.Height);
            y += 20f + 10f;
            if (part.rows != null && part.unknown)
                for (int r = 0; r < 2; r++)
                {
                    V5PickKit.Place(part.rows[r].rectTransform, x0, y, w, RowH);
                    V5PickKit.Place(part.rowLabels[r].rectTransform, 12f, 0f, w * .6f, RowH);
                    V5PickKit.Place(part.rowMarks[r].rectTransform, w * .4f, 0f, w * .6f - 12f, RowH);
                    y += RowH + RowGap6;
                }
            bool swap = part.swapText.Length > 0;
            float foot = y0 + h - (swap ? 24f : 0f);
            if (swap)
            {
                V5PickKit.Place(part.swap.rectTransform, x0 - 4f, y0 + h - 20f, w + 8f, 20f);
                V5TitleKit.FitWidth(part.swap, 14, 10);
            }
            if (!choice.body) return;
            V5PickKit.Place(choice.body.rectTransform, x0 + 2f, y + 4f, w - 4f, Mathf.Max(20f, foot - y - 6f));
            choice.FitBody();
            ArenaTextFit.Refit(choice.body, choice.body.resizeTextMaxSize, choice.body.resizeTextMinSize);
        }

        void LayFoot()
        {
            Plate go = null;
            var left = new List<Plate>();
            foreach (var plate in plates)
            {
                if (!plate.button || !plate.button.gameObject.activeSelf) continue;
                if (plate.go) go = plate;
                else left.Add(plate);
            }
            float row = shape == Shape.Intro ? IntroGoTop : RowTop;
            float x = Side;
            foreach (var plate in left)
            {
                plate.plate.Place(x, row + (GoH - SecondaryH) * .5f, SecondaryW, SecondaryH);
                x += SecondaryW + RowGap;
            }
            float rightEdge = W - Side;
            if (go != null)
            {
                if (shape == Shape.Ready) go.plate.Place((W - ReadyGoW) * .5f, ReadyGoTop, ReadyGoW, ReadyGoH);
                else if (shape == Shape.Intro) go.plate.Place((W - IntroGoW) * .5f, IntroGoTop, IntroGoW, IntroGoH);
                else
                {
                    go.plate.Place(W - Side - GoW, row, GoW, GoH);
                    rightEdge = W - Side - GoW - RowGap;
                }
                float goH = go.plate.Rect.sizeDelta.y;
                V5PickKit.PlaceHazard(go.hazard, 1f, goH);
                if (go.label) V5PickKit.Place(go.label.rectTransform, 64f, 0f, go.plate.Rect.sizeDelta.x - 76f, goH);
            }
            foreach (var plate in left)
                if (plate.label) V5PickKit.Place(plate.label.rectTransform, 8f, 0f, SecondaryW - 16f, SecondaryH);
            float leftEdge = x;
            bool middle = shape is Shape.Weapons or Shape.Cards;
            float centre = middle ? (leftEdge + rightEdge) * .5f : W * .5f;
            hints.PlaceCentre(centre, (middle ? row : RowTop) + (GoH - hints.Height) * .5f);
            V5PickKit.Show(hints, shape != Shape.Intro);
        }

        // ───── Each frame ─────

        void OnEnable() => age = 0f;

        void LateUpdate()
        {
            if (!content) return;
            Follow(false);
            float dt = Time.unscaledDeltaTime;
            age += dt;
            // A short fade in; a screen that is not 16:9 scales the whole canvas-sized content down to fit.
            float t = Mathf.Clamp01(age / .16f);
            t = t * t * (3f - 2f * t);
            if (group && group.alpha != t) group.alpha = t;
            var canvas = ((RectTransform)transform).rect.size;
            float fit = Mathf.Min(1f, canvas.x / W, canvas.y / H);
            var scale = Vector3.one * fit * (.985f + .015f * t);
            if (content.localScale != scale) content.localScale = scale;
            FitWords();
            // A click or a card's key takes it at once on these screens, so the card under the pointer is the chosen one.
            foreach (var part in parts)
                if (part.choice && part.choice.gameObject.activeSelf) part.card.Chosen = part.card.Hovered && part.card.Live;
        }

        /// <summary>The heading and the line are fitted again whenever their words change (a language switch, a count).</summary>
        void FitWords()
        {
            var heading = screen.heading;
            if (heading && heading.text != headingShown)
            {
                headingShown = heading.text;
                V5PickKit.SyncFont(heading);
                V5TitleKit.FitWidth(heading, HeadingLargest, HeadingSmallest);
            }
            var line = screen.body;
            if (line && shape != Shape.Intro && line.text != lineShown)
            {
                lineShown = line.text;
                int largest = shape == Shape.Ready ? 20 : LineLargest;
                ArenaTextFit.Refit(line, largest, LineSmallest);
            }
        }

        /// <summary>Dresses what follows the art switch here: the veil, the buttons' words and stripes.</summary>
        void Follow(bool force)
        {
            bool art = Art.Enabled;
            if (!force && shownArt == art) return;
            shownArt = art;
            if (veil)
            {
                var colour = Color.white;
                // The starting weapon is picked over the title's picture: the veil lets a little more of it through.
                if (screen.state == ArenaUiState.Loadout) colour.a = .86f;
                V5TitleKit.Dress(veil, art ? V5PickKit.Veil : null, art ? colour : V5PickKit.FlatVeil);
            }
            foreach (var plate in plates)
            {
                if (plate.label) plate.label.color = plate.go ? V5PickKit.GoInk(art) : V5TitleKit.Ink;
                V5PickKit.Show(plate.hazard, art);
            }
            foreach (var part in parts) V5PickKit.Show(part.artGlow, art);
            if (introGlow) V5PickKit.Show(introGlow, art);
        }

        /// <summary>A plain button that cannot be pressed now (no reroll left) is darker, its words too.</summary>
        void Update()
        {
            foreach (var plate in plates)
            {
                if (!plate.button) continue;
                bool live = plate.button.interactable;
                plate.plate.Dim = !live;
                if (plate.label)
                {
                    var colour = plate.label.color;
                    float alpha = live ? 1f : .5f;
                    if (!Mathf.Approximately(colour.a, alpha))
                    {
                        colour.a = alpha;
                        plate.label.color = colour;
                    }
                }
            }
        }
    }
}
