using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class ControlsTableTest
    {
        const string SceneCn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const string SceneEn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        static readonly Regex Tags = new("<[^<>]+>");

        [SetUp]
        public void SetUp() => V2Tutorial.ForgetShown();

        [TearDown]
        public void TearDown()
        {
            if (V2Tutorial.IsOpen) V2Tutorial.Close();
            V2Tutorial.ForgetShown();
            Time.timeScale = 1f;
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task Chinese_FirstRun_ShowsThePages_FrozenUntilTheLastOne_AndARestartDoesNot() => await FirstRunShowsThePages(SceneCn);

        [Test, BuildScene(SceneEn), Category("Integration"), Timeout(90000)]
        public async Task English_FirstRun_ShowsThePages_FrozenUntilTheLastOne_AndARestartDoesNot() => await FirstRunShowsThePages(SceneEn);

        static async Task FirstRunShowsThePages(string scene)
        {
            await SceneManagerHelper.LoadSceneAsync(scene);
            var game = await ReadyScene();
            var title = Screen(ArenaUiState.Title);
            Assert.That(title.GetComponentInChildren<V2ControlsTable>(true), Is.Null, "the title keeps no controls table");

            await DispatchUntil(Object.FindAnyObjectByType<V2Hud>(), () => game.RunPhase != ArenaUiState.Title);
            await Until(() => V2Tutorial.IsOpen, 3f, "the pages open once the run is in the arena");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.SupplyDrop));
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the arena is frozen while they are open");

            var lines = new List<string>();
            for (int i = 0; i < V2Tutorial.PageCount; i++)
            {
                Assert.That(V2Tutorial.Page, Is.EqualTo(i));
                await Awaitable.NextFrameAsync();
                Canvas.ForceUpdateCanvases();
                var line = GameObject.Find("HowToPlay/Panel/Line").GetComponent<UnityEngine.UI.Text>();
                Assert.That(line.text, Is.Not.Empty, $"page {i + 1} has its line");
                Assert.That(line.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(line.text.Length), $"page {i + 1}: \"{line.text}\" is not cut off");
                lines.Add(line.text);
                Click(TutorialButton("Next"));
            }
            Assert.That(lines, Has.Some.Contains("Tab"), "a page teaches putting the card panel away");
            Assert.That(lines, Has.Some.Contains("Esc"), "a page teaches the pause screen");
            Assert.That(V2Tutorial.IsOpen, Is.False, "the last page closes them");
            Assert.That(Time.timeScale, Is.EqualTo(game.CanPlayerMove || game.BuildTimeFlows ? 1f : 0f), "and the arena gets its own time back");
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            Assert.That(V2Tutorial.IsOpen, Is.False, "once in a run");

            await SceneManagerHelper.LoadSceneAsync(scene);
            game = await ReadyScene();
            await DispatchUntil(Object.FindAnyObjectByType<V2Hud>(), () => game.RunPhase != ArenaUiState.Title);
            for (int i = 0; i < 10; i++) await Awaitable.NextFrameAsync();
            Assert.That(V2Tutorial.IsOpen, Is.False, "a later run goes straight to the crate");
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task ARunStartedByCode_HasNoPages_AndSkipClosesThemAtOnce()
        {
            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            for (int i = 0; i < 10; i++) await Awaitable.NextFrameAsync();
            Assert.That(V2Tutorial.IsOpen, Is.False, "a run started by code (the other tests) is not interrupted");

            V2Tutorial.Show(null);
            Assert.That(V2Tutorial.IsOpen && Time.timeScale == 0f, Is.True);
            await Awaitable.NextFrameAsync();
            Click(TutorialButton("Skip"));
            Assert.That(V2Tutorial.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(game.CanPlayerMove || game.BuildTimeFlows ? 1f : 0f), "closing gives the arena its own time back at once");
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task Chinese_Pause_ShowsTheWholeTable_AndOpensThePages() => await PauseShowsTheTable(SceneCn);

        [Test, BuildScene(SceneEn), Category("Integration"), Timeout(90000)]
        public async Task English_Pause_ShowsTheWholeTable_AndOpensThePages() => await PauseShowsTheTable(SceneEn);

        static async Task PauseShowsTheTable(string scene)
        {
            await SceneManagerHelper.LoadSceneAsync(scene);
            var game = await ReadyScene();
            await StartRun(game);
            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            var paused = Screen(ArenaUiState.Paused);
            AssertTable(paused);

            var howTo = paused.transform.Find("Content/HowToPlay");
            Assert.That(howTo && howTo.gameObject.activeInHierarchy, Is.True, "the pause screen has the tutorial's button");
            Assert.That(howTo.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo(ArenaText.Pair("HOW TO PLAY","HOW TO PLAY")));
            Click((RectTransform)howTo);
            Assert.That(V2Tutorial.IsOpen && V2Tutorial.HoldsInput, Is.True, "it opens the pages, which hold the game's keys");
            await Awaitable.NextFrameAsync();
            foreach (var name in V2Hud.PauseButtons)
            {
                var hit = TopHit((RectTransform)paused.transform.Find("Content/" + name), out _);
                Assert.That(hit && hit.GetComponentInParent<V2Tutorial>(), Is.True, $"the pages cover {name}, so a click there stays on them");
            }
            V2Tutorial.Close();
            Assert.That(V2Tutorial.HoldsInput, Is.True, "the closing frame is held too, so its Esc does not also resume");
            await Awaitable.NextFrameAsync();
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Paused), "closing them goes back to the pause screen");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "still paused");

            await CommandUntil(game, ArenaUiCommand.Resume, () => game.RunPhase == ArenaUiState.WaveActive);
            await Awaitable.NextFrameAsync();
            Assert.That(paused.gameObject.activeInHierarchy, Is.False, "the table goes away with the pause screen");
        }

        internal static async Task PauseCoversTheCrate(GameManager game)
        {
            var hud = Object.FindAnyObjectByType<V2Hud>();
            var crate = Object.FindAnyObjectByType<V3SupplyDropPresentation>();
            Assert.That(crate.overlayRoot.gameObject.activeInHierarchy, Is.True, "the crate and its hint are up");
            hud.DamageNumber(game.player.Position, 5f, Color.white, false);
            hud.FlashDamage();

            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            var paused = Screen(ArenaUiState.Paused).transform;
            Assert.That(crate.overlayRoot.gameObject.activeInHierarchy, Is.True, "the crate waits under the panel");
            Assert.That(hud.damageLabels.Any(label => label.gameObject.activeInHierarchy), Is.True, "the number is still up");
            foreach (var part in new[] { crate.overlayRoot, hud.damageLabels[0].transform.parent, hud.damageFlash.transform })
                Assert.That(part.GetSiblingIndex(), Is.LessThan(paused.GetSiblingIndex()), $"{part.name} draws under the pause panel");
            var canvas = paused.parent;
            for (int i = paused.GetSiblingIndex() + 1; i < canvas.childCount; i++)
                foreach (var graphic in canvas.GetChild(i).GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    Assert.That(graphic.gameObject.activeInHierarchy && graphic.enabled && graphic.color.a > 0f, Is.False, $"{graphic.name} under {canvas.GetChild(i).name} draws over the pause panel");

            await CommandUntil(game, ArenaUiCommand.Resume, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => !paused.gameObject.activeInHierarchy, 2f, "the pause screen goes at the HUD's next refresh");

            float deadline = Time.realtimeSinceStartup + 3f;
            while (game.RunPhase == ArenaUiState.SupplyDrop)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("The click never opened the crate.");
                await Awaitable.NextFrameAsync();
                Click((RectTransform)crate.openButton.transform);
            }
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task T_DoesNothingWithoutAPick_AndOpensTheOneEscKeptForLater()
        {
            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();
            await StartRun(game);
            await Until(() => game.CanUseCombatInput, 3f, "the fight takes input");
            Assert.That(game.progression.BankedDrafts, Is.Zero);
            game.PressKey(KeyCode.T);
            await Awaitable.NextFrameAsync();
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveActive), "no pick kept, so T has nothing to open");

            game.progression.GainXp(game.progression.XpRequired);
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "the level opens a pick");
            await KeyUntil(game, KeyCode.Escape, () => game.RunPhase == ArenaUiState.WaveActive);
            Assert.That(game.progression.BankedDrafts, Is.EqualTo(1), "Esc keeps it for later");
            Assert.That(game.UiSnapshot.hint, Does.Contain("T"), "the screen says T opens it");
            await KeyUntil(game, KeyCode.T, () => game.RunPhase == ArenaUiState.LevelUp);
        }

        static async Task KeyUntil(GameManager game, KeyCode key, Func<bool> completed)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"{key} did not answer in {game.RunPhase}.");
                game.PressKey(key);
                await Awaitable.NextFrameAsync();
            }
        }

        static void AssertTable(V2UiScreen screen)
        {
            var table = screen.GetComponentInChildren<V2ControlsTable>(true);
            Assert.That(table && table.gameObject.activeInHierarchy, Is.True, "the pause screen shows the controls table");

            Assert.That(table.keys.Select(key => key.text).ToArray(), Is.EqualTo(new[]
            {
                "WASD", ArenaText.Pair("Mouse","Mouse"), ArenaText.Pair("Left mouse button","Left mouse button"), ArenaText.Pair("Right mouse button","Right mouse button"),
                "Shift", "1 / 2 / 3", "E", "Esc", "R", "T", "Tab",
            }));
            foreach (var key in new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Escape, KeyCode.R, KeyCode.T, KeyCode.Tab })
                Assert.That(GameManager.ShownKeyCodes, Does.Contain(key), "the table names only keys the game reads");

            Canvas.ForceUpdateCanvases();
            var bounds = Bounds((RectTransform)screen.transform.Find("Content"));
            foreach (var label in table.keys.Concat(table.actions))
            {
                string plain = Tags.Replace(label.text, "");
                Assert.That(plain, Is.Not.Empty);
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(plain.Length), $"\"{plain}\" is not cut off");
                var box = Bounds(label.rectTransform);
                Assert.That(bounds.Contains(box.min) && bounds.Contains(box.max), Is.True, $"\"{plain}\" stays inside the panel");
            }
        }

        static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x - 1f, corners[0].y - 1f, corners[2].x + 1f, corners[2].y + 1f);
        }

        static V2UiScreen Screen(ArenaUiState state) =>
            Object.FindAnyObjectByType<V2Hud>().screens.First(s => s && s.state == state);

        static RectTransform TutorialButton(string name) => (RectTransform)GameObject.Find("HowToPlay/Panel/" + name).transform;

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

        static void Click(RectTransform button)
        {
            var hit = TopHit(button, out var pointer);
            Assert.That(hit, Is.Not.Null, $"{button.name} is under the pointer");
            var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            Assert.That(receiver, Is.EqualTo(button.gameObject), $"a click on {button.name} reaches it, not {hit.name}");
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f,
                "scene, localization and map initialize");
            return GameManager.Instance;
        }

        static async Task StartRun(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) WorldMap.Active.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task DispatchUntil(V2Hud hud, Func<bool> completed)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("The title's button timed out.");
                hud.Dispatch(ArenaUiCommand.Start);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
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
    }
}
