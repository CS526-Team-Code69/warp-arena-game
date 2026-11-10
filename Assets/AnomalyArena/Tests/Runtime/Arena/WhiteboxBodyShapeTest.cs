using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Whitebox bodies in a run (2026-11-10): a mini Splitter's look is the Splitter's at its smaller size and stands on the
    /// ground (the old box kept its centre and floated 0.4 m up); a kind whose table look has no front of its own wears the white front
    /// wedge in front of its body, one with a front of its own does not. Enemies come from their own pools in the sandbox's clear lane.</summary>
    [TestFixture]
    public class WhiteboxBodyShapeTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        bool savedArt;

        [SetUp]
        public void SetUp() => savedArt = Art.Enabled;

        [TearDown]
        public void TearDown()
        {
            Art.Enabled = savedArt;
            Time.timeScale = 1f;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MiniSplitter_Whitebox_TheSplittersLookSmaller_OnTheGround()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Art.Enabled = false;
            foreach (var target in targets) target.DiscardWithoutReward();
            var splitter = game.waves.SpawnLab(EnemyKind.Splitter, game.player.Position + Vector3.forward * 6f, false, ElementKind.None);
            Assume.That(splitter, Is.Not.Null, "the pool has a free Splitter");
            splitter.SuppressRewards = true;
            splitter.enabled = false;
            var look = splitter.GetComponent<WhiteboxCharacter>();
            await Until(() => look && look.Shown, 2f, "the whitebox look shows");
            var full = WorldBounds(look.Look);

            splitter.Configure(splitter.definition, 1f, false, ElementKind.None, mini: true);
            splitter.enabled = false;
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            var mini = WorldBounds(look.Look);
            float ground = splitter.Position.y;
            float share = game.waves.splitterMiniScale;

            Assert.That(look.Look.LookId, Is.EqualTo("splitter"), "a mini Splitter wears the Splitter's look");
            Assert.That(full.min.y, Is.EqualTo(ground).Within(.05f), "the full Splitter stands on the ground");
            Assert.That(mini.min.y, Is.EqualTo(ground).Within(.05f), "the mini Splitter stands on the ground");
            Assert.That(mini.size.y, Is.EqualTo(full.size.y * share).Within(.05f), "at the mini's share of the size");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FrontMark_OnAKindWithoutAFront_InFrontOfItsBody_NotAddedWhereTheTableHasOne()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Art.Enabled = false;
            foreach (var target in targets) target.DiscardWithoutReward();
            var origin = game.player.Position;
            var splitter = game.waves.SpawnLab(EnemyKind.Splitter, origin + Vector3.forward * 5f, false, ElementKind.None);
            var grunt = game.waves.SpawnLab(EnemyKind.Grunt, origin + Vector3.forward * 9f, false, ElementKind.None);
            Assume.That(splitter && grunt, Is.True, "the pools have a free Splitter and Grunt");
            splitter.SuppressRewards = grunt.SuppressRewards = true;
            splitter.enabled = grunt.enabled = false;
            var withMark = splitter.GetComponent<WhiteboxCharacter>();
            var ownMark = grunt.GetComponent<WhiteboxCharacter>();
            await Until(() => withMark.Shown && ownMark.Shown, 2f, "both looks show");

            Assert.That(withMark.FaceMark, Is.Not.Null, "the Splitter's octahedron gets the front wedge");
            var mark = withMark.FaceMark.PartRenderer(0).bounds.center;
            var body = withMark.Look.PartRenderer(withMark.Look.MainPart).bounds.center;
            Assert.That(Vector3.Dot(mark - body, splitter.transform.forward), Is.GreaterThan(.1f), "the wedge is on the side the body faces");
            Assert.That(ownMark.FaceMark, Is.Null, "the Grunt's look carries the table's own wedge");
        }

        /// <summary>The label and mark point over a whitebox body (Combatant.TryModelTop) is at the look's top as the camera sees it: on
        /// screen above the body's middle, and no higher than the look's highest corner (the Brute's broad shoulders).</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task TryModelTop_Whitebox_OverTheTopOfTheLook()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Art.Enabled = false;
            foreach (var target in targets) target.DiscardWithoutReward();
            var brute = game.waves.SpawnLab(EnemyKind.Brute, game.player.Position + Vector3.forward * 7f, false, ElementKind.None);
            Assume.That(brute, Is.Not.Null, "the pool has a free Brute");
            brute.SuppressRewards = true;
            brute.enabled = false;
            var look = brute.GetComponent<WhiteboxCharacter>();
            await Until(() => look && look.Shown, 2f, "the whitebox look shows");
            var cam = game.cam ? game.cam : Camera.main;
            Assume.That(cam, Is.Not.Null, "the scene has a game camera");

            bool found = brute.TryModelTop(cam, out var top);

            Assert.That(found, Is.True, "a whitebox body has a top");
            float labelY = cam.WorldToScreenPoint(top).y;
            var bounds = WorldBounds(look.Look);
            float highest = float.NegativeInfinity;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                var corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);
                highest = Mathf.Max(highest, cam.WorldToScreenPoint(corner).y);
            }
            Assert.That(labelY, Is.GreaterThan(cam.WorldToScreenPoint(brute.Position + Vector3.up * look.Look.LocalBounds.max.y * .5f).y), "above the body's middle");
            Assert.That(labelY, Is.LessThanOrEqualTo(highest + 1f), "no higher than the look's highest corner");
        }

        static Bounds WorldBounds(WhiteboxLook look)
        {
            var bounds = look.PartRenderer(0).bounds;
            for (int i = 1; i < look.PartCount; i++) bounds.Encapsulate(look.PartRenderer(i).bounds);
            return bounds;
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (true)
            {
                var map = WorldMap.Active;
                if (map && map.HoleIntroPlaying) map.SkipHoleIntro();
                if (completed()) return;
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
