using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The void dash, in the saved arena's open mode. Before the void trial is won Space does nothing in a fight and the battle
    /// HUD shows nothing of a dash. Once it is won, a one-line notice gives the dash when play is back, its key joins the hints at the
    /// bottom right without overlapping them, and Space moves the player the dash's distance (5 cells) toward the aim point and spends
    /// its share of the stamina bar (half); a wall in the way stops it in front; the cooldown, and a bar the sprint has run empty, refuse
    /// the next dash until they are over. A continued run that had won the trial has the dash, and the Space that closes the weapon card
    /// does not also dash. Space goes in through the code the keyboard reaches (GameManager.PressKey, TestKeyboard), as in the other key
    /// tests: device events never reach the game in the batch-mode test runner.</summary>
    [TestFixture]
    public class VoidDashTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        TestKeyboard keyboard;

        /// <summary>What stops a walking body, and so the dash: the wall layer and the base's footprint that stops bodies only.</summary>
        static int BodyWalls => GameManager.WallMask | 1 << Combatant.BodyOnlyWallLayer;

        [TearDown]
        public void TearDown()
        {
            keyboard?.Dispose();
            keyboard = null;
            var game = GameManager.Instance;
            if (game && game.player) game.player.debugSprintHeld = null;
            Time.timeScale = 1f;
        }

        // ───── Locked ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task BeforeTheTrialIsWon_Space_DoesNotDash_AndTheHudShowsNoDash()
        {
            var game = await ReadyScene();
            await StartRun(game);
            QuietArena(game);
            var player = game.player;
            var start = await StandAtALane(game, Vector3.right, player.voidDash.distance + 3f);
            player.stamina.Refill();
            keyboard = new TestKeyboard();

            await keyboard.TapFor(.6f, KeyCode.Space);

            Assert.That(game.VoidDashUnlocked, Is.False, "no dash before the void trial is won");
            Assert.That(Query.Flat(player.Position - start).magnitude, Is.LessThan(.05f), "Space does not move the player");
            Assert.That(player.stamina.Value, Is.EqualTo(1f), "nor spend stamina");
            if (V5HudKit.On)
                Assert.That(Hint("DashKey").gameObject.activeInHierarchy || Hint("DashWord").gameObject.activeInHierarchy, Is.False,
                    "the key hints show nothing of a dash");
            // The same keyboard does reach the game: Esc, a key the hints show, pauses the fight.
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.Paused, "Esc pauses the fight");
        }

        // ───── Won: the dash, its notice and its key ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task TrialWon_Space_DashesItsDistanceTowardTheAim_SpendsItsShare_AndTheHudShowsTheDash()
        {
            var game = await ReadyScene();
            await StartRun(game);
            await WinTheTrial(game);
            Assert.That(game.VoidDashUnlocked, Is.True, "the won trial gives the dash");
            if (V5HudKit.On)
            {
                var notice = Root().Find("V5Notice/Line").GetComponent<Text>();
                await Until(() => notice.gameObject.activeInHierarchy && notice.text.Contains(ArenaText.Pair("虚空冲刺", "Void Dash")), 3f,
                    "a one-line notice gives the dash once play is back");
                Assert.That(notice.text, Does.Contain(ArenaText.Pair("空格", "Space")), "and names its key");
                var key = Hint("DashKey");
                var word = Hint("DashWord");
                await Until(() => key.gameObject.activeInHierarchy && word.gameObject.activeInHierarchy, 2f, "the dash's key joins the hints");
                Assert.That(key.GetComponentInChildren<Text>().text, Is.EqualTo("Space"), "its cap says Space");
                Assert.That(word.GetComponent<Text>().text, Is.EqualTo(ArenaText.Pair("冲刺", "Dash")), "and its word what it does");
                Canvas.ForceUpdateCanvases();
                foreach (var part in new[] { key, word })
                    foreach (var other in new[] { "InteractKey", "InteractWord", "PauseKey", "PauseWord" }.Select(Hint))
                        Assert.That(Box(part).Overlaps(Box(other)), Is.False, $"{part.name} overlaps {other.name}");
            }
            QuietArena(game);
            var player = game.player;
            var dash = player.voidDash;
            var start = await StandAtALane(game, Vector3.right, dash.distance + 3f);
            player.stamina.Refill();

            game.PressKey(KeyCode.Space);

            Vector3 moved = Query.Flat(player.Position - start);
            Assert.That(moved.magnitude, Is.EqualTo(dash.distance).Within(.15f), "the dash covers its distance");
            Assert.That(Vector3.Angle(moved, Vector3.right), Is.LessThan(1f), "toward the aim point");
            Assert.That(player.stamina.Value, Is.EqualTo(1f - dash.staminaShare).Within(.001f), "and spends its share of the stamina");
            Assert.That(player.stamina.Exhausted, Is.False, "with half the bar left it is not exhausted");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task TrialWon_AWallInTheWay_StopsTheDashInFrontOfIt()
        {
            var game = await ReadyScene();
            await StartRun(game);
            await WinTheTrial(game);
            QuietArena(game);
            var player = game.player;
            var dash = player.voidDash;
            var (spot, dir) = BeforeAWall(WorldMap.Active, player.Radius, 1.5f, dash.distance - 1f);
            Settle(player);
            V3CombatSmokeTest.Teleport(player, spot);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            Vector3 start = player.Position;
            Assume.That(Physics.SphereCast(Query.AtCastHeight(start), player.Radius, dir, out var hit, dash.distance, BodyWalls, QueryTriggerInteraction.Ignore),
                Is.True, "a wall stands within a dash");
            float wall = hit.distance;
            player.DebugSetAimPoint(start + dir * (dash.distance + 5f));
            player.stamina.Refill();

            game.PressKey(KeyCode.Space);

            Vector3 end = player.Position;
            float moved = Query.Flat(end - start).magnitude;
            Assert.That(moved, Is.LessThan(dash.distance - .5f), "the wall cuts the dash short");
            Assert.That(moved, Is.GreaterThan(wall - .6f), "it still goes up to the wall");
            Assert.That(moved, Is.LessThanOrEqualTo(wall + .05f), "and no farther");
            Assert.That(Physics.CheckSphere(Query.AtCastHeight(end), player.Radius * .9f, BodyWalls, QueryTriggerInteraction.Ignore), Is.False,
                "the player stands in front of the wall, not in it");
            Assert.That(WorldMap.Active.IsWall(end), Is.False, "nor inside the wall's place on the map");
            Assert.That(player.stamina.Value, Is.LessThan(1f), "a dash that moved is paid");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task TrialWon_TheCooldownAndABarRunEmpty_RefuseTheNextDashUntilTheyAreOver()
        {
            var game = await ReadyScene();
            await StartRun(game);
            await WinTheTrial(game);
            QuietArena(game);
            var player = game.player;
            var dash = player.voidDash;
            var start = await StandAtALane(game, Vector3.right, dash.distance + 3f);
            player.stamina.Refill();
            Assume.That(Dash(game, start), Is.EqualTo(dash.distance).Within(.15f), "the first dash");
            float firstAt = Time.time, left = player.stamina.Value;

            Assert.That(Dash(game, start), Is.LessThan(.05f), "a second Space at once is refused while the cooldown runs");
            Assert.That(player.stamina.Value, Is.EqualTo(left), "and spends nothing");
            await GameTime(dash.cooldown * .5f);
            Assert.That(Dash(game, start), Is.LessThan(.05f), "still refused half way through the cooldown");
            await Until(() => Time.time >= firstAt + dash.cooldown + .02f, 3f, "the cooldown passes");
            float before = player.stamina.Value;
            Assert.That(Dash(game, start), Is.EqualTo(dash.distance).Within(.15f), "after the cooldown the next dash goes");
            Assert.That(player.stamina.Value, Is.EqualTo(dash.StaminaAfter(before)).Within(.001f), "and spends its share again");

            // A bar the sprint runs empty refuses the dash, the sprint's own rule, until it is full again.
            player.debugSprintHeld = true;
            await Until(() => player.stamina.Exhausted, 10f, "sprinting runs the bar empty");
            player.debugSprintHeld = null;
            await Until(() => !dash.CoolingDown, 3f, "the last dash's cooldown is over");
            Assume.That(player.stamina.Exhausted, Is.True, "the bar is still filling up");
            Assert.That(Dash(game, start), Is.LessThan(.05f), "an exhausted bar refuses the dash");
            await Until(() => !player.stamina.Exhausted, 10f, "the bar fills up again");
            Assert.That(Dash(game, start), Is.EqualTo(dash.distance).Within(.15f), "a full bar dashes again");
        }

        // ───── A continued run, and the weapon card ─────

        /// <summary>Both kinds of continued save hand the map whether the trial was won (WorldMap.RestoreRunState); a run that had won it
        /// dashes. The weapon card closes with Space: that press hands play back and does not also dash; the next Space dashes.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task ContinuedRunThatHadWonTheTrial_HasTheDash_AndTheSpaceThatClosesTheWeaponCardDoesNotAlsoDash()
        {
            var game = await ReadyScene();
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            var world = WorldMap.Active;
            var restore = typeof(WorldMap).GetMethod("RestoreRunState", Private);
            Assume.That(restore, Is.Not.Null, "WorldMap still has RestoreRunState, the step a continued save takes");
            // The trial won and the first hole's cutscene seen, as a save of such a run hands them back.
            restore.Invoke(world, new object[] { true, true });
            Assert.That(game.VoidDashUnlocked, Is.True, "a continued run that had won the trial has the dash");
            var player = game.player;
            var start = await StandAtALane(game, Vector3.right, player.voidDash.distance + 3f);
            player.ProtectFor(600f);
            player.stamina.Refill();
            keyboard = new TestKeyboard();

            await keyboard.TapUntil(KeyCode.Space, () => game.RunPhase != ArenaUiState.WeaponIntro, "Space closes the weapon card");

            Assert.That(game.CanPlayerMove, Is.True, "closing the card hands play back");
            Assert.That(Query.Flat(player.Position - start).magnitude, Is.LessThan(.05f), "the Space that closed the card did not also dash");
            Assert.That(player.stamina.Value, Is.EqualTo(1f), "nor spend stamina");
            QuietArena(game);
            if (V5HudKit.On) await Until(() => Hint("DashKey").gameObject.activeInHierarchy, 2f, "the continued run's hints show the dash's key");
            // The same keyboard does reach the dash: the next Space dashes.
            await keyboard.TapUntil(KeyCode.Space, () => Query.Flat(player.Position - start).magnitude > player.voidDash.distance - .5f, "the next Space dashes");
        }

        // ───── Helpers ─────

        /// <summary>Wins the void trial mid-wave as the other trial tests do (open the portal, enter, CompleteTrial), takes the greater
        /// void reward's way back, lets the frozen return end and puts the extra level's pick off: the fight goes on.</summary>
        static async Task WinTheTrial(GameManager game)
        {
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Physics.SyncTransforms();
            Assume.That(world.TryEnterPortal(game.player), Is.True, "the player enters the open portal");
            Assert.That(game.VoidDashUnlocked, Is.False, "in the trial the dash is not won yet");
            world.CompleteTrial();
            Assume.That(world.TrialWon && game.RunPhase == ArenaUiState.Draft && game.UiSnapshot.isGreaterReward, Is.True,
                "a real win offers the greater void reward first");
            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => game.RunPhase != ArenaUiState.Draft);
            await Until(() => game.RunPhase != ArenaUiState.TrialReturn, game.trialReturnFreeze + 3f, "the frozen return ends");
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "the extra level's pick pops up");
            await CommandUntil(game, ArenaUiCommand.Cancel, () => game.RunPhase == ArenaUiState.WaveActive);
        }

        /// <summary>Puts the player at the start of a clear lane along dir, aiming far down it; where the player stands then.</summary>
        static async Task<Vector3> StandAtALane(GameManager game, Vector3 dir, float length)
        {
            Settle(game.player);
            V3CombatSmokeTest.Teleport(game.player, LaneStart(WorldMap.Active, dir, length));
            Physics.SyncTransforms();
            // Under a frozen screen (the weapon card) no physics step comes: the next frame is enough there.
            if (Time.timeScale > 0f) await Awaitable.FixedUpdateAsync();
            else await Awaitable.NextFrameAsync();
            Vector3 start = game.player.Position;
            game.player.DebugSetAimPoint(start + dir * (length + 10f));
            return start;
        }

        /// <summary>Whatever the fight left on the player (a stun, a status, a slide) is cleared, and the player is protected again, so
        /// only the dash's own rules decide whether it goes.</summary>
        static void Settle(PlayerController player)
        {
            player.ClearWaveState();
            player.ProtectFor(600f);
        }

        /// <summary>Puts the player back at start and presses Space once: how far the player went.</summary>
        static float Dash(GameManager game, Vector3 start)
        {
            V3CombatSmokeTest.Teleport(game.player, start);
            game.PressKey(KeyCode.Space);
            return Query.Flat(game.player.Position - start).magnitude;
        }

        /// <summary>A spot on open ground and one of eight ways from it where a wall stands between least and most cells off, the ground
        /// before it open.</summary>
        static (Vector3 start, Vector3 dir) BeforeAWall(WorldMap world, float radius, float least, float most)
        {
            var bounds = world.Bounds;
            for (float x = bounds.min.x + 4f; x <= bounds.max.x - 4f; x += 2f)
            for (float z = bounds.min.z + 4f; z <= bounds.max.z - 4f; z += 2f)
            {
                var start = new Vector3(x, 0f, z);
                if (!world.IsWalkable(start, 1.2f)) continue;
                for (int turn = 0; turn < 8; turn++)
                {
                    Vector3 dir = Quaternion.Euler(0f, 45f * turn, 0f) * Vector3.forward;
                    if (!Physics.SphereCast(Query.AtCastHeight(start), radius, dir, out var hit, most, BodyWalls, QueryTriggerInteraction.Ignore)
                        || hit.distance < least) continue;
                    bool open = true;
                    for (float along = .5f; along < hit.distance - .6f && open; along += .5f) open = world.IsWalkable(start + dir * along, .3f);
                    if (open) return (start, dir);
                }
            }
            Assert.Fail("The saved map has no wall within a dash of open ground.");
            return default;
        }

        /// <summary>Start of a straight lane of the given length along dir with no wall, hole or gravity ring on it.</summary>
        static Vector3 LaneStart(WorldMap world, Vector3 dir, float length)
        {
            for (int x = -36; x <= 36; x += 4)
            for (int z = -24; z <= 24; z += 4)
            {
                var start = new Vector3(x, 0f, z);
                bool clear = true;
                for (float along = 0f; along <= length && clear; along += .5f) clear = world.IsWalkable(start + dir * along, 1.2f);
                if (clear && !Physics.SphereCast(Query.AtCastHeight(start), 1f, dir, out _, length, BodyWalls, QueryTriggerInteraction.Ignore))
                    return start;
            }
            Assert.Fail("The saved map has no clear lane for the dash.");
            return Vector3.zero;
        }

        /// <summary>The battle HUD's play screen (V2Hud.gameplayRoot), where the fifth pass builds its pieces.</summary>
        static Transform Root() => Object.FindAnyObjectByType<V2Hud>().gameplayRoot.transform;

        /// <summary>A piece of the key hints at the bottom right (V5BattleHud).</summary>
        static RectTransform Hint(string name)
        {
            var part = Root().Find("V5KeyHints/" + name) as RectTransform;
            Assert.That(part, Is.Not.Null, "the battle HUD's key hints have " + name);
            return part;
        }

        static Rect Box(RectTransform part)
        {
            var corners = new Vector3[4];
            part.GetWorldCorners(corners);
            return Rect.MinMaxRect(Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y),
                Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            return GameManager.Instance;
        }

        /// <summary>Title → supply crate → Rifle → close its card → the first wave is in combat (a first-sight cutscene is ended at once).</summary>
        static async Task StartRun(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            await UntilWaveActive(game);
            // One feed for each hole: an unfed hole coming into view after a teleport would start the first-sight cutscene.
            foreach (var hole in WorldMap.Active.holes) if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0) hole.Feed(1, game.waves.WaveIndex + 1);
            await Awaitable.NextFrameAsync();
            await UntilWaveActive(game);
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

        /// <summary>Stops the wave spawning and takes away what is about, so nothing walks into the player during a check.</summary>
        static void QuietArena(GameManager game)
        {
            game.waves.enabled = false;
            foreach (var enemy in game.waves.preparedEnemies) if (enemy && enemy.gameObject.activeSelf) enemy.DiscardWithoutReward();
            foreach (var marker in game.waves.preparedMarkers) if (marker) marker.gameObject.SetActive(false);
        }

        /// <summary>Feeds the first active fixed hole until its portal opens.</summary>
        static BlackHole OpenPortal(GameManager game)
        {
            var hole = WorldMap.Active.holes.First(h => h && h.Phase == HolePhase.Active);
            hole.Feed(hole.feedsToOpen, game.waves.WaveIndex + 1);
            hole.Tick(2f);
            Assume.That(hole.Phase, Is.EqualTo(HolePhase.Portal), "the fed hole opens a portal");
            return hole;
        }

        /// <summary>Waits this much game time (a pause would hold it).</summary>
        static async Task GameTime(float seconds)
        {
            float until = Time.time + seconds;
            await Until(() => Time.time >= until, seconds + 3f, $"{seconds:0.##} s of game time");
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
