using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class EditionArtPauseMusicTest
    {
        const string SceneCn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const string SceneEn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        static readonly FieldInfo ChoiceInitialized = typeof(Art).GetField("v2ChoiceInitialized", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo ToggleChoice = typeof(Art).GetField("toggleChoice", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly PropertyInfo IllustratedOnly = typeof(Art).GetProperty(nameof(Art.IllustratedOnly), BindingFlags.Public | BindingFlags.Static);
        bool savedArt, savedInitialized, savedIllustratedOnly;
        bool? savedToggleChoice;
        WaveSet originalWaves, runtimeWaves;

        [SetUp]
        public void SetUp()
        {
            Assume.That(ChoiceInitialized != null && ToggleChoice != null && IllustratedOnly != null, Is.True, "Art keeps its edition state");
            savedArt = Art.Enabled;
            savedInitialized = (bool)ChoiceInitialized.GetValue(null);
            savedToggleChoice = (bool?)ToggleChoice.GetValue(null);
            savedIllustratedOnly = Art.IllustratedOnly;
        }

        [TearDown]
        public void TearDown()
        {
            Art.Enabled = savedArt;
            ChoiceInitialized?.SetValue(null, savedInitialized);
            ToggleChoice?.SetValue(null, savedToggleChoice);
            IllustratedOnly?.SetValue(null, savedIllustratedOnly);
            if (runtimeWaves)
            {
                if (GameManager.Instance && GameManager.Instance.waves) GameManager.Instance.waves.waveSet = originalWaves;
                Object.Destroy(runtimeWaves);
            }

            originalWaves = runtimeWaves = null;
            Time.timeScale = 1f;
        }

        [Test]
        public void EnsureV2Default_ChineseEdition_TurnsTheArtOnWhateverWasKept_AndToggleCannotTurnItOff()
        {
            ChoiceInitialized.SetValue(null, true);
            ToggleChoice.SetValue(null, null);
            Art.Enabled = false;                                                                         

            Art.EnsureV2Default(true);
            bool started = Art.Enabled;
            Art.Toggle();

            Assert.That(started, Is.True, "the Chinese edition starts illustrated");
            Assert.That(Art.IllustratedOnly, Is.True, "it is the illustrated-only edition");
            Assert.That(Art.Enabled, Is.True, "its switch does nothing");
        }

        [Test]
        public void EnsureV2Default_EnglishEdition_StartsInWhitebox_AndGetsItsOwnChoiceBackAfterTheChineseEdition()
        {
            ChoiceInitialized.SetValue(null, false);
            ToggleChoice.SetValue(null, null);
            Art.Enabled = true;

            Art.EnsureV2Default();
            bool first = Art.Enabled;
            Art.EnsureV2Default(true);
            Art.EnsureV2Default(false);
            bool backFromChinese = Art.Enabled;
            Art.Toggle();
            bool switched = Art.Enabled;
            Art.EnsureV2Default(true);
            Art.EnsureV2Default(false);

            Assert.That(first, Is.False, "the first English run starts in whitebox");
            Assert.That(backFromChinese, Is.False, "after the Chinese edition the English one is in whitebox again, as it was left");
            Assert.That(switched, Is.True, "its title switch works");
            Assert.That(Art.Enabled, Is.True, "and the illustrated choice is kept across another visit to the Chinese edition");
            Assert.That(Art.IllustratedOnly, Is.False, "the English edition is not illustrated-only");
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task ChineseEdition_WhiteboxLeftByAnEarlierRun_StartsIllustrated_AndTheTitleCannotSwitchIt()
        {
            ChoiceInitialized.SetValue(null, true);
            Art.Enabled = false;                                                                                  

            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();
            bool started = Art.Enabled, shown = game.UiSnapshot.illustrated, everWhitebox = false;

            float until = Time.realtimeSinceStartup + .8f;
            while (Time.realtimeSinceStartup < until)
            {
                game.ExecuteUiCommand(ArenaUiCommand.ToggleArt);
                everWhitebox |= !Art.Enabled;
                await Awaitable.NextFrameAsync();
            }

            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);

            Assert.That(started, Is.True, "the Chinese edition starts illustrated");
            Assert.That(shown, Is.True, "and tells the view so");
            Assert.That(everWhitebox, Is.False, "the art command never switches it to whitebox");
            Assert.That(Art.Enabled, Is.True, "the run is illustrated");
        }

        [Test, BuildScene(SceneEn), Category("Integration"), Timeout(60000)]
        public async Task EnglishEdition_FirstRun_StartsInWhitebox_AndItsTitleSwitchesTheArt()
        {
            ChoiceInitialized.SetValue(null, false);
            ToggleChoice.SetValue(null, null);
            Art.Enabled = true;

            await SceneManagerHelper.LoadSceneAsync(SceneEn);
            var game = await ReadyScene();
            bool started = Art.Enabled;
            await CommandUntil(game, ArenaUiCommand.ToggleArt, () => Art.Enabled);

            Assert.That(started, Is.False, "the English edition still starts in whitebox");
            Assert.That(Art.IllustratedOnly, Is.False, "it keeps its title switch");
            Assert.That(game.UiSnapshot.illustrated, Is.True, "the switch turns the illustrated art on");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(120000)]
        public async Task BuildPanelOpen_Esc_PausesTheRunAndItsClocks_EscAgainResumesThePanelOrTheWalk()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 1.5f);
            await ReachBuildCrate(game);
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            Assume.That(game.BuildTimeFlows && Time.timeScale == 1f, Is.True, "the build phase's time flows with the armory open");
            Assume.That(game.skills.Learn(SkillId.Overcharge), Is.True, "the player learns a skill");
            var slot = game.skills.Slots[0];
            game.selectionTimeLimit = 60f;                                           
            game.buildTimeLeft = 60f;
            using var keyboard = new TestKeyboard();

            slot.CooldownLeft = 5f;
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses with the armory open");
            float cooldown = slot.CooldownLeft, clock = game.buildTimeLeft;
            await RealSeconds(.6f);
            Assert.That(Time.timeScale, Is.Zero, "the world stops");
            Assert.That(slot.CooldownLeft, Is.EqualTo(cooldown), "the timers wait");
            Assert.That(game.buildTimeLeft, Is.EqualTo(clock), "the build clock waits");
            Assert.That(ScreenShown(ArenaUiState.Paused) && !ScreenShown(ArenaUiState.Armory), Is.True, "the pause menu replaces the panel");

            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase != ArenaUiState.Paused, "Esc resumes");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Armory), "the armory panel is back");
            Assert.That(game.BuildTimeFlows && Time.timeScale == 1f, Is.True, "with time flowing again");
            float resumed = slot.CooldownLeft, clockResumed = game.buildTimeLeft;
            await ForGameSeconds(.4f, null);
            Assert.That(slot.CooldownLeft, Is.LessThan(resumed - .2f), "the timers run again");
            Assert.That(game.buildTimeLeft, Is.LessThan(clockResumed - .2f), "and so does the build clock");

            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => game.UiSnapshot.earlyStartConfirmation);
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses over the question");
            Assert.That(game.UiSnapshot.earlyStartConfirmation, Is.False, "the early-start question is closed");
            await CommandFor(game, ArenaUiCommand.ConfirmEarlyStart, .6f);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Paused), "its confirm does nothing on the pause menu");
            Assert.That(game.waves.Phase, Is.EqualTo(WavePhase.WaitingBuild), "the next wave waits");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Armory, "Esc resumes the armory");

            await keyboard.TapUntil(KeyCode.Tab, () => game.BuildWalking, "Tab puts the panel away");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses the walk");
            Assert.That(Time.timeScale, Is.Zero, "the walk stops");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase != ArenaUiState.Paused, "Esc resumes the walk");
            Assert.That(game.BuildWalking, Is.True, "the panel stays put away");
            Assert.That(game.CanPlayerMove && Time.timeScale == 1f, Is.True, "and the player is free to walk");

            await keyboard.TapUntil(KeyCode.Tab, () => !game.BuildWalking, "Tab brings the panel back");
            await CommandUntil(game, ArenaUiCommand.SkipArmory, () => game.RunPhase != ArenaUiState.Armory);
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Ready), "no card pick is banked, so the build is ready");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses the ready panel");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase != ArenaUiState.Paused, "Esc resumes it");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Ready), "the ready panel is back");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(120000)]
        public async Task WaveEndCrateAndBuildPanels_TheMusicPlaysOn_OnlyThePauseMenuHoldsIt()
        {
            var game = await ReadyScene();
            var audio = V3WorldAudio.Instance;
            Assume.That(audio && audio.musicSource, Is.True, "the scene has its music channel");
            var music = audio.musicSource;
            ShortenWaves(game, 1.5f);
            await StartRun(game);
            float full = audio.musicGain * audio.masterGain;
            await Until(() => music.isPlaying && music.volume >= full * .99f, 5f, "the combat music plays in the wave");

            bool watching = true, stopped = false, restarted = false;
            float quietest = float.MaxValue;
            async Task Watch()
            {
                float last = music.time;
                while (watching && music)
                {
                    await Awaitable.NextFrameAsync();
                    if (!watching || !music) break;
                    stopped |= !music.isPlaying;
                    quietest = Mathf.Min(quietest, music.volume);
                    float now = music.time;
                    if (now + .05f < last && music.clip && last < music.clip.length - 1f) restarted = true;
                    last = now;
                }
            }

            var watch = Watch();
            try
            {
                await ClearTheWave(game);
                await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 10f, "the build phase's supply crate lands");
                await RealSeconds(.6f);
                await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
                await RealSeconds(1f);
                await CommandUntil(game, ArenaUiCommand.TakeArmory, () => game.RunPhase != ArenaUiState.Armory, 0);
                Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.WeaponIntro), "the armory weapon's card opens");
                await RealSeconds(.6f);
                await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
                await RealSeconds(1f);
            }
            finally
            {
                watching = false;
            }

            await watch;
            Assume.That(game.RunPhase is ArenaUiState.Draft or ArenaUiState.Ready, Is.True, "a build panel is open");
            Assert.That(stopped, Is.False, "the music never stops between the wave and the build panels");
            Assert.That(quietest, Is.GreaterThanOrEqualTo(full * .99f), "nor fades");
            Assert.That(restarted, Is.False, "nor starts over");

            using var keyboard = new TestKeyboard();
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses the build panel");
            await Awaitable.NextFrameAsync();
            bool heldPlaying = music.isPlaying;
            float heldAt = music.time;
            await RealSeconds(.5f);
            float heldLater = music.time;
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase != ArenaUiState.Paused, "Esc resumes");
            await Awaitable.NextFrameAsync();

            Assert.That(heldPlaying, Is.False, "the pause menu holds the music");

            Assert.That(heldLater, Is.EqualTo(heldAt).Within(.1f), "where it was");
            Assert.That(music.isPlaying, Is.True, "Esc again plays it on");
            Assert.That(music.time, Is.GreaterThanOrEqualTo(heldAt - .01f), "from there, not from the start");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(40000)]
        public async Task SpitterSpit_FliesTenCellsASecond()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[0].DiscardWithoutReward();
            targets[1].DiscardWithoutReward();
            var spitter = targets[2];                                                                       
            var data = game.waves.DefinitionOf(EnemyKind.Spitter);
            spitter.Configure(data, 1f, false);
            spitter.enabled = true;

            EnemyBolt spit = null;
            await UntilFixed(() => (spit = game.waves.preparedBolts.FirstOrDefault(b => b && b.gameObject.activeInHierarchy && b.Hostile && !b.IsBossShot)) != null,
                6f, "the Spitter spits");

            await Awaitable.FixedUpdateAsync();
            Vector3 from = spit.transform.position;
            float start = Time.fixedTime;
            for (int step = 0; step < 10 && spit.gameObject.activeInHierarchy; step++) await Awaitable.FixedUpdateAsync();
            float flown = Query.Flat(spit.transform.position - from).magnitude, seconds = (Time.fixedTime - start) * game.WorldTimeScale;
            Assume.That(spit.gameObject.activeInHierarchy && seconds > .05f, Is.True, "the spit is still in the air");

            Assert.That(data.projectileSpeed, Is.EqualTo(10f), "the saved Spitter spits at 10 cells a second");
            Assert.That(flown / seconds, Is.EqualTo(10f).Within(.5f), "and its spit flies that fast");
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f,
                "scene, localization and map initialize");
            return GameManager.Instance;
        }

        static bool ScreenShown(ArenaUiState state)
        {
            var hud = Object.FindAnyObjectByType<V2Hud>();
            return hud && hud.screens.Any(s => s && s.state == state && s.gameObject.activeInHierarchy);
        }

        void ShortenWaves(GameManager game, float duration)
        {
            originalWaves = game.waves.waveSet;
            runtimeWaves = Object.Instantiate(originalWaves);
            foreach (var wave in runtimeWaves.waves) wave.duration = duration;
            game.waves.waveSet = runtimeWaves;
        }

        static async Task StartRun(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            await UntilWaveActive(game);
            foreach (var hole in WorldMap.Active.holes)
                if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0) hole.Feed(1, game.waves.WaveIndex + 1);
            await Awaitable.NextFrameAsync();
            await UntilWaveActive(game);
        }

        static async Task ClearTheWave(GameManager game)
        {
            await Until(() => game.waves.TimeExpired, 10f, "the shortened wave runs out");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (game.RunPhase == ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never ended; {game.waves.Remaining} enemies remain.");
                foreach (var enemy in game.waves.Alive.ToArray())
                {
                    if (!enemy || !enemy.IsAlive) continue;
                    enemy.SuppressRewards = true;
                    enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                }

                await Awaitable.NextFrameAsync();
            }
        }

        static async Task ReachBuildCrate(GameManager game)
        {
            await StartRun(game);
            await ClearTheWave(game);
            await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 10f, "the build phase's supply crate lands");
        }

        static async Task UntilWaveActive(GameManager game)
        {
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

        static async Task CommandFor(GameManager game, ArenaUiCommand command, float seconds, int index = -1)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
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

        static async Task UntilFixed(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.FixedUpdateAsync();
            }
        }

        static async Task ForGameSeconds(float seconds, Action eachStep)
        {
            float until = Time.time + seconds, deadline = Time.realtimeSinceStartup + seconds + 10f;
            while (Time.time < until)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Game time stood still for {seconds + 10f:0.#} s.");
                await Awaitable.NextFrameAsync();
                eachStep?.Invoke();
            }
        }

        static async Task RealSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
        }
    }
}
