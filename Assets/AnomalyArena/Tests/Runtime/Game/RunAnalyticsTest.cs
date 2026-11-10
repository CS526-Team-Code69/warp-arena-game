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
    /// <summary>GDD Metric 2: the run-start and run-end rows sent to the analytics sheet, and the whitebox default the
    /// course requires. The network is replaced by RunAnalytics.Transport, so nothing leaves the machine.</summary>
    [TestFixture]
    public class RunAnalyticsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
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
                         "stage", "wave", "run_seconds", "level", "kills", "starting_weapon", "mode" })
                Assert.That(json, Does.Contain($"\"{key}\":"), key);
            Assert.That(json, Does.Contain("\"event\":\"run_end\""));
            Assert.That(json, Does.Contain("\"mode\":\"open\""), "a row is the open mode unless a campaign marks it");
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
            foreach (var key in new[] { "schema", "event", "run_id", "rows", "source", "dot", "stage", "wave", "damage", "damage_pct", "hits", "mode" })
                Assert.That(damage, Does.Contain($"\"{key}\":"), "wave_damage " + key);
            Assert.That(damage, Does.Contain("\"source\":\"Brute_elite\""));

            // A full chunk of damage rows must stay under the endpoint's 4096-character limit.
            var rows = new RunAnalytics.DamageRow[RunAnalytics.DamageRowsPerEvent];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = new RunAnalytics.DamageRow { source = "Splitter_elite", dot = true, stage = "void_trial", wave = 14, damage = 99999.9, damage_pct = 9999.9, hits = 9999 };
            string full = JsonUtility.ToJson(new RunAnalytics.DamageEvent { @event = "wave_damage", run_id = new string('a', 32), session_id = new string('b', 32),
                edition = "en", build = "1.0.0+3fa2b1c9", rows = rows, mode = RunAnalytics.ModeStory });
            Assert.That(full.Length, Is.LessThan(4096));
        }

        // ---------------------------------------------------------------- V2 day campaign

        [Test]
        public void V2Events_Json_UseTheSheetFieldNames()
        {
            string day = JsonUtility.ToJson(new RunAnalytics.V2DayEvent { @event = "v2_day", day = 2 });
            foreach (var key in new[] { "schema", "event", "run_id", "session_id", "edition", "build", "day", "overtime", "reached_dawn", "end_stage", "end_cause", "kills",
                         "player_damage", "player_damage_pct", "player_hits", "base_damage", "base_damage_hp", "base_hp", "base_shield", "base_max_hp",
                         "earned_alloy", "earned_energy", "earned_dark", "spent_alloy", "spent_energy", "spent_dark", "alloy", "energy", "dark",
                         "night_death", "lost_alloy", "lost_energy", "lost_dark", "shadows", "overrun_regions", "repairs" })
                Assert.That(day, Does.Contain($"\"{key}\":"), "v2_day " + key);
            string purchase = JsonUtility.ToJson(new RunAnalytics.V2PurchaseEvent { @event = "v2_purchase", day = 2 });
            foreach (var key in new[] { "schema", "event", "run_id", "session_id", "edition", "build", "day", "stage", "item", "detail", "room",
                         "alloy", "energy", "dark" })
                Assert.That(purchase, Does.Contain($"\"{key}\":"), "v2_purchase " + key);
            string milestone = JsonUtility.ToJson(new RunAnalytics.V2Event { @event = "v2_event", day = 2, kind = CampaignEvents.MilestoneCrate });
            foreach (var key in new[] { "schema", "event", "run_id", "session_id", "edition", "build", "day", "stage", "kind", "detail", "value" })
                Assert.That(milestone, Does.Contain($"\"{key}\":"), "v2_event " + key);
            var full = new RunAnalytics.V2DayEvent { @event = "v2_day", run_id = new string('a', 32), session_id = new string('b', 32), edition = "zh",
                build = "1.0.0+3fa2b1c9", day = 99, kills = 99999, player_damage = 9999999.9, player_damage_pct = 999999.9, player_hits = 99999,
                base_damage = 9999999.9, base_damage_hp = 9999999.9, base_hp = 99999.9, base_shield = 99999.9, base_max_hp = 99999.9 };
            Assert.That(JsonUtility.ToJson(full).Length, Is.LessThan(4096));
        }

        [TestCase("tutorial_step", "Crate", 0, ExpectedResult = "tutorial_step:Crate")]
        [TestCase("prophet", "", 4, ExpectedResult = "prophet")]
        [TestCase("void_crystal", "", 0, ExpectedResult = "void_crystal")]
        [TestCase("hole_check", "start", 3, ExpectedResult = "hole_check:3:start")]
        [TestCase("hole_check", "passed", 3, ExpectedResult = "hole_check:3:passed")]
        [TestCase("hole_check", "failed_timeout", 3, ExpectedResult = "hole_check:3:failed")]
        [TestCase("hole_check", "failed_night", 3, ExpectedResult = "hole_check:3:failed")]
        [TestCase("crate", "Afternoon:Railgun", 2, ExpectedResult = null)]
        [TestCase("void_purchase", "armor", 5, ExpectedResult = null)]
        public string MilestoneOnce_RepeatingKinds_ShareOneKeyPerRunOrPerDay(string kind, string detail, int day)
            => RunAnalytics.MilestoneOnce(kind, detail, day);

        [Test]
        public void EnsureV2Default_FirstRun_StartsInWhitebox()
        {
            var flag = typeof(Art).GetField("v2ChoiceInitialized", BindingFlags.NonPublic | BindingFlags.Static);
            Assume.That(flag, Is.Not.Null);
            bool savedFlag = (bool)flag.GetValue(null), savedArt = Art.Enabled;
            try
            {
                flag.SetValue(null, false);
                Art.DefaultForTests = null; // the game's own default, not the test run's (TestStartLanguage)
                Art.Enabled = true;
                Art.EnsureV2Default();
                Assert.That(Art.Enabled, Is.False, "whitebox is the default art mode");
                Art.Enabled = true;
                Art.EnsureV2Default();
                Assert.That(Art.Enabled, Is.True, "a choice made with T is kept for the rest of the session");
            }
            finally
            {
                Art.DefaultForTests = TestStartLanguage.RunArt;
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
            Assert.That(start.edition, Is.EqualTo(ArenaText.IsChinese ? "zh" : "en"));

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
            Assert.That((start.mode, end.mode), Is.EqualTo(("open", "open")), "a classic run is the open mode");
            Assert.That(DamageEvents().Select(e => e.mode), Has.All.EqualTo("open"), "its damage rows too");
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
            // Restart reloads the scene by name, which fails with a logged error when the test project's build profile lacks it;
            // only the row matters here, so let the reload attempt finish inside this test and ignore its log.
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
            // Wall damage is not an attack, so hit protection does not block it; the Hollow then falls before the frame ends.
            Assert.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player dies");
            game.CompleteVoidTrial();
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial), "a dead player gets no reward draft");
            await Until(() => game.RunPhase == ArenaUiState.Result, 5f, "the run ends");

            var end = Parse(RunRows()[1]);
            Assert.That((end.outcome, end.cause, end.stage), Is.EqualTo(("defeat", "void_trial", "void_trial")));
            Assert.That(DamageRows().Any(r => r.source == "wall_slam" && r.stage == "void_trial"), "the wall damage is reported as wall_slam in the trial");
        }

        /// <summary>CampaignEvents raised by hand on a classic run (V2Campaign raises them through the same internal methods): the
        /// rows then carry the campaign day and its stage, a hit without a dealer at night is the shadows', each dawn sends the day's
        /// v2_day row, a purchase sends v2_purchase, card picks and air drops send nothing, and a fallen base is the run's cause. Needs
        /// the GameManagerV2.cs and Combatant.cs parts of new_data (the player's damage reaches RunAnalytics through DamageTaken).</summary>
        [Test, LoadScene(Scene), Timeout(90000)]
        public async Task CampaignEvents_V2RowsCarryTheDay_AndAFallenBaseIsTheCause()
        {
            var game = await StartRun();
            Assume.That(game.TryGetComponent(out V2Campaign campaign), Is.True, "the scene has the V2 campaign");
            ListenToCampaign();
            await Until(() => game.RunPhase == ArenaUiState.WaveActive && game.waves.Alive.Any(e => e && e.IsAlive && e.definition), 15f, "an enemy is in the arena");
            var killer = game.waves.Alive.First(e => e && e.IsAlive && e.definition);
            RaiseCampaign("RaiseDayStarted", 3, false);
            int campaignFrom = sent.Count;
            RaiseCampaign("RaisePhase", "night", 3);
            await Until(() => !game.player.Protected, 5f, "the player's hit protection ends");
            Assert.That(game.player.ReceiveDamage(new DamageInfo { amount = 5f, source = DamageSource.Hit }), Is.True, "a shadow's touch lands");
            RaiseCampaign("RaiseNightDeath", new CampaignCost(1, 2, 3), 3);
            RaiseCampaign("RaisePhase", "dawn", 3);
            RaiseCampaign("RaiseDawn", 3, new CampaignCost(10, 20, 30), new CampaignCost(100, 200, 300));
            Assert.That(DamageRows().Any(r => r.source == "shadow" && r.stage == "night" && r.wave == 3 && r.hits >= 1),
                "the night's damage is the shadows' and leaves at dawn");
            var day3 = Days().Single();
            Assert.That((day3.day, day3.reached_dawn, day3.night_death, day3.lost_dark), Is.EqualTo((3, true, true, 3)));
            Assert.That((day3.end_stage, day3.end_cause), Is.EqualTo(("", "")), "a day that reached dawn has no end");
            Assert.That((day3.alloy, day3.earned_alloy, day3.earned_dark), Is.EqualTo((10, 100, 300)));
            Assert.That(day3.player_hits, Is.GreaterThanOrEqualTo(1), "the shadow's touch counts in the day");

            // V2Campaign raises Spent when it takes the money and Purchased once the purchase is given: one row, and the price counts once.
            // A payment given back (Spent without Purchased) counts nowhere.
            RaiseCampaign("RaiseSpent", new CampaignCost(7));
            RaiseCampaign("RaiseSpent", new CampaignCost(5));
            RaiseCampaign("RaisePurchased", "repair", "Armament", CampaignRoom.Armament, new CampaignCost(5), 3);
            var purchases = sent.Where(json => Parse(json).@event == "v2_purchase").Select(json => JsonUtility.FromJson<RunAnalytics.V2PurchaseEvent>(json)).ToList();
            Assert.That(purchases, Has.Count.EqualTo(1), "one purchase sends one row");
            var purchase = purchases[0];
            Assert.That((purchase.day, purchase.stage, purchase.alloy, purchase.item, purchase.detail, purchase.room),
                Is.EqualTo((4, "dawn", 5, "repair", "Armament", "Armament")), "a dawn purchase counts in the coming day and says what was bought");
            RaiseCampaign("RaiseDayStarted", 4, false);
            RaiseCampaign("RaiseBaseDamaged", 50f, 20f, CampaignRoom.Armament);
            RunAnalytics.UpgradePicked(game, true, new[] { "Stat:MaxHp", "", "" }, 0, false);
            RunAnalytics.SupplyOpened(new[] { new RunWeapon { form = WeaponForm.Rifle }, null, null });
            RunAnalytics.SupplyResolved(game, RunAnalytics.SupplySkip);

            var destroyed = typeof(V2Campaign).GetProperty(nameof(V2Campaign.BaseDestroyed));
            try
            {
                destroyed.SetValue(campaign, true);
                await Until(() => !game.player.Protected, 5f, "the player's hit protection ends");
                Assert.That(game.player.ReceiveDamage(DamageInfo.Attack(99999f, killer)), Is.True, "the hit lands");
                await Until(() => game.RunPhase == ArenaUiState.Result, 5f, "the run ends");
            }
            finally
            {
                destroyed.SetValue(campaign, false);
            }

            var end = Parse(RunRows()[1]);
            Assert.That((end.outcome, end.cause, end.enemy, end.stage, end.wave), Is.EqualTo(("defeat", "base_destroyed", "", "day", 4)),
                "a fallen base is the cause, not the last hit on the player");
            Assert.That((Parse(RunRows()[0]).mode, end.mode), Is.EqualTo(("open", "story")),
                "a run marked as a campaign at its first campaign event sent its start as open; its end is story");
            Assert.That(sent.Skip(campaignFrom).Select(json => JsonUtility.FromJson<RunAnalytics.DamageEvent>(json))
                .Where(e => e.@event == "wave_damage").Select(e => e.mode), Has.All.EqualTo("story"), "damage sent once it is a campaign run");
            var day4 = Days().Last();
            Assert.That((day4.day, day4.reached_dawn, day4.spent_alloy, day4.repairs, day4.base_damage, day4.base_damage_hp), Is.EqualTo((4, false, 5, 1, 50d, 20d)));
            Assert.That((day4.end_stage, day4.end_cause), Is.EqualTo(("day", "base_destroyed")), "the last day says where and how the run ended");
            Assert.That(sent.IndexOf(RunRows()[1]), Is.EqualTo(sent.Count - 1), "run_end is the last row");
            var events = sent.Skip(campaignFrom).Select(json => Parse(json).@event).Distinct().ToList();
            Assert.That(events, Is.SubsetOf(new[] { "run_end", "wave_damage", "v2_day", "v2_purchase" }), "a campaign run sends no card picks and no air drops");
            Assert.That(RunAnalytics.StageOf(ArenaUiState.WaveActive), Is.EqualTo("wave"), "the campaign marks end with the run");
        }

        /// <summary>CampaignEvents.Milestone raised by hand on a classic run, as above: each sends one v2_event row with the day being
        /// counted and the stage; a second result of the black hole's check on the same day, a second press at the void crystals and an
        /// empty kind send nothing, the next day's check is sent again, and the run's last day carries how the run ended.</summary>
        [Test, LoadScene(Scene), Timeout(90000)]
        public async Task CampaignMilestones_SendV2EventRows_RepeatOnlyWhereAllowed()
        {
            var game = await StartRun();
            Assume.That(game.TryGetComponent(out V2Campaign _), Is.True, "the scene has the V2 campaign");
            ListenToCampaign();
            RaiseCampaign("RaiseDayStarted", 2, false);
            RaiseCampaign("RaiseMilestone", "hole_check", "start", .25f, 2);
            RaiseCampaign("RaiseMilestone", "hole_check", "start", .5f, 2);
            RaiseCampaign("RaiseMilestone", "hole_check", "failed_timeout", .75f, 2);
            RaiseCampaign("RaiseMilestone", "hole_check", "failed_night", .75f, 2);
            RaiseCampaign("RaiseMilestone", "void_crystal", "", 0f, 2);
            RaiseCampaign("RaiseMilestone", "void_crystal", "", 0f, 2);
            RaiseCampaign("RaiseMilestone", "", "nothing", 1f, 2);
            RaiseCampaign("RaisePhase", "dusk", 2);
            RaiseCampaign("RaiseMilestone", "crate", "Afternoon:Railgun", 2f, 2);
            RaiseCampaign("RaiseMilestone", "crate", "Afternoon:left", 0f, 2);
            RaiseCampaign("RaiseDayStarted", 3, false);
            RaiseCampaign("RaiseMilestone", "hole_check", "start", .5f, 3);
            RunAnalytics.RunEnded(game, ArenaUiState.WaveActive, "quit_to_title");

            var rows = Milestones();
            Assert.That(rows.Select(r => (r.day, r.stage, r.kind, r.detail, r.value)), Is.EqualTo(new[]
            {
                (2, "day", "hole_check", "start", .25),
                (2, "day", "hole_check", "failed_timeout", .75),
                (2, "day", "void_crystal", "", 0d),
                (2, "dusk", "crate", "Afternoon:Railgun", 2d),
                (2, "dusk", "crate", "Afternoon:left", 0d),
                (3, "day", "hole_check", "start", .5),
            }), "one row each, in order");
            Assert.That(rows.Select(r => r.run_id), Has.All.EqualTo(Parse(RunRows()[0]).run_id), "the rows join the run");
            var last = Days().Last();
            Assert.That((last.day, last.reached_dawn, last.end_stage, last.end_cause), Is.EqualTo((3, false, "day", "quit_to_title")),
                "the last day carries how the run ended");
        }

        List<RunAnalytics.V2DayEvent> Days() => sent
            .Where(json => Parse(json).@event == "v2_day")
            .Select(json => JsonUtility.FromJson<RunAnalytics.V2DayEvent>(json))
            .ToList();

        List<RunAnalytics.V2Event> Milestones() => sent
            .Where(json => Parse(json).@event == "v2_event")
            .Select(json => JsonUtility.FromJson<RunAnalytics.V2Event>(json))
            .ToList();

        /// <summary>The title's campaign Start: the run_start row is day 0 of the tutorial.</summary>
        [Test, LoadScene(Scene), Timeout(90000)]
        public async Task CampaignRun_StartRow_IsDayZeroOfTheTutorial()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            await CommandUntil(game, ArenaUiCommand.StartCampaign, () => game.CampaignRunning);
            // What comes before the first weapon (the day 0 title card, the first run's black-hole cutscene, tutorial pages) belongs to
            // the V2 campaign and changes with it. The row checked here is written by RunStarted, so the test calls it directly, as
            // GameManager.CampaignEnterPlay does once the starting weapon's card is closed.
            RunAnalytics.RunStarted(game);
            Assert.That(RunRows(), Is.Not.Empty, "a run-start row was sent");
            var start = Parse(RunRows()[0]);
            Assert.That((start.@event, start.stage, start.wave, start.mode), Is.EqualTo(("run_start", "tutorial", 0, "story")));
        }

        /// <summary>A campaign mark left by a run that never sent run_end (the same GameManager plays both modes) does not carry over: the
        /// next run that is not a campaign starts as the open mode.</summary>
        [Test, LoadScene(Scene), Timeout(90000)]
        public async Task RunStarted_AfterACampaignRunThatNeverEnded_StartsInTheOpenMode()
        {
            var game = await StartRun();
            Assume.That(game.TryGetComponent(out V2Campaign _), Is.True, "the scene has the V2 campaign");
            ListenToCampaign();
            RaiseCampaign("RaiseDayStarted", 2, false);
            Assume.That(RunAnalytics.StageOf(ArenaUiState.WaveActive), Is.EqualTo("day"), "the run is marked as a campaign run");

            RunAnalytics.RunStarted(game);

            var start = Parse(RunRows().Last());
            Assert.That((start.@event, start.mode, start.stage, start.wave), Is.EqualTo(("run_start", "open", "wave", 1)));
        }

        // V2Campaign raises CampaignEvents through internal methods; the tests reach them, and RunAnalytics' listener, by reflection.
        static void RaiseCampaign(string raise, params object[] args)
        {
            var method = typeof(CampaignEvents).GetMethod(raise, BindingFlags.NonPublic | BindingFlags.Static);
            Assume.That(method, Is.Not.Null, "CampaignEvents." + raise);
            method.Invoke(null, args);
        }

        static void ListenToCampaign()
        {
            var listen = typeof(RunAnalytics).GetMethod("ListenToCampaign", BindingFlags.NonPublic | BindingFlags.Static);
            Assume.That(listen, Is.Not.Null, "RunAnalytics.ListenToCampaign");
            listen.Invoke(null, null);
        }

        static RunAnalytics.RunEvent Parse(string json) => JsonUtility.FromJson<RunAnalytics.RunEvent>(json);

        // The "runs" rows only; beta events (damage, picks) are sent alongside them.
        List<string> RunRows() => sent.Where(json => Parse(json).@event.StartsWith("run_")).ToList();

        List<RunAnalytics.DamageRow> DamageRows() => DamageEvents().SelectMany(e => e.rows).ToList();

        List<RunAnalytics.DamageEvent> DamageEvents() => sent
            .Select(json => JsonUtility.FromJson<RunAnalytics.DamageEvent>(json))
            .Where(e => e.@event == "wave_damage")
            .ToList();

        /// <summary>Title → supply crate → Rifle → close its card: the first wave starts, which is when the run-start row is sent.</summary>
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
