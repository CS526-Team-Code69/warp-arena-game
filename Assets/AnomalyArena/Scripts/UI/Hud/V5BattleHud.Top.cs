using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>The top of the battle HUD: the vitals in both modes; in the open mode the wave plate at the top centre (in the story day
    /// plate's family: a dial with the wave where the story shows the day, the wave's state and clock and the foes, then the level, the
    /// experience bar) and the wallet at the top right (the energy once it is unlocked, and the foes killed).</summary>
    public sealed partial class V5BattleHud
    {
        // ───── Vitals (mock-up .vitals: left 24, top 18) ─────

        const float VitalsLeft = 24f, VitalsTop = 18f, FaceSize = 92f, FacePicture = .58f, BarsGap = 10f, BarsW = 300f, BarsH = 76f;
        const float BarsPadLeft = 18f, BarsPadRight = 16f, LabelColumn = 32f, ColumnGap = 10f, ValueColumn = 72f, LifeRow = 22.5f, StaminaRow = 53f;
        // The skills' energy as a third row (the story's: the open mode shows it in its wallet): the plate grows to BarsHWithEnergy and
        // stays centred on the portrait, so its foot ends 113 down (84.75 canvas units), above the story's goal plate (about 93).
        const float EnergyRow = 77f, EnergyRowH = 20f, BarsHWithEnergy = 98f;
        // The lighter chunk of life just lost stays a moment, then runs down to the life left.
        const float LostHold = .35f, LostSpeed = 1.2f;

        RectTransform vitals;
        Image facePicture, portrait;
        V5HudPlate barsPlate;
        Text lifeWord, staminaWord, lifeValue;
        V5BattleBar lifeBar, staminaBar;
        V5HudKey sprintKey;
        float lifeShown = -1f, lostFrom, lostWait;
        Text energyWord, energyValue;
        V5BattleBar energyBar;
        bool energyRow;
        // The numbers as last written (written and fitted again only when they change).
        int lifeWhole = int.MinValue, lifeMost, energyWhole = int.MinValue, energyMost;
        Font lifeFont, energyFont;

        void BuildVitals()
        {
            vitals = V5TitleKit.Rect(root, "V5Vitals");
            V5BattleKit.Place(vitals, VitalsLeft, VitalsTop, FaceSize + BarsGap + BarsW, FaceSize);
            var face = V5TitleKit.Rect(vitals, "Face");
            V5BattleKit.Place(face, 0f, 0f, FaceSize, FaceSize);
            facePicture = V5TitleKit.Picture(face, "Picture", V5BattleKit.FaceBack, Color.white);
            V5TitleKit.Centre(facePicture.rectTransform, Vector2.one * FaceSize * FacePicture * Css);
            facePicture.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            portrait = V5TitleKit.Picture(facePicture.transform, "Portrait", null, Color.white);
            portrait.preserveAspect = true;
            // The ring's picture has a dark face of its own: it goes behind the portrait, as in the mock-up.
            V5HudKit.Ring(face, "Ring", FaceSize * Css).transform.SetAsFirstSibling();
            barsPlate = V5HudKit.Plate(vitals, "Bars", V5HudKit.PlateLook.Plain, 10f, Css);
            V5BattleKit.Place(barsPlate.Rect, FaceSize + BarsGap, (FaceSize - BarsH) * .5f, BarsW, BarsH);
            var plate = barsPlate.transform;
            lifeWord = V5BattleKit.Label(plate, "LifeWord", 14f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            staminaWord = V5BattleKit.Label(plate, "StaminaWord", 14f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            lifeBar = V5BattleBar.Create(plate, "Life", V5BattleKit.LifeTop, V5BattleKit.LifeFoot, true, true, true);
            staminaBar = V5BattleBar.Create(plate, "Stamina", V5BattleKit.StaminaTop, V5BattleKit.StaminaFoot, true);
            lifeValue = V5BattleKit.Label(plate, "LifeValue", 21f, V5TitleKit.Ink, TextAnchor.MiddleRight, true);
            sprintKey = V5HudKit.Key(plate, "SprintKey", ArenaKeys.SprintKey, true, Css, 22f, 12f);
            // The third row: the skills' energy in the stamina bar's style, violet (the energy research's colour), its number on the right
            // like the life's. Hidden until the story has it (RenderVitals).
            energyWord = V5BattleKit.Label(plate, "EnergyWord", 14f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            energyBar = V5BattleBar.Create(plate, "Energy", V5HudKit.VioletHi, V5HudKit.Violet, true);
            energyValue = V5BattleKit.Label(plate, "EnergyValue", 16f, V5HudKit.VioletHi, TextAnchor.MiddleRight, true);
            ShowEnergyRow(false);
        }

        void ShowEnergyRow(bool shown)
        {
            if (energyWord.gameObject.activeSelf != shown) energyWord.gameObject.SetActive(shown);
            if (energyBar.gameObject.activeSelf != shown) energyBar.gameObject.SetActive(shown);
            if (energyValue.gameObject.activeSelf != shown) energyValue.gameObject.SetActive(shown);
        }

        /// <summary>The portrait inside the gauge ring: the helmet with the art on (a little larger than its circle and lowered, as the
        /// mock-up crops it); in whitebox the player's own flat mark (WhiteboxIcons.Armor: the player's colour with the visor slit).</summary>
        void DressPortrait(bool art)
        {
            if (!portrait) return;
            float circle = FaceSize * FacePicture * Css;
            if (art)
            {
                portrait.sprite = V5TitleKit.Piece("portrait");
                V5TitleKit.Centre(portrait.rectTransform, Vector2.one * circle * 1.2f);
                portrait.rectTransform.anchoredPosition = new Vector2(0f, -.14f * circle);
            }
            else
            {
                portrait.sprite = WhiteboxIcons.Armor();
                V5TitleKit.Centre(portrait.rectTransform, Vector2.one * circle * .72f);
            }
            facePicture.color = art ? Color.white : WhiteboxSkin.PlateFill;
            facePicture.sprite = V5BattleKit.FaceBack;
        }

        /// <summary>The bars' rows: the words' column as wide as the widest word in the language showing (the mock-up's 32 holds the
        /// Chinese ones; the energy's word counts even while hidden, so the bars do not move when it shows), the life bar up to its
        /// numbers, the stamina bar up to the sprint key's cap, and in the story the energy bar up to its numbers. The plate is as tall
        /// as its rows, centred on the portrait.</summary>
        void LayVitals()
        {
            if (!barsPlate) return;
            V5BattleKit.Sync(lifeWord);
            V5BattleKit.Sync(staminaWord);
            V5BattleKit.Sync(energyWord);
            lifeWord.text = ArenaText.Pair("生命", "HP");
            staminaWord.text = ArenaText.Pair("体力", "Stamina");
            energyWord.text = ArenaText.Pair("能量", "ENERGY");
            sprintKey.SetText(ArenaKeys.SprintKey);
            float barsH = energyRow ? BarsHWithEnergy : BarsH;
            V5BattleKit.Place(barsPlate.Rect, FaceSize + BarsGap, (FaceSize - barsH) * .5f, BarsW, barsH);
            float words = Mathf.Max(V5BattleKit.Width(lifeWord), Mathf.Max(V5BattleKit.Width(staminaWord), V5BattleKit.Width(energyWord)));
            float column = Mathf.Max(LabelColumn, words / Css + 2f);
            float barLeft = BarsPadLeft + column + ColumnGap;
            Row(lifeWord.rectTransform, BarsPadLeft, LifeRow, column, 21f);
            V5BattleKit.Place(lifeBar.Rect, barLeft, LifeRow - 8f, BarsW - BarsPadRight - ValueColumn - ColumnGap - barLeft, 16f);
            Row(lifeValue.rectTransform, BarsW - BarsPadRight - ValueColumn, LifeRow, ValueColumn, 21f);
            float keyWidth = sprintKey.Rect.sizeDelta.x / Css;
            var key = sprintKey.Rect;
            key.anchorMin = key.anchorMax = key.pivot = new Vector2(0f, 1f);
            key.anchoredPosition = new Vector2((BarsW - BarsPadRight - keyWidth) * Css, -(StaminaRow - 11f) * Css);
            Row(staminaWord.rectTransform, BarsPadLeft, StaminaRow, column, 22f);
            V5BattleKit.Place(staminaBar.Rect, barLeft, StaminaRow - 3.5f, BarsW - BarsPadRight - keyWidth - ColumnGap - barLeft, 7f);
            Row(energyWord.rectTransform, BarsPadLeft, EnergyRow, column, EnergyRowH);
            V5BattleKit.Place(energyBar.Rect, barLeft, EnergyRow - 3.5f, BarsW - BarsPadRight - ValueColumn - ColumnGap - barLeft, 7f);
            Row(energyValue.rectTransform, BarsW - BarsPadRight - ValueColumn, EnergyRow, ValueColumn, EnergyRowH);
            // The numbers fit their boxes again at the next draw.
            lifeWhole = energyWhole = int.MinValue;
        }

        static void Row(RectTransform rect, float left, float centre, float width, float height) =>
            V5BattleKit.Place(rect, left, centre - height * .5f, width, height);

        // The same box with its pivot in its middle: a number that pulses grows from its middle (V5BattleHud.Pulse).
        static void RowCentred(RectTransform rect, float left, float centre, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2((left + width * .5f) * Css, -centre * Css);
            rect.sizeDelta = new Vector2(width, height) * Css;
        }

        void RenderVitals(ArenaUiSnapshot s, bool relabel)
        {
            if (!barsPlate) return;
            // The skills' energy shows as the third row while the story runs (the story's wallet holds the campaign's own currencies); the
            // open mode's wallet shows it there.
            bool energy = s.hideXp && s.energyUnlocked;
            if (energy != energyRow)
            {
                energyRow = energy;
                ShowEnergyRow(energy);
                relabel = true;
            }
            if (relabel) LayVitals();
            float max = Mathf.Max(1f, s.maxHp), hp = Mathf.Clamp(s.hp, 0f, max), life = hp / max;
            if (lifeShown < 0f) lostFrom = life;
            else if (life < lifeShown - .0005f)
            {
                lostFrom = Mathf.Max(lostFrom, lifeShown);
                lostWait = LostHold;
            }
            lifeShown = life;
            if (lostFrom < life) lostFrom = life;
            lifeBar.SetValue(life);
            lifeBar.SetLost(lostFrom);
            // The shield's bar is full at 60 % of max life, as the saved HUD's was (the level-5 Bulwark's shield).
            lifeBar.SetBand(s.maxHp > 0f ? s.shield / (s.maxHp * .6f) : 0f);
            V5BattleKit.Sync(lifeValue);
            int whole = Mathf.CeilToInt(hp), most = Mathf.CeilToInt(s.maxHp);
            if (whole != lifeWhole || most != lifeMost || lifeValue.font != lifeFont)
            {
                lifeWhole = whole;
                lifeMost = most;
                lifeFont = lifeValue.font;
                lifeValue.text = $"{whole}<size={V5BattleKit.Font(13f)}><color=#{V5BattleKit.Hex(V5TitleKit.Ink3)}> / {most}</color></size>";
                V5TitleKit.FitWidth(lifeValue, V5BattleKit.Font(21f), V5BattleKit.Font(13f));
            }
            if (energyRow) RenderEnergy(s);
            staminaBar.SetValue(s.stamina);
            staminaBar.SetColours(s.staminaExhausted ? V5BattleKit.Tired : V5BattleKit.StaminaTop, s.staminaExhausted ? Color.Lerp(V5BattleKit.Tired, Color.black, .2f) : V5BattleKit.StaminaFoot);
        }

        /// <summary>The energy row: the bar, and "30 / 100" with the most smaller and dimmer, written and fitted only when it changes.</summary>
        void RenderEnergy(ArenaUiSnapshot s)
        {
            energyBar.SetValue(s.maxEnergy > 0f ? s.energy / s.maxEnergy : 0f);
            V5BattleKit.Sync(energyValue);
            int now = Mathf.CeilToInt(Mathf.Max(0f, s.energy)), most = Mathf.CeilToInt(s.maxEnergy);
            if (now == energyWhole && most == energyMost && energyValue.font == energyFont) return;
            energyWhole = now;
            energyMost = most;
            energyFont = energyValue.font;
            energyValue.text = $"{now}<size={V5BattleKit.Font(12f)}><color=#{V5BattleKit.Hex(V5TitleKit.Ink3)}> / {most}</color></size>";
            V5TitleKit.FitWidth(energyValue, V5BattleKit.Font(16f), V5BattleKit.Font(11f));
        }

        void TickLife(float dt)
        {
            if (!lifeBar || lifeShown < 0f || lostFrom <= lifeShown) return;
            if (lostWait > 0f) { lostWait -= dt; return; }
            lostFrom = Mathf.MoveTowards(lostFrom, lifeShown, dt * LostSpeed);
            lifeBar.SetLost(lostFrom);
        }

        // ───── The open mode's wave plate (the story day plate's place and family: top centre, 12 down) ─────

        const float WaveTop = 12f, DialSize = 74f, DialArc = 62f, InfoW = 372f, WavePadLeft = 10f, WavePadRight = 18f, WavePadY = 9f, DialGap = 14f;
        const float HeadRow = 14f, BaseRow = 50f, InfoH = 63f;

        RectTransform wave, info, picks;
        Image waveArc, foeMark, picksLine;
        Text dialNumber, dialWord, phase, clock, foes, level, xpNumbers, picksWord, picksCount;
        V5BattleBar xpBar;
        V5HudKey picksKey;
        Text[] waveLabels;
        int shownWave = -1, shownLevel = -1, shownPicks = -1, arcWave = -1;
        float arcTotal = 1f, buildTotal = 1f;

        float WaveW => WavePadLeft + DialSize + DialGap + InfoW + WavePadRight;

        void BuildWave()
        {
            wave = V5TitleKit.Rect(root, "V5WavePlate");
            V5BattleKit.Anchor(wave, new Vector2(.5f, 1f), new Vector2(0f, -WaveTop * Css), new Vector2(WaveW, DialSize + 2f * WavePadY) * Css);
            var plate = V5HudKit.Plate(wave, "Plate", V5HudKit.PlateLook.Plain, 11f, Css);
            V5TitleKit.Stretch(plate.Rect);
            var dial = V5TitleKit.Rect(wave, "Dial");
            V5BattleKit.Place(dial, WavePadLeft, WavePadY, DialSize, DialSize);
            V5HudKit.Ring(dial, "Ring", DialSize * Css);
            waveArc = V5HudKit.Arc(dial, "Arc", DialArc * Css, V5TitleKit.Gold);
            dialNumber = V5BattleKit.Label(dial, "Number", 22f, Color.white, TextAnchor.MiddleCenter, true);
            RowCentred(dialNumber.rectTransform, 12f, 32f, DialSize - 24f, 22f);
            dialWord = V5BattleKit.Label(dial, "Word", 9f, V5TitleKit.Ink2, TextAnchor.MiddleCenter, true);
            V5BattleKit.Place(dialWord.rectTransform, 8f, 44f, DialSize - 16f, 10f);
            dialWord.text = V5TitleKit.Spaced("WAVE");

            info = V5TitleKit.Rect(wave, "Info");
            V5BattleKit.Place(info, WavePadLeft + DialSize + DialGap, WavePadY + (DialSize - InfoH) * .5f, InfoW, InfoH);
            phase = V5BattleKit.Label(info, "Phase", 21f, V5TitleKit.Gold, TextAnchor.MiddleLeft, true);
            clock = V5BattleKit.Label(info, "Clock", 29f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            foes = V5BattleKit.Label(info, "Foes", 13f, V5TitleKit.Ink2, TextAnchor.MiddleRight);
            foeMark = V5TitleKit.Picture(info, "FoeMark", V5HudKit.Triangle, V5TitleKit.Crimson);
            level = V5BattleKit.Label(info, "Level", 13f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            xpBar = V5BattleBar.Create(info, "Experience", V5BattleKit.XpTop, V5BattleKit.XpFoot, true);
            xpNumbers = V5BattleKit.Label(info, "Numbers", 15f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            picks = V5TitleKit.Rect(info, "Picks");
            picksLine = V5TitleKit.Picture(picks, "Hairline", WhiteboxSkin.Square, V5HudKit.Hairline);
            picksWord = V5BattleKit.Label(picks, "Word", 13f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            picksCount = V5BattleKit.Label(picks, "Count", 16f, V5TitleKit.OrangeHi, TextAnchor.MiddleLeft, true);
            picksKey = V5HudKit.Key(picks, "Key", ArenaKeys.CardPickKey, true, Css, 20f, 11f);
            waveLabels = new[] { dialNumber, dialWord, phase, clock, foes, level, xpNumbers, picksWord, picksCount };
            wave.gameObject.SetActive(false);
        }

        void RenderWave(ArenaUiSnapshot s, bool show)
        {
            if (!wave) return;
            if (wave.gameObject.activeSelf != show) wave.gameObject.SetActive(show);
            if (!show) return;
            foreach (var label in waveLabels) V5BattleKit.Sync(label);

            string number = $"{s.wave}/{s.maxWaves}";
            if (dialNumber.text != number)
            {
                if (shownWave >= 0 && s.wave != shownWave) Pulse(dialNumber);
                dialNumber.text = number;
                V5TitleKit.FitWidth(dialNumber, V5BattleKit.Font(22f), V5BattleKit.Font(12f));
            }
            shownWave = s.wave;

            // The wave's state in the phase's place, its clock, and the foes alive and still to come.
            bool boss = s.state is ArenaUiState.FinalBoss or ArenaUiState.VoidTrial;
            bool build = s.buildWalking;
            bool cleared = !boss && s.waveTimeExpired;
            string state;
            Color stateColour = V5TitleKit.Gold, arcColour = V5TitleKit.Gold;
            float arc, seconds = -1f;
            if (s.state == ArenaUiState.FinalBoss)
            {
                state = ArenaText.Pair("首领战", "BOSS");
                stateColour = arcColour = V5HudKit.LifeText;
                arc = s.bossMaxHp > 0f ? s.bossHp / s.bossMaxHp : 0f;
            }
            else if (s.state == ArenaUiState.VoidTrial)
            {
                state = ArenaText.Pair("虚空试炼", "VOID TRIAL");
                stateColour = arcColour = V5HudKit.VioletHi;
                arc = s.bossMaxHp > 0f ? s.bossHp / s.bossMaxHp : 0f;
            }
            else if (build)
            {
                state = ArenaText.Pair("整备", "BUILD");
                if (s.buildTime > buildTotal + .5f || s.buildTime <= 0f) buildTotal = Mathf.Max(1f, s.buildTime);
                arc = s.selectionUnlimited ? 1f : s.buildTime / buildTotal;
                if (!s.selectionUnlimited) seconds = s.buildTime;
            }
            else if (s.state == ArenaUiState.SupplyDrop)
            {
                state = ArenaText.Pair("补给", "SUPPLY");
                arc = 0f;
            }
            else if (cleared)
            {
                state = ArenaText.Pair("清场", "CLEAR");
                stateColour = arcColour = V5TitleKit.OrangeHi;
                arc = 0f;
            }
            else
            {
                state = s.state == ArenaUiState.WaveIntro ? ArenaText.Pair("来袭", "INCOMING") : ArenaText.Pair("交战", "FIGHT");
                if (s.wave != arcWave || s.waveTime > arcTotal + .5f)
                {
                    arcWave = s.wave;
                    arcTotal = Mathf.Max(1f, s.waveTime);
                }
                arc = s.waveTime / arcTotal;
                seconds = s.waveTime;
            }
            phase.text = state;
            phase.color = stateColour;
            waveArc.color = arcColour;
            waveArc.fillAmount = Mathf.Clamp01(arc);
            bool timed = seconds >= 0f;
            if (clock.gameObject.activeSelf != timed) clock.gameObject.SetActive(timed);
            if (timed)
            {
                int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
                clock.text = $"{whole / 60:00}:{whole % 60:00}";
            }
            bool counting = s.state is ArenaUiState.WaveActive or ArenaUiState.WaveIntro or ArenaUiState.LevelUp || boss;
            string small = $"<size={V5BattleKit.Font(16f)}><color=#{V5BattleKit.Hex(V5TitleKit.Ink)}>";
            foes.text = !counting ? ""
                : cleared || boss ? $"{ArenaText.Pair("活敌", "Alive")} {small}{s.waveLiving}</color></size>"
                : $"{ArenaText.Pair("活敌", "Alive")} {small}{s.waveLiving}</color></size>    {ArenaText.Pair("待生成", "Pending")} {small}{s.wavePending}</color></size>";

            float phaseWidth = V5BattleKit.Width(phase) / Css;
            Row(phase.rectTransform, 0f, HeadRow, phaseWidth + 2f, 28f);
            float clockLeft = phaseWidth + 12f;
            Row(clock.rectTransform, clockLeft, HeadRow, 90f, 30f);
            float foesWidth = V5BattleKit.Width(foes) / Css;
            Row(foes.rectTransform, InfoW - foesWidth - 2f, HeadRow, foesWidth + 2f, 28f);
            bool marked = foes.text.Length > 0;
            if (foeMark.gameObject.activeSelf != marked) foeMark.gameObject.SetActive(marked);
            if (marked) V5BattleKit.Place(foeMark.rectTransform, InfoW - foesWidth - 2f - 15f, HeadRow - 4.5f, 10f, 9f);

            // The level, the experience bar and its numbers. The card picks waiting are told once, by the hint plate at the top
            // (V5BattleHud.Hint), so their row here stays hidden.
            bool top = s.level >= s.maxLevel;
            string levelNumber = top ? ArenaText.Text("ui.level.max") : s.level.ToString();
            Color levelColour = s.voidLevel ? V5HudKit.VioletHi : V5TitleKit.Ink;
            level.text = $"{ArenaText.Pair("等级", "LV")} <size={V5BattleKit.Font(16f)}><color=#{V5BattleKit.Hex(levelColour)}><b>{levelNumber}</b></color></size>";
            if (shownLevel >= 0 && s.level > shownLevel) Pulse(level);
            shownLevel = s.level;
            xpBar.SetValue(top ? 1f : s.xpNeeded > 0f ? s.xp / s.xpNeeded : 0f);
            xpNumbers.text = top ? "MAX"
                : $"{Mathf.FloorToInt(s.xp)}<size={V5BattleKit.Font(12f)}><color=#{V5BattleKit.Hex(V5TitleKit.Ink3)}> / {Mathf.CeilToInt(s.xpNeeded)}</color></size>";
            bool waiting = false;
            if (picks.gameObject.activeSelf != waiting) picks.gameObject.SetActive(waiting);
            float right = InfoW;
            if (waiting)
            {
                picksWord.text = ArenaText.Pair("待选", "Picks");
                picksCount.text = "×" + s.bankedDrafts;
                if (shownPicks >= 0 && s.bankedDrafts > shownPicks) Pulse(picksCount);
                picksKey.SetText(ArenaKeys.CardPickKey);
                float wordWidth = V5BattleKit.Width(picksWord) / Css, countWidth = V5BattleKit.Width(picksCount) / Css, keyWidth = picksKey.Rect.sizeDelta.x / Css;
                float width = 1f + 12f + wordWidth + 4f + countWidth + 6f + keyWidth;
                V5BattleKit.Place(picks, InfoW - width, BaseRow - 13f, width, 26f);
                V5BattleKit.Place(picksLine.rectTransform, 0f, 3f, 1f, 20f);
                Row(picksWord.rectTransform, 13f, 13f, wordWidth + 2f, 26f);
                RowCentred(picksCount.rectTransform, 13f + wordWidth + 4f, 13f, countWidth + 2f, 26f);
                var key = picksKey.Rect;
                key.anchorMin = key.anchorMax = key.pivot = new Vector2(1f, .5f);
                key.anchoredPosition = Vector2.zero;
                right -= width + 12f;
            }
            shownPicks = s.bankedDrafts;
            float levelWidth = V5BattleKit.Width(level) / Css;
            RowCentred(level.rectTransform, 0f, BaseRow, levelWidth + 2f, 26f);
            float numbersWidth = Mathf.Max(64f, V5BattleKit.Width(xpNumbers) / Css);
            Row(xpNumbers.rectTransform, right - numbersWidth, BaseRow, numbersWidth + 2f, 26f);
            float barLeft = levelWidth + 8f;
            V5BattleKit.Place(xpBar.Rect, barLeft, BaseRow - 4.5f, Mathf.Max(20f, right - numbersWidth - 8f - barLeft), 9f);
        }

        // ───── The open mode's wallet (the story wallet's place and style: top right) ─────

        const float WalletRight = 24f, WalletTop = 18f, WalletMedal = 42f, WalletPadLeft = 10f, WalletPadRight = 14f, WalletPadY = 8f, WalletItemGap = 4f;

        RectTransform wallet;
        V5HudMedal energyMedal, killsMedal;
        Text energyNumber, killsNumber;

        void BuildWallet()
        {
            wallet = V5TitleKit.Rect(root, "V5Wallet");
            V5BattleKit.Anchor(wallet, Vector2.one, new Vector2(-WalletRight * Css, -WalletTop * Css), new Vector2(200f, WalletMedal + 2f * WalletPadY) * Css);
            var plate = V5HudKit.Plate(wallet, "Plate", V5HudKit.PlateLook.Plain, 10f, Css);
            V5TitleKit.Stretch(plate.Rect);
            energyMedal = V5HudKit.Medal(wallet, "EnergyMedal", WalletMedal * Css, null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            energyNumber = V5BattleKit.Label(wallet, "Energy", 23f, V5HudKit.VioletHi, TextAnchor.MiddleLeft, true);
            killsMedal = V5HudKit.Medal(wallet, "KillsMedal", WalletMedal * Css, V5BattleKit.FoeMark, V5BattleKit.FoeMark);
            killsMedal.SetRing(V5TitleKit.Crimson);
            killsNumber = V5BattleKit.Label(wallet, "Kills", 23f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            wallet.gameObject.SetActive(false);
        }

        void RenderWallet(ArenaUiSnapshot s, bool show)
        {
            if (!wallet) return;
            if (wallet.gameObject.activeSelf != show) wallet.gameObject.SetActive(show);
            if (!show) return;
            V5BattleKit.Sync(energyNumber);
            V5BattleKit.Sync(killsNumber);
            var icons = GameManager.Instance && GameManager.Instance.TryGetComponent<V2Campaign>(out var campaign) ? campaign.art : null;
            energyMedal.SetIcons(icons ? icons.energy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            // Void energy (after the void trial) rings its medal in violet.
            energyMedal.SetRing(s.voidEnergy ? V5HudKit.Violet : Color.clear);
            bool energy = s.energyUnlocked;
            if (energyMedal.gameObject.activeSelf != energy) energyMedal.gameObject.SetActive(energy);
            if (energyNumber.gameObject.activeSelf != energy) energyNumber.gameObject.SetActive(energy);
            energyNumber.text = $"{Mathf.CeilToInt(s.energy)}<size={V5BattleKit.Font(13f)}><color=#{V5BattleKit.Hex(V5TitleKit.Ink3)}> / {Mathf.CeilToInt(s.maxEnergy)}</color></size>";
            killsNumber.text = s.kills.ToString();
            float x = WalletPadLeft;
            if (energy) x = WalletItem(energyMedal, energyNumber, x);
            x = WalletItem(killsMedal, killsNumber, x);
            wallet.sizeDelta = new Vector2(x - WalletItemGap + WalletPadRight, WalletMedal + 2f * WalletPadY) * Css;
        }

        // A medal and its number from x (mock-up pixels); where the next item starts.
        static float WalletItem(V5HudMedal medal, Text number, float x)
        {
            var rect = medal.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(x * Css, 0f);
            float width = V5BattleKit.Width(number) / Css;
            var label = number.rectTransform;
            label.anchorMin = label.anchorMax = label.pivot = new Vector2(0f, .5f);
            label.anchoredPosition = new Vector2((x + WalletMedal + 7f) * Css, 0f);
            label.sizeDelta = new Vector2(width + 2f, 30f) * Css;
            return x + WalletMedal + 7f + width + 8f + WalletItemGap;
        }
    }
}
