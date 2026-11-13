using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The day plate at the top centre of the story layer (the mock-up's .dayline). A dial in the gauge ring: the day of the planned days
    /// over "DAY", and round it an arc filled by the time of day, gold by day and moonlight blue at night. Beside it the head row: the
    /// phase in the same colour, the clock (the morning wave's time left; the afternoon's and the night's only where evenings have a
    /// clock), and on the right the foes alive and still to come, or at night the way to dawn. Under it the base row: the control room's
    /// medal (its ring orange while a room of the base is broken), the base's life in a segmented teal bar with its number (a shield, when
    /// the base has one, along the bar's upper part and after the number), and after a hairline the ship's engine, armour and fuel with
    /// their marks. It replaces the old base bar and shows what the HUD's wave clock showed for the story. On Day 0's walk (the base is
    /// not reached yet) and in the last fight (the ship is ready and the boss's bar has the reading) the plate keeps the dial and the head
    /// row, which then stands in the middle of the plate.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The plate in mock-up pixels: 12 from the top, padding 9 / 18 / 9 / 10, corners cut at 11; the dial 74 across, its arc at 40 % of
        // it; 14 to the information column, 372 wide: the head row (28 tall) and 9 under it the base row (26 tall), the pair centred
        // against the dial; 24-pixel medals in the base row.
        const float DayTopPx = 12f, DayPadXPx = 10f, DayPadYPx = 9f, DayPadRightPx = 18f, DialPx = 74f, DayGapPx = 14f, InfoPx = 372f;
        const float HeadPx = 28f, BaseRowGapPx = 9f, BaseRowPx = 26f, RowMedalPx = 24f;
        // The arc is the time of day: the morning's wave fills the first third, the afternoon's light the next sixth, the night the rest.
        // Day 0's walk shows the middle of the morning. A night without a clock fills toward NightArcReach over the night's planned
        // length and stays there: dawn comes only when the player waits for it at the base. The last fight shows the whole ring. The arc
        // eases toward its value at ArcEase a second, and starts again from the dawn when the day turns.
        const float MorningArc = 1f / 3f, AfternoonArc = .5f, TutorialArc = 1f / 6f, NightArcReach = .92f, ArcEase = 5f;

        RectTransform storyDay, storyDayHead, storyDayBase;
        Image storyArc, storyFoesMark, storyHairline;
        Text storyDayNumber, storyDayCap, storyPhase, storyClock, storyUntil, storyFoes, storyBaseNumber;
        V5HudMedal storyControl;
        V5StoryBar storyBaseBar;
        readonly V5HudMedal[] storyShip = new V5HudMedal[3];
        readonly Text[] storyShipMarks = new Text[3];
        float storyArcShown = -1f, storyArcWanted, storyBaseNumberWidth;
        Color storyArcColour = V5TitleKit.Gold;
        int storyDayShown = -1;
        static readonly string[] ShipPartNames = { "Engine", "Armor", "Fuel" };
        // The foes the head row words (LayOutStoryHead), and the head row as last laid out: it is laid out again only when one changed.
        int storyFoesAlive, storyFoesPending, headAlive = -1, headPending = -1;
        bool storyFoesShown, storyFoesExpired, headFoes, headExpired, headClockRight;
        bool? headChinese;
        string headPhase, headClock, headUntil;
        Font headFont;

        void BuildStoryDay()
        {
            storyDay = V5TitleKit.Rect(hud, "DayPlate");
            storyDay.anchorMin = storyDay.anchorMax = storyDay.pivot = new Vector2(.5f, 1f);
            storyDay.anchoredPosition = new Vector2(0f, -Sp(DayTopPx));
            storyDay.sizeDelta = new Vector2(Sp(DayPadXPx + DialPx + DayGapPx + InfoPx + DayPadRightPx), Sp(DayPadYPx * 2f + DialPx));
            V5TitleKit.Stretch(V5HudKit.Plate(storyDay, "Plate", V5HudKit.PlateLook.Plain, 11f, V5HudKit.StoryCss).Rect);

            var dial = V5TitleKit.Rect(storyDay, "Dial");
            V5TitleKit.Place(dial, Sp(DayPadXPx), Sp(DayPadYPx), Sp(DialPx), Sp(DialPx));
            V5HudKit.Ring(dial, "Ring", Sp(DialPx));
            // The ring line's circle runs at 61 / 64 of its picture's half width: this size puts the arc at 40 % of the dial.
            storyArc = V5HudKit.Arc(dial, "Arc", Sp(DialPx) * .8f * 64f / 61f, V5TitleKit.Gold);
            storyDayNumber = V5StoryKit.Label(dial, "Day", 22f, Color.white, TextAnchor.MiddleCenter, true);
            PlaceCentre(storyDayNumber.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, Sp(5f)), new Vector2(Sp(DialPx), Sp(26f)));
            storyDayCap = V5StoryKit.Label(dial, "Cap", 9f, V5TitleKit.Ink2, TextAnchor.MiddleCenter, true);
            PlaceCentre(storyDayCap.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, -Sp(13f)), new Vector2(Sp(DialPx), Sp(12f)));
            storyDayCap.text = V5TitleKit.Spaced("DAY");

            var info = V5TitleKit.Rect(storyDay, "Info");
            V5TitleKit.Place(info, Sp(DayPadXPx + DialPx + DayGapPx), Sp(DayPadYPx), Sp(InfoPx), Sp(DialPx));
            storyDayHead = StoryRow(info, "Head", HeadPx);
            storyPhase = V5StoryKit.Label(storyDayHead, "Phase", 21f, V5TitleKit.Gold, TextAnchor.MiddleCenter, true);
            storyClock = V5StoryKit.Label(storyDayHead, "Clock", 29f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            storyUntil = V5StoryKit.Label(storyDayHead, "Until", 13f, V5TitleKit.Ink2, TextAnchor.MiddleCenter);
            storyFoes = V5StoryKit.Label(storyDayHead, "Foes", 13f, V5TitleKit.Ink2, TextAnchor.MiddleCenter);
            storyFoesMark = V5HudKit.Arrow(storyDayHead, "FoesMark", new Vector2(Sp(10f), Sp(9f)), V5TitleKit.Crimson);
            storyDayBase = StoryRow(info, "Base", BaseRowPx);
            storyControl = V5HudKit.Medal(storyDayBase, "Control", Sp(RowMedalPx));
            storyControl.SetRing(V5TitleKit.Teal);
            storyBaseBar = V5StoryBar.Create(storyDayBase, "Bar", V5StoryKit.BaseTop, V5StoryKit.BaseBottom, V5TitleKit.Steel, 10);
            storyBaseNumber = V5StoryKit.Label(storyDayBase, "Number", 15f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            storyHairline = V5TitleKit.Picture(storyDayBase, "Hairline", null, V5HudKit.Hairline);
            for (int i = 0; i < 3; i++)
            {
                storyShip[i] = V5HudKit.Medal(storyDayBase, ShipPartNames[i], Sp(RowMedalPx));
                storyShipMarks[i] = V5StoryKit.Label(storyDayBase, ShipPartNames[i] + "Mark", 14f, V5TitleKit.Teal, TextAnchor.MiddleCenter, true);
            }
        }

        /// <summary>A row of the information column, as wide as the column and heightPx mock-up pixels tall (placed by RefreshStoryDay).</summary>
        static RectTransform StoryRow(RectTransform parent, string name, float heightPx)
        {
            var row = V5TitleKit.Rect(parent, name);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(.5f, 1f);
            row.sizeDelta = new Vector2(0f, Sp(heightPx));
            return row;
        }

        void RefreshStoryDay()
        {
            var c = campaign;
            var rules = c.rules;
            var waves = gm ? gm.waves : null;
            var stage = c.Stage;
            bool night = stage == CampaignStage.Night, morning = stage is CampaignStage.Day or CampaignStage.Lull;
            bool last = stage is CampaignStage.FinalBoss or CampaignStage.Core;
            bool baseRow = !(last || stage == CampaignStage.Tutorial);
            Color accent = night ? V5TitleKit.Moon : V5TitleKit.Gold;

            // The dial: the day of the planned days, orange past them (overtime); a new day pulses once and starts its arc from the dawn.
            int days = rules ? rules.days : 0;
            string day = days > 0 ? $"{c.Day}/{days}" : c.Day.ToString();
            if (storyDayNumber.text != day)
            {
                if (storyDayShown >= 0 && storyDayShown != c.Day)
                {
                    PulseOnce(storyDayNumber.transform);
                    storyArcShown = -1f;
                }
                storyDayShown = c.Day;
                storyDayNumber.text = day;
            }
            storyDayNumber.color = c.Overtime ? V5TitleKit.OrangeHi : Color.white;
            storyArcWanted = ArcOfDay(c, waves);
            storyArcColour = accent;

            string phase = stage switch
            {
                CampaignStage.Tutorial => ArenaText.Pair("坠落之后", "AFTER THE FALL"),
                CampaignStage.Day or CampaignStage.Lull => ArenaText.Pair("上午", "MORNING"),
                CampaignStage.Dusk => ArenaText.Pair("下午", "AFTERNOON"),
                CampaignStage.Night => ArenaText.Pair("夜晚", "NIGHT"),
                CampaignStage.FinalBoss => ArenaText.Pair("最终决战", "FINAL BATTLE"),
                CampaignStage.Core => ArenaText.Pair("暗物质核心", "THE CORE"),
                _ => ""
            };
            if (morning && rules && rules.wavesPerDay > 1)
                phase += ArenaText.Pair($" · 敌袭 {c.WaveOfDay + 1}/{rules.wavesPerDay}", $" · ATTACK {c.WaveOfDay + 1}/{rules.wavesPerDay}");
            if (c.Overtime && !last) phase += ArenaText.Pair(" · 超时", " · OVERTIME");

            // The clock and what stands on the right of the head row.
            string clock = "", until = "";
            bool clockOnRight = false;
            Color clockColour = V5TitleKit.Ink;
            int alive = waves ? waves.LivingCount : 0, pending = waves ? waves.PendingCount : 0;
            bool expired = waves && waves.Phase == WavePhase.WaveActive && waves.TimeExpired;
            if (morning && waves)
            {
                if (expired)
                {
                    clock = ArenaText.Pair("清场中", "CLEAR");
                    clockColour = V5TitleKit.OrangeHi;
                }
                else
                {
                    // Before the wave starts its clock the dial shows the wave's whole length; its last five seconds turn orange.
                    float left = waves.Phase == WavePhase.WaveIntro ? waves.WaveSeconds : waves.TimeLeft;
                    clock = ClockText(left);
                    if (waves.Phase == WavePhase.WaveActive && left <= 5f) clockColour = V5TitleKit.OrangeHi;
                }
            }
            else if (stage == CampaignStage.Dusk && !c.EveningsOpenEnded) clock = ClockText(c.StageLength - c.StageClock);
            else if (night)
            {
                clockOnRight = true;
                if (c.EveningsOpenEnded) until = ArenaText.Pair("回基地门口等天亮", "Wait for dawn at the base door");
                else
                {
                    until = ArenaText.Pair("离天亮还有", "Dawn in");
                    clock = ClockText(c.StageLength - c.StageClock);
                }
            }
            // The foes are the wave's: at night the morning's are gone and the shadows are not counted. LayOutStoryHead words them at the
            // size that fits.
            storyFoesShown = !night && alive + pending > 0;
            storyFoesAlive = alive;
            storyFoesPending = pending;
            storyFoesExpired = expired;

            StoryText(storyPhase, phase);
            storyPhase.color = accent;
            StoryText(storyClock, clock);
            storyClock.color = clockColour;
            StoryText(storyUntil, until);
            LayOutStoryHead(clockOnRight);

            float pairTop = (DialPx - HeadPx - BaseRowGapPx - BaseRowPx) * .5f;
            storyDayHead.anchoredPosition = new Vector2(0f, -Sp(baseRow ? pairTop : (DialPx - HeadPx) * .5f));
            storyDayBase.anchoredPosition = new Vector2(0f, -Sp(pairTop + HeadPx + BaseRowGapPx));
            SetShown(storyDayBase, baseRow);
            if (baseRow) RefreshStoryBase(c);
        }

        /// <summary>The foes' words at a size, the counts larger (16 to 13 at the mock-up's size, in proportion at a smaller one), bold and
        /// in Ink; "待生成 n" only when asked for. Once the wave's time is up, only the foes left.</summary>
        string FoesWords(int size, bool pending, int alive, int waiting)
        {
            int count = Mathf.RoundToInt(size * (float)V5StoryKit.FontSize(16f) / V5StoryKit.FontSize(13f));
            if (storyFoesExpired) return ArenaText.Pair($"剩余活敌 {FoeCount(alive, count)}", $"{FoeCount(alive, count)} left");
            return pending
                ? ArenaText.Pair($"活敌 {FoeCount(alive, count)}   待生成 {FoeCount(waiting, count)}", $"Alive {FoeCount(alive, count)}   Pending {FoeCount(waiting, count)}")
                : ArenaText.Pair($"活敌 {FoeCount(alive, count)}", $"Alive {FoeCount(alive, count)}");
        }

        static string FoeCount(int count, int size) => $"<size={size}><b><color=#{HexOf(V5TitleKit.Ink)}>{count}</color></b></size>";

        static string ClockText(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        /// <summary>The head row from the left: the phase, 12 to the clock; on the right the foes after their crimson mark, or at night the
        /// way to dawn (and its clock at the far right). The foes keep at least the room of "活敌 88" at their full size: when the phase
        /// and the clock leave less (an English overtime morning: "MORNING · OVERTIME" and the clock take all but a few units), the phase
        /// is made smaller first, down to 9. Then, while the foes are wider than their room, "待生成 n" goes first and the words shrink,
        /// their counts with them, down to 8; the way to dawn only shrinks. Laid out again only when something in the row changed.</summary>
        void LayOutStoryHead(bool clockOnRight)
        {
            if (headChinese == ArenaText.IsChinese && headFont == storyPhase.font && headPhase == storyPhase.text && headClock == storyClock.text
                && headUntil == storyUntil.text && headFoes == storyFoesShown && headAlive == storyFoesAlive && headPending == storyFoesPending
                && headExpired == storyFoesExpired && headClockRight == clockOnRight) return;
            headChinese = ArenaText.IsChinese;
            headFont = storyPhase.font;
            headPhase = storyPhase.text;
            headClock = storyClock.text;
            headUntil = storyUntil.text;
            headFoes = storyFoesShown;
            headAlive = storyFoesAlive;
            headPending = storyFoesPending;
            headExpired = storyFoesExpired;
            headClockRight = clockOnRight;

            float width = Sp(InfoPx), height = Sp(HeadPx), x = 0f, right = width;
            bool phased = storyPhase.text.Length > 0, clocked = storyClock.text.Length > 0;
            bool until = storyUntil.text.Length > 0, foes = !until && storyFoesShown;
            SetShown(storyPhase, phased);
            SetShown(storyClock, clocked);
            int phaseLargest = V5StoryKit.FontSize(21f), largest = V5StoryKit.FontSize(13f);
            float mark = foes ? Sp(15f) : 0f;
            storyPhase.fontSize = phaseLargest;
            float phaseWidth = phased ? storyPhase.preferredWidth : 0f, clockWidth = clocked ? storyClock.preferredWidth : 0f;
            if (foes && phased)
            {
                storyFoes.fontSize = largest;
                float least = V5StoryKit.WidthOf(storyFoes, FoesWords(largest, false, storyFoesAlive < 100 ? 88 : 888, 0));
                float used = phaseWidth + (clocked && !clockOnRight ? Sp(12f) + clockWidth : 0f);
                float edge = right - (clocked && clockOnRight ? clockWidth + Sp(8f) : 0f);
                float spare = edge - used - Sp(12f) - mark - least;
                if (spare < 0f)
                {
                    PlaceSpan(storyPhase, 0f, Mathf.Max(1f, phaseWidth + spare), height);
                    V5TitleKit.FitWidth(storyPhase, phaseLargest, 9);
                    phaseWidth = storyPhase.preferredWidth;
                }
            }
            if (phased)
            {
                PlaceSpan(storyPhase, 0f, phaseWidth, height);
                x = phaseWidth;
            }
            if (clocked)
            {
                if (clockOnRight)
                {
                    right -= clockWidth;
                    PlaceSpan(storyClock, right, clockWidth, height);
                    right -= Sp(8f);
                }
                else
                {
                    x += phased ? Sp(12f) : 0f;
                    PlaceSpan(storyClock, x, clockWidth, height);
                    x += clockWidth;
                }
            }
            var words = until ? storyUntil : storyFoes;
            SetShown(until ? storyFoes : storyUntil, false);
            SetShown(words, until || foes);
            SetShown(storyFoesMark, foes);
            if (!until && !foes) return;
            float room = Mathf.Max(0f, right - x - Sp(12f) - mark);
            words.fontSize = largest;
            if (foes)
            {
                // The counts are sized with the words (FoesWords), so the whole line shrinks.
                bool pending = !storyFoesExpired;
                StoryText(storyFoes, FoesWords(largest, pending, storyFoesAlive, storyFoesPending));
                if (pending && storyFoes.preferredWidth > room)
                {
                    pending = false;
                    StoryText(storyFoes, FoesWords(largest, false, storyFoesAlive, storyFoesPending));
                }
                for (int size = largest; size > 8 && storyFoes.preferredWidth > room;)
                {
                    size--;
                    storyFoes.fontSize = size;
                    StoryText(storyFoes, FoesWords(size, pending, storyFoesAlive, storyFoesPending));
                }
            }
            else if (storyUntil.preferredWidth > room)
            {
                storyUntil.rectTransform.sizeDelta = new Vector2(room, height);
                V5TitleKit.FitWidth(storyUntil, largest, 8);
            }
            float wide = Mathf.Min(words.preferredWidth, room);
            PlaceSpan(words, right - wide, wide, height);
            if (foes)
                PlaceCentre(storyFoesMark.rectTransform, new Vector2(0f, .5f), new Vector2(right - wide - Sp(10f), 0f), new Vector2(Sp(10f), Sp(9f)));
        }

        void RefreshStoryBase(V2Campaign c)
        {
            var art = c.art;
            storyControl.SetIcons(art ? art.control : null, WhiteboxIcons.Room(CampaignRoom.Control));
            bool broken = false;
            foreach (var room in V2Campaign.SideRoom) broken |= c.Room(room).damaged;
            storyControl.SetRing(broken ? V5TitleKit.Orange : V5TitleKit.Teal);
            float max = Mathf.Max(1f, c.BaseMaxHp), hp = Mathf.Clamp(c.BaseHp, 0f, max), shield = c.ShieldLevel > 0 ? Mathf.Max(0f, c.BaseShield) : 0f;
            storyBaseBar.Set(hp / max, shield / max);
            StoryText(storyBaseNumber, BaseNumber(hp, max, shield));
            // As wide as the number at full life, so the bar does not shift while the base takes damage.
            storyBaseNumberWidth = V5StoryKit.WidthOf(storyBaseNumber, BaseNumber(max, max, shield));
            ShipPart(0, art ? art.engine : null, V5StoryKit.EngineIcon, c.EngineFixed);
            ShipPart(1, art ? art.armor : null, V5StoryKit.ArmorIcon, c.ArmorFixed);
            // The fuel counts its units: gold while it fills, teal once the tank is full.
            storyShip[2].SetIcons(art ? art.fuel : null, V5StoryKit.FuelIcon);
            storyShip[2].SetRing(c.FuelFull ? V5TitleKit.Teal : V5TitleKit.Gold);
            ShipMark(2, $"{c.Fuel}/{c.FuelUnits}", c.FuelFull ? V5TitleKit.Teal : V5TitleKit.Gold);
            LayOutStoryBase();
        }

        /// <summary>The base's life over its most ("1250 / 1500", the second part smaller and dimmer) and its shield after it in steel.</summary>
        static string BaseNumber(float hp, float max, float shield)
        {
            int small = V5StoryKit.FontSize(12f);
            string number = $"{Mathf.CeilToInt(hp)}<size={small}><color=#{HexOf(V5TitleKit.Ink3)}> / {Mathf.CeilToInt(max)}</color></size>";
            if (shield > 0f) number += $"<size={small}><color=#{HexOf(V5TitleKit.Steel)}> +{Mathf.CeilToInt(shield)}</color></size>";
            return number;
        }

        /// <summary>The engine or the armour: a teal ring and a tick once repaired, an orange ring and "!" until then.</summary>
        void ShipPart(int index, Sprite artIcon, Sprite flatIcon, bool fixedNow)
        {
            storyShip[index].SetIcons(artIcon, flatIcon);
            storyShip[index].SetRing(fixedNow ? V5TitleKit.Teal : V5TitleKit.Orange);
            ShipMark(index, fixedNow ? "✓" : "!", fixedNow ? V5TitleKit.Teal : V5TitleKit.OrangeHi);
        }

        void ShipMark(int index, string mark, Color ink)
        {
            var label = storyShipMarks[index];
            if (label.text != mark)
            {
                if (label.text.Length > 0) PulseOnce(label.transform);
                label.text = mark;
            }
            label.color = ink;
        }

        /// <summary>The base row from the right: fuel, armour and engine (each its medal, 4 to its mark, 9 between parts), a hairline 12
        /// before them, the number 9 before that; from the left the control room's medal and 8 after it the bar up to 8 before the number.</summary>
        void LayOutStoryBase()
        {
            float height = Sp(BaseRowPx), medal = Sp(RowMedalPx), right = Sp(InfoPx);
            for (int i = 2; i >= 0; i--)
            {
                var label = storyShipMarks[i];
                float w = label.preferredWidth;
                PlaceSpan(label, right - w, w, height);
                right -= w + Sp(4f);
                PlaceCentre(storyShip[i].Rect, new Vector2(0f, .5f), new Vector2(right - medal * .5f, 0f), Vector2.one * medal);
                right -= medal;
                if (i > 0) right -= Sp(9f);
            }
            right -= Sp(12f);
            PlaceCentre(storyHairline.rectTransform, new Vector2(0f, .5f), new Vector2(right, 0f), new Vector2(1f, height));
            right -= Sp(9f);
            float number = Mathf.Max(storyBaseNumber.preferredWidth, storyBaseNumberWidth);
            PlaceSpan(storyBaseNumber, right - number, number, height);
            right -= number + Sp(8f);
            PlaceCentre(storyControl.Rect, new Vector2(0f, .5f), new Vector2(medal * .5f, 0f), Vector2.one * medal);
            float barLeft = medal + Sp(8f);
            var bar = storyBaseBar.Rect;
            bar.anchorMin = bar.anchorMax = new Vector2(0f, .5f);
            bar.pivot = new Vector2(0f, .5f);
            bar.anchoredPosition = new Vector2(barLeft, 0f);
            bar.sizeDelta = new Vector2(Mathf.Max(Sp(24f), right - barLeft), Sp(9f));
        }

        float ArcOfDay(V2Campaign c, WaveManager waves)
        {
            var rules = c.rules;
            switch (c.Stage)
            {
                case CampaignStage.Tutorial:
                    return TutorialArc;
                case CampaignStage.Day:
                case CampaignStage.Lull:
                    if (!waves || waves.Phase == WavePhase.WaveIntro) return 0f;
                    if (waves.Phase != WavePhase.WaveActive || waves.TimeExpired) return MorningArc;
                    return MorningArc * Mathf.Clamp01(1f - waves.TimeLeft / Mathf.Max(1f, waves.WaveSeconds));
                case CampaignStage.Dusk:
                    return Mathf.Lerp(MorningArc, AfternoonArc, Mathf.Clamp01(c.StageClock / Mathf.Max(.1f, rules ? rules.duskSeconds : 15f)));
                case CampaignStage.Night:
                    if (!c.EveningsOpenEnded) return Mathf.Lerp(AfternoonArc, 1f, Mathf.Clamp01(c.StageClock / Mathf.Max(.1f, c.StageLength)));
                    return Mathf.Lerp(AfternoonArc, NightArcReach, Mathf.Clamp01(c.StageClock / Mathf.Max(1f, rules ? rules.NightLength(c.Day) : 40f)));
                case CampaignStage.FinalBoss:
                case CampaignStage.Core:
                    return 1f;
                default:
                    return storyArcWanted;
            }
        }

        void TickStoryDay(float dt)
        {
            if (!storyArc) return;
            if (storyArcShown < 0f) storyArcShown = storyArcWanted;
            float keep = Mathf.Exp(-ArcEase * dt);
            storyArcShown = Mathf.Lerp(storyArcWanted, storyArcShown, keep);
            storyArc.fillAmount = storyArcShown;
            storyArc.color = Color.Lerp(storyArcColour, storyArc.color, keep);
        }
    }
}
