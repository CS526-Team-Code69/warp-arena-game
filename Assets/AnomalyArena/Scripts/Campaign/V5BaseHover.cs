using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AnomalyArena
{
    /// <summary>Tells the base's pages (V5BasePage.cs) when the pointer comes onto one of their parts and when it leaves: a card shows its
    /// details beside it, a button takes the keyboard's focus. Only the pointer's arrival and departure are read, so a turn of the wheel
    /// over the part still reaches the list it scrolls.</summary>
    [DisallowMultipleComponent]
    public sealed class V5BaseHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>True as the pointer comes onto the part, false as it leaves it or the part is hidden under it.</summary>
        public Action<bool> Hovered;

        bool inside;

        public void OnPointerEnter(PointerEventData eventData)
        {
            inside = true;
            Hovered?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData) => Leave();

        void OnDisable() => Leave();

        void Leave()
        {
            if (!inside) return;
            inside = false;
            Hovered?.Invoke(false);
        }
    }
}
