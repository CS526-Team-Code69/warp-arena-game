using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
                                                                                                                                   
                                                                                                                                      
                                                                                                                                         
                                                                                                        
    [TestFixture]
    public class VoidWalkDamageCardTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task VoidWalk_EnemyNearby_SearchesWhereYouVanishedWithoutAttacking_ForFiveSeconds()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var skills = Learned(game);
            Vector3 origin = game.player.Position;
            var grunt = Spawn(game, EnemyKind.Grunt, origin + Vector3.forward * 3f);

            Assume.That(skills.TryCast(SlotOf(skills, SkillId.VoidWalk)), Is.True, "Void Walk is cast");
            float start = Time.time;
            V3CombatSmokeTest.Teleport(game.player, origin + Vector3.back * 5f);
            Physics.SyncTransforms();
            bool woundUp = false;
            while (Time.time - start < 3f)
            {
                woundUp |= grunt.WindingUp;
                await Awaitable.NextFrameAsync();
            }

            bool stillHidden = skills.VoidWalkActive;
            float fromVanish = Query.Flat(grunt.Position - origin).magnitude;
            await Until(() => !skills.VoidWalkActive, 3f, "the void state ends");
            float lasted = Time.time - start;

            Assert.That(woundUp, Is.False, "no attack starts on a player who cannot be targeted");
            Assert.That(fromVanish, Is.LessThan(grunt.radius + 1.5f), "it searches where the player vanished");
            Assert.That(stillHidden, Is.True, "still in the void after 3 s");
            Assert.That(lasted, Is.EqualTo(5f).Within(.3f), "Void Walk lasts 5 s");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task VoidWalk_EnemyShot_FliesThroughThePlayer()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var skills = Learned(game);
            Vector3 origin = game.player.Position;
            var spitter = Spawn(game, EnemyKind.Spitter, origin + Vector3.forward * 6f);
            spitter.enabled = false;
            Assume.That(skills.TryCast(SlotOf(skills, SkillId.VoidWalk)), Is.True, "Void Walk is cast");

            game.waves.LaunchSpit(spitter, spitter.Position, Vector3.back);
            var bolt = Object.FindObjectsByType<EnemyBolt>(FindObjectsSortMode.None).FirstOrDefault(b => b.gameObject.activeSelf && b.Hostile);
            Assume.That(bolt, Is.Not.Null, "a saved bolt is launched");
            await Until(() => !bolt.gameObject.activeSelf || Vector3.Dot(bolt.transform.position - game.player.Position, Vector3.back) > 1.5f, 3f,
                "the spit reaches the player");

            Assert.That(bolt.gameObject.activeSelf, Is.True, "the spit flies on past the player instead of stopping on them");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task VoidWalk_RunsOut_BothHandWeaponsCarryVoidForFiveSeconds_ThenTheirOwnElement()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var (rifle, flamer) = HandPair(game);
            var skills = Learned(game);
            Assume.That(skills.TryCast(SlotOf(skills, SkillId.VoidWalk)), Is.True, "Void Walk is cast");

            await Until(() => !skills.VoidWalkActive, 6f, "the void state ends");
            float ended = Time.time;
            var during = (rifle.element, flamer.element);
            float left = game.runWeapons.VoidEnchantLeft;
            await Until(() => game.runWeapons.VoidEnchantLeft <= 0f, 6f, "the enchant runs out");
            float lasted = Time.time - ended;

            Assert.That(during, Is.EqualTo((ElementKind.Void, ElementKind.Void)), "both hand weapons carry Void");
            Assert.That(left, Is.EqualTo(5f).Within(.1f), "for 5 s");
            Assert.That(lasted, Is.EqualTo(5f).Within(.3f), "and it runs out after 5 s");
            Assert.That((rifle.element, flamer.element), Is.EqualTo((ElementKind.None, ElementKind.Fire)), "then they get their own element back");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task VoidEnchant_WaveEffectsCleared_GivesTheElementsBackAtOnce()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var (rifle, flamer) = HandPair(game);

            game.runWeapons.VoidEnchantHands(5f);
            var during = (rifle.element, flamer.element);
            game.runWeapons.ClearWaveEffects();

            Assert.That(during, Is.EqualTo((ElementKind.Void, ElementKind.Void)), "enchanted");
            Assert.That((rifle.element, flamer.element), Is.EqualTo((ElementKind.None, ElementKind.Fire)), "own elements back");
            Assert.That(game.runWeapons.VoidEnchantLeft, Is.Zero, "nothing left running");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task DamageCard_ScalesDirectDamageSkillsIncluded_AndAddsNoCritChance()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && ArenaText.IsChinese, 12f, "scene and Chinese localization initialize");
            var rules = GameManager.Instance.progression.Rules;
            var row = rules.statCards[(int)StatId.Damage];
            var card = new StatCardOffer(StatId.Damage, CardRarity.Common, row.common, row.bonus, row.bonusCommon);

            Assert.That(row.bonus, Is.EqualTo(StatBonus.None), "no bonus effect");
            Assert.That(card.Description, Does.Contain("Direct damage, skills included, scaled up"), "the card says what it scales");
            Assert.That(card.Description, Does.Not.Contain("Crit"), "and mentions no crit");
        }

                              

        static SkillLoadout Learned(GameManager game)
        {
            var skills = game.skills;
            skills.ResetRun();
            Assume.That(skills.Learn(SkillId.VoidWalk), Is.True, "Void Walk is learned");
            game.progression.GainEnergy(1000f);
            return skills;
        }

        static (RunWeapon rifle, RunWeapon flamer) HandPair(GameManager game)
        {
            var weapons = game.runWeapons;
            weapons.ClearWaveEffects();
            var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, rounds = weapons.ClipSize(WeaponForm.Rifle, 0) };
            var flamer = new RunWeapon
            {
                form = WeaponForm.Flamethrower, variant = 0, revealed = true, element = ElementKind.Fire, builtInElement = true,
                axes = UpgradeAxis.Infusion, rounds = weapons.ClipSize(WeaponForm.Flamethrower, 0),
            };
            weapons.Hand[0] = rifle;
            weapons.Hand[1] = flamer;
            return (rifle, flamer);
        }

        static int SlotOf(SkillLoadout skills, SkillId id) => Array.FindIndex(skills.Slots, s => !s.Empty && s.Id == id);

                                                                                                               
        static Enemy Spawn(GameManager game, EnemyKind kind, Vector3 at)
        {
            var enemy = game.waves.SpawnLab(kind, at, false, ElementKind.None);
            Assume.That(enemy, Is.Not.Null, $"the pool has a free {kind}");
            enemy.SuppressRewards = true;
            enemy.enabled = true;
            Physics.SyncTransforms();
            return enemy;
        }

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
