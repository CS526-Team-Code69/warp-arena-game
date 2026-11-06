using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnomalyArena
{
                                                                                                                       
                                                                                                                    
    [TestFixture]
    public class RunAnalyticsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        readonly List<string> sent = new();

        [SetUp]
        public void SetUp()
        {
            sent.Clear();
            RunAnalytics.Transport = sent.Add;
        }

        [TearDown]
        public void TearDown()
        {
            RunAnalytics.Transport = null;
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
        }

        [TestCase(ArenaUiState.VoidTrial, DeathCause.FellIntoGap, DamageSource.Hit, false, ExpectedResult = "void_trial")]
        [TestCase(ArenaUiState.WaveActive, DeathCause.FellIntoGap, DamageSource.Hit, true, ExpectedResult = "black_hole")]
        [TestCase(ArenaUiState.WaveActive, DeathCause.HpDepleted, DamageSource.Burn, true, ExpectedResult = "backfire")]
        [TestCase(ArenaUiState.WaveActive, DeathCause.HpDepleted, DamageSource.Wall, false, ExpectedResult = "wall_slam")]
        [TestCase(ArenaUiState.WaveActive, DeathCause.HpDepleted, DamageSource.Poison, false, ExpectedResult = "status")]
        [TestCase(ArenaUiState.FinalBoss, DeathCause.HpDepleted, DamageSource.Hit, false, ExpectedResult = "unknown")]
        public string DefeatCause_WithoutAnEnemy_FollowsTheDocumentedOrder(ArenaUiState phase, DeathCause death, DamageSource source, bool bySelf)
            => RunAnalytics.DefeatCause(phase, death, source, bySelf, null, false).cause;

        [Test]
        public void DefeatCause_EnemyHits_NameTheKindAndTheTyrantSeparately()
        {
            Assert.That(RunAnalytics.DefeatCause(ArenaUiState.WaveActive, DeathCause.HpDepleted, DamageSource.Hit, false, EnemyKind.Shade, true),
                Is.EqualTo(("enemy", "Shade", true)));
            Assert.That(RunAnalytics.DefeatCause(ArenaUiState.FinalBoss, DeathCause.HpDepleted, DamageSource.Hit, false, EnemyKind.Tyrant, false),
                Is.EqualTo(("tyrant", "Tyrant", false)));
            Assert.That(RunAnalytics.DefeatCause(ArenaUiState.WaveActive, DeathCause.HpDepleted, DamageSource.Burn, false, EnemyKind.Spitter, false),
                Is.EqualTo(("status", "Spitter", false)), "an elite's burn keeps the enemy kind but counts as damage over time");
            Assert.That(RunAnalytics.DefeatCause(ArenaUiState.VoidTrial, DeathCause.FellIntoGap, DamageSource.Hit, false, EnemyKind.Hollow, false),
                Is.EqualTo(("void_trial", "Hollow", false)), "knocked off the platform by the Hollow");
            Assert.That(RunAnalytics.DefeatCause(ArenaUiState.VoidTrial, DeathCause.FellIntoGap, DamageSource.Hit, false, EnemyKind.Grunt, true),
                Is.EqualTo(("void_trial", "", false)), "a hit from before the trial is not named");
        }

        [TestCase(ArenaUiState.WaveActive, ExpectedResult = "wave")]
        [TestCase(ArenaUiState.LevelUp, ExpectedResult = "wave")]
        [TestCase(ArenaUiState.Armory, ExpectedResult = "build")]
        [TestCase(ArenaUiState.VoidTrial, ExpectedResult = "void_trial")]
        [TestCase(ArenaUiState.FinalBoss, ExpectedResult = "final_boss")]
        public string StageOf_MapsTheRunPhase(ArenaUiState phase) => RunAnalytics.StageOf(phase);

        [TestCase("http://localhost:8765/index.html")]
        [TestCase("HTTP://LOCALHOST/")]
        [TestCase("http://127.0.0.1:8000/")]
        [TestCase("http://[::1]:8000/")]
        [TestCase("http://arena.localhost:3000/zh/")]
        [TestCase("https://arena.test/zh/")]
        [TestCase("file:///D:/Builds/WebGL-CN/index.html")]
        public void IsLocalPage_PageOpenedFromThisMachine_ReturnsTrue(string url) => Assert.That(RunAnalytics.IsLocalPage(url), Is.True);

        [TestCase("https://cs526-team-code69.github.io/warp-arena-game/")]
        [TestCase("https://cs526-team-code69.github.io/warp-arena-game/zh/")]
        [TestCase("https://localhost.example.com/")]
        [TestCase("https://notlocalhost/")]
        [TestCase("https://contest/")]
        [TestCase("https://arena.test.example.com/")]
        [TestCase("https://example.com/?from=http://localhost/#127.0.0.1")]
        [TestCase("")]
        [TestCase(null)]
        public void IsLocalPage_PublishedOrUnreadableAddress_ReturnsFalse(string url) => Assert.That(RunAnalytics.IsLocalPage(url), Is.False);

        [Test]
        public void RunEvent_Json_UsesTheSheetColumnNames()
        {
            string json = JsonUtility.ToJson(new RunAnalytics.RunEvent { @event = "run_end", cause = "enemy", run_seconds = 12.3 });
            foreach (var key in new[] { "schema", "event", "run_id", "session_id", "edition", "build", "outcome", "cause", "enemy", "elite",
                         "stage", "wave", "run_seconds", "level", "kills", "starting_weapon" })
                Assert.That(json, Does.Contain($"\"{key}\":"), key);
            Assert.That(json, Does.Contain("\"event\":\"run_end\""));
        }

        // ---------------------------------------------------------------- Beta metrics

        [TestCase(DamageSource.Hit, false, EnemyKind.Brute, false, ExpectedResult = "Brute")]
        [TestCase(DamageSource.Hit, false, EnemyKind.Brute, true, ExpectedResult = "Brute_elite")]
        [TestCase(DamageSource.Poison, false, EnemyKind.Spitter, false, ExpectedResult = "Spitter")]
        [TestCase(DamageSource.Burn, true, null, false, ExpectedResult = "backfire")]
        [TestCase(DamageSource.Hit, true, null, false, ExpectedResult = "backfire")]
        [TestCase(DamageSource.Wall, false, EnemyKind.Rammer, false, ExpectedResult = "wall_slam")]
        [TestCase(DamageSource.Wall, true, null, false, ExpectedResult = "wall_slam")]
        [TestCase(DamageSource.Hit, false, EnemyKind.Tyrant, false, ExpectedResult = "Tyrant")]
        [TestCase(DamageSource.Hit, false, null, false, ExpectedResult = "other")]
        public string DamageSourceOf_NamesTheApplierAndKeepsWallsApart(DamageSource source, bool bySelf, EnemyKind? byEnemy, bool byElite)
            => RunAnalytics.DamageSourceOf(source, bySelf, byEnemy, byElite);

        [Test]
        public void CardIds_AlwaysThreeEntries_EmptyForMissingCards()
        {
            Assert.That(RunAnalytics.CardIds(null), Is.EqualTo(new[] { "", "", "" }));
            Assert.That(RunAnalytics.CardIds(Array.Empty<DraftCard>()), Is.EqualTo(new[] { "", "", "" }));
            Assert.That(RunAnalytics.CardId(null), Is.EqualTo(""));
            var skill = new DraftCard(DraftCardKind.Skill, "Flame Wake", "", _ => true, payload: SkillId.FlameWake);
            Assert.That(RunAnalytics.CardId(skill), Is.EqualTo("Skill:FlameWake"), "the id never uses the translated title");
        }

        [Test]
        public void OfferId_MarksAHigherMkOfAnOwnedWeapon()
        {
            var owned = new RunWeapon { form = WeaponForm.Rifle };
            Assert.That(RunAnalytics.OfferId(new RunWeapon { form = WeaponForm.Minigun }), Is.EqualTo("Minigun"));
            Assert.That(RunAnalytics.OfferId(new RunWeapon { form = WeaponForm.Rifle, upgradeOf = owned }), Is.EqualTo("Rifle:upgrade"));
            Assert.That(RunAnalytics.OfferId(null), Is.EqualTo(""));
        }

        [Test]
        public void SupplyResolved_WithoutAnOpenDrop_SendsNothing()
        {
            RunAnalytics.SupplyResolved(null, RunAnalytics.SupplySkip);
            RunAnalytics.SupplyReplaceCancelled();
            RunAnalytics.FlushDamage(null);
            Assert.That(sent, Is.Empty);
        }

        [Test]
        public void BetaEvents_Json_UseTheSheetFieldNames()
        {
            string upgrade = JsonUtility.ToJson(new RunAnalytics.UpgradePickEvent { @event = "upgrade_pick", picked = "Weapon:Rifle:Power" });
            foreach (var key in new[] { "schema", "event", "run_id", "session_id", "edition", "build", "context", "wave", "level",
                         "offer_1", "offer_2", "offer_3", "picked", "rerolled" })
                Assert.That(upgrade, Does.Contain($"\"{key}\":"), "upgrade_pick " + key);

            string supply = JsonUtility.ToJson(new RunAnalytics.SupplyPickEvent { @event = "supply_pick", action = RunAnalytics.SupplyReplace });
            foreach (var key in new[] { "schema", "event", "run_id", "wave", "level", "offer_1", "offer_2", "offer_3", "action", "picked",
                         "replaced", "replace_cancels" })
                Assert.That(supply, Does.Contain($"\"{key}\":"), "supply_pick " + key);

            var row = new RunAnalytics.DamageRow { source = "Brute_elite", dot = false, stage = "wave", wave = 3, damage = 12.5, damage_pct = 25, hits = 2 };
            string damage = JsonUtility.ToJson(new RunAnalytics.DamageEvent { @event = "wave_damage", rows = new[] { row } });
            foreach (var key in new[] { "schema", "event", "run_id", "rows", "source", "dot", "stage", "wave", "damage", "damage_pct", "hits" })
                Assert.That(damage, Does.Contain($"\"{key}\":"), "wave_damage " + key);
            Assert.That(damage, Does.Contain("\"source\":\"Brute_elite\""));

            // A full chunk of damage rows must stay under the endpoint's 4096-character limit.
            var rows = new RunAnalytics.DamageRow[RunAnalytics.DamageRowsPerEvent];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = new RunAnalytics.DamageRow { source = "Splitter_elite", dot = true, stage = "void_trial", wave = 14, damage = 99999.9, damage_pct = 9999.9, hits = 9999 };
            string full = JsonUtility.ToJson(new RunAnalytics.DamageEvent { @event = "wave_damage", run_id = new string('a', 32), session_id = new string('b', 32),
                edition = "en", build = "1.0.0+3fa2b1c9", rows = rows });
            Assert.That(full.Length, Is.LessThan(4096));
        }

        [Test]
        public void EnsureV2Default_FirstRun_StartsInWhitebox()
        {
            var flag = typeof(Art).GetField("v2ChoiceInitialized", BindingFlags.NonPublic | BindingFlags.Static);
            Assume.That(flag, Is.Not.Null);
            bool savedFlag = (bool)flag.GetValue(null), savedArt = Art.Enabled;
            try
            {
                flag.SetValue(null, false);
                Art.Enabled = true;
                Art.EnsureV2Default();
                Assert.That(Art.Enabled, Is.False, "whitebox is the default art mode");
                Art.Enabled = true;
                Art.EnsureV2Default();
                Assert.That(Art.Enabled, Is.True, "a choice made with T is kept for the rest of the session");
            }
            finally
            {
                flag.SetValue(null, savedFlag);
                Art.Enabled = savedArt;
            }
        }

        [Test, LoadScene(Scene), Timeout(90000)]
        public async Task Run_KilledByAnEnemy_SendsAStartRowAndADefeatRowNamingIt()
        {
            var game = await StartRun();
            var start = Parse(RunRows().Single());
            Assert.That(start.@event, Is.EqualTo("run_start"));
            Assert.That(start.run_id, Does.Match("^[0-9a-f]{32}$"));
            Assert.That(start.session_id, Does.Match("^[0-9a-f]{32}$"));
            Assert.That(start.starting_weapon, Is.EqualTo(nameof(WeaponForm.Rifle)));
            Assert.That(start.edition, Is.EqualTo("en"));

            await Until(() => game.RunPhase == ArenaUiState.WaveActive && game.waves.Alive.Any(e => e && e.IsAlive && e.definition), 15f, "an enemy is in the arena");
            var killer = game.waves.Alive.First(e => e && e.IsAlive && e.definition);
            var kind = killer.definition.kind;
            await Until(() => !game.player.Protected, 5f, "the player's hit protection ends");
            Assert.That(game.player.ReceiveDamage(DamageInfo.Attack(99999f, killer)), Is.True, "the hit lands");
            await Until(() => game.RunPhase == ArenaUiState.Result, 5f, "the run ends");

            Assert.That(RunRows(), Has.Count.EqualTo(2));
            var end = Parse(RunRows()[1]);
            Assert.That(end.@event, Is.EqualTo("run_end"));
            Assert.That(end.run_id, Is.EqualTo(start.run_id), "both rows belong to the same run");
            Assert.That(end.session_id, Is.EqualTo(start.session_id));
            Assert.That((end.outcome, end.cause, end.enemy), Is.EqualTo(("defeat", "enemy", kind.ToString())));
            Assert.That((end.stage, end.wave), Is.EqualTo(("wave", 1)));
            Assert.That(end.run_seconds, Is.GreaterThan(0d));
            Assert.That(DamageRows().Any(r => r.source.StartsWith(kind.ToString()) && !r.dot && r.wave == 1 && r.hits >= 1 && r.damage_pct > 0),
                "the killing hit is reported as damage from that enemy kind");
            Assert.That(sent.IndexOf(RunRows()[1]), Is.EqualTo(sent.Count - 1), "the damage of the last wave is sent before run_end");
        }

        [Test, LoadScene(Scene), Timeout(90000)]
        public async Task Run_RestartedFromThePauseMenu_SendsAnAbandonedRow()
        {
            var game = await StartRun();
            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 15f, "the wave starts");
            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            await CommandUntil(game, ArenaUiCommand.Restart, () => RunRows().Count == 2);

            var end = Parse(RunRows()[1]);
            Assert.That((end.@event, end.outcome, end.cause, end.stage), Is.EqualTo(("run_end", "abandoned", "restart", "wave")));
                                                                                                                                 
                                                                                                               
            LogAssert.ignoreFailingMessages = true;
            float until = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
            Assert.That(RunRows(), Has.Count.EqualTo(2), "a run ends only once");
        }

        [Test, LoadScene(Scene), Timeout(90000)]
        public async Task Run_PlayerAndHollowDieInTheSameFrame_IsAVoidTrialDefeatWithoutAReward()
        {
            var game = await StartRun();
            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 15f, "the wave starts");
            game.EnterVoidTrial();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial));
                                                                                                                              
            Assert.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player dies");
            game.CompleteVoidTrial();
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial), "a dead player gets no reward draft");
            await Until(() => game.RunPhase == ArenaUiState.Result, 5f, "the run ends");

            var end = Parse(RunRows()[1]);
            Assert.That((end.outcome, end.cause, end.stage), Is.EqualTo(("defeat", "void_trial", "void_trial")));
            Assert.That(DamageRows().Any(r => r.source == "wall_slam" && r.stage == "void_trial"), "the wall damage is reported as wall_slam in the trial");
        }

        static RunAnalytics.RunEvent Parse(string json) => JsonUtility.FromJson<RunAnalytics.RunEvent>(json);

        // The "runs" rows only; beta events (damage, picks) are sent alongside them.
        List<string> RunRows() => sent.Where(json => Parse(json).@event.StartsWith("run_")).ToList();

        List<RunAnalytics.DamageRow> DamageRows() => sent
            .Select(json => JsonUtility.FromJson<RunAnalytics.DamageEvent>(json))
            .Where(e => e.@event == "wave_damage")
            .SelectMany(e => e.rows)
            .ToList();

                                                                                                                                             
        static async Task<GameManager> StartRun()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.State == GameState.Playing);
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
