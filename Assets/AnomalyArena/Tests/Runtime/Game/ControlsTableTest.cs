using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class ControlsTableTest
    {
        const string SceneEn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        static readonly Regex Tags = new("<[^<>]+>");

        [TearDown]
        public void TearDown()
        {
            if (V2Tutorial.IsOpen) V2Tutorial.Close();
            Time.timeScale = 1f;
        }

        [Test]
        public void Controls_MatchTheDesignDocument_RowForRow()
        {
            var rows = DocumentRows();

            Assert.That(rows.Length, Is.EqualTo(12), "GDD section 7 lists twelve controls");
            Assert.That(V2Hud.Controls.Select(c => (c.action, c.input)).ToArray(), Is.EqualTo(rows), "the in-game table copies the document");
        }

        [Test]
        public void Split_PutsTheKeyOnTheCap_AndKeepsEveryWord()
        {
            foreach (var (_, input) in V2Hud.Controls)
            {
                var (key, note) = V2ControlsTable.Split(input);
                Assert.That(note == "" ? key : key + " " + note, Is.EqualTo(input));
            }
            Assert.That(V2ControlsTable.Split("E while standing on the portal"), Is.EqualTo(("E", "while standing on the portal")));
        }

        [Test, BuildScene(SceneEn), Category("Integration"), Timeout(90000)]
        public async Task EnglishEdition_StartRun_ShowsHowToPlayInTheArena_FrozenUntilTheLastPage()
        {
            await SceneManagerHelper.LoadSceneAsync(SceneEn);
            var game = await ReadyScene();
            var title = Screen(ArenaUiState.Title);

            Assert.That(title.GetComponentInChildren<V2ControlsTable>(true), Is.Null, "the title keeps no controls table");
            Assert.That(title.transform.Find("Content/Controls").gameObject.activeInHierarchy, Is.True, "and keeps its one-line strip");

            var hud = Object.FindAnyObjectByType<V2Hud>();
            await DispatchUntil(hud, () => game.RunPhase != ArenaUiState.Title);
            Assert.That(V2Tutorial.IsOpen, Is.False, "nothing covers the title");
            await Until(() => V2Tutorial.IsOpen, 3f, "the pages open once the run is in the arena");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.SupplyDrop));
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the arena is frozen while they are open");

            for (int i = 1; i < V2Tutorial.PageCount; i++)
            {
                V2Tutorial.Next();
                Assert.That(V2Tutorial.Page, Is.EqualTo(i));
                await Awaitable.NextFrameAsync();
            }
            V2Tutorial.Next();
            Assert.That(V2Tutorial.IsOpen, Is.False, "the last page closes them");
            Assert.That(Time.timeScale, Is.EqualTo(game.CanPlayerMove || game.BuildTimeFlows ? 1f : 0f), "and the arena gets its own time back");
            await Awaitable.NextFrameAsync();
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            Assert.That(V2Tutorial.IsOpen, Is.False, "once per run");
        }

        [Test, BuildScene(SceneEn), Category("Integration"), Timeout(90000)]
        public async Task EnglishEdition_SkipClosesHowToPlay_AndARunStartedByCodeHasNone()
        {
            await SceneManagerHelper.LoadSceneAsync(SceneEn);
            var game = await ReadyScene();
            var hud = Object.FindAnyObjectByType<V2Hud>();

            await DispatchUntil(hud, () => game.RunPhase != ArenaUiState.Title);
            await Until(() => V2Tutorial.IsOpen, 3f, "the pages open in the arena");
            V2Tutorial.Close();
            Assert.That(V2Tutorial.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(game.CanPlayerMove || game.BuildTimeFlows ? 1f : 0f), "Skip gives the arena its own time back at once");

            await SceneManagerHelper.LoadSceneAsync(SceneEn);
            game = await ReadyScene();
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            for (int i = 0; i < 10; i++) await Awaitable.NextFrameAsync();
            Assert.That(V2Tutorial.IsOpen, Is.False, "a run started by code (the other tests) is not interrupted");
        }

        static async Task DispatchUntil(V2Hud hud, Func<bool> completed)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("START RUN timed out.");
                hud.Dispatch(ArenaUiCommand.Start);
                await Awaitable.NextFrameAsync();
            }
        }

        [Test, BuildScene(SceneEn), Category("Integration"), Timeout(90000)]
        public async Task EnglishEdition_Pause_ShowsTheWholeTable_AndOpensHowToPlay()
        {
            await SceneManagerHelper.LoadSceneAsync(SceneEn);
            var game = await ReadyScene();
            var rows = DocumentRows();

            await StartRun(game);
            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            var paused = Screen(ArenaUiState.Paused);
            Assert.That(paused.body.text, Is.EqualTo("The battlefield and the build clock are paused."));
            AssertTable(paused, rows);

            var howTo = paused.transform.Find("Content/HowToPlay");
            Assert.That(howTo && howTo.gameObject.activeInHierarchy, Is.True, "the pause screen has a HOW TO PLAY button");
            Assert.That(howTo.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("HOW TO PLAY"));
            howTo.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(V2Tutorial.IsOpen && V2Tutorial.HoldsInput, Is.True, "it opens the pages, which hold the game's keys");
            V2Tutorial.Close();
            Assert.That(V2Tutorial.HoldsInput, Is.True, "the closing frame is held too, so its Esc does not also resume");
            await Awaitable.NextFrameAsync();
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Paused), "closing them goes back to the pause screen");

            await CommandUntil(game, ArenaUiCommand.Resume, () => game.RunPhase == ArenaUiState.WaveActive);
            await Awaitable.NextFrameAsync();
            Assert.That(paused.gameObject.activeInHierarchy, Is.False, "the table goes away with the pause screen");
        }

        static void AssertTable(V2UiScreen screen, (string action, string input)[] rows)
        {
            var table = screen.GetComponentInChildren<V2ControlsTable>(true);
            Assert.That(table && table.gameObject.activeInHierarchy, Is.True, $"{screen.state} shows the controls table");
            var shown = table.keys.Zip(table.actions, (key, action) => (key: Tags.Replace(key.text, ""), text: Tags.Replace(action.text, ""))).ToArray();
            Assert.That(shown.Length, Is.EqualTo(rows.Length), $"{screen.state}: one row per control");
            for (int i = 0; i < rows.Length; i++)
            {
                var (key, note) = V2ControlsTable.Split(rows[i].input);
                Assert.That(shown[i].key, Is.EqualTo(key), $"{screen.state} row {i + 1}: key");
                Assert.That(shown[i].text, Is.EqualTo(note == "" ? rows[i].action : rows[i].action + "  " + note), $"{screen.state} row {i + 1}: action");
            }

            Canvas.ForceUpdateCanvases();
            var bounds = Bounds(screen.state == ArenaUiState.Paused ? (RectTransform)screen.transform.Find("Content") : (RectTransform)screen.transform);
            foreach (var label in table.keys.Concat(table.actions))
            {
                string plain = Tags.Replace(label.text, "");
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(plain.Length),
                    $"{screen.state}: \"{plain}\" is not cut off");
                var box = Bounds(label.rectTransform);
                Assert.That(bounds.Contains(box.min) && bounds.Contains(box.max), Is.True, $"{screen.state}: \"{plain}\" stays inside the panel");
            }
        }

        static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x - 1f, corners[0].y - 1f, corners[2].x + 1f, corners[2].y + 1f);
        }

        static (string action, string input)[] DocumentRows()
        {
            var lines = File.ReadAllLines(Path.Combine(Application.dataPath, "..", "Docs", "GDD.md"));
            int start = Array.FindIndex(lines, l => l.StartsWith("## ") && l.Contains("Controls"));
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "the GDD has a Controls section");
            return lines.Skip(start + 1)
                .TakeWhile(l => !l.StartsWith("## "))
                .Where(l => l.StartsWith("|") && !l.StartsWith("|---") && !l.StartsWith("| Action"))
                .Select(l => l.Trim('|').Split('|'))
                .Select(c => (c[0].Trim(), c[1].Trim()))
                .ToArray();
        }

        static V2UiScreen Screen(ArenaUiState state) =>
            Object.FindAnyObjectByType<V2Hud>().screens.First(s => s && s.state == state);

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
