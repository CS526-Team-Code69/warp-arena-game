using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Starts the official Localization in the language the game opens in (ArenaLanguage: the player's saved choice, the web
    /// page's address, or the system language). Until 2026-11-10 each scene carried its own starting Locale and the scene decided the
    /// language; one scene now serves both languages, and the field that held the Locale is gone (the saved scenes still list it, and
    /// Unity skips it when they load).</summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class SceneLocalePreset : MonoBehaviour
    {
        // Decided in Awake, before any Start reads the language.
        void Awake() => _ = PrepareAsync(ArenaLanguage.ResolveStartup(gameObject.scene.name));

        async Awaitable PrepareAsync(string startingCode)
        {
            try { await ArenaText.InitializeAsync(startingCode, destroyCancellationToken); }
            catch (OperationCanceledException) { /* Scene unloading cancels its initialization request. */ }
        }
    }
}
