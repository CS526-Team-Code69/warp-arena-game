using UnityEngine;

namespace AnomalyArena
{
    public class V2UiScreen : MonoBehaviour
    {
        public ArenaUiState state;
        public UnityEngine.UI.Text heading, body, clock, summary;
        public V2UiChoice[] choices;
        public UnityEngine.UI.Button reroll;
        public UnityEngine.UI.Text rerollLabel;
        public UnityEngine.UI.Button startNext;
    }
}
