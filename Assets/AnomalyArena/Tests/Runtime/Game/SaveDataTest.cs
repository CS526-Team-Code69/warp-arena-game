using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnomalyArena
{
    /// <summary>The save slots without a scene (ArenaSaves, SaveData). A save keeps every part through its JSON; each mode has three
    /// slots, apart from each other and from the other mode's; the summaries show what each slot holds, an empty slot only its mode and
    /// number; a won run's slot is marked cleared and cannot be continued; a deleted slot leaves nothing behind, so a new save there
    /// starts clean; text that cannot be read, or a save of a later version, reads as an empty slot. The tests keep the saves in memory
    /// (TestSavedSettings switches ArenaPrefs to memory for the whole run, and the saves follow it): no player's saves are read or
    /// touched.</summary>
    [TestFixture]
    public class SaveDataTest
    {
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
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void StoreThenLoad_KeepsEveryPartOfAStorySave()
        {
            var save = StorySample();

            Assert.That(ArenaSaves.Store(SaveMode.Story, 1, save), Is.True, "the save is written");
            var back = ArenaSaves.Load(SaveMode.Story, 1);

            Assert.That(back, Is.Not.Null, "and read back");
            Assert.That(back.version, Is.EqualTo(SaveData.CurrentVersion), "the version");
            var s = back.summary;
            Assert.That((s.mode, s.slot, s.empty, s.cleared, s.day, s.phase), Is.EqualTo((SaveMode.Story, 1, false, false, 4, SavePhase.Night)), "the summary's slot and day");
            Assert.That((s.engineFixed, s.armorFixed, s.fuel, s.fuelMax, s.mapFamily), Is.EqualTo((true, false, 37, 100, 1)), "the summary's ship and map");
            Assert.That(s.weapons, Is.EqualTo(new[] { WeaponForm.Pistol, WeaponForm.Railgun, WeaponForm.OrbitBlades }), "the summary's weapons");

            var r = back.run;
            Assert.That((r.level, r.xp, r.energy, r.energyUnlocked, r.voidEnergy, r.voidBadge), Is.EqualTo((3, 12.5f, 40f, true, false, true)), "the progression");
            Assert.That((r.stats.maxHp, r.stats.moveSpeed, r.stats.caps[(int)StatId.MaxEnergy]), Is.EqualTo((88f, 7.5f, 300f)), "the stats, caps included");
            Assert.That((r.statCardPity, r.draftWave, r.skillCardPity), Is.EqualTo((2, 5, 3)), "the pity counters and the draws' wave");
            Assert.That(r.bankedPicks, Is.EqualTo(new[] { false, true }), "the picks banked");
            Assert.That(r.skills, Is.EqualTo(new[] { (int)SkillId.Blink, -1, (int)SkillId.PocketVoid }), "the skills by slot");
            Assert.That(r.skillLevels, Is.EqualTo(new[] { 4, 1, 1 }), "their levels");
            var railgun = r.hand[0];
            Assert.That((railgun.held, railgun.form, railgun.variant, railgun.revealed, railgun.axes, railgun.mk, railgun.element, railgun.builtInElement, railgun.voidEnchant),
                Is.EqualTo((true, WeaponForm.Railgun, 1, true, UpgradeAxis.Tame | UpgradeAxis.Infusion, 6, ElementKind.Frost, false, true)), "a weapon with all it carries");
            Assert.That((r.hand[1].held, r.auto[0].held, r.auto[0].form, r.auto[1].held), Is.EqualTo((false, true, WeaponForm.OrbitBlades, false)), "the empty slots stay empty");
            Assert.That((r.activeHand, r.hp, r.stamina), Is.EqualTo((0, 61.5f, .25f)), "the player");
            Assert.That((r.waveIndex, r.kills, r.elapsed, r.mapIndex, r.trialWon, r.holeIntroDone), Is.EqualTo((7, 321, 444f, 3, true, true)), "the waves and the map");

            var st = back.story;
            Assert.That((st.day, st.phase, st.overtime, st.mapSeed), Is.EqualTo((4, SavePhase.Night, false, 123456789)), "the day and the terrain's seed");
            Assert.That((st.alloy, st.energy, st.dark, st.earnedAlloy, st.earnedEnergy, st.earnedDark), Is.EqualTo((10, 20, 30, 110, 120, 130)), "the currencies");
            Assert.That((st.lampStock, st.voidEnergy, st.voidAlloy, st.prophetBeaten, st.hasVoidOreCentre), Is.EqualTo((2, 5, 6, true, true)), "the lamps and the void wallet");
            Assert.That(st.voidOreCentre, Is.EqualTo(new Vector3(60f, 0f, 40f)), "where the void alloy grows");
            Assert.That(st.roomLevels, Is.EqualTo(new[] { 1, 2, 3, 0 }), "the rooms' levels");
            Assert.That(st.roomDamaged, Is.EqualTo(new[] { false, true, false, false }), "the broken room");
            Assert.That((st.durability, st.shieldLevel, st.baseHp, st.baseShield), Is.EqualTo((2, 1, 333.5f, 40f)), "the base");
            Assert.That((st.newWeaponsBought, st.teamMk, st.shipOreFromDay, st.overloadDamageBuys), Is.EqualTo((2, 4, 3, 1)), "the purchase counters");
            Assert.That(st.upgradesBought, Is.EqualTo(new[] { 1, 0, 2, 0, 0 }), "the upgrades bought");
            Assert.That(st.researchBought, Is.EqualTo(new[] { 0, 1, 0 }), "the research tiers bought");
            Assert.That(st.statsBoughtStat.Zip(st.statsBoughtRarity, (stat, rarity) => ((StatId)stat, (CardRarity)rarity)),
                Is.EqualTo(new[] { (StatId.Damage, CardRarity.Epic), (StatId.MaxHp, CardRarity.Common) }), "the body items bought");
            Assert.That(st.crateKinds, Is.EqualTo(new[] { 2 }), "the crate on the map");
            Assert.That(st.cratePositions, Is.EqualTo(new[] { new Vector3(12.5f, 0f, -30.25f) }), "and where it lies");
            Assert.That((st.cratesOpened, st.changedWeaponSaid, st.switchTaught), Is.EqualTo((2, true, true)), "the lines and hints given");
            Assert.That((st.holeSeen, st.holeAwake, st.holeFed, st.holeCheckDay), Is.EqualTo((true, true, 10, 4)), "the black hole's side quest");
            Assert.That((st.checkState, st.checkRequired, st.checkSeconds, st.checkDealt, st.checkElapsed), Is.EqualTo((HoleCheckState.Running, 900f, 30f, 450f, 12f)), "the day's timed check");
            Assert.That((st.hole.phase, st.hole.openedWave, st.hole.wavesLeft), Is.EqualTo((HolePhase.Portal, 3, 2)), "the hole");
            Assert.That((st.hasTrailTarget, st.trailTarget), Is.EqualTo((true, new Vector3(70f, 0f, 45f))), "where the ore trail heads");
            Assert.That(st.killRegion.Zip(st.killCount, (region, kills) => (region, kills)), Is.EqualTo(new[] { (5, 12), (9, 3) }), "the morning's kills by region");
            Assert.That(st.kindRegion.Zip(st.kindKill, (region, kind) => (region, (EnemyKind)kind)),
                Is.EqualTo(new[] { (5, EnemyKind.Grunt), (9, EnemyKind.Grunt) }), "the kinds killed by region");
        }

        [Test]
        public void StoreThenLoad_KeepsTheOpenModesHoles()
        {
            var save = new SaveData();
            save.run.waveIndex = 5;
            save.open.holes = new[]
            {
                new HoleSave { position = new Vector3(-20f, 0f, 14f), feeds = 17, phase = HolePhase.Active },
                new HoleSave { position = new Vector3(22f, 0f, -9f), feeds = 40, phase = HolePhase.Portal, openedWave = 5, wavesLeft = 2 },
            };

            ArenaSaves.Store(SaveMode.Open, 0, save);
            var holes = ArenaSaves.Load(SaveMode.Open, 0).open.holes;

            Assert.That(holes.Select(h => (h.position, h.feeds, h.phase, h.openedWave, h.wavesLeft)), Is.EqualTo(new[]
            {
                (new Vector3(-20f, 0f, 14f), 17, HolePhase.Active, 0, 3),
                (new Vector3(22f, 0f, -9f), 40, HolePhase.Portal, 5, 2),
            }), "both holes where they stood, with their feeds and phases");
        }

        [Test]
        public void ThreeSlotsPerMode_KeepApart_FromEachOtherAndFromTheOtherMode()
        {
            for (int slot = 0; slot < ArenaSaves.SlotCount; slot++) ArenaSaves.Store(SaveMode.Open, slot, OpenSample(wave: 3 + slot));

            ArenaSaves.Delete(SaveMode.Open, 1);

            var open = ArenaSaves.List(SaveMode.Open);
            Assert.That(open.Select(s => (s.slot, s.empty, s.wave)), Is.EqualTo(new[] { (0, false, 3), (1, true, 0), (2, false, 5) }),
                "deleting one slot leaves the other two as they were");
            Assert.That(ArenaSaves.List(SaveMode.Story).Select(s => s.empty), Is.All.True, "the story's slots hold nothing of the open mode's");
        }

        [Test]
        public void List_EmptySlots_CarryOnlyTheirModeAndNumber()
        {
            var list = ArenaSaves.List(SaveMode.Story);

            Assert.That(list, Has.Length.EqualTo(ArenaSaves.SlotCount), "always three slots");
            Assert.That(list.Select(s => (s.mode, s.slot, s.empty, s.cleared, s.day, s.wave)), Is.EqualTo(new[]
            {
                (SaveMode.Story, 0, true, false, 0, 0), (SaveMode.Story, 1, true, false, 0, 0), (SaveMode.Story, 2, true, false, 0, 0),
            }), "each empty, in order");
            Assert.That(list.Select(s => s.weapons.Length), Is.All.Zero, "with no weapons");
        }

        [Test]
        public void List_ShowsTheSummary_UnderTheSlotItWasStoredIn()
        {
            var save = OpenSample(wave: 7);
            save.summary.mode = SaveMode.Story; // a summary that says otherwise is set right as it is stored
            save.summary.slot = 0;
            long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            ArenaSaves.Store(SaveMode.Open, 2, save);
            var shown = ArenaSaves.List(SaveMode.Open)[2];

            Assert.That((shown.mode, shown.slot, shown.empty, shown.wave, shown.waveCount, shown.mapFamily), Is.EqualTo((SaveMode.Open, 2, false, 7, 15, 0)),
                "the slot's own mode and number, and what the save summarises");
            Assert.That(shown.weapons, Is.EqualTo(new[] { WeaponForm.Rifle, WeaponForm.SentryDrone }), "the weapons held");
            Assert.That(shown.savedAtUnixSeconds, Is.InRange(before, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), "when it was written");
        }

        [Test]
        public void Changed_IsRaisedAfterEveryWriteAndDelete()
        {
            int raised = 0;
            void Count() => raised++;
            ArenaSaves.Changed += Count;
            try
            {
                ArenaSaves.Store(SaveMode.Story, 0, StorySample());
                ArenaSaves.MarkCleared(SaveMode.Story, 0);
                ArenaSaves.Delete(SaveMode.Story, 0);
            }
            finally { ArenaSaves.Changed -= Count; }

            Assert.That(raised, Is.EqualTo(3), "a write, the cleared mark (a write) and a delete");
        }

        [Test]
        public void AWonRun_KeepsItsSlotMarkedCleared_WhichCannotBeContinued()
        {
            ArenaSaves.Store(SaveMode.Open, 2, OpenSample(wave: 14));
            Assume.That(ArenaSaves.CanContinue(SaveMode.Open, 2), Is.True, "a save can be continued");

            Assert.That(ArenaSaves.MarkCleared(SaveMode.Open, 2, summary => summary.wave = 15), Is.True, "the won run's slot is marked");

            var shown = ArenaSaves.List(SaveMode.Open)[2];
            Assert.That((shown.empty, shown.cleared, shown.wave), Is.EqualTo((false, true, 15)), "the slot is kept, cleared, showing how the run finished");
            Assert.That(shown.weapons, Is.EqualTo(new[] { WeaponForm.Rifle, WeaponForm.SentryDrone }), "what the finish did not change stays");
            Assert.That(ArenaSaves.Load(SaveMode.Open, 2).run.waveIndex, Is.EqualTo(12), "the save itself is kept");
            Assert.That(ArenaSaves.CanContinue(SaveMode.Open, 2), Is.False, "a cleared slot cannot be continued");
            Assert.That(ArenaSaves.CanContinue(SaveMode.Open, 0), Is.False, "nor can an empty one");
        }

        [Test]
        public void DeletingAFilledSlot_LeavesNothing_SoANewSaveThereStartsClean()
        {
            var old = StorySample();
            ArenaSaves.Store(SaveMode.Story, 0, old);
            ArenaSaves.MarkCleared(SaveMode.Story, 0);
            Assume.That(ArenaSaves.HasEntryForTests(SaveMode.Story, 0), Is.True, "the old, cleared save fills the slot");

            ArenaSaves.Delete(SaveMode.Story, 0);

            Assert.That((ArenaSaves.HasEntryForTests(SaveMode.Story, 0), ArenaSaves.ReadRawForTests(SaveMode.Story, 0), ArenaSaves.Load(SaveMode.Story, 0)),
                Is.EqualTo((false, (string)null, (SaveData)null)), "the store holds no entry for the slot any more");
            Assert.That(ArenaSaves.List(SaveMode.Story)[0].empty, Is.True, "the slot shows empty");

            var fresh = new SaveData();
            fresh.summary.day = 1;
            fresh.summary.phase = SavePhase.Morning;
            fresh.summary.weapons = new[] { WeaponForm.Pistol };
            ArenaSaves.Store(SaveMode.Story, 0, fresh);

            var shown = ArenaSaves.List(SaveMode.Story)[0];
            Assert.That((shown.empty, shown.cleared, shown.day, shown.phase), Is.EqualTo((false, false, 1, SavePhase.Morning)), "the new run's save, not cleared");
            Assert.That(shown.weapons, Is.EqualTo(new[] { WeaponForm.Pistol }), "with nothing of the old one");
            Assert.That(ArenaSaves.Load(SaveMode.Story, 0).story.mapSeed, Is.Zero, "not even the old map");
        }

        [Test]
        public void TextThatCannotBeRead_OrALaterVersion_ReadsAsAnEmptySlot()
        {
            // Broken text is reported as a warning; should Unity's JSON reader log its own error too, that is this test's input, not a fault.
            LogAssert.ignoreFailingMessages = true;
            ArenaSaves.WriteRawForTests(SaveMode.Open, 0, "{ this is not a save");
            var later = OpenSample(wave: 4);
            later.version = SaveData.CurrentVersion + 1;
            later.summary.mode = SaveMode.Open;
            ArenaSaves.WriteRawForTests(SaveMode.Open, 1, JsonUtility.ToJson(later));
            ArenaSaves.WriteRawForTests(SaveMode.Open, 2, JsonUtility.ToJson(StorySample())); // the story's save in an open-mode slot

            Assert.That(ArenaSaves.List(SaveMode.Open).Select(s => s.empty), Is.All.True, "none of them is shown as a save");
            Assert.That(Enumerable.Range(0, ArenaSaves.SlotCount).Select(slot => ArenaSaves.CanContinue(SaveMode.Open, slot)), Is.All.False, "nor can be continued");
        }

        [Test]
        public void Bind_BindsOneSlotPerMode_AndASlotOutOfRangeBindsNone()
        {
            ArenaSaves.Bind(SaveMode.Story, 2);
            Assert.That((ArenaSaves.BoundSlot(SaveMode.Story), ArenaSaves.BoundSlot(SaveMode.Open)), Is.EqualTo((2, -1)), "the story's next run writes into slot 3");

            ArenaSaves.Bind(SaveMode.Open, 0);
            Assert.That((ArenaSaves.BoundSlot(SaveMode.Story), ArenaSaves.BoundSlot(SaveMode.Open)), Is.EqualTo((2, 0)), "each mode keeps its own");

            ArenaSaves.Bind(SaveMode.Story, ArenaSaves.SlotCount);
            Assert.That(ArenaSaves.BoundSlot(SaveMode.Story), Is.EqualTo(-1), "a slot out of range binds none");
        }

        [Test]
        public void Numbering_TheStorysDayAndTheOpenModesWave()
        {
            Assert.That(ArenaSaves.StoryDay(0, SavePhase.Morning), Is.EqualTo(1), "the first save, as Day 0's night ends, is the start of day 1");
            Assert.That(ArenaSaves.StoryDay(3, SavePhase.Morning), Is.EqualTo(4), "a morning belongs to the day it begins");
            Assert.That((ArenaSaves.StoryDay(3, SavePhase.Afternoon), ArenaSaves.StoryDay(3, SavePhase.Night)), Is.EqualTo((3, 3)), "afternoons and nights to their own day");
            Assert.That(ArenaSaves.OpenWave(-1), Is.EqualTo(1), "before the first wave: wave 1");
            Assert.That(ArenaSaves.OpenWave(0), Is.EqualTo(2), "after the first: wave 2");
            Assert.That(ArenaSaves.OpenWave(13), Is.EqualTo(15), "after the 14th ordinary wave: the Tyrant's, the 15th");
        }

        // ───── Samples ─────

        static SaveData StorySample()
        {
            var save = new SaveData();
            var s = save.summary;
            s.day = 4; s.phase = SavePhase.Night; s.engineFixed = true; s.fuel = 37; s.fuelMax = 100; s.mapFamily = 1;
            s.weapons = new[] { WeaponForm.Pistol, WeaponForm.Railgun, WeaponForm.OrbitBlades };

            var r = save.run;
            r.level = 3; r.xp = 12.5f; r.energy = 40f; r.energyUnlocked = true; r.voidBadge = true;
            r.stats.maxHp = 88f; r.stats.moveSpeed = 7.5f; r.stats.caps[(int)StatId.MaxEnergy] = 300f;
            r.statCardPity = 2; r.draftWave = 5; r.skillCardPity = 3; r.bankedPicks = new[] { false, true };
            r.skills = new[] { (int)SkillId.Blink, -1, (int)SkillId.PocketVoid };
            r.skillLevels = new[] { 4, 1, 1 };
            var railgun = new RunWeapon { form = WeaponForm.Railgun, variant = 1, revealed = true, axes = UpgradeAxis.Tame | UpgradeAxis.Infusion, mk = 6, element = ElementKind.Frost };
            r.hand = new[] { WeaponSave.Of(railgun, true), WeaponSave.Of(null) };
            r.auto = new[] { WeaponSave.Of(new RunWeapon { form = WeaponForm.OrbitBlades, mk = 6 }), WeaponSave.Of(null) };
            r.activeHand = 0; r.hp = 61.5f; r.stamina = .25f;
            r.waveIndex = 7; r.kills = 321; r.elapsed = 444f; r.mapIndex = 3; r.trialWon = true; r.holeIntroDone = true;

            var st = save.story;
            st.day = 4; st.phase = SavePhase.Night; st.mapSeed = 123456789;
            st.alloy = 10; st.energy = 20; st.dark = 30; st.earnedAlloy = 110; st.earnedEnergy = 120; st.earnedDark = 130;
            st.lampStock = 2; st.voidEnergy = 5; st.voidAlloy = 6; st.prophetBeaten = true;
            st.hasVoidOreCentre = true; st.voidOreCentre = new Vector3(60f, 0f, 40f);
            st.roomLevels = new[] { 1, 2, 3, 0 }; st.roomDamaged = new[] { false, true, false, false };
            st.durability = 2; st.shieldLevel = 1; st.baseHp = 333.5f; st.baseShield = 40f;
            st.newWeaponsBought = 2; st.teamMk = 4; st.shipOreFromDay = 3; st.overloadDamageBuys = 1;
            st.upgradesBought = new[] { 1, 0, 2, 0, 0 }; st.researchBought = new[] { 0, 1, 0 };
            st.statsBoughtStat = new[] { (int)StatId.Damage, (int)StatId.MaxHp };
            st.statsBoughtRarity = new[] { (int)CardRarity.Epic, (int)CardRarity.Common };
            st.crateKinds = new[] { 2 }; st.cratePositions = new[] { new Vector3(12.5f, 0f, -30.25f) };
            st.cratesOpened = 2; st.changedWeaponSaid = true; st.switchTaught = true;
            st.holeSeen = true; st.holeAwake = true; st.holeFed = 10; st.holeCheckDay = 4;
            st.checkState = HoleCheckState.Running; st.checkRequired = 900f; st.checkSeconds = 30f; st.checkDealt = 450f; st.checkElapsed = 12f;
            st.hole = new HoleSave { position = new Vector3(70f, 0f, 45f), phase = HolePhase.Portal, openedWave = 3, wavesLeft = 2 };
            st.hasTrailTarget = true; st.trailTarget = new Vector3(70f, 0f, 45f);
            st.killRegion = new[] { 5, 9 }; st.killCount = new[] { 12, 3 };
            st.kindRegion = new[] { 5, 9 }; st.kindKill = new[] { (int)EnemyKind.Grunt, (int)EnemyKind.Grunt };
            return save;
        }

        static SaveData OpenSample(int wave)
        {
            var save = new SaveData();
            save.summary.wave = wave;
            save.summary.waveCount = 15;
            save.summary.mapFamily = 0;
            save.summary.weapons = new[] { WeaponForm.Rifle, WeaponForm.SentryDrone };
            save.run.waveIndex = wave - 2;
            return save;
        }
    }
}
