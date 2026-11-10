using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>One saved floor, three authored surface styles. WorldMap owns actual biome/layout selection.</summary>
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
            [Tooltip("地面图案每次重复对应的世界尺寸；不影响碰撞或地图格子。")] public float tileWorldSize = 24f;
        }
        public WorldMap world;
        public V2SurfaceArt surface;
        public Renderer ground;
        public Style[] styles;
        Biome selected;
        // campaignMap: the V2 day campaign's larger map is in place (since 2026-11-06); the floor grew, so its tiling and the walls'
        // shadow texture follow (WorldMap.ApplyCampaignLayout), and again when the saved map comes back. Always false in the classic run.
        bool illustrated, trial, initialized, campaignMap;
        MaterialPropertyBlock block;
        void Start() => Apply();
        void Update()
        {
            if (!world || !ground || !surface) return;
            if (!initialized || selected != world.biome || illustrated != Art.Enabled || trial != world.TrialActive || campaignMap != world.CampaignMapApplied) Apply();
        }
        void Apply()
        {
            if (!world || !ground || !surface || styles == null) return;
            selected = world.biome; illustrated = Art.Enabled; trial = world.TrialActive; campaignMap = world.CampaignMapApplied; initialized = true;
            Style active = null;
            foreach (var style in styles)
            {
                if (style == null) continue;
                bool match = false;
                if (style.variants != null) foreach (var variant in style.variants) if (variant == selected) { match = true; break; }
                if (match) active = style;
                // The Ruins' saved overlays (roots, spore scars, chitin) belong to the old bio-desert look; their ground now shows its own rubble.
                bool ruins = match && selected && selected.name.Contains("WreckField");
                if (style.detailRoot) style.detailRoot.gameObject.SetActive(match && illustrated && !trial && !ruins);
            }
            if (active == null || !active.ground) return;
            float size = Mathf.Max(1f, active.tileWorldSize);
            surface.SetIllustratedMaterial(active.ground, new Vector2(ground.bounds.size.x / size, ground.bounds.size.z / size));
            // The walls' shadows on the ground of the maps built from stone.
            if (illustrated && world.biomes != null && world.biomeWallGroups != null)
            {
                int index = System.Array.IndexOf(world.biomes, selected);
                if (index >= 0 && index < world.biomeWallGroups.Length && V5TerrainWalls.StyleOfGroup(world.biomeWallGroups[index], out _)) V5TerrainGround.Attach(world, ground);
            }
            block ??= new MaterialPropertyBlock(); ground.GetPropertyBlock(block);
            // The authored coloured floor needs a neutral tint; whitebox mode restores the actual biome's tint.
            Color tint = illustrated ? Color.white : selected && selected.floor ? selected.floor.color : Color.white;
            block.SetColor("_BaseColor", tint); block.SetColor("_Color", tint); ground.SetPropertyBlock(block);
        }
    }
}
