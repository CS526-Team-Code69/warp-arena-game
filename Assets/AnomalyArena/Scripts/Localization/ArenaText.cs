using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AnomalyArena
{
    /// <summary>Reads the official selected Locale; cached tables are data, never a separate language setting.</summary>
    public static class ArenaText
    {
        public const string StringCollection = "ArenaUI";
        public const string AssetCollection = "ArenaAssets";
        public const string UiFontKey = "font.ui";
        static StringTable table;
        static Font font;
        static AsyncOperationHandle<Font> fontLease;
        static bool loaded;
        static int revision;
        static readonly HashSet<string> missingKeys = new();
        public static event Action Changed;
        public static string LoadError { get; private set; } = "";

        public static bool Ready
        {
            get
            {
                var selection = LocalizationSettings.SelectedLocaleAsync;
                return loaded && table && selection.IsDone && selection.Result
                    && table.LocaleIdentifier == selection.Result.Identifier;
            }
        }
        public static bool IsChinese
        {
            get
            {
                var selection = LocalizationSettings.SelectedLocaleAsync;
                return selection.IsDone && selection.Result
                    && selection.Result.Identifier.Code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            }
        }
        // Null until the locale's font asset arrives; the scene's labels keep the packaged font they were saved with until then.
        public static Font Font => Ready ? font : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime()
        {
            LocalizationSettings.SelectedLocaleChanged -= LocalizationSettings_SelectedLocaleChanged;
            if (fontLease.IsValid()) Addressables.Release(fontLease);
            fontLease = default;
            table = null; font = null; loaded = false; revision = 0;
            LoadError = ""; missingKeys.Clear(); Changed = null;
        }

        /// <summary>Waits for the official Localization, selects the starting language (a locale code, StartupLanguage.Chinese or
        /// English; an unknown one keeps what Localization's own start selected) and loads its text table.</summary>
        public static async Awaitable InitializeAsync(string startingCode, CancellationToken cancellationToken = default)
        {
            loaded = false; OnChanged();
            // WebGL cannot use synchronous Addressables completion or the handle Task property.
            var initialization = LocalizationSettings.InitializationOperation;
            await WaitAsync(initialization, cancellationToken);
            if (!initialization.IsValid() || initialization.Status != AsyncOperationStatus.Succeeded)
            { Fail("Localization initialization failed."); return; }
            LocalizationSettings.SelectedLocaleChanged -= LocalizationSettings_SelectedLocaleChanged;
            var startingLocale = string.IsNullOrEmpty(startingCode) ? null
                : LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(startingCode));
            if (startingLocale) LocalizationSettings.SelectedLocale = startingLocale;
            LocalizationSettings.SelectedLocaleChanged += LocalizationSettings_SelectedLocaleChanged;
            await LoadAsync(LocalizationSettings.SelectedLocaleAsync.Result, ++revision, cancellationToken);
        }

        static void LocalizationSettings_SelectedLocaleChanged(Locale locale)
        {
            loaded = false; OnChanged();
            _ = LoadAsync(locale, ++revision, CancellationToken.None);
        }

        static async Awaitable LoadAsync(Locale locale, int request, CancellationToken cancellationToken)
        {
            if (!locale) { Fail("No official selected Locale."); return; }
            AsyncOperationHandle<Font> acquiredFont = default;
            try
            {
                var textOperation = LocalizationSettings.StringDatabase.GetTableAsync(StringCollection, locale);
                var fontOperation = LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<Font>(AssetCollection, UiFontKey, locale);
                // Localized-asset operations auto-release after completion; retain a lease for the cached font.
                acquiredFont = Addressables.ResourceManager.Acquire(fontOperation);
                await WaitAsync(textOperation, cancellationToken);
                if (request != revision || LocalizationSettings.SelectedLocaleAsync.Result != locale) return;
                if (!textOperation.IsValid() || textOperation.Status != AsyncOperationStatus.Succeeded || !textOperation.Result)
                { Fail("ArenaUI StringTable did not load for " + locale.Identifier.Code); return; }
                // Text is enough to show the UI. The font asset is a separate web download of the whole CJK font, so waiting
                // for it here kept the title screen hidden behind the bare arena.
                table = textOperation.Result;
                missingKeys.Clear(); LoadError = ""; loaded = true; OnChanged();

                await WaitAsync(acquiredFont, cancellationToken);
                if (request != revision || LocalizationSettings.SelectedLocaleAsync.Result != locale) return;
                if (!acquiredFont.IsValid() || acquiredFont.Status != AsyncOperationStatus.Succeeded || !acquiredFont.Result)
                {
                    LoadError = "ArenaAssets/font.ui did not load for " + locale.Identifier.Code;
                    Debug.LogError(LoadError); return;
                }
                if (fontLease.IsValid()) Addressables.Release(fontLease);
                fontLease = acquiredFont; acquiredFont = default;
                font = fontLease.Result; OnChanged();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                if (request == revision) Fail(exception.Message);
            }
            finally
            {
                if (acquiredFont.IsValid()) Addressables.Release(acquiredFont);
            }
        }

        static async Awaitable WaitAsync<T>(AsyncOperationHandle<T> operation, CancellationToken cancellationToken)
        {
            while (operation.IsValid() && !operation.IsDone)
                await Awaitable.NextFrameAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        public static string Pair(string chinese, string english) => Ready ? IsChinese ? chinese : english : "";

        /// <summary>Formats an already loaded official entry. No synchronous loading or fallback-language flash.</summary>
        public static string Text(string key, params object[] arguments)
        {
            if (!Ready || string.IsNullOrEmpty(key)) return "";
            var entry = table.GetEntry(key);
            if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
            {
                if (missingKeys.Add(key)) Debug.LogError("ArenaUI missing translation: " + key);
                return "";
            }
            return entry.GetLocalizedString(arguments);
        }
        static void Fail(string message)
        {
            loaded = false; LoadError = message;
            Debug.LogError(message); OnChanged();
        }
        static void OnChanged() => Changed?.Invoke();
    }
}
