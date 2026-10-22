using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    [Category("V3Smoke")]
    public class V3SmokeTest
    {
        const string SceneCn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const string SceneEn = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        WaveSet originalWaves, runtimeWaves;
        UnityEngine.Random.State savedRandom;

        [SetUp]
        public void SetUp()
        {
            savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(7319);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Random.state = savedRandom;
            if (runtimeWaves)
            {
                if (GameManager.Instance && GameManager.Instance.waves)
                    GameManager.Instance.waves.waveSet = originalWaves;
                Object.Destroy(runtimeWaves);
            }
            originalWaves = runtimeWaves = null;
            Time.timeScale = 1f;
        }

        [Test, LoadScene(SceneCn), FocusGameView, Category("Integration"), Category("Acceptance"), Timeout(60000)]
        public async Task ChineseRun_ClickedCrateAndClearedExpiredWave_OpensNextDistinctArmory()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 6f);
            var spawned = new List<Enemy>();
            game.waves.EnemySpawned += spawned.Add;

            await OpenLoadout(game);
            Assert.That(game.player.Hp, Is.EqualTo(25f), "initial HP");
            Assert.That(game.UiSnapshot.selectionUnlimited, Is.True, "choice has no countdown");
            await ClickUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            Assert.That(Time.timeScale, Is.Zero, "the weapon card pauses the game");
            Assert.That(game.runWeapons.Hand[0].revealed, Is.True, "the weapon is revealed the moment it is taken");
            Assert.That(game.UiSnapshot.weaponIntroBody, Does.Contain(WeaponGuide.Backfire(game.runWeapons.Hand[0].form, game.runWeapons.Hand[0].variant)), "the card states the backfire");
            await ClickUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase == ArenaUiState.WaveIntro);
            game.player.ProtectFor(120f);
            await Until(() => game.waves.LivingCount >= 8, 10f, "first eight saved enemies spawn");
            var balance = game.waves.waveSet.encounterBalance;
            float fodderHp = Mathf.Max(1f, Mathf.Round(balance.DpsAt(1) * balance.fodderSeconds));
            Assert.That(spawned, Has.All.Matches<Enemy>(e => !e.Elite && e.maxHp == fodderHp), "opening wave contains fodder only, at the wave-1 TTK budget");
            Assert.That(spawned, Has.All.Matches<Enemy>(e => Mathf.Abs(game.waves.ExperienceFor(e) - .5f) < .0001f), "fodder reward: fixed 0.5 XP (system doc §9.2)");

            await Until(() => game.waves.TimeExpired, 12f, "wave timer expiry");
            Assert.That(game.waves.PendingCount, Is.Zero, "births stop after expiry");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveActive), "survivors keep combat open");
            Assert.That(game.waves.LivingCount, Is.GreaterThan(0), "survivors exist at timer expiry");
            foreach (var enemy in game.waves.Alive.ToArray())
            {
                if (!enemy || !enemy.IsAlive) continue;
                enemy.SuppressRewards = true;
                enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
            }
            await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 5f, "next weapon crate");
            await ClickUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            Assert.That(game.runWeapons.Offers.Where(w => w != null).Select(w => w.form), Is.Unique, "armory form uniqueness");
            Assert.That(game.runWeapons.Offers, Has.Length.EqualTo(RunWeapons.ArmoryOfferCount), "three armory offers");
            Assert.That(game.runWeapons.Offers, Has.None.Matches<RunWeapon>(w => w != null && game.runWeapons.OwnsForm(w.form) && w.upgradeOf == null), "held forms only appear as upgrades");
            Assert.That(game.runWeapons.Offers[0], Is.Not.Null.And.Matches<RunWeapon>(w => w.upgradeOf == null), "first offer is a new form");
            var flourish = Object.FindAnyObjectByType<V2CardPickFx>(FindObjectsInactive.Include);
            Assert.That(flourish, Is.Not.Null, "the card-pick flourish is saved in the scene");
            int picks = game.UiSnapshot.cardPickSequence, plays = flourish.Plays;
            await ClickUntil(game, ArenaUiCommand.TakeArmory, () => game.RunPhase == ArenaUiState.WeaponIntro, 0);
            Assert.That(game.UiSnapshot.cardPickSequence, Is.EqualTo(picks + 1), "taking an offer is one card pick");
            Assert.That(flourish.Plays, Is.EqualTo(plays + 1), "the picked card plays its flourish");
            await ClickUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase == ArenaUiState.Ready);
            Assert.That(game.runWeapons.Hand.Count(w => w != null), Is.EqualTo(2), "second hand weapon equipped");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Ready), "manual next wave confirmation remains");
        }

        [Test, LoadScene(SceneEn), FocusGameView, Category("Integration"), Category("Acceptance"), Timeout(25000)]
        public async Task EnglishRun_ClickedCrate_ReachesThreeStartingWeaponChoices()
        {
            var game = await ReadyScene();

            await OpenLoadout(game);

            Assert.That(game.UiSnapshot.loadoutChoices, Has.Length.EqualTo(3), "three initial choices");
            Assert.That(game.runWeapons.Hand, Has.All.Null, "opening the crate has not auto-equipped a weapon");
            Assert.That(game.UiSnapshot.selectionUnlimited, Is.True, "English choice has no countdown");
        }

        [Test, LoadScene(SceneEn), Category("Integration"), Timeout(25000)]
        public async Task BottomRow_SlotTexts_StayInsideTheirOwnBoxes()
        {
            await ReadyScene();
            var hud = Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            var slots = hud.handSlots.Concat(hud.autoSlots).Concat(hud.skillSlots).Where(slot => slot).ToArray();
            Assert.That(slots, Has.Length.EqualTo(8), "two hand, three auto and three skill slots");
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                Rect box = Bounds(slot.transform, hud.transform);
                for (int j = i + 1; j < slots.Length; j++)
                    Assert.That(box.Overlaps(Bounds(slots[j].transform, hud.transform)), Is.False, $"{slot.name} overlaps {slots[j].name}");
                var texts = new[] { slot.transform.Find("SlotKey").GetComponent<Text>(), slot.title, slot.detail };
                var glyphs = new Graphic[] { slot.icon, slot.weaponIcon, slot.contentIcon, slot.cooldown };
                foreach (var text in texts)
                {
                    Rect area = Bounds(text.transform, hud.transform);
                    Assert.That(area.xMin >= box.xMin - .5f && area.xMax <= box.xMax + .5f && area.yMin >= box.yMin - .5f && area.yMax <= box.yMax + .5f,
                        Is.True, $"{slot.name}/{text.name} stays inside the slot");
                    Assert.That(text.resizeTextForBestFit && text.verticalOverflow == VerticalWrapMode.Truncate, Is.True,
                        $"{slot.name}/{text.name} shrinks to fit instead of spilling");
                    foreach (var glyph in glyphs)
                        if (glyph) Assert.That(area.Overlaps(Bounds(glyph.transform, hud.transform)), Is.False, $"{slot.name}/{text.name} is clear of {glyph.name}");
                    foreach (var other in texts)
                        if (other != text) Assert.That(area.Overlaps(Bounds(other.transform, hud.transform)), Is.False, $"{slot.name}/{text.name} is clear of {other.name}");
                }
            }
        }

        static Rect Bounds(Transform part, Transform space)
        {
            var corners = new Vector3[4];
            ((RectTransform)part).GetWorldCorners(corners);
            Vector2 a = space.InverseTransformPoint(corners[0]), b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(25000)]
        public async Task GainXp_TrueLevelsAndPostCapDrafts_PreservesDeficitAndManaCardBudget()
        {
            var game = await ReadyScene();
            var progression = game.progression;
            game.player.ReceiveDamage(new DamageInfo { amount = 5f, source = DamageSource.Poison, ignoreShield = true });

            progression.GainXp(15f);

            Assert.That(progression.Level, Is.EqualTo(2), "level two");
            Assert.That(game.player.maxHp, Is.EqualTo(30f), "level-two max HP");
            Assert.That(game.player.Hp, Is.EqualTo(25f), "only five HP restored, existing deficit retained");
            Assert.That(progression.Stats.maxEnergy, Is.EqualTo(106f), "capacity grows before first skill");
            Assert.That(progression.EnergyUnlocked, Is.False, "levelling alone does not unlock skills");

            progression.UnlockEnergy();
            progression.ApplyStat(new StatCardOffer(StatId.MaxEnergy, CardRarity.Common, 1000f));

            progression.GainXp(899f);
            Assert.That(progression.Level, Is.EqualTo(12), "level cap");
            Assert.That(game.player.maxHp, Is.EqualTo(80f), "max-level HP without HP cards");
            Assert.That(progression.Stats.maxEnergy, Is.EqualTo(316f), "level growth retains original 150 mana card budget");
            Assert.That(progression.Energy, Is.EqualTo(316f), "unlocked mana refills on true level-up");
            int before = progression.BankedDrafts;

            progression.GainXp(123f);

            Assert.That(progression.BankedDrafts, Is.EqualTo(before + 1), "post-cap XP grants one further card choice");
            Assert.That(game.player.maxHp, Is.EqualTo(80f), "post-cap draft grants no extra level HP");
            Assert.That(progression.Stats.maxEnergy, Is.EqualTo(316f), "post-cap draft grants no extra level mana");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "XP does not independently open a menu");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(25000)]
        public async Task Drop_XpEightUnitsAway_MagnetCollectsWithoutWalkingOntoIt()
        {
            var game = await ReadyScene();
            await BeginCombat(game);
            game.waves.enabled = false;
            Vector3 origin = game.player.Position;
            Vector3 drop = origin;
            bool clearPath = false;
            for (int sector = 0; sector < 16 && !clearPath; sector++)
            {
                Vector3 direction = Quaternion.Euler(0, sector * 22.5f, 0) * Vector3.forward;
                clearPath = true;
                for (int step = 1; step <= 32; step++)
                    if (SkillWorldAPI.IsHole?.Invoke(origin + direction * (step * .25f)) == true) { clearPath = false; break; }
                if (clearPath) drop = origin + direction * 8f;
            }
            Assume.That(clearPath, Is.True, "a straight eight-unit path without a hole exists");
            Assert.That(game.orbs.xpMagnetBaseRadius, Is.EqualTo(9f), "saved doubled starting range");
            float before = game.progression.Xp;

            game.orbs.Drop(drop, .1f);
            await Until(() => game.progression.Xp > before, 4f, "XP pickup from eight units away");

            Assert.That(game.progression.Xp - before, Is.EqualTo(.1f).Within(.00001f), "collect exactly once");
            Assert.That(Query.Flat(game.player.Position - origin).magnitude, Is.LessThan(1f), "player did not walk to the pickup");
            Assert.That(game.orbs.pool, Has.None.Matches<ExperienceOrb>(o => o && o.InUse), "collected orb returns to saved pool");
        }

        [Test, LoadScene(SceneCn), Timeout(20000)]
        public async Task RollArmory_EarlyAndLateOffers_DistinctFormsWithHeldWeaponsOnlyAsHigherMk()
        {
            var game = await ReadyScene();
            game.runWeapons.Hand[0] = new RunWeapon { form = WeaponForm.Rifle, variant = 0 };
            game.runWeapons.Hand[1] = new RunWeapon { form = WeaponForm.Knife, variant = 1 };
            game.runWeapons.Auto[0] = new RunWeapon { form = WeaponForm.OrbitBlades, variant = 0 };
            var failures = new List<string>();
            int upgradesSeen = 0;

            for (int wave = 2; wave <= 15; wave++)
            for (int draw = 0; draw < 20; draw++)
            {
                game.runWeapons.RollArmory(wave);
                var offers = game.runWeapons.Offers.Where(w => w != null).ToArray();
                var forms = offers.Select(w => w.form).ToArray();
                bool distinct = forms.Distinct().Count() == forms.Length;
                bool heldOnlyAsUpgrade = offers.All(w => !game.runWeapons.OwnsForm(w.form) || w.upgradeOf != null && w.upgradeOf.form == w.form && w.mk > w.upgradeOf.mk);
                bool newFormPresent = offers.Any(w => w.upgradeOf == null);
                upgradesSeen += offers.Count(w => w.upgradeOf != null);
                if (game.runWeapons.Offers.Length != RunWeapons.ArmoryOfferCount || !distinct || !heldOnlyAsUpgrade || !newFormPresent)
                    failures.Add($"wave {wave}, draw {draw}: {string.Join(",", offers.Select(w => w.form + (w.upgradeOf != null ? " Mk" + w.mk : "")))}");
            }

            Assert.That(failures, Is.Empty);
            Assert.That(upgradesSeen, Is.GreaterThan(0), "held weapons do appear as upgrades");
        }

        [Test, LoadScene(SceneCn), Timeout(20000)]
        public async Task TakeOffer_HeldWeaponUpgrade_RaisesMkInPlaceAndKeepsChanges()
        {
            var game = await ReadyScene();
            var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, axes = UpgradeAxis.Twist, revealed = true };
            game.runWeapons.Hand[0] = rifle;
            game.runWeapons.Hand[1] = null;
            RunWeapon upgrade = null;
            for (int draw = 0; draw < 200 && upgrade == null; draw++)
            {
                game.runWeapons.RollArmory(5);
                upgrade = game.runWeapons.Offers.FirstOrDefault(w => w != null && w.upgradeOf == rifle);
            }
            Assert.That(upgrade, Is.Not.Null, "held rifle offered as an upgrade");
            int index = System.Array.IndexOf(game.runWeapons.Offers, upgrade);

            Assert.That(game.runWeapons.TakeOffer(index), Is.True);

            Assert.That(game.runWeapons.Hand[0], Is.SameAs(rifle), "same weapon object, no slot change");
            Assert.That(rifle.mk, Is.EqualTo(game.runWeapons.UpgradeTargetMk(new RunWeapon { mk = 1 }, 5)), "catches up to one tier above the wave standard");
            Assert.That(rifle.Has(UpgradeAxis.Twist), Is.True, "mechanic change kept");
            Assert.That(game.runWeapons.Hand[1], Is.Null, "upgrade does not fill a slot");
        }

        [Test, LoadScene(SceneCn), Timeout(20000)]
        public async Task TryFindV2SpawnPoint_OpenStartingArea_DistributesAcrossAllFourSides()
        {
            var game = await ReadyScene();
            var fodder = game.waves.roster.First(d => d && d.kind == EnemyKind.Grunt);
            int[] quadrants = new int[4];
            int accepted = 0;
            var violations = new List<float>();

            for (int i = 0; i < 64; i++)
            {
                if (!game.waves.TryFindV2SpawnPoint(fodder, out var point)) continue;
                Vector3 offset = Query.Flat(point - game.player.Position);
                quadrants[(offset.x >= 0 ? 1 : 0) + (offset.z >= 0 ? 2 : 0)]++;
                accepted++;
                if (offset.magnitude < game.waves.nearbySpawnMin - .01f || offset.magnitude > game.waves.nearbySpawnMax + .01f)
                    violations.Add(offset.magnitude);
            }

            TestContext.Progress.WriteLine($"V3_SPAWN accepted={accepted}/64 quadrants={string.Join(",", quadrants)}");
            Assert.That(accepted, Is.EqualTo(64), "all candidates find walkable points around the saved starting area");
            Assert.That(quadrants, Has.All.GreaterThanOrEqualTo(4), "every quarter of the surrounding ring is used");
            Assert.That(violations, Is.Empty, "every birth remains within player-near distance bounds");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Category("Acceptance"), Timeout(25000)]
        public async Task FinalBossAndPlayerDieInSameFrame_VictoryTakesPriority()
        {
            var game = await ReadyScene();
            await BeginCombat(game);
            game.waves.enabled = false;

            game.NotifyFinalBossDefeated();
            game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
            await Awaitable.NextFrameAsync();

            Assert.That(game.State, Is.EqualTo(GameState.Won), "agreed final-boss double KO counts as victory");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Result), "double KO ends the run once");
        }

        [TestCase(6), TestCase(7), TestCase(8), LoadScene(SceneCn), Category("Integration"), Category("Acceptance"), Timeout(45000)]
        public async Task MiddleWave_DenseFirePoisonAndAreaDamage_ClockKeepsRunningWithoutGlobalFreeze(int wave)
        {
            var game = await ReadyScene();
            ShortenWaves(game, 20f);
            await BeginCombat(game, wave);
            int target = game.waves.PopulationTargetForWave(wave - 1);
            await Until(() => game.waves.LivingCount >= Mathf.FloorToInt(target * .65f), 8f, "dense authored enemy population");
            int peak = game.waves.LivingCount, ticks = 0, scaleDrops = 0, sampledFrames = 0;
            var observed = new HashSet<ElementStatus>();
            void ApplyDots(Enemy enemy)
            {
                var status = ElementStatus.For(enemy);
                if (!status) return;
                if (observed.Add(status)) status.DamageTicked += CountTick;
                status.Add(ElementKind.Fire, 2f, game.player);
                status.Add(ElementKind.Poison, 1f, game.player);
            }
            void CountTick(float _, DamageSource __, Combatant ___) => ticks++;
            game.waves.EnemySpawned += ApplyDots;
            foreach (var enemy in game.waves.Alive.ToArray()) if (enemy && enemy.IsAlive) ApplyDots(enemy);
            var field = AttackPool.Instance.Acquire();
            Assume.That(field, Is.Not.Null, "one authored attack actor is available");
            field.Configure(AttackMode.Field, game.player,
                new RunWeapon { form = WeaponForm.Flamethrower, variant = 1, element = ElementKind.Fire },
                game.player.Position, Vector3.forward, 1f, 0f, 0f, 0f, 18f, 4f);
            field.tickInterval = .4f;
            float startTime = Time.realtimeSinceStartup, startClock = game.waves.TimeLeft;
            try
            {
                while (Time.realtimeSinceStartup - startTime < 4f)
                {
                    await Awaitable.NextFrameAsync();
                    sampledFrames++;
                    peak = Mathf.Max(peak, game.waves.LivingCount);
                    if (Time.timeScale < .999f) scaleDrops++;
                }
            }
            finally
            {
                game.waves.EnemySpawned -= ApplyDots;
                foreach (var status in observed) if (status) status.DamageTicked -= CountTick;
            }
            float elapsedClock = startClock - game.waves.TimeLeft;
            TestContext.Progress.WriteLine($"V3_DENSE wave={wave} peak={peak} target={target} frames={sampledFrames} dotTicks={ticks} globalSlowFrames={scaleDrops} clockAdvanced={elapsedClock:0.000}");

            Assert.That(ticks, Is.GreaterThan(0), "actual DOT ticks observed");
            Assert.That(scaleDrops, Is.Zero, "enemy DOT/field hits never slow the whole world");
            Assert.That(elapsedClock, Is.GreaterThan(1.5f), "wave clock advances during dense effects");
            Assert.That(game.player.IsAlive, Is.True, "protected smoke player remains alive");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveActive), "combat state remains active");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(25000)]
        public async Task LearnAndTryCast_Overcharge_UnlocksManaSpendsCostAndStartsCooldown()
        {
            var game = await ReadyScene();
            await BeginCombat(game);
            Assert.That(game.skills.Learn(SkillId.Overcharge), Is.True, "learn saved skill definition");
            float before = game.progression.Energy;
            float cost = game.skills.Slots[0].Definition.cost;

            bool actual = game.skills.TryCast(0);

            Assert.That(actual, Is.True, "cast succeeds");
            Assert.That(game.progression.EnergyUnlocked, Is.True, "learning skill unlocks mana");
            Assert.That(game.progression.Energy, Is.EqualTo(before - cost), "cast spends exact saved cost");
            Assert.That(game.skills.Slots[0].CooldownLeft, Is.GreaterThan(0f), "cooldown begins");
            Assert.That(game.skills.WeaponCooldownRate, Is.EqualTo(2f), "overcharge gameplay effect applies");
            Assert.That(game.skills.TryCast(0), Is.False, "immediate recast is refused");
            Assert.That(game.progression.Energy, Is.EqualTo(before - cost), "refused recast does not charge mana twice");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task PlayerHit_NonBossEnemiesNeitherStunNorPush_BossesStillPush_GameNeverSlows()
        {
            var game = await ReadyScene();
            await BeginCombat(game);
            game.waves.enabled = false;
            var player = game.player;
            player.protectionTime = 0f;
            player.ClearWaveState();
            Enemy Attacker(EnemyKind kind, bool elite)
            {
                var enemy = game.waves.preparedEnemies.First(e => e && !e.gameObject.activeSelf && e.definition && e.definition.kind == kind);
                enemy.Configure(enemy.definition, 1f, elite);
                enemy.enabled = false;
                enemy.gameObject.SetActive(false);
                return enemy;
            }

            var grunt = Attacker(EnemyKind.Grunt, false);
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, grunt, Vector3.right, 6f)), Is.True, "the basic hit lands");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "a hit on the player never slows the game");
            Assert.That(player.State, Is.EqualTo(CharacterState.Normal), "basic enemies do not knock the player back");
            Assert.That(player.Stunned, Is.False, "a basic hit does not stun the player");
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, grunt, Vector3.right, 6f)), Is.True, "a second hit still deals damage");

            var spire = Attacker(EnemyKind.Spire, true);
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, spire, Vector3.right, 8f)), Is.True, "the elite hit lands");
            Assert.That(player.State, Is.EqualTo(CharacterState.Normal), "elites do not knock the player back either");
            Assert.That(player.Stunned, Is.False, "an elite's plain hit does not stun either (only a Rammer charge stuns, in Enemy)");

            foreach (var kind in new[] { EnemyKind.Stomper, EnemyKind.Rammer })
            {
                player.ClearWaveState();
                var pusher = Attacker(kind, false);
                Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, pusher, Vector3.right, pusher.definition.knockback)), Is.True, kind + " hit lands");
                Assert.That(player.State, Is.EqualTo(CharacterState.Knocked), kind + " knocks the player back");
            }

            player.ClearWaveState();
            var boss = game.waves.tyrant;
            Assume.That(boss && boss.definition && boss.IsBoss, Is.True, "the scene keeps the Tyrant");
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, boss, Vector3.right, 6f)), Is.True, "the boss hit lands");
            Assert.That(player.State, Is.EqualTo(CharacterState.Knocked), "bosses still knock the player back");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "a boss hit does not slow the game either");

            player.ClearWaveState();

            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Paused), "pause menu is accepted after a hit");
            Assert.That(Time.timeScale, Is.Zero, "menu has fully paused time");
            await CommandUntil(game, ArenaUiCommand.Resume, () => game.RunPhase == ArenaUiState.WaveActive);
            await Awaitable.NextFrameAsync();
            Assert.That(Time.timeScale, Is.EqualTo(1f), "resume restores normal time");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task LandedHits_ShotsJoltWithoutHold_MeleeHoldsOncePerCooldown()
        {
            var game = await ReadyScene();
            await BeginCombat(game);
            game.waves.enabled = false;
            var target = game.waves.preparedEnemies.First(e => e && !e.gameObject.activeSelf && e.definition && e.definition.kind == EnemyKind.Grunt);
            target.Configure(target.definition, 1f, false);
            target.maxHp = 100000f;
            target.Heal(100000f);

            var spot = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back }
                .Select(d => game.player.Position + d * 3f)
                .FirstOrDefault(p => !WorldMap.Active || WorldMap.Active.IsWalkable(p, target.radius));
            V3CombatSmokeTest.Teleport(target, spot);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            Assume.That(target.IsAlive && target.State == CharacterState.Normal, Is.True, "the target stands on the ground");
            DamageInfo Hit(HitKind kind, HitFeel feel)
            {
                var info = DamageInfo.Attack(1f, game.player, Vector3.right, 0f, kind);
                info.feel = feel;
                return info;
            }

            target.ReceiveDamage(Hit(HitKind.Bullet, HitFeel.LightShot));
            Assert.That(target.HitPaused, Is.False, "a light round never holds the target");
            Assert.That(target.VisualOffset.sqrMagnitude, Is.GreaterThan(0f), "a light round jolts the model");

            target.ReceiveDamage(Hit(HitKind.Slam, HitFeel.Melee));
            float meleeAt = Time.realtimeSinceStartup;
            Assert.That(target.HitPaused, Is.True, "a melee hit holds the target");
            Assert.That(game.player.PoseHeld, Is.True, "a melee hit also bites on the attacker's animation");
            Assert.That(game.player.Stunned, Is.False, "the attacker's bite never stops its movement or actions");
            await Until(() => !target.HitPaused, 1f, "melee hold ends");
            target.ReceiveDamage(Hit(HitKind.Slam, HitFeel.Melee));
            Assert.That(target.HitPaused, Is.False, "a melee hit inside the cooldown does not hold again");
            float readyAfter = game.feedback.melee.victimHold + game.feedback.victimHoldCooldown + .05f;
            await Until(() => Time.realtimeSinceStartup - meleeAt >= readyAfter, 2f, "hold cooldown passes");
            target.ReceiveDamage(Hit(HitKind.Slam, HitFeel.Melee));
            Assert.That(target.HitPaused, Is.True, "after the cooldown a melee hit holds again");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "landed hits never slow the game");
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "saved scene and localization initialize");
            var game = GameManager.Instance;
            Assume.That(game.player && game.waves && game.runWeapons && game.progression && game.skills && game.orbs && game.hud && game.hud.v2Hud,
                Is.True, "V2 scene references exist");
            return game;
        }

        void ShortenWaves(GameManager game, float duration)
        {
            originalWaves = game.waves.waveSet;
            runtimeWaves = Object.Instantiate(originalWaves);
            foreach (var wave in runtimeWaves.waves) wave.duration = duration;
            game.waves.waveSet = runtimeWaves;
        }

        static async Task OpenLoadout(GameManager game)
        {
            await ClickUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            Assert.That(game.SupplyDropReady, Is.False, "crate falls before it can open");
            await Until(() => game.SupplyDropReady, 4f, "supply crate landing");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.SupplyDrop), "landing alone does not open selection");
            await ClickUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
        }

        static async Task BeginCombat(GameManager game, int wave = 1)
        {

            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 4f, "supply crate landing");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase == ArenaUiState.WaveIntro);
            while (game.waves.WaveIndex < wave - 1) game.waves.BeginNextWave();
            game.player.ProtectFor(120f);
            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 7f, "wave introduction completes");
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} did not complete; phase={game.RunPhase}");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task ClickUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {

            float deadline = Time.realtimeSinceStartup + 5f;
            string blocker = "no active command button";
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Button {command}[{index}] unreachable or rejected: {blocker}; phase={game.RunPhase}");
                Canvas.ForceUpdateCanvases();
                await Awaitable.NextFrameAsync();
                var action = Object.FindObjectsByType<ArenaUiButton>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.command == command && b.index == index && b.GetComponent<Button>().IsInteractable());
                if (!action || !EventSystem.current) continue;
                var button = action.GetComponent<Button>();
                var rect = (RectTransform)button.transform;
                var canvas = button.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0) { blocker = "empty raycast"; continue; }
                var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                if (receiver != button.gameObject) { blocker = hits[0].gameObject.name; continue; }
                blocker = "pointer accepted; controller input debounce";
                ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
