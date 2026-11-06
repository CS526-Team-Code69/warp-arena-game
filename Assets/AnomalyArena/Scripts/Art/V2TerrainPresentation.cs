using UnityEngine;

namespace AnomalyArena
{

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

        public void Refresh()
        {
            if (TryGetComponent(out V5TerrainWallArt art)) art.Refresh();
        }
    }
}
