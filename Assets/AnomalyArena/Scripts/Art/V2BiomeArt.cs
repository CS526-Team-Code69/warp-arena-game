using System;
using UnityEngine;

namespace AnomalyArena
{

    [DefaultExecutionOrder(400)]
    public sealed class V2BiomeArt : MonoBehaviour
    {
        [Serializable]
        public sealed class Style
        {
            public string family;
            public Biome[] variants;
            public Material ground;
            public Transform detailRoot;
            [Tooltip("World size per ground texture repetition; does not affect collision or map cells.")] public float tileWorldSize = 24f;
        }
        public WorldMap world;
        public V2SurfaceArt surface;
        public Renderer ground;
        public Style[] styles;
        Biome selected;
        bool illustrated, trial, initialized;
        MaterialPropertyBlock block;
        void Start() => Apply();
        void Update()
        {
            if (!world || !ground || !surface) return;
            if (!initialized || selected != world.biome || illustrated != Art.Enabled || trial != world.TrialActive) Apply();
        }
        void Apply()
        {
            if (!world || !ground || !surface || styles == null) return;
            selected = world.biome; illustrated = Art.Enabled; trial = world.TrialActive; initialized = true;
            Style active = null;
            foreach (var style in styles)
            {
                if (style == null) continue;
                bool match = false;
                if (style.variants != null) foreach (var variant in style.variants) if (variant == selected) { match = true; break; }
                if (match) active = style;

                bool ruins = match && selected && selected.name.Contains("WreckField");
                if (style.detailRoot) style.detailRoot.gameObject.SetActive(match && illustrated && !trial && !ruins);
            }
            if (active == null || !active.ground) return;
            float size = Mathf.Max(1f, active.tileWorldSize);
            surface.SetIllustratedMaterial(active.ground, new Vector2(ground.bounds.size.x / size, ground.bounds.size.z / size));

            if (illustrated && world.biomes != null && world.biomeWallGroups != null)
            {
                int index = System.Array.IndexOf(world.biomes, selected);
                if (index >= 0 && index < world.biomeWallGroups.Length && V5TerrainWalls.StyleOfGroup(world.biomeWallGroups[index], out _)) V5TerrainGround.Attach(world, ground);
            }
            block ??= new MaterialPropertyBlock(); ground.GetPropertyBlock(block);

            Color tint = illustrated ? Color.white : selected && selected.floor ? selected.floor.color : Color.white;
            block.SetColor("_BaseColor", tint); block.SetColor("_Color", tint); ground.SetPropertyBlock(block);
        }
    }
}
