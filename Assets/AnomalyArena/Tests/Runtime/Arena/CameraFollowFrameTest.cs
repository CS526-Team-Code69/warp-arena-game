using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Where the player is drawn at the map's sides and corners (CameraRig.KeepInFrame). The view is kept inside the map, and
    /// with its 45 degree turn it used to stop 22 cells short of every side: a player who walked on to the side was drawn under the top
    /// plates or behind the weapon bar, and in the last cells and in the corners past the picture's rim. Wherever the player stands on
    /// the map, it is drawn in the part of the picture the plates leave clear; away from the sides it stands in the picture's middle
    /// as before.</summary>
    [TestFixture]
    public class CameraFollowFrameTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        // The part of the picture the plates leave clear, as shares of its width and height from the bottom left: the top plates end
        // 17% down (a body's height more for the head), the weapon bar takes the bottom 11%, the side plates the outer tenth.
        const float Left = .12f, Right = .88f, Bottom = .14f, Top = .80f;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(240000)]
        public async Task AtTheMapsSidesAndCorners_ThePlayerIsDrawnClearOfThePlates()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 4f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            await Until(() => game.RunPhase == ArenaUiState.WaveActive || game.RunPhase == ArenaUiState.Cutscene, 10f, "the first wave starts");

            var map = WorldMap.Active;
            var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
            Assume.That(rig && rig.camera && rig.target == game.player.transform, Is.True, "the scene's camera follows the player");
            Bounds b = map.Bounds;
            float ex = b.extents.x - 1.5f, ez = b.extents.z - 1.5f;
            var places = new List<(string name, Vector3 at)>
            {
                ("the +x +z corner", new Vector3(ex, 0f, ez)), ("the -x -z corner", new Vector3(-ex, 0f, -ez)),
                ("the +x -z corner", new Vector3(ex, 0f, -ez)), ("the -x +z corner", new Vector3(-ex, 0f, ez)),
                ("the +x side", new Vector3(ex, 0f, 0f)), ("the -x side", new Vector3(-ex, 0f, 0f)),
                ("the +z side", new Vector3(0f, 0f, ez)), ("the -z side", new Vector3(0f, 0f, -ez)),
                ("7 cells inside the +x side", new Vector3(b.extents.x - 7f, 0f, ez * .5f)),
                ("7 cells inside the -z side", new Vector3(-ex * .5f, 0f, -(b.extents.z - 7f))),
            };
            var said = new List<string>();
            var wrong = new List<string>();
            int measured = 0;
            foreach (var (name, at) in places)
            {
                // A hole's pull would carry the body off while the view settles: such a place is left out.
                if (map.holes.Any(h => h && h.Phase != HolePhase.Gone && Query.Flat(at - h.Centre).magnitude < h.GravityRadius + 3f)) { said.Add($"{name}: left out (a hole)"); continue; }
                Vector3 v = await DrawnAt(game, rig, at);
                measured++;
                string line = $"{name} {P(game.player.Position)}: drawn at ({v.x:0.00}, {v.y:0.00})";
                said.Add(line);
                if (v.x < Left || v.x > Right || v.y < Bottom || v.y > Top) wrong.Add(line);
            }
            Debug.Log("[CameraFollowFrameTest] " + string.Join("; ", said));
            Assert.That(measured, Is.GreaterThanOrEqualTo(6), "enough places measured: " + string.Join("; ", said));
            Assert.That(wrong, Is.Empty,
                $"The player is drawn outside the part of the picture the plates leave clear ({Left:0.00}..{Right:0.00} across, {Bottom:0.00}..{Top:0.00} up; 0.5, 0.5 is the middle, outside 0..1 is off the screen), map {b.size.x:0} x {b.size.z:0}:\n  " + string.Join("\n  ", wrong));

            // Away from the sides nothing changes: the player stands in the picture's middle.
            Vector3 inland = new Vector3(Mathf.Min(40f, ex - 40f), 0f, 0f);
            foreach (var probe in new[] { inland, inland + new Vector3(0f, 0f, 8f), inland + new Vector3(-12f, 0f, -6f) })
            {
                if (map.holes.Any(h => h && h.Phase != HolePhase.Gone && Query.Flat(probe - h.Centre).magnitude < h.GravityRadius + 3f)) continue;
                Vector3 v = await DrawnAt(game, rig, probe);
                Assert.That(Mathf.Abs(v.x - .5f) < .06f && Mathf.Abs(v.y - .5f) < .08f, Is.True,
                    $"away from the map's sides the player stands in the picture's middle: at {P(game.player.Position)} drawn at ({v.x:0.00}, {v.y:0.00})");
                return;
            }
            Assert.Inconclusive("no place away from the sides was free of a hole");
        }

        /// <summary>Sets the player down at a place and waits for the view to come to rest there (a cutscene's own view first, when the
        /// place shows a hole for the first time); returns where the player's feet are drawn, as shares of the picture.</summary>
        static async Task<Vector3> DrawnAt(GameManager game, CameraRig rig, Vector3 at)
        {
            var player = game.player;
            player.ProtectFor(60f);
            player.GetComponent<Rigidbody>().position = at;
            player.transform.position = at;
            Physics.SyncTransforms();
            float deadline = Time.realtimeSinceStartup + 30f;
            float restSince = -1f;
            Vector3 last = new Vector3(-9f, -9f, 0f);
            while (true)
            {
                await Awaitable.NextFrameAsync();
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The view did not come to rest with the player at {P(at)} ({game.RunPhase}).");
                if (!player.IsAlive) Assert.Fail($"The player fell at {P(at)} while the view was measured.");
                Vector3 v = rig.camera.WorldToViewportPoint(player.Position);
                bool playing = game.RunPhase is ArenaUiState.WaveActive or ArenaUiState.WaveIntro && !rig.Focused;
                if (!playing || Mathf.Abs(v.x - last.x) > .0015f || Mathf.Abs(v.y - last.y) > .0015f) restSince = -1f;
                else if (restSince < 0f) restSince = Time.realtimeSinceStartup;
                last = v;
                if (restSince >= 0f && Time.realtimeSinceStartup - restSince > 1.2f) return v;
            }
        }

        static string P(Vector3 p) => $"({p.x:0.0}, {p.z:0.0})";

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
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
