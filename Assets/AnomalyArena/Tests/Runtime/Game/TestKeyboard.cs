using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Presses keys through the code the keyboard reaches: GameManager.PressKey for every menu key (Space and Enter included, which
    /// only the weapon card answers to) and, for E, WorldMap's portal entry, which runs while the game is in combat, the player can move or
    /// the build phase's clock runs (a build panel open: the portal then refuses). Device events queued
    /// into the Input System never reach the game in the batch-mode test runner, so the keys go in here instead; SourceKeysTest makes sure
    /// no other script reads Space or Enter from the keyboard.</summary>
    public sealed class TestKeyboard : IDisposable
    {
        public void Dispose() { }

        /// <summary>Presses the keys, then lets three frames pass so the phase they lead to has settled.</summary>
        public async Task Tap(params KeyCode[] keys)
        {
            var game = GameManager.Instance;
            foreach (var key in keys)
            {
                if (key != KeyCode.E) { game?.PressKey(key); continue; }
                var world = WorldMap.Active;
                if (world && game && game.player && (game.IsCombatActive || game.CanPlayerMove || game.BuildTimeFlows)) world.TryEnterPortal(game.player);
            }
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
        }

        /// <summary>Taps the keys again and again for the given real time (longer than any menu input lock).</summary>
        public async Task TapFor(float seconds, params KeyCode[] keys)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Tap(keys);
        }

        /// <summary>Taps the key until completed holds.</summary>
        public async Task TapUntil(KeyCode key, Func<bool> completed, string operation, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Key {key} never did this: {operation}.");
                await Tap(key);
            }
        }
    }
}
