using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
                                                                                                                                      
                                                                                                                                    
                                                                                                                                        
                                                             
    [TestFixture]
    [Category("V3Smoke")]
    public class BossNoStaggerTest
    {
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";
        const string ArenaScene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
                                                                                                                        
        const int Hits = 12;
        const float HeavyKnock = 10f;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

                               

        [Test]
        public void StaggerState_IsGoneFromBossesTuningStatusRulesAndHud()
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var type in new[] { typeof(Enemy), typeof(BossTuning), typeof(StatusRules), typeof(ElementStatus), typeof(ArenaUiSnapshot), typeof(V2Hud) })
            {
                var names = type.GetMembers(members).Select(m => m.Name)
                    .Where(n => n.IndexOf("stagger", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                Assert.That(names, Is.Empty, $"{type.Name} keeps no stagger state, tuning or bar");
            }

            Assert.That(typeof(ElementStatus).GetEvent("OnControlThreshold", members), Is.Null,
                "a boss reaching a Frost or Lightning threshold feeds nothing toward a stun");
            Assert.That(typeof(BossTuning).GetField("prophetStunSeconds"), Is.Null, "the Prophet has no stun of its own any more");
            Assert.That(typeof(BossTuning).GetField("tyrantWallStun"), Is.Not.Null, "the Tyrant's wall-hit stun stays");
        }

        [Test]
        public void CardTexts_SayBossesAreNotKnockedBack_NeverStagger()
        {
            const BindingFlags tables = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in typeof(ArenaTerms).GetFields(tables).Where(f => f.FieldType == typeof(string[])))
            foreach (var text in (string[])field.GetValue(null) ?? Array.Empty<string>())
            {
                Assert.That(text ?? "", Does.Not.Contain("stagger"), $"{field.Name}: {text}");
                Assert.That((text ?? "").IndexOf("stagger", StringComparison.OrdinalIgnoreCase), Is.LessThan(0), $"{field.Name}: {text}");
            }

            var tameZh = (string[])typeof(ArenaTerms).GetField("ExpandedTameEn", tables).GetValue(null);
            var tameEn = (string[])typeof(ArenaTerms).GetField("ExpandedTameEn", tables).GetValue(null);
            Assert.That(tameZh.Count(t => t.Contains("{tankPush}") && t.Contains("bosses are not knocked back")), Is.EqualTo(1),
                "the Stream Tame card says bosses are immune to its knockback");
            Assert.That(tameEn.Count(t => t.Contains("{tankPush}") && t.Contains("bosses are not knocked back")), Is.EqualTo(1),
                "and so does its English twin");
        }

                                                     

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task Prophet_HeavyKnockbackHits_NeitherMoveNorKnockNorStunIt()
        {
            var (game, _, hollow) = await BossRedesignTest.StartTrial();
            hollow.enabled = false;                                                            
            await AssertKnockbackChangesNothing(game, hollow);
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task BossHud_AfterHeavyKnockback_ShowsNoStaggerBar()
        {
            var (game, _, hollow) = await BossRedesignTest.StartTrial();
            hollow.enabled = false;
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial), "the lab trial runs as the void trial, so the boss bars show");
            var hud = Object.FindAnyObjectByType<V2Hud>();
            Assume.That(hud && hud.bossRoot && hud.bossShieldFill, Is.True, "the scene HUD has its boss bars");
            Vector3 away = Query.Flat(hollow.Position - game.player.Position).normalized;
            for (int i = 0; i < Hits; i++) hollow.ReceiveDamage(DamageInfo.Attack(1f, game.player, away, HeavyKnock, HitKind.Slam));

                                                                               
            float until = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
            hud.RefreshView();
            Assert.That(hud.bossRoot.activeInHierarchy, Is.True, "the boss bars are up");
            var bars = hud.bossRoot.GetComponentsInChildren<Transform>(true)
                .Concat(hud.bossShieldFill.transform.parent.parent.GetComponentsInChildren<Transform>(true));
            var stagger = bars.Where(t => t.name.IndexOf("stagger", StringComparison.OrdinalIgnoreCase) >= 0).Select(t => t.name).Distinct().ToArray();
            Assert.That(stagger, Is.Empty, "no stagger bar under the boss HP and shield bars");
            Assert.That(typeof(ArenaUiSnapshot).GetField("bossStagger"), Is.Null, "and the HUD snapshot carries no stagger value");
        }

                                             

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task Tyrant_HeavyKnockbackHits_NeitherMoveNorKnockNorStunIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * 12f);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");
            tyrant.enabled = false;                                                            
            Physics.SyncTransforms();
            await AssertKnockbackChangesNothing(game, tyrant);
        }

                              

                                                                                                                                    
                                                                                                                                      
        static async Task AssertKnockbackChangesNothing(GameManager game, Enemy boss)
        {
                                                                                   
            for (int i = 0; i < 5; i++) await Awaitable.FixedUpdateAsync();
            Vector3 settled = Query.Flat(boss.Position);
            Vector3 away = Query.Flat(boss.Position - game.player.Position).normalized;
            bool knocked = false;
            for (int i = 0; i < Hits; i++)
            {
                boss.ReceiveDamage(DamageInfo.Attack(1f, game.player, away, HeavyKnock, HitKind.Slam));
                Assert.That(boss.BossStunned, Is.False, $"hit {i + 1}: knockback never stuns a boss");
                await Awaitable.FixedUpdateAsync();
                knocked |= boss.State is CharacterState.Knocked or CharacterState.Thrown;
            }

            for (int i = 0; i < 10; i++)
            {
                await Awaitable.FixedUpdateAsync();
                knocked |= boss.State is CharacterState.Knocked or CharacterState.Thrown;
            }

            Assert.That(knocked, Is.False, "never knocked back");
            Assert.That(Query.Flat(boss.Position - settled).magnitude, Is.LessThan(.05f), "not moved by any of the hits");
            Assert.That(boss.BossStunned, Is.False, "and not stunned after all of them");

            float hp = boss.Hp;
            boss.ReceiveDamage(new DamageInfo { amount = 10f, isAttack = true, ignoreShield = true, kind = HitKind.Blunt });
            Assert.That(hp - boss.Hp, Is.EqualTo(10f).Within(.01f), "a later hit deals its own amount: no stun bonus waiting");
        }
    }
}