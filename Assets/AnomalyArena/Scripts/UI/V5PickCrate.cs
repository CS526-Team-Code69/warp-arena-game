using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The story's weapon crate in the fifth interface pass (work/art-plans/style-1112/ui-f/crate.html, its first variant), on the story
    /// layer's canvas, as the mock-up's 1920 x 1080 pixels times V5HudKit.StoryCss (the cards narrower on a screen too narrow for them).
    /// It is drawn over the campaign's own crate panel (V2CratePanel), which stays open behind it, its own look and keys set aside, so the
    /// campaign still knows a crate window covers the screen.
    /// The banner "武器箱 · 三选一", the line under it, then one card per weapon on the crate lid's frame: its key, the weapon's picture,
    /// its name with its tier beside it, the attack row (a stripe at its left, "攻击方式" and under it this weapon's attack in a few words,
    /// WeaponGuide.Style), "特性 ???" (the trait shows only once it is taken) and, when taking it pushes a weapon out, "会换下" and that
    /// weapon.
    /// Taking is two steps, as the mock-up shows: 1 / 2 / 3 or a click chooses a card (it lifts, glows orange and the pointer stands over
    /// it); the go button "拿下…", a click on the chosen card or the interact key takes it. Enter cannot be the key: the game reads Space
    /// and Enter only on the weapon card that follows (SourceKeysTest). Esc leaves the crate when it may be left, as the "不拿" button does.
    /// Keys and clicks wait a moment after the window opens, so the key that opened the crate never chooses at once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5PickCrate : MonoBehaviour
    {
        /// <summary>One weapon as the window shows it; the names arrive localized.</summary>
        public sealed class Offer
        {
            public string name = "";
            public int mk = 1;
            public int formIndex = -1;
            /// <summary>This weapon's attack in a few words (WeaponGuide.Style), localized; "? ? ?" shows when it is empty.</summary>
            public string style = "";
            /// <summary>The weapon taking this one pushes out (empty when it goes into a free slot).</summary>
            public string replaces = "";
            /// <summary>The weapon pushed out carries upgrades, which go with it.</summary>
            public bool replacedUpgrades;
        }

        sealed class Card
        {
            public V5PickCard view;
            public V5HudKey key;
            public Image artGlow, weapon;
            public Text name, swap;
            public V5PickChip tier;
            public Image attack, stripe, trait;
            public Text attackLabel, attackStyle, traitLabel, traitMark;
            public int formIndex = -1;
        }

        const float Css = V5HudKit.StoryCss;
        // Keys and clicks wait this long after the window opens: the key that opened the crate, or a held fire button, must not choose.
        const float InputDelay = .25f;
        // The mock-up's places, in its pixels. The cards keep SideRoom clear of each side of the screen; on a screen too narrow for them
        // they and everything in them are drawn smaller (Layout's fit).
        const float BannerTop = 30f, BannerW = 820f, BannerH = 146f, TitlePx = 54f, SubTop = 196f, SubPx = 24f;
        const float CardsTop = 266f, CardW = 580f, CardH = 580f, CardGap = 30f, SideRoom = 24f;
        const float GoTop = 872f, GoH = 60f, GoPx = 24f, GoPadLeft = 30f, GoPadRight = 24f, GoKeyGap = 22f, LeaveW = 150f, ButtonGap = 18f;
        const float FootBottom = 24f;
        // Inside a card (the mock-up's .in: 15.5 % at the sides, 9 % at the top, 11.5 % at the foot).
        const float InLeft = .155f, InTop = .09f, InFoot = .115f;
        const float KeyH = 32f, KeyPx = 18f, GlowTop = 10f, GlowH = 170f, PicW = 330f, PicH = 160f, PicTop = 12f, NamePx = 38f, NameGap = 16f,
            ChipGap = 16f, ChipH = 28f, ChipPx = 18f, RowsGap = 20f, RowGap = 10f, SwapPx = 18f;
        // The attack row: padding 14 / 16 / 16 / 18 inside a 4-pixel stripe, "攻击方式" in 18-pixel letters, the attack 10 under it in
        // 30-pixel letters (as small as 18 to fit). The trait row: 48 tall, padding 22 / 18, its words 18 and its marks 22.
        const float AttackPadTop = 14f, AttackPadRight = 16f, AttackPadFoot = 16f, AttackPadLeft = 18f, StripeW = 4f, AttackLabelPx = 18f,
            StylePx = 30f, StyleSmallestPx = 18f, StyleGap = 10f;
        const float TraitH = 48f, TraitPadLeft = 22f, TraitPadRight = 18f, TraitPx = 18f, MarkPx = 22f;

        RectTransform root;
        Image veil, banner;
        Text title, sub, goLabel, leaveLabel;
        V5Plate go, leave;
        V5HudKey goKey;
        RawImage hazard;
        V5PickHints hints;
        readonly Card[] cards = new Card[RunWeapons.ArmoryOfferCount];
        readonly List<(string[] keys, string word)> hintItems = new();
        readonly List<Offer> offers = new();
        Action<int> take;
        Action leaveCrate;
        int count, chosen = -1;
        // Canvas units per mock-up pixel for the cards: Css, or less on a screen too narrow for them (Layout).
        float cardCss = Css;
        bool mayLeave, open;
        float openedAt;
        bool? shownArt;
        int shownState = int.MinValue;

        /// <summary>Opens the window over the campaign's crate panel that was just opened: the panel's own look and keys are set aside
        /// (the window takes them), the panel stays open behind it. onTake gets the index taken; onLeave runs when the crate is left.
        /// Should the window fail to be drawn, the panel is left as it is, so the crate can still be taken or left.</summary>
        public static V5PickCrate Over(V2CratePanel panel, string headingText, string subText, IReadOnlyList<Offer> weapons, bool canLeave,
            Action<int> onTake, Action onLeave)
        {
            if (!panel) return null;
            V5PickCrate window = null;
            bool shown = V5PickKit.Safely(() =>
            {
                if (!panel.TryGetComponent(out window)) window = panel.gameObject.AddComponent<V5PickCrate>();
                window.Show(headingText, subText, weapons, canLeave, onTake, onLeave);
            });
            var old = panel.transform.Find("Panel");
            if (!shown)
            {
                if (window) window.Hide();
                panel.enabled = true;
                if (old) old.gameObject.SetActive(true);
                return null;
            }
            panel.enabled = false;
            if (old) old.gameObject.SetActive(false);
            return window;
        }

        void Hide()
        {
            open = false;
            take = null;
            leaveCrate = null;
            if (root) root.gameObject.SetActive(false);
        }

        void Build()
        {
            if (root) return;
            root = V5TitleKit.Rect(transform, "V5Crate");
            V5TitleKit.Stretch(root);
            veil = V5TitleKit.Picture(root, "Veil", null, Color.white);
            V5TitleKit.Stretch(veil.rectTransform);
            banner = V5PickKit.Banner(root, "Banner");
            title = V5PickKit.Label(banner.transform, "Title", TitlePx, Css, Color.white, TextAnchor.MiddleCenter, true);
            V5TitleKit.Painted(title);
            sub = V5PickKit.Label(root, "Sub", SubPx, Css, V5TitleKit.Ink2, TextAnchor.MiddleCenter);
            for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard(i);
            go = V5Plate.Make(root, "Take", V5Plate.Look.Go, 10f, Take, true);
            hazard = V5PickKit.Hazard(go.transform, Css, GoH * Css);
            goLabel = V5PickKit.Label(go.transform, "Label", GoPx, Css, V5TitleKit.Dark, TextAnchor.MiddleLeft, true);
            goKey = V5HudKit.Key(go.transform, "Key", ArenaKeys.InteractKey, false, Css, 28f, 15f);
            leave = V5Plate.Make(root, "Leave", V5Plate.Look.Plate, 10f, Leave);
            leaveLabel = V5PickKit.Label(leave.transform, "Label", 20f, Css, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(leaveLabel.rectTransform, 4f);
            hints = V5PickHints.Make(root, "Keys", Css);
        }

        Card BuildCard(int i)
        {
            var card = new Card { view = V5PickCard.Make(root, "Weapon" + (i + 1), Css) };
            int index = i;
            card.view.Clicked += _ => Click(index);
            card.view.HoverPitch = 1.06f - i * .05f;
            card.view.SetSize(new Vector2(CardW, CardH) * Css);
            card.view.SetTint(WhiteboxSkin.Edge, 0f);
            var parent = card.view.transform;
            card.artGlow = V5TitleKit.Picture(parent, "ArtGlow", V5PickKit.ArtGlow, V5TitleKit.Rgb(255, 236, 210, .12f));
            card.weapon = V5TitleKit.Picture(parent, "Picture", null, Color.white);
            card.weapon.preserveAspect = true;
            card.weapon.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -8f * Css);
            card.key = V5HudKit.Key(parent, "Key", ArenaKeys.Name(GameAction.Skill1 + i), true, Css, KeyH, KeyPx);
            card.name = V5PickKit.Label(parent, "Name", NamePx, Css, Color.white, TextAnchor.MiddleCenter, true);
            V5TitleKit.Painted(card.name);
            card.tier = V5PickChip.Make(parent, "Tier", Css, ChipH, ChipPx);
            // The attack row, the card's main line: a stripe at its left, its heading and under it the weapon's attack.
            card.attack = Row(parent, "Attack");
            card.stripe = V5TitleKit.Picture(card.attack.transform, "Stripe", null, V5TitleKit.Orange);
            card.attackLabel = V5PickKit.Label(card.attack.transform, "Label", AttackLabelPx, Css, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            card.attackStyle = V5PickKit.Label(card.attack.transform, "Style", StylePx, Css, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(card.attackStyle);
            // The trait row: its heading and the question marks.
            card.trait = Row(parent, "Trait");
            card.traitLabel = V5PickKit.Label(card.trait.transform, "Label", TraitPx, Css, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            card.traitMark = V5PickKit.Label(card.trait.transform, "Unknown", MarkPx, Css, Color.white, TextAnchor.MiddleRight, true);
            V5TitleKit.Painted(card.traitMark);
            card.traitMark.text = "? ? ?";
            card.swap = V5PickKit.Label(parent, "Swap", SwapPx, Css, V5TitleKit.Ink3, TextAnchor.MiddleCenter);
            return card;
        }

        /// <summary>A row's dark backing with its hairline edge.</summary>
        static Image Row(Transform parent, string name)
        {
            var row = V5TitleKit.Picture(parent, name, null, V5PickKit.RowFill);
            var edge = row.gameObject.AddComponent<Outline>();
            edge.useGraphicAlpha = false;
            edge.effectColor = V5PickKit.RowEdge;
            edge.effectDistance = new Vector2(1f, -1f);
            return row;
        }

        void Show(string headingText, string subText, IReadOnlyList<Offer> weapons, bool canLeave, Action<int> onTake, Action onLeave)
        {
            Build();
            offers.Clear();
            if (weapons != null) foreach (var offer in weapons) if (offer != null && offers.Count < cards.Length) offers.Add(offer);
            count = offers.Count;
            take = onTake;
            leaveCrate = onLeave;
            mayLeave = canLeave;
            chosen = -1;
            open = true;
            openedAt = Time.unscaledTime;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            title.text = V5PickKit.BannerWords(headingText);
            sub.text = subText ?? "";
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                bool used = i < count;
                V5PickKit.Show(card.view, used);
                if (!used) continue;
                var offer = offers[i];
                card.formIndex = offer.formIndex;
                card.name.text = offer.name;
                card.tier.Set(V5PickKit.Tier(offer.mk), V5PickKit.TierColour(offer.mk), V5BattleKit.TierInk);
                card.attackLabel.text = ArenaText.Pair("攻击方式", "Attack");
                card.attackStyle.text = string.IsNullOrEmpty(offer.style) ? "? ? ?" : offer.style;
                card.traitLabel.text = ArenaText.Pair("特性", "Trait");
                card.key.SetText(ArenaKeys.Name(GameAction.Skill1 + i));
                card.swap.supportRichText = true;
                card.swap.text = string.IsNullOrEmpty(offer.replaces) ? ""
                    : ArenaText.Pair($"会换下 <b><color={V5PickKit.SwapHex}>{offer.replaces}</color></b>", $"Replaces the <b><color={V5PickKit.SwapHex}>{offer.replaces}</color></b>");
                card.view.Chosen = false;
                card.view.Live = false;
                card.view.Settle();
            }
            leaveLabel.text = ArenaText.Pair("不拿", "Leave it");
            shownArt = null;
            shownState = int.MinValue;
            Layout();
            Refresh();
        }

        /// <summary>The window closes with the campaign's crate panel (its parent); this only forgets what it was doing.</summary>
        void OnDisable()
        {
            open = false;
            take = null;
            leaveCrate = null;
        }

        void Layout()
        {
            var size = root.rect.size;
            float canvasW = size.x > 1f ? size.x : 1600f, canvasH = size.y > 1f ? size.y : 900f;
            // The mock-up's places from the top; the foot from the bottom, so a taller screen keeps it at the foot.
            float bannerW = BannerW * Css, bannerH = BannerH * Css;
            V5PickKit.Place(banner.rectTransform, (canvasW - bannerW) * .5f, BannerTop * Css, bannerW, bannerH);
            float ends = bannerW * V5PickKit.BannerEnds;
            V5PickKit.Place(title.rectTransform, ends, 0f, bannerW - ends * 2f, bannerH);
            V5TitleKit.FitWidth(title, V5PickKit.Px(TitlePx, Css), V5PickKit.Px(28f, Css));
            V5PickKit.Place(sub.rectTransform, 0f, (SubTop - 2f) * Css, canvasW, 30f * Css);
            // The mock-up's cards, smaller on a screen too narrow for them with SideRoom clear at each side.
            float row = count * CardW + Mathf.Max(0, count - 1) * CardGap;
            float fit = Mathf.Clamp((canvasW / Css - 2f * SideRoom) / Mathf.Max(1f, row), .5f, 1f);
            cardCss = Css * fit;
            float w = CardW * cardCss, h = CardH * cardCss, gap = CardGap * cardCss;
            float left = (canvasW - count * w - Mathf.Max(0, count - 1) * gap) * .5f;
            for (int i = 0; i < count; i++)
            {
                cards[i].view.SetSize(new Vector2(w, h));
                cards[i].view.SetRest(left + i * (w + gap), CardsTop * Css);
                LayCard(cards[i]);
            }
            hints.PlaceCentre(canvasW * .5f, canvasH - FootBottom * Css - hints.Height);
        }

        void LayCard(Card card)
        {
            float c = cardCss, w = CardW * c, h = CardH * c;
            float inLeft = w * InLeft, inTop = h * InTop, inW = w * (1f - InLeft * 2f), inH = h * (1f - InTop - InFoot);
            V5PickKit.Place(card.key.Rect, inLeft, inTop, card.key.Rect.sizeDelta.x, KeyH * Css);
            V5PickKit.Place(card.artGlow.rectTransform, 0f, GlowTop * c, w, GlowH * c);
            float y = inTop + PicTop * c;
            V5PickKit.Place(card.weapon.rectTransform, (w - PicW * c) * .5f, y, PicW * c, PicH * c);
            y += (PicH + NameGap) * c;
            // The name and its tier on one line, centred together.
            float nameH = NamePx * c * 1.25f, chipGap = ChipGap * c;
            card.name.fontSize = V5PickKit.Px(NamePx, c);
            float nameW = Mathf.Min(Mathf.Ceil(card.name.preferredWidth) + 2f, Mathf.Max(1f, inW - card.tier.Width - chipGap));
            float nameLeft = (w - nameW - chipGap - card.tier.Width) * .5f;
            V5PickKit.Place(card.name.rectTransform, nameLeft, y, nameW, nameH);
            V5TitleKit.FitWidth(card.name, V5PickKit.Px(NamePx, c), V5PickKit.Px(22f, c));
            V5PickKit.Place(card.tier.Rect, nameLeft + nameW + chipGap, y + (nameH - card.tier.Height) * .5f, card.tier.Width, card.tier.Height);
            y += nameH + RowsGap * c;
            // The attack row: the stripe down its left edge, the heading, then the attack, fitted to the row's width.
            float labelH = AttackLabelPx * c * 1.25f, styleH = StylePx * c * 1.2f;
            float attackH = (AttackPadTop + StyleGap + AttackPadFoot) * c + labelH + styleH;
            float textLeft = (StripeW + AttackPadLeft) * c, textW = inW - textLeft - AttackPadRight * c;
            V5PickKit.Place(card.attack.rectTransform, inLeft, y, inW, attackH);
            V5PickKit.Place(card.stripe.rectTransform, 0f, 0f, StripeW * c, attackH);
            card.attackLabel.fontSize = V5PickKit.Px(AttackLabelPx, c);
            V5PickKit.Place(card.attackLabel.rectTransform, textLeft, AttackPadTop * c, textW, labelH);
            V5PickKit.Place(card.attackStyle.rectTransform, textLeft, AttackPadTop * c + labelH + StyleGap * c, textW, styleH);
            V5TitleKit.FitWidth(card.attackStyle, V5PickKit.Px(StylePx, c), V5PickKit.Px(StyleSmallestPx, c));
            y += attackH + RowGap * c;
            // The trait row: its heading at the left, the question marks at the right.
            float traitH = TraitH * c;
            V5PickKit.Place(card.trait.rectTransform, inLeft, y, inW, traitH);
            card.traitLabel.fontSize = V5PickKit.Px(TraitPx, c);
            card.traitMark.fontSize = V5PickKit.Px(MarkPx, c);
            V5PickKit.Place(card.traitLabel.rectTransform, TraitPadLeft * c, 0f, inW * .6f, traitH);
            V5PickKit.Place(card.traitMark.rectTransform, inW * .4f, 0f, inW * .6f - TraitPadRight * c, traitH);
            float swapH = SwapPx * c * 1.6f;
            V5PickKit.Place(card.swap.rectTransform, inLeft - 6f * c, inTop + inH - swapH, inW + 12f * c, swapH);
            V5TitleKit.FitWidth(card.swap, V5PickKit.Px(SwapPx, c), V5PickKit.Px(11f, c));
        }

        void Update()
        {
            if (!open) return;
            bool ready = Time.unscaledTime - openedAt >= InputDelay;
            for (int i = 0; i < count; i++) cards[i].view.Live = ready;
            Refresh();
            if (!ready || V2Tutorial.HoldsInput || ArenaSettingsPanel.HoldsInput || ArenaCodex.HoldsInput) return;
            var keys = Keyboard.current;
            if (keys == null) return;
            for (int i = 0; i < count; i++)
                if (ArenaKeys.Pressed(GameAction.Skill1 + i) || NumpadPressed(keys, i))
                {
                    Choose(i);
                    return;
                }
            if (chosen >= 0 && ArenaKeys.Pressed(GameAction.Interact)) Take();
            else if (mayLeave && keys.escapeKey.wasPressedThisFrame) Leave();
        }

        static bool NumpadPressed(Keyboard keys, int i) => i switch
        {
            0 => keys.numpad1Key.wasPressedThisFrame,
            1 => keys.numpad2Key.wasPressedThisFrame,
            _ => keys.numpad3Key.wasPressedThisFrame,
        };

        /// <summary>What changes while the window is open: the chosen card, the go button's words, the keys shown, the art switch.</summary>
        void Refresh()
        {
            bool art = Art.Enabled;
            if (shownArt != art)
            {
                shownArt = art;
                V5TitleKit.Dress(veil, art ? V5PickKit.Veil : null, art ? Color.white : V5PickKit.FlatVeil);
                var set = ArtSet();
                for (int i = 0; i < count; i++) V2Hud.SetWeaponIcon(cards[i].weapon, set, cards[i].formIndex);
                for (int i = 0; i < count; i++) V5PickKit.Show(cards[i].artGlow, art);
                // The attack row's stripe: the mock-up's orange with the art on, the whitebox's edge colour without it.
                for (int i = 0; i < count; i++) cards[i].stripe.color = art ? V5TitleKit.Orange : new Color(WhiteboxSkin.Edge.r, WhiteboxSkin.Edge.g, WhiteboxSkin.Edge.b, 1f);
            }
            bool picked = chosen >= 0 && chosen < count;
            go.Button.interactable = open && picked;
            // The words and keys change only with the choice, the language, the keys the player set and the art switch.
            int state = (chosen + 1) | (mayLeave ? 1 << 3 : 0) | (count << 4) | (ArenaText.IsChinese ? 1 << 7 : 0) | (art ? 1 << 8 : 0) | (ArenaKeys.Revision << 9);
            if (state != shownState)
            {
                shownState = state;
                for (int i = 0; i < count; i++)
                {
                    cards[i].view.Chosen = i == chosen;
                    cards[i].key.SetDark(i != chosen);
                    cards[i].key.SetText(ArenaKeys.Name(GameAction.Skill1 + i));
                }
                go.Dim = !picked;
                V5PickKit.SetText(goLabel, picked ? ArenaText.Pair($"拿下{offers[chosen].name}", $"Take the {offers[chosen].name}") : ArenaText.Pair("先选一件武器", "Choose a weapon first"));
                goLabel.color = V5PickKit.GoInk(art);
                V5PickKit.Show(hazard, art);
                goKey.SetText(ArenaKeys.InteractKey);
                V5PickKit.Show(goKey, picked);
                V5PickKit.Show(leave, mayLeave);
                hintItems.Clear();
                var numbers = new string[count];
                for (int i = 0; i < count; i++) numbers[i] = ArenaKeys.Name(GameAction.Skill1 + i);
                hintItems.Add((numbers, ArenaText.Pair("选择", "Choose")));
                // The interact key takes only once a card is chosen, so it is shown from then on.
                if (picked) hintItems.Add((new[] { ArenaKeys.InteractKey }, ArenaText.Pair("拿下", "Take")));
                if (mayLeave) hintItems.Add((new[] { "Esc" }, ArenaText.Pair("不拿", "Leave it")));
                hints.Set(hintItems);
            }
            LayButtons();
        }

        void LayButtons()
        {
            var size = root.rect.size;
            float canvasW = size.x > 1f ? size.x : 1600f;
            V5PickKit.SyncFont(goLabel);
            float h = GoH * Css, pad = GoPadLeft * Css + 40f * Css, keyW = goKey.gameObject.activeSelf ? goKey.Rect.sizeDelta.x + GoKeyGap * Css : 0f;
            float labelW = Mathf.Ceil(goLabel.preferredWidth);
            float goW = Mathf.Max(260f * Css, pad + labelW + keyW + GoPadRight * Css);
            float leaveW = mayLeave ? LeaveW * Css : 0f, gap = mayLeave ? ButtonGap * Css : 0f;
            float left = (canvasW - goW - gap - leaveW) * .5f;
            go.Place(left, GoTop * Css, goW, h);
            V5PickKit.PlaceHazard(hazard, Css, h);
            V5PickKit.Place(goLabel.rectTransform, pad, 0f, labelW + 2f, h);
            if (goKey.gameObject.activeSelf) V5PickKit.Place(goKey.Rect, pad + labelW + GoKeyGap * Css, (h - goKey.Rect.sizeDelta.y) * .5f, goKey.Rect.sizeDelta.x, goKey.Rect.sizeDelta.y);
            if (mayLeave) leave.Place(left + goW + gap, GoTop * Css, leaveW, h);
        }

        void Click(int index)
        {
            if (!Ready) return;
            if (index == chosen) Take();
            else Choose(index);
        }

        void Choose(int index)
        {
            if (index < 0 || index >= count || index == chosen) return;
            chosen = index;
            V5PickKit.Sound(V5UiSound.Ui.Tab, 1.04f - index * .04f);
            Refresh();
        }

        bool Ready => open && Time.unscaledTime - openedAt >= InputDelay;

        void Take()
        {
            if (!Ready || chosen < 0 || chosen >= count) return;
            int index = chosen;
            var then = take;
            open = false;
            take = null;
            leaveCrate = null;
            V5PickKit.Sound(V5UiSound.Ui.Press);
            then?.Invoke(index);
        }

        void Leave()
        {
            if (!Ready || !mayLeave) return;
            var then = leaveCrate;
            open = false;
            take = null;
            leaveCrate = null;
            V5PickKit.Sound(V5UiSound.Ui.Back);
            then?.Invoke();
        }

        static V2ArtSet ArtSet()
        {
            var gm = GameManager.Instance;
            return gm && gm.hud && gm.hud.v2Hud ? gm.hud.v2Hud.art : null;
        }
    }
}
