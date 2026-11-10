using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The start screen as the editor shows it while not playing (2026-11-10). The title is put together by code when the game starts
    /// (V2Hud's entries and Language option, the menu skin, the whitebox looks, the text tables), so the title saved in the scene is
    /// only its raw material and looked many versions old in the editor. This object lies over it with a picture of the real start
    /// screen, taken in Play mode by the menu Anomaly Arena/Editor title picture/Refresh. It is tagged EditorOnly, so no build holds
    /// it, and it removes itself the moment a play starts, before anything is drawn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EditorTitlePreview : MonoBehaviour
    {
        void Awake()
        {
            if (!Application.isPlaying) return;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
