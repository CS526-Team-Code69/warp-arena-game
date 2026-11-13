using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The title's save windows (V5Title) on made-up summaries, with the calls to ArenaSaves noted instead of made. 继续游戏: tabs
    /// for the two modes, the most recent save chosen; a story save shows its day, the part of the day, seven day marks, the weapons, the
    /// engine, the armor and the fuel; an open-mode save its wave out of 15 (the last mark the Tyrant's) and its map; an empty slot cannot be
    /// chosen; a won run cannot be continued; deleting asks twice. 新游戏: the mode first, then the slot; an empty slot is bound and the
    /// story starts as the saved Start does; a filled one is overwritten only after one more question (the story's deleted, then bound,
    /// at once; the open mode's once a map starts the run: OpenModeMapSelectTest). The story
    /// mode's battle HUD shows no level; the open mode's keeps it.</summary>
    [TestFixture]
    public class V5TitleSavesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const string Continue = "Window/Pages/Continue/", NewGame = "Window/Pages/NewGame/";
        readonly List<string> calls = new();

        [SetUp]
        public void SetUp()
        {
            calls.Clear();
            V5Title.SummariesForTests = EmptySlots;
            V5Title.SaveCallsForTests = (call, mode, slot) => calls.Add($"{call} {mode} {slot}");
        }

        [TearDown]
        public void TearDown()
        {
            V5Title.SummariesForTests = null;
            V5Title.SaveCallsForTests = null;
            if (V2Tutorial.IsOpen) V2Tutorial.Close();
            Time.timeScale = 1f;
        }

        static SaveSummary Story(int slot, int day, SavePhase phase, long at, bool engine, bool armor, int fuel, params WeaponForm[] weapons) => new()
        {
            mode = SaveMode.Story, slot = slot, empty = false, day = day, phase = phase, engineFixed = engine, armorFixed = armor, fuel = fuel, fuelMax = 10,
            weapons = weapons, savedAtUnixSeconds = at,
        };

        static SaveSummary Open(int slot, int wave, int family, long at, params WeaponForm[] weapons) => new()
        {
            mode = SaveMode.Open, slot = slot, empty = false, wave = wave, waveCount = 15, mapFamily = family, weapons = weapons, savedAtUnixSeconds = at,
        };

        void Use(SaveSummary[] story, SaveSummary[] open) => V5Title.SummariesForTests = mode => mode == SaveMode.Story ? story : open;

        static SaveSummary[] SomeSaves(out SaveSummary[] open)
        {
            open = new[] { Open(0, 9, 0, 100, WeaponForm.Rifle, WeaponForm.ScatterCannon, WeaponForm.Minigun), Open(1, 13, 1, 150, WeaponForm.Knife), new SaveSummary { mode = SaveMode.Open, slot = 2 } };
            return new[]
            {
                Story(0, 5, SavePhase.Night, 200, true, true, 6, WeaponForm.RocketLauncher, WeaponForm.CryoCannon, WeaponForm.OrbitBlades),
                Story(1, 2, SavePhase.Afternoon, 50, true, false, 0, WeaponForm.Pistol, WeaponForm.Flamethrower),
                new SaveSummary { mode = SaveMode.Story, slot = 2 },
            };
        }

        static int ActiveChildren(string path, string prefix) => Part(path).Cast<Transform>().Count(child => child.gameObject.activeSelf && child.name.StartsWith(prefix));

        // ───── 继续游戏 ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Continue_OpensOnTheMostRecentSave_DrawnFromItsSummary()
        {
            await ReadyScene();
            var story = SomeSaves(out var open);
            Use(story, open);
            V5Title.ReloadForTests();
            await Settle();

            Assert.That(V5Title.Active.Tab, Is.EqualTo(V5Title.ContinueTab), "with saves the title opens on Continue");
            Assert.That(Part(Continue + "Modes/Story").GetComponent<V5Plate>().Chosen, Is.True, "on the story mode, which holds the most recent save");
            Assert.That(Part(Continue + "Slot1").GetComponent<V5Plate>().Chosen, Is.True, "with that save chosen");
            Assert.That(Words(Continue + "Slot1/Save/Headline").text, Does.Contain("5"), "its day");
            Assert.That(Words(Continue + "Slot1/Save/Phase/Words").text, Is.EqualTo(ArenaText.Pair("夜晚", "Night")), "the part of the day");
            int days = V2Tutorial.StoryDays;
            var pips = Part(Continue + "Slot1/Save").Cast<Transform>().Where(child => child.gameObject.activeSelf && child.name.StartsWith("Pip")).Select(child => child.GetComponent<Image>()).ToArray();
            Assert.That(pips.Length, Is.EqualTo(days), "a mark for each day of the story");
            Assert.That(pips.Count(pip => pip.color == V5TitleKit.Gold), Is.EqualTo(5), "five of them gold");
            Assert.That(ActiveChildren(Continue + "Slot1/Save", "WeaponName"), Is.EqualTo(3), "its three weapons by name");
            Assert.That(Words(Continue + "Slot1/Save/WeaponName1").text, Is.EqualTo(ArenaTerms.WeaponForm(WeaponForm.RocketLauncher)), "in the order held");
            Assert.That(Words(Continue + "Slot1/Save/EngineWords").text, Does.Contain("✓"), "the engine mended");
            Assert.That(Words(Continue + "Slot2/Save/ArmorWords").text, Does.Contain(ArenaText.Pair("待修复", "Needs repair")), "the second save's armor still broken");
            Assert.That(ActiveChildren(Continue + "Slot1/Save/FuelBar", "Cell"), Is.EqualTo(6), "six cells of fuel out of ten");
            Assert.That(Words(Continue + "Slot3/Empty/Title").text, Is.EqualTo(ArenaText.Pair("空存档", "Empty slot")), "the empty slot says so");
            Assert.That(Words("Window/Foot/Go/Label").text, Is.EqualTo(ArenaText.Pair("继续游戏", "Continue")), "the foot continues");
            Assert.That(Words("Window/Foot/Go/Sub").text, Is.EqualTo(ArenaText.Pair("存档 1 · 第 5 天", "Slot 1 · Day 5")), "the chosen save");
            Assert.That(Part("Window/Foot/Delete").gameObject.activeSelf, Is.True, "a filled slot can be deleted");

            await ClickUntil(Part(Continue + "Modes/Open"), () => Part(Continue + "Modes/Open").GetComponent<V5Plate>().Chosen, "the open mode's tab");
            await Settle();
            var wavePips = Part(Continue + "Slot1/Save").Cast<Transform>().Where(child => child.gameObject.activeSelf && child.name.StartsWith("Pip")).Select(child => child.GetComponent<Image>()).ToArray();
            Assert.That(Words(Continue + "Slot1/Save/Headline").text, Does.Contain("9").And.Contain("/ 15"), "an open-mode save's wave out of 15");
            Assert.That(wavePips.Length, Is.EqualTo(15), "a mark for each wave");
            Assert.That(wavePips.Count(pip => pip.color == V5TitleKit.OrangeHi), Is.EqualTo(9), "nine reached");
            Assert.That(wavePips[14].color.r, Is.GreaterThan(wavePips[14].color.g * 2f), "the last mark, the Tyrant's, in crimson");
            Assert.That(Words(Continue + "Slot1/Save/MapWords").text, Does.Contain(ArenaText.Pair("沙丘", "Dunes")), "and its map");
            Assert.That(Part(Continue + "Slot2").GetComponent<V5Plate>().Chosen, Is.True, "the open mode's most recent save chosen");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Continue_AnEmptySlotCannotBeChosen_TheFootContinuesTheChosenSave()
        {
            await ReadyScene();
            var story = SomeSaves(out var open);
            Use(story, open);
            V5Title.ReloadForTests();
            await Settle();

            Click(Part(Continue + "Slot3"));
            await Frames(2);
            Assert.That(Part(Continue + "Slot3").GetComponent<V5Plate>().Chosen, Is.False, "an empty slot cannot be chosen");
            Assert.That(Part(Continue + "Slot1").GetComponent<V5Plate>().Chosen, Is.True, "the save chosen stays");
            Assert.That(calls, Is.Empty, "and nothing is continued");

            Click(Part("Window/Foot/Go"));
            Assert.That(calls, Is.EqualTo(new[] { "Continue Story 0" }), "the foot continues the chosen save");

            Click(Part(Continue + "Slot2"));
            await Frames(2);
            Assert.That(Words("Window/Foot/Go/Sub").text, Is.EqualTo(ArenaText.Pair("存档 2 · 第 2 天", "Slot 2 · Day 2")), "a click chooses another save");
            Click(Part(Continue + "Slot2"));
            Assert.That(calls.Last(), Is.EqualTo("Continue Story 1"), "a click on the chosen save continues it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Continue_AWonRun_ShowsItsMark_AndIsNotContinued()
        {
            await ReadyScene();
            var won = Story(0, 7, SavePhase.Night, 300, true, true, 10, WeaponForm.Railgun);
            won.cleared = true;
            Use(new[] { won, new SaveSummary { mode = SaveMode.Story, slot = 1 }, new SaveSummary { mode = SaveMode.Story, slot = 2 } }, EmptySlots(SaveMode.Open));
            V5Title.ReloadForTests();
            await Settle();

            Assert.That(Part(Continue + "Slot1/Save/Cleared").gameObject.activeSelf, Is.True, "the won run carries its mark");
            Assert.That(Words(Continue + "Slot1/Save/Cleared/Words").text, Is.EqualTo(ArenaText.Pair("通关", "CLEARED")));
            Assert.That(ActiveChildren(Continue + "Slot1/Save", "Pip"), Is.Zero, "in place of the day marks");
            Assert.That(Words("Window/Foot/Go/Label").text, Is.EqualTo(ArenaText.Pair("已通关", "Cleared")), "the foot says it was won");
            Assert.That(Part("Window/Foot/Go").GetComponent<Button>().interactable, Is.False, "and does nothing");
            Click(Part("Window/Foot/Go"));
            Click(Part(Continue + "Slot1"));
            Assert.That(calls, Is.Empty, "a won run is not continued");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Continue_Delete_AsksForASecondClick()
        {
            await ReadyScene();
            var story = SomeSaves(out var open);
            Use(story, open);
            V5Title.ReloadForTests();
            await Settle();
            var delete = Part("Window/Foot/Delete");

            Click(delete);
            await Frames(1);
            Assert.That(calls, Is.Empty, "one click deletes nothing");
            Assert.That(Words("Window/Foot/Delete/Label").text, Is.EqualTo(ArenaText.Pair("再点一次删除", "Click again")), "it asks for a second");

            Click(delete);
            Assert.That(calls, Is.EqualTo(new[] { "Delete Story 0" }), "the second deletes the chosen save");
        }

        // ───── 新游戏 ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task NewGame_TheModeFirst_ThenItsSlots_BackAndEscReturn()
        {
            await ReadyScene();
            V5Title.ReloadForTests();
            await Settle();

            Assert.That(V5Title.Active.Tab, Is.EqualTo(V5Title.NewGameTab), "without saves the title opens on the new game");
            Assert.That(Part(NewGame + "StoryMode").gameObject.activeSelf && Part(NewGame + "OpenMode").gameObject.activeSelf, Is.True, "two modes to choose from");
            Assert.That(Words(NewGame + "StoryMode/Line").text, Is.Empty, "the story mode by its name alone: nothing of the story is told");
            Assert.That(Words(NewGame + "OpenMode/Line").text, Does.Contain("15"), "the open mode says its waves");
            Assert.That(Part("Window/Foot").gameObject.activeSelf, Is.False, "no foot before a mode is chosen");

            await ClickUntil(Part(NewGame + "StoryMode"), () => Part(NewGame + "Top").gameObject.activeSelf, "the story mode's card");
            await Settle();
            Assert.That(ActiveChildren(NewGame, "Slot"), Is.EqualTo(3), "then its three slots");
            Assert.That(Part(NewGame + "StoryMode").gameObject.activeSelf, Is.False, "in place of the cards");
            Assert.That(Words("Window/Head/Title").text, Is.EqualTo(ArenaText.Pair("新游戏 · 剧情模式", "New Game · Story Mode")), "the window names the mode");
            Assert.That(Part(NewGame + "Slot1").GetComponent<V5Plate>().Chosen, Is.True, "the first empty slot chosen");
            Assert.That(Words("Window/Foot/Go/Label").text, Is.EqualTo(ArenaText.Pair("开始新游戏", "New Game")), "the foot starts a new game");

            Click(Part(NewGame + "Top/Back"));
            await Settle();
            Assert.That(Part(NewGame + "StoryMode").gameObject.activeSelf, Is.True, "Back returns to the modes");

            await ClickUntil(Part(NewGame + "OpenMode"), () => Part(NewGame + "Top").gameObject.activeSelf, "the open mode's card");
            await Settle();
            Assert.That(V5Title.Back(), Is.True, "Esc goes back a step");
            await Frames(1);
            Assert.That(Part(NewGame + "OpenMode").gameObject.activeSelf, Is.True, "to the modes");
            Assert.That(V5Title.Back(), Is.False, "and no further");
            Assert.That(calls, Is.Empty, "nothing was bound");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task NewGame_AnEmptySlot_IsBound_AndTheStoryStarts_WithNoLevelLine()
        {
            var game = await ReadyScene();
            V5Title.ReloadForTests();
            await Settle();
            await ClickUntil(Part(NewGame + "StoryMode"), () => Part(NewGame + "Top").gameObject.activeSelf, "the story mode's card");
            await Settle();

            await ClickUntil(Part("Window/Foot/Go"), () => V2Campaign.Active && V2Campaign.Active.Running, "the empty slot's Start starts the story");
            var hud = Object.FindAnyObjectByType<V2Hud>();
            await Until(() => hud.xpLabel && !hud.xpLabel.gameObject.activeSelf, 5f, "the story mode's HUD shows no level");

            Assert.That(calls.Distinct(), Is.EqualTo(new[] { "Bind Story 0" }), "the slot is bound and nothing is deleted");
            Assert.That(game.CampaignRunning, Is.True, "the V2 day campaign runs");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task NewGame_AFilledSlot_AsksOnce_ThenIsDeletedBoundAndStarted()
        {
            await ReadyScene();
            var full = new[] { Story(0, 3, SavePhase.Morning, 30, false, false, 0), Story(1, 6, SavePhase.Night, 60, true, true, 8), Story(2, 1, SavePhase.Afternoon, 10, false, false, 0) };
            Use(full, EmptySlots(SaveMode.Open));
            V5Title.ReloadForTests();
            await Settle();
            await ShowWindow("NewGame", V5Title.NewGameTab);
            await ClickUntil(Part(NewGame + "StoryMode"), () => Part(NewGame + "Top").gameObject.activeSelf, "the story mode's card");
            await Settle();

            Assert.That(Words(NewGame + "Top/Line").text, Is.EqualTo(ArenaText.Pair("存档栏位已满，选一个存档覆盖", "Every slot is in use; choose one to overwrite")),
                "every slot taken: one line says so");
            Assert.That(Part("Window/Foot/Go").GetComponent<Button>().interactable, Is.False, "nothing starts before a slot is chosen");

            Click(Part(NewGame + "Slot2"));
            await Frames(1);
            Assert.That(Words("Window/Foot/Go/Label").text, Is.EqualTo(ArenaText.Pair("覆盖并开始新游戏", "Overwrite and Start")), "a filled slot is overwritten");
            Click(Part("Window/Foot/Go"));
            await Frames(1);
            Assert.That(Words(NewGame + "Top/Line").text, Is.EqualTo(ArenaText.Pair("这会删除存档 2 的进度", "This deletes the progress saved in slot 2")), "after one more question");
            Assert.That(Part("Window/Foot/Confirm").gameObject.activeSelf && Part("Window/Foot/Cancel").gameObject.activeSelf, Is.True, "answered in the window");
            Assert.That(calls, Is.Empty, "nothing is deleted yet");

            Click(Part("Window/Foot/Cancel"));
            await Frames(1);
            Assert.That(Part("Window/Foot/Go").gameObject.activeSelf, Is.True, "Cancel takes the question back");
            Assert.That(calls, Is.Empty, "and deletes nothing");

            // Start, then confirm, until the run's commands are taken (the menus' input lock after the title opens).
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!(V2Campaign.Active && V2Campaign.Active.Running))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "the confirmed overwrite starts the story");
                var confirm = Part("Window/Foot/Confirm");
                Click(confirm.gameObject.activeInHierarchy ? confirm : Part("Window/Foot/Go"));
                await Frames(1);
            }
            Assert.That(calls.Take(2), Is.EqualTo(new[] { "Delete Story 1", "Bind Story 1" }), "the old save is deleted, then the slot bound");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task OpenMode_TheClassicRun_KeepsItsLevelLine()
        {
            var game = await ReadyScene();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (game.RunPhase != ArenaUiState.SupplyDrop)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "the classic run starts");
                game.ExecuteUiCommand(ArenaUiCommand.Start);
                await Awaitable.NextFrameAsync();
            }
            var hud = Object.FindAnyObjectByType<V2Hud>();
            await Until(() => hud.xpLabel && hud.xpLabel.gameObject.activeSelf, 5f, "the open mode's HUD keeps its level line");
        }

        // ───── Helpers ─────

        static Transform Title() => GameObject.Find("V2Canvas/TitleScreen/Content/" + V5Title.RootName).transform;

        static RectTransform Part(string path)
        {
            var part = Title().Find(path);
            Assert.That(part, Is.Not.Null, $"the title has {path}");
            return (RectTransform)part;
        }

        static Text Words(string path) => Part(path).GetComponent<Text>();

        static SaveSummary[] EmptySlots(SaveMode mode) => Enumerable.Range(0, ArenaSaves.SlotCount).Select(i => new SaveSummary { mode = mode, slot = i }).ToArray();

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f,
                "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>Waits for the title's window to stop moving (its parts slide in), then a frame.</summary>
        static async Task Settle()
        {
            await Until(() => V5Title.Active && V5Title.Active.Settled, 3f, "the title's window settles");
            await Awaitable.NextFrameAsync();
        }

        /// <summary>Clicks a menu item until its window shows.</summary>
        static async Task ShowWindow(string item, int tab)
        {
            await ClickUntil(Part("Menu/" + item), () => V5Title.Active.Tab == tab, $"{item} shows its window");
            await Settle();
        }

        /// <summary>What the pointer lands on over the middle of this rect: the top EventSystem raycast, as a player's click.</summary>
        static GameObject TopHit(RectTransform rect, out PointerEventData pointer)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        /// <summary>A real pointer click: the button has to be the one on top where it is drawn.</summary>
        static void Click(RectTransform button)
        {
            var hit = TopHit(button, out var pointer);
            Assert.That(hit, Is.Not.Null, $"{button.name} is under the pointer");
            var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            Assert.That(receiver, Is.EqualTo(button.gameObject), $"a click on {button.name} reaches it, not {hit.name}");
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>Clicks until done holds (the run's commands wait out the menus' input lock after the title opens).</summary>
        static async Task ClickUntil(RectTransform button, Func<bool> done, string what, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + what);
                if (button && button.gameObject.activeInHierarchy) Click(button);
                await Awaitable.NextFrameAsync();
            }
        }

        /// <summary>The part's rectangle in the given space.</summary>
        static Rect Box(Transform part, Transform space)
        {
            var corners = new Vector3[4];
            ((RectTransform)part).GetWorldCorners(corners);
            Vector2 a = space.InverseTransformPoint(corners[0]), b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
        }
    }
}
