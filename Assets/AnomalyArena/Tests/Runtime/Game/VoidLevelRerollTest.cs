using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class VoidLevelRerollTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task VoidTrialWonMidWave_GrantsOneLevelsRewardsAndPopsItsPickUpOnReturn()
        {
            var game = await StartRun();
            var world = WorldMap.Active;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The first wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) world.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
            var hole = world.holes.First(h => h && h.Phase == HolePhase.Active);
            hole.Feed(hole.feedsToOpen, game.waves.WaveIndex + 1);
            hole.Tick(2f);
            V3CombatSmokeTest.Teleport(game.player, hole.Centre);
            var progression = game.progression;
            int level = progression.Level, drafts = progression.BankedDrafts, required = progression.XpRequired;
            float xp = progression.Xp, maxHp = progression.Stats.maxHp, maxEnergy = progression.Stats.maxEnergy;
            Assume.That(world.TryEnterPortal(game.player), Is.True, "the player enters the open portal");

            world.CompleteTrial();

            Assert.That(world.TrialWon, Is.True, "a real trial win");
            Assert.That(progression.Level, Is.EqualTo(level), "level number");
            Assert.That(progression.Xp, Is.EqualTo(xp), "XP");
            Assert.That(progression.XpRequired, Is.EqualTo(required), "XP needed for the next level");
            Assert.That(progression.BankedDrafts, Is.EqualTo(drafts + 1), "one extra card pick");
            Assert.That(progression.Stats.maxHp, Is.EqualTo(maxHp + progression.Rules.maxHpPerLevel).Within(.01f), "max HP grows as on a level-up");
            Assert.That(progression.Stats.maxEnergy, Is.EqualTo(maxEnergy + progression.Rules.maxEnergyPerLevel).Within(.01f), "max energy grows as on a level-up");
            Assert.That(game.UiSnapshot.voidLevel, Is.True, "the level turns purple below max level");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Draft), "the greater void reward is offered first");
            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => game.RunPhase != ArenaUiState.Draft);
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 6f, "the extra pick pops up once the wave resumes");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task PauseAndResume_DuringTheWaveIntro_GrantsNoFreshReroll()
        {
            var game = await StartRun();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveIntro), "the first wave is being introduced");
            var progression = game.progression;
            progression.GainXp(progression.XpRequired);
            progression.DrawCards(1);
            Assume.That(progression.Reroll(), Is.True, "the wave's reroll is spent");

            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            await CommandUntil(game, ArenaUiCommand.Resume, () => game.RunPhase != ArenaUiState.Paused);

            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveIntro), "the wave intro resumed");
            Assert.That(progression.RerollsRemaining, Is.Zero);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollUpgradeCard_RerollingACard_NeverOffersTheSameWeaponAndDirection()
        {
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var weapons = GameManager.Instance.runWeapons;
            var tuning = weapons.upgrades;
            float chance = tuning.reinfuseChance;
            try
            {
                tuning.reinfuseChance = .5f;

                weapons.Hand = new[] { new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, element = ElementKind.Fire, axes = UpgradeAxis.Infusion }, null };
                weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
                var repeated = new List<UpgradeAxis>();

                for (int i = 0; i < 40; i++)
                {
                    var old = (WeaponUpgradeOffer)weapons.RollUpgradeCard().Payload;
                    var actual = (WeaponUpgradeOffer)weapons.RollUpgradeCard(old).Payload;
                    if (actual.Weapon == old.Weapon && actual.Axis == old.Axis) repeated.Add(old.Axis);
                }

                Assert.That(repeated, Is.Empty);
            }
            finally { tuning.reinfuseChance = chance; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollUpgradeCard_RerollingTheOnlyOpenDirection_OffersItAgain()
        {
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var weapons = GameManager.Instance.runWeapons;

            weapons.Hand = new[] { new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, element = ElementKind.Fire, builtInElement = true,
                axes = UpgradeAxis.Infusion | UpgradeAxis.Twist }, null };
            weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
            var old = (WeaponUpgradeOffer)weapons.RollUpgradeCard().Payload;

            var actual = weapons.RollUpgradeCard(old);

            Assert.That(((WeaponUpgradeOffer)actual.Payload).Axis, Is.EqualTo(UpgradeAxis.Tame));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RollCard_RerollingASkillCard_OffersAnotherSkill()
        {
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var skills = GameManager.Instance.skills;
            const int wave = 7;
            Assume.That(skills.Catalog.skills.Count(d => d && !d.greaterVoid && SkillLoadout.OffersNewSkill(d.category, wave)), Is.GreaterThan(1),
                "the wave's band holds more than one skill");
            var repeated = new List<SkillId>();

            for (int i = 0; i < 40; i++)
            {
                var old = (SkillId)skills.RollCard(wave).Payload;
                var actual = (SkillId)skills.RollCard(wave, old).Payload;
                if (actual == old) repeated.Add(old);
            }

            Assert.That(repeated, Is.Empty);
        }

        static async Task<GameManager> StartRun()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            return game;
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
