using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The afternoon a story slot keeps beside its latest save, without a scene (ArenaSaves). A story save of an afternoon's
    /// start is written twice: as the slot's latest save and as its afternoon entry, which the night's and the morning's saves that
    /// follow leave alone. A slot whose latest save is a morning's offers that entry (AfternoonBefore) only when it is the afternoon
    /// of the day before on the same map and terrain; day 1's morning, a night's, an afternoon's or the final battle's save, an entry
    /// from another day or another run, a slot written before the entry was kept, and the open mode offer none. Deleting a slot removes
    /// the entry with it, and so does the mark of a won run; each slot keeps its own. The saves are kept in memory.</summary>
    [TestFixture]
    public class SaveAfternoonTest
    {
        const int Seed = 4711, Map = 2;

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaSaves.InMemoryForTests, Is.True, "the saves are kept in memory during the tests");
            ArenaSaves.ResetForTests();
        }

        [TearDown]
        public void TearDown() => ArenaSaves.ResetForTests();

        [Test]
        public void AnAfternoonsSave_IsKeptBesideTheLatest_ThroughTheNightAndTheMorningThatFollow()
        {
            Assert.That(ArenaSaves.HasAfternoonForTests(SaveMode.Story, 1), Is.False, "an empty slot keeps no afternoon");

            ArenaSaves.Store(SaveMode.Story, 1, Story(3, SavePhase.Afternoon, alloy: 40));
            Assert.That(ArenaSaves.HasAfternoonForTests(SaveMode.Story, 1), Is.True, "the afternoon's save is kept a second time");
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 1), Is.Null, "while it is the latest save itself there is nothing to go back to");

            ArenaSaves.Store(SaveMode.Story, 1, Story(3, SavePhase.Night, alloy: 55));
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 1), Is.Null, "nor from a night's save: a night cannot be lost");

            ArenaSaves.Store(SaveMode.Story, 1, Story(4, SavePhase.Morning, alloy: 70));
            var latest = ArenaSaves.List(SaveMode.Story)[1];
            Assert.That((latest.day, latest.phase), Is.EqualTo((4, SavePhase.Morning)), "the slot shows its latest save, the morning");
            Assert.That(ArenaSaves.Load(SaveMode.Story, 1).story.alloy, Is.EqualTo(70), "which is what Continue loads");
            var before = ArenaSaves.AfternoonBefore(SaveMode.Story, 1);
            Assert.That(before, Is.Not.Null, "the morning has the afternoon before it to go back to");
            Assert.That((before.mode, before.slot, before.empty, before.cleared, before.day, before.phase), Is.EqualTo((SaveMode.Story, 1, false, false, 3, SavePhase.Afternoon)),
                "day 3's afternoon, under its slot");
            var kept = ArenaSaves.LoadAfternoonForTests(SaveMode.Story, 1);
            Assert.That((kept.story.alloy, kept.story.day, kept.story.phase), Is.EqualTo((40, 3, SavePhase.Afternoon)), "kept as that afternoon began, not as the night left it");
        }

        [Test]
        public void ANewAfternoon_ReplacesTheOneKept()
        {
            ArenaSaves.Store(SaveMode.Story, 0, Story(2, SavePhase.Afternoon, alloy: 10));
            ArenaSaves.Store(SaveMode.Story, 0, Story(3, SavePhase.Morning, alloy: 20));
            ArenaSaves.Store(SaveMode.Story, 0, Story(3, SavePhase.Afternoon, alloy: 30));
            ArenaSaves.Store(SaveMode.Story, 0, Story(4, SavePhase.Morning, alloy: 40));

            var before = ArenaSaves.AfternoonBefore(SaveMode.Story, 0);
            Assert.That(before, Is.Not.Null, "day 4's morning has an afternoon before it");
            Assert.That((before.day, ArenaSaves.LoadAfternoonForTests(SaveMode.Story, 0).story.alloy), Is.EqualTo((3, 30)), "day 3's, the last one saved");
        }

        [Test]
        public void OnlyTheAfternoonOfTheDayBefore_OnTheSameMap_IsOffered()
        {
            // Day 1's morning, the run's first save: nothing before it.
            ArenaSaves.Store(SaveMode.Story, 0, Story(1, SavePhase.Morning));
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 0), Is.Null, "day 1's morning has no afternoon before it");

            // An afternoon two days back (the morning between them was never saved over it).
            ArenaSaves.Store(SaveMode.Story, 0, Story(2, SavePhase.Afternoon));
            ArenaSaves.Store(SaveMode.Story, 0, Story(4, SavePhase.Morning));
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 0), Is.Null, "an afternoon that is not the day before's is not offered");

            // Another run's afternoon left in the slot: the day fits, the terrain does not.
            ArenaSaves.Store(SaveMode.Story, 1, Story(3, SavePhase.Afternoon, seed: Seed + 1));
            ArenaSaves.Store(SaveMode.Story, 1, Story(4, SavePhase.Morning));
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 1), Is.Null, "nor one from another terrain");
            ArenaSaves.Store(SaveMode.Story, 2, Story(3, SavePhase.Afternoon, map: Map + 1));
            ArenaSaves.Store(SaveMode.Story, 2, Story(4, SavePhase.Morning));
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 2), Is.Null, "nor one from another map");
        }

        [Test]
        public void TheFinalBattle_GoesBackToNoAfternoon()
        {
            ArenaSaves.Store(SaveMode.Story, 2, Story(6, SavePhase.Afternoon));
            ArenaSaves.Store(SaveMode.Story, 2, Story(6, SavePhase.Tyrant));

            Assert.That(ArenaSaves.HasAfternoonForTests(SaveMode.Story, 2), Is.True, "the slot still keeps the last afternoon before the ignition");
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 2), Is.Null, "but the final battle's save does not go back to it");
            Assert.That(ArenaSaves.CanContinue(SaveMode.Story, 2), Is.True, "it is continued from itself");
        }

        [Test]
        public void ASlotWrittenBeforeTheAfternoonWasKept_StillLoadsAndContinues_AndOffersNone()
        {
            // The text an earlier build left: one entry, the morning's, and nothing beside it.
            ArenaSaves.WriteRawForTests(SaveMode.Story, 0, JsonUtility.ToJson(Story(5, SavePhase.Morning, alloy: 33)));

            Assert.That(ArenaSaves.HasAfternoonForTests(SaveMode.Story, 0), Is.False, "no afternoon was kept then");
            var shown = ArenaSaves.List(SaveMode.Story)[0];
            Assert.That((shown.empty, shown.day, shown.phase), Is.EqualTo((false, 5, SavePhase.Morning)), "the slot shows its save");
            Assert.That((ArenaSaves.CanContinue(SaveMode.Story, 0), ArenaSaves.Load(SaveMode.Story, 0).story.alloy), Is.EqualTo((true, 33)), "and continues from it");
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 0), Is.Null, "with only that morning to retry");
        }

        [Test]
        public void TheOpenMode_KeepsNoAfternoon()
        {
            var open = new SaveData();
            open.summary.wave = 3;
            open.summary.phase = SavePhase.Afternoon; // a summary that says so is still no story afternoon
            ArenaSaves.Store(SaveMode.Open, 0, open);

            Assert.That(ArenaSaves.HasAfternoonForTests(SaveMode.Open, 0), Is.False, "the open mode's save is written once");
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Open, 0), Is.Null, "and has nothing to go back to");
        }

        [Test]
        public void DeletingASlot_AndAWonRunsMark_RemoveTheAfternoonKept_AndEachSlotKeepsItsOwn()
        {
            for (int slot = 0; slot < ArenaSaves.SlotCount; slot++)
            {
                ArenaSaves.Store(SaveMode.Story, slot, Story(2 + slot, SavePhase.Afternoon));
                ArenaSaves.Store(SaveMode.Story, slot, Story(3 + slot, SavePhase.Morning));
            }
            Assume.That(Enumerable.Range(0, ArenaSaves.SlotCount).Select(slot => ArenaSaves.AfternoonBefore(SaveMode.Story, slot)?.day), Is.EqualTo(new int?[] { 2, 3, 4 }),
                "each slot keeps its own afternoon");

            ArenaSaves.Delete(SaveMode.Story, 0);
            Assert.That((ArenaSaves.HasEntryForTests(SaveMode.Story, 0), ArenaSaves.HasAfternoonForTests(SaveMode.Story, 0)), Is.EqualTo((false, false)),
                "deleting a slot leaves neither its save nor its afternoon");
            // A new run's first morning in the emptied slot finds nothing of the old run.
            ArenaSaves.Store(SaveMode.Story, 0, Story(3, SavePhase.Morning));
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 0), Is.Null, "so a new run there is offered nothing of the old one");

            Assert.That(ArenaSaves.MarkCleared(SaveMode.Story, 1), Is.True, "a won run's slot is marked");
            Assert.That((ArenaSaves.HasEntryForTests(SaveMode.Story, 1), ArenaSaves.HasAfternoonForTests(SaveMode.Story, 1)), Is.EqualTo((true, false)),
                "the won run keeps its save and no afternoon");
            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 1), Is.Null, "a won run goes back to nothing");

            Assert.That(ArenaSaves.AfternoonBefore(SaveMode.Story, 2)?.day, Is.EqualTo(4), "the third slot's afternoon is untouched");
        }

        // ───── Samples ─────

        /// <summary>A story save as the campaign writes one: the summary's day is the day the phase belongs to, the story part's day is
        /// the campaign's own count (a morning is saved while it still counts the day before).</summary>
        static SaveData Story(int day, SavePhase phase, int alloy = 0, int seed = Seed, int map = Map)
        {
            var save = new SaveData();
            save.summary.day = day;
            save.summary.phase = phase;
            save.summary.weapons = new[] { WeaponForm.Pistol };
            save.story.day = phase == SavePhase.Morning ? day - 1 : day;
            save.story.phase = phase;
            save.story.mapSeed = seed;
            save.story.alloy = alloy;
            save.run.mapIndex = map;
            return save;
        }
    }
}
