using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Where the saves are kept, one entry per mode and slot. On the web, in the browser's storage through ArenaPrefs (the keys
    /// WarpArena.Save.Story.1 to WarpArena.Save.Open.3); on a desktop player and in the editor, as one JSON file each in
    /// persistentDataPath/saves/ (story-1.json to open-3.json), a folder a store's cloud sync can follow as it is. While the tests keep the
    /// settings in memory (ArenaPrefs.UseMemoryForTests, on for their whole run) the saves are kept in memory too, so no test reads a
    /// player's saves or writes over them. A read or write that fails is logged and taken as no save; it never stops the run. A story
    /// slot has a second entry beside its latest save, the afternoon it also keeps (Afternoon), under its own key and in its own file.
    /// </summary>
    static class SaveStore
    {
        /// <summary>A slot's second entry: the story's last afternoon save, kept beside the latest one (ArenaSaves.AfternoonBefore). Its
        /// key ends in .afternoon (WarpArena.Save.Story.1.afternoon) and its file in -afternoon (story-1-afternoon.json).</summary>
        public const string Afternoon = "afternoon";

        /// <summary>An entry's key and its file's name: the slot's latest save with no part, else the part named.</summary>
        public static string Key(SaveMode mode, int slot, string part = null) => $"WarpArena.Save.{mode}.{slot + 1}{(string.IsNullOrEmpty(part) ? "" : "." + part)}";
        public static string FileName(SaveMode mode, int slot, string part = null) =>
            $"{(mode == SaveMode.Story ? "story" : "open")}-{slot + 1}{(string.IsNullOrEmpty(part) ? "" : "-" + part)}.json";
        public static string Folder => Path.Combine(Application.persistentDataPath, "saves");

        /// <summary>The text of one of the slot's entries (its latest save, or the part named), or null when it holds none.</summary>
        public static string Read(SaveMode mode, int slot, string part = null)
        {
#if UNITY_INCLUDE_TESTS
            var memory = TestMemory;
            if (memory != null) return memory.TryGetValue(Key(mode, slot, part), out var kept) ? kept : null;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            string text = ArenaPrefs.GetString(Key(mode, slot, part), "");
            return string.IsNullOrEmpty(text) ? null : text;
#else
            string path = Path.Combine(Folder, FileName(mode, slot, part));
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception e)
            {
                Debug.LogWarning($"[Saves] could not read {path}: {e.Message}");
                return null;
            }
#endif
        }

        /// <summary>Keeps the text in one of the slot's entries (its latest save, or the part named) in place of what it held; false when
        /// it could not be written.</summary>
        public static bool Write(SaveMode mode, int slot, string text, string part = null)
        {
#if UNITY_INCLUDE_TESTS
            var memory = TestMemory;
            if (memory != null)
            {
                memory[Key(mode, slot, part)] = text;
                return true;
            }
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            ArenaPrefs.SetString(Key(mode, slot, part), text);
            ArenaPrefs.Save();
            return true;
#else
            string path = Path.Combine(Folder, FileName(mode, slot, part)), temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Folder);
                // Written beside the slot's file first and then put in its place, so a crash halfway never leaves half a save.
                File.WriteAllText(temporary, text);
                if (!File.Exists(path)) File.Move(temporary, path);
                else
                {
                    try { File.Replace(temporary, path, null); }
                    catch (Exception e) when (e is IOException or PlatformNotSupportedException)
                    {
                        File.Copy(temporary, path, true);
                        File.Delete(temporary);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Saves] could not write {path}: {e.Message}");
                return false;
            }
#endif
        }

        /// <summary>Removes one of the slot's entries (its latest save, or the part named).</summary>
        public static void Delete(SaveMode mode, int slot, string part = null)
        {
#if UNITY_INCLUDE_TESTS
            var memory = TestMemory;
            if (memory != null)
            {
                memory.Remove(Key(mode, slot, part));
                return;
            }
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            ArenaPrefs.Delete(Key(mode, slot, part));
            ArenaPrefs.Save();
#else
            string path = Path.Combine(Folder, FileName(mode, slot, part));
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); // what a write cut short may have left beside it
            }
            catch (Exception e) { Debug.LogWarning($"[Saves] could not delete {path}: {e.Message}"); }
#endif
        }

#if UNITY_INCLUDE_TESTS
        static Dictionary<string, string> testMemory;

        /// <summary>Tests only: the store in memory while ArenaPrefs keeps the settings in memory, else null (the real store).</summary>
        static Dictionary<string, string> TestMemory
        {
            get
            {
                if (!ArenaPrefs.MemoryForTests)
                {
                    testMemory = null;
                    return null;
                }
                return testMemory ??= new Dictionary<string, string>();
            }
        }

        /// <summary>Tests only: the saves are kept in memory now.</summary>
        public static bool InMemoryForTests => TestMemory != null;

        /// <summary>Tests only: the memory store is emptied.</summary>
        public static void ClearMemoryForTests() => TestMemory?.Clear();

        /// <summary>Tests only: whether the memory store holds an entry for the slot at all (a deleted slot leaves none behind), its
        /// latest save or the part named.</summary>
        public static bool HasEntryForTests(SaveMode mode, int slot, string part = null) => TestMemory?.ContainsKey(Key(mode, slot, part)) ?? false;
#endif
    }
}
