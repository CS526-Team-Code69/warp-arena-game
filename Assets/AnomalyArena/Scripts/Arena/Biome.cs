using System;
using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/World/Biome")]
    public sealed class Biome : ScriptableObject
    {

        [Serializable]
        public struct QuicksandLayer
        {
            public Texture texture;
            public Color tint;
            [Min(.1f), Tooltip("Decal width as a share of the pool's diameter: 1 = exactly the pool.")] public float size;
            [Tooltip("Turning speed in degrees per second; negative turns the other way, 0 stays still.")] public float spin;
        }

        public string displayName = "Interstellar Battlefield";
        public MapLayout layout;
        [Tooltip("Night ambient: the dark look every map had until 2026-11-06, kept for V2's night (not built yet).")]
        public Color ambient = new Color(0.19f, 0.24f, 0.35f);
        [Header("Lighting (2026-11-06: every map is daytime for now; night comes later)")]
        [Tooltip("Colour of the arena's directional light by day.")] public Color daySun = new Color(1f, .96f, .88f);
        [Min(0f), Tooltip("Intensity of the arena's directional light by day.")] public float daySunIntensity = 2.2f;
        [Tooltip("Flat ambient light by day: how bright and what colour the shaded sides are.")] public Color dayAmbient = new Color(.55f, .57f, .62f);
        [Tooltip("Directional light at night: the cold blue every map had until 2026-11-06. Not used yet.")] public Color nightSun = new Color(.72f, .84f, 1f);
        [Min(0f), Tooltip("Intensity of the directional light at night. Not used yet.")] public float nightSunIntensity = 1.2f;
        public Material floor;
        public Material wall;
        [Tooltip("Enemy mix offsets, in EnemyKind order. Only already-unlocked types can be tilted.")]
        public int[] mixTilt = new int[11];
        [Tooltip("Whether a run may start on this map. Off for the Rift Zone maps: system doc 12.2 has two maps, the Dunes and the Ruins, and the void " +
                 "is the hidden trial.")]
        public bool inRotation = true;
        [Header("Quicksand look (system doc 12.1, Dunes)")]
        [Tooltip("Transparent unlit material the quicksand decals share; texture and colour go through property blocks.")]
        public Material quicksandMaterial;
        [Tooltip("Decals drawn on every quicksand pool of the layout, bottom first. Without a material or layers the pools still slow, unseen.")]
        public QuicksandLayer[] quicksandLayers;

        public void ApplyLighting(bool night = false)
        {
            RenderSettings.ambientLight = night ? ambient : dayAmbient;
            var sun = RenderSettings.sun;
            if (!sun)
                foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                    if (light.type == LightType.Directional && light.enabled) { sun = light; break; }
            if (!sun) return;
            sun.color = night ? nightSun : daySun;
            sun.intensity = night ? nightSunIntensity : daySunIntensity;
        }

        public static int PickForRun(Biome[] biomes)
        {
            int open = 0;
            foreach (var biome in biomes) if (biome && biome.inRotation) open++;
            if (open == 0) return UnityEngine.Random.Range(0, biomes.Length);
            int pick = UnityEngine.Random.Range(0, open);
            for (int i = 0; i < biomes.Length; i++)
                if (biomes[i] && biomes[i].inRotation && pick-- == 0) return i;
            return 0;
        }
    }
}
