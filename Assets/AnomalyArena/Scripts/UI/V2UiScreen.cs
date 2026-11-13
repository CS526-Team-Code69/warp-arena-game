using UnityEngine;

namespace AnomalyArena
{
    /// <summary>One scene-authored menu panel; layout can be edited in Unity without rewriting logic.</summary>
    public class V2UiScreen : MonoBehaviour
    {
        public ArenaUiState state;
        public UnityEngine.UI.Text heading, body, clock, summary;
        public V2UiChoice[] choices;
        public UnityEngine.UI.Button reroll;
        public UnityEngine.UI.Text rerollLabel;
        public UnityEngine.UI.Button startNext;
        [Tooltip("Optional picture for screens about one thing (the weapon card).")] public UnityEngine.UI.Image icon;
        [Tooltip("Optional label of a continue button whose text follows the language.")] public UnityEngine.UI.Text continueLabel;
        /// <summary>The fifth interface pass's look of this screen (set when it is installed); V2Hud asks it to draw at each refresh.</summary>
        [System.NonSerialized] public V5PickScreen pick;
    }
}
