using UnityEngine;
using UnityEngine.EventSystems;

namespace AnomalyArena
{
    public class ArenaUiButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public V2Hud hud;
        public ArenaUiCommand command;
        public int index = -1;
        UnityEngine.UI.Button button;
        void Awake() => button = GetComponent<UnityEngine.UI.Button>();
        public void Invoke() { if (hud) hud.Dispatch(command, index); }
        public void OnPointerEnter(PointerEventData eventData) { if (button && button.IsInteractable()) V2Audio.Instance?.PlayUI(ArenaUiSound.Hover); }
        public void OnSelect(BaseEventData eventData) { if (button && button.IsInteractable()) V2Audio.Instance?.PlayUI(ArenaUiSound.Hover); }
    }
}
