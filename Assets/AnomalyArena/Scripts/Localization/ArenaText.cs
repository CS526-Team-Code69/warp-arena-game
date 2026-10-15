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
                return loaded && table && font && selection.IsDone && selection.Result
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

        public static async Awaitable InitializeAsync(Locale startingLocale, CancellationToken cancellationToken = default)
        {
            loaded = false; OnChanged();

            var initialization = LocalizationSettings.InitializationOperation;
            await WaitAsync(initialization, cancellationToken);
            if (!initialization.IsValid() || initialization.Status != AsyncOperationStatus.Succeeded)
            { Fail("Localization initialization failed."); return; }
            LocalizationSettings.SelectedLocaleChanged -= LocalizationSettings_SelectedLocaleChanged;
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

                acquiredFont = Addressables.ResourceManager.Acquire(fontOperation);
                await WaitAsync(textOperation, cancellationToken);
                await WaitAsync(acquiredFont, cancellationToken);
                if (request != revision || LocalizationSettings.SelectedLocaleAsync.Result != locale) return;
                if (!textOperation.IsValid() || textOperation.Status != AsyncOperationStatus.Succeeded || !textOperation.Result)
                { Fail("ArenaUI StringTable did not load for " + locale.Identifier.Code); return; }
                if (!acquiredFont.IsValid() || acquiredFont.Status != AsyncOperationStatus.Succeeded || !acquiredFont.Result)
                { Fail("ArenaAssets/font.ui did not load for " + locale.Identifier.Code); return; }
                if (fontLease.IsValid()) Addressables.Release(fontLease);
                fontLease = acquiredFont; acquiredFont = default;
                table = textOperation.Result; font = fontLease.Result;
                missingKeys.Clear(); LoadError = ""; loaded = true; OnChanged();
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

        public static string Pair(string chinese, string english) => Ready ? english : "";

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
