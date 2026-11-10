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
    /// <summary>Boss stagger removed (system doc §5.3: bosses are wholly immune to knockback). Heavy
    /// knockback on the Nash Prophet or the Tyrant neither moves it, nor knocks it, nor stuns it, nor makes it take more damage; no
    /// stagger state, tuning or HUD bar is left, and no card says a boss takes stagger. Nor is a boss ever stunned: the
    /// Tyrant's charge breaks the walls it runs into instead of crashing into them (TyrantRedesignTest).</summary>
    [TestFixture]
    [Category("V3Smoke")]
    public class BossNoStaggerTest
    {
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";
        const string ArenaScene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        // Twelve 10-cell knockbacks: three times the Prophet's old 40-cell stagger limit and twice the Tyrant's old 60.
        const int Hits = 12;
        const float HeavyKnock = 10f;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        // ───── No scene ─────

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
            Assert.That(typeof(BossTuning).GetFields().Select(f => f.Name).Where(n => n.IndexOf("stun", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.Empty, "no boss stun tuning is left, the Tyrant's wall-crash stun included (2026-10-25: a boss is never stunned)");
            Assert.That(typeof(Enemy).GetProperty("BossStunned", members), Is.Null, "and no boss stun state");
        }

        [Test]
        public void CardTexts_SayBossesAreNotKnockedBack_NeverStagger()
        {
            const BindingFlags tables = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in typeof(ArenaTerms).GetFields(tables).Where(f => f.FieldType == typeof(string[])))
            foreach (var text in (string[])field.GetValue(null) ?? Array.Empty<string>())
            {
                Assert.That(text ?? "", Does.Not.Contain("失衡"), $"{field.Name}: {text}");
                Assert.That((text ?? "").IndexOf("stagger", StringComparison.OrdinalIgnoreCase), Is.LessThan(0), $"{field.Name}: {text}");
            }

            var tameZh = (string[])typeof(ArenaTerms).GetField("ExpandedTameZh", tables).GetValue(null);
            var tameEn = (string[])typeof(ArenaTerms).GetField("ExpandedTameEn", tables).GetValue(null);
            Assert.That(tameZh.Count(t => t.Contains("{tankPush}") && t.Contains("Boss 免疫击退")), Is.EqualTo(1),
                "the Stream Tame card says bosses are immune to its knockback");
            Assert.That(tameEn.Count(t => t.Contains("{tankPush}") && t.Contains("bosses are not knocked back")), Is.EqualTo(1),
                "and so does its English twin");
        }

        // ───── Nash Prophet (enemy lab trial) ─────

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task Prophet_HeavyKnockbackHits_NeitherMoveNorKnockNorStunIt()
        {
            var (game, _, hollow) = await BossRedesignTest.StartTrial();
            hollow.enabled = false; // holds still: any movement below would come from the hits
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

            // A few of the HUD's own 10 Hz refreshes, then one more right now.
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

        // ───── Tyrant (arena sandbox) ─────

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task Tyrant_HeavyKnockbackHits_NeitherMoveNorKnockNorStunIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * 12f);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");
            tyrant.enabled = false; // holds still: any movement below would come from the hits
            Physics.SyncTransforms();
            await AssertKnockbackChangesNothing(game, tyrant);
        }

        // ───── Helpers ─────

        /// <summary>Heavy player knockback hits, all pushing the boss straight away from the player: it stays where it stood, never
        /// enters the knocked state, is never stunned, and a plain hit afterwards deals exactly its amount (no stun bonus).</summary>
        static async Task AssertKnockbackChangesNothing(GameManager game, Enemy boss)
        {
            // A few steps first, so any push-out from overlapping scenery is over.
            for (int i = 0; i < 5; i++) await Awaitable.FixedUpdateAsync();
            Vector3 settled = Query.Flat(boss.Position);
            Vector3 away = Query.Flat(boss.Position - game.player.Position).normalized;
            bool knocked = false;
            for (int i = 0; i < Hits; i++)
            {
                boss.ReceiveDamage(DamageInfo.Attack(1f, game.player, away, HeavyKnock, HitKind.Slam));
                Assert.That(boss.Stunned, Is.False, $"hit {i + 1}: knockback never stuns a boss");
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
            Assert.That(boss.Stunned, Is.False, "and not stunned after all of them");

            float hp = boss.Hp;
            boss.ReceiveDamage(new DamageInfo { amount = 10f, isAttack = true, ignoreShield = true, kind = HitKind.Blunt });
            Assert.That(hp - boss.Hp, Is.EqualTo(10f).Within(.01f), "a later hit deals its own amount: no stun bonus waiting");
        }
    }
}