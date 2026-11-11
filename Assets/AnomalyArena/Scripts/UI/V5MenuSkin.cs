using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The menu look: layered dark panels in the title art's colours, large cards whose picture is the weapon or skill itself
    /// and whose frame carries the rarity colour. Since 2026-11-10 all of it follows the art switch: in whitebox the title art behind a panel
    /// gives way to flat blocks (WhiteboxBackdrop), the cards' pictures to shapes (WhiteboxIcons, through V2Hud's icon setters), and the
    /// panels, cards, plates and buttons to flat rectangles with a thin edge, the card's edge in its rarity colour, the glows, clouds and
    /// rules left out (WhiteboxSkin). The saved
    /// scene is left as it is: when a scene loads, this component is added to its V2Hud and changes the saved panels' images and rectangles, and
    /// V2Hud keeps driving the same objects. The textures are in Resources/V5/UI (work/art-plans/art-redo-1030/ui/make_ui_skin.py).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5MenuSkin : MonoBehaviour
    {
        const string Folder = "V5/UI/";
        // The textures are drawn at twice their canvas size.
        const float Density = 2f;

        // Card screens, in canvas units at the 1440 x 810 reference. The panel's outer 12 units are glow.
        const float CardPanelW = 1328f, CardPanelH = 776f, CardPanelNoButtonsH = 704f, Margin = 52f;
        const float CardW = 392f, CardH = 474f, CardGap = 24f, CardsTop = 198f, BottomRowTop = 684f, BottomRowH = 56f;
        // The pictures a little smaller than the art band.
        const float ArtH = 208f, MainWeaponW = 304f, MainWeaponH = 152f, MainGlyph = 156f, SmallIcon = 34f;
        const int TitleLargest = 26, TitleSmallest = 18;

        /// <summary>The void colour on screen: "black" has to stay visible on a dark panel, so a void card is a black line in a violet glow.</summary>
        public static readonly Color VoidGlow = new(.62f, .34f, 1f);
        static readonly Color VoidText = ArenaRarity.VoidInk;
        static readonly Color SoftText = new(.86f, .90f, .96f), FaintText = new(.62f, .72f, .85f);
        static readonly Color AccentCyan = new(.47f, .89f, 1f);
        // By ArenaRarity.Rank: white, green, blue, purple, orange, black.
        static readonly float[] GlowByRank = { .10f, .50f, .58f, .72f, .88f, .95f };
        static readonly float[] CloudByRank = { .12f, .40f, .44f, .48f, .53f, .68f };
        static readonly float[] RadialByRank = { .12f, .40f, .45f, .50f, .56f, .64f };
        // Common is white, which as a backdrop would be the brightest of them all; its line stays white and its art a cool grey.
        static readonly Color CommonBackdrop = new(.70f, .76f, .90f);

        static Sprite panelSprite, cardSprite, cardLine, cardGlow, buttonSprite, buttonPrimary, ruleSprite, pillSprite, radialSprite, dimSprite, nebulaSprite, cloudSprite, fadeSprite;

        sealed class TextFit
        {
            public Text label;
            public int largest, smallest;
            public string shown;
        }

        sealed class Card
        {
            public V2UiChoice choice;
            public RectTransform rect;
            public Image line, glow, cloud, radial, rule, pill;
            /// <summary>The card's own image in whitebox: a flat plate whose thin edge carries the rarity colour.</summary>
            public WhiteboxSkin.Entry plate;
            public Color glowColour;
            public float glowAlpha, hover;
            public bool pulses, weaponShown, glyphShown, elementShown, placed;
            public string title;
        }

        sealed class Panel
        {
            public GameObject root;
            public RectTransform content;
            public CanvasGroup group;
            public Vector2 size;
            public float heightWithoutSummary, summaryTop;
            public Text summary;
            public float age;
            public bool wasOn;
            public string summaryShown;
            public int shownCards = -1;
            public readonly List<TextFit> fits = new();
            public readonly List<Card> cards = new();
        }

        readonly List<Panel> panels = new();
        // 2026-11-10 (second round): every image this skin dresses, with its flat whitebox look (WhiteboxSkin); LateUpdate follows the switch.
        readonly WhiteboxSkin.Set flat = new();
        V2Hud hud;
        Sprite themeSprite;
        // A clicked card stays the selected object, so the selection only counts as a highlight once the keyboard is in use.
        bool keyboardInUse;

        // Registered once per play: the editor keeps statics between plays (domain reload is off), so it is taken off first.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Entering Play Mode without a scene reload (this project's editor setting) raises no sceneLoaded for the scene that is
        // already open, so the open scenes are skinned here as well; a built game loads its first scene normally, and the check
        // in OnSceneLoaded keeps it to one skin.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SkinOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) OnSceneLoaded(scene, LoadSceneMode.Additive);
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var view in root.GetComponentsInChildren<V2Hud>(true))
                {
                    if (!view.GetComponent<V5MenuSkin>()) view.gameObject.AddComponent<V5MenuSkin>().Install(view);
                }
        }

        /// <summary>The saved screens have taken this look.</summary>
        public bool Installed { get; private set; }

        void Install(V2Hud view)
        {
            hud = view;
            if (!LoadSprites()) { Debug.LogWarning("V5MenuSkin: the textures in Resources/V5/UI are missing; the saved look stays."); enabled = false; return; }
            Installed = true;
            if (hud.screens != null)
                foreach (var screen in hud.screens)
                    if (screen) Skin(screen);
            SkinConfirmation();
        }

        static bool LoadSprites()
        {
            if (panelSprite && fadeSprite) return true;
            panelSprite = Load("ui-panel", 112f);
            cardSprite = Load("ui-card", 64f);
            cardLine = Load("ui-card-line", 64f);
            cardGlow = Load("ui-card-glow", 112f);
            buttonSprite = Load("ui-button", 40f);
            buttonPrimary = Load("ui-button-primary", 40f);
            pillSprite = Load("ui-pill", 16f);
            ruleSprite = Load("ui-rule", 0f);
            radialSprite = Load("ui-radial", 0f);
            dimSprite = Load("ui-dim", 0f);
            nebulaSprite = Load("ui-nebula", 0f);
            cloudSprite = Load("ui-cloud", 0f);
            fadeSprite = Load("ui-fade", 0f);
            return panelSprite && cardSprite && cardLine && cardGlow && buttonSprite && buttonPrimary && pillSprite && ruleSprite && radialSprite && dimSprite && nebulaSprite && cloudSprite && fadeSprite;
        }

        static Sprite Load(string name, float border)
        {
            var texture = Resources.Load<Texture2D>(Folder + name);
            if (!texture) return null;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * border);
        }

        void Skin(V2UiScreen screen)
        {
            if (screen.state == ArenaUiState.Title) { SkinTitle(screen); return; }
            if (screen.transform.Find("Content") is not RectTransform content) return;
            switch (screen.state)
            {
                case ArenaUiState.Loadout:
                case ArenaUiState.Armory:
                case ArenaUiState.Draft:
                case ArenaUiState.ReplaceWeapon:
                case ArenaUiState.ReplaceSkill:
                    SkinCardScreen(screen, content);
                    break;
                case ArenaUiState.WeaponIntro:
                    SkinWeaponCard(screen, content);
                    break;
                case ArenaUiState.Paused:
                    SkinPaused(screen, content);
                    break;
                case ArenaUiState.Ready:
                case ArenaUiState.Result:
                    SkinCentred(screen, content);
                    break;
            }
        }

        // The title is the look the other screens follow; only its buttons join the new set. Since 2026-11-10 (one game in two languages)
        // these are the three entries and the open mode's map choices in Start's own skin, and the Settings button, the Language option and
        // the map pick's back button in the plain one (V2Hud makes them all in its Awake, before this runs). The saved art switch is
        // not shown any more (the art mode is chosen in the settings).
        void SkinTitle(V2UiScreen screen)
        {
            foreach (var name in V2Hud.TitlePrimaryButtons)
                if (screen.transform.Find("Content/" + name) is { } part && part.GetComponent<Button>() is { } button) SkinButton(button, true, 0);
            foreach (var name in V2Hud.TitleSecondaryButtons)
                if (screen.transform.Find("Content/" + name) is { } part && part.GetComponent<Button>() is { } button) SkinButton(button, false, 0);
            // In whitebox the art switch is a plain button too, and the drawn rules above and under the saved buttons (and their copies) go.
            if (screen.transform.Find("Content/ToggleArt") is { } toggle && toggle.GetComponent<Image>() is { } switchImage) flat.Add(switchImage, WhiteboxSkin.Look.Button);
            foreach (var image in screen.GetComponentsInChildren<Image>(true))
                if (image.name is "V3TopRule" or "V3BottomRule") flat.Add(image, WhiteboxSkin.Look.Hide);
        }

        void SkinCardScreen(V2UiScreen screen, RectTransform content)
        {
            var buttons = new List<Button>();
            foreach (var button in content.GetComponentsInChildren<Button>(true))
                if (button.transform.parent == content && !button.GetComponent<V2UiChoice>()) buttons.Add(button);
            float height = buttons.Count > 0 ? CardPanelH : CardPanelNoButtonsH;
            var panel = MakePanel(screen.gameObject, content, new Vector2(CardPanelW, height));
            // Loadout already stands on the title art (V2Hud shows it there).
            Backdrop(screen, screen.state != ArenaUiState.Loadout);
            Style(screen.heading, Margin, 24f, CardPanelW - Margin * 2f - 340f, 54f, 36, TextAnchor.MiddleLeft);
            Gradient(screen.heading);
            Style(screen.clock, CardPanelW - Margin - 320f, 30f, 320f, 42f, 20, TextAnchor.MiddleRight);
            Rule(content, "HeadingRule", 40f, 78f, CardPanelW - 80f, 14f, AccentCyan);
            // Narrower than the panel: shorter lines read better, and no paragraph ends in a line holding one mark.
            Style(screen.body, Margin, 94f, 1120f, 98f, 18, TextAnchor.UpperLeft);
            Fit(panel, screen.body, 18, 13);
            Style(screen.summary, Margin + 280f, BottomRowTop, CardPanelW - Margin * 2f - 280f - 340f, BottomRowH, 14, TextAnchor.MiddleLeft);
            if (screen.summary) screen.summary.color = FaintText;
            Fit(panel, screen.summary, 14, 10);
            foreach (var button in buttons)
            {
                bool next = button == screen.startNext;
                SkinButton(button, next, 18);
                if (next) Place(button, CardPanelW - Margin - 320f, BottomRowTop, 320f, BottomRowH);
                else Place(button, Margin, BottomRowTop, 260f, BottomRowH);
            }
            if (screen.choices == null) return;
            foreach (var choice in screen.choices)
                if (choice) panel.cards.Add(SkinCard(choice));
        }

        Card SkinCard(V2UiChoice choice)
        {
            var rect = (RectTransform)choice.transform;
            var card = new Card { choice = choice, rect = rect };
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(CardW, CardH);
            if (choice.GetComponent<Image>() is { } baseImage)
            {
                baseImage.sprite = cardSprite;
                baseImage.type = Image.Type.Sliced;
                baseImage.pixelsPerUnitMultiplier = Density;
                baseImage.color = Color.white;
                card.plate = flat.Add(baseImage, WhiteboxSkin.Look.Card);
            }
            card.glow = NewImage(rect, "CardGlow", cardGlow, Color.clear, Image.Type.Sliced);
            card.glow.rectTransform.anchorMin = Vector2.zero;
            card.glow.rectTransform.anchorMax = Vector2.one;
            card.glow.rectTransform.offsetMin = Vector2.one * -24f;
            card.glow.rectTransform.offsetMax = Vector2.one * 24f;
            card.glow.transform.SetSiblingIndex(0);

            var art = new GameObject("CardArt", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            art.SetParent(rect, false);
            art.SetSiblingIndex(1);
            Place(art, 3f, 3f, CardW - 6f, ArtH - 3f);
            card.cloud = NewImage(art, "Cloud", cloudSprite, Color.clear, Image.Type.Simple);
            Stretch(card.cloud.rectTransform);
            card.radial = NewImage(art, "Radial", radialSprite, Color.clear, Image.Type.Simple);
            Place(card.radial, (CardW - 6f) * .5f - 220f, ArtH * .5f - 220f, 440f, 440f);
            var fade = NewImage(art, "TopFade", fadeSprite, Color.white, Image.Type.Simple);
            Place(fade, 0f, 0f, CardW - 6f, 58f);

            card.rule = NewImage(rect, "ArtRule", ruleSprite, Color.white, Image.Type.Simple);
            Place(card.rule, 6f, ArtH - 6f, CardW - 12f, 14f);
            card.rule.transform.SetSiblingIndex(2);
            card.line = NewImage(rect, "CardLine", cardLine, Color.white, Image.Type.Sliced);
            Stretch(card.line.rectTransform);
            card.line.transform.SetSiblingIndex(3);

            Style(choice.kind, 16f, 9f, CardW - 120f, 26f, 16, TextAnchor.MiddleLeft);
            Shadowed(choice.kind);
            Style(choice.title, 18f, ArtH + 18f, CardW - 36f, 66f, TitleLargest, TextAnchor.MiddleCenter);
            // From under the title to the card's foot: the long comparison texts (one level against the next) need all of it.
            Style(choice.body, 20f, ArtH + 88f, CardW - 40f, CardH - ArtH - 100f, 19, TextAnchor.UpperLeft);
            choice.bodyFontSize = 19;
            foreach (var image in new[] { choice.icon, choice.weaponIcon, choice.contentIcon })
                if (image) image.preserveAspect = true;
            // The rarity name sits on the line under the picture. These stay in front of the large picture.
            card.pill = NewImage(rect, "BadgePill", pillSprite, Color.white, Image.Type.Sliced);
            Place(card.pill, CardW * .5f - 38f, ArtH - 13f, 76f, 28f);
            // In whitebox only the plate and its rarity edge stay: the glow, the art band's cloud, light and fade, the rule, the line and the
            // pill are drawn pictures.
            foreach (var picture in new[] { card.glow, card.cloud, card.radial, fade, card.rule, card.line, card.pill }) flat.Add(picture, WhiteboxSkin.Look.Hide);
            if (choice.badge)
            {
                Style(choice.badge, CardW * .5f - 38f, ArtH - 13f, 76f, 28f, 15, TextAnchor.MiddleCenter);
                choice.badge.transform.SetAsLastSibling();
            }
            if (choice.icon) choice.icon.transform.SetAsLastSibling();
            if (choice.kind) choice.kind.transform.SetAsLastSibling();
            choice.rarityApplied = rarity => SetRarity(card, rarity);
            SetRarity(card, CardRarity.Common);
            return card;
        }

        void SetRarity(Card card, CardRarity rarity)
        {
            bool isVoid = rarity == CardRarity.Void;
            int rank = ArenaRarity.Rank(rarity);
            Color tint = isVoid ? VoidGlow : ArenaRarity.ColorFor(rarity);
            card.glowColour = tint;
            card.glowAlpha = GlowByRank[rank];
            card.pulses = rank >= 3;
            card.line.color = isVoid ? new Color(.015f, .01f, .04f) : tint;
            Color backdrop = rarity == CardRarity.Common ? CommonBackdrop : tint;
            card.cloud.color = WithAlpha(isVoid ? new Color(.24f, .10f, .50f) : backdrop, CloudByRank[rank]);
            card.radial.color = WithAlpha(backdrop, RadialByRank[rank]);
            card.rule.color = tint;
            card.pill.color = tint;
            card.plate?.SetEdge(tint);
            if (!isVoid) return;
            // ArenaRarity's black cannot be read on a dark card, so a void card's words are pale violet.
            if (card.choice.title) card.choice.title.color = VoidText;
            if (card.choice.badge) card.choice.badge.color = VoidText;
        }

        /// <summary>Which picture leads depends on what V2Hud switched on: a weapon when there is one (an upgrade direction's glyph then sits small beside the
        /// element), otherwise the skill or stat glyph.</summary>
        static void ArrangeArt(Card card, bool force)
        {
            var choice = card.choice;
            bool weapon = choice.weaponIcon && choice.weaponIcon.gameObject.activeSelf;
            bool glyph = choice.contentIcon && choice.contentIcon.gameObject.activeSelf;
            bool element = choice.icon && choice.icon.gameObject.activeSelf;
            if (!force && weapon == card.weaponShown && glyph == card.glyphShown && element == card.elementShown) return;
            card.weaponShown = weapon;
            card.glyphShown = glyph;
            card.elementShown = element;
            float right = CardW - 12f - SmallIcon;
            if (choice.icon) Place(choice.icon, right, 11f, SmallIcon, SmallIcon);
            if (weapon) Place(choice.weaponIcon, (CardW - MainWeaponW) * .5f, (ArtH - MainWeaponH) * .5f + 6f, MainWeaponW, MainWeaponH);
            if (!choice.contentIcon) return;
            if (weapon) Place(choice.contentIcon, element ? right - SmallIcon - 6f : right, 11f, SmallIcon, SmallIcon);
            else Place(choice.contentIcon, (CardW - MainGlyph) * .5f, (ArtH - MainGlyph) * .5f + 4f, MainGlyph, MainGlyph);
        }

        /// <summary>The weapon card's picture against its former size: since 2026-11-06 half as wide and half as high (the plate was 420 x 440 and
        /// the weapon 348 x 174). It keeps the top of the left column, and the description takes the width it gave up.</summary>
        public const float WeaponCardArtScale = .5f;
        const float WeaponCardTop = 112f, WeaponCardTextH = 440f, WeaponCardTextGap = 28f;

        void SkinWeaponCard(V2UiScreen screen, RectTransform content)
        {
            const float w = 1240f, h = 700f, s = WeaponCardArtScale;
            MakePanel(screen.gameObject, content, new Vector2(w, h));
            Backdrop(screen, true);
            Style(screen.heading, Margin, 26f, w - Margin * 2f, 56f, 38, TextAnchor.MiddleCenter);
            Gradient(screen.heading);
            Rule(content, "HeadingRule", 40f, 84f, w - 80f, 14f, AccentCyan);
            float plateW = 420f * s, plateH = 440f * s, weaponW = 348f * s, weaponH = 174f * s;
            var plate = NewImage(content, "ArtPlate", cardSprite, Color.white, Image.Type.Sliced);
            Place(plate, Margin, WeaponCardTop, plateW, plateH);
            // The cloud fills the plate's upper half inside its 3-unit frame; the glow is centred on the plate and a little larger.
            var cloud = NewImage(plate.transform, "Cloud", cloudSprite, WithAlpha(new Color(.42f, .30f, .95f), .28f), Image.Type.Simple);
            Place(cloud, 3f, 3f, plateW - 6f, 221f * s);
            var radial = NewImage(plate.transform, "Radial", radialSprite, WithAlpha(AccentCyan, .22f), Image.Type.Simple);
            Place(radial, -20f * s, -10f * s, 460f * s, 460f * s);
            var line = NewImage(plate.transform, "CardLine", cardLine, WithAlpha(AccentCyan, .9f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            // In whitebox: a flat plate with a thin cyan edge behind the weapon's shape; the cloud, the light and the drawn line go.
            flat.Add(plate, WhiteboxSkin.Look.Plate).SetEdge(WithAlpha(AccentCyan, .9f));
            foreach (var picture in new[] { cloud, radial, line }) flat.Add(picture, WhiteboxSkin.Look.Hide);
            if (screen.icon)
            {
                screen.icon.preserveAspect = true;
                Place(screen.icon, Margin + (plateW - weaponW) * .5f, WeaponCardTop + (plateH - weaponH) * .5f, weaponW, weaponH);
                screen.icon.transform.SetAsLastSibling();
            }
            float textLeft = Margin + plateW + WeaponCardTextGap;
            Style(screen.body, textLeft, WeaponCardTop, w - Margin - textLeft, WeaponCardTextH, 24, TextAnchor.UpperLeft);
            if (content.Find("ContinueButton") is RectTransform go && go.GetComponent<Button>() is { } button)
            {
                SkinButton(button, true, 20);
                Place(go, (w - 440f) * .5f, 572f, 440f, 62f);
            }
            Style(screen.summary, Margin, 640f, w - Margin * 2f, 34f, 16, TextAnchor.MiddleCenter);
            if (screen.summary) screen.summary.color = FaintText;
        }

        void SkinPaused(V2UiScreen screen, RectTransform content)
        {
            const float w = 1040f, buttonGap = 18f;
            float buttonW = (w - 80f - buttonGap * (V2Hud.PauseButtons.Length - 1)) / V2Hud.PauseButtons.Length;
            float top = V2Hud.PauseTableBottom + 24f, h = top + 64f + 16f + 128f + 24f;
            var panel = MakePanel(screen.gameObject, content, new Vector2(w, h));
            panel.heightWithoutSummary = top + 64f + 40f;
            panel.summary = screen.summary;
            // The frozen battlefield stays in view around the panel.
            Backdrop(screen, false);
            Style(screen.heading, 40f, 28f, w - 80f, 56f, 36, TextAnchor.MiddleCenter);
            Gradient(screen.heading);
            Rule(content, "HeadingRule", 40f, 86f, w - 80f, 14f, AccentCyan);
            // The paused line; V2Hud puts the controls table under it.
            Style(screen.body, 40f, 106f, w - 80f, 36f, 18, TextAnchor.MiddleCenter);
            if (screen.body) screen.body.color = SoftText;
            float left = 40f;
            foreach (var name in V2Hud.PauseButtons)
            {
                if (content.Find(name) is RectTransform go && go.GetComponent<Button>() is { } button)
                {
                    SkinButton(button, name == "ResumeRun", 20);
                    Place(go, left, top, buttonW, 64f);
                }
                left += buttonW + buttonGap;
            }
            // The settings button stands alone in the panel's top-right corner, beside the centred heading, clear of the controls
            // table and of the row of four under it.
            if (content.Find(V2Hud.PauseSettingsButton) is RectTransform settings && settings.GetComponent<Button>() is { } settingsButton)
            {
                SkinButton(settingsButton, false, 18);
                Place(settings, w - 40f - 176f, 34f, 176f, 44f);
            }
            top += 64f + 16f;
            panel.summaryTop = top;
            Style(screen.summary, 40f, panel.summaryTop, w - 80f, 128f, 15, TextAnchor.UpperCenter);
            if (screen.summary) screen.summary.color = FaintText;
            Fit(panel, screen.summary, 15, 10);
        }

        /// <summary>The ready and result screens: a heading, some lines and one large button in the middle of the title art.</summary>
        void SkinCentred(V2UiScreen screen, RectTransform content)
        {
            const float w = 900f, h = 560f;
            var panel = MakePanel(screen.gameObject, content, new Vector2(w, h));
            panel.heightWithoutSummary = 440f;
            panel.summary = screen.summary;
            panel.summaryTop = 384f;
            Backdrop(screen, true);
            bool result = screen.state == ArenaUiState.Result;
            Style(screen.heading, 48f, 30f, w - 96f, 68f, result ? 44 : 38, TextAnchor.MiddleCenter);
            Gradient(screen.heading);
            Style(screen.clock, w - 48f - 300f, 34f, 300f, 40f, 18, TextAnchor.MiddleRight);
            Rule(content, "HeadingRule", 60f, 100f, w - 120f, 14f, AccentCyan);
            Style(screen.body, 64f, 124f, w - 128f, 150f, 22, TextAnchor.MiddleCenter);
            if (screen.body) screen.body.color = SoftText;
            Fit(panel, screen.body, 22, 14);
            var go = result ? content.Find("NewRun") as RectTransform : screen.startNext ? screen.startNext.transform as RectTransform : null;
            if (go && go.GetComponent<Button>() is { } button)
            {
                SkinButton(button, true, 22);
                Place(go, (w - 520f) * .5f, 290f, 520f, 76f);
            }
            Style(screen.summary, 64f, panel.summaryTop, w - 128f, 140f, 15, TextAnchor.UpperCenter);
            if (screen.summary) screen.summary.color = FaintText;
            Fit(panel, screen.summary, 15, 10);
        }

        void SkinConfirmation()
        {
            if (!hud.earlyStartPrompt) return;
            if (hud.earlyStartPrompt.transform.Find("ConfirmationSurface") is not RectTransform surface) return;
            const float w = 780f, h = 320f;
            MakePanel(hud.earlyStartPrompt, surface, new Vector2(w, h));
            if (hud.earlyStartPrompt.GetComponent<Image>() is { } dim) Dim(dim);
            Style(hud.earlyStartLabel, Margin, 44f, w - Margin * 2f, 112f, 24, TextAnchor.MiddleCenter);
            if (hud.earlyStartLabel) hud.earlyStartLabel.color = SoftText;
            float x = Margin;
            foreach (var name in new[] { "ConfirmRandomAndStart", "KeepChoosing" })
            {
                if (surface.Find(name) is RectTransform go && go.GetComponent<Button>() is { } button)
                {
                    SkinButton(button, name == "ConfirmRandomAndStart", 18);
                    Place(go, x, 186f, (w - Margin * 2f - 24f) * .5f, 64f);
                }
                x += (w - Margin * 2f - 24f) * .5f + 24f;
            }
        }

        Panel MakePanel(GameObject root, RectTransform content, Vector2 size)
        {
            var panel = new Panel { root = root, content = content, size = size };
            content.sizeDelta = size;
            if (content.GetComponent<Image>() is { } image)
            {
                image.sprite = panelSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = Density;
                image.color = Color.white;
                flat.Add(image, WhiteboxSkin.Look.Panel);
            }
            var nebula = NewImage(content, "Nebula", nebulaSprite, Color.white, Image.Type.Simple);
            flat.Add(nebula, WhiteboxSkin.Look.Hide);
            nebula.rectTransform.anchorMin = Vector2.zero;
            nebula.rectTransform.anchorMax = Vector2.one;
            nebula.rectTransform.offsetMin = Vector2.one * 14f;
            nebula.rectTransform.offsetMax = Vector2.one * -14f;
            nebula.transform.SetAsFirstSibling();
            panel.group = content.GetComponent<CanvasGroup>();
            if (!panel.group) panel.group = content.gameObject.AddComponent<CanvasGroup>();
            panels.Add(panel);
            return panel;
        }

        /// <summary>The screen behind the panel: the title art itself, dimmed (art), or whatever is under the screen, dimmed.</summary>
        void Backdrop(V2UiScreen screen, bool art)
        {
            if (screen.GetComponent<Image>() is not { } root) return;
            var theme = art && hud.art ? hud.art.themeBackground : null;
            if (!theme) { Dim(root); return; }
            if (!themeSprite) themeSprite = Sprite.Create(theme, new Rect(0f, 0f, theme.width, theme.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            root.sprite = themeSprite;
            root.type = Image.Type.Simple;
            root.color = new Color(.78f, .78f, .86f, 1f);
            var dim = NewImage(screen.transform, "Dim", dimSprite, Color.white, Image.Type.Simple);
            Stretch(dim.rectTransform);
            dim.transform.SetAsFirstSibling();
            flat.Add(dim, WhiteboxSkin.Look.Dim);
            // 2026-11-10: in whitebox flat blocks cover the title art, under the dim layer and the panel.
            WhiteboxBackdrop.Attach(root);
        }

        void Dim(Image image)
        {
            image.sprite = dimSprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            flat.Add(image, WhiteboxSkin.Look.Dim);
        }

        void SkinButton(Button button, bool primary, int textSize)
        {
            if (!button) return;
            if (button.GetComponent<Image>() is { } image)
            {
                image.sprite = primary ? buttonPrimary : buttonSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = Density;
                image.color = Color.white;
                button.targetGraphic = image;
                flat.Add(image, primary ? WhiteboxSkin.Look.PrimaryButton : WhiteboxSkin.Look.Button);
            }
            button.transition = Selectable.Transition.ColorTint;
            var colours = button.colors;
            colours.normalColor = new Color(.84f, .88f, .95f);
            colours.highlightedColor = Color.white;
            colours.selectedColor = Color.white;
            colours.pressedColor = new Color(.62f, .68f, .80f);
            colours.disabledColor = new Color(.40f, .43f, .50f, .75f);
            colours.colorMultiplier = 1f;
            colours.fadeDuration = .08f;
            button.colors = colours;
            if (textSize <= 0) return;
            var label = button.GetComponentInChildren<Text>(true);
            if (!label) return;
            label.resizeTextForBestFit = false;
            label.fontSize = textSize;
            label.color = new Color(.95f, .98f, 1f);
            Shadowed(label);
        }

        void Rule(Transform parent, string name, float left, float top, float width, float height, Color colour)
        {
            var rule = NewImage(parent, name, ruleSprite, colour, Image.Type.Simple);
            flat.Add(rule, WhiteboxSkin.Look.Hide);
            Place(rule, left, top, width, height);
        }

        static void Style(Text label, float left, float top, float width, float height, int size, TextAnchor anchor)
        {
            if (!label) return;
            Place(label, left, top, width, height);
            label.resizeTextForBestFit = false;
            label.fontSize = size;
            label.alignment = anchor;
        }

        static void Gradient(Text label)
        {
            if (!label) return;
            if (!label.GetComponent<V5TextGradient>()) label.gameObject.AddComponent<V5TextGradient>();
            Shadowed(label);
        }

        static void Shadowed(Text label)
        {
            if (!label) return;
            var shadow = label.GetComponent<Shadow>();
            if (!shadow) shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .7f);
            shadow.effectDistance = new Vector2(1f, -2f);
        }

        static void Fit(Panel panel, Text label, int largest, int smallest)
        {
            if (label) panel.fits.Add(new TextFit { label = label, largest = largest, smallest = smallest });
        }

        static Image NewImage(Transform parent, string name, Sprite sprite, Color colour, Image.Type type)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.type = type;
            if (type == Image.Type.Sliced) image.pixelsPerUnitMultiplier = Density;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Left and top measured from the parent's top-left corner, as in V2HudLayout.</summary>
        static void Place(Component part, float left, float top, float width, float height)
        {
            if (!part || part.transform is not RectTransform rect) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }

        void LateUpdate()
        {
            flat.Follow();
            Vector2 canvas = ((RectTransform)transform).rect.size;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            var pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) keyboardInUse = true;
            if (Mouse.current != null && Mouse.current.delta.ReadValue() != Vector2.zero) keyboardInUse = false;
            var selected = keyboardInUse && EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var panel in panels)
            {
                if (!panel.root || !panel.root.activeInHierarchy) { panel.wasOn = false; continue; }
                if (!panel.wasOn) { panel.wasOn = true; panel.age = 0f; }
                panel.age += dt;
                Tick(panel, canvas);
                foreach (var card in panel.cards)
                    if (card.choice.gameObject.activeSelf) TickCard(card, pointer, selected, now, dt);
            }
        }

        static void Tick(Panel panel, Vector2 canvas)
        {
            // A short fade in. Very narrow windows scale the panel down whole instead of cutting it off.
            float t = Mathf.Clamp01(panel.age / .16f);
            t = t * t * (3f - 2f * t);
            if (panel.group.alpha != t) panel.group.alpha = t;
            // The panels with a free-length summary end just below it.
            if (panel.summary && panel.summary.text != panel.summaryShown)
            {
                panel.summaryShown = panel.summary.text;
                bool empty = string.IsNullOrWhiteSpace(panel.summaryShown);
                panel.content.sizeDelta = new Vector2(panel.size.x, empty ? panel.heightWithoutSummary
                    : Mathf.Clamp(panel.summaryTop + panel.summary.preferredHeight + 44f, panel.heightWithoutSummary, panel.size.y));
            }
            float fit = Mathf.Min(1f, canvas.x / panel.content.sizeDelta.x, canvas.y / panel.content.sizeDelta.y);
            // Written only when it changes: a moved transform makes the canvas batch again.
            Vector3 scale = Vector3.one * fit * (.985f + .015f * t);
            if (panel.content.localScale != scale) panel.content.localScale = scale;
            foreach (var entry in panel.fits)
            {
                if (entry.label.text == entry.shown) continue;
                entry.shown = entry.label.text;
                ArenaTextFit.Refit(entry.label, entry.largest, entry.smallest);
            }
            if (panel.cards.Count == 0) return;
            // Replacing a weapon offers two cards where the others offer three, so the row is centred on what is shown.
            int shown = 0;
            foreach (var card in panel.cards) if (card.choice.gameObject.activeSelf) shown++;
            if (shown == panel.shownCards) return;
            panel.shownCards = shown;
            float x = (panel.size.x - shown * CardW - (shown - 1) * CardGap) * .5f + CardW * .5f;
            foreach (var card in panel.cards)
            {
                if (!card.choice.gameObject.activeSelf) continue;
                card.rect.anchoredPosition = new Vector2(x, -CardsTop - CardH * .5f);
                x += CardW + CardGap;
            }
        }

        void TickCard(Card card, Vector2 pointer, GameObject selected, float now, float dt)
        {
            ArrangeArt(card, !card.placed);
            card.placed = true;
            var title = card.choice.title;
            if (title && title.text != card.title)
            {
                card.title = title.text;
                ArenaTextFit.Refit(title, TitleLargest, TitleSmallest);
            }
            bool over = RectTransformUtility.RectangleContainsScreenPoint(card.rect, pointer) && card.choice.button && card.choice.button.interactable;
            card.hover = Mathf.MoveTowards(card.hover, over || selected == card.choice.gameObject ? 1f : 0f, dt * 9f);
            Vector3 scale = Vector3.one * (1f + .025f * card.hover);
            if (card.rect.localScale != scale) card.rect.localScale = scale;
            float pulse = card.pulses ? 1f + .16f * Mathf.Sin(now * 2.6f) : 1f;
            card.glow.color = WithAlpha(card.glowColour, Mathf.Clamp01(card.glowAlpha * pulse + .3f * card.hover));
        }
    }
}
