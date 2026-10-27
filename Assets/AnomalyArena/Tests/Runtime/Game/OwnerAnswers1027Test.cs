using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
                                                                                                                                    
                                                                                                                                      
                                                                                                                                      
                                                                                                                                           
                                               
    [TestFixture]
    public class OwnerAnswers1027Test
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task WeaponFireHit_BurnsAQuarterOfTheWeaponsDps_ABuiltInFireTimesItsFactor_AboutThreeForTheStream()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, element = ElementKind.Fire, axes = UpgradeAxis.Infusion };
            var stream = new RunWeapon
            {
                form = WeaponForm.Flamethrower, variant = 0, revealed = true, element = ElementKind.Fire, builtInElement = true,
                axes = UpgradeAxis.Infusion,
            };
            var wildfire = new RunWeapon
            {
                form = WeaponForm.Flamethrower, variant = 1, revealed = true, element = ElementKind.Fire, builtInElement = true,
                axes = UpgradeAxis.Infusion,
            };
            weapons.Hand = new[] { rifle, stream };
            var rifleBoard = Bare(targets[0]);
            var streamBoard = Bare(targets[1]);
            var wildfireBoard = Bare(targets[2]);
            var rules = rifleBoard.Rules;

            targets[0].ReceiveDamage(WeaponHit(game, rifle, 2f));
            targets[1].ReceiveDamage(WeaponHit(game, stream, 1f));
            targets[2].ReceiveDamage(WeaponHit(game, wildfire, 1f));
            float wildfireDps = weapons.HitDamage(wildfire) / weapons.CycleSeconds(wildfire);
            float rifleDps = weapons.HitDamage(rifle) / weapons.CycleSeconds(rifle);
            float streamDps = weapons.HitDamage(stream) / weapons.CycleSeconds(stream);

            Assert.That(weapons.TheoreticalDps(rifle), Is.EqualTo(rifleDps).Within(.001f), "DPS is the card's hit damage over its cycle");
            Assert.That(rifleBoard.Fire, Is.EqualTo(rules.fireBurnShare * rifleDps).Within(.001f), "an infused rifle burns a quarter of its DPS");
            Assert.That(streamBoard.Fire, Is.EqualTo(rules.fireBurnShare * streamDps * rules.builtInFireBurnFactor).Within(.001f),
                "a built-in Fire times its own factor");
            Assert.That(streamBoard.Fire, Is.EqualTo(3f).Within(.2f), "the Stream at Mk1 burns about 3 a second");
            Assert.That(wildfireBoard.Fire, Is.EqualTo(rules.fireBurnShare * wildfireDps * rules.wildfireFireBurnFactor).Within(.001f),
                "Wildfire has its own factor");
            Assert.That(wildfireBoard.Fire, Is.EqualTo(3f).Within(.3f), "the Wildfire at Mk1 burns about 3 a second too (owner 2026-10-27)");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PoisonOnAnEnemy_TicksEveryHalfSecond_EachStackDealsPointThreeASecond()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var board = Bare(targets[0]);
            board.enabled = false;
            var rules = board.Rules;
            board.Add(ElementKind.Poison, rules.poisonOnEnemyCap, game.player);
            float hp = targets[0].Hp;

            board.Tick(.49f);
            float beforeFirstTick = targets[0].Hp;
            board.Tick(.02f);
            float afterFirstTick = targets[0].Hp;

            Assert.That((rules.poisonOnEnemyInterval, rules.poisonOnEnemyDamagePerStack), Is.EqualTo((.5f, .3f)), "the owner's numbers");
            Assert.That(beforeFirstTick, Is.EqualTo(hp), "nothing before half a second");
            Assert.That(hp - afterFirstTick, Is.EqualTo(Mathf.Floor(rules.poisonOnEnemyCap * .3f * .5f * ElementCombat.WaveScale)).Within(.01f),
                "ten stacks deal 1.5 (× the wave scale) on the first tick");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Singularity_UnderTimeStop_LastsThreeOfThePlayersSeconds_AndSlowsTheEnemiesItDrawsInBySeventyPercent()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            skills.ResetRun();
            Assume.That(skills.Learn(SkillId.Singularity), Is.True, "the Singularity is learned");
            game.progression.GainEnergy(1000f);
            var definition = skills.Catalog.Find(SkillId.Singularity);
                                                                                                                                         
            targets[0].enabled = true;
            await Until(() => SkillWorldAPI.IsOnScreen == null || SkillWorldAPI.IsOnScreen(targets[0].Position), 4f, "the camera shows the target");
            try
            {
                SkillWorldAPI.TimeSlip?.Invoke(.3f, 30f);
                Assume.That(game.WorldTimeScale, Is.EqualTo(.3f).Within(.001f), "the world runs at 30%");
                Assume.That(skills.TryCast(Array.FindIndex(skills.Slots, s => !s.Empty && s.Id == SkillId.Singularity)), Is.True, "it is cast");
                float start = Time.time;
                await Awaitable.NextFrameAsync();
                float slowed = SkillLoadout.EnemySlowMultiplier(targets[0]);
                await Until(() => !skills.SingularityActive, 6f, "the Singularity ends");
                float lasted = Time.time - start;

                Assert.That(definition.singularitySlowMultiplier, Is.EqualTo(.3f).Within(.001f), "slowed by 70%");
                Assert.That(slowed, Is.EqualTo(.3f).Within(.001f), "an enemy it draws in moves at 30%");
                Assert.That(lasted, Is.EqualTo(definition.Duration(1)).Within(.3f), "3 of the player's seconds, not stretched by Time Stop");
            }
            finally
            {
                SkillWorldAPI.TimeSlip?.Invoke(1f, 0f);
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task HudShieldBar_IsFullAtSixtyPercentOfMaxHp()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var hud = Object.FindAnyObjectByType<V2Hud>();
            Assume.That(hud && hud.shieldFill, Is.True, "the HUD has its Shield bar");
            var board = ElementStatus.For(game.player);
            board.enabled = false;
            board.SetShield(game.player.maxHp * .3f);
                                                               
            hud.RefreshView();

            Assert.That(hud.shieldFill.fillAmount, Is.EqualTo(.5f).Within(.02f), "30% of max HP fills half the bar");
        }

                              

        static DamageInfo WeaponHit(GameManager game, RunWeapon weapon, float amount)
        {
            var info = DamageInfo.Attack(amount, game.player);
            info.element = weapon.element;
            info.builtInElement = weapon.builtInElement;
            info.weapon = weapon;
            return info;
        }

                                                                                      
        static ElementStatus Bare(Combatant body)
        {
            var board = ElementStatus.For(body);
            board.ClearStatuses();
            board.SetShield(0f);
            return board;
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
