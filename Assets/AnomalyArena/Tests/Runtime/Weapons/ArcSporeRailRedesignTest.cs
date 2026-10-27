using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class ArcSporeRailRedesignTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant, UpgradeAxis axes = UpgradeAxis.None)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, axes = axes, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        static void Place(Combatant body, Vector3 at)
        {
            V3CombatSmokeTest.Teleport(body, at);
            Physics.SyncTransforms();
        }

        static void LeaveHp(Combatant body, float hp) =>
            body.ReceiveDamage(new DamageInfo { amount = body.Hp - hp, source = DamageSource.Poison, ignoreShield = true });

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.NextFrameAsync();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Execute_KillingShot_GivesHalfItsCooldownBackAndArmsTheNextShotOnce()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var rail = Equip(game, WeaponForm.Railgun, 1);
            float hit = weapons.HitDamage(rail), cycle = weapons.CycleSeconds(rail);
            Assert.That(weapons.roster.Get(WeaponForm.Railgun).damageB, Is.EqualTo(36f), "owner 2026-10-27: one very heavy shot");
            Assert.That(weapons.roster.Get(WeaponForm.Railgun).cooldownB, Is.EqualTo(1.8f), "fired less often, so its damage per second stays");

            LeaveHp(targets[0], hit + targets[0].maxHp * .2f);
            weapons.HandleInput(true, true, false);
            Assert.That(targets[0].IsAlive, Is.False);
            Assert.That(rail.cooldown, Is.EqualTo(cycle * (1f - tuning.executeKillRefund)).Within(.02f), "half the cooldown comes back at once");
            Assert.That(rail.executeNextShot, Is.EqualTo(tuning.executeNextShotBonus).Within(.0001f), "and the next shot is armed");
            Assert.That(game.UiSnapshot.handSlots[weapons.ActiveIndex].detail, Does.Contain($"+{tuning.executeNextShotBonus * 100f:0}%"), "the slot shows it");

            LeaveHp(targets[1], hit * (1f + tuning.executeNextShotBonus) + targets[1].maxHp * .2f);
            rail.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            Assert.That(targets[1].IsAlive, Is.False);
            Assert.That(rail.executeNextShot, Is.EqualTo(tuning.executeNextShotBonus).Within(.0001f), "a second kill arms it again, no higher");

            targets[2].maxHp = 1000f; targets[2].Heal(targets[2].maxHp);
            rail.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            Assert.That(targets[2].maxHp - targets[2].Hp, Is.EqualTo(hit * (1f + tuning.executeNextShotBonus)).Within(.01f), "the armed shot hits harder");
            Assert.That(rail.executeNextShot, Is.Zero, "and spends the bonus");
            Assert.That(rail.cooldown, Is.EqualTo(cycle * tuning.executeMissCooldown).Within(.05f), "it killed nothing: the long cooldown");
            await Awaitable.NextFrameAsync();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Execute_BossHitButNotKilled_CountsNeitherAsAKillNorAsAMiss()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            var boss = targets[0];
            boss.Configure(game.waves.DefinitionOf(EnemyKind.Hollow), 1f, false);
            boss.enabled = false;
            boss.Heal(boss.maxHp);
            Assume.That(boss.IsBoss, Is.True, "the scene keeps the Nash Prophet");
            var player = game.player;
            ElementStatus.For(player).SetShield(0f);
            float hp = player.Hp;

            var rail = Equip(game, WeaponForm.Railgun, 1);
            weapons.HandleInput(true, true, false);
            Assert.That(boss.Hp, Is.LessThan(boss.maxHp), "the shot hits the boss");
            Assert.That(boss.IsAlive, Is.True, "and never executes it");
            Assert.That(player.Hp, Is.EqualTo(hp), "no shock: a boss hit is no miss");
            Assert.That(rail.cooldown, Is.EqualTo(weapons.CycleSeconds(rail)).Within(.02f), "and no long cooldown");
            Assert.That(rail.executeNextShot, Is.Zero, "nor is it a kill");

            var tamed = Equip(game, WeaponForm.Railgun, 1, UpgradeAxis.Tame);
            weapons.HandleInput(true, true, false);
            Assert.That(tamed.executeAuraLeft, Is.Zero, "the Tame raises no arc shield for it either");
            await Awaitable.NextFrameAsync();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LanceBeam_DrawnAlongTheSlugsRealPath_EndsWhereTheSlugStops()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            Vector3 origin = game.player.Position;

            WorldMap.Active.CreateWall(origin + Vector3.forward * 8f, 4f, 60f, 0f, game.player);
            Physics.SyncTransforms();
            var rail = Equip(game, WeaponForm.Railgun, 0, UpgradeAxis.Tame);
            bool Beam(out WeaponTracer beam)
            {
                beam = weapons.Tracers.FirstOrDefault(t => Mathf.Approximately(t.width, tuning.lanceBeamWidth) && t.until >= Time.unscaledTime);
                return beam.width > 0f;
            }

            weapons.HandleInput(true, true, false);
            await Until(() => AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == rail && a.mode == AttackMode.Bolt), 3f, "the slug leaves");
            var slug = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == rail && a.mode == AttackMode.Bolt);
            await Until(() => !slug.gameObject.activeSelf, 2f, "the slug stops at the wall");
            await Awaitable.NextFrameAsync();

            Assert.That(Beam(out var drawn), Is.True, "the shot shows as a beam");
            Assert.That(Query.Flat(drawn.from - origin).magnitude, Is.LessThan(1f), "from where it left you");
            Assert.That(Query.Flat(drawn.to - slug.transform.position).magnitude, Is.LessThan(.05f), "to where the slug really stopped");
            Assert.That(Query.Flat(drawn.to - origin).magnitude, Is.InRange(6f, 8.2f), "which is the wall, not the end of its range");
            Assert.That(Vector3.Angle(Query.Flat(drawn.to - drawn.from), Vector3.forward), Is.LessThan(1f), "along the shot");

            await Seconds(tuning.lanceBeamSeconds + .2f);
            Assert.That(Beam(out _), Is.False, "it fades a moment after the slug stops");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task CloudTwistAndTame_KickedPodLandingInsideACloud_DetonatesIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var player = game.player; var tuning = game.runWeapons.upgrades;
            Vector3 origin = player.Position, at = origin + Vector3.forward * 4f;
            var cloud = Equip(game, WeaponForm.SporeLauncher, 0, UpgradeAxis.Twist | UpgradeAxis.Tame);
            cloud.element = ElementKind.Poison; cloud.builtInElement = true;
            player.DebugSetAimPoint(at);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field), 2f, "the first cloud lands");
            AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field).Release();
            await Until(() => game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.SporePod), 1f, "the pods drop");
            var pod = game.runWeapons.Hazards.Where(h => h.kind == GroundHazardKind.SporePod).OrderBy(h => h.position.z).First();

            Vector3 landing = WorldMap.Active.ClampPoint(pod.position + Vector3.forward * tuning.sporeKickDistance, .2f);
            player.DebugSetAimPoint(landing);
            cloud.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field), 2f, "the second cloud lands");
            var second = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field);
            Assume.That(Query.Flat(landing - second.point).magnitude, Is.LessThan(second.radius - .5f), "the kick lands inside the second cloud");
            await Seconds(.5f);
            float left = second.remaining;

            V3CombatSmokeTest.Teleport(player, pod.position - Vector3.forward * .3f);
            await Until(() => second.remaining > left + .2f, 3f, "the kicked pod lands inside the cloud and sets it off");
            Assert.That(second.remaining, Is.GreaterThan(tuning.cloudSeconds - .5f), "the cloud's time starts again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task VoidTrial_StoredJumpsAndTheArmedShot_StayOnTheBattlefield()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var chain = Equip(game, WeaponForm.ArcCoil, 0, UpgradeAxis.Tame);
            var rail = new RunWeapon { form = WeaponForm.Railgun, variant = 1, revealed = true };
            weapons.Hand[1 - weapons.ActiveIndex] = rail;
            chain.chainStoredJumps = 2; rail.executeNextShot = tuning.executeNextShotBonus;

            float longCooldown = weapons.CycleSeconds(rail) * tuning.executeMissCooldown;
            rail.cooldown = rail.holeReturnSpan = longCooldown;
            Assume.That(weapons.MoveMultiplier, Is.EqualTo(tuning.chainTameMoveSpeed).Within(.0001f));

            weapons.FreezeBattlefieldForTrial(true);
            Assert.That(chain.chainStoredJumps, Is.Zero, "the stored jumps wait on the battlefield");
            Assert.That(weapons.MoveMultiplier, Is.EqualTo(1f), "so the trial gets no speed-up");
            Assert.That(rail.executeNextShot, Is.Zero, "the armed shot waits there too");
            rail.cooldown = rail.holeReturnSpan = 0f;                                         

            weapons.FreezeBattlefieldForTrial(false);
            Assert.That(chain.chainStoredJumps, Is.EqualTo(2), "back on the battlefield they are there again");
            Assert.That(rail.executeNextShot, Is.EqualTo(tuning.executeNextShotBonus).Within(.0001f));
            Assert.That(weapons.MoveMultiplier, Is.EqualTo(tuning.chainTameMoveSpeed).Within(.0001f));
            Assert.That(rail.cooldown, Is.EqualTo(longCooldown).Within(.0001f), "the battlefield cooldown comes back");
            Assert.That(weapons.GetCooldownDuration(rail), Is.EqualTo(longCooldown).Within(.0001f), "and the slot bar still spans the long cooldown");
            await Awaitable.NextFrameAsync();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task WeaponCards_BothLanguages_SayWhatTheArcCannonVenomLauncherAndRailgunNowDo()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var tuning = GameManager.Instance.runWeapons.upgrades;
            var texts = new System.Collections.Generic.Dictionary<string, string[]>();
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                foreach (var code in new[] { "zh-Hans", "en" })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    texts[code] = new[]
                    {
                        WeaponGuide.How(WeaponForm.ArcCoil, 0),
                        WeaponGuide.How(WeaponForm.ArcCoil, 1),
                        WeaponGuide.Backfire(WeaponForm.SporeLauncher, 1),
                        WeaponGuide.How(WeaponForm.Railgun, 0),
                        WeaponGuide.Backfire(WeaponForm.Railgun, 0),
                        WeaponGuide.How(WeaponForm.Railgun, 1),
                        WeaponGuide.Backfire(WeaponForm.Railgun, 1),
                        ArenaTerms.ExpandedUpgradeDescription(WeaponForm.ArcCoil, 0, UpgradeAxis.Tame),
                        ArenaTerms.ExpandedUpgradeDescription(WeaponForm.SporeLauncher, 0, UpgradeAxis.Twist),
                        ArenaTerms.ExpandedUpgradeDescription(WeaponForm.SporeLauncher, 1, UpgradeAxis.Tame),
                        ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Railgun, 0, UpgradeAxis.Tame),
                        ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Railgun, 1, UpgradeAxis.Tame),
                    };
                }
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                Object.Destroy(locale.gameObject);
            }

            string[] zh = texts["zh-Hans"], en = texts["en"];
            foreach (var text in zh.Concat(en)) Assert.That(text, Does.Not.Contain("{").And.Not.Empty, "every number is filled in");
            Assert.That(zh[0], Does.Contain("cone ahead of your aim").And.Contain("never through a wall").And.Contain("does not discharge"), "Arc Jump");
            Assert.That(en[0], Does.Contain("cone ahead of your aim").And.Contain("never through a wall").And.Contain("does not discharge"));
            Assert.That(zh[1], Does.Contain("in your hand").And.Contain("enemies close by").And.Contain("full charge hits harder"), "Capacitor");
            Assert.That(en[1], Does.Contain("in your hand").And.Contain("enemies close by").And.Contain("full charge hits harder"));
            Assert.That(zh[2], Does.Contain("reload").And.Contain("mist").And.Not.Contain("costs you HP"), "Needler trait: the reload mist");
            Assert.That(en[2], Does.Contain("reload").And.Contain("mist"));
            Assert.That(zh[3], Does.Contain("rail beam").And.Contain("Shield's damage cut"), "Lance: a rail beam");
            Assert.That(en[3], Does.Contain("rail beam"));
            Assert.That(zh[4], Does.Contain("BLACK HOLE").And.Contain("does not hurt"), "Lance trait: the push can carry you into a black hole");
            Assert.That(en[4], Does.Contain("black hole").And.Contain("does not hurt"));
            Assert.That(zh[5], Does.Contain("cooldown back").And.Contain("next shot").And.Not.Contain("threshold"), "Execute: the kill reward, no growing line");
            Assert.That(en[5], Does.Contain("cooldown back").And.Contain("next shot").And.Not.Contain("threshold"));
            Assert.That(zh[6], Does.Contain("shocks you").And.Contain("long cooldown").And.Contain("Boss").And.Not.Contain("Max HP"), "Execute trait");
            Assert.That(en[6], Does.Contain("shocks you").And.Contain("long cooldown"));
            string speed = ((tuning.chainTameMoveSpeed - 1f) * 100f).ToString("0.#");
            Assert.That(zh[7], Does.Contain($"{speed}%").And.Contain($"up to +{tuning.chainStoreMax}"), "Arc Jump Tame: the cap stays and the speed-up is new");
            Assert.That(en[7], Does.Contain($"{speed}% faster"));
            Assert.That(zh[8], Does.Contain("detonates").And.Contain(""), "Cloud Twist");
            Assert.That(en[8], Does.Contain("detonates"));
            Assert.That(zh[9], Does.Contain("reload").And.Contain("no longer hurts you"), "Needler Tame");
            Assert.That(en[9], Does.Contain("reload").And.Contain("no longer hurts you"));
            Assert.That(zh[10], Does.Contain("wind-up still slows you"), "Lance Tame keeps the wind-up slow");
            Assert.That(en[10], Does.Contain("wind-up still slows you"));
            Assert.That(zh[11], Does.Contain("arc shield").And.Contain("blocks no damage").And.Not.Contain("Max HP"), "Execute Tame: the arc shield");
            Assert.That(en[11], Does.Contain("arc shield").And.Contain("blocks no damage"));
        }
    }
}
