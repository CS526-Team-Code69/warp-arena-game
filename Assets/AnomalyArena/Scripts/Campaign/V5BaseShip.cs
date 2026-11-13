using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The launch bay's ship status on the base page (V5BasePage), drawn over the view of the ship so one look tells where the ship stands.
    /// Along the view's foot, four cells: the base's hull (its life over its most and, once the base has a shield, the shield after it, over
    /// the day plate's segmented teal bar with the shield's steel band); the engine and the armour (teal once repaired; until then orange,
    /// with what each still needs under it: the launch bay built, at the level the part asks or repaired, else the repair's price in the
    /// currencies' icons, red where the stock is short); the fuel (its units over a full tank, gold over a bar notched per unit while it
    /// fills, teal and ticked once full; while the bay cannot make fuel, what the bay needs in place of the bar). At the view's top right,
    /// level with its caption, the ignition: a diamond each for the engine, the armour and the fuel, teal once done, and "ready to launch"
    /// in teal or how many are still missing. Everything is read from the campaign's own values whenever the page refreshes; nothing here
    /// changes them. With the art on: the dark chamfered backing of the view's chips, the machined medals and the bars' pictures; in
    /// whitebox: flat fills with the thin edge, the whitebox icons and flat bars. Built only with the fifth pass's page (V5HudKit.On).
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The board in the view's window (802 x 434 mock-up pixels inside the view's rim): four cells 84 tall, 14 from the window's sides
        // and foot and 10 apart; the hull and the fuel, which carry a number and a bar, 206 wide, the engine and the armour 166.
        const float B5ShipInset = 14f, B5ShipCellH = 84f, B5ShipCellGap = 10f;
        static readonly float[] B5ShipCellW = { 206f, 166f, 166f, 206f };
        static readonly string[] B5ShipCellNames = { "Hull", "Engine", "Armor", "Fuel" };
        // Inside a cell: the medal 40 across, 10 in and 9 down; the name (16 px) and the state (14 px, a number 15) 60 in; the detail row
        // 10 in and 57 down, 18 tall: the bar (9 tall, 61 down), the words of what is still needed (13 px), or a price (16-pixel icons).
        const float B5ShipMedal = 40f, B5ShipText = 60f, B5ShipDetailTop = 57f, B5ShipDetailH = 18f, B5ShipBarTop = 61f, B5ShipBarH = 9f, B5ShipCoin = 16f;
        // The ignition at the window's top right, level with the caption: 36 tall, 8 down and 14 in; its diamonds 11 across, their centres
        // 18 apart from 22 in; its words 76 in and 16 from its right end.
        const float B5IgniteTop = 8f, B5IgniteH = 36f, B5IgniteDiamond = 11f, B5IgniteStep = 18f, B5IgniteFirst = 22f, B5IgniteText = 76f, B5IgniteEnd = 16f;
        // A tank of more units than this is notched in tenths instead of once per unit.
        const int B5FuelNotchesMost = 20;

        sealed class B5ShipCell
        {
            public RectTransform root;
            public Image back;
            public V5HudMedal medal;
            public Text name, state, detail;
            public V5StoryBar bar, barFull;
            public readonly Image[] holes = new Image[3], coins = new Image[3];
            public readonly Text[] amounts = new Text[3];
            // The state last shown, so a change while the board shows swells once.
            public string shown;
        }

        RectTransform b5Ship, b5Ignite;
        Image b5IgniteBack;
        Text b5IgniteWords;
        string b5IgniteShown;
        bool? b5ShipChinese;
        int b5FuelNotches = -1;
        readonly Image[] b5IgniteDiamonds = new Image[3];
        readonly B5ShipCell[] b5ShipCells = new B5ShipCell[4];

        // ───── Building (BuildBase5Middle) ─────

        void BuildBase5Ship(RectTransform window)
        {
            b5Ship = V5TitleKit.Rect(window, "ShipStatus");
            V5TitleKit.Stretch(b5Ship);
            float x = B5ShipInset;
            for (int i = 0; i < 4; i++)
            {
                var cell = b5ShipCells[i] = new B5ShipCell();
                float w = B5ShipCellW[i];
                cell.root = V5TitleKit.Rect(b5Ship, B5ShipCellNames[i]);
                cell.root.anchorMin = cell.root.anchorMax = cell.root.pivot = Vector2.zero;
                cell.root.anchoredPosition = new Vector2(Bp(x), Bp(B5ShipInset));
                cell.root.sizeDelta = new Vector2(Bp(w), Bp(B5ShipCellH));
                x += w + B5ShipCellGap;
                cell.back = V5TitleKit.Picture(cell.root, "Back", null, Color.white);
                V5TitleKit.Stretch(cell.back.rectTransform);
                cell.medal = V5HudKit.Medal(cell.root, "Medal", Bp(B5ShipMedal));
                PlaceCentre(cell.medal.Rect, new Vector2(0f, 1f), new Vector2(Bp(10f + B5ShipMedal * .5f), -Bp(9f + B5ShipMedal * .5f)), Vector2.one * Bp(B5ShipMedal));
                cell.name = V5StoryKit.Label(cell.root, "Name", 16f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                B5ShipPlace(cell.name, B5ShipText, 9f, w - B5ShipText - 10f, 20f);
                cell.state = V5StoryKit.Label(cell.root, "State", 14f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);
                B5ShipPlace(cell.state, B5ShipText, 31f, w - B5ShipText - 10f, 18f);
                cell.detail = V5StoryKit.Label(cell.root, "Needs", 13f, V5TitleKit.Ink3, TextAnchor.MiddleLeft);
                B5ShipPlace(cell.detail, 10f, B5ShipDetailTop, w - 20f, B5ShipDetailH);
                if (i == 0)
                {
                    // The base's life with the shield's band along its upper part, notched in tenths: the day plate's bar.
                    cell.bar = V5StoryBar.Create(cell.root, "Bar", V5StoryKit.BaseTop, V5StoryKit.BaseBottom, V5TitleKit.Steel, 10);
                    V5TitleKit.Place(cell.bar.Rect, Bp(10f), Bp(B5ShipBarTop), Bp(w - 20f), Bp(B5ShipBarH));
                }
                if (i is 1 or 2)
                {
                    // The repair's price: each currency's icon, cut round with the art on, and its amount.
                    var disc = V5TitleKit.Piece("disc");
                    for (int k = 0; k < 3; k++)
                    {
                        cell.holes[k] = V5TitleKit.Picture(cell.root, "Coin" + k, disc, Color.white);
                        cell.holes[k].gameObject.AddComponent<Mask>().showMaskGraphic = false;
                        cell.coins[k] = V5TitleKit.Picture(cell.holes[k].transform, "Icon", null, Color.white);
                        V5TitleKit.Stretch(cell.coins[k].rectTransform);
                        cell.coins[k].preserveAspect = true;
                        cell.amounts[k] = V5StoryKit.Label(cell.root, "Amount" + k, 14f, V5TitleKit.Steel, TextAnchor.MiddleLeft, true);
                    }
                }
                // The fuel's bars are made at the first refresh, notched by the rules' tank (B5FuelBars).
            }

            b5Ignite = V5TitleKit.Rect(b5Ship, "Ignition");
            b5Ignite.anchorMin = b5Ignite.anchorMax = b5Ignite.pivot = Vector2.one;
            b5Ignite.anchoredPosition = new Vector2(-Bp(B5ShipInset), -Bp(B5IgniteTop));
            b5Ignite.sizeDelta = new Vector2(Bp(B5IgniteText + B5IgniteEnd), Bp(B5IgniteH));
            b5IgniteBack = V5TitleKit.Picture(b5Ignite, "Back", null, Color.white);
            V5TitleKit.Stretch(b5IgniteBack.rectTransform);
            for (int k = 0; k < 3; k++)
            {
                var diamond = b5IgniteDiamonds[k] = V5TitleKit.Picture(b5Ignite, "Diamond" + B5ShipCellNames[k + 1], null, Color.white);
                var rect = diamond.rectTransform;
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(Bp(B5IgniteFirst + k * B5IgniteStep), 0f);
                rect.sizeDelta = Vector2.one * Bp(B5IgniteDiamond);
                var rim = diamond.gameObject.AddComponent<UnityEngine.UI.Outline>();
                rim.useGraphicAlpha = false;
                rim.effectColor = B5DiamondRim;
                rim.effectDistance = new Vector2(1f, -1f);
            }
            b5IgniteWords = V5StoryKit.Label(b5Ignite, "Words", 16f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
        }

        // ───── Refreshing (RefreshBase5View) ─────

        /// <summary>The board, shown on the launch bay's view only: the four cells and the ignition, from the campaign's values now.</summary>
        void RefreshBase5Ship(V2Campaign c, bool art, bool shown)
        {
            if (!b5Ship) return;
            SetShown(b5Ship, shown);
            // Seen again later, or in the other language, the board starts from what it shows then: no swell for what changed meanwhile.
            if (!shown || b5ShipChinese != ArenaText.IsChinese)
            {
                foreach (var cell in b5ShipCells) cell.shown = null;
                b5IgniteShown = null;
                b5ShipChinese = ArenaText.IsChinese;
            }
            if (!shown) return;
            var set = c.art;
            var rules = c.rules;
            bool settled = Time.frameCount - b5TickFrame <= 1;
            foreach (var cell in b5ShipCells)
                B5Flat(cell.back, art, V5TitleKit.Chamfer, B5ChipBack, V5HudKit.ChamferDensity(10f, B5Css), WhiteboxSkin.PlateFill, WhiteboxSkin.Edge);

            // The base's hull: its life over its most, the shield after it while the base has one; orange while it is not whole (Central
            // Control repairs it).
            var hull = b5ShipCells[0];
            float max = Mathf.Max(1f, c.BaseMaxHp), hp = Mathf.Clamp(c.BaseHp, 0f, max);
            bool shielded = c.ShieldLevel > 0;
            float shield = shielded ? Mathf.Max(0f, c.BaseShield) : 0f;
            hull.medal.SetIcons(set ? set.durability : null, WhiteboxIcons.Room(CampaignRoom.Control));
            hull.medal.SetRing(hp >= max - .5f ? V5TitleKit.Teal : V5TitleKit.Orange);
            B5ShipWords(hull, ArenaText.Pair("基地外壳", "Base hull"), B5HullNumber(hp, max, shield, shielded), V5TitleKit.Ink, 15f, settled);
            hull.bar.Set(hp / max, shield / max);
            SetShown(hull.detail, false);

            // The engine (the launch bay's first level) and the armour (its second).
            B5ShipPart(b5ShipCells[1], c, art, set ? set.engine : null, V5StoryKit.EngineIcon, ArenaText.Pair("引擎", "Engine"), c.EngineFixed, 1,
                rules ? rules.engineRepair : default, ArenaText.Pair("运转正常", "Running"), settled);
            B5ShipPart(b5ShipCells[2], c, art, set ? set.armor : null, V5StoryKit.ArmorIcon, ArenaText.Pair("装甲", "Armor"), c.ArmorFixed, 2,
                rules ? rules.armorRepair : default, ArenaText.Pair("装甲完好", "Intact"), settled);
            B5ShipFuel(b5ShipCells[3], c, set ? set.fuel : null, settled);
            RefreshBase5Ignite(c, art, settled);
        }

        /// <summary>The engine or the armour: repaired in teal (and a quiet word that it works); until then orange, with what it still needs
        /// under it: the launch bay at the level it asks or repaired, else the repair's price.</summary>
        void B5ShipPart(B5ShipCell cell, V2Campaign c, bool art, Sprite artIcon, Sprite flatIcon, string name, bool done, int level, CampaignCost price,
            string working, bool settled)
        {
            cell.medal.SetIcons(artIcon, flatIcon);
            cell.medal.SetRing(done ? V5TitleKit.Teal : V5TitleKit.Orange);
            B5ShipWords(cell, name, done ? ArenaText.Pair("已修好", "Repaired") : ArenaText.Pair("待修复", "Needs repair"),
                done ? V5TitleKit.Teal : V5TitleKit.OrangeHi, 14f, settled);
            var colour = V5TitleKit.Ink3;
            string words = done ? working : B5ShipNeeds(c, level, false, out colour);
            B5ShipPrice(cell, c, art, price, words == null && !price.IsFree);
            B5ShipDetail(cell, words, colour);
        }

        /// <summary>The fuel: its units over a full tank (gold while it fills, teal and ticked once full) over a bar notched per unit; while
        /// the launch bay cannot make fuel, what the bay needs in place of the bar.</summary>
        void B5ShipFuel(B5ShipCell cell, V2Campaign c, Sprite artIcon, bool settled)
        {
            bool full = c.FuelFull;
            int units = Mathf.Max(1, c.FuelUnits), fuel = Mathf.Clamp(c.Fuel, 0, units);
            cell.medal.SetIcons(artIcon, V5StoryKit.FuelIcon);
            cell.medal.SetRing(full ? V5TitleKit.Teal : V5TitleKit.Gold);
            B5ShipWords(cell, ArenaText.Pair("燃料", "Fuel"), full ? $"{fuel} / {units}  ✓" : $"{fuel} / {units}", full ? V5TitleKit.Teal : V5TitleKit.Gold, 15f, settled);
            var colour = V5TitleKit.Ink3;
            string needs = full ? null : B5ShipNeeds(c, 3, true, out colour);
            B5FuelBars(cell, units);
            bool barred = needs == null;
            SetShown(cell.bar, barred && !full);
            SetShown(cell.barFull, barred && full);
            if (barred) (full ? cell.barFull : cell.bar).Set(fuel / (float)units, 0f);
            B5ShipDetail(cell, needs, colour);
        }

        /// <summary>The fuel's two bars (filling in gold, full in teal), notched once per unit of the tank, or in tenths for a tank of more
        /// than B5FuelNotchesMost units; made again should the tank's size change.</summary>
        void B5FuelBars(B5ShipCell cell, int units)
        {
            int notches = units <= B5FuelNotchesMost ? units : 10;
            if (cell.bar && cell.barFull && notches == b5FuelNotches) return;
            if (cell.bar) Destroy(cell.bar.gameObject);
            if (cell.barFull) Destroy(cell.barFull.gameObject);
            b5FuelNotches = notches;
            int count = (int)B5StepLook.Count, done = (int)B5StepLook.Done;
            cell.bar = V5StoryBar.Create(cell.root, "Bar", B5StepTop[count], B5StepFoot[count], V5TitleKit.Gold, notches);
            cell.barFull = V5StoryBar.Create(cell.root, "BarFull", B5StepTop[done], B5StepFoot[done], V5TitleKit.Teal, notches);
            float w = B5ShipCellW[3] - 20f;
            V5TitleKit.Place(cell.bar.Rect, Bp(10f), Bp(B5ShipBarTop), Bp(w), Bp(B5ShipBarH));
            V5TitleKit.Place(cell.barFull.Rect, Bp(10f), Bp(B5ShipBarTop), Bp(w), Bp(B5ShipBarH));
        }

        /// <summary>What the launch bay must be before a part of the ship can be worked on there, or null when it is (the campaign's own
        /// rule: the bay at the part's level and not broken). Built, or at the part's level: orange when that is the bay's next level, grey
        /// when it is further; repaired: orange.</summary>
        static string B5ShipNeeds(V2Campaign c, int level, bool fuel, out Color colour)
        {
            var bay = c.Room(CampaignRoom.Ship);
            colour = V5TitleKit.OrangeHi;
            if (bay.level < level)
            {
                if (level > bay.level + 1) colour = V5TitleKit.Ink3;
                if (level == 1) return ArenaText.Pair("先建造飞船发射区", "Build the bay first");
                return fuel ? ArenaText.Pair($"飞船发射区 {level} 级后可合成", $"Needs bay level {level}")
                    : ArenaText.Pair($"飞船发射区 {level} 级后可修", $"Needs bay level {level}");
            }
            return bay.damaged ? ArenaText.Pair("先修好飞船发射区", "Repair the bay first") : null;
        }

        /// <summary>A part's repair price along its cell's detail row: each currency's icon and amount, the amount red while the stock is
        /// short of it (as the actions' prices).</summary>
        void B5ShipPrice(B5ShipCell cell, V2Campaign c, bool art, CampaignCost price, bool shown)
        {
            float x = Bp(10f), middle = -Bp(B5ShipDetailTop + B5ShipDetailH * .5f), coin = Bp(B5ShipCoin);
            int[] held = { c.Energy, c.Alloy, c.Dark };
            for (int k = 0; k < 3; k++)
            {
                int amount = !shown ? 0 : k == 0 ? price.energy : k == 1 ? price.alloy : price.dark;
                bool on = amount > 0;
                SetShown(cell.holes[k], on);
                SetShown(cell.amounts[k], on);
                if (!on) continue;
                var sprite = CurrencySprite(k);
                // With the art on the currency's picture is cut round; whitebox shows its icon whole.
                var round = cell.holes[k].GetComponent<Mask>();
                if (round.enabled != art) round.enabled = art;
                cell.holes[k].enabled = art;
                cell.coins[k].sprite = sprite;
                cell.coins[k].enabled = sprite;
                B5DarkIcon(k == 2 ? cell.coins[k].transform : null, art);
                PlaceCentre(cell.holes[k].rectTransform, new Vector2(0f, 1f), new Vector2(x + coin * .5f, middle), Vector2.one * coin);
                if (sprite) x += coin + Bp(5f);
                string text = sprite ? amount.ToString() : $"{CurrencyLabel(k)} {amount}";
                var label = cell.amounts[k];
                StoryText(label, text);
                label.color = held[k] >= amount ? WalletColour(k) : B5Short;
                float w = V5StoryKit.WidthOf(label, text);
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, .5f);
                rect.anchoredPosition = new Vector2(x, middle);
                rect.sizeDelta = new Vector2(w + 2f, Bp(B5ShipDetailH));
                x += w + Bp(12f);
            }
        }

        /// <summary>The words along a cell's detail row (what is still needed, or that a part works), none for none.</summary>
        static void B5ShipDetail(B5ShipCell cell, string words, Color colour)
        {
            bool shown = !string.IsNullOrEmpty(words);
            SetShown(cell.detail, shown);
            if (!shown) return;
            StoryText(cell.detail, words);
            cell.detail.color = colour;
            V5TitleKit.FitWidth(cell.detail, V5StoryKit.FontSize(13f), V5StoryKit.FontSize(11f));
        }

        /// <summary>A cell's name and state; a state that changes while the board shows swells once.</summary>
        void B5ShipWords(B5ShipCell cell, string name, string state, Color colour, float statePx, bool settled)
        {
            StoryText(cell.name, name);
            V5TitleKit.FitWidth(cell.name, V5StoryKit.FontSize(16f), V5StoryKit.FontSize(12f));
            if (settled && cell.shown != null && cell.shown != state) PulseOnce(cell.state.transform);
            cell.shown = state;
            StoryText(cell.state, state);
            cell.state.color = colour;
            V5TitleKit.FitWidth(cell.state, V5StoryKit.FontSize(statePx), V5StoryKit.FontSize(11f));
        }

        /// <summary>The base's life over its most ("1250 / 1500", the second part smaller and dimmer) and, while the base has a shield, the
        /// shield after it in steel ("+300"; "+0" once it is spent), as the day plate writes it.</summary>
        static string B5HullNumber(float hp, float max, float shield, bool shielded)
        {
            int small = V5StoryKit.FontSize(12f);
            string number = $"{Mathf.CeilToInt(hp)}<size={small}><color=#{HexOf(V5TitleKit.Ink3)}> / {Mathf.CeilToInt(max)}</color></size>";
            if (shielded) number += $"<size={small}><color=#{HexOf(V5TitleKit.Steel)}>  +{Mathf.CeilToInt(shield)}</color></size>";
            return number;
        }

        /// <summary>The ignition: a diamond each for the engine, the armour and the fuel (the cells' order), teal once done, then "ready to
        /// launch" in teal (the campaign's CanIgnite) or how many are still missing, the count in orange.</summary>
        void RefreshBase5Ignite(V2Campaign c, bool art, bool settled)
        {
            bool[] done = { c.EngineFixed, c.ArmorFixed, c.FuelFull };
            int missing = 0, teal = (int)B5StepLook.Done;
            for (int k = 0; k < 3; k++)
            {
                var diamond = b5IgniteDiamonds[k];
                if (!done[k]) missing++;
                V5TitleKit.Dress(diamond, done[k] && art ? V5StoryKit.Fill(B5StepTop[teal], B5StepFoot[teal]) : WhiteboxSkin.Square,
                    done[k] ? (art ? Color.white : V5TitleKit.Teal) : B5DiamondOff);
                if (diamond.TryGetComponent<UnityEngine.UI.Outline>(out var rim)) rim.enabled = !done[k];
            }
            bool ready = c.CanIgnite;
            string count = $"<color=#{HexOf(V5TitleKit.OrangeHi)}>{missing}</color>";
            string words = ready ? ArenaText.Pair("可以点火返航", "Ready to launch")
                : ArenaText.Pair($"点火还差 {count} 项", $"{count} more before ignition");
            if (settled && b5IgniteShown != null && b5IgniteShown != words) PulseOnce(b5IgniteWords.transform);
            b5IgniteShown = words;
            StoryText(b5IgniteWords, words);
            b5IgniteWords.color = ready ? V5TitleKit.Teal : V5TitleKit.Ink2;
            b5IgniteWords.fontSize = V5StoryKit.FontSize(16f);
            float wordsW = V5StoryKit.WidthOf(b5IgniteWords, words), height = Bp(B5IgniteH);
            B5PlaceLeft(b5IgniteWords, Bp(B5IgniteText), wordsW, height);
            b5Ignite.sizeDelta = new Vector2(Bp(B5IgniteText + B5IgniteEnd) + wordsW, height);
            B5Flat(b5IgniteBack, art, V5TitleKit.Chamfer, B5ChipBack, V5HudKit.ChamferDensity(10f, B5Css), WhiteboxSkin.PlateFill,
                ready ? V5TitleKit.Teal : WhiteboxSkin.Edge);
        }

        /// <summary>A label in a cell, left and top from the cell's top-left corner in mock-up pixels; it swells from its left end.</summary>
        static void B5ShipPlace(Text label, float left, float top, float width, float height)
        {
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(Bp(left), -Bp(top + height * .5f));
            rect.sizeDelta = new Vector2(Bp(width), Bp(height));
        }
    }
}
