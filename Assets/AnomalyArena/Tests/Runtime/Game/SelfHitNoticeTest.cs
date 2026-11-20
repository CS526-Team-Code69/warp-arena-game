using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The player's own weapon hurting them is said where it happens: the weapon's dock slot flashes red every time, a notice names the
    /// weapon the first two times a run, the result screen's first line names what ended the run, the weapon card's trait heading says
    /// whether the trait can hurt or move the player, and a new trait's card holds a moment. In a black hole's pull the hint plate names
    /// the sprint key.
    /// </summary>
    [TestFixture]
    public class SelfHitNoticeTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const int ChineseLimit = 34, EnglishLimit = 95;

        [TearDown]
        public void TearDown()
        {
            GameManager.ShortWeaponCardHold = true;
            Time.timeScale = 1f;
        }

        [Test]
        public void WeaponCardHold_ANewTraitHoldsTwoSeconds_ExceptWhileTestsAskForTheShortLock()
        {
            GameManager.ShortWeaponCardHold = false;
            Assert.That(GameManager.WeaponCardHold(true), Is.EqualTo(GameManager.NewTraitCardSeconds));
            Assert.That(GameManager.NewTraitCardSeconds, Is.GreaterThanOrEqualTo(2f), "long enough to be read");
            Assert.That(GameManager.WeaponCardHold(false), Is.EqualTo(GameManager.CardLockSeconds), "a seen trait keeps the usual lock");
            GameManager.ShortWeaponCardHold = true;
            Assert.That(GameManager.WeaponCardHold(true), Is.EqualTo(GameManager.CardLockSeconds), "the tests keep the old 0.4 s");
        }

        [Test]
        public void TraitKinds_HurtingAndMovingTraitsAreTold()
        {
            Assert.That(WeaponGuide.HurtsYou(WeaponForm.Knife, 0), Is.True, "Flying Blade comes back at you");
            Assert.That(WeaponGuide.HurtsYou(WeaponForm.RocketLauncher, 0), Is.True, "Strike");
            Assert.That(WeaponGuide.HurtsYou(WeaponForm.RocketLauncher, 1), Is.True, "Launch Yourself slams into walls");
            Assert.That(WeaponGuide.MovesYou(WeaponForm.Knife, 1), Is.True, "Chain Blade drags you");
            Assert.That(WeaponGuide.MovesYou(WeaponForm.Minigun, 0), Is.True, "Spin-up pushes you back");
            Assert.That(WeaponGuide.HurtsYou(WeaponForm.Rifle, 0) || WeaponGuide.MovesYou(WeaponForm.Rifle, 0), Is.False, "Charged Shot does neither");
            Assert.That(WeaponGuide.HurtsYou(WeaponForm.Pistol, 0) || WeaponGuide.MovesYou(WeaponForm.Pistol, 0), Is.False, "the Pistol has no trait");
        }

        /// <summary>Every self-hit line fits the notice's second line in both languages and ends as a sentence; the Strike and Bombard
        /// cards say a wall hurts again; every cause has its result line, the black hole's naming the sprint key.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task Texts_FitAndNameTheirCause_InBothLanguages()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            bool startedChinese = ArenaText.IsChinese;
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                foreach (var code in new[] { "zh-Hans", "en" })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    int limit = chinese ? ChineseLimit : EnglishLimit;
                    var tooLong = new List<string>();
                    foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
                    for (int variant = 0; variant < 2; variant++)
                    foreach (bool wall in new[] { false, true })
                    {
                        string line = WeaponGuide.SelfHit(form, variant, wall);
                        Assert.That(line, Is.Not.Empty, $"{form} {variant} {wall}");
                        Assert.That("。.".IndexOf(line.TrimEnd().Last()) >= 0, $"{form} {variant} {wall} ends as a sentence: {line}");
                        if (line.Length > limit) tooLong.Add($"{code} {form} {variant} {wall}: {line.Length}");
                    }
                    Assert.That(WeaponGuide.SelfHit(null, 0, false).Length, Is.LessThanOrEqualTo(limit), "the general line");
                    Assert.That(tooLong, Is.Empty, "lines longer than the notice's second line");

                    string wallWord = chinese ? "撞上墙" : "wall";
                    Assert.That(WeaponGuide.Backfire(WeaponForm.RocketLauncher, 0), Does.Contain(wallWord), "the Strike card says a wall hurts again");
                    Assert.That(WeaponGuide.Backfire(WeaponForm.SwarmLauncher, 1), Does.Contain(wallWord), "the Bombard card says a wall hurts again");

                    // The stat cards name Protection as the other pages do, with no formula in the card text.
                    var protection = new StatCardOffer(StatId.Protection, CardRarity.Common, 25f);
                    Assert.That(protection.Description, Does.StartWith(ArenaTerms.Stat(StatId.Protection) + " +25").And.Not.Contain("100/("));
                    Assert.That(new StatCardOffer(StatId.CritChance, CardRarity.Common, .05f).Title, Is.EqualTo(ArenaTerms.Stat(StatId.CritChance)));

                    var causes = new Dictionary<string, string>
                    {
                        ["base"] = GameManager.ResultCauseLine("enemy", EnemyKind.Grunt, DamageSource.Hit, null, 0, false, true),
                        ["void_trial"] = GameManager.ResultCauseLine("void_trial", EnemyKind.Hollow, DamageSource.Hit, null, 0, false, false),
                        ["black_hole"] = GameManager.ResultCauseLine("black_hole", null, DamageSource.Hit, null, 0, false, false),
                        ["backfire"] = GameManager.ResultCauseLine("backfire", null, DamageSource.Hit, WeaponForm.Knife, 0, false, false),
                        ["backfire wall"] = GameManager.ResultCauseLine("backfire", null, DamageSource.Wall, WeaponForm.RocketLauncher, 0, true, false),
                        ["wall_slam"] = GameManager.ResultCauseLine("wall_slam", EnemyKind.Rammer, DamageSource.Wall, null, 0, true, false),
                        ["status"] = GameManager.ResultCauseLine("status", EnemyKind.Spitter, DamageSource.Poison, null, 0, false, false),
                        ["tyrant"] = GameManager.ResultCauseLine("tyrant", EnemyKind.Tyrant, DamageSource.Hit, null, 0, false, false),
                        ["enemy"] = GameManager.ResultCauseLine("enemy", EnemyKind.Splitter, DamageSource.Hit, null, 0, false, false),
                    };
                    foreach (var pair in causes) Assert.That(pair.Value, Is.Not.Empty, $"{code} {pair.Key}");
                    Assert.That(causes["black_hole"], Does.Contain(ArenaKeys.SprintKey), "the fall's line names the sprint key");
                    Assert.That(causes["backfire"], Does.Contain(ArenaTerms.WeaponForm(WeaponForm.Knife)).And.Contain(WeaponGuide.SelfHit(WeaponForm.Knife, 0, false)));
                    Assert.That(causes["backfire wall"], Does.Contain(WeaponGuide.SelfHit(WeaponForm.RocketLauncher, 0, true)), "the slam's own line");
                    Assert.That(causes["wall_slam"], Does.Contain(ArenaText.Text("enemy.Rammer")));
                    Assert.That(causes["enemy"], Does.Contain(ArenaCodex.MonsterNote(EnemyKind.Splitter)), "the collection's note on the monster");
                    if (!chinese) Assert.That(causes["enemy"], Does.StartWith("A Splitter ended this run."));
                    Assert.That(GameManager.ResultCauseLine("unknown", null, DamageSource.Hit, null, 0, false, false), Is.Empty, "nothing to name");
                }
            }
            finally
            {
                locale.localeCode = startedChinese ? "zh-Hans" : "en";
                locale.SelectLocale();
                UnityEngine.Object.Destroy(locale.gameObject);
            }
        }

        /// <summary>The player's own hit and a wall slam its flight started flash the weapon's slot each time and name the weapon; the notice
        /// counts once for a hit and its slam together, twice a run at most; a hit nobody's weapon dealt does neither.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task OwnWeaponHits_FlashTheSlotEveryTime_AndNameTheWeaponTwiceARun()
        {
            var game = await StartRun();
            var player = game.player;
            player.Invulnerable = true; // the hits cost HP but never end the run
            var weapon = game.runWeapons.Hand[0];
            Assert.That(weapon, Is.Not.Null, "the run starts with a hand weapon");
            var slot = game.UiSnapshot.handSlots[0];
            var shownUntil = typeof(GameManager).GetField("selfHitShownUntil", BindingFlags.Instance | BindingFlags.NonPublic);
            // The dock draws its slots ten times a second: let it bind them first, so the flash is the hit's and not a stale count.
            float bound = Time.realtimeSinceStartup + .3f;
            await Until(() => Time.realtimeSinceStartup > bound, 2f, "the dock draws its slots");
            int before = slot.selfHits;

            SelfHit(player, weapon);
            Assert.That(slot.selfHits, Is.EqualTo(before + 1), "the slot flashes for the hit");
            Assert.That(game.SelfHitNoticesShown(weapon.form, weapon.variant), Is.EqualTo(1), "the notice names the weapon");
            await Awaitable.NextFrameAsync();
            var docked = UnityEngine.Object.FindObjectsByType<V5BattleSlot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (docked.Length > 0) Assert.That(docked.Any(s => s.HurtFlashing), "the dock's slot shows the red flash");

            player.BounceFromWall(player.Position, Vector3.left, 0f, true, weapon);
            Assert.That(player.LastDamageWeapon, Is.SameAs(weapon), "a slam its flight started names the weapon");
            Assert.That(slot.selfHits, Is.EqualTo(before + 2), "the slot flashes for the slam too");
            Assert.That(game.SelfHitNoticesShown(weapon.form, weapon.variant), Is.EqualTo(1), "the slam right after the hit is the same notice");

            player.ReceiveDamage(new DamageInfo { amount = 1f, isAttack = true, knockDir = Vector3.left });
            Assert.That(slot.selfHits, Is.EqualTo(before + 2), "a hit no weapon of the player's dealt does not flash the slot");

            shownUntil.SetValue(game, -1f);
            SelfHit(player, weapon);
            Assert.That(game.SelfHitNoticesShown(weapon.form, weapon.variant), Is.EqualTo(2), "a later hit is named again");
            shownUntil.SetValue(game, -1f);
            SelfHit(player, weapon);
            Assert.That(game.SelfHitNoticesShown(weapon.form, weapon.variant), Is.EqualTo(GameManager.SelfHitNoticesPerWeapon), "twice a run at most");
            Assert.That(slot.selfHits, Is.EqualTo(before + 4), "while the slot flashes every time");
        }

        /// <summary>Standing in a black hole's ring outside the void trial, the hint plate says the hole is pulling and names the sprint key.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task HolePull_TheHintPlateNamesTheSprintKey()
        {
            var game = await StartRun();
            var world = WorldMap.Active;
            var hole = world && world.holes != null ? world.holes.FirstOrDefault(h => h && h.isActiveAndEnabled && h.Phase == HolePhase.Active && h.GravityRadius > h.KillRadius + .5f) : null;
            Assume.That(hole, Is.Not.Null, "the map has an active black hole");
            var player = game.player;
            player.Invulnerable = true;
            Vector3 home = player.Position;
            Vector3 ring = hole.Centre + Vector3.right * ((hole.KillRadius + hole.GravityRadius) * .5f);
            ring.y = home.y;
            try
            {
                Place(player, ring);
                Assume.That(world.GetPull(player.Position, player).sqrMagnitude, Is.GreaterThan(.01f), "the point is in the pull");
                var s = game.UiSnapshot;
                Assert.That(s.hintKey, Is.EqualTo(ArenaKeys.SprintKey), "the plate's keycap is the sprint key");
                Assert.That(s.hintLead + s.hintTail, Does.Contain(ArenaText.IsChinese ? "黑洞" : "black hole"));
            }
            finally { Place(player, home); }
            Assert.That(game.UiSnapshot.hintKey, Is.Not.EqualTo(ArenaKeys.SprintKey), "out of the pull the prompt goes");
        }

        // ───── Helpers ─────

        static void SelfHit(PlayerController player, RunWeapon weapon)
        {
            var info = DamageInfo.Attack(2f, player);
            info.weapon = weapon;
            player.ReceiveDamage(info);
        }

        static void Place(Combatant body, Vector3 at)
        {
            body.CancelDisplacement();
            body.GetComponent<Rigidbody>().position = at;
            body.transform.position = at;
        }

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
