using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The rocket page (火箭), the base page the ship opens (work/art-plans/style-1112/ui-f/rocket.html), in the frame of V5BasePage.cs.
    /// It holds only what takes the crew home. Left, the view of the ship with its parts' pins (V5BaseShip.cs) and under it a card for
    /// each part, the engine, the hull and the fuel tank: what it gives once done, its price and its button (the next one to do warm with
    /// the orange button, one the stock does not pay for grey); a part done is lit in teal and shows no price. Right, the way home: its
    /// four steps (the three parts and the fuel, its count over a full tank on a violet bar, with the button that pours the dark matter
    /// held into it), the big ignition button (grey until all four are done) and the line under it; below, the mining lamps: how many,
    /// their price and the button that makes one. Everything is read and bought through V2Campaign.Redesign.cs (ShipParts,
    /// TryBuyShipPart, TryAddFuel, CanIgnite and AskIgnite, LampPrice and TryCraftLamp).
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The rocket page (mock-up pixels): under the view, the parts' cards from R5PartsTop to the floor, R5PartW wide and R5PartGap
        // apart; at the right, from R5RightX and R5RightW wide, the way home beside the view and the lamps' card beside the parts.
        const float R5PartsTop = 654f, R5PartW = 380f, R5PartGap = 20f, R5RightX = 1260f, R5RightW = 624f;
        // The way home: 34 in, its title 26 down, its steps from R5StepsTop R5StepH tall and R5StepGap apart, the fuel's bar 10 under the
        // last, the ignition button R5FireH tall over its line at the plate's foot.
        const float R5Pad = 34f, R5StepsTop = 88f, R5StepH = 32f, R5StepGap = 16f, R5StepDot = 30f, R5FireH = 72f;

        static readonly Color R5FuelTop = V5TitleKit.Rgb(196, 164, 255), R5FuelFoot = V5TitleKit.Rgb(106, 63, 214), R5FuelInk = V5TitleKit.Rgb(185, 150, 255),
            R5StepOff = V5TitleKit.Rgb(16, 20, 31), R5StepRim = V5TitleKit.Rgb(163, 172, 190, .55f), R5StepTrack = V5TitleKit.Rgb(163, 172, 190, .28f);
        static readonly Regex R5Numbers = new(@"[+−\-]\d+(\.\d+)?%");

        sealed class R5Part
        {
            public V2Campaign.ShipPartKind kind;
            public V5HudPlate plate;
            public Image tab;
            public V5HudMedal medal;
            public Text name, status, effect;
            public B5Coins coins;
            public V5Plate button;
            public Text verb;
        }

        sealed class R5Step
        {
            public Image dot, ring;
            public Text words;
        }

        readonly R5Part[] r5Parts = new R5Part[3];
        readonly R5Step[] r5Steps = new R5Step[4];
        RectTransform r5Home, r5Lamp;
        V5HudPlate r5HomePlate, r5LampPlate;
        Image r5StepLine;
        Text r5HomeTitle, r5FuelCount, r5RefuelVerb, r5FireLabel, r5FireNote, r5LampTitle, r5LampLine, r5LampVerb;
        V5StoryBar r5FuelBar;
        V5Plate r5Refuel, r5Fire, r5LampButton;
        V5HudMedal r5LampMedal;
        B5Coins r5LampCoins;

        // ───── Building (BuildBase5) ─────

        void BuildRocket5(RectTransform parent)
        {
            BuildRocket5View(parent);
            for (int k = 0; k < 3; k++)
            {
                var part = r5Parts[k] = new R5Part { kind = (V2Campaign.ShipPartKind)k };
                part.plate = V5HudKit.Plate(parent, "Part" + part.kind, V5HudKit.PlateLook.Plain, 14f, B5Css);
                var t = part.plate.transform;
                part.tab = V5TitleKit.Picture(t, "Tab", null, Color.white);
                part.medal = V5HudKit.Medal(t, "Medal", Bp(66f));
                part.name = V5StoryKit.Label(t, "Name", 28f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                part.status = V5StoryKit.Label(t, "State", 20f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
                part.effect = V5StoryKit.Label(t, "Effect", 19f, V5TitleKit.Ink2, TextAnchor.UpperLeft);
                part.effect.lineSpacing = 1.15f;
                part.coins = B5BuildCoins(t, 32f);
                var kind = part.kind;
                part.button = B5Button(t, "Buy", V5Plate.Look.Plate, () => B5Act(() => campaign.TryBuyShipPart(kind)));
                part.verb = V5StoryKit.Label(part.button.transform, "Verb", 21f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(part.verb.rectTransform);
            }

            r5Home = V5TitleKit.Rect(parent, "WayHome");
            r5HomePlate = V5HudKit.Plate(r5Home, "Plate", V5HudKit.PlateLook.Plain, 16f, B5Css);
            V5TitleKit.Stretch(r5HomePlate.Rect);
            r5HomeTitle = V5StoryKit.Label(r5Home, "Title", 30f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(r5HomeTitle);
            // The line that joins the steps' dots, behind them.
            r5StepLine = V5TitleKit.Picture(r5Home, "StepLine", null, R5StepTrack);
            for (int i = 0; i < 4; i++)
            {
                var step = r5Steps[i] = new R5Step();
                step.ring = V5TitleKit.Picture(r5Home, "StepRing" + (i + 1), V5HudKit.RingLine, R5StepRim);
                step.dot = V5TitleKit.Picture(r5Home, "Step" + (i + 1), V5TitleKit.Piece("disc"), R5StepOff);
                step.words = V5StoryKit.Label(r5Home, "StepWords" + (i + 1), 24f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            }
            r5FuelCount = V5StoryKit.Label(r5Home, "Fuel", 26f, R5FuelInk, TextAnchor.MiddleRight, true);
            r5FuelBar = V5StoryBar.Create(r5Home, "FuelBar", R5FuelTop, R5FuelFoot, R5FuelInk, 10);
            r5Refuel = B5Button(r5Home, "Refuel", V5Plate.Look.Plate, () => B5Act(() => campaign.TryAddFuel()));
            r5RefuelVerb = V5StoryKit.Label(r5Refuel.transform, "Verb", 19f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(r5RefuelVerb.rectTransform);
            r5Fire = B5Button(r5Home, "Ignite", V5Plate.Look.Plate, () => B5Act(() =>
            {
                if (!campaign.CanIgnite) return false;
                campaign.AskIgnite(); // the question first: after the ignition there is no way back (V2Campaign.IgniteAsk.cs)
                return true;
            }));
            r5FireLabel = V5StoryKit.Label(r5Fire.transform, "Label", 28f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(r5FireLabel.rectTransform);
            r5FireNote = V5StoryKit.Label(r5Home, "FireNote", 18f, V5TitleKit.Ink3, TextAnchor.MiddleCenter);

            r5Lamp = V5TitleKit.Rect(parent, "Lamps");
            r5LampPlate = V5HudKit.Plate(r5Lamp, "Plate", V5HudKit.PlateLook.Plain, 14f, B5Css);
            V5TitleKit.Stretch(r5LampPlate.Rect);
            r5LampMedal = V5HudKit.Medal(r5Lamp, "Medal", Bp(104f));
            r5LampMedal.SetGlow(V5StoryKit.Amber);
            r5LampTitle = V5StoryKit.Label(r5Lamp, "Title", 28f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            r5LampLine = V5StoryKit.Label(r5Lamp, "Line", 19f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            r5LampCoins = B5BuildCoins(r5Lamp, 30f);
            r5LampButton = B5Button(r5Lamp, "Make", V5Plate.Look.Plate, () => B5Act(() => campaign.TryCraftLamp()));
            r5LampVerb = V5StoryKit.Label(r5LampButton.transform, "Verb", 21f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(r5LampVerb.rectTransform);
        }

        // ───── Refreshing (RefreshBase5) ─────

        void RefreshRocket5(V2Campaign c, bool art)
        {
            var parts = c.ShipParts();
            RefreshRocket5View(c, art, parts);
            // The next part to do: the first one not done that the stock pays for.
            int next = -1;
            for (int k = 0; k < parts.Count && next < 0; k++)
                if (!parts[k].done && parts[k].affordable) next = k;
            for (int k = 0; k < 3; k++) LayPart5(c, art, r5Parts[k], k < parts.Count ? parts[k] : null, k, k == next);
            LayHome5(c, art, parts);
            LayLamps5(c, art);
        }

        /// <summary>One part's card: its medal, its name and state, what it gives (once done, or "once repaired / fitted:" before), and
        /// while it is not done its price and button. The next part to do is warm with the hazard tab and the orange button; a part done
        /// is lit in teal with its numbers in alloy's copper.</summary>
        void LayPart5(V2Campaign c, bool art, R5Part card, V2Campaign.ShipPart part, int index, bool next)
        {
            SetShown(card.plate, part != null);
            if (part == null) return;
            float w = R5PartW, h = B5Floor - R5PartsTop;
            B5Rect(card.plate.Rect, B5Margin + index * (R5PartW + R5PartGap), R5PartsTop, w, h);
            bool done = part.done;
            card.plate.SetLook(done ? V5HudKit.PlateLook.Teal : next ? V5HudKit.PlateLook.Warm : V5HudKit.PlateLook.Plain);
            SetShown(card.tab, next);
            if (next)
            {
                V5TitleKit.Dress(card.tab, art ? V5TitleKit.Piece("hazard-tab") : null, art ? Color.white : V5TitleKit.Orange);
                B5Rect(card.tab.rectTransform, 26f, -5f, 72f, 10f);
            }
            var set = c.art;
            Sprite artIcon = set ? part.kind switch { V2Campaign.ShipPartKind.Engine => set.engine, V2Campaign.ShipPartKind.Hull => set.armor, _ => set.fuel } : null;
            Sprite flatIcon = part.kind switch { V2Campaign.ShipPartKind.Engine => V5StoryKit.EngineIcon, V2Campaign.ShipPartKind.Hull => V5StoryKit.ArmorIcon, _ => V5StoryKit.FuelIcon };
            card.medal.SetIcons(artIcon, flatIcon);
            card.medal.SetRing(done ? V5TitleKit.Steel : next ? V5TitleKit.Orange : Color.clear);
            PlaceCentre(card.medal.Rect, new Vector2(0f, 1f), new Vector2(Bp(22f + 33f), -Bp(22f + 33f)), Vector2.one * Bp(66f));
            StoryText(card.name, part.name);
            B5Rect(card.name.rectTransform, 104f, 22f, w - 124f, 36f);
            V5TitleKit.FitWidth(card.name, V5StoryKit.FontSize(28f), V5StoryKit.FontSize(18f));
            StoryText(card.status, (part.status ?? "").Replace("✓", $"<color=#{HexOf(B5Ok)}>✓</color>"));
            card.status.color = done ? V5TitleKit.Steel : next ? V5TitleKit.OrangeHi : V5TitleKit.Ink2;
            B5Rect(card.status.rectTransform, 104f, 60f, w - 124f, 26f);
            V5TitleKit.FitWidth(card.status, V5StoryKit.FontSize(20f), V5StoryKit.FontSize(14f));

            SetShown(card.button, !done);
            if (done)
            {
                B5HideCoins(card.coins);
                StoryText(card.effect, R5Numbers.Replace(part.effect ?? "", m => $"<color=#{HexOf(ResourceColours.Alloy)}>{m.Value}</color>"));
                card.effect.color = V5TitleKit.Ink;
                B5Rect(card.effect.rectTransform, 22f, 98f, w - 44f, h - 98f - 18f);
                B5FitLines(card.effect, V5StoryKit.FontSize(22f), V5StoryKit.FontSize(14f));
                return;
            }
            string before = part.kind == V2Campaign.ShipPartKind.Tank ? ArenaText.Pair("装好后：", "Once fitted: ") : ArenaText.Pair("修好后：", "Once repaired: ");
            StoryText(card.effect, before + part.effect);
            card.effect.color = V5TitleKit.Ink2;
            float buttonTop = h - 16f - 54f;
            B5Rect(card.effect.rectTransform, 22f, 94f, w - 44f, buttonTop - 94f - 6f);
            B5FitLines(card.effect, V5StoryKit.FontSize(19f), V5StoryKit.FontSize(13f));
            StoryText(card.verb, part.verb);
            card.verb.fontSize = V5StoryKit.FontSize(21f);
            float bw = Mathf.Max(118f, B5W(card.verb, part.verb) + 40f);
            card.button.SetLook(next ? V5Plate.Look.Go : V5Plate.Look.Plate);
            B5Place(card.button, w - 24f - bw, buttonTop, bw, 54f);
            B5Live(card.button, card.verb, part.affordable, next ? art ? V5TitleKit.Dark : Color.white : V5TitleKit.Ink);
            int n = B5PriceOf(part.price);
            B5LayCoins(card.coins, n, 22f, buttonTop + 27f, 36f, art, false);
            B5AddFocus("c.part." + part.kind, card.button);
        }

        /// <summary>The way home: its four steps (a lit dot and a green check once done), the fuel's count and bar with the button that
        /// pours the dark matter held into it, the ignition button (orange once all four are done, grey until then) and its line.</summary>
        void LayHome5(V2Campaign c, bool art, IReadOnlyList<V2Campaign.ShipPart> parts)
        {
            float w = R5RightW, h = R5ViewH;
            B5Rect(r5Home, R5RightX, B5Top, w, h);
            StoryText(r5HomeTitle, ArenaText.Pair("回家进度", "Way home"));
            B5Rect(r5HomeTitle.rectTransform, R5Pad, 26f, w - R5Pad * 2f, 38f);
            V5TitleKit.FitWidth(r5HomeTitle, V5StoryKit.FontSize(30f), V5StoryKit.FontSize(20f));
            bool full = c.Fuel >= c.FuelTop;
            bool[] done = { Done(0), Done(1), Done(2), full };
            string[] words =
            {
                ArenaText.Pair("① 修好引擎", "① Repair the engine"), ArenaText.Pair("② 修好船体", "② Repair the hull"),
                ArenaText.Pair("③ 装上燃料舱", "③ Fit the fuel tank"), ArenaText.Pair("④ 燃料加满", "④ Fill the fuel"),
            };
            float dotX = R5Pad + R5StepDot * .5f, textLeft = R5Pad + R5StepDot + 18f;
            float firstMiddle = R5StepsTop + R5StepH * .5f, lastMiddle = R5StepsTop + 3f * (R5StepH + R5StepGap) + R5StepH * .5f;
            B5Rect(r5StepLine.rectTransform, dotX - 1.5f, firstMiddle, 3f, lastMiddle - firstMiddle);
            for (int i = 0; i < 4; i++)
            {
                var step = r5Steps[i];
                float middle = R5StepsTop + i * (R5StepH + R5StepGap) + R5StepH * .5f;
                PlaceCentre(step.dot.rectTransform, new Vector2(0f, 1f), new Vector2(Bp(dotX), -Bp(middle)), Vector2.one * Bp(R5StepDot - 4f));
                PlaceCentre(step.ring.rectTransform, new Vector2(0f, 1f), new Vector2(Bp(dotX), -Bp(middle)), Vector2.one * Bp(R5StepDot + 4f));
                step.dot.color = done[i] ? V5TitleKit.Steel : R5StepOff;
                step.ring.color = done[i] ? new Color(V5TitleKit.Steel.r, V5TitleKit.Steel.g, V5TitleKit.Steel.b, .8f) : R5StepRim;
                StoryText(step.words, done[i] ? $"{words[i]}  <color=#{HexOf(B5Ok)}>✓</color>" : words[i]);
                step.words.color = done[i] ? V5TitleKit.Ink : V5TitleKit.Ink2;
                B5Rect(step.words.rectTransform, textLeft, middle - 18f, i == 3 ? 260f : w - R5Pad - textLeft, 36f);
                V5TitleKit.FitWidth(step.words, V5StoryKit.FontSize(24f), V5StoryKit.FontSize(16f));
            }

            // The fuel: its count over a full tank beside the last step, the button that pours the dark matter held into it, the bar under.
            float fuelMiddle = lastMiddle, right = w - R5Pad;
            SetShown(r5Refuel, !full);
            if (!full)
            {
                StoryText(r5RefuelVerb, ArenaText.Pair("加注", "Refuel"));
                r5RefuelVerb.fontSize = V5StoryKit.FontSize(19f);
                float bw = Mathf.Max(92f, B5W(r5RefuelVerb, r5RefuelVerb.text) + 40f);
                B5Place(r5Refuel, right - bw, fuelMiddle - 20f, bw, 40f);
                B5Live(r5Refuel, r5RefuelVerb, c.CanAddFuel, V5TitleKit.Ink);
                B5AddFocus("c.refuel", r5Refuel);
                right -= bw + 16f;
            }
            StoryText(r5FuelCount, $"{c.Fuel}<size={V5StoryKit.FontSize(20f)}><color=#{HexOf(V5TitleKit.Ink3)}> / {c.FuelTop}</color></size>");
            r5FuelCount.color = full ? V5TitleKit.Teal : R5FuelInk;
            r5FuelCount.fontSize = V5StoryKit.FontSize(26f);
            float countW = B5W(r5FuelCount, r5FuelCount.text);
            B5Rect(r5FuelCount.rectTransform, right - countW - 4f, fuelMiddle - 18f, countW + 4f, 36f);
            // The last step's words end before the count.
            var fourth = r5Steps[3].words;
            B5Rect(fourth.rectTransform, textLeft, fuelMiddle - 18f, Mathf.Max(60f, right - countW - 16f - textLeft), 36f);
            V5TitleKit.FitWidth(fourth, V5StoryKit.FontSize(24f), V5StoryKit.FontSize(15f));
            float barTop = lastMiddle + R5StepH * .5f + 12f;
            B5Rect(r5FuelBar.Rect, textLeft, barTop, w - R5Pad - textLeft, 18f);
            r5FuelBar.Set(c.FuelTop > 0 ? c.Fuel / (float)c.FuelTop : 0f, 0f);

            // The ignition: the big button, orange once the four steps are done, and its line under it.
            float noteTop = h - 22f - 20f, fireTop = noteTop - 12f - R5FireH;
            bool ready = c.CanIgnite;
            r5Fire.SetLook(ready ? V5Plate.Look.Go : V5Plate.Look.Plate);
            StoryText(r5FireLabel, ArenaText.Pair("点 火 回 家", "Ignite and go home"));
            B5Place(r5Fire, R5Pad, fireTop, w - R5Pad * 2f, R5FireH);
            B5Live(r5Fire, r5FireLabel, ready, art ? V5TitleKit.Dark : Color.white);
            V5TitleKit.FitWidth(r5FireLabel, V5StoryKit.FontSize(28f), V5StoryKit.FontSize(18f));
            B5AddFocus("c.ignite", r5Fire);
            StoryText(r5FireNote, ArenaText.Pair("四步做完才能点火，点火后有一场决战", "All four steps must be done to ignite; a final battle follows"));
            B5Rect(r5FireNote.rectTransform, R5Pad, noteTop, w - R5Pad * 2f, 22f);
            V5TitleKit.FitWidth(r5FireNote, V5StoryKit.FontSize(18f), V5StoryKit.FontSize(13f));

            bool Done(int k) => k < parts.Count && parts[k].done;
        }

        /// <summary>The mining lamps: the lamp's medal, "lamps ×n", what they are for, and the price over the button that makes one.</summary>
        void LayLamps5(V2Campaign c, bool art)
        {
            float w = R5RightW, h = B5Floor - R5PartsTop;
            B5Rect(r5Lamp, R5RightX, R5PartsTop, w, h);
            var set = c.art;
            r5LampMedal.SetIcons(set ? set.lamp : null, V5StoryKit.LampIcon);
            PlaceCentre(r5LampMedal.Rect, new Vector2(0f, 1f), new Vector2(Bp(30f + 52f), -Bp(h * .5f)), Vector2.one * Bp(104f));
            // The price over the button, in a column at the right.
            StoryText(r5LampVerb, ArenaText.Pair("造一盏", "Make one"));
            r5LampVerb.fontSize = V5StoryKit.FontSize(21f);
            float bw = Mathf.Max(132f, B5W(r5LampVerb, r5LampVerb.text) + 44f), columnMiddle = w - 30f - bw * .5f;
            var price = c.LampPrice;
            bool live = c.CanAfford(price) && c.LampsCraftable;
            B5Place(r5LampButton, columnMiddle - bw * .5f, h * .5f + 2f, bw, 54f);
            B5Live(r5LampButton, r5LampVerb, live, V5TitleKit.Ink);
            B5AddFocus("c.lamp", r5LampButton);
            int n = B5PriceOf(price);
            float cw = B5CoinsWidth(r5LampCoins, n, 32f, art);
            B5LayCoins(r5LampCoins, n, columnMiddle - cw * .5f, h * .5f - 30f, 32f, art, false);
            float textLeft = 30f + 104f + 24f, textW = columnMiddle - bw * .5f - 24f - textLeft;
            StoryText(r5LampTitle, ArenaText.Pair("矿灯", "Lamps") + $"  <size={V5StoryKit.FontSize(32f)}><color=#{HexOf(V5TitleKit.Gold)}>×{c.LampStock}</color></size>");
            B5Rect(r5LampTitle.rectTransform, textLeft, h * .5f - 46f, textW, 44f);
            V5TitleKit.FitWidth(r5LampTitle, V5StoryKit.FontSize(28f), V5StoryKit.FontSize(18f));
            StoryText(r5LampLine, ArenaText.Pair("夜里采暗物质要用", "For mining dark matter at night"));
            B5Rect(r5LampLine.rectTransform, textLeft, h * .5f + 6f, textW, 30f);
            V5TitleKit.FitWidth(r5LampLine, V5StoryKit.FontSize(19f), V5StoryKit.FontSize(13f));
        }

        // ───── Where the base tour points ─────

        /// <summary>The button at the top right that opens the rocket page, while the workshop shows it (the base tour's third step and
        /// Day 1's guide).</summary>
        RectTransform B5RocketButton => BaseScreenShown == V2Campaign.BaseScreen.Workshop && b5Switch && b5Switch.gameObject.activeInHierarchy ? b5Switch.Rect : null;

        /// <summary>The main way out along the foot (on Day 0's arrival the only one: out into the night), on either page.</summary>
        RectTransform B5GoWay
        {
            get
            {
                if (BaseScreenShown == null) return null;
                foreach (var way in b5Ways)
                    if (way.go && way.act != null && way.plate && way.plate.gameObject.activeInHierarchy) return way.plate.Rect;
                return null;
            }
        }
    }
}
