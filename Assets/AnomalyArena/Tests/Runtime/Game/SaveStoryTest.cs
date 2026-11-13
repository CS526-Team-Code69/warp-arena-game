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
    /// <summary>The story mode's saves in the scene. A run the title bound to a slot saves at the start of each phase from day 1 on: day 1's
    /// morning as Day 0's night ends, its afternoon as the morning's wave is over; the summary shows the day, the phase, the ship and the
    /// weapons. A new game in a filled slot deletes it first and then binds it. A won run's slot is marked cleared, and Continue does
    /// nothing for it; a lost run's slot keeps its last save. Continuing an afternoon save brings the run back as it was when that
    /// afternoon began: the day, the phase, the currencies, the base, the ship, the player's level, weapons and skills, on the same map.
    /// Day 0's walk is skipped with the campaign's own calls for checks (its night begun and ended at once), and the mornings last a few
    /// seconds (a copy of the rules); the saves are kept in memory.</summary>
    [TestFixture]
    public class SaveStoryTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const int Slot = 1;
        CampaignRules rulesCopy;

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaSaves.InMemoryForTests, Is.True, "the saves are kept in memory during the tests");
            ArenaSaves.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ArenaSaves.ResetForTests();
            WorldMap.NextMapIndex = -1;
            if (rulesCopy) Object.Destroy(rulesCopy);
            rulesCopy = null;
            Time.timeScale = 1f;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(150000)]
        public async Task NewGameInAFilledSlot_SavesDay1sMorningAndAfternoon_AndItsWinMarksTheSlotCleared()
        {
            // An old run's cleared save fills the slot; the title's new game deletes it, then binds the slot.
            var old = new SaveData();
            old.story.day = 6;
            old.summary.day = 6;
            old.summary.phase = SavePhase.Night;
            old.summary.weapons = new[] { WeaponForm.Railgun };
            Assume.That(ArenaSaves.Store(SaveMode.Story, Slot, old) && ArenaSaves.MarkCleared(SaveMode.Story, Slot), Is.True, "an old, cleared save fills the slot");
            ArenaSaves.Delete(SaveMode.Story, Slot);
            Assert.That(ArenaSaves.HasEntryForTests(SaveMode.Story, Slot), Is.False, "deleting the slot leaves nothing of it");
            ArenaSaves.Bind(SaveMode.Story, Slot);

            var game = await ReadyScene();
            var campaign = await StartStory(game, true);
            Assert.That(ArenaSaves.BoundSlot(SaveMode.Story), Is.EqualTo(-1), "the run took the binding as it began");
            Assert.That(ArenaSaves.List(SaveMode.Story)[Slot].empty, Is.True, "Day 0 writes no save");

            BeginDay1(game, campaign);
            var morning = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((morning.empty, morning.cleared, morning.day, morning.phase), Is.EqualTo((false, false, 1, SavePhase.Morning)), "day 1's morning is saved as it begins");
            Assert.That(morning.weapons, Is.EqualTo(new[] { WeaponForm.Pistol }), "with the pistol and nothing of the old run");
            Assert.That((morning.engineFixed, morning.armorFixed, morning.fuel, morning.fuelMax), Is.EqualTo((false, false, 0, campaign.FuelUnits)), "the ship as it is");
            Assert.That(morning.mapFamily, Is.EqualTo(ArenaSaves.MapFamily(WorldMap.Active.biome)), "the map laid");
            Assert.That(ArenaSaves.Load(SaveMode.Story, Slot).story.day, Is.Zero, "taken as Day 0's night ends, before the day is counted up");

            campaign.DebugGrant(5000, 5000, 5000);
            Assume.That(campaign.TryUpgradeRoom(CampaignRoom.Ship) && campaign.TryRepairEngine(), Is.True, "the Ship Launch Bay is built and the engine repaired");
            game.runWeapons.GiveToAuto(0, WeaponForm.OrbitBlades);
            await FinishTheMorning(game, campaign);

            var afternoon = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((afternoon.empty, afternoon.cleared, afternoon.day, afternoon.phase), Is.EqualTo((false, false, 1, SavePhase.Afternoon)), "day 1's afternoon is saved as it begins");
            Assert.That((afternoon.engineFixed, afternoon.armorFixed), Is.EqualTo((true, false)), "with the engine repaired that morning");
            Assert.That(afternoon.weapons, Is.EqualTo(new[] { WeaponForm.Pistol, WeaponForm.OrbitBlades }), "and the weapons held: hands first, then the auto slots");
            Assert.That(ArenaSaves.List(SaveMode.Story).Where(s => s.slot != Slot).Select(s => s.empty), Is.All.True, "the other story slots stay empty");
            Assert.That(ArenaSaves.List(SaveMode.Open).Select(s => s.empty), Is.All.True, "and so do the open mode's");

            // The run is won (the end of the ending, called directly): the slot keeps its save, marked cleared.
            var win = typeof(GameManager).GetMethod("CampaignWin", BindingFlags.Instance | BindingFlags.NonPublic);
            Assume.That(win, Is.Not.Null, "GameManager still has CampaignWin");
            win.Invoke(game, null);
            await Until(() => game.State == GameState.Won, 5f, "the run is won");
            var won = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((won.empty, won.cleared, won.day, won.engineFixed), Is.EqualTo((false, true, 1, true)), "the won run's slot is kept, cleared, showing how it finished");
            Assert.That(ArenaSaves.CanContinue(SaveMode.Story, Slot), Is.False, "a cleared slot cannot be continued");

            var title = await QuitToTitle(game);
            ArenaSaves.Continue(SaveMode.Story, Slot);
            await Seconds(1.2f);
            Assert.That(GameManager.Instance, Is.SameAs(title), "Continue on a cleared slot loads nothing");
            Assert.That((title.RunPhase, title.CampaignRunning), Is.EqualTo((ArenaUiState.Title, false)), "the title stays");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(150000)]
        public async Task ContinuingAnAfternoonSave_BringsTheRunBackAsThatAfternoonBegan()
        {
            ArenaSaves.Bind(SaveMode.Story, Slot);
            var game = await ReadyScene();
            var campaign = await StartStory(game, true);
            BeginDay1(game, campaign);

            // A run worth bringing back: currencies, two rooms, the engine, an armor level, two more weapons, a skill, a higher Mk.
            campaign.DebugGrant(5000, 5000, 5000);
            Assume.That(campaign.TryUpgradeRoom(CampaignRoom.Ship) && campaign.TryRepairEngine(), Is.True, "the Ship Launch Bay is built and the engine repaired");
            Assume.That(campaign.TryUpgradeRoom(CampaignRoom.Armament) && campaign.TryBuyPlayerLevel(), Is.True, "the Armament Bay is built and an armor level bought");
            game.runWeapons.GiveToHand(1, WeaponForm.Rifle);
            game.runWeapons.GiveToAuto(0, WeaponForm.OrbitBlades);
            Assume.That(game.skills.Learn(SkillId.Blink, -1, 2), Is.True, "Blink is learned at level 2");
            campaign.SetTeamMk(3);
            await FinishTheMorning(game, campaign);
            campaign.DebugCollectPickups();
            await Until(() => campaign.PickupCount == 0, 5f, "the day's last energy is picked up");
            var before = StoryState.Of(game, campaign);
            var saved = ArenaSaves.List(SaveMode.Story)[Slot];
            Assume.That((saved.empty, saved.day, saved.phase), Is.EqualTo((false, 1, SavePhase.Afternoon)), "the afternoon's save is in the slot");

            var title = await QuitToTitle(game);
            Assert.That(ArenaSaves.CanContinue(SaveMode.Story, Slot), Is.True, "leaving the run keeps its save");
            ArenaSaves.Continue(SaveMode.Story, Slot);
            var next = await NextScene(title, 25f);
            var resumed = next.GetComponent<V2Campaign>();
            await Until(() => resumed.Running && resumed.Stage == CampaignStage.Dusk && next.State == GameState.Playing, 15f, "the continued run is in its afternoon");
            var after = StoryState.Of(next, resumed);

            Assert.That((after.day, after.stage), Is.EqualTo((before.day, before.stage)), "the day and the phase");
            Assert.That(after.currencies, Is.EqualTo(before.currencies), "the currencies and what was earned");
            Assert.That(after.rooms, Is.EqualTo(before.rooms), "the rooms");
            Assert.That(after.baseState, Is.EqualTo(before.baseState), "the base's level, shield and HP");
            Assert.That(after.ship, Is.EqualTo(before.ship), "the ship");
            Assert.That((after.lamps, after.teamMk, after.level, after.maxHp), Is.EqualTo((before.lamps, before.teamMk, before.level, before.maxHp)), "the lamps, the Mk and the armor level");
            Assert.That(after.hp, Is.EqualTo(before.hp).Within(.5f), "the player's HP");
            Assert.That(after.weapons, Is.EqualTo(before.weapons), "the weapons in their slots");
            Assert.That(after.skills, Is.EqualTo(before.skills), "the skills and their levels");
            Assert.That((after.seed, after.map), Is.EqualTo((before.seed, before.map)), "the same map and terrain");
            Assert.That(Vector3.Distance(after.hole, before.hole), Is.LessThan(.01f), "the black hole where it stood");
            var still = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((still.day, still.phase, still.cleared), Is.EqualTo((1, SavePhase.Afternoon, false)), "the slot still holds the afternoon's save");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task ALostRun_KeepsItsSlotAtTheLastSave()
        {
            ArenaSaves.Bind(SaveMode.Story, Slot);
            var game = await ReadyScene();
            var campaign = await StartStory(game, false);
            BeginDay1(game, campaign);
            var saved = ArenaSaves.List(SaveMode.Story)[Slot];
            Assume.That((saved.empty, saved.day, saved.phase), Is.EqualTo((false, 1, SavePhase.Morning)), "day 1's morning is saved");

            // Wall damage is no attack, so the protection does not hold it back: the player falls in the morning and the run is lost.
            Assume.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player falls");
            await Until(() => game.State == GameState.Lost, 5f, "the run is lost");

            var kept = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((kept.empty, kept.cleared, kept.day, kept.phase, kept.savedAtUnixSeconds), Is.EqualTo((false, false, 1, SavePhase.Morning, saved.savedAtUnixSeconds)),
                "the lost run's slot keeps its last save as it was");
            Assert.That(ArenaSaves.CanContinue(SaveMode.Story, Slot), Is.True, "and it can be continued from there");
        }

        // ───── Helpers ─────

        /// <summary>What a continue must bring back, read from a running campaign.</summary>
        sealed class StoryState
        {
            public int day, lamps, teamMk, level, seed, map;
            public CampaignStage stage;
            public float hp, maxHp;
            public int[] currencies, rooms, ship;
            public float[] baseState;
            public string[] weapons, skills;
            public Vector3 hole;

            public static StoryState Of(GameManager game, V2Campaign campaign) => new()
            {
                day = campaign.Day,
                stage = campaign.Stage,
                currencies = new[] { campaign.Alloy, campaign.Energy, campaign.Dark, campaign.EarnedAlloy, campaign.EarnedEnergy, campaign.EarnedDark },
                rooms = Enum.GetValues(typeof(CampaignRoom)).Cast<CampaignRoom>().SelectMany(r => new[] { campaign.Room(r).level, campaign.Room(r).damaged ? 1 : 0 }).ToArray(),
                baseState = new[] { campaign.Durability, campaign.ShieldLevel, Mathf.Round(campaign.BaseHp * 10f), Mathf.Round(campaign.BaseShield * 10f) },
                ship = new[] { campaign.EngineFixed ? 1 : 0, campaign.ArmorFixed ? 1 : 0, campaign.Fuel },
                lamps = campaign.LampStock,
                teamMk = campaign.TeamMk,
                level = game.progression.Level,
                maxHp = game.player.maxHp,
                hp = game.player.Hp,
                weapons = game.runWeapons.Hand.Concat(game.runWeapons.Auto).Select(w => w == null ? "-" : $"{w.form} {w.variant} Mk{w.mk} {w.revealed}")
                    .Append($"active {game.runWeapons.ActiveIndex}").ToArray(),
                skills = game.skills.Slots.Select(s => s.Empty ? "-" : $"{s.Id} {s.Level}").ToArray(),
                seed = campaign.MapSeed,
                map = WorldMap.Active.BiomeIndex,
                hole = WorldMap.Active.CampaignHole ? WorldMap.Active.CampaignHole.Centre : Vector3.zero,
            };
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>The story mode from the title, up to Day 0's walk. shortMornings: the mornings last 3 seconds (a copy of the rules).</summary>
        async Task<V2Campaign> StartStory(GameManager game, bool shortMornings)
        {
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            if (shortMornings)
            {
                rulesCopy = Object.Instantiate(campaign.rules);
                rulesCopy.morningSeconds = 3f;
                campaign.rules = rulesCopy;
            }
            await CommandUntil(game, ArenaUiCommand.StartCampaign, () => game.CampaignRunning);
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
            return campaign;
        }

        /// <summary>Day 0's night begun and ended at once (the campaign's calls for checks): its dawn writes day 1's morning save and
        /// starts the morning. The player is protected from the morning's monsters.</summary>
        static void BeginDay1(GameManager game, V2Campaign campaign)
        {
            campaign.DebugBeginNight(0, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Night), "Day 0's night has begun");
            campaign.EndNight(false);
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Day)), "the dawn began day 1's morning");
            game.player.ProtectFor(600f);
        }

        /// <summary>The morning's wave runs out and what is left of it is taken away without rewards, until the afternoon begins.</summary>
        static async Task FinishTheMorning(GameManager game, V2Campaign campaign)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (campaign.Stage != CampaignStage.Dusk)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The morning never ended: {campaign.Stage}, {game.RunPhase}, {game.waves.Remaining} enemies left.");
                if (game.RunPhase == ArenaUiState.Cutscene && WorldMap.Active) WorldMap.Active.SkipHoleIntro();
                if (game.waves.TimeExpired) KillAll(game);
                await Awaitable.NextFrameAsync();
            }
        }

        static void KillAll(GameManager game)
        {
            foreach (var enemy in game.waves.Alive.ToArray())
            {
                if (!enemy || !enemy.IsAlive) continue;
                enemy.SuppressRewards = true;
                enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
            }
        }

        /// <summary>Quit to title (the scene loads again) and wait for its title.</summary>
        static async Task<GameManager> QuitToTitle(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.QuitToTitle, () => !game, 8f);
            var title = await NextScene(game);
            await Until(() => title.RunPhase == ArenaUiState.Title, 5f, "the title");
            return title;
        }

        /// <summary>The GameManager of the scene loaded after old's, once it has started.</summary>
        static async Task<GameManager> NextScene(GameManager old, float timeout = 20f)
        {
            await Until(() => GameManager.Instance && !ReferenceEquals(GameManager.Instance, old) && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active,
                timeout, "the scene loaded again");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command);
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
