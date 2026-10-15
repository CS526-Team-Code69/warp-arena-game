using System;
using UnityEngine;
using UnityEngine.Localization;

namespace AnomalyArena
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class SceneLocalePreset : MonoBehaviour
    {
        [field: SerializeField, Tooltip("Official Locale asset selected before showing this scene's UI.")]
        public Locale StartingLocale { get; set; }

        void Awake() => _ = PrepareAsync();

        async Awaitable PrepareAsync()
        {
            try { await ArenaText.InitializeAsync(StartingLocale, destroyCancellationToken); }
            catch (OperationCanceledException) {                                                           }
        }
    }
}
