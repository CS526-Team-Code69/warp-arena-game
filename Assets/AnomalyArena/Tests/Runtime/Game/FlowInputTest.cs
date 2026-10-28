using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class FlowInputTest
    {
        const string SceneCn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const string SceneEn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        static readonly KeyCode[] SpaceAndEnter = { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter };
        WaveSet originalWaves, runtimeWaves;
        TestKeyboard keyboard;
        bool? artWas;

        [TearDown]
        public void TearDown()
        {
            keyboard?.Dispose();
            keyboard = null;
            if (artWas.HasValue) Art.Enabled = artWas.Value;
            artWas = null;
            if (runtimeWaves)
            {
                if (GameManager.Instance && GameManager.Instance.waves) GameManager.Instance.waves.waveSet = originalWaves;
                Object.Destroy(runtimeWaves);
            }
            originalWaves = runtimeWaves = null;
            Time.timeScale = 1f;
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task Title_SpaceOrEnter_DoesNotStartAndTheUiHasNoSubmit()
        {
            var game = await ReadyScene();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));

            var modules = Object.FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include)
                .Where(module => module.GetType().GetProperty("submit") != null).ToArray();
            Assume.That(modules, Is.Not.Empty, "the scene's event system reads an input action asset");
            keyboard = new TestKeyboard();
            SelectButton(ArenaUiCommand.Start);

            await keyboard.TapFor(.8f, SpaceAndEnter);

            Assert.That(modules.Select(module => module.GetType().GetProperty("submit").GetValue(module)), Has.All.Null,
                "no submit action, so Enter cannot press the selected button either");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "Space and Enter do not start the run, even on the selected Start button");

            Assert.That(GameManager.ShownKeyCodes, Is.EquivalentTo(new[]
                { KeyCode.Escape, KeyCode.T, KeyCode.Tab, KeyCode.R, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter }),
                "the menus read only the keys the game shows");

            artWas = Art.Enabled;

            for (int press = 0; press < 3; press++)
            {
                await keyboard.Tap(KeyCode.T);
                Assert.That(Art.Enabled, Is.EqualTo(artWas.Value), $"T does nothing on the title (press {press + 1})");
            }
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));

            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task ChineseTitle_ShowsOnlyStartAndTheKeyHints()
        {
            var game = await ReadyScene();
            Assume.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, Is.EqualTo("ArenaV2_CN"), "the Chinese edition's title");
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            await Awaitable.NextFrameAsync();
            var content = GameObject.Find("V2Canvas/TitleScreen/Content");
            Assume.That(content, Is.Not.Null, "the saved title screen");

            foreach (var hidden in new[] { "InterstellarSubtitle", "ToggleArt", "ChineseTeam", "EnglishShowcase" })
            {
                var element = content.transform.Find(hidden);
                Assert.That(element == null || !element.gameObject.activeInHierarchy, Is.True, hidden + " is not shown");
            }
            foreach (var shown in new[] { "StartRun", "Controls" })
                Assert.That(content.transform.Find(shown).gameObject.activeInHierarchy, Is.True, shown + " is shown");
            Assert.That(ArenaText.IsChinese, Is.True, "the Chinese edition starts in Chinese");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task CrateAndMidWavePick_SpaceOrEnter_DoNothingAndNoTextNamesThem()
        {
            var game = await ReadyScene();
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await Awaitable.NextFrameAsync();
            var crate = Object.FindAnyObjectByType<V3SupplyDropPresentation>();
            Assume.That(crate && crate.openHint && crate.openButton, Is.True, "the saved crate has its hint and button");
            Assert.That(EventSystem.current ? EventSystem.current.currentSelectedGameObject : null, Is.Not.SameAs(crate.openButton.gameObject),
                "the crate button is not selected for a keyboard confirm");
            keyboard = new TestKeyboard();
            Select(crate.openButton);

            await keyboard.TapFor(.8f, SpaceAndEnter);

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.SupplyDrop), "Space and Enter do not open the crate, even with its button selected");
            Assert.That(crate.openHint.text, Does.Contain("CLICK").IgnoreCase.And.Not.Contain("Space").IgnoreCase.And.Not.Contain("Enter").IgnoreCase, "the crate asks for a click");

            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);

            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            await ReachCombat(game);
            game.progression.GainXp(game.progression.XpRequired);
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "a level gained mid-wave opens its pick");
            var screen = Object.FindAnyObjectByType<V2Hud>().screens.First(s => s && s.state == ArenaUiState.Draft);
            Select(screen.startNext);
            await keyboard.TapFor(.8f, SpaceAndEnter);

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.LevelUp), "Space and Enter do not put the pick off, even on its button");
            Assert.That(screen.body.text, Does.Contain("Esc").And.Not.Contain("Space").IgnoreCase, "the pick names Esc, not Space");
            Assert.That(screen.startNext.GetComponentInChildren<UnityEngine.UI.Text>().text, Does.Contain("Esc"), "the pick-later button names its key");

            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.WaveActive, "Esc puts the pick off");
        }

        [TestCase(KeyCode.Space), TestCase(KeyCode.Return), TestCase(KeyCode.KeypadEnter), LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task WeaponCard_SpaceOrEnter_ClosesIt(KeyCode key)
        {
            var game = await ReadyScene();
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            Assert.That(game.UiSnapshot.weaponIntroHint, Does.Contain("CLICK").And.Contain("Space").And.Contain("Enter"),
                "the weapon card names the keys that close it");
            keyboard = new TestKeyboard();

            await keyboard.TapUntil(key, () => game.RunPhase != ArenaUiState.WeaponIntro, key + " closes the weapon card");

            Assert.That(game.State, Is.EqualTo(GameState.Playing), "the run goes on as after a click");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task ChineseTables_KeyTexts_NameNoSpaceOrEnterAndShowEsc()
        {
            await ReadyScene();

            foreach (var key in new[] { "ui.nextwave", "ui.return.reward", "ui.ready.body", "ui.controls", "ui.hole.enter", "ui.hole.enter.build" })
                Assert.That(ArenaText.Text(key), Does.Not.Contain("Space").IgnoreCase.And.Not.Contain("Enter").IgnoreCase, key);
            Assert.That(ArenaText.Text("ui.controls"), Does.Contain("ESC PAUSE").IgnoreCase.And.Contain("E ON A PORTAL").IgnoreCase, "the title lists the pause key and where E works");
            Assert.That(ArenaText.Text("ui.hole.enter"), Does.Contain("shield").IgnoreCase, "the portal says entering empties the shield");
        }

        [Test, LoadScene(SceneEn), Category("Integration"), Timeout(30000)]
        public async Task EnglishTables_KeyTexts_NameNoSpaceOrEnterAndShowEsc()
        {
            await ReadyScene();
            Assume.That(ArenaText.IsChinese, Is.False, "the English scene starts in English");

            foreach (var key in new[] { "ui.nextwave", "ui.return.reward", "ui.ready.body", "ui.controls", "ui.hole.enter.build" })
                Assert.That(ArenaText.Text(key).ToUpperInvariant(), Does.Not.Contain("SPACE").And.Not.Contain("ENTER /").And.Not.Contain("/ ENTER"), key);
            Assert.That(ArenaText.Text("ui.controls"), Does.Contain("ESC PAUSE"), "the title lists the pause key");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task MidWavePick_OneCard_BackToTheFight_TheOtherBankedPickWaitsForT()
        {
            var game = await ReadyScene();
            await StartRun(game);
            var progression = game.progression;
            progression.GainXp(progression.XpRequired);
            progression.GainXp(progression.XpRequired);
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "two levels gained mid-wave open a pick");
            Assume.That(progression.BankedDrafts, Is.EqualTo(2), "two picks are banked");

            await CommandUntil(game, ArenaUiCommand.ChooseCard, () => game.RunPhase != ArenaUiState.LevelUp, 0);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveActive), "one card, then back to the fight");
            Assert.That(progression.BankedDrafts, Is.EqualTo(1), "the other pick stays banked");
            Assert.That(game.UiSnapshot.hint, Does.Contain("T"), "the HUD says T opens it");

            keyboard = new TestKeyboard();
            await keyboard.TapUntil(KeyCode.T, () => game.RunPhase == ArenaUiState.LevelUp, "T opens the banked pick");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task BuildPhase_StartNextWave_NoPickBanked_StartsAtOnceEvenAtTheArmory()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 6f);
            await StartRun(game);
            await Until(() => game.waves.TimeExpired, 15f, "the shortened wave times out");
            await ClearTheWave(game);
            await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 8f, "the build phase crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            if (game.progression.BankedDrafts > 0) game.progression.ResolvePendingRandom();
            Assume.That(game.progression.BankedDrafts, Is.Zero, "no card pick is banked");

            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => game.RunPhase != ArenaUiState.Armory);
            Assert.That(game.UiSnapshot.earlyStartConfirmation, Is.False, "nothing to confirm without unpicked cards");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveIntro).Or.EqualTo(ArenaUiState.WaveActive), "the next wave starts at once");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task BuildPhase_OpenPortal_EnteredOnlyByStandingOnItWithThePanelAway()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 6f);
            await StartRun(game);
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            await Until(() => game.waves.TimeExpired, 15f, "the shortened wave times out");
            await ClearTheWave(game);
            await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 8f, "the build phase crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            Assume.That(portal.Phase, Is.EqualTo(HolePhase.Portal), "the portal stays open into the build phase");
            var armory = Object.FindAnyObjectByType<V2Hud>().screens.First(s => s && s.state == ArenaUiState.Armory);
            Assert.That(armory.body.text, Does.Contain("stand on it and press E").IgnoreCase.And.Contain("shield").IgnoreCase.And.Not.Contain("can also enter").IgnoreCase,
                "the build panel says to stand on the portal and what entering costs");
            keyboard = new TestKeyboard();

            V3CombatSmokeTest.Teleport(game.player, Spot(world, portal.Centre, 6f));
            await keyboard.TapFor(.6f, KeyCode.E);
            await CommandFor(game, ArenaUiCommand.EnterPortal, .3f);
            Assert.That(world.TrialActive, Is.False, "the build panel no longer enters the trial from a distance");
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            await keyboard.TapFor(.6f, KeyCode.E);
            await CommandFor(game, ArenaUiCommand.EnterPortal, .3f);
            Assert.That(world.TrialActive, Is.False, "with the panel open the player is not standing anywhere");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Armory));

            await keyboard.TapUntil(KeyCode.Tab, () => game.BuildWalking, "Tab puts the build panel away");
            V3CombatSmokeTest.Teleport(game.player, Spot(world, portal.Centre, 2f));
            await keyboard.Tap(KeyCode.E);
            Assert.That(world.TrialActive, Is.False, "walking, but beside the portal");
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            await keyboard.TapUntil(KeyCode.E, () => world.TrialActive, "E on the portal enters the trial");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial));
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task OnThePortal_StunnedOrFrozen_EDoesNotEnterUntilThePlayerIsFreeToAct()
        {
            var game = await ReadyScene();
            await StartRun(game);
            QuietArena(game);
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            var status = ElementStatus.For(game.player);
            StandOn(game, portal);
            Assume.That(world.CanEnterPortal(game.player, portal), Is.True, "free to act on the open portal");
            keyboard = new TestKeyboard();

            game.player.Stun(.6f);
            Assume.That(game.player.State, Is.EqualTo(CharacterState.Normal), "the player is in the normal state, only stunned");
            await keyboard.Tap(KeyCode.E);
            Assert.That(world.TrialActive, Is.False, "stunned, E does not enter");
            Assert.That(world.CanEnterPortal(game.player, portal), Is.False, "and the portal label does not offer E");
            await Until(() => !game.player.Stunned, 3f, "the stun wears off");

            status.Add(ElementKind.Frost, 999f);
            Assume.That(status.Frozen, Is.True, "enough Frost freezes the player");
            StandOn(game, portal);
            await keyboard.Tap(KeyCode.E);
            Assert.That(world.TrialActive, Is.False, "frozen, E does not enter");
            status.ClearStatuses();

            StandOn(game, portal);
            await keyboard.TapUntil(KeyCode.E, () => world.TrialActive, "free to act again, E on the portal enters the trial");
            Assert.That(game.player.Stunned || status.Frozen, Is.False, "nothing that stops the player is carried onto the platform");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task EnterTrial_StatusesAndShieldCarried_AreAllCleared()
        {
            var game = await ReadyScene();
            await StartRun(game);
            var portal = OpenPortal(game);
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            var status = ElementStatus.For(game.player);
            status.Add(ElementKind.Fire, 3f);
            status.Add(ElementKind.Poison, 3f);
            status.Add(ElementKind.Frost, 1f);
            status.Add(ElementKind.Lightning, 1f);
            status.GainShield(5f);
            Assume.That(status.Fire > 0f && status.Poison > 0f && status.Frost > 0f && status.Lightning > 0f && status.Shield > 0f, Is.True,
                "the player carries statuses and a shield");

            Assert.That(WorldMap.Active.TryEnterPortal(game.player), Is.True, "the player enters the open portal");

            Assert.That(new[] { status.Fire, status.Poison, status.Frost, status.Lightning }, Has.All.Zero, "every status is cleared");
            Assert.That(status.Frozen || status.Paralysed, Is.False);
            Assert.That(status.Shield, Is.Zero, "document 12.5: the shield goes to 0");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task TrialWonMidWave_ReturnFreezesWorldAndPlayerWhileAbsorbing_ThenThePickPopsUp()
        {
            var game = await ReadyScene();
            await StartRun(game);
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Assume.That(world.TryEnterPortal(game.player), Is.True, "the player enters the open portal");
            world.CompleteTrial();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Draft), "the greater void reward is offered first");
            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => game.RunPhase != ArenaUiState.Draft);
            float frozenAt = Time.realtimeSinceStartup;
            Vector3 stood = game.player.Position;

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.TrialReturn), "the run returns through the freeze");
            Assert.That(game.CanPlayerMove, Is.False, "the player is frozen too, not only the battlefield");
            Assert.That(Time.timeScale, Is.Zero, "nothing moves during the freeze");
            await CommandFor(game, ArenaUiCommand.Pause, .8f);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.TrialReturn), "no input acts during the freeze, pausing included");
            Assert.That(FxPool.Active.items.Any(item => item && item.gameObject.activeSelf), Is.True, "the absorb animation is playing");
            Assert.That(Query.Flat(game.player.Position - stood).magnitude, Is.LessThan(.01f), "the player stays where they came back");
            Assert.That(portal.Phase, Is.Not.EqualTo(HolePhase.Gone), "the portal is still in view during the freeze");

            await Until(() => game.RunPhase != ArenaUiState.TrialReturn, game.trialReturnFreeze + 3f, "the freeze ends by itself");
            Assert.That(Time.realtimeSinceStartup - frozenAt, Is.GreaterThanOrEqualTo(game.trialReturnFreeze - .15f), "it lasts the tuned freeze");
            Assert.That(world.holes.Where(h => h).All(h => h.Phase == HolePhase.Gone), Is.True, "after the freeze every black hole has turned into ground");
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "the extra level's pick pops up as the wave resumes");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task GreaterRewardAfterAMidWaveWin_Tab_DoesNotWalkTheLiveBattlefield()
        {
            var game = await ReadyScene();
            await StartRun(game);
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Assume.That(world.TryEnterPortal(game.player), Is.True, "the player enters the open portal");
            world.CompleteTrial();
            Assume.That(game.RunPhase == ArenaUiState.Draft && game.UiSnapshot.isGreaterReward, Is.True, "the greater void reward is offered first");
            float clock = game.waves.TimeLeft;
            keyboard = new TestKeyboard();

            await keyboard.TapFor(.8f, KeyCode.Tab);
            await CommandFor(game, ArenaUiCommand.ToggleBuildWalk, .4f);

            Assert.That(game.BuildWalking, Is.False, "the greater reward is no build phase: Tab does not put it away");
            Assert.That(game.CanPlayerMove, Is.False, "the player cannot walk the live battlefield under the reward");
            Assert.That(Time.timeScale, Is.Zero, "the battlefield stays frozen under the reward");
            Assert.That(game.waves.TimeLeft, Is.EqualTo(clock), "the wave clock does not run");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Draft));

            await keyboard.TapUntil(KeyCode.Alpha1, () => game.RunPhase != ArenaUiState.Draft, "1 takes the first greater void skill");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.TrialReturn), "the run goes back through the freeze");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task KnockedBack_SkillCast_IsRefusedUntilTheSlideEnds()
        {
            var game = await ReadyScene();
            await StartRun(game);
            QuietArena(game);
            Assume.That(game.skills.Learn(SkillId.Overcharge), Is.True, "the player learns a skill");
            var lane = LaneStart(WorldMap.Active, Vector3.right, 10f);
            V3CombatSmokeTest.Teleport(game.player, lane);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();

            game.player.Knockback(Vector3.right, 4f, KnockKind.Hit);
            Assume.That(game.player.State, Is.EqualTo(CharacterState.Knocked), "the player slides from the knockback");
            Assert.That(game.skills.TryCast(0), Is.False, "no skill while knocked back");

            await Until(() => game.player.State == CharacterState.Normal, 3f, "the slide ends");
            Assert.That(game.skills.TryCast(0), Is.True, "skills work again once the slide is over");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task KnockedBack_Blink_StillCastsAndEndsTheSlide()
        {
            var game = await ReadyScene();
            await StartRun(game);
            QuietArena(game);
            Assume.That(game.skills.Learn(SkillId.Blink), Is.True, "the player learns Blink");
            var lane = LaneStart(WorldMap.Active, Vector3.right, 10f);
            V3CombatSmokeTest.Teleport(game.player, lane);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            game.player.DebugSetAimPoint(lane + Vector3.right * 7f);

            game.player.Knockback(Vector3.right, 4f, KnockKind.Hit);
            Assume.That(game.player.State, Is.EqualTo(CharacterState.Knocked), "the player slides from the knockback");
            Assert.That(game.skills.TryCast(0), Is.True, "Blink forcibly breaks a displacement, so it casts while knocked back");
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Normal), "and the slide is over");
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            return GameManager.Instance;
        }

        static async Task StartRun(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            await ReachCombat(game);
        }

        static async Task ReachCombat(GameManager game)
        {
            game.player.ProtectFor(600f);
            await UntilWaveActive(game);
            foreach (var hole in WorldMap.Active.holes) if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0) hole.Feed(1, game.waves.WaveIndex + 1);
            await Awaitable.NextFrameAsync();
            await UntilWaveActive(game);
        }

        static void QuietArena(GameManager game)
        {
            game.waves.enabled = false;
            foreach (var enemy in game.waves.preparedEnemies) if (enemy && enemy.gameObject.activeSelf) enemy.DiscardWithoutReward();
            foreach (var marker in game.waves.preparedMarkers) if (marker) marker.gameObject.SetActive(false);
        }

        static async Task ClearTheWave(GameManager game)
        {
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

        static BlackHole OpenPortal(GameManager game)
        {
            var hole = WorldMap.Active.holes.First(h => h && h.Phase == HolePhase.Active);
            hole.Feed(hole.feedsToOpen, game.waves.WaveIndex + 1);
            hole.Tick(2f);
            Assume.That(hole.Phase, Is.EqualTo(HolePhase.Portal), "the fed hole opens a portal");
            return hole;
        }

        static void StandOn(GameManager game, BlackHole portal)
        {
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Physics.SyncTransforms();
        }

        void ShortenWaves(GameManager game, float duration)
        {
            originalWaves = game.waves.waveSet;
            runtimeWaves = Object.Instantiate(originalWaves);
            foreach (var wave in runtimeWaves.waves) wave.duration = duration;
            game.waves.waveSet = runtimeWaves;
        }

        static void Select(UnityEngine.UI.Button button)
        {
            Assume.That(EventSystem.current && button && button.isActiveAndEnabled && button.IsInteractable(), Is.True, "the button is shown and can be pressed");
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        static void SelectButton(ArenaUiCommand command) => Select(Object.FindObjectsByType<ArenaUiButton>(FindObjectsInactive.Exclude)
            .Where(saved => saved.command == command).Select(saved => saved.GetComponent<UnityEngine.UI.Button>())
            .FirstOrDefault(button => button && button.isActiveAndEnabled && button.IsInteractable()));

        static Vector3 Spot(WorldMap world, Vector3 centre, float distance)
        {
            for (int step = 0; step < 16; step++)
            {
                Vector3 at = centre + Quaternion.Euler(0f, 22.5f * step, 0f) * Vector3.forward * distance;
                at.y = 0f;
                if (world.IsWalkable(at, .6f)) return at;
            }
            Assert.Fail($"No free spot {distance} cells from {centre}.");
            return centre;
        }

        static Vector3 LaneStart(WorldMap world, Vector3 dir, float length)
        {
            for (int x = -36; x <= 36; x += 4)
            for (int z = -24; z <= 24; z += 4)
            {
                var start = new Vector3(x, 0f, z);
                bool clear = true;
                for (float along = 0f; along <= length && clear; along += .5f) clear = world.IsWalkable(start + dir * along, 1.2f);
                if (clear && !Physics.SphereCast(Query.AtCastHeight(start), 1f, dir, out _, length, GameManager.WallMask, QueryTriggerInteraction.Ignore))
                    return start;
            }
            Assert.Fail("The saved map has no clear lane for the knockback.");
            return Vector3.zero;
        }

        static async Task CommandFor(GameManager game, ArenaUiCommand command, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                game.ExecuteUiCommand(command);
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
