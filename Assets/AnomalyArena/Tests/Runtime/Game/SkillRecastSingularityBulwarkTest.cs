using System;
using System.Collections.Generic;
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
    public class SkillRecastSingularityBulwarkTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";

        static readonly SkillId[] Lasting =
        {
            SkillId.FlameWake, SkillId.Contagion, SkillId.StormCall, SkillId.Bulwark, SkillId.Radiance, SkillId.GravityWell, SkillId.TimeSlip,
            SkillId.Decoy, SkillId.Overcharge, SkillId.VoidWalk, SkillId.Singularity, SkillId.PocketVoid,
        };

        static readonly SkillId[] Instant = { SkillId.FrostNova, SkillId.Shockwave, SkillId.Blink };

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, CreateScene]
        public void ResolveDamage_BulwarkShieldLeft_HitsCountItsShareAndUseItFirst_ThenTheNormalShare()
        {
            var sut = Board(out var rules);
            try
            {
                sut.GainShield(10f);
                sut.GainBulwarkShield(10f, .5f);

                float first = sut.ResolveDamage(10f);
                float shieldAfterFirst = sut.Shield, bulwarkAfterFirst = sut.BulwarkShield;
                float second = sut.ResolveDamage(20f);
                float shieldAfterSecond = sut.Shield, bulwarkAfterSecond = sut.BulwarkShield;
                float third = sut.ResolveDamage(10f);

                Assert.That(first, Is.Zero, "first hit: no HP lost");
                Assert.That(shieldAfterFirst, Is.EqualTo(15f), "first hit: 10 counts 5 on the Shield");
                Assert.That(bulwarkAfterFirst, Is.EqualTo(5f), "first hit: taken from the Bulwark's part first");
                Assert.That(second, Is.Zero, "second hit: no HP lost");
                Assert.That(shieldAfterSecond, Is.EqualTo(5f), "second hit: it meets the Bulwark's part, so 20 counts 10");
                Assert.That(bulwarkAfterSecond, Is.Zero, "second hit: the Bulwark's part is used up");
                Assert.That(third, Is.EqualTo(Mathf.Round(10f * rules.shieldDirectFactor) - 5f), "third hit: the normal 70%, past the last 5 Shield");
                Assert.That(sut.Shield, Is.Zero, "third hit: the Shield is gone");
            }
            finally { Object.Destroy(rules); }
        }

        [Test, CreateScene]
        public void SetShield_BelowTheBulwarkPart_TrimsThatPartToo()
        {
            var sut = Board(out var rules);
            try
            {
                sut.GainBulwarkShield(10f, .5f);

                sut.SetShield(4f);

                Assert.That(sut.BulwarkShield, Is.EqualTo(4f));
            }
            finally { Object.Destroy(rules); }
        }

        [Test, CreateScene]
        public void GainBulwarkShield_BelowLevelFive_LeavesNoBulwarkPart()
        {
            var sut = Board(out var rules);
            try
            {
                sut.GainBulwarkShield(10f);

                Assert.That(sut.BulwarkShield, Is.Zero);
            }
            finally { Object.Destroy(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BulwarkShield_LevelFourAsANormalShield_LevelFiveLiftsAnyShieldToSixtyPercentAboveTheCapAndHitsOnItCountHalf()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            var player = game.player;
            var board = ElementStatus.For(player);
            var bulwark = skills.Catalog.Find(SkillId.Bulwark);
            skills.ResetRun();
            WorldMap.Active.ClearTemporaryObjects();
            board.SetShield(0f);
            player.protectionTime = 0f;
            player.DebugSetAimPoint(player.Position + Vector3.forward * 12f);
            float taken = 10f * game.progression.Stats.DamageTakenMultiplier;
            Assume.That(skills.Learn(SkillId.Bulwark, -1, 4), Is.True, "Bulwark at level 4");
            Assume.That(skills.TryCast(0), Is.True, "Bulwark at level 4 is cast");
            float shieldAtFour = board.Shield;
            float hitAtFour = ShieldSpentBy(player, board, targets[0], 10f);
            skills.ClearWaveEffects();
            WorldMap.Active.ClearTemporaryObjects();
            board.SetShield(0f);

            board.GainShield(player.maxHp);
            float ordinary = board.Shield;
            skills.Learn(SkillId.Bulwark);
            skills.Slots[0].CooldownLeft = 0f;
            Assume.That(skills.TryCast(0), Is.True, "Bulwark at level 5 is cast");
            float shieldAtFive = board.Shield, bulwarkPart = board.BulwarkShield;
            board.GainShield(1f);
            float afterTopUp = board.Shield;

            float hitAtFive = ShieldSpentBy(player, board, targets[0], 10f);

            float cap = player.maxHp * board.Rules.playerShieldCapShare;
            Assert.That(shieldAtFour, Is.EqualTo(player.maxHp * .35f).Within(1e-3f), "level 4: a Shield of 35% of max HP");
            Assert.That(hitAtFour, Is.EqualTo(Mathf.Round(taken * board.Rules.shieldDirectFactor)), "level 4: a hit counts 70% on it");
            Assert.That(ordinary, Is.EqualTo(cap).Within(1e-3f), "an ordinary Shield stops at the usual cap");
            Assert.That(shieldAtFive, Is.EqualTo(player.maxHp * .6f).Within(1e-3f), "level 5: the Shield is lifted to 60% of max HP");
            Assert.That(shieldAtFive, Is.GreaterThan(cap), "level 5: above the usual cap");
            Assert.That(bulwarkPart, Is.EqualTo(shieldAtFive), "level 5: the Shield it lifted merges into the Bulwark's");
            Assert.That(afterTopUp, Is.EqualTo(shieldAtFive), "a Shield top-up from elsewhere never cuts it back to the cap");
            Assert.That(hitAtFive, Is.EqualTo(Mathf.Round(taken * bulwark.bulwarkShieldDirectFactor)), "level 5: a hit counts half on it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task TryCast_EffectStillLasting_RefusedSpendingNothingAndSlotNotReady_InstantSkillsCastAgain()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            var progression = game.progression;
            Vector3 aim = game.player.Position + Vector3.forward * 12f;
            string reason = null;
            void Refused(int _, string message) => reason = message;
            var wrong = new List<string>();
            skills.CastRefused += Refused;
            try
            {
                foreach (var id in Lasting.Concat(Instant))
                {
                    bool lasting = Lasting.Contains(id);
                    skills.ResetRun();
                    WorldMap.Active.ClearTemporaryObjects();
                    Assume.That(skills.Learn(id, -1, id == SkillId.Blink ? SkillDefinition.MaxLevel : 1), Is.True, $"{id} is learned");
                    game.player.DebugSetAimPoint(aim);
                    progression.GainEnergy(1000f);
                    Assume.That(skills.TryCast(0), Is.True, $"{id} is cast");

                    skills.Slots[0].CooldownLeft = 0f;
                    progression.GainEnergy(1000f);
                    float energy = progression.Energy;
                    bool shownReady = game.UiSnapshot.skillSlots[0].available;
                    reason = null;

                    bool again = skills.TryCast(0);

                    if (lasting && (again || reason != ArenaText.Pair("Still active","Still active")))
                        wrong.Add($"{id}: cast again {again}, refused with \"{reason}\"");
                    if (lasting && (progression.Energy != energy || skills.Slots[0].CooldownLeft > 0f))
                        wrong.Add($"{id}: the refused press spent Energy or started the cooldown");
                    if (lasting == shownReady) wrong.Add($"{id}: slot shows ready {shownReady} while the effect lasts {lasting}");
                    if (!lasting && !again) wrong.Add($"{id}: an instant skill was refused (\"{reason}\")");
                }
            }
            finally
            {
                skills.CastRefused -= Refused;
                skills.ResetRun();
                WorldMap.Active.ClearTemporaryObjects();
            }

            Assert.That(wrong, Is.Empty);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task TryCast_BulwarkWallStanding_IsRefused_OnceTheTyrantBreaksItCastsAgain()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            skills.ResetRun();
            WorldMap.Active.ClearTemporaryObjects();
            Assume.That(skills.Learn(SkillId.Bulwark), Is.True, "Bulwark is learned");
            game.player.DebugSetAimPoint(game.player.Position + Vector3.forward * 12f);
            Assume.That(skills.TryCast(0), Is.True, "Bulwark is cast");
            var wall = WorldMap.Active.temporaryWalls.First(w => w && w.gameObject.activeSelf);
            skills.Slots[0].CooldownLeft = 0f;
            bool whileStanding = skills.TryCast(0);

            wall.gameObject.SetActive(false);

            bool afterBreak = skills.TryCast(0);

            Assert.That(whileStanding, Is.False, "no second Bulwark while the first wall stands");
            Assert.That(afterBreak, Is.True, "a broken wall no longer stands, so it casts again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task MirrorImageEcho_TheOtherSkillStillLasting_SkipsIt_OnceItHasEndedCastsIt()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            var world = WorldMap.Active;
            skills.ResetRun();
            world.ClearTemporaryObjects();
            Assume.That(skills.Learn(SkillId.Decoy, -1, SkillDefinition.MaxLevel) && skills.Learn(SkillId.Contagion), Is.True,
                "Mirror Image at level 5 and Contagion");
            int decoy = SlotOf(skills, SkillId.Decoy), contagion = SlotOf(skills, SkillId.Contagion);
            game.player.DebugSetAimPoint(game.player.Position + Vector3.forward * 12f);
            int echoes = 0;
            void Counted(SkillId id, Vector3 _, float __, float ___) { if (id == SkillId.Contagion) echoes++; }
            int whileLasting;
            bool stillLasting;
            skills.CastSucceeded += Counted;
            try
            {
                Assume.That(skills.TryCast(contagion), Is.True, "Contagion is cast");
                Assume.That(skills.TryCast(decoy), Is.True, "the mirror image is cast");
                echoes = 0;
                await Until(() => !world.DecoyActive, 8f, "the mirror ends");
                await Awaitable.NextFrameAsync();
                whileLasting = echoes;
                stillLasting = skills.EffectActive(SkillId.Contagion);
                skills.ClearWaveEffects();
                skills.Slots[decoy].CooldownLeft = 0f;
                game.progression.GainEnergy(1000f);
                echoes = 0;
                Assume.That(skills.TryCast(decoy), Is.True, "the mirror image is cast again");

                await Until(() => !world.DecoyActive, 8f, "the mirror ends");
                await Awaitable.NextFrameAsync();
            }
            finally { skills.CastSucceeded -= Counted; }

            Assume.That(stillLasting, Is.True, "Contagion still lasted when the first mirror ended");
            Assert.That(whileLasting, Is.Zero, "no echo of Contagion while it still lasts");
            Assert.That(echoes, Is.EqualTo(1), "the echo casts Contagion once it has ended");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Singularity_UntilItsBlast_NoDamageNoKillNoKnockback_ThenHitsLandAgain()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            var player = game.player;
            var definition = skills.Catalog.Find(SkillId.Singularity);
            skills.ResetRun();
            player.protectionTime = 0f;
            Assume.That(skills.Learn(SkillId.Singularity), Is.True, "Singularity is learned");
            Assume.That(skills.TryCast(0), Is.True, "Singularity is cast");
            float hp = player.Hp;
            float cast = Time.time;

            bool hitLanded = player.ReceiveDamage(DamageInfo.Attack(5f, null, Vector3.back, 6f));
            bool burnLanded = player.ReceiveDamage(new DamageInfo { amount = 3f, source = DamageSource.Burn, dealer = targets[0], element = ElementKind.Fire });
            player.Kill(targets[0]);
            bool alive = player.IsAlive, invincible = skills.Invincible;
            float hpMeanwhile = player.Hp;
            var stateMeanwhile = player.State;
            await Until(() => !skills.SingularityActive, 6f, "the blast");
            float lasted = Time.time - cast;
            bool invincibleAfter = skills.Invincible;

            bool landedAfter = player.ReceiveDamage(DamageInfo.Attack(5f, null, Vector3.back, 6f));

            Assert.That(definition.Duration(1), Is.EqualTo(3f), "it lasts 3 s");
            Assert.That(lasted, Is.EqualTo(3f).Within(.25f), "and blasts after them");
            Assert.That(invincible, Is.True, "invincible while it gathers");
            Assert.That(hitLanded || burnLanded, Is.False, "no hit or burn lands meanwhile");
            Assert.That(alive && hpMeanwhile == hp, Is.True, "no HP lost and no outright kill meanwhile");
            Assert.That(stateMeanwhile, Is.EqualTo(CharacterState.Normal), "no knockback meanwhile");
            Assert.That(invincibleAfter, Is.False, "not invincible after the blast");
            Assert.That(landedAfter && player.Hp < hp && player.State == CharacterState.Knocked, Is.True, "the same hit lands and pushes after it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Singularity_UntilItsBlast_OwnBlastsKnockNothingBackAndBackfiresStackNothing()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            var player = game.player;
            var board = ElementStatus.For(player);
            skills.ResetRun();
            Assume.That(skills.Learn(SkillId.Singularity), Is.True, "Singularity is learned");
            Assume.That(skills.TryCast(0), Is.True, "Singularity is cast");

            player.Knockback(Vector3.back, 6f, KnockKind.Hit);
            board.Add(ElementKind.Lightning, 10f, player);
            var stateMeanwhile = player.State;
            float lightningMeanwhile = board.Lightning;
            bool paralysedMeanwhile = board.Paralysed;
            await Until(() => !skills.SingularityActive, 6f, "the blast");

            player.Knockback(Vector3.back, 6f, KnockKind.Hit);

            Assert.That(stateMeanwhile, Is.EqualTo(CharacterState.Normal), "no knockback of any kind meanwhile");
            Assert.That(lightningMeanwhile, Is.Zero, "no stacks meanwhile, not even a backfire's");
            Assert.That(paralysedMeanwhile, Is.False, "so no paralysis either");
            Assert.That(player.State, Is.EqualTo(CharacterState.Knocked), "after the blast a knockback works again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Singularity_CastWhileParalysed_EndsTheParalysis()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var skills = game.skills;
            var board = ElementStatus.For(game.player);
            skills.ResetRun();
            Assume.That(skills.Learn(SkillId.Singularity), Is.True, "Singularity is learned");
            for (int i = 0; i < 50 && !board.Paralysed; i++) board.Add(ElementKind.Lightning, 1f, targets[0]);
            Assume.That(board.Paralysed && !board.BlocksActions, Is.True, "paralysed, between two twitches");

            Assume.That(skills.TryCast(0), Is.True, "Singularity is cast");

            Assert.That(board.Paralysed, Is.False, "the paralysis ends");
            Assert.That(board.Lightning, Is.Zero, "and its stacks go");
            await Awaitable.NextFrameAsync();
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task Singularity_DuringTheNashProphetsGravityPull_ThePlayerIsNotDrawn()
        {
            var (game, world, hollow) = await StartTrial();
            var b = game.waves.bosses;
            var player = game.player;
            var skills = game.skills;
            Vector3 centre = Query.Flat(world.trialPlatform.position);
            skills.ResetRun();
            Assume.That(skills.Learn(SkillId.Singularity), Is.True, "Singularity is learned");
            game.progression.GainEnergy(1000f);
            Place(player, centre + Vector3.back * 9f);
            Force(hollow, Enemy.GravityPullMove);
            await Until(() => hollow.GravityPulling, b.nashPullWindup + 2f, "the pull");
            Assume.That(skills.TryCast(0), Is.True, "Singularity is cast");
            float from = Query.Flat(player.Position - centre).magnitude, start = Time.time;

            while (Time.time - start < 1f) await Awaitable.FixedUpdateAsync();

            float drift = (from - Query.Flat(player.Position - centre).magnitude) / (Time.time - start);
            Assume.That(hollow.GravityPulling && skills.SingularityActive, Is.True, "the pull and the singularity both still run");
            Assert.That(drift, Is.LessThan(b.nashPullSpeed * .2f));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task SkillTexts_BothLanguages_SingularityNamesItsInvincibility_BulwarkItsWallLengthAndLevelFiveShield()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "saved scene and localization initialize");
            var catalog = GameManager.Instance.skills.Catalog;
            var singularity = catalog.Find(SkillId.Singularity);
            var bulwark = catalog.Find(SkillId.Bulwark);
            var texts = new Dictionary<string, (string singularity, string bulwarkOne, string bulwarkFive)>();
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                foreach (var code in new[] { "zh-Hans", "en" })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    texts[code] = (singularity.Description(1), bulwark.Description(1), bulwark.Description(SkillDefinition.MaxLevel));
                }
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                Object.Destroy(locale.gameObject);
            }

            Assert.That(texts["zh-Hans"].singularity, Does.Contain("for 3 s").And.Contain("fully invincible").And.Contain("heavily slowed"), "Chinese Singularity");
            Assert.That(texts["en"].singularity, Does.Contain("for 3 s").And.Contain("fully invincible").And.Contain("heavily slowed"), "English Singularity");
            Assert.That(texts["zh-Hans"].bulwarkOne, Does.Contain("wall 5 long").And.Contain("Shield 20% of max HP"), "Chinese Bulwark level 1");
            Assert.That(texts["en"].bulwarkOne, Does.Contain("wall 5 long").And.Contain("Shield 20% of max HP"), "English Bulwark level 1");
            Assert.That(texts["zh-Hans"].bulwarkFive, Does.Contain("wall 8 long").And.Contain("Shield 60% of max HP").And.Contain("count 50%"), "Chinese Bulwark level 5");
            Assert.That(texts["en"].bulwarkFive, Does.Contain("wall 8 long").And.Contain("Shield 60% of max HP").And.Contain("count 50%"), "English Bulwark level 5");
        }

        static int SlotOf(SkillLoadout skills, SkillId id) => Array.FindIndex(skills.Slots, s => !s.Empty && s.Id == id);

        static ElementStatus Board(out StatusRules rules)
        {
            rules = ScriptableObject.CreateInstance<StatusRules>();
            var board = new GameObject("status board").AddComponent<ElementStatus>();
            board.Rules = rules;
            return board;
        }

        static float ShieldSpentBy(PlayerController player, ElementStatus board, Combatant dealer, float damage)
        {
            float before = board.Shield;
            player.ReceiveDamage(DamageInfo.Attack(damage, dealer));
            return before - board.Shield;
        }

        static void Force(Enemy hollow, int move)
        {
            hollow.ForceNextBossMove(move);
            typeof(Enemy).GetField("cooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hollow, 0f);
        }

        static void Place(Combatant body, Vector3 at)
        {
            body.ClearWaveState();
            V3CombatSmokeTest.Teleport(body, new Vector3(at.x, 0f, at.z));
            Physics.SyncTransforms();
        }

        static async Task<(GameManager game, WorldMap world, Enemy hollow)> StartTrial()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active && Object.FindAnyObjectByType<EnemyLab>(), 12f,
                "scene, localization, map and lab");
            var game = GameManager.Instance;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The lab never reached combat; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Loadout) game.ExecuteUiCommand(ArenaUiCommand.ChooseLoadout, 0);
                await Awaitable.NextFrameAsync();
            }

            var lab = Object.FindAnyObjectByType<EnemyLab>();
            var world = WorldMap.Active;
            lab.selected = EnemyKind.Hollow;
            Assume.That(lab.Spawn(), Is.SameAs(world.hollow), "the lab enters the void trial");
            Assume.That(world.TrialActive, Is.True);
            await Awaitable.FixedUpdateAsync();
            return (game, world, world.hollow);
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
