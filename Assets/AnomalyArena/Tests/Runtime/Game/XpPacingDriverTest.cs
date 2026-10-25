using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    [Category("Pacing")]
    public class XpPacingDriverTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const float TimeScale = 3f;
        const float WallClockLimit = 1200f;
        const float HitInterval = .25f;
        const float StallSeconds = 60f;
        const float StuckSeconds = 3f;
        const float BenchSeconds = 10f;
        static string LogsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
        static string RequestPath => Path.Combine(LogsDir, "xp-pacing.request");

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (File.Exists(RequestPath)) File.Delete(RequestPath);
        }

        [TestCase(.8f), TestCase(1f), LoadScene(Scene), Category("Integration"), Timeout(1500000)]
        public async Task ReferenceDpsBot_FourteenWaves_LogsLevelReachedPerWave(float efficiency)
        {
            if (!File.Exists(RequestPath))
                Assert.Ignore("Pacing driver runs only through Anomaly Arena/V5/Run XP Pacing Driver.");
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 4f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase == ArenaUiState.WaveIntro);

            var progression = game.progression;
            var rules = progression.Rules;
            var balance = game.waves.waveSet ? game.waves.waveSet.encounterBalance : null;
            Assume.That(balance, Is.Not.Null, "saved wave set carries an encounter balance");

            int thresholds = 0, lastBanked = progression.BankedDrafts;
            progression.Changed += () =>
            {
                int delta = progression.BankedDrafts - lastBanked;
                if (delta > 0) thresholds += delta;
                lastBanked = progression.BankedDrafts;
            };

            float Collected()
            {
                float total = progression.Xp;
                for (int i = 0; i < thresholds; i++)
                    total += i < rules.levelCosts.Length ? rules.levelCosts[i] : rules.xpPerStatDraft;
                return total;
            }

            float potentialXp = 0f, fixedXp = 0f;
            int kills = 0;
            var kinds = new Dictionary<string, int>();
            var causes = new Dictionary<DeathCause, int>();
            var stalledKinds = new Dictionary<string, int>();
            int refused = 0, switched = 0;

            var benched = new Dictionary<Enemy, float>();
            Enemy current = null;
            float currentSince = 0f, currentTotal = 0f;
            game.waves.EnemyEliminated += enemy =>
            {
                if (!enemy || enemy.SuppressRewards || enemy.IsTrialSummon) return;
                float reward = game.waves.ExperienceFor(enemy);
                potentialXp += reward;

                if (enemy.Mini || enemy.definition && !enemy.Elite && EnemyRoles.IsMelee(enemy.definition.kind)) fixedXp += reward;
                kills++;
                string kind = (enemy.definition ? enemy.definition.kind.ToString() : "?") + (enemy.Elite ? "*" : "") +
                              (enemy.Mini ? "m" : "");
                kinds[kind] = kinds.TryGetValue(kind, out var seen) ? seen + 1 : 1;
                causes[enemy.DeathCause] = causes.TryGetValue(enemy.DeathCause, out var count) ? count + 1 : 1;
            };

            var rows = new List<string>();
            int completedWaves = 0, killsAtWaveStart = 0, killsAtExpiry = 0, stalled = 0;
            float waveStartedAt = -1f, expiredAt = -1f, potentialAtWaveStart = 0f, collectedAtWaveStart = 0f, fixedAtWaveStart = 0f;
            game.waves.WaveCompleted += wave =>
            {
                completedWaves = wave;
                float seconds = waveStartedAt >= 0f ? Time.time - waveStartedAt : 0f;
                float timer = expiredAt >= 0f ? expiredAt - waveStartedAt : seconds;
                float cleanup = expiredAt >= 0f ? Time.time - expiredAt : 0f;
                float got = Collected();
                int guaranteed = wave <= 8 ? wave + 1 : Mathf.Min(rules.maxLevel, 9 + (wave - 8) / 2);
                int fast = Mathf.Min(rules.maxLevel, wave + 1);
                var definition =
                    game.waves.waveSet.waves[Mathf.Clamp(wave - 1, 0, game.waves.waveSet.waves.Length - 1)];
                float modelXp = efficiency * game.waves.xpPerReferenceSecond * definition.duration;
                string kindList = string.Join(" ",
                    kinds.OrderByDescending(p => p.Value).Select(p => $"{p.Key}:{p.Value}"));
                string causeList = string.Join(" ", causes.Select(p => $"{p.Key}:{p.Value}"));
                string row =
                    $"[XpPacing] eff={efficiency:0.00} wave={wave,2} secs={seconds,5:0.0}/{definition.duration,-3:0} (timer {timer:0} + cleanup {cleanup:0})"
                    + $" kills={kills - killsAtWaveStart,3} (pre-expiry {killsAtExpiry - killsAtWaveStart}) stall={stalled}"
                    + $" potXp={potentialXp - potentialAtWaveStart,6:0.0} gotXp={got - collectedAtWaveStart,6:0.0} modelXp={modelXp,6:0.0}"
                    + $" fixXp={fixedXp - fixedAtWaveStart,6:0.0} scale={balance.DpsAt(wave) / balance.DpsAt(1) * definition.experienceScale:0.000}"
                    + $" level={progression.Level,2} xp={progression.Xp,5:0.0}/{progression.XpRequired,-3} drafts={progression.BankedDrafts}"
                    + $" guar={guaranteed,2} fast={fast,2} {(progression.Level >= guaranteed ? "ok" : "MISS")}"
                    + $" | kinds {kindList} | causes {causeList}"
                    + $" | refused {refused} switched {switched} | stalled {string.Join(" ", stalledKinds.OrderByDescending(p => p.Value).Select(p => $"{p.Key}:{p.Value}"))}";
                rows.Add(row);
                Debug.Log(row);
                waveStartedAt = -1f;
                expiredAt = -1f;
                stalled = 0;
                refused = 0;
                switched = 0;
                benched.Clear();
                stalledKinds.Clear();
                kinds.Clear();
                causes.Clear();
                potentialAtWaveStart = potentialXp;
                fixedAtWaveStart = fixedXp;
                collectedAtWaveStart = got;
                killsAtWaveStart = kills;
                killsAtExpiry = kills;
            };

            float bank = 0f, hitClock = 0f, wallStart = Time.realtimeSinceStartup;
            int ordinary = game.waves.WaveCount;
            string stop = null;
            while (completedWaves < ordinary && stop == null)
            {
                if (Time.realtimeSinceStartup - wallStart > WallClockLimit) stop = "wall clock limit";
                else if (game.State != GameState.Playing) stop = $"run ended: {game.State} {game.LoseCause}";
                if (game.CanPlayerMove && Time.timeScale > 0f) Time.timeScale = TimeScale;
                if (game.IsCombatActive && game.waves.Phase == WavePhase.WaveActive)
                {
                    if (waveStartedAt < 0f) waveStartedAt = Time.time;
                    if (expiredAt < 0f && game.waves.TimeExpired)
                    {
                        expiredAt = Time.time;
                        killsAtExpiry = kills;
                    }

                    game.player.ProtectFor(2f);
                    game.player.Heal(game.player.maxHp);
                    float dt = game.WorldDeltaTime;
                    bank += efficiency * balance.DpsAt(game.waves.WaveIndex + 1) * dt;
                    hitClock += dt;

                    if (hitClock >= HitInterval && bank >= 1f)
                    {
                        var target = PickTarget(game, benched);
                        float total = target ? target.Hp + (ElementStatus.For(target) ? ElementStatus.For(target).Shield : 0f) : 0f;
                        if (target != current || total < currentTotal - .5f)
                        {
                            current = target;
                            currentSince = Time.time;
                            currentTotal = total;
                        }
                        else if (target && Time.time - currentSince > StuckSeconds)
                        {
                            benched[target] = Time.time + BenchSeconds;
                            switched++;
                            current = null;
                            target = PickTarget(game, benched);
                        }
                        if (target && target.ReceiveDamage(DamageInfo.Attack(bank, null)))
                        {
                            bank = 0f;
                            hitClock = 0f;
                        }
                        else if (target) refused++;
                    }

                    if (expiredAt >= 0f && Time.time - expiredAt > StallSeconds)
                    {
                        foreach (var enemy in game.waves.Alive.ToArray())
                        {
                            if (!enemy || !enemy.IsAlive) continue;
                            string kind = (enemy.definition ? enemy.definition.kind.ToString() : "?") + (enemy.Elite ? "*" : "")
                                          + (enemy.Mini ? "m" : "") + (enemy.Protected ? "(protected)" : "");
                            stalledKinds[kind] = stalledKinds.TryGetValue(kind, out var n) ? n + 1 : 1;
                            enemy.DiscardWithoutReward();
                            stalled++;
                        }
                    }

                    game.orbs?.CollectAll();
                }
                else
                {
                    switch (game.RunPhase)
                    {

                        case ArenaUiState.SupplyDrop:
                            if (game.SupplyDropReady) game.ExecuteUiCommand(ArenaUiCommand.OpenSupply);
                            break;
                        case ArenaUiState.WeaponIntro: game.ExecuteUiCommand(ArenaUiCommand.DismissWeaponIntro); break;
                        case ArenaUiState.Armory: game.ExecuteUiCommand(ArenaUiCommand.SkipArmory); break;
                        case ArenaUiState.Draft:
                        case ArenaUiState.LevelUp: game.ExecuteUiCommand(ArenaUiCommand.ChooseCard, 0); break;
                        case ArenaUiState.ReplaceSkill:
                        case ArenaUiState.ReplaceWeapon: game.ExecuteUiCommand(ArenaUiCommand.Cancel); break;
                        case ArenaUiState.Ready:
                            game.ExecuteUiCommand(game.UiSnapshot.earlyStartConfirmation
                                ? ArenaUiCommand.ConfirmEarlyStart
                                : ArenaUiCommand.StartNextWave);
                            break;
                    }
                }

                await Awaitable.NextFrameAsync();
            }

            Time.timeScale = 1f;

            var sb = new StringBuilder();
            sb.AppendLine(
                $"XP pacing driver {DateTime.Now:yyyy-MM-dd HH:mm} efficiency={efficiency:0.00} timeScale={TimeScale} hitInterval={HitInterval} stallSeconds={StallSeconds} scene={Scene}");
            sb.AppendLine(
                $"rho={game.waves.xpPerReferenceSecond} swarmXp={game.waves.swarmExperienceReward} costs={string.Join(",", rules.levelCosts)}"
                + $" statDraft={rules.xpPerStatDraft} maxLevel={rules.maxLevel} referenceDps={string.Join(",", balance.referenceDps)}");
            foreach (var row in rows) sb.AppendLine(row);
            sb.AppendLine(stop == null
                ? $"[XpPacing] done: {completedWaves} waves, final level {progression.Level}, xp {progression.Xp:0.0}/{progression.XpRequired}, total kills {kills}"
                : $"[XpPacing] stopped early: {stop} after {completedWaves} waves at level {progression.Level}");
            Directory.CreateDirectory(LogsDir);
            File.WriteAllText(Path.Combine(LogsDir, $"XpPacing-eff{efficiency:0.00}.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            Assert.That(stop, Is.Null, "the driver reaches the end of the ordinary waves");
        }

        static Enemy PickTarget(GameManager game, Dictionary<Enemy, float> benched)
        {
            Enemy best = null;
            float bestScore = float.MaxValue;
            var origin = game.player.Position;
            foreach (var enemy in game.waves.Alive)
            {
                if (!enemy || !enemy.IsAlive) continue;
                float score = Query.Flat(enemy.Position - origin).magnitude;
                if (enemy.definition && enemy.definition.kind is EnemyKind.Warden or EnemyKind.Chanter) score -= 1000f;
                if (enemy.Protected || enemy.State == CharacterState.Dashing) score += 500f;
                if (benched.TryGetValue(enemy, out float until) && Time.time < until) score += 5000f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }

            return best;
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}