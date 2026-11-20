using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// A monster's wall slam takes a share of its own max HP (GameManager.Rules.WallSlamShare: an elite half of it, a boss the fixed
    /// slam grown with the wave's HP). On the player, a slam their own knock started takes RunWeapons.SelfDamageScale and one an enemy's
    /// knock started RunWeapons.EnemySlamScale.
    /// </summary>
    [TestFixture]
    public class WallSlamShareTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        static readonly MethodInfo TakeWallDamage = typeof(Combatant).GetMethod("TakeWallDamage", BindingFlags.Instance | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown() => DifficultyScales.Neutral = true;

        [TestCase(.2f, false, ExpectedResult = .2f)]
        [TestCase(.2f, true, ExpectedResult = .1f)]
        [TestCase(1.5f, false, ExpectedResult = 1f)]
        [TestCase(-1f, true, ExpectedResult = 0f)]
        public float WallSlamShare_IsTheRulesShare_HalfForAnElite_WithinZeroAndOne(float share, bool elite) =>
            new GameManager.Rules { wallSlamHpShare = share }.WallSlamShare(elite);

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task MonsterSlam_TakesItsShareOfMaxHp_AnEliteHalf()
        {
            var game = await StartRun();
            Assert.That(TakeWallDamage, Is.Not.Null, "Combatant.TakeWallDamage");
            Vector3 side = Query.Flat(game.cam.transform.right).normalized;
            // A Grunt as it comes and a Stomper made elite (a Grunt cannot be one); any opening shield is taken away so the slam reaches HP.
            foreach (var (kind, elite) in new[] { (EnemyKind.Grunt, false), (EnemyKind.Stomper, true) })
            {
                var monster = game.waves.SpawnLab(kind, game.player.Position + side * 8f, elite, ElementKind.None);
                Assert.That(monster, Is.Not.Null, "a lab monster");
                Assert.That(monster.Elite, Is.EqualTo(elite), "spawned as asked");
                monster.enabled = false; // it stands still and does not hit back
                ElementStatus.For(monster)?.SetShield(0f);
                float before = monster.Hp;
                TakeWallDamage.Invoke(monster, new object[] { Vector3.left, null, null });
                float expected = Mathf.Min(before, monster.maxHp * game.rules.WallSlamShare(elite));
                Assert.That(before - monster.Hp, Is.EqualTo(expected).Within(.01f + expected * .001f),
                    $"{(elite ? "an elite" : "a monster")} loses its share of {monster.maxHp:0} HP");
                monster.DiscardWithoutReward();
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task PlayerSlam_ItsOwnKnockTakesTheSelfScale_AnEnemysKnockTheEnemySlamScale()
        {
            var game = await StartRun();
            DifficultyScales.Neutral = false;
            var player = game.player;
            var weapons = game.runWeapons;
            Assume.That(weapons.SelfDamageScale, Is.Not.EqualTo(weapons.EnemySlamScale), "the scene sets two different scales");
            var enemy = game.waves.SpawnLab(EnemyKind.Grunt, player.Position + Vector3.right * 10f, false, ElementKind.None);
            enemy.enabled = false;
            player.maxHp = 1000f;
            player.Heal(1000f);
            float before = player.Hp;
            TakeWallDamage.Invoke(player, new object[] { Vector3.left, player, null });
            Assert.That(before - player.Hp, Is.EqualTo(game.rules.wallDamage * weapons.SelfDamageScale).Within(.01f), "a slam the player's own knock started");
            before = player.Hp;
            TakeWallDamage.Invoke(player, new object[] { Vector3.left, enemy, null });
            Assert.That(before - player.Hp, Is.EqualTo(game.rules.wallDamage * weapons.EnemySlamScale).Within(.01f), "a slam an enemy's knock started");
            before = player.Hp;
            TakeWallDamage.Invoke(player, new object[] { Vector3.left, null, null });
            Assert.That(before - player.Hp, Is.EqualTo(game.rules.wallDamage * weapons.EnemySlamScale).Within(.01f), "a slam whose start is not known");
            enemy.DiscardWithoutReward();
        }

        static async Task<GameManager> StartRun()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop || game.RunPhase == ArenaUiState.Loadout);
            if (game.RunPhase == ArenaUiState.SupplyDrop)
            {
                await Until(() => game.SupplyDropReady || game.RunPhase != ArenaUiState.SupplyDrop, 5f, "supply crate lands");
                await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            }

            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            // Lab monsters come once the first wave runs; a black hole's first-sight cutscene may come first and is skipped.
            await Until(() =>
            {
                if (game.RunPhase == ArenaUiState.Cutscene && WorldMap.Active) WorldMap.Active.SkipHoleIntro();
                return game.RunPhase == ArenaUiState.WaveActive;
            }, 20f, "the first wave runs");
            game.player.ProtectFor(0f);
            game.player.Invulnerable = false;
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
