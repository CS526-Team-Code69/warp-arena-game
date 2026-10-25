using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{

    public sealed class TestKeyboard : IDisposable
    {
        public void Dispose() { }

        public async Task Tap(params KeyCode[] keys)
        {
            var game = GameManager.Instance;
            foreach (var key in keys)
            {
                if (key != KeyCode.E) { game?.PressKey(key); continue; }
                var world = WorldMap.Active;
                if (world && game && game.player && (game.IsCombatActive || game.CanPlayerMove)) world.TryEnterPortal(game.player);
            }
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
        }

        public async Task TapFor(float seconds, params KeyCode[] keys)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Tap(keys);
        }

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
