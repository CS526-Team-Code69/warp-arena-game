using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture, Category("SkillVfx")]
    public class SkillVfxTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), FocusGameView, Timeout(40000)]
        public async Task FlameWake_LeavesVisibleFireAtPreviousPositions()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            Assert.That(game.skills.Learn(SkillId.FlameWake), Is.True);
            int casts=0;game.skills.CastSucceeded+=(_,__,___,____)=>casts++;
            Assert.That(game.skills.TryCast(0), Is.True);
            Vector3 start = game.player.Position;
            for (int i = 1; i <= 6; i++)
            {
                V3CombatSmokeTest.Teleport(game.player, start + Vector3.right * (i * .7f));
                await Awaitable.NextFrameAsync();
            }
            var ground = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .SingleOrDefault(p => p.name == "FlameWake_Ground");
            Assert.That(ground, Is.Not.Null, "authored fire ground renderer is connected to the saved scene");
            Assert.That(ground.particleCount, Is.GreaterThanOrEqualTo(6), "old path remains visible after player moves");
            Assert.That(casts,Is.EqualTo(1),"path samples never replay a cast event or cast audio");
            var view=Object.FindAnyObjectByType<SkillGroundPresentation>();
            var samples=new List<SkillGroundVisual>();game.skills.CopyGroundVisuals(samples);
            var cameraRig=game.cam.GetComponent<CameraRig>();cameraRig.enabled=false;
            game.cam.transform.position=game.player.Position+cameraRig.offset;
            await TestHelper.RuntimeInternals.ScreenshotHelper.TakeScreenshotAsync("work/authoring-receipts","flame-wake-native.png");
            game.skills.CopyGroundVisuals(samples);
            float before=samples[0].Remaining;
            typeof(GameManager).GetMethod("SetPhase",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{ArenaUiState.Paused});
            float until=Time.realtimeSinceStartup+.2f;
            while(Time.realtimeSinceStartup<until)await Awaitable.NextFrameAsync();
            game.skills.CopyGroundVisuals(samples);Assert.That(samples[0].Remaining,Is.EqualTo(before));
            game.skills.FreezeBattlefieldForTrial(true);await Awaitable.NextFrameAsync();
            Assert.That(view.VisibleFlameCount,Is.Zero,"battlefield path hidden inside trial");
            game.skills.FreezeBattlefieldForTrial(false);await Awaitable.NextFrameAsync();
            Assert.That(view.VisibleFlameCount,Is.GreaterThan(0),"battlefield path restored with its lifetime");
            game.skills.ClearWaveEffects();await Awaitable.NextFrameAsync();
            Assert.That(ground.particleCount,Is.Zero,"cleanup removes persistent fire");
        }

        [Test,LoadScene(Scene),Timeout(40000)]
        public async Task TrialBlink_StaysOnTrialPlatform_AndGravityAreaUsesRealLifetime()
        {
            var sandbox=await V3CombatSmokeTest.StartSandbox();var game=sandbox.game;var world=WorldMap.Active;
            game.skills.Learn(SkillId.Blink);game.skills.Learn(SkillId.GravityWell);
            var hole=world.holes.First(h=>h.Phase==HolePhase.Active);
            hole.Feed(hole.feedsToOpen,game.waves.WaveIndex+1);hole.Tick(2f);
            V3CombatSmokeTest.Teleport(game.player,hole.Centre);
            Assert.That(world.TryEnterPortal(game.player),Is.True);
            Vector3 destination=game.player.Position+Vector3.right*4f;
            game.player.DebugSetAimPoint(destination);Assert.That(game.skills.TryCast(0),Is.True);
            Assert.That(Query.Flat(game.player.Position-destination).magnitude,Is.LessThan(.1f));
            Assert.That(Query.Flat(game.player.Position-world.trialPlatform.position).magnitude,Is.LessThan(world.trialRadius));
            Assert.That(game.player.IsAlive,Is.True,"blink must not clamp the trial position onto the distant main map");
            game.player.DebugSetAimPoint(destination+Vector3.forward*2f);Assert.That(game.skills.TryCast(1),Is.True);
            await Awaitable.NextFrameAsync();await Awaitable.NextFrameAsync();
            var view=Object.FindAnyObjectByType<SkillGroundPresentation>();
            Assert.That(view.VisibleAreaCount,Is.EqualTo(1));
            var samples=new List<SkillGroundVisual>();game.skills.CopyGroundVisuals(samples);
            Assert.That(Query.Flat(view.areaFields[0].transform.position-samples[0].Position).magnitude,Is.LessThan(.01f));
            game.skills.ClearWaveEffects();await Awaitable.NextFrameAsync();await Awaitable.NextFrameAsync();
            Assert.That(view.VisibleAreaCount,Is.Zero);
        }
    }
}
