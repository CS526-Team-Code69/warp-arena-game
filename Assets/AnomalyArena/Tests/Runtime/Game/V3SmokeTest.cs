using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            await ClearSurvivors(game);
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
            ((RectTransform)hud.transform).sizeDelta = hud.GetComponentInParent<CanvasScaler>().referenceResolution;
            var slots = hud.handSlots.Concat(hud.autoSlots).Concat(hud.skillSlots).Where(slot => slot).ToArray();
            Assert.That(slots, Has.Length.EqualTo(8), "two hand, three auto and three skill slots");
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                Rect box = Bounds(slot.transform, hud.transform);
                for (int j = i + 1; j < slots.Length; j++)
                    Assert.That(box.Overlaps(Bounds(slots[j].transform, hud.transform)), Is.False, $"{slot.name} overlaps {slots[j].name}");
                AssertPartsKeepToTheirBoxes(slot, hud);
            }
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(25000)]
        public async Task BottomRow_ChineseSlotParts_StayInsideTheirOwnBoxes()
        {
            await ReadyScene();
            AssumeChineseEdition();
            var hud = Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            foreach (var slot in hud.handSlots.Concat(hud.autoSlots).Concat(hud.skillSlots)) AssertPartsKeepToTheirBoxes(slot, hud);
        }

        static void AssumeChineseEdition() =>
            Assume.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, Is.EqualTo(V2Hud.ChineseEditionScene), "the Chinese edition's boxes");

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(25000)]
        public async Task BottomRow_ChineseNames_ShowInFullInEverySlot()
        {
            await ReadyScene();
            AssumeChineseEdition();
            var hud = Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            string empty = ArenaText.Pair("Empty slot","Empty slot");
            var weapons = new List<(string name, bool inHand, bool inAuto)> { (empty, true, true) };
            foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
                foreach (int mk in new[] { 1, 6 })
                {
                    bool auto = new RunWeapon { form = form }.IsAuto;
                    weapons.Add((new RunWeapon { form = form, mk = mk }.Label, !auto, auto));
                    for (int variant = 0; variant < 2; variant++) weapons.Add((new RunWeapon { form = form, variant = variant, revealed = true, mk = mk }.Label, !auto, auto));
                }
            var skills = new List<string> { ArenaText.Pair("Empty","Empty") };
            foreach (SkillId id in Enum.GetValues(typeof(SkillId))) skills.Add(ArenaTerms.Skill(id));

            var cutOff = new List<string>();
            foreach (var view in hud.handSlots) foreach (var weapon in weapons) CollectCutOffName(hud, view, weapon.name, weapon.inHand, cutOff);
            foreach (var view in hud.autoSlots) foreach (var weapon in weapons) CollectCutOffName(hud, view, weapon.name, weapon.inAuto, cutOff);
            foreach (var view in hud.skillSlots) foreach (var name in skills) CollectCutOffName(hud, view, name, true, cutOff);
            Assert.That(cutOff, Is.Empty, $"{cutOff.Count} names are cut off, first ones: {string.Join("; ", cutOff.Take(12))}");

            var noticed = new List<string>();
            CollectCutOffName(hud, hud.autoSlots[0], new string('M', 60), false, noticed);
            Assert.That(noticed, Is.Not.Empty, "a name too long for the box counts as cut off");
            noticed.Clear();
            CollectCutOffName(hud, hud.autoSlots[0], new string('M', 18), false, noticed);
            Assert.That(noticed, Has.Some.Contains("split word True"), "a name broken inside a word counts too");
            noticed.Clear();
            CollectCutOffName(hud, hud.autoSlots[0], new string('M', 8) + " " + new string('M', 8), true, noticed);
            Assert.That(noticed, Is.Not.Empty, "a name that belongs in this slot kind has to keep to the lines the slot asks for");
            noticed.Clear();
            CollectCutOffName(hud, hud.autoSlots[0], new string('M', 8) + " " + new string('M', 8), false, noticed);
            Assert.That(noticed, Is.Empty, "a name from another slot kind may wrap at a space, as long as it shows in full");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(25000)]
        public async Task BottomRow_ChineseDetails_ShowInFullInEverySlot()
        {
            var game = await ReadyScene();
            AssumeChineseEdition();
            var hud = Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            var weapons = game.runWeapons;
            typeof(RunWeapons).GetField("meltdownHasteLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapons, 4.2f);
            var cutOff = new List<string>();
            foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
                for (int variant = 0; variant < 2; variant++)
                    for (int moment = 0; moment < 3; moment++)
                    {
                        var weapon = new RunWeapon { form = form, variant = variant, revealed = true, mk = 6 };
                        if (moment > 0)
                        {
                            if (!weapon.IsAuto) weapon.reload = 1.2f;
                            if (form == WeaponForm.Minigun && variant == 0) weapon.turretLeft = 3.4f;
                            weapon.chainStoredJumps = 9; weapon.knifeCaught = true; weapon.executeNextShot = .5f; weapon.executeAuraLeft = 3.2f; weapon.heat = 4f; weapon.charge = 99f;
                        }
                        if (moment == 1) { weapon.droneDownLeft = 3.2f; weapon.drainOverloadLeft = 3.2f; }
                        if (moment == 2) weapon.drainShieldUp = true;
                        var held = weapon.IsAuto ? weapons.Auto : weapons.Hand;
                        held[1] = weapon;
                        var snapshot = game.UiSnapshot;
                        CollectCutOffDetail(hud, weapon.IsAuto ? hud.autoSlots[1] : hud.handSlots[1], weapon.IsAuto ? snapshot.autoSlots[1] : snapshot.handSlots[1], cutOff);
                        held[1] = null;
                    }
            foreach (SkillId id in Enum.GetValues(typeof(SkillId)))
                foreach (int level in new[] { 1, SkillDefinition.MaxLevel })
                {
                    game.skills.ResetRun();
                    Assume.That(game.skills.Learn(id, -1, level), Is.True, "the skill can be learned");
                    var value = game.UiSnapshot.skillSlots[0];
                    CollectCutOffDetail(hud, hud.skillSlots[0], value, cutOff);
                    if (!hud.skillSlots[0].detail.text.Contains(ArenaText.Pair("Energy","Energy"))) cutOff.Add($"{id} level {level} lost its cost: {hud.skillSlots[0].detail.text}");
                }
            Assert.That(cutOff, Is.Empty, $"{cutOff.Count} detail lines are cut off, first ones: {string.Join("; ", cutOff.Take(12))}");

            weapons.Hand[1] = new RunWeapon { form = WeaponForm.Rifle, revealed = true };
            weapons.Auto[1] = new RunWeapon { form = WeaponForm.OrbitBlades, revealed = true };
            var plain = game.UiSnapshot;
            hud.handSlots[1].Render(plain.handSlots[1], hud.art);
            hud.autoSlots[1].Render(plain.autoSlots[1], hud.art);
            string whole = ArenaText.Pair("Common Lv 1 None","Common Lv 1 None");
            Assert.That(hud.handSlots[1].detail.text, Is.EqualTo(whole), "a hand slot shows all three");
            Assert.That(hud.autoSlots[1].detail.text, Is.EqualTo(whole), "an auto slot shows all three");

            game.skills.ResetRun();
            Assume.That(game.skills.Learn(SkillId.GravityWell), Is.True, "a skill with a level");
            hud.skillSlots[0].Render(game.UiSnapshot.skillSlots[0], hud.art);
            Assert.That(hud.skillSlots[0].detail.text, Does.Contain("1  "), "the gap between two numbers is wider than a space");
        }

        static void CollectCutOffDetail(V2Hud hud, V2UiSlot view, ArenaUiSlot value, List<string> cutOff)
        {
            view.Render(value, hud.art);
            string shown = view.detail.text;
            string now = value.detail.Split(" · ").Last().Replace(" ", "");
            if (!shown.Replace(" ", "").EndsWith(now, StringComparison.Ordinal)) cutOff.Add($"{view.name}: \"{value.detail}\" lost its last part: \"{shown}\"");
            foreach (float scale in CanvasScales)
            {
                var generator = Generate(view.detail, scale);
                if (generator.characterCountVisible < shown.Length)
                    cutOff.Add($"{view.name} ({view.detail.rectTransform.rect.width:0}x{view.detail.rectTransform.rect.height:0}) scale {scale:0.00} size {generator.fontSizeUsedForBestFit}: {shown} ({generator.characterCountVisible}/{shown.Length} characters, from \"{value.detail}\")");
            }
        }

        static readonly float[] CanvasScales = Enumerable.Range(0, 31).Select(step => .5f + step * .05f).ToArray();

        static TextGenerator Generate(Text label, float scale)
        {
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.scaleFactor = scale;
            var generator = new TextGenerator();
            generator.Populate(label.text, settings);
            return generator;
        }

        static void CollectCutOffName(V2Hud hud, V2UiSlot view, string name, bool holdsThisName, List<string> cutOff)
        {
            view.Render(new ArenaUiSlot { title = name }, hud.art);
            string shown = view.title.text;
            int asked = shown.Count(c => c == '\n') + 1;
            foreach (float scale in CanvasScales)
            {
                var generator = Generate(view.title, scale);
                bool splitsWord = generator.GetLinesArray().Any(line => line.startCharIdx > 0 && line.startCharIdx < shown.Length && shown[line.startCharIdx - 1] != '\n' && shown[line.startCharIdx - 1] != ' ');
                if (generator.characterCountVisible < shown.Length || generator.lineCount > 2 || splitsWord || holdsThisName && generator.lineCount != asked)
                    cutOff.Add($"{view.name} ({view.title.rectTransform.rect.width:0}x{view.title.rectTransform.rect.height:0}) scale {scale:0.00} size {generator.fontSizeUsedForBestFit}: {shown.Replace('\n', '/')} ({generator.characterCountVisible}/{shown.Length} characters, {generator.lineCount} lines, split word {splitsWord})");
            }
        }

        static void AssertPartsKeepToTheirBoxes(V2UiSlot slot, V2Hud hud)
        {
            Rect box = Bounds(slot.transform, hud.transform);
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

        static Rect Bounds(Transform part, Transform space)
        {
            var corners = new Vector3[4];
            ((RectTransform)part).GetWorldCorners(corners);
            Vector2 a = space.InverseTransformPoint(corners[0]), b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(25000)]
        public async Task GainXp_TrueLevelsAndPostCapDrafts_RefillHpAndKeepManaCardBudget()
        {
            var game = await ReadyScene();
            var progression = game.progression;
            game.player.ReceiveDamage(new DamageInfo { amount = 5f, source = DamageSource.Poison, ignoreShield = true });

            progression.GainXp(progression.Rules.levelCosts[0]);

            Assert.That(progression.Level, Is.EqualTo(2), "level two");
            Assert.That(game.player.maxHp, Is.EqualTo(30f), "level-two max HP");
            Assert.That(game.player.Hp, Is.EqualTo(30f), "a level-up restores HP to full, the earlier deficit included");
            Assert.That(progression.Stats.maxEnergy, Is.EqualTo(106f), "capacity grows before first skill");
            Assert.That(progression.EnergyUnlocked, Is.False, "levelling alone does not unlock skills");

            progression.UnlockEnergy();
            progression.ApplyStat(new StatCardOffer(StatId.MaxEnergy, CardRarity.Common, 1000f));
            Assert.That(progression.TrySpendEnergy(40f), Is.True, "spend some Energy before levelling");
            game.player.ReceiveDamage(new DamageInfo { amount = 10f, source = DamageSource.Poison, ignoreShield = true });

            progression.GainXp(progression.Rules.levelCosts.Skip(1).Sum());
            Assert.That(progression.Level, Is.EqualTo(12), "level cap");
            Assert.That(game.player.maxHp, Is.EqualTo(80f), "max-level HP without HP cards");
            Assert.That(game.player.Hp, Is.EqualTo(80f), "HP is full again after the level-ups");
            Assert.That(progression.Stats.maxEnergy, Is.EqualTo(316f), "level growth retains original 150 mana card budget");
            Assert.That(progression.Energy, Is.EqualTo(316f), "unlocked mana refills on true level-up");
            int before = progression.BankedDrafts;

            progression.GainXp(progression.Rules.xpPerStatDraft);

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
        public async Task TakeOffer_HeldWeaponVersion_UpgradesTheHeldWeaponInPlace()
        {
            var game = await ReadyScene();
            var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, axes = UpgradeAxis.Twist | UpgradeAxis.Infusion, element = ElementKind.Fire, revealed = true };
            game.runWeapons.Hand[0] = rifle;
            game.runWeapons.Hand[1] = null;
            RunWeapon version = null;
            for (int draw = 0; draw < 200 && version == null; draw++)
            {
                game.runWeapons.RollArmory(5);
                version = game.runWeapons.Offers.FirstOrDefault(w => w != null && w.upgradeOf == rifle);
            }
            Assert.That(version, Is.Not.Null, "held rifle offered as a stronger version");
            int index = System.Array.IndexOf(game.runWeapons.Offers, version);
            int target = game.runWeapons.UpgradeTargetMk(new RunWeapon { mk = 1 }, 5);

            Assert.That(game.runWeapons.TakeOffer(index), Is.True);

            Assert.That(game.runWeapons.Hand[0], Is.SameAs(rifle), "the held rifle stays in its slot");
            Assert.That(rifle.mk, Is.EqualTo(version.mk).And.InRange(target, target + 1),
                "its Mk rises to the offer's: one tier above the wave standard, or one more with the wave's bonus chance");
            Assert.That(rifle.form, Is.EqualTo(WeaponForm.Rifle), "same form");
            Assert.That(rifle.variant, Is.Zero, "same pairing");
            Assert.That(rifle.axes, Is.EqualTo(UpgradeAxis.Twist | UpgradeAxis.Infusion), "its upgrades stay");
            Assert.That(rifle.element, Is.EqualTo(ElementKind.Fire), "and so does its infusion");
            Assert.That(rifle.revealed, Is.True, "nothing to reveal again");
            Assert.That(game.runWeapons.Hand[1], Is.Null, "no other slot is filled");
            Assert.That(game.runWeapons.Offers, Has.All.Null, "the armory is spent");
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

        internal static async Task ClearSurvivors(GameManager game, float timeout = 15f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (game.waves.Phase == WavePhase.WaveActive && (game.waves.LivingCount > 0 || game.waves.PendingCount > 0))
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Timed out clearing the survivors: {game.waves.LivingCount} alive, {game.waves.PendingCount} queued; phase={game.RunPhase}");
                foreach (var enemy in game.waves.Alive.ToArray())
                {
                    if (!enemy || !enemy.IsAlive) continue;
                    enemy.SuppressRewards = true;
                    enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                }
                await Awaitable.NextFrameAsync();
            }
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
                // A player skips the how-to-play pages that open as the run enters the arena.
                if (V2Tutorial.IsOpen) { V2Tutorial.Close(); blocker = "how-to-play pages"; }
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
