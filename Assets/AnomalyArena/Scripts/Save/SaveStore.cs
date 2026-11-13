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
    /// player's saves or writes over them. A read or write that fails is logged and taken as no save; it never stops the run.
    /// </summary>
    static class SaveStore
    {
        public static string Key(SaveMode mode, int slot) => $"WarpArena.Save.{mode}.{slot + 1}";
        public static string FileName(SaveMode mode, int slot) => $"{(mode == SaveMode.Story ? "story" : "open")}-{slot + 1}.json";
        public static string Folder => Path.Combine(Application.persistentDataPath, "saves");

        /// <summary>The slot's text, or null when it holds none.</summary>
        public static string Read(SaveMode mode, int slot)
        {
#if UNITY_INCLUDE_TESTS
            var memory = TestMemory;
            if (memory != null) return memory.TryGetValue(Key(mode, slot), out var kept) ? kept : null;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            string text = ArenaPrefs.GetString(Key(mode, slot), "");
            return string.IsNullOrEmpty(text) ? null : text;
#else
            string path = Path.Combine(Folder, FileName(mode, slot));
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception e)
            {
                Debug.LogWarning($"[Saves] could not read {path}: {e.Message}");
                return null;
            }
#endif
        }

        /// <summary>Keeps the text in the slot in place of what it held; false when it could not be written.</summary>
        public static bool Write(SaveMode mode, int slot, string text)
        {
#if UNITY_INCLUDE_TESTS
            var memory = TestMemory;
            if (memory != null)
            {
                memory[Key(mode, slot)] = text;
                return true;
            }
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            ArenaPrefs.SetString(Key(mode, slot), text);
            ArenaPrefs.Save();
            return true;
#else
            string path = Path.Combine(Folder, FileName(mode, slot)), temporary = path + ".tmp";
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

        public static void Delete(SaveMode mode, int slot)
        {
#if UNITY_INCLUDE_TESTS
            var memory = TestMemory;
            if (memory != null)
            {
                memory.Remove(Key(mode, slot));
                return;
            }
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            ArenaPrefs.Delete(Key(mode, slot));
            ArenaPrefs.Save();
#else
            string path = Path.Combine(Folder, FileName(mode, slot));
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

        /// <summary>Tests only: whether the memory store holds an entry for the slot at all (a deleted slot leaves none behind).</summary>
        public static bool HasEntryForTests(SaveMode mode, int slot) => TestMemory?.ContainsKey(Key(mode, slot)) ?? false;
#endif
    }
}
