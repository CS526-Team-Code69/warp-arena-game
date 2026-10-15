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
            if (illustratedRoot) illustratedRoot.gameObject.SetActive(illustrated);
            if (whitebox) whitebox.enabled = !illustrated;
        }
    }
}
