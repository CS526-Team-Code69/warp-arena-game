using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnomalyArena
{
                 
                                                                                                                                                 
                                                                                                                                                    
                                                                                                                                                 
                                                                                                                                    
                  
    [DisallowMultipleComponent]
    public sealed class V5MenuSkin : MonoBehaviour
    {
        const string Folder = "V5/UI/";
                                                             
        const float Density = 2f;

                                                                                                          
        const float CardPanelW = 1328f, CardPanelH = 776f, CardPanelNoButtonsH = 704f, Margin = 52f;
        const float CardW = 392f, CardH = 474f, CardGap = 24f, CardsTop = 198f, BottomRowTop = 684f, BottomRowH = 56f;
        const float ArtH = 208f, MainWeaponW = 348f, MainWeaponH = 174f, MainGlyph = 180f, SmallIcon = 38f;
        const int TitleLargest = 26, TitleSmallest = 18;

                                                                                                                                                       
        public static readonly Color VoidGlow = new(.62f, .34f, 1f);
        static readonly Color VoidText = new(.86f, .79f, 1f);
        static readonly Color SoftText = new(.86f, .90f, .96f), FaintText = new(.62f, .72f, .85f);
        static readonly Color AccentCyan = new(.47f, .89f, 1f);
                                                                          
        static readonly float[] GlowByRank = { .10f, .50f, .58f, .72f, .88f, .95f };
        static readonly float[] CloudByRank = { .15f, .50f, .55f, .60f, .66f, .85f };
        static readonly float[] RadialByRank = { .16f, .55f, .62f, .68f, .76f, .85f };
                                                                                                                                 
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
        V2Hud hud;
        Sprite themeSprite;
                                                                                                                             
        bool keyboardInUse;

                                                                                                                             
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var view in root.GetComponentsInChildren<V2Hud>(true))
                {
                    if (!view.GetComponent<V5MenuSkin>()) view.gameObject.AddComponent<V5MenuSkin>().Install(view);
                }
        }

                                                                      
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

                                                                                             
        void SkinTitle(V2UiScreen screen)
        {
            var start = screen.transform.Find("Content/StartRun");
            if (start && start.GetComponent<Button>() is { } button) SkinButton(button, true, 0);
        }

        void SkinCardScreen(V2UiScreen screen, RectTransform content)
        {
            var buttons = new List<Button>();
            foreach (var button in content.GetComponentsInChildren<Button>(true))
                if (button.transform.parent == content && !button.GetComponent<V2UiChoice>()) buttons.Add(button);
            float height = buttons.Count > 0 ? CardPanelH : CardPanelNoButtonsH;
            var panel = MakePanel(screen.gameObject, content, new Vector2(CardPanelW, height));
                                                                              
            Backdrop(screen, screen.state != ArenaUiState.Loadout);
            Style(screen.heading, Margin, 24f, CardPanelW - Margin * 2f - 340f, 54f, 36, TextAnchor.MiddleLeft);
            Gradient(screen.heading);
            Style(screen.clock, CardPanelW - Margin - 320f, 30f, 320f, 42f, 20, TextAnchor.MiddleRight);
            Rule(content, "HeadingRule", 40f, 78f, CardPanelW - 80f, 14f, AccentCyan);
                                                                                                                    
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
                                                                                                                              
            Style(choice.body, 20f, ArtH + 88f, CardW - 40f, CardH - ArtH - 100f, 19, TextAnchor.UpperLeft);
            choice.bodyFontSize = 19;
            foreach (var image in new[] { choice.icon, choice.weaponIcon, choice.contentIcon })
                if (image) image.preserveAspect = true;
                                                                                                            
            card.pill = NewImage(rect, "BadgePill", pillSprite, Color.white, Image.Type.Sliced);
            Place(card.pill, CardW * .5f - 38f, ArtH - 13f, 76f, 28f);
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
            if (!isVoid) return;
                                                                                                         
            if (card.choice.title) card.choice.title.color = VoidText;
            if (card.choice.badge) card.choice.badge.color = VoidText;
        }

                                                                                                                                                               
                                                                  
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

        void SkinWeaponCard(V2UiScreen screen, RectTransform content)
        {
            const float w = 1240f, h = 700f;
            MakePanel(screen.gameObject, content, new Vector2(w, h));
            Backdrop(screen, true);
            Style(screen.heading, Margin, 26f, w - Margin * 2f, 56f, 38, TextAnchor.MiddleCenter);
            Gradient(screen.heading);
            Rule(content, "HeadingRule", 40f, 84f, w - 80f, 14f, AccentCyan);
            var plate = NewImage(content, "ArtPlate", cardSprite, Color.white, Image.Type.Sliced);
            Place(plate, Margin, 112f, 420f, 440f);
            var cloud = NewImage(plate.transform, "Cloud", cloudSprite, WithAlpha(new Color(.42f, .30f, .95f), .36f), Image.Type.Simple);
            Place(cloud, 3f, 3f, 414f, 221f);
            var radial = NewImage(plate.transform, "Radial", radialSprite, WithAlpha(AccentCyan, .30f), Image.Type.Simple);
            Place(radial, -20f, -10f, 460f, 460f);
            var line = NewImage(plate.transform, "CardLine", cardLine, WithAlpha(AccentCyan, .9f), Image.Type.Sliced);
            Stretch(line.rectTransform);
            if (screen.icon)
            {
                screen.icon.preserveAspect = true;
                Place(screen.icon, Margin + 10f, 232f, 400f, 200f);
                screen.icon.transform.SetAsLastSibling();
            }
            Style(screen.body, Margin + 448f, 112f, w - Margin * 2f - 448f, 440f, 24, TextAnchor.UpperLeft);
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
            const float w = 1040f, buttonW = 226f, buttonGap = 18f;
            float top = V2Hud.PauseTableBottom + 24f, h = top + 64f + 16f + 128f + 24f;
            var panel = MakePanel(screen.gameObject, content, new Vector2(w, h));
            panel.heightWithoutSummary = top + 64f + 40f;
            panel.summary = screen.summary;
                                                                     
            Backdrop(screen, false);
            Style(screen.heading, 40f, 28f, w - 80f, 56f, 36, TextAnchor.MiddleCenter);
            Gradient(screen.heading);
            Rule(content, "HeadingRule", 40f, 86f, w - 80f, 14f, AccentCyan);
            // The paused line; V2Hud puts the controls table under it.
            Style(screen.body, 40f, 106f, w - 80f, 36f, 18, TextAnchor.MiddleCenter);
            if (screen.body) screen.body.color = SoftText;
            float left = (w - V2Hud.PauseButtons.Length * (buttonW + buttonGap) + buttonGap) * .5f;
            foreach (var name in V2Hud.PauseButtons)
            {
                if (content.Find(name) is RectTransform go && go.GetComponent<Button>() is { } button)
                {
                    SkinButton(button, name == "ResumeRun", 20);
                    Place(go, left, top, buttonW, 64f);
                }
                left += buttonW + buttonGap;
            }
            top += 64f + 16f;
            panel.summaryTop = top;
            Style(screen.summary, 40f, panel.summaryTop, w - 80f, 128f, 15, TextAnchor.UpperCenter);
            if (screen.summary) screen.summary.color = FaintText;
            Fit(panel, screen.summary, 15, 10);
        }

                                                                                                                                       
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
            }
            var nebula = NewImage(content, "Nebula", nebulaSprite, Color.white, Image.Type.Simple);
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
        }

        static void Dim(Image image)
        {
            image.sprite = dimSprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }

        static void SkinButton(Button button, bool primary, int textSize)
        {
            if (!button) return;
            if (button.GetComponent<Image>() is { } image)
            {
                image.sprite = primary ? buttonPrimary : buttonSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = Density;
                image.color = Color.white;
                button.targetGraphic = image;
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

        static void Rule(Transform parent, string name, float left, float top, float width, float height, Color colour)
        {
            var rule = NewImage(parent, name, ruleSprite, colour, Image.Type.Simple);
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
                                                                                                         
            float t = Mathf.Clamp01(panel.age / .16f);
            t = t * t * (3f - 2f * t);
            if (panel.group.alpha != t) panel.group.alpha = t;
                                                                       
            if (panel.summary && panel.summary.text != panel.summaryShown)
            {
                panel.summaryShown = panel.summary.text;
                bool empty = string.IsNullOrWhiteSpace(panel.summaryShown);
                panel.content.sizeDelta = new Vector2(panel.size.x, empty ? panel.heightWithoutSummary
                    : Mathf.Clamp(panel.summaryTop + panel.summary.preferredHeight + 44f, panel.heightWithoutSummary, panel.size.y));
            }
            float fit = Mathf.Min(1f, canvas.x / panel.content.sizeDelta.x, canvas.y / panel.content.sizeDelta.y);
                                                                                            
            Vector3 scale = Vector3.one * fit * (.985f + .015f * t);
            if (panel.content.localScale != scale) panel.content.localScale = scale;
            foreach (var entry in panel.fits)
            {
                if (entry.label.text == entry.shown) continue;
                entry.shown = entry.label.text;
                ArenaTextFit.Refit(entry.label, entry.largest, entry.smallest);
            }
            if (panel.cards.Count == 0) return;
                                                                                                                        
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
