using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The enemies and the build phase, in the saved Chinese arena. A kind whose authored speed
    /// reaches Enemy.gravityFollowSpeed (before the scene's global movement multiplier) follows the player into a black hole's gravity ring
    /// and is swallowed, a slower kind waits at the ring's edge, and with the player outside every ring even a fast kind walks round them
    /// (document 9.4). The whitebox Bloater is yellow from the start of its rush (document 9.2); an elite in a Chanter's aura shows the
    /// chant tag after its affix; the Nash Prophet's gravity pull comes only from phase 2 (document 12.1). In the build phase time flows with
    /// a panel open as in a wave without enemies: timers, statuses, void energy and black holes run, while the player still cannot move,
    /// fire or cast and no wave clock or spawn runs; the weapon card and the greater void reward keep their freeze (document 2.2). While
    /// the build phase's supply crate comes down time flows too, and the player walks and may take an open portal; back from that trial
    /// the crate comes down again.</summary>
    [TestFixture]
    public class BuildTimeAndEnemyRulesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        WaveSet originalWaves, runtimeWaves;
        bool artWas;

        [SetUp]
        public void SetUp() => artWas = Art.Enabled;

        [TearDown]
        public void TearDown()
        {
            Art.Enabled = artWas;
            SkillWorldAPI.HoleProtection = null;
            if (runtimeWaves)
            {
                if (GameManager.Instance && GameManager.Instance.waves) GameManager.Instance.waves.waveSet = originalWaves;
                Object.Destroy(runtimeWaves);
            }

            originalWaves = runtimeWaves = null;
            Time.timeScale = 1f;
        }

        // ───── The Nash Prophet's far moves (no scene) ─────

        [Test]
        public void NashMoveFor_FarInPhaseOne_OnlyTheRing_TheGravityPullComesFromPhaseTwo()
        {
            var b = new BossTuning();
            var phaseOne = new HashSet<int>();
            for (int i = 0; i < 400; i++)
            foreach (bool pillars in new[] { false, true })
                phaseOne.Add(Enemy.NashMoveFor(1, false, true, pillars, i / 400f, b));

            Assert.That(phaseOne, Is.EquivalentTo(new[] { Enemy.RingMove }), "phase 1, far: only the equilibrium ring, whatever the roll");
            for (int phase = 2; phase <= 3; phase++)
            {
                int stage = phase;
                Assert.That(Enumerable.Range(0, 400).Select(i => Enemy.NashMoveFor(stage, false, true, false, i / 400f, b)),
                    Has.Member(Enemy.GravityPullMove), $"phase {phase}, far: the gravity pull comes");
            }
        }

        // ───── Chasing into a gravity ring (document 9.4) ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task GravityRing_PlayerStandingInIt_FastKindsFollowAndAreSwallowed_SlowKindsWaitAtItsEdge()
        {
            var (game, targets) = await Sandbox();
            var world = WorldMap.Active;
            // Who follows: the authored speed against the tunable threshold, never the scene's slowed walking speed.
            var probe = targets[0];
            var follows = new List<EnemyKind>();
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                var data = game.waves.DefinitionOf(kind);
                if (!data || EnemyRoles.IsBoss(kind)) continue;
                probe.Configure(data, 1f, false);
                if (probe.FollowsIntoGravity) follows.Add(kind);
            }

            // 2026-10-27 (P35): the threshold rose to 5, so the 4.5-speed Stomper, Warden and Chanter no longer follow.
            Assert.That(probe.gravityFollowSpeed, Is.EqualTo(5f), "the threshold is 5");
            Assert.That(follows, Is.EquivalentTo(new[] { EnemyKind.Grunt, EnemyKind.Skitter, EnemyKind.Splitter, EnemyKind.Shade }),
                "only the kinds authored at 5 or faster follow; the 4.5 kinds and the slower ones do not");
            foreach (var target in targets) target.DiscardWithoutReward();

            var (hole, inside, start) = RingApproach(world);
            SkillWorldAPI.HoleProtection = body => body == game.player; // the player stands in the ring without being drawn in
            V3CombatSmokeTest.Teleport(game.player, inside);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            Assume.That(hole.InGravity(game.player.Position), Is.True, "the player stands in the gravity ring");

            // A slow kind walks up to the ring and waits outside it.
            var slow = Spawn(game, EnemyKind.Brute, start);
            Assume.That(slow.FollowsIntoGravity, Is.False, "the Brute is a slow kind");
            bool slowEntered = false;
            await ForGameSeconds(5f, () => slowEntered |= world.GetPull(slow.Position).sqrMagnitude > .01f);
            Assert.That(slowEntered, Is.False, "a slow kind never steps into the ring");
            Assert.That(slow.IsAlive, Is.True, "nor is it swallowed");
            Assert.That(Flat(slow.Position - hole.Centre).magnitude, Is.LessThan(hole.GravityRadius + 2.5f),
                "it has walked up to the ring's edge and waits there");
            slow.DiscardWithoutReward();

            // A fast kind follows the player in, and the pull takes it into the hole.
            int feeds = hole.Feeds;
            var fast = Spawn(game, EnemyKind.Grunt, start);
            Assume.That(fast.FollowsIntoGravity, Is.True, "the Grunt is a fast kind");
            bool fastEntered = false;
            await UntilFixed(() =>
            {
                fastEntered |= hole.InGravity(fast.Position);
                return !fast.IsAlive;
            }, 10f, "the Grunt follows the player into the ring and goes down");
            Assert.That(fastEntered, Is.True, "a fast kind walks into the ring after the player");
            Assert.That(fast.DeathCause, Is.EqualTo(DeathCause.FellIntoGap), "the black hole swallows it");
            Assert.That(hole.Feeds, Is.EqualTo(feeds + 1), "and counts it toward its portal");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task GravityRing_PlayerOutsideEveryRing_AFastKindWalksRoundIt()
        {
            var (game, targets) = await Sandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            var (_, you, start) = AcrossRing(world);
            V3CombatSmokeTest.Teleport(game.player, you);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            Assume.That(world.GetPull(game.player.Position).sqrMagnitude, Is.LessThan(.01f), "the player stands outside every ring");

            var fast = Spawn(game, EnemyKind.Grunt, start);
            Assume.That(fast.FollowsIntoGravity, Is.True, "the Grunt is a fast kind");
            float away = Flat(start - you).magnitude, closest = away;
            bool entered = false;
            await ForGameSeconds(6f, () =>
            {
                entered |= world.GetPull(fast.Position).sqrMagnitude > .01f;
                closest = Mathf.Min(closest, Flat(fast.Position - you).magnitude);
            });

            Assert.That(closest, Is.LessThan(away - 2f), "the Grunt heads for the player");
            Assert.That(entered, Is.False, "round the ring: only a player standing in one draws the fast kinds in");
        }

        // ───── The Bloater's colour in whitebox (document 9.2) ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Bloater_Whitebox_YellowFromTheStartOfItsRush_ItsOwnColourWhenTheRushRunsOut_YellowOnItsFuse()
        {
            var (game, targets) = await Sandbox();
            Art.Enabled = false;
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var bloater = targets[0];
            // A copy with a short rush, which runs out before it reaches the player; the saved definition stays as it is.
            var data = Object.Instantiate(game.waves.DefinitionOf(EnemyKind.Bloater));
            data.chargeDistance = 2f;
            try
            {
                Release(bloater, data, game.player.Position + Vector3.forward * (data.triggerDistance - .5f));
                Assert.That(Diff(BodyColour(bloater), data.colour), Is.LessThan(.01f), "its own colour while it walks in");

                await UntilFixed(() => bloater.Rushing, 3f, "the Bloater starts its rush");
                Assert.That(Diff(BodyColour(bloater), Enemy.WindupBodyColour), Is.LessThan(.01f), "yellow from the start of its rush, as its illustration");

                await UntilFixed(() => !bloater.Rushing, 2f, "the short rush ends");
                Assume.That(bloater.WindingUp, Is.False, "the rush ran out short of the player");
                Assert.That(Diff(BodyColour(bloater), data.colour), Is.LessThan(.01f), "its own colour again once the rush has run out");

                await UntilFixed(() => bloater.WindingUp, 5f, "it reaches the player and lights its fuse");
                await Awaitable.NextFrameAsync(); // the warning flash is drawn late in the frame
                var fuse = BodyColour(bloater);
                Assert.That(fuse.r, Is.GreaterThan(.9f), "the lit fuse keeps the body yellow, flashing toward red");
                Assert.That(Diff(fuse, data.colour), Is.GreaterThan(.3f), "never its own colour while the fuse burns");
            }
            finally
            {
                if (bloater && bloater.IsAlive) bloater.DiscardWithoutReward();
                Object.Destroy(data);
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Bloater_OwnFuseAtThePlayer_SwellsBeforeItBlows_ModelAndIllustration()
        {
            var (game, targets) = await Sandbox();
            Art.Enabled = true;
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var bloater = targets[0];
            var data = Object.Instantiate(game.waves.DefinitionOf(EnemyKind.Bloater));
            game.player.ProtectFor(30f);
            var drawn = bloater.GetComponent<V2CharacterIllustration>();
            try
            {
                Release(bloater, data, game.player.Position + Vector3.forward * (data.triggerDistance - .5f));
                await Awaitable.NextFrameAsync();
                float body = bloater.bodyRenderer.transform.localScale.y;
                float picture = drawn && drawn.illustration ? drawn.illustration.transform.lossyScale.y : 0f;
                var model = bloater.GetComponent<V2ModelPresentation>();
                float shown = model && model.modelRoot ? model.modelRoot.localScale.y : 0f;
                Assume.That(bloater.Swell, Is.EqualTo(1f), "it walks in at its own size");

                // Document 9.2: once at the player it lights its fuse and swells before it blows (it used to swell only after a death).
                await UntilFixed(() => bloater.WindingUp, 5f, "it reaches the player and lights its fuse");
                float fuse = Mathf.Max(.05f, data.windup);
                await UntilFixed(() => !bloater.WindingUp || bloater.Swell > 1f + .2f, fuse * 3f, "the fuse burns");
                Assert.That(bloater.Swell, Is.GreaterThan(1.2f), "the Bloater has swollen by its fuse's end");
                Assert.That(bloater.bodyRenderer.transform.localScale.y, Is.GreaterThan(body * 1.2f), "the whitebox body with it");
                if (model && model.UsingModel)
                    Assert.That(model.modelRoot.localScale.y, Is.GreaterThan(shown * 1.15f), "and its 3D model, the look the illustrated style shows");
                if (drawn && drawn.illustration && drawn.illustration.enabled)
                    Assert.That(drawn.illustration.transform.lossyScale.y, Is.GreaterThan(picture * 1.15f), "and the illustration");
            }
            finally
            {
                if (bloater && bloater.IsAlive) bloater.DiscardWithoutReward();
                Object.Destroy(data);
            }
        }

        // ───── The chant tag on an elite's bar (document 9.2) ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task EliteInAChanterAura_ItsHpBarLabel_ShowsTheChantTagAfterItsAffix()
        {
            var (game, targets) = await Sandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var labels = Object.FindAnyObjectByType<V2WorldLabels>();
            Assume.That(labels, Is.Not.Null, "the scene has its world labels");
            Vector3 origin = game.player.Position;
            var kind = new[] { EnemyKind.Stomper, EnemyKind.Rammer, EnemyKind.Bloater, EnemyKind.Spire }
                .First(k => game.waves.DefinitionOf(k) is { canElite: true, canAffix: true });
            var elite = Hold(game.waves.SpawnLab(kind, origin + Vector3.forward * 5f, true, ElementKind.Fire));
            var plain = Hold(game.waves.SpawnLab(EnemyKind.Grunt, origin + Vector3.forward * 3f, false, ElementKind.None));
            var chanter = Hold(game.waves.SpawnLab(EnemyKind.Chanter, origin + Vector3.forward * (5f + Enemy.ChantRadius + 4f), false, ElementKind.None));
            Physics.SyncTransforms();
            Assume.That(elite.Elite && elite.Affix == ElementKind.Fire, Is.True, "an elite with the Fire affix");
            // An elite names its kind, then the elite tag and its affix. Ordinary enemies (the basics, such as the Grunt)
            // show only their bar, while specials (the Chanter), elites and bosses keep their name; status words still show on any enemy.
            string eliteTag = ArenaText.Text("enemy." + kind) + " · " + ArenaText.Text("ui.elite") + " · " + ArenaTerms.Element(ElementKind.Fire);
            string grunt = ArenaText.Text("enemy.Grunt");
            string chant = ArenaText.Pair("吟唱强化", "CHANTED");

            await Until(() => Texts(labels).Contains(eliteTag), 3f, "outside the aura the elite's bar shows its kind and affix");
            Assert.That(elite.Chanted, Is.False, "outside the aura");
            Assert.That(Texts(labels), Has.Member(ArenaText.Text("enemy.Chanter")), "a special enemy names its kind");
            Assert.That(Texts(labels), Has.No.Member(grunt), "an ordinary enemy shows only its bar");

            V3CombatSmokeTest.Teleport(chanter, origin + Vector3.forward * 8f);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            Assume.That(elite.Chanted && plain.Chanted, Is.True, "both stand in the Chanter's aura");

            await Until(() => Texts(labels).Contains(eliteTag + " · " + chant), 3f, "inside the aura the elite's bar adds the chant tag after its affix");
            Assert.That(Texts(labels), Has.Member(chant), "an ordinary enemy shows the chant tag on its own");
        }

        // ───── Time in the build phase (document 2.2) ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task BuildPanelOpen_TimeFlows_TimersStatusesEnergyAndHolesRun_ThePlayerCannotActAndNoWaveRuns()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 1.5f);
            await ReachBuildCrate(game);
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            var world = WorldMap.Active;
            var player = game.player;
            var status = ElementStatus.For(player);
            using var keyboard = new TestKeyboard();

            Assert.That(game.BuildWalking || game.CanPlayerMove, Is.False, "the armory panel is open, so the player stands");
            Assert.That(game.BuildTimeFlows, Is.True, "the build phase's clock runs");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "time flows with the panel open");

            // Away from every ring: a skill and a weapon cooling down, a burn, void energy below its cap and a black hole fed to the full.
            Vector3 stand = LaneStart(world, Vector3.right, 6f);
            V3CombatSmokeTest.Teleport(player, stand);
            Physics.SyncTransforms();
            Assume.That(game.skills.Learn(SkillId.Overcharge), Is.True, "the player learns a skill, which brings the energy bar");
            var slot = game.skills.Slots[0];
            slot.CooldownLeft = 5f;
            var weapon = game.runWeapons.Hand[0];
            weapon.cooldown = 5f;
            game.progression.CompleteVoidTrial(); // void energy regenerates by itself
            Assume.That(game.progression.TrySpendEnergy(game.progression.Energy * .5f), Is.True, "energy spent below its cap");
            float energy = game.progression.Energy;
            status.Add(ElementKind.Fire, 4f);
            float fire = status.Fire, hp = player.Hp;
            var portal = world.holes.First(h => h && h.Phase == HolePhase.Active);
            portal.Feed(portal.feedsToOpen, game.waves.WaveIndex + 1);
            Assume.That(portal.Phase, Is.EqualTo(HolePhase.Collapsing), "a black hole fed to the full starts collapsing");
            float waveClock = game.waves.TimeLeft, start = Time.time;
            player.debugMoveHeld = Vector2.right; // a held key: the panel keeps the player standing
            try
            {
                await Until(() => portal.Phase == HolePhase.Portal && Time.time - start >= 1.5f, 8f,
                    "the hole collapses into a portal with the panel open");
            }
            finally
            {
                player.debugMoveHeld = null;
            }

            float passed = Time.time - start;
            Assert.That(slot.CooldownLeft, Is.EqualTo(5f - passed).Within(.25f), "the skill cooldown runs");
            Assert.That(weapon.cooldown, Is.EqualTo(5f - passed).Within(.25f), "the weapon cooldown runs");
            Assert.That(game.progression.Energy, Is.GreaterThan(energy + 1f), "void energy regenerates");
            Assert.That(status.Fire, Is.LessThan(fire), "the player's burn ticks");
            Assert.That(player.Hp, Is.LessThan(hp), "and hurts");
            Assert.That(Flat(player.Position - stand).magnitude, Is.LessThan(.05f), "the player cannot walk with the panel open");
            Assert.That(game.CanUseCombatInput, Is.False, "nor fire");
            slot.CooldownLeft = 0f;
            Assert.That(game.skills.TryCast(0), Is.False, "nor cast a skill");
            Assert.That(game.waves.Phase, Is.EqualTo(WavePhase.WaitingBuild), "no wave starts");
            Assert.That(game.waves.TimeLeft, Is.EqualTo(waveClock), "the wave clock stays stopped");
            Assert.That(game.waves.LivingCount + game.waves.PendingCount, Is.Zero, "nothing spawns");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Armory), "the panel is still open");

            // The other black hole draws the player in with the panel open, exactly as while walking.
            var hole = world.holes.FirstOrDefault(h => h && h.Phase == HolePhase.Active);
            Assume.That(hole, Is.Not.Null, "a second black hole is still active");
            player.Invulnerable = true; // a swallow would set the player back on safe ground instead of ending the run
            try
            {
                V3CombatSmokeTest.Teleport(player, RingSpot(world, hole));
                Physics.SyncTransforms();
                await Awaitable.FixedUpdateAsync();
                float before = Flat(player.Position - hole.Centre).magnitude;
                for (int step = 0; step < 4; step++) await Awaitable.FixedUpdateAsync();
                Assert.That(Flat(player.Position - hole.Centre).magnitude, Is.LessThan(before - .1f), "the ring pulls the player toward its hole");
            }
            finally
            {
                player.Invulnerable = false;
                V3CombatSmokeTest.Teleport(player, stand);
                Physics.SyncTransforms();
            }

            // Tab puts the panel away: the player walks, and the clock keeps running.
            await keyboard.TapUntil(KeyCode.Tab, () => game.BuildWalking, "Tab puts the panel away");
            weapon.cooldown = 3f;
            player.debugMoveHeld = Vector2.right;
            try
            {
                await ForGameSeconds(.5f, null);
            }
            finally
            {
                player.debugMoveHeld = null;
            }

            Assert.That(Flat(player.Position - stand).magnitude, Is.GreaterThan(.5f), "with the panel put away the player walks");
            Assert.That(weapon.cooldown, Is.LessThan(2.7f), "and the timers keep running");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task BuildPhase_SupplyCrateFlows_WeaponCardAndGreaterVoidReward_KeepTheirFreeze()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 1.5f);
            await ReachBuildCrate(game);
            var world = WorldMap.Active;
            Assume.That(game.skills.Learn(SkillId.Overcharge), Is.True, "the player learns a skill");
            var slot = game.skills.Slots[0];

            // While the build phase's supply crate comes down, time flows and the player walks the empty arena.
            var player = game.player;
            var stand = player.Position;
            slot.CooldownLeft = 5f;
            player.debugMoveHeld = Vector2.right;
            try
            {
                await ForGameSeconds(.5f, null);
            }
            finally
            {
                player.debugMoveHeld = null;
            }

            Assert.That(Time.timeScale, Is.EqualTo(1f), "time flows while the build phase's supply crate comes down");
            Assert.That(game.BuildTimeFlows && game.BuildCrateWalking, Is.True, "its clock runs");
            Assert.That(slot.CooldownLeft, Is.LessThan(4.7f), "and so do the timers");
            Assert.That(Flat(player.Position - stand).magnitude, Is.GreaterThan(.5f), "the player walks");
            Assert.That(game.CanUseCombatInput, Is.False, "but cannot fire");
            Assert.That(game.waves.LivingCount + game.waves.PendingCount, Is.Zero, "and nothing spawns");

            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            await CommandUntil(game, ArenaUiCommand.TakeArmory, () => game.RunPhase != ArenaUiState.Armory, 0);
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.WeaponIntro), "the armory weapon's card opens");
            float onCard = slot.CooldownLeft;
            await RealSeconds(.6f);
            Assert.That(Time.timeScale, Is.Zero, "the weapon card keeps its freeze");
            Assert.That(slot.CooldownLeft, Is.EqualTo(onCard), "the timers wait on it");

            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Ready), "no card pick is banked, so the build is ready");
            Assert.That(game.BuildTimeFlows && Time.timeScale == 1f, Is.True, "the ready panel lets time flow");
            float ready = slot.CooldownLeft;
            await ForGameSeconds(.4f, null);
            Assert.That(slot.CooldownLeft, Is.LessThan(ready - .2f), "and the timers run on it");

            // A void trial entered from the build phase: its greater reward still holds everything.
            var portal = world.holes.First(h => h && h.Phase == HolePhase.Active);
            portal.Feed(portal.feedsToOpen, game.waves.WaveIndex + 1);
            await Until(() => portal.Phase == HolePhase.Portal, 4f, "the fed hole collapses into a portal while the build is ready");
            using var keyboard = new TestKeyboard();
            await keyboard.TapUntil(KeyCode.Tab, () => game.BuildWalking, "Tab puts the panel away");
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Physics.SyncTransforms();
            await keyboard.TapUntil(KeyCode.E, () => world.TrialActive, "E on the portal enters the trial");
            world.CompleteTrial();
            Assume.That(game.RunPhase == ArenaUiState.Draft && game.UiSnapshot.isGreaterReward, Is.True, "the greater void reward is offered");
            slot.CooldownLeft = 5f;
            await RealSeconds(.6f);
            Assert.That(Time.timeScale, Is.Zero, "the greater void reward keeps its freeze");
            Assert.That(game.BuildTimeFlows, Is.False, "its clock does not run, though the trial was entered from the build phase");
            Assert.That(slot.CooldownLeft, Is.EqualTo(5f), "nor do the timers");

            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => !game.UiSnapshot.isGreaterReward);
            if (game.RunPhase == ArenaUiState.SupplyDrop)
            {
                await Until(() => game.SupplyDropReady, 5f, "the crate for the trial's extra pick lands");
                await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase != ArenaUiState.SupplyDrop);
            }

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Draft), "back in the build phase with the trial's extra pick");
            Assert.That(game.BuildTimeFlows && Time.timeScale == 1f, Is.True, "where time flows again");
            float back = slot.CooldownLeft;
            await ForGameSeconds(.4f, null);
            Assert.That(slot.CooldownLeft, Is.LessThan(back - .2f), "and the timers run again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task BuildPhase_SupplyCrate_ThePortalCanBeTaken_AndTheCrateComesDownAgainAfterTheTrial()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 1.5f);
            await ReachBuildCrate(game);
            var world = WorldMap.Active;
            Assume.That(game.BuildCrateWalking, Is.True, "the build phase's crate is down and waits to be opened");

            // While the crate waits above the player, an open portal can be taken with E.
            var portal = world.holes.First(h => h && h.Phase == HolePhase.Active);
            portal.Feed(portal.feedsToOpen, game.waves.WaveIndex + 1);
            await Until(() => portal.Phase == HolePhase.Portal, 4f, "the fed hole collapses into a portal while the crate waits");
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Physics.SyncTransforms();
            Assert.That(world.CanEnterPortal(game.player, portal), Is.True, "standing on the portal, E can take the player in");
            using var keyboard = new TestKeyboard();
            await keyboard.TapUntil(KeyCode.E, () => world.TrialActive, "E on the portal enters the trial");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial), "the trial runs");

            world.CompleteTrial();
            Assume.That(game.UiSnapshot.isGreaterReward, Is.True, "the greater void reward is offered");
            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => !game.UiSnapshot.isGreaterReward);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.SupplyDrop), "back in the build phase, the crate comes down again");
            Assert.That(game.BuildCrateWalking && Time.timeScale == 1f, Is.True, "and time flows while it does");
            await Until(() => game.SupplyDropReady, 5f, "the crate lands again");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase != ArenaUiState.SupplyDrop);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Armory), "and opens the armory the trial interrupted");
        }

        // ───── Helpers ─────

        static Vector3 Flat(Vector3 v) => Query.Flat(v);

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            return GameManager.Instance;
        }

        /// <summary>The combat sandbox (V3CombatSmokeTest) with every black hole fed once, so no first-sight cutscene stops the clock.</summary>
        static async Task<(GameManager game, Enemy[] targets)> Sandbox()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var world = WorldMap.Active;
            if (world.HoleIntroPlaying) world.SkipHoleIntro();
            foreach (var hole in world.holes)
                if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0) hole.Feed(1, sandbox.game.waves.WaveIndex + 1);
            await Awaitable.NextFrameAsync();
            Assume.That(sandbox.game.IsCombatActive, Is.True, "the sandbox is in combat");
            return sandbox;
        }

        void ShortenWaves(GameManager game, float duration)
        {
            originalWaves = game.waves.waveSet;
            runtimeWaves = Object.Instantiate(originalWaves);
            foreach (var wave in runtimeWaves.waves) wave.duration = duration;
            game.waves.waveSet = runtimeWaves;
        }

        /// <summary>Title → supply crate → Rifle → close its card → the first wave's combat (every black hole fed once, so no first-sight
        /// cutscene stops the clock), then the shortened wave runs out and its survivors fall without rewards, until the build phase's
        /// supply crate is down.</summary>
        static async Task ReachBuildCrate(GameManager game)
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

        /// <summary>One enemy of this kind from the saved pool, walking on its own, worth no reward.</summary>
        static Enemy Spawn(GameManager game, EnemyKind kind, Vector3 at)
        {
            var enemy = game.waves.SpawnLab(kind, at, false, ElementKind.None);
            Assume.That(enemy, Is.Not.Null, $"the pool has a free {kind}");
            enemy.SuppressRewards = true;
            enemy.enabled = true;
            Physics.SyncTransforms();
            return enemy;
        }

        /// <summary>A pool enemy that holds still where it was put (its own update is off), worth no reward.</summary>
        static Enemy Hold(Enemy enemy)
        {
            Assume.That(enemy, Is.Not.Null, "the pool has a free body of that kind");
            enemy.SuppressRewards = true;
            enemy.enabled = false;
            return enemy;
        }

        /// <summary>A frozen sandbox target becomes a free-moving live enemy of this definition at a point.</summary>
        static void Release(Enemy enemy, EnemyDefinition data, Vector3 at)
        {
            enemy.Configure(data, 1f, false);
            V3CombatSmokeTest.Teleport(enemy, at);
            enemy.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            enemy.enabled = true;
            Physics.SyncTransforms();
        }

        static List<string> Texts(V2WorldLabels labels) => labels.enemyLabels.Where(l => l.name).Select(l => l.name.text).ToList();

        static Color BodyColour(Enemy enemy)
        {
            var block = new MaterialPropertyBlock();
            enemy.bodyRenderer.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        /// <summary>Colours read back from a property block can differ in the last float bits, so compare with a small tolerance.</summary>
        static float Diff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);

        /// <summary>An active black hole, a spot halfway across its gravity ring for the player and, three cells outside the ring and
        /// turned 50 degrees from that spot, an enemy start with open ground on the straight way in.</summary>
        static (BlackHole hole, Vector3 inside, Vector3 start) RingApproach(WorldMap world)
        {
            foreach (var hole in world.holes)
            {
                if (!hole || hole.Phase != HolePhase.Active) continue;
                Vector3 centre = Flat(hole.Centre);
                float middle = (hole.KillRadius + hole.GravityRadius) * .5f;
                for (int angle = 0; angle < 360; angle += 15)
                {
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    Vector3 inside = centre + dir * middle;
                    Vector3 start = centre + Quaternion.Euler(0f, 50f, 0f) * dir * (hole.GravityRadius + 3f);
                    if (world.IsWalkable(start, 1f) && Open(world, start, inside, hole)) return (hole, inside, start);
                }
            }

            Assert.Fail("No black hole on the map has open ground round its gravity ring.");
            return default;
        }

        /// <summary>An active black hole with open ground on a circle two cells outside its gravity ring: the player's spot on it and an
        /// enemy start 150 degrees round, so the straight line between them crosses the ring and the open way round is the shorter arc.</summary>
        static (BlackHole hole, Vector3 you, Vector3 start) AcrossRing(WorldMap world)
        {
            foreach (var hole in world.holes)
            {
                if (!hole || hole.Phase != HolePhase.Active) continue;
                Vector3 centre = Flat(hole.Centre);
                float radius = hole.GravityRadius + 2f;
                for (int angle = 0; angle < 360; angle += 15)
                {
                    bool open = true;
                    for (int along = 0; along <= 150 && open; along += 10)
                    {
                        Vector3 point = centre + Quaternion.Euler(0f, angle + along, 0f) * Vector3.forward * radius;
                        open = world.IsWalkable(point, 1.2f) && !Quicksand.IsQuicksand(point);
                    }

                    if (open)
                        return (hole, centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius,
                            centre + Quaternion.Euler(0f, angle + 150f, 0f) * Vector3.forward * radius);
                }
            }

            Assert.Fail("No black hole on the map has open ground round its gravity ring.");
            return default;
        }

        /// <summary>Open ground from a to b for a body: inside the map, no wall, no hole's core, no quicksand and no gravity ring but this hole's.</summary>
        static bool Open(WorldMap world, Vector3 a, Vector3 b, BlackHole hole)
        {
            for (float t = 0f; t <= 1f; t += .04f)
            {
                Vector3 point = Vector3.Lerp(a, b, t);
                if (!world.Contains(point, 1f) || world.IsWall(point, 1f) || world.IsHole(point) || Quicksand.IsQuicksand(point)) return false;
                if (!hole.InGravity(point) && world.GetPull(point).sqrMagnitude > .01f) return false;
            }

            return true;
        }

        /// <summary>A spot in the outer part of this black hole's gravity ring with no wall about it.</summary>
        static Vector3 RingSpot(WorldMap world, BlackHole hole)
        {
            float radius = Mathf.Max(hole.KillRadius + .8f, hole.GravityRadius - .6f);
            for (int angle = 0; angle < 360; angle += 15)
            {
                Vector3 at = Flat(hole.Centre) + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                if (world.Contains(at, 1f) && !world.IsWall(at, 1f)) return at;
            }

            Assert.Fail("No open ground in the black hole's gravity ring.");
            return Vector3.zero;
        }

        /// <summary>Start of a straight lane of the given length along dir with no wall, hole, gravity ring or quicksand on it.</summary>
        static Vector3 LaneStart(WorldMap world, Vector3 dir, float length)
        {
            for (int x = -36; x <= 36; x += 4)
            for (int z = -24; z <= 24; z += 4)
            {
                var start = new Vector3(x, 0f, z);
                bool clear = true;
                for (float along = 0f; along <= length && clear; along += .5f)
                {
                    var point = start + dir * along;
                    clear = world.IsWalkable(point, 1.2f) && !Quicksand.IsQuicksand(point);
                }

                if (clear) return start;
            }

            Assert.Fail("The saved map has no clear lane.");
            return Vector3.zero;
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

        /// <summary>Checks completed after every physics step, so a short state (a rush) is not missed between frames.</summary>
        static async Task UntilFixed(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.FixedUpdateAsync();
            }
        }

        /// <summary>Lets this much game time pass, frame by frame, calling eachStep after every frame. Fails when game time stands still
        /// (a frozen screen) instead of waiting for ever.</summary>
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
