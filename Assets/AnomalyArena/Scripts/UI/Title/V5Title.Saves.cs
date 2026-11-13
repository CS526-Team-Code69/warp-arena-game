using System;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    public sealed partial class V5Title
    {
        // ───── The saves: the continue window and the new game's two steps ─────
        // Continue: tabs for the two modes, each with its three slots; the mode of the most recent save opens first with it chosen. An empty
        // slot cannot be chosen; a won run shows its mark and cannot be continued. Delete asks for a second click.
        // New game: first the mode (two cards, names and icons only), then the slot; a filled slot is overwritten only after one more
        // question. The story starts at once: its slot's old save is deleted, the slot bound (ArenaSaves.Bind) and the run started the way
        // the saved Start does. The open mode opens its map pick first, and only a map that really starts the run deletes the old save and
        // binds the slot (MapChosen); backing out of the pick leaves the slot as it was.

        const float RowH = 33f, RowGap = 10.5f, SlotGap = 9f, ModeTabW = 84f, BackW = 84f, DeleteAskSeconds = 3f, ModeMedal = 81f;
        const int MaxPips = 16;
        static float SlotH => (BodyH - RowH - RowGap - 2f * SlotGap) / 3f;
        static readonly SaveMode[] Modes = { SaveMode.Story, SaveMode.Open };
        static readonly Color DividerColour = V5TitleKit.Rgb(163, 172, 190, .16f), PipOff = V5TitleKit.Rgb(36, 42, 58), MarkInk = V5TitleKit.Rgb(6, 32, 30);

        sealed class ChipView
        {
            public RectTransform root;
            public Image back;
            public Text text;
        }

        sealed class SlotView
        {
            public V5Plate plate;
            public float ra, rb, rc;
            public Text number, numberCap;
            public Image divider;
            public RectTransform full, empty;
            public Text headline;
            public ChipView phase, mark;
            public readonly Image[] pips = new Image[MaxPips];
            public readonly Image[] weaponPictures = new Image[4];
            public readonly Text[] weaponNames = new Text[4];
            public readonly MedalView[] parts = new MedalView[3];
            public readonly Text[] partTexts = new Text[3];
            public Image fuelTrack;
            public readonly Image[] fuelCells = new Image[10];
            public Image mapHex, mapPicture;
            public Text mapText;
            public Text emptyTitle, emptyLine;
        }

        readonly SaveSummary[][] saves = { new SaveSummary[ArenaSaves.SlotCount], new SaveSummary[ArenaSaves.SlotCount] };
        bool savesDirty = true, continueShown, continuePicked;

        V5Block continueModes;
        readonly V5Plate[] continueTabs = new V5Plate[2];
        readonly Text[] continueTabLabels = new Text[2];
        readonly SlotView[] continueSlots = new SlotView[ArenaSaves.SlotCount];
        int continueMode;
        readonly int[] continueChosen = { -1, -1 };
        float deleteAskedUntil = -1f;

        readonly V5Plate[] modeCards = new V5Plate[2];
        readonly MedalView[] modeMedals = new MedalView[2];
        readonly Text[] modeNames = new Text[2], modeCaps = new Text[2], modeLines = new Text[2];
        V5Block newGameTop;
        V5Plate backPlate;
        Text backLabel, newGameLine;
        Image backArrow;
        readonly SlotView[] newGameSlots = new SlotView[ArenaSaves.SlotCount];
        int newGameStep = 1, newGameMode;
        readonly int[] newGameChosen = { -1, -1 };
        bool overwriteAsked;
        // The open mode's slot waiting for a map (MapChosen), and whether it holds a save the new run overwrites.
        int pendingOpenSlot = -1;
        bool pendingOpenOverwrite;

#if UNITY_INCLUDE_TESTS
        /// <summary>Tests only: the summaries the save windows show, in place of ArenaSaves.List.</summary>
        public static Func<SaveMode, SaveSummary[]> SummariesForTests { get; set; }
        /// <summary>Tests only: receives the calls to ArenaSaves ("Bind", "Continue", "Delete") in its place.</summary>
        public static Action<string, SaveMode, int> SaveCallsForTests { get; set; }
        /// <summary>Tests only: reads the summaries again and opens the window a freshly shown title opens with (the most recent save
        /// chosen to continue, or the new game's first step).</summary>
        public static void ReloadForTests()
        {
            if (!active) return;
            active.continuePicked = false;
            active.continueChosen[0] = active.continueChosen[1] = -1;
            active.newGameChosen[0] = active.newGameChosen[1] = -1;
            active.newGameStep = 1;
            active.overwriteAsked = false;
            active.deleteAskedUntil = -1f;
            active.LoadSaves();
            active.tab = -1;
            active.SetTab(active.continueShown ? ContinueTab : NewGameTab, false);
        }
#endif

        static SaveSummary[] Summaries(SaveMode mode)
        {
#if UNITY_INCLUDE_TESTS
            if (SummariesForTests != null) return SummariesForTests(mode);
#endif
            return ArenaSaves.List(mode);
        }

        static void CallBind(SaveMode mode, int slot)
        {
#if UNITY_INCLUDE_TESTS
            if (SaveCallsForTests != null)
            {
                SaveCallsForTests("Bind", mode, slot);
                return;
            }
#endif
            ArenaSaves.Bind(mode, slot);
        }

        static void CallContinue(SaveMode mode, int slot)
        {
#if UNITY_INCLUDE_TESTS
            if (SaveCallsForTests != null)
            {
                SaveCallsForTests("Continue", mode, slot);
                return;
            }
#endif
            ArenaSaves.Continue(mode, slot);
        }

        static void CallDelete(SaveMode mode, int slot)
        {
#if UNITY_INCLUDE_TESTS
            if (SaveCallsForTests != null)
            {
                SaveCallsForTests("Delete", mode, slot);
                return;
            }
#endif
            ArenaSaves.Delete(mode, slot);
        }

        static bool Filled(SaveSummary save) => save != null && !save.empty;

        // A won run: its save is kept, marked, and cannot be continued (ArenaSaves.MarkCleared).
        static bool Cleared(SaveSummary save) => Filled(save) && save.cleared;

        /// <summary>Reads the slots again; the menu shows Continue while any slot holds a save.</summary>
        void LoadSaves()
        {
            for (int m = 0; m < 2; m++)
            {
                var list = Summaries(Modes[m]);
                for (int i = 0; i < ArenaSaves.SlotCount; i++) saves[m][i] = list != null && i < list.Length ? list[i] : null;
            }
            bool any = false;
            long newest = long.MinValue;
            int newestMode = -1, newestSlot = -1;
            for (int m = 0; m < 2; m++)
                for (int i = 0; i < ArenaSaves.SlotCount; i++)
                {
                    var save = saves[m][i];
                    if (!Filled(save)) continue;
                    any = true;
                    if (save.savedAtUnixSeconds <= newest) continue;
                    newest = save.savedAtUnixSeconds;
                    newestMode = m;
                    newestSlot = i;
                }
            for (int m = 0; m < 2; m++)
                if (continueChosen[m] < 0 || !Filled(saves[m][continueChosen[m]])) continueChosen[m] = NewestIn(m);
            if (!continuePicked && newestMode >= 0)
            {
                continuePicked = true;
                continueMode = newestMode;
                continueChosen[newestMode] = newestSlot;
            }
            for (int m = 0; m < 2; m++)
                if (newGameChosen[m] < 0) newGameChosen[m] = Array.FindIndex(saves[m], save => !Filled(save));
            bool was = continueShown;
            continueShown = any;
            LayMenu();
            if (!built || was == continueShown) return;
            if (!continueShown && tab == ContinueTab) SetTab(NewGameTab, true);
            RenderMenu();
            PlacePointer();
        }

        int NewestIn(int mode)
        {
            int best = -1;
            for (int i = 0; i < ArenaSaves.SlotCount; i++)
                if (Filled(saves[mode][i]) && (best < 0 || saves[mode][i].savedAtUnixSeconds > saves[mode][best].savedAtUnixSeconds)) best = i;
            return best;
        }

        void TickSaves()
        {
            if (deleteAskedUntil < 0f || Time.unscaledTime <= deleteAskedUntil) return;
            deleteAskedUntil = -1f;
            if (tab == ContinueTab) SetDelete();
        }

        void BuildSaves()
        {
            var page = pageRoots[ContinueTab];
            continueModes = Block(page, "Modes", 0f, 0f, InnerW, RowH);
            for (int m = 0; m < 2; m++)
            {
                int mode = m;
                var plate = V5Plate.Make(continueModes.transform, m == 0 ? "Story" : "Open", V5Plate.Look.Plate, 9f, () => OnContinueMode(mode));
                plate.Place(m * (ModeTabW + 6f), 0f, ModeTabW, RowH);
                continueTabLabels[m] = V5TitleKit.Label(plate.transform, "Label", 13, V5TitleKit.Ink2, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(continueTabLabels[m].rectTransform, 4f);
                continueTabs[m] = plate;
            }
            for (int i = 0; i < ArenaSaves.SlotCount; i++)
            {
                int slot = i;
                continueSlots[i] = MakeSlot(page, "Slot" + (i + 1), RowH + RowGap + i * (SlotH + SlotGap), SlotH, () => OnContinueSlot(slot));
            }

            page = pageRoots[NewGameTab];
            float cardH = (BodyNoFootH - SlotGap) * .5f, textLeft = 30f + ModeMedal + 24f;
            for (int m = 0; m < 2; m++)
            {
                int mode = m;
                var card = V5Plate.Make(page, m == 0 ? "StoryMode" : "OpenMode", V5Plate.Look.Plate, 16f, () => OnModeCard(mode), true);
                card.Place(0f, m * (cardH + SlotGap), InnerW, cardH);
                card.HoverPitch = 1.06f - m * .06f;
                modeMedals[m] = Medal(card.transform, "Medal", ModeMedal);
                V5TitleKit.Place(modeMedals[m].root, 30f, (cardH - ModeMedal) * .5f, ModeMedal, ModeMedal);
                modeNames[m] = V5TitleKit.Label(card.transform, "Name", 26, Color.white, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(modeNames[m].rectTransform, textLeft, cardH * .5f - 32f, InnerW - textLeft - 18f, 30f);
                V5TitleKit.Painted(modeNames[m]);
                modeCaps[m] = V5TitleKit.Label(card.transform, "Cap", 10, V5TitleKit.OrangeHi, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(modeCaps[m].rectTransform, textLeft, cardH * .5f + 6f, InnerW - textLeft - 18f, 12f);
                modeLines[m] = V5TitleKit.Label(card.transform, "Line", 11, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
                V5TitleKit.Place(modeLines[m].rectTransform, textLeft, cardH * .5f + 24f, InnerW - textLeft - 18f, 14f);
                modeCards[m] = card;
            }
            newGameTop = Block(page, "Top", 0f, 0f, InnerW, RowH);
            backPlate = V5Plate.Make(newGameTop.transform, "Back", V5Plate.Look.Plate, 9f, OnBackToModes);
            backPlate.Place(0f, 0f, BackW, RowH);
            backArrow = V5TitleKit.Picture(backPlate.transform, "Arrow", V5TitleKit.Piece("pointer"), V5TitleKit.OrangeHi);
            var arrow = backArrow.rectTransform;
            arrow.anchorMin = arrow.anchorMax = new Vector2(0f, 1f);
            arrow.pivot = new Vector2(.5f, .5f);
            arrow.anchoredPosition = new Vector2(16f, -RowH * .5f);
            arrow.sizeDelta = new Vector2(9f * Css, 14f * Css);
            arrow.localScale = new Vector3(-1f, 1f, 1f);
            backLabel = V5TitleKit.Label(backPlate.transform, "Label", 13, V5TitleKit.OrangeHi, TextAnchor.MiddleLeft, true);
            V5TitleKit.Place(backLabel.rectTransform, 27f, 0f, BackW - 33f, RowH);
            newGameLine = V5TitleKit.Label(newGameTop.transform, "Line", 11, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            V5TitleKit.Place(newGameLine.rectTransform, BackW + 12f, 0f, InnerW - BackW - 12f, RowH);
            for (int i = 0; i < ArenaSaves.SlotCount; i++)
            {
                int slot = i;
                newGameSlots[i] = MakeSlot(page, "Slot" + (i + 1), RowH + RowGap + i * (SlotH + SlotGap), SlotH, () => OnNewGameSlot(slot));
            }
        }

        // ───── A slot ─────

        SlotView MakeSlot(Transform parent, string name, float top, float height, Action click)
        {
            var view = new SlotView();
            view.plate = V5Plate.Make(parent, name, V5Plate.Look.Plate, 12f, click);
            view.plate.Place(0f, top, InnerW, height);
            view.plate.AddTab(22f);
            var root = view.plate.transform;
            // The slot's number in a column of its own, a hairline, then what the save holds (rows: the day or wave, the weapons, the ship
            // or the map).
            float columnLeft = 14f * Css, column = 56f * Css, mainLeft = columnLeft + column + 12f * Css + 1f + 14f * Css;
            float mainW = InnerW - 20f * Css - mainLeft, blockTop = (height - 45f) * .5f;
            view.number = V5TitleKit.Label(root, "Number", 30, Color.white, TextAnchor.MiddleCenter, true);
            V5TitleKit.Place(view.number.rectTransform, columnLeft, blockTop, column, 30f);
            V5TitleKit.Painted(view.number);
            view.numberCap = V5TitleKit.Label(root, "NumberCap", 9, V5TitleKit.Ink3, TextAnchor.MiddleCenter, true);
            V5TitleKit.Place(view.numberCap.rectTransform, columnLeft, blockTop + 36f, column, 9f);
            view.divider = V5TitleKit.Picture(root, "Divider", null, DividerColour);
            V5TitleKit.Place(view.divider.rectTransform, columnLeft + column + 9f, 12f, 1f, height - 24f);
            view.full = V5TitleKit.Rect(root, "Save");
            V5TitleKit.Place(view.full, mainLeft, 0f, mainW, height);
            view.empty = V5TitleKit.Rect(root, "Empty");
            V5TitleKit.Place(view.empty, mainLeft + 6f * Css, 0f, mainW, height);
            view.ra = 12f;
            view.rc = height - 12f - 18f;
            view.rb = (view.ra + 21f + view.rc) * .5f - 13.5f;
            view.headline = V5TitleKit.Label(view.full, "Headline", 18, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            view.phase = Chip(view.full, "Phase");
            view.mark = Chip(view.full, "Cleared");
            for (int p = 0; p < MaxPips; p++) view.pips[p] = V5TitleKit.Picture(view.full, "Pip" + (p + 1), V5TitleKit.Piece("pip"), PipOff);
            for (int w = 0; w < 4; w++)
            {
                view.weaponPictures[w] = V5TitleKit.Picture(view.full, "Weapon" + (w + 1), null, Color.white);
                view.weaponPictures[w].preserveAspect = true;
                view.weaponNames[w] = V5TitleKit.Label(view.full, "WeaponName" + (w + 1), 10, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            }
            string[] partNames = { "Engine", "Armor", "Fuel" };
            for (int k = 0; k < 3; k++)
            {
                view.parts[k] = Medal(view.full, partNames[k], 18f);
                view.partTexts[k] = V5TitleKit.Label(view.full, partNames[k] + "Words", 10, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            }
            view.fuelTrack = V5TitleKit.Picture(view.full, "FuelBar", V5TitleKit.Piece("bar-track"), Color.white, V5TitleKit.PieceDensity);
            for (int c = 0; c < 10; c++) view.fuelCells[c] = V5TitleKit.Picture(view.fuelTrack.transform, "Cell" + (c + 1), V5TitleKit.Piece("bar-fill"), V5TitleKit.Gold, V5TitleKit.PieceDensity);
            view.mapHex = V5TitleKit.Picture(view.full, "Map", V5TitleKit.Piece("hex-mask"), Color.white);
            view.mapHex.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            view.mapPicture = V5TitleKit.Picture(view.mapHex.transform, "Picture", null, Color.white);
            V5TitleKit.Stretch(view.mapPicture.rectTransform);
            view.mapText = V5TitleKit.Label(view.full, "MapWords", 10, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            view.emptyTitle = V5TitleKit.Label(view.empty, "Title", 18, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            view.emptyLine = V5TitleKit.Label(view.empty, "Line", 10, V5TitleKit.Ink3, TextAnchor.MiddleLeft);
            return view;
        }

        static ChipView Chip(Transform parent, string name)
        {
            var chip = new ChipView { root = V5TitleKit.Rect(parent, name) };
            chip.back = V5TitleKit.Picture(chip.root, "Back", V5TitleKit.Piece("chip"), Color.white, V5TitleKit.PieceDensity);
            V5TitleKit.Stretch(chip.back.rectTransform);
            chip.text = V5TitleKit.Label(chip.root, "Words", 10, V5TitleKit.Dark, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(chip.text.rectTransform);
            return chip;
        }

        void RenderSlot(SlotView view, SaveMode mode, SaveSummary save, int slot, bool chosen, bool forNewGame)
        {
            bool filled = Filled(save);
            view.plate.SetLook(filled ? V5Plate.Look.Plate : V5Plate.Look.Empty);
            view.plate.Chosen = chosen;
            view.number.text = (slot + 1).ToString("00");
            view.numberCap.text = ArenaText.Pair("存 档", "SLOT");
            if (view.full.gameObject.activeSelf != filled) view.full.gameObject.SetActive(filled);
            if (view.empty.gameObject.activeSelf == filled) view.empty.gameObject.SetActive(!filled);
            if (!filled)
            {
                if (forNewGame)
                {
                    view.emptyTitle.text = "<color=#FF9B5E>＋</color> " + ArenaText.Pair("开始新游戏", "New Game");
                    view.emptyTitle.color = V5TitleKit.Ink;
                    view.emptyLine.text = ArenaText.Pair("空存档", "Empty slot") + (mode == SaveMode.Open ? ArenaText.Pair(" · 开始前先选地图", " · pick a map first") : "");
                }
                else
                {
                    view.emptyTitle.text = ArenaText.Pair("空存档", "Empty slot");
                    view.emptyTitle.color = V5TitleKit.Ink3;
                    view.emptyLine.text = "";
                }
                float width = view.empty.rect.width, height = view.empty.rect.height;
                bool line = view.emptyLine.text.Length > 0;
                float top = line ? (height - 35.5f) * .5f : (height - 21f) * .5f;
                V5TitleKit.Place(view.emptyTitle.rectTransform, 0f, top, width, 21f);
                V5TitleKit.FitWidth(view.emptyTitle, 18, 12);
                V5TitleKit.Place(view.emptyLine.rectTransform, 0f, top + 25.5f, width, 10f);
                V5TitleKit.FitWidth(view.emptyLine, 10, 8);
                return;
            }
            float mainW = view.full.rect.width;
            LayHeadline(view, mode, save, mainW);
            LayWeapons(view, save.weapons, mainW);
            if (mode == SaveMode.Story) LayStoryParts(view, save, mainW);
            else LayOpenParts(view, save, mainW);
        }

        void LayHeadline(SlotView view, SaveMode mode, SaveSummary save, float width)
        {
            bool story = mode == SaveMode.Story;
            int waves = WaveCount(save);
            view.headline.text = story ? ArenaText.Pair($"第 <size=21>{save.day}</size> 天", $"Day <size=21>{save.day}</size>")
                : ArenaText.Pair($"第 <size=21>{save.wave}</size> 波", $"Wave <size=21>{save.wave}</size>") + $" <size=12><color=#7D8397>/ {waves}</color></size>";
            float x = view.headline.preferredWidth;
            V5TitleKit.Place(view.headline.rectTransform, 0f, view.ra, x + 2f, 21f);
            x += 7.5f;
            if (story) LayChip(view.phase, x, view.ra, PhaseWords(save.phase), PhaseBack(save.phase), PhaseInk(save.phase));
            else Show(view.phase.root, false);
            if (Cleared(save))
            {
                // A won run is complete: its mark stands where the progress was.
                LayPips(view, 0, 0, false, width);
                view.mark.text.text = ArenaText.Pair("通关", "CLEARED");
                LayChip(view.mark, width - view.mark.text.preferredWidth - 12f, view.ra, view.mark.text.text, V5TitleKit.Teal, MarkInk);
                return;
            }
            Show(view.mark.root, false);
            if (story) LayPips(view, StoryDays, Mathf.Clamp(save.day, 0, StoryDays), false, width);
            else LayPips(view, waves, Mathf.Clamp(save.wave, 0, waves), true, width);
        }

        static float LayChip(ChipView chip, float x, float rowTop, string words, Color back, Color ink)
        {
            chip.text.text = words;
            chip.text.color = ink;
            float w = chip.text.preferredWidth + 12f;
            V5TitleKit.Place(chip.root, x, rowTop + (21f - 16.5f) * .5f, w, 16.5f);
            Show(chip.root, true);
            V5TitleKit.Dress(chip.back, Art.Enabled ? V5TitleKit.Piece("chip") : null, back, V5TitleKit.PieceDensity);
            return x + w;
        }

        static void LayPips(SlotView view, int count, int filled, bool waves, float width)
        {
            float pw = (waves ? 10f : 15f) * Css, ph = 11f * Css, gap = 4f * Css;
            float total = count * pw + Mathf.Max(0, count - 1) * gap, left = width - total, top = view.ra + (21f - ph) * .5f;
            for (int p = 0; p < MaxPips; p++)
            {
                var pip = view.pips[p];
                bool shown = p < count;
                Show(pip, shown);
                if (!shown) continue;
                V5TitleKit.Place(pip.rectTransform, left + p * (pw + gap), top, pw, ph);
                bool on = p < filled, boss = waves && p == count - 1;
                pip.color = boss ? on ? V5TitleKit.Crimson : V5TitleKit.Rgb(226, 59, 78, .35f) : on ? waves ? V5TitleKit.OrangeHi : V5TitleKit.Gold : PipOff;
            }
        }

        void LayWeapons(SlotView view, WeaponForm[] forms, float width)
        {
            var set = hud ? hud.art : null;
            bool on = Art.Enabled;
            int count = forms == null ? 0 : Mathf.Min(4, forms.Length);
            float picW = 66f * Css, picH = 33f * Css, inner = 6f * Css, between = 12f * Css;
            int size = 10;
            bool names = true;
            for (int k = 0; k < 4; k++) view.weaponNames[k].text = k < count ? ArenaTerms.WeaponForm(forms[k]) : "";
            // A long row keeps every picture: the names shrink first, then they are left out.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float total = 0f;
                for (int k = 0; k < count; k++)
                {
                    view.weaponNames[k].fontSize = size;
                    total += picW + (names ? inner + view.weaponNames[k].preferredWidth : 0f) + (k > 0 ? between : 0f);
                }
                if (total <= width) break;
                if (size > 8) size--;
                else if (names) names = false;
                else
                {
                    picW *= .85f;
                    picH *= .85f;
                }
            }
            float x = 0f;
            for (int k = 0; k < 4; k++)
            {
                var picture = view.weaponPictures[k];
                var name = view.weaponNames[k];
                bool shown = k < count;
                int form = shown ? (int)forms[k] : -1;
                Sprite sprite = !shown ? null : !on ? WhiteboxIcons.Weapon(form)
                    : set && set.weaponSprites != null && form < set.weaponSprites.Length ? set.weaponSprites[form] : null;
                Show(picture, shown && sprite);
                Show(name, shown && names);
                if (!shown) continue;
                picture.sprite = sprite;
                V5TitleKit.Place(picture.rectTransform, x, view.rb + (27f - picH) * .5f, picW, picH);
                x += picW;
                if (names)
                {
                    float w = name.preferredWidth;
                    V5TitleKit.Place(name.rectTransform, x + inner, view.rb, w + 1f, 27f);
                    x += inner + w;
                }
                x += between;
            }
        }

        void LayStoryParts(SlotView view, SaveSummary save, float width)
        {
            Show(view.mapHex, false);
            Show(view.mapText, false);
            var icons = CampaignIcons;
            Sprite[] sprites = { icons ? icons.engine : null, icons ? icons.armor : null, icons ? icons.fuel : null };
            string[] words =
            {
                ArenaText.Pair("引擎", "Engine") + " " + Repaired(save.engineFixed),
                ArenaText.Pair("装甲", "Armor") + " " + Repaired(save.armorFixed),
                ArenaText.Pair("燃料", "Fuel"),
            };
            const float medal = 18f, near = 4.5f, apart = 10.5f;
            int size = 10;
            float bar = 75f;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float total = 0f;
                for (int k = 0; k < 3; k++)
                {
                    view.partTexts[k].fontSize = size;
                    view.partTexts[k].text = words[k];
                    total += medal + near + view.partTexts[k].preferredWidth;
                }
                total += 2f * apart + near + bar;
                if (total <= width) break;
                if (bar > 40f) bar = Mathf.Max(40f, bar - (total - width));
                else if (size > 8) size--;
                else break;
            }
            float x = 0f;
            for (int k = 0; k < 3; k++)
            {
                var part = view.parts[k];
                Show(part.root, true);
                V5TitleKit.Place(part.root, x, view.rc, medal, medal);
                ShowMedal(part, sprites[k]);
                x += medal + near;
                var text = view.partTexts[k];
                Show(text, true);
                float w = text.preferredWidth;
                V5TitleKit.Place(text.rectTransform, x, view.rc, w + 1f, medal);
                x += w + (k < 2 ? apart : near);
            }
            bool on = Art.Enabled;
            Show(view.fuelTrack, true);
            V5TitleKit.Place(view.fuelTrack.rectTransform, x, view.rc + (medal - 7.5f) * .5f, bar, 7.5f);
            V5TitleKit.Dress(view.fuelTrack, on ? V5TitleKit.Piece("bar-track") : null, on ? Color.white : V5TitleKit.Rgb(10, 13, 21), V5TitleKit.PieceDensity);
            int cells = save.fuelMax > 0 ? Mathf.Clamp(Mathf.RoundToInt(10f * save.fuel / save.fuelMax), 0, 10) : 0;
            float cellW = (bar - 3f - 9f * 1.5f) / 10f;
            for (int c = 0; c < 10; c++)
            {
                var cell = view.fuelCells[c];
                Show(cell, c < cells);
                V5TitleKit.Place(cell.rectTransform, 1.5f + c * (cellW + 1.5f), 1.5f, cellW, 4.5f);
                V5TitleKit.Dress(cell, on ? V5TitleKit.Piece("bar-fill") : null, V5TitleKit.Gold, V5TitleKit.PieceDensity);
            }
        }

        static string Repaired(bool done) => done ? "<b><color=#3BD3C0>✓</color></b>" : "<b><color=#FF9B5E>" + ArenaText.Pair("待修复", "Needs repair") + "</color></b>";

        void LayOpenParts(SlotView view, SaveSummary save, float width)
        {
            for (int k = 0; k < 3; k++)
            {
                Show(view.parts[k].root, false);
                Show(view.partTexts[k], false);
            }
            Show(view.fuelTrack, false);
            bool on = Art.Enabled;
            // A map of neither family (ArenaSaves.MapFamily) is not named.
            int family = save.mapFamily;
            bool known = family is 0 or 1;
            Show(view.mapHex, known);
            Show(view.mapText, known);
            if (!known) return;
            V5TitleKit.Place(view.mapHex.rectTransform, 0f, view.rc - 1f, 30f * Css, 26f * Css);
            view.mapPicture.sprite = on ? V5TitleKit.Piece(family == 1 ? "map-ruins" : "map-dunes") : null;
            Show(view.mapPicture, on && view.mapPicture.sprite);
            view.mapHex.color = on && view.mapPicture.sprite ? Color.white : MapFlat[family];
            view.mapText.text = ArenaText.Pair("地图", "Map") + " <b><color=#ECE7DC>" + V2Hud.MapWord(family) + "</color></b>";
            V5TitleKit.Place(view.mapText.rectTransform, 30f * Css + 6f, view.rc, width - 30f * Css - 6f, 18f);
            V5TitleKit.FitWidth(view.mapText, 10, 8);
        }

        static string PhaseWords(SavePhase phase) => phase switch
        {
            SavePhase.Morning => ArenaText.Pair("上午", "Morning"),
            SavePhase.Afternoon => ArenaText.Pair("下午", "Afternoon"),
            _ => ArenaText.Pair("夜晚", "Night"),
        };

        static Color PhaseBack(SavePhase phase) => phase switch
        {
            SavePhase.Morning => V5TitleKit.Gold,
            SavePhase.Afternoon => V5TitleKit.OrangeHi,
            _ => V5TitleKit.Moon,
        };

        static Color PhaseInk(SavePhase phase) => phase switch
        {
            SavePhase.Morning => V5TitleKit.Rgb(42, 26, 4),
            SavePhase.Afternoon => V5TitleKit.Rgb(42, 18, 6),
            _ => V5TitleKit.Rgb(7, 26, 44),
        };

        static int StoryDays => Mathf.Clamp(V2Tutorial.StoryDays, 1, MaxPips);

        // The open mode's waves, the Tyrant's included: his wave comes after the wave set.
        static int OpenWaves => GameManager.Instance && GameManager.Instance.waves && GameManager.Instance.waves.WaveCount > 0 ? GameManager.Instance.waves.WaveCount + 1 : 15;

        static int WaveCount(SaveSummary save) => Mathf.Clamp(save != null && save.waveCount > 0 ? save.waveCount : OpenWaves, 1, MaxPips);

        static string ModeWords(int mode) => mode == 0 ? ArenaText.Pair("剧情模式", "Story Mode") : ArenaText.Pair("开放模式", "Open Mode");

        static string SlotWords(SaveMode mode, int slot, SaveSummary save)
        {
            string words = ArenaText.Pair($"存档 {slot + 1}", $"Slot {slot + 1}");
            if (!Filled(save)) return words;
            return words + (mode == SaveMode.Story ? ArenaText.Pair($" · 第 {save.day} 天", $" · Day {save.day}") : ArenaText.Pair($" · 第 {save.wave} 波", $" · Wave {save.wave}"));
        }

        // ───── Continue ─────

        void RenderContinue()
        {
            for (int m = 0; m < 2; m++)
            {
                continueTabs[m].Chosen = m == continueMode;
                continueTabLabels[m].text = ModeWords(m);
                continueTabLabels[m].color = m == continueMode ? V5TitleKit.Chosen : V5TitleKit.Ink2;
                V5TitleKit.FitWidth(continueTabLabels[m], 13, 9);
            }
            var mode = Modes[continueMode];
            for (int i = 0; i < ArenaSaves.SlotCount; i++)
                RenderSlot(continueSlots[i], mode, saves[continueMode][i], i, continueChosen[continueMode] == i, false);
            int chosen = continueChosen[continueMode];
            var save = chosen >= 0 ? saves[continueMode][chosen] : null;
            bool filled = Filled(save), won = Cleared(save);
            ShowFoot(filled ? FootUse.GoAndDelete : FootUse.Go);
            SetGo(won ? ArenaText.Pair("已通关", "Cleared") : ArenaText.Pair("继续游戏", "Continue"), filled ? SlotWords(mode, chosen, save) : "", filled && !won);
            SetDelete();
        }

        void SetDelete()
        {
            bool asked = deleteAskedUntil >= 0f;
            deleteLabel.text = asked ? ArenaText.Pair("再点一次删除", "Click again") : ArenaText.Pair("删除存档", "Delete save");
            deleteLabel.color = asked ? V5TitleKit.OrangeHi : V5TitleKit.Ink3;
            V5TitleKit.FitWidth(deleteLabel, 11, 8);
        }

        void OnContinueMode(int mode)
        {
            if (mode == continueMode) return;
            continueMode = mode;
            deleteAskedUntil = -1f;
            V5UiSound.Play(V5UiSound.Ui.Tab);
            RenderContinue();
            Stagger(continueSlots);
        }

        void OnContinueSlot(int slot)
        {
            var save = saves[continueMode][slot];
            if (!Filled(save))
            {
                V5UiSound.Play(V5UiSound.Ui.Denied);
                Shake(continueSlots[slot].plate);
                return;
            }
            if (continueChosen[continueMode] == slot)
            {
                TryContinue();
                return;
            }
            continueChosen[continueMode] = slot;
            deleteAskedUntil = -1f;
            V5UiSound.Play(V5UiSound.Ui.Press);
            RenderContinue();
        }

        void TryContinue()
        {
            int slot = continueChosen[continueMode];
            var save = slot >= 0 ? saves[continueMode][slot] : null;
            // A won run is not continued: its button reads 已通关 and does nothing.
            if (!Filled(save) || Cleared(save)) return;
            V5UiSound.Play(V5UiSound.Ui.Start);
            CallContinue(Modes[continueMode], slot);
        }

        void OnDelete()
        {
            int slot = continueChosen[continueMode];
            if (slot < 0 || !Filled(saves[continueMode][slot])) return;
            if (deleteAskedUntil < 0f)
            {
                deleteAskedUntil = Time.unscaledTime + DeleteAskSeconds;
                V5UiSound.Play(V5UiSound.Ui.Press);
                SetDelete();
                return;
            }
            deleteAskedUntil = -1f;
            V5UiSound.Play(V5UiSound.Ui.Back);
            CallDelete(Modes[continueMode], slot);
            savesDirty = true;
        }

        // ───── New game ─────

        void RenderNewGame()
        {
            bool second = newGameStep == 2;
            for (int m = 0; m < 2; m++) Show(modeCards[m], !second);
            Show(newGameTop, second);
            for (int i = 0; i < ArenaSaves.SlotCount; i++) Show(newGameSlots[i].plate, second);
            if (!second)
            {
                var icons = CampaignIcons;
                for (int m = 0; m < 2; m++)
                {
                    modeNames[m].text = ModeWords(m);
                    V5TitleKit.FitWidth(modeNames[m], 26, 16);
                    modeCaps[m].text = ArenaText.Pair(V5TitleKit.Spaced(m == 0 ? "STORY" : "OPEN"), "");
                    // Names and icons only: the story is not told ahead of time.
                    modeLines[m].text = m == 1 ? ArenaText.Pair($"{OpenWaves} 波 · 开始前先选地图", $"{OpenWaves} waves · pick a map first") : "";
                    V5TitleKit.FitWidth(modeLines[m], 11, 8);
                    ShowMedal(modeMedals[m], icons ? m == 0 ? icons.shipBay : icons.armament : null);
                }
                ShowFoot(FootUse.None);
                return;
            }
            var mode = Modes[newGameMode];
            int chosen = newGameChosen[newGameMode];
            backLabel.text = ArenaText.Pair("返回", "Back");
            V5TitleKit.FitWidth(backLabel, 13, 9);
            bool full = Array.TrueForAll(saves[newGameMode], Filled);
            if (overwriteAsked && chosen >= 0)
            {
                newGameLine.text = ArenaText.Pair($"这会删除存档 {chosen + 1} 的进度", $"This deletes the progress saved in slot {chosen + 1}");
                newGameLine.color = V5TitleKit.OrangeHi;
            }
            else
            {
                newGameLine.text = full ? ArenaText.Pair("存档栏位已满，选一个存档覆盖", "Every slot is in use; choose one to overwrite") : "";
                newGameLine.color = V5TitleKit.Ink2;
            }
            V5TitleKit.FitWidth(newGameLine, 11, 8);
            for (int i = 0; i < ArenaSaves.SlotCount; i++) RenderSlot(newGameSlots[i], mode, saves[newGameMode][i], i, chosen == i, true);
            if (overwriteAsked && chosen >= 0)
            {
                ShowFoot(FootUse.Confirm);
                confirmLabel.text = ArenaText.Pair("确认覆盖", "Overwrite");
                cancelLabel.text = ArenaText.Pair("取消", "Cancel");
                V5TitleKit.FitWidth(confirmLabel, 17, 12);
                V5TitleKit.FitWidth(cancelLabel, 17, 12);
                return;
            }
            var save = chosen >= 0 ? saves[newGameMode][chosen] : null;
            ShowFoot(FootUse.Go);
            SetGo(Filled(save) ? ArenaText.Pair("覆盖并开始新游戏", "Overwrite and Start") : ArenaText.Pair("开始新游戏", "New Game"),
                chosen >= 0 ? SlotWords(mode, chosen, save) : "", chosen >= 0);
        }

        void OnModeCard(int mode)
        {
            newGameMode = mode;
            newGameStep = 2;
            overwriteAsked = false;
            V5UiSound.Play(V5UiSound.Ui.Panel, 1f + (mode - .5f) * .05f);
            RenderHead();
            RenderNewGame();
            Enter();
        }

        void OnBackToModes()
        {
            if (newGameStep != 2) return;
            newGameStep = 1;
            overwriteAsked = false;
            V5UiSound.Play(V5UiSound.Ui.Back);
            RenderHead();
            RenderNewGame();
            Enter();
        }

        void OnNewGameSlot(int slot)
        {
            if (newGameChosen[newGameMode] == slot && !overwriteAsked)
            {
                OnNewGameGo();
                return;
            }
            newGameChosen[newGameMode] = slot;
            overwriteAsked = false;
            V5UiSound.Play(V5UiSound.Ui.Press);
            RenderNewGame();
        }

        void OnNewGameGo()
        {
            int slot = newGameChosen[newGameMode];
            if (slot < 0) return;
            if (Filled(saves[newGameMode][slot]))
            {
                // A filled slot asks once more, in the window.
                overwriteAsked = true;
                V5UiSound.Play(V5UiSound.Ui.Press);
                RenderNewGame();
                return;
            }
            StartNew(newGameMode, slot);
        }

        void OnConfirmOverwrite()
        {
            if (!overwriteAsked) return;
            int slot = newGameChosen[newGameMode];
            if (slot >= 0) StartNew(newGameMode, slot);
        }

        void OnCancelOverwrite()
        {
            if (!overwriteAsked) return;
            overwriteAsked = false;
            V5UiSound.Play(V5UiSound.Ui.Back);
            RenderNewGame();
        }

        void StartNew(int mode, int slot)
        {
            overwriteAsked = false;
            if (mode == 0)
            {
                // The story starts at once: the old save goes now and the slot is bound for the new run.
                if (Filled(saves[0][slot])) CallDelete(SaveMode.Story, slot);
                CallBind(SaveMode.Story, slot);
                V5UiSound.Play(V5UiSound.Ui.Start);
                StartStory();
                return;
            }
            // The open mode starts only once a map is taken: until then the slot keeps what it holds.
            pendingOpenSlot = slot;
            pendingOpenOverwrite = Filled(saves[1][slot]);
            V5UiSound.Play(V5UiSound.Ui.Panel);
            V5Plate.Quiet = true;
            try { hud.OpenMapSelect(); }
            finally { V5Plate.Quiet = false; }
        }

        /// <summary>V2Hud dispatched a map from the pick (ArenaUiCommand.ChooseMap). Once the open run really starts (the loadout opened, or the
        /// scene reloads for the map: either way the title can no longer start anything), the slot chosen for it is bound, an old save in it
        /// deleted first. A pick the menus did not take (their input lock) changes nothing.</summary>
        public void MapChosen()
        {
            var game = GameManager.Instance;
            if (pendingOpenSlot < 0 || !game || game.CanContinueSave) return;
            int slot = pendingOpenSlot;
            bool overwrite = pendingOpenOverwrite;
            pendingOpenSlot = -1;
            pendingOpenOverwrite = false;
            if (overwrite) CallDelete(SaveMode.Open, slot);
            CallBind(SaveMode.Open, slot);
        }

        /// <summary>A new story run, the way the saved Start begins it: its command is the story mode's (V2Campaign.RouteTitleStart).</summary>
        void StartStory()
        {
            var start = content ? content.Find(V2Hud.TitleStoryButton) : null;
            if (start && start.TryGetComponent<ArenaUiButton>(out var relay)) relay.Invoke();
            else if (hud) hud.Dispatch(ArenaUiCommand.StartCampaign);
        }

        /// <summary>The slots come in one after another (a change of the continue window's mode).</summary>
        void Stagger(SlotView[] views)
        {
            for (int k = 0; k < views.Length; k++)
            {
                var plate = views[k].plate;
                Animate(plate, .24f, k * .018f, EaseIn, p =>
                {
                    plate.Offset = new Vector2(0f, -6f * (1f - p));
                    plate.Alpha = p;
                });
            }
        }
    }
}
