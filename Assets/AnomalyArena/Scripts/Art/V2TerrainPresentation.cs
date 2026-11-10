using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Switches a wall between whitebox and illustrated art. The original wall owns collision and navigation. On the Ruins and the Dunes the illustrated wall is
    /// the runtime stone (V5TerrainWallArt) that fills the wall's rectangle; a map without stone (the Rift Zone) keeps its saved artwork.</summary>
    [DisallowMultipleComponent]
    public sealed class V2TerrainPresentation : MonoBehaviour
    {
        public Transform illustratedRoot;
        public Renderer whitebox;
        bool applied, illustrated;

        void OnEnable() => Apply();
        void Update()
        {
            if (!applied || illustrated != Art.Enabled) Apply();
        }
        void Apply()
        {
            applied = true; illustrated = Art.Enabled;
            bool stone = V5TerrainWalls.StyleOfGroup(transform.parent, out var style);
            if (stone) V5TerrainWallArt.Attach(gameObject, style).Invalidate();
            if (illustratedRoot) illustratedRoot.gameObject.SetActive(illustrated && !stone);
            if (whitebox) whitebox.enabled = !illustrated;
        }

        /// <summary>The wall was just moved or resized (a copy cut from a longer wall): the stone follows at once, before the frame is drawn.</summary>
        public void Refresh()
        {
            if (TryGetComponent(out V5TerrainWallArt art)) art.Refresh();
        }
    }
}
