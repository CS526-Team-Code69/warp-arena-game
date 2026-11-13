using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Runs the fifth pass's base page's frame while the page shows (V5BasePage.cs): the keys that change zone, the page's fit to
    /// the screen and the art switch. It sits on the page, so it stops when the page is hidden.</summary>
    [DisallowMultipleComponent]
    public sealed class V5BaseTicker : MonoBehaviour
    {
        public Action Tick;

        void Update() => Tick?.Invoke();
    }
}
