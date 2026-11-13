using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Three save slots for each mode. A run started from a slot writes itself into that slot by itself: the story mode at the start of
    /// every morning, afternoon and night from day 1 on, the open mode just before every wave. Continuing a slot starts the run again at
    /// the point it was saved. The English Alpha keeps its single "Start Game" and has no saves.
    /// Only a run bound to a slot writes saves: one the title bound (Bind) before it started, or a continued one. Every other run (the
    /// tests', or one whose start bound no slot) writes none. When a run ends its slot keeps its last save, in both modes: a lost run can
    /// be continued from there, and a won run's save is marked cleared (SaveSummary.cleared, MarkCleared), which cannot be continued. A
    /// new game in a filled slot empties it first (Delete), then binds it. Each slot holds one SaveData, kept as JSON by SaveStore (the
    /// browser's storage on the web, a file on a desktop and in the editor, memory in the tests).
    /// </summary>
    public static class ArenaSaves
    {
        public const int SlotCount = 3;

        /// <summary>Saves exist on this page.</summary>
        public static bool Enabled => !BuildEdition.IsAlpha;

        /// <summary>Raised after a save was written or deleted.</summary>
        public static event Action Changed;

        // The slot the next run of each mode writes into (Bind), -1 for none; a continue waiting for its scene to load.
        static int storyBinding = -1, openBinding = -1;
        static SaveData pendingSave;
        static SaveMode pendingMode;
        static int pendingSlot = -1;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            storyBinding = openBinding = -1;
            pendingSave = null;
            pendingSlot = -1;
        }

        static bool ValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        /// <summary>The mode's slots, always SlotCount of them, in order. Each call reads the slots from storage: read them when a window
        /// opens and again on Changed.</summary>
        public static SaveSummary[] List(SaveMode mode)
        {
            var list = new SaveSummary[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                var save = Enabled ? Load(mode, i) : null;
                var summary = save != null ? save.summary : new SaveSummary();
                summary.mode = mode;
                summary.slot = i;
                summary.empty = save == null;
                summary.weapons ??= Array.Empty<WeaponForm>();
                list[i] = summary;
            }
            return list;
        }

        /// <summary>The next run of this mode writes its saves into this slot. Called before a new run starts from the title. The slot
        /// keeps what it holds until the new run writes its first save (the story at the start of day 1, the open mode before wave 1), so
        /// a new game in a filled slot deletes it first (Delete, then Bind). A slot out of range binds none.</summary>
        public static void Bind(SaveMode mode, int slot)
        {
            int bound = Enabled && ValidSlot(slot) ? slot : -1;
            if (mode == SaveMode.Story) storyBinding = bound;
            else openBinding = bound;
        }

        /// <summary>The slot the next run of this mode writes into, or -1.</summary>
        public static int BoundSlot(SaveMode mode) => mode == SaveMode.Story ? storyBinding : openBinding;

        /// <summary>A run of this mode starts: it takes the slot bound for it (-1: none, it writes nothing). Both modes' bindings are used
        /// up, so one left by a run that never started cannot reach a later run.</summary>
        internal static int ClaimBinding(SaveMode mode)
        {
            int slot = BoundSlot(mode);
            storyBinding = openBinding = -1;
            return Enabled ? slot : -1;
        }

        /// <summary>The slot holds a save that can be continued: not empty, not cleared.</summary>
        public static bool CanContinue(SaveMode mode, int slot)
        {
            var save = Enabled ? Load(mode, slot) : null;
            return save != null && !save.summary.cleared;
        }

        /// <summary>Starts the saved run of this slot where it was saved: the scene loads again with the saved map, and the run goes on
        /// from the start of the saved phase (story) or wave (open mode), its later saves into the same slot. Only from the title; an
        /// empty or cleared slot starts nothing (CanContinue). The caller plays the click's sound (the scene loads a moment later).</summary>
        public static void Continue(SaveMode mode, int slot)
        {
            if (!Enabled || !ValidSlot(slot) || pendingSave != null) return;
            var save = Load(mode, slot);
            if (save == null)
            {
                Debug.LogWarning($"[Saves] {mode} slot {slot + 1} holds no save to continue");
                return;
            }
            if (save.summary.cleared)
            {
                Debug.Log($"[Saves] {mode} slot {slot + 1} holds a cleared run, which is not continued");
                return;
            }
            var game = GameManager.Instance;
            if (!game || !game.CanContinueSave)
            {
                Debug.LogWarning("[Saves] a save is continued from the title only");
                return;
            }
            pendingSave = save;
            pendingMode = mode;
            pendingSlot = slot;
            storyBinding = openBinding = -1;
            WorldMap.NextMapIndex = save.run.mapIndex;
            game.ReloadForSave();
        }

        /// <summary>A continue waits for the scene loaded for it; mode is its mode.</summary>
        internal static bool ContinuePending(out SaveMode mode)
        {
            mode = pendingMode;
            return pendingSave != null;
        }

        /// <summary>The waiting continue, taken once by the scene loaded for it.</summary>
        internal static bool TakeContinue(out SaveMode mode, out int slot, out SaveData save)
        {
            mode = pendingMode;
            slot = pendingSlot;
            save = pendingSave;
            pendingSave = null;
            pendingSlot = -1;
            return save != null && ValidSlot(slot);
        }

        /// <summary>Empties the slot.</summary>
        public static void Delete(SaveMode mode, int slot)
        {
            if (!ValidSlot(slot)) return;
            SaveStore.Delete(mode, slot);
            Changed?.Invoke();
        }

        /// <summary>The slot's save, or null when it holds none, or one this build cannot read (broken text, a later version).</summary>
        public static SaveData Load(SaveMode mode, int slot)
        {
            if (!ValidSlot(slot)) return null;
            string text = SaveStore.Read(mode, slot);
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                var save = JsonUtility.FromJson<SaveData>(text);
                if (save == null || save.version < 1 || save.version > SaveData.CurrentVersion || save.summary == null || save.summary.mode != mode) return null;
                save.run ??= new RunSave();
                save.story ??= new StorySave();
                save.open ??= new OpenSave();
                return save;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Saves] {mode} slot {slot + 1} could not be read: {e.Message}");
                return null;
            }
        }

        /// <summary>Writes the save into the slot in place of what it held; its summary takes the slot's mode and number and the time now,
        /// and Changed follows. False when it could not be written (logged; the run goes on without it) or saves are off on this page.</summary>
        public static bool Store(SaveMode mode, int slot, SaveData save)
        {
            if (!Enabled || !ValidSlot(slot) || save == null) return false;
            save.version = SaveData.CurrentVersion;
            save.summary ??= new SaveSummary();
            save.summary.mode = mode;
            save.summary.slot = slot;
            save.summary.empty = false;
            save.summary.savedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool written = SaveStore.Write(mode, slot, JsonUtility.ToJson(save));
            if (written) Changed?.Invoke();
            return written;
        }

        /// <summary>The run bound to this slot was won: the slot keeps its last save, marked cleared, its summary brought up to the finished
        /// run (finished, when given, fills in what the run ended with). A lost run calls nothing: its slot keeps its last save as it is.
        /// False when nothing could be written.</summary>
        public static bool MarkCleared(SaveMode mode, int slot, Action<SaveSummary> finished = null)
        {
            if (!Enabled || !ValidSlot(slot)) return false;
            var save = Load(mode, slot) ?? new SaveData();
            finished?.Invoke(save.summary);
            save.summary.cleared = true;
            return Store(mode, slot, save);
        }

        // ───── What the summaries show ─────

        /// <summary>The day a story save belongs to: a morning's save is taken as the night before ends, while V2Campaign.Day still counts
        /// that night's day; the afternoon's and the night's within their own day.</summary>
        public static int StoryDay(int campaignDay, SavePhase phase) => phase == SavePhase.Morning ? campaignDay + 1 : campaignDay;

        /// <summary>The open-mode wave about to start, from 1, after the last wave fought (WaveManager.WaveIndex, -1 before the first);
        /// the one after the ordinary waves is the Tyrant's.</summary>
        public static int OpenWave(int lastWaveIndex) => Mathf.Max(-1, lastWaveIndex) + 2;

        /// <summary>A map's family as a summary shows it: 0 the Dunes, 1 the Ruins, -1 neither.</summary>
        public static int MapFamily(Biome map) => !map ? -1 : map.InFamily(Biome.Dunes) ? 0 : map.InFamily(Biome.Ruins) ? 1 : -1;

        /// <summary>The weapons a summary lists: hand 1, hand 2, auto 1, auto 2, the empty slots left out.</summary>
        public static WeaponForm[] WeaponsOf(RunWeapons weapons)
        {
            var forms = new List<WeaponForm>(4);
            if (weapons)
            {
                foreach (var weapon in weapons.Hand) if (weapon != null) forms.Add(weapon.form);
                foreach (var weapon in weapons.Auto) if (weapon != null) forms.Add(weapon.form);
            }
            return forms.ToArray();
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>Tests only: the saves are kept in memory now (as long as ArenaPrefs keeps the settings there, the whole test run).</summary>
        public static bool InMemoryForTests => SaveStore.InMemoryForTests;

        /// <summary>Tests only: every slot of the memory store is emptied, and the bindings and a waiting continue are forgotten. Nothing
        /// happens while the saves are kept for real.</summary>
        public static void ResetForTests()
        {
            if (!SaveStore.InMemoryForTests) return;
            SaveStore.ClearMemoryForTests();
            ResetForPlay();
        }

        /// <summary>Tests only: the slot's stored text as it is (null when the store holds no entry for it), and whether it holds one.</summary>
        public static string ReadRawForTests(SaveMode mode, int slot) => SaveStore.InMemoryForTests ? SaveStore.Read(mode, slot) : null;
        public static bool HasEntryForTests(SaveMode mode, int slot) => SaveStore.HasEntryForTests(mode, slot);

        /// <summary>Tests only: puts this text into the slot as it is (broken or foreign text included); nothing while saves are real.</summary>
        public static void WriteRawForTests(SaveMode mode, int slot, string text)
        {
            if (SaveStore.InMemoryForTests) SaveStore.Write(mode, slot, text);
        }
#endif
    }
}
