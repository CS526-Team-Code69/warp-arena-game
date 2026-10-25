using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture, Category("TtkBalance")]
    public class EncounterBalanceTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [TestCase(WeaponForm.Rifle, 0), TestCase(WeaponForm.Rifle, 1), TestCase(WeaponForm.Knife, 0)]
        [TestCase(WeaponForm.RocketLauncher, 0), TestCase(WeaponForm.RocketLauncher, 1)]
        [LoadScene(Scene), Timeout(40000)]
        public async Task Starter_AttackInputToFodderDeath_RecordsRealTimeline(WeaponForm form, int variant)
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var target = sandbox.targets[0];
            foreach (var other in sandbox.targets.Skip(1)) other.DiscardWithoutReward();
            target.Configure(game.waves.roster.First(e => e.kind == EnemyKind.Grunt), 1f, false);
            target.enabled = false;
            target.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            V3CombatSmokeTest.Teleport(target, game.player.Position + Vector3.forward * 6f);
            game.runWeapons.ClearWaveEffects();
            game.runWeapons.Auto = new RunWeapon[3];
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            float start = Time.time, deadline = Time.realtimeSinceStartup + 6f;
            while (target.IsAlive)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "starter must finish the target");
                Vector3 aim = Query.Flat(target.Position - game.player.Position).normalized;
                if (variant == 1 && form == WeaponForm.Rifle) game.player.DebugSetAim(-aim);
                else game.player.DebugSetAimPoint(target.Position);
                if (form == WeaponForm.Rifle && variant == 0 && weapon.charging)
                    game.runWeapons.HandleInput(false, weapon.charge < .99f, weapon.charge >= .99f);
                else game.runWeapons.HandleInput(true, true, false);
                await Awaitable.NextFrameAsync();
            }

            float seconds = Time.time - start;
            Directory.CreateDirectory("work/authoring-receipts");
            File.AppendAllText("work/authoring-receipts/ttk-runtime.csv",
                FormattableString.Invariant($"{form},{variant},{target.maxHp},{seconds}\n"));
            Assert.That(target.maxHp, Is.EqualTo(5f));

            if (form == WeaponForm.RocketLauncher && variant == 1)
                Assert.That(seconds, Is.LessThan(game.runWeapons.CycleSeconds(weapon)), "one flight kills the target");

            else Assert.That(seconds, Is.InRange(.4f, .95f), "input-to-death with one saved target six units away");
        }

        [Test, LoadScene(Scene), Timeout(40000)]
        public async Task AuthoredWaveBudgets_UseRealHpAndShield_WithoutReadingLiveBuild()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var target = sandbox.targets[0];
            var balance = game.waves.waveSet.encounterBalance;
            var waveField = typeof(WaveManager).GetField("<WaveIndex>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (int wave in new[] { 1, 6, 10, 15 })
            {
                waveField.SetValue(game.waves, wave - 1);
                foreach (var data in game.waves.roster.Where(d => d))

                foreach (bool elite in EnemyRoles.IsSpecial(data.kind) && data.canElite &&
                                       wave >= game.waves.firstEliteWave
                             ? new[] { false, true }
                             : new[] { false })
                {
                    target.Configure(data, game.waves.HpMultiplier, elite);
                    target.enabled = false;
                    float actualShield = ElementStatus.For(target).Shield;

                    float phases = data.kind == EnemyKind.Tyrant
                        ? target.maxHp * balance.tyrantPhaseShieldShare * (Enemy.TyrantPhaseCount - 1)
                        : 0f;

                    float dps = balance.DpsAt(balance.BudgetWave(data, wave));
                    if (data.kind == EnemyKind.Hollow)
                        Assert.That(target.maxHp, Is.EqualTo(Mathf.Round(balance.DpsAt(balance.prophetBudgetWave) * balance.bossSeconds
                            / (1f + EncounterBalance.InitialShieldShare(data, ElementKind.None) / balance.ShieldDirectFactor))),
                            "the Prophet's HP does not depend on the wave the trial is entered");
                    float modeled = (target.maxHp + (actualShield + phases) / balance.ShieldDirectFactor) / dps;
                    if (target.IsBoss) Assert.That(modeled, Is.InRange(80f, 100f), data.kind + " boss budget (90 s)");
                    else if (elite) Assert.That(modeled, Is.InRange(5f, 10f), data.kind + " elite budget");
                    else if (EnemyRoles.IsMelee(data.kind))
                        Assert.That(modeled, Is.InRange(.5f, .8f),
                            data.kind + " melee variants share the fodder budget");
                    else Assert.That(modeled, Is.InRange(1f, 3.5f), data.kind + " ordinary function/special budget");

                    float xp = game.waves.ExperienceFor(target);
                    if (target.IsBoss) Assert.That(xp, Is.Zero, data.kind + " boss gives no XP");
                    else if (!elite && EnemyRoles.IsMelee(data.kind))
                        Assert.That(xp, Is.EqualTo(game.waves.swarmExperienceReward), data.kind + " fixed swarm XP");
                    else
                        Assert.That(xp / balance.TargetSeconds(data, elite),
                            Is.EqualTo(game.waves.xpPerReferenceSecond * game.waves.experienceRateMultiplier
                                       * game.waves.WaveExperienceScale)
                                .Within(.0001f), data.kind + " XP per budget second");
                    float hp = target.maxHp;
                    game.progression.Stats.damageBonus += 1f;
                    target.Configure(data, game.waves.HpMultiplier, elite);
                    target.enabled = false;
                    Assert.That(target.maxHp, Is.EqualTo(hp), "a player's upgrade must not inflate enemies");
                }
            }
        }

        [Test, LoadScene(Scene), Timeout(40000)]
        public async Task TimeSlipCleanup_AndImmovableBossPull_RespectGameplayState()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            game.skills.Learn(SkillId.TimeSlip);
            Assert.That(game.skills.TryCast(0), Is.True);
            Assert.That(game.WorldTimeScale, Is.LessThan(1f));
            game.skills.ClearWaveEffects();
            Assert.That(game.WorldTimeScale, Is.EqualTo(1f), "slow time must not leak into the next wave");
            var boss = sandbox.targets[0];
            boss.Configure(game.waves.roster.First(e => e.kind == EnemyKind.Hollow), 1f, false);
            boss.enabled = false;
            Vector3 before = boss.Position;
            SkillWorldAPI.Pull(boss, before + Vector3.forward * 10f, 5f);
            await Awaitable.FixedUpdateAsync();
            Assert.That(Query.Flat(boss.Position - before).magnitude, Is.LessThan(.01f), "boss is immovable");
        }
    }
}