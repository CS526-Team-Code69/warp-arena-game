using UnityEngine;

namespace AnomalyArena
{
    public class V2SurfaceArt : MonoBehaviour
    {
        public Renderer target;
        public Material whitebox;
        public Material interstellar;
        public Vector2 tileScale = Vector2.one;
        [Tooltip("Use the saved biome wall colour with its interstellar hull texture.")]
        public bool tintIllustratedFromWhitebox;
        bool illustrated;
        MaterialPropertyBlock block;
        void OnEnable() => ApplyMode();
        void Update()
        {
            if (illustrated != Art.Enabled) ApplyMode();
        }
        public void SetWhiteboxMaterial(Material material)
        {
            whitebox = material;
            ApplyMode();
        }
        public void SetIllustratedMaterial(Material material, Vector2 scale)
        {
            interstellar = material; tileScale = scale; ApplyMode();
        }
        void ApplyMode()
        {
            if (!target) return;
            illustrated = Art.Enabled;
            target.sharedMaterial = Art.Enabled && interstellar ? interstellar : whitebox;
            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            Vector2 scale = illustrated ? tileScale : Vector2.one;
            block.SetVector("_BaseMap_ST", new Vector4(scale.x, scale.y, 0f, 0f));
            if (tintIllustratedFromWhitebox && whitebox)
            {
                block.SetColor("_BaseColor", whitebox.color);
                block.SetColor("_Color", whitebox.color);
            }
            target.SetPropertyBlock(block);
        }
    }
}
