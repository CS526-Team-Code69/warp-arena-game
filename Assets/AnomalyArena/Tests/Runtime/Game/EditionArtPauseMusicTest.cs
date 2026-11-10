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
    /// <summary>The art, the pause, the music and the Spitter; the art switch as changed on
    /// 2026-11-06 and 2026-11-10 (one game in two languages): the first run of a play starts illustrated when the game opened in Chinese and
    /// in whitebox when it opened in English, whatever an earlier play left behind; the title switches either way, and once the player has
    /// switched, later runs keep that choice in either language. The enemy lab shows only the illustrated art. With a
    /// build panel open Esc pauses the run, its timers and the build clock, the early-start question closes so the pause menu cannot start
    /// the wave, and Esc again brings back the same panel, or the walk with the panel put away. The combat music plays only while the
    /// player can move, as before 10-26: the wave's end, an opened supply crate, the build panels and a mid-wave card pick
    /// fade it out and stop it, and once the player can move again it starts from the top; only the pause menu holds it where it is.
    /// The Spitter's spit flies 10 cells a second (it was 14).</summary>
    [TestFixture]
    public class EditionArtPauseMusicTest
    {
        // Since 2026-11-10 one scene serves both languages; the English cases load it with an English start (TestStartLanguage).
        const string SceneCn = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const string SceneLab = "Assets/AnomalyArena/Scenes/EnemyLab.unity";
        static readonly FieldInfo ChoiceInitialized = typeof(Art).GetField("v2ChoiceInitialized", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly PropertyInfo IllustratedOnly = typeof(Art).GetProperty(nameof(Art.IllustratedOnly), BindingFlags.Public | BindingFlags.Static);
        // Every static field of Art (the switch, whether the first run took its default, the choice held while the lab runs), saved before
        // each test and put back after it.
        static readonly FieldInfo[] ArtState = typeof(Art).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => !f.IsLiteral && !f.IsInitOnly && f.FieldType != typeof(MaterialPropertyBlock)).ToArray();
        object[] savedArtState;
        WaveSet originalWaves, runtimeWaves;

        [SetUp]
        public void SetUp()
        {
            Assume.That(ChoiceInitialized != null && IllustratedOnly != null, Is.True, "Art keeps whether the first run took its default");
            savedArtState = ArtState.Select(f => f.GetValue(null)).ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            if (savedArtState != null)
                for (int i = 0; i < ArtState.Length; i++) ArtState[i].SetValue(null, savedArtState[i]);
            savedArtState = null;
            TestStartLanguage.Reset();
            if (runtimeWaves)
            {
                if (GameManager.Instance && GameManager.Instance.waves) GameManager.Instance.waves.waveSet = originalWaves;
                Object.Destroy(runtimeWaves);
            }

            originalWaves = runtimeWaves = null;
            Time.timeScale = 1f;
        }

        // ───── The hit flash (document 3.6, no scene) ─────

        [Test]
        public void EdgeVignette_ClearCentre_FullEdgesAndCorners()
        {
            var sprite = V2Hud.EdgeVignette();
            try
            {
                var texture = sprite.texture;
                Assert.That(texture.GetPixel(128, 128).a, Is.EqualTo(0f).Within(.01f), "the centre stays clear");
                Assert.That(texture.GetPixel(178, 128).a, Is.EqualTo(0f).Within(.01f), "and so does the inner 40%");
                Assert.That(texture.GetPixel(255, 128).a, Is.GreaterThan(.95f), "an edge's middle is full");
                Assert.That(texture.GetPixel(0, 0).a, Is.GreaterThan(.95f), "and so are the corners");
            }
            finally { Object.DestroyImmediate(sprite.texture); Object.DestroyImmediate(sprite); }
        }

        // ───── The first run's art (no scene) ─────
        // The whole test run keeps the art the tests were written for (TestStartLanguage); these three test the game's own default, so
        // each switches that off first and the TearDown puts it back.

        [TestCase(true, TestName = "An illustrated play left behind, the first run is in whitebox")]
        [TestCase(false, TestName = "A whitebox play left behind, the first run is in whitebox")]
        public void EnsureV2Default_FirstRun_IsWhiteboxWhateverWasLeft(bool leftIllustrated)
        {
            Art.DefaultForTests = null;
            ChoiceInitialized.SetValue(null, false);
            Art.Enabled = leftIllustrated; // the statics survive a scene reload

            Art.EnsureV2Default();

            Assert.That(Art.Enabled, Is.False, "whitebox is the default in either language");
        }

        [Test]
        public void EnsureV2Default_PlayerSwitched_LaterRunsKeepTheChoice()
        {
            Art.DefaultForTests = null;
            ChoiceInitialized.SetValue(null, false);
            Art.EnsureV2Default();
            bool first = Art.Enabled;
            Art.Toggle(); // the player turns the illustrated art on at the title

            Art.EnsureV2Default(); // a restart reloads the scene
            bool restarted = Art.Enabled;
            Art.Toggle();
            Art.EnsureV2Default();

            Assert.That(first, Is.False, "the first run starts in whitebox");
            Assert.That(restarted, Is.True, "the switch is kept when the run starts again");
            Assert.That(Art.Enabled, Is.False, "and so is a switch back to whitebox");
            Assert.That(Art.IllustratedOnly, Is.False, "the run scene has its switch");
        }

        [Test]
        public void EnsureV2Default_TheLab_IsOnlyIllustrated_ThenTheRunsGetTheirChoiceBack()
        {
            Art.DefaultForTests = null;
            ChoiceInitialized.SetValue(null, false);
            Art.EnsureV2Default(); // whitebox

            Art.EnsureV2Default(Art.IsIllustratedOnlyScene(Art.EnemyLabScene));
            bool labStarted = Art.Enabled, labOnly = Art.IllustratedOnly;
            Art.Toggle();
            bool labSwitched = Art.Enabled;
            Art.EnsureV2Default(Art.IsIllustratedOnlyScene("ArenaV2"));

            Assert.That(labStarted && labOnly && labSwitched, Is.True, "the enemy lab is illustrated and its switch does nothing");
            Assert.That(Art.Enabled, Is.False, "after the lab the run gets its whitebox back");
            Assert.That(Art.IllustratedOnly, Is.False, "with its switch");
        }

        // ───── The first run's art (the saved scene) ─────

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task OpenedInChinese_FirstRun_StartsInWhiteboxWhateverWasLeft_AndItsTitleSwitchesTheArtOnAndOff()
        {
            Art.DefaultForTests = null; // the game's own default, not the test run's (TestStartLanguage)
            ChoiceInitialized.SetValue(null, false);
            Art.Enabled = true; // an earlier play in the editor left the illustrated art behind
            TestStartLanguage.Use(StartupLanguage.Chinese);

            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();
            bool started = Art.Enabled, shown = game.UiSnapshot.illustrated;
            await CommandUntil(game, ArenaUiCommand.ToggleArt, () => Art.Enabled);
            bool illustratedShown = game.UiSnapshot.illustrated;
            await CommandUntil(game, ArenaUiCommand.ToggleArt, () => !Art.Enabled);

            Assert.That(started, Is.False, "opened in Chinese, the first run starts in whitebox too (the default since 2026-11-10)");
            Assert.That(shown, Is.False, "and tells the view so");
            Assert.That(Art.IllustratedOnly, Is.False, "its title has the switch");
            Assert.That(illustratedShown, Is.True, "the art command switches the illustrated art on");
            Assert.That(game.UiSnapshot.illustrated, Is.False, "and back to whitebox");
        }

        [Test, BuildScene(SceneLab), Category("Integration"), Timeout(60000)]
        public async Task EnemyLab_WhiteboxLeftByAnEarlierRun_StartsIllustrated()
        {
            ChoiceInitialized.SetValue(null, true);
            Art.Enabled = false; // an earlier run left whitebox behind
            IllustratedOnly.SetValue(null, false); // an earlier Chinese-edition test may have left it on

            await SceneManagerHelper.LoadSceneAsync(SceneLab);
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "the lab and localization initialize");
            // The art is set as the game manager starts, a frame or two after it exists.
            await Until(() => Art.IllustratedOnly, 5f, "the lab starts in the illustrated-only mode");

            Assert.That(Art.Enabled, Is.True, "the enemy lab shows the illustrated art (2026-10-27)");
            Assert.That(Art.IllustratedOnly, Is.True, "and keeps it");
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task OpenedInEnglish_FirstRun_StartsInWhitebox_AndItsTitleSwitchesTheArt()
        {
            Art.DefaultForTests = null; // the game's own default
            ChoiceInitialized.SetValue(null, false);
            Art.Enabled = true; // an earlier play in the editor left the illustrated art behind
            TestStartLanguage.Use(StartupLanguage.English);

            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();
            bool started = Art.Enabled;
            await CommandUntil(game, ArenaUiCommand.ToggleArt, () => Art.Enabled);

            Assert.That(started, Is.False, "opened in English, the first run starts in whitebox (the course's default)");
            Assert.That(Art.IllustratedOnly, Is.False, "it keeps its title switch");
            Assert.That(game.UiSnapshot.illustrated, Is.True, "the switch turns the illustrated art on");
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task OpenedInChinese_HasTheMenuLookFromTheTitle()
        {
            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            Assert.That(MenuSkin().Installed, Is.True, "the title has the menu look from the start");
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task OpenedInEnglish_HasTheMenuLookInWhitebox_AndKeepsItWithTheArtOn()
        {
            ChoiceInitialized.SetValue(null, false);
            TestStartLanguage.Use(StartupLanguage.English);

            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();
            bool whitebox = !Art.Enabled, inWhitebox = MenuSkin().Installed;
            await CommandUntil(game, ArenaUiCommand.ToggleArt, () => Art.Enabled);
            await Awaitable.NextFrameAsync();

            Assert.That(whitebox, Is.True, "whitebox is the default");
            Assert.That(inWhitebox, Is.True, "the art switch is not about the menus (2026-10-30)");
            Assert.That(MenuSkin().Installed, Is.True, "and the art on keeps them");
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task OpenedInEnglish_SwitchOnAndOffAtTheTitle_TheTrialPlatformGetsItsSavedMaterialBack()
        {
            ChoiceInitialized.SetValue(null, false);
            TestStartLanguage.Use(StartupLanguage.English);

            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();
            var platform = WorldMap.Active.trialPlatform.GetComponent<Renderer>();
            var saved = platform.sharedMaterial;

            await CommandUntil(game, ArenaUiCommand.ToggleArt, () => Art.Enabled);
            await Until(() => platform.sharedMaterial != saved, 5f, "the art dresses the platform");
            await CommandUntil(game, ArenaUiCommand.ToggleArt, () => !Art.Enabled);
            await Until(() => platform.sharedMaterial == saved, 5f, "whitebox gives the saved material back");

            Assert.That(platform.sharedMaterial, Is.SameAs(saved));
        }

        static V5MenuSkin MenuSkin() => Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include).GetComponent<V5MenuSkin>();

        // ───── Esc with a build panel open ─────

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
            game.selectionTimeLimit = 60f; // a build clock that runs, to see it stop
            game.buildTimeLeft = 60f;
            using var keyboard = new TestKeyboard();

            // The armory panel: Esc pauses the world, the timers and the build clock.
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

            // The early-start question closes on the pause, so the pause menu cannot start the wave. It is asked only while a card pick
            // is unpicked (document 2.3), so one is banked first.
            game.progression.GainXp(game.progression.XpRequired);
            Assume.That(game.progression.BankedDrafts, Is.GreaterThan(0), "a card pick is banked");
            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => game.UiSnapshot.earlyStartConfirmation);
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses over the question");
            Assert.That(game.UiSnapshot.earlyStartConfirmation, Is.False, "the early-start question is closed");
            await CommandFor(game, ArenaUiCommand.ConfirmEarlyStart, .6f);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Paused), "its confirm does nothing on the pause menu");
            Assert.That(game.waves.Phase, Is.EqualTo(WavePhase.WaitingBuild), "the next wave waits");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Armory, "Esc resumes the armory");
            game.progression.ResolvePendingRandom(); // the pick banked for the question is settled, so the build can be ready below

            // Walking the arena with the panel put away: Esc pauses, Esc again walks on.
            await keyboard.TapUntil(KeyCode.Tab, () => game.BuildWalking, "Tab puts the panel away");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses the walk");
            Assert.That(Time.timeScale, Is.Zero, "the walk stops");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase != ArenaUiState.Paused, "Esc resumes the walk");
            Assert.That(game.BuildWalking, Is.True, "the panel stays put away");
            Assert.That(game.CanPlayerMove && Time.timeScale == 1f, Is.True, "and the player is free to walk");

            // The ready panel (no card pick is banked) pauses and comes back the same way.
            await keyboard.TapUntil(KeyCode.Tab, () => !game.BuildWalking, "Tab brings the panel back");
            await CommandUntil(game, ArenaUiCommand.SkipArmory, () => game.RunPhase != ArenaUiState.Armory);
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Ready), "no card pick is banked, so the build is ready");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses the ready panel");
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase != ArenaUiState.Paused, "Esc resumes it");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Ready), "the ready panel is back");
        }

        // ───── The music through the build phase ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(120000)]
        public async Task WaveEndAndBuildPanels_FadeTheMusicOut_WalkingStartsItFromTheTop_ThePauseMenuHoldsIt()
        {
            var game = await ReadyScene();
            var audio = V3WorldAudio.Instance;
            Assume.That(audio && audio.musicSource, Is.True, "the scene has its music channel");
            var music = audio.musicSource;
            ShortenWaves(game, 1.5f);
            await StartRun(game);
            float full = audio.musicGain * audio.masterGain;
            await Until(() => music.isPlaying && music.volume >= full * .99f, 5f, "the combat music plays in the wave");

            // The wave's end holds the player for a second: the music fades out and stops within it.
            await ClearTheWave(game);
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveEnd), "the wave has ended");
            await Until(() => !music.isPlaying, .9f, "the wave's end fades the music out and stops it");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveEnd), "before the build phase opens");

            // While the supply crate comes down the player walks, so the music starts again from the top.
            await Until(() => game.BuildCrateWalking && music.isPlaying && music.volume >= full * .99f, 5f, "the music plays while the crate comes down");
            Assert.That(music.time, Is.LessThan(1.5f), "from the top");

            // The opened crate's panel holds the player: the music fades out and stops.
            await Until(() => game.SupplyDropReady, 10f, "the build phase's supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            await RealSeconds(audio.backgroundFadeSeconds + .4f);
            Assert.That(music.volume, Is.EqualTo(0f).Within(.001f), "the armory panel fades the music out");
            Assert.That(music.isPlaying, Is.False, "and stops it");

            // The panel put away, the player walks the arena and the music starts again from the top.
            using var keyboard = new TestKeyboard();
            await keyboard.TapUntil(KeyCode.Tab, () => game.BuildWalking, "Tab puts the panel away");
            await Until(() => music.isPlaying && music.volume >= full * .99f, 3f, "the music plays on the walk");
            Assert.That(music.time, Is.LessThan(1.5f), "from the top");

            // The pause menu holds the music where it is; Esc again plays it on from there.
            await RealSeconds(.5f);
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses the walk");
            await Awaitable.NextFrameAsync();
            bool heldPlaying = music.isPlaying;
            float heldAt = music.time;
            await RealSeconds(.5f);
            float heldLater = music.time;
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase != ArenaUiState.Paused, "Esc resumes");
            await Awaitable.NextFrameAsync();

            Assert.That(heldPlaying, Is.False, "the pause menu holds the music");
            // The reported position may still catch up by the one audio buffer already sent (about 0.02 s); playing on would move it 0.5 s.
            Assert.That(heldLater, Is.EqualTo(heldAt).Within(.1f), "where it was");
            Assert.That(music.isPlaying, Is.True, "Esc again plays it on");
            Assert.That(music.time, Is.GreaterThanOrEqualTo(heldAt - .01f), "from there, not from the start");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task MidWavePick_TheMusicFadesOutAndStops_ThenStartsAgainFromTheTop()
        {
            var game = await ReadyScene();
            var audio = V3WorldAudio.Instance;
            Assume.That(audio && audio.musicSource, Is.True, "the scene has its music channel");
            var music = audio.musicSource;
            await StartRun(game);
            float full = audio.musicGain * audio.masterGain;
            await Until(() => music.isPlaying && music.volume >= full * .99f && music.time >= 2f, 8f, "the combat music has played a while in the wave");

            // The music plays only while the player can move, like before 10-26: a mid-wave pick fades the music out and
            // stops it (so do the black-hole cutscene and the trial's return); back in the wave it starts again from the top.
            float before = music.time;
            game.progression.GainXp(game.progression.XpRequired);
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "a level gained mid-wave opens its pick");
            await RealSeconds(audio.backgroundFadeSeconds + .4f);
            float stoppedVolume = music.volume;
            bool stoppedPlaying = music.isPlaying;
            await CommandUntil(game, ArenaUiCommand.Cancel, () => game.RunPhase != ArenaUiState.LevelUp);
            await Until(() => music.isPlaying && music.volume >= full * .99f, 3f, "the music comes back in the wave");

            Assert.That(stoppedVolume, Is.EqualTo(0f).Within(.001f), "the pick fades the music out");
            Assert.That(stoppedPlaying, Is.False, "and stops it");
            Assert.That(music.time, Is.LessThan(Mathf.Min(1.5f, before - .3f)), "back in the wave it starts again from the top");
        }

        // ───── The Spitter's spit ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(40000)]
        public async Task SpitterSpit_FliesTenCellsASecond()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[0].DiscardWithoutReward();
            targets[1].DiscardWithoutReward();
            var spitter = targets[2]; // nine cells down the clear lane, inside its 8-10 cell keep-away band
            var data = game.waves.DefinitionOf(EnemyKind.Spitter);
            spitter.Configure(data, 1f, false);
            spitter.enabled = true;

            EnemyBolt spit = null;
            await UntilFixed(() => (spit = game.waves.preparedBolts.FirstOrDefault(b => b && b.gameObject.activeInHierarchy && b.Hostile && !b.IsBossShot)) != null,
                6f, "the Spitter spits");
            // Both readings are taken right after a physics step, so the distance and the time cover the same steps.
            await Awaitable.FixedUpdateAsync();
            Vector3 from = spit.transform.position;
            float start = Time.fixedTime;
            for (int step = 0; step < 10 && spit.gameObject.activeInHierarchy; step++) await Awaitable.FixedUpdateAsync();
            float flown = Query.Flat(spit.transform.position - from).magnitude, seconds = (Time.fixedTime - start) * game.WorldTimeScale;
            Assume.That(spit.gameObject.activeInHierarchy && seconds > .05f, Is.True, "the spit is still in the air");

            Assert.That(data.projectileSpeed, Is.EqualTo(10f), "the saved Spitter spits at 10 cells a second");
            Assert.That(flown / seconds, Is.EqualTo(10f).Within(.5f), "and its spit flies that fast");
        }

        // ───── Helpers ─────

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

        /// <summary>Title → supply crate → Rifle → close its card → the first wave's combat, every black hole fed once so no first-sight
        /// cutscene stops the clock.</summary>
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

        /// <summary>The shortened wave runs out and its survivors fall without rewards, so the wave ends.</summary>
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

        /// <summary>Sends the command every frame for the given real time (longer than any menu input lock).</summary>
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

        /// <summary>Checks completed after every physics step, so a short state (a spit just fired) is not missed between frames.</summary>
        static async Task UntilFixed(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.FixedUpdateAsync();
            }
        }

        /// <summary>Lets this much game time pass, frame by frame. Fails when game time stands still (a frozen screen) instead of
        /// waiting for ever.</summary>
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
