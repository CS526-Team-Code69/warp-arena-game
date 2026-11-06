using UnityEngine;

namespace AnomalyArena
{

    [CreateAssetMenu(menuName = "Anomaly Arena/Campaign/Art")]
    public sealed class CampaignArt : ScriptableObject
    {
        [Header("Models (feet-centre pivot, metres)")]
        public GameObject basePrefab;
        [Tooltip("Material of the damaged base (the retextured base model) used while a side of the base is broken.")]
        public Material baseDamagedMaterial;
        public GameObject shipWreckPrefab;
        public GameObject shipIntactPrefab;
        public GameObject lampPrefab;
        public GameObject oreNodePrefab;
        public GameObject darkNodePrefab;
        [Tooltip("The three looks of the one shadow monster.")] public GameObject[] shadowPrefabs;
        [Tooltip("Additive unlit material the dropped energy and alloy shards copy (their icon goes on as the texture).")]
        public Material shardMaterial;

        [Header("Icons (sliced from the V2 icon sheets)")]
        public Sprite energy;
        public Sprite alloy;
        public Sprite dark;
        public Sprite control;
        public Sprite shipBay;
        public Sprite armament;
        public Sprite energyLab;
        public Sprite shield;
        public Sprite durability;
        public Sprite engine;
        public Sprite armor;
        public Sprite fuel;
        public Sprite lamp;
        public Sprite skill;
        public Sprite locked;
        public Sprite repair;
        public Sprite nightLock;

        [Header("Pictures (shown only with the illustrated art; the whitebox draws a plain backdrop)")]
        public Texture2D roomControl;
        public Texture2D roomArmament;
        public Texture2D roomEnergy;
        [Tooltip("Armament Bay backgrounds for levels 2 and 3 (D6-B: each level shows its own room); level 1 and missing ones use roomArmament.")]
        public Texture2D[] roomArmamentLevels;
        [Tooltip("Energy Lab backgrounds for levels 2 and 3; level 1 and missing ones use roomEnergy.")]
        public Texture2D[] roomEnergyLevels;
        [Tooltip("Ship tab pictures, used only when the live view of the ship cannot be drawn (D1-C shows the ship itself).")]
        public Texture2D shipWreckPicture;
        public Texture2D shipIntactPicture;
        [Tooltip("Memory CG backgrounds in order (CG01 shots 2 to 6: the soldier in the cabin, the wormhole, the crash, the base deploying, the pull-back to the present).")]
        public Texture2D[] memoryShots;
        [Tooltip("The transparent foreground layer of each memory shot, drawn over its background with a little parallax; may be empty.")]
        public Texture2D[] memoryForegrounds;

        [Header("Voice (D9: English in both editions) and CG sounds")]
        [Tooltip("Voiced subtitle lines by line id (T01, S07, ...); a line without a clip shows its subtitle only.")]
        public CampaignVoice[] voices;
        [Tooltip("Base system line S04 by room, in CampaignRoom order (Control, Armament, Energy, Ship): its subtitle names the broken room.")]
        public AudioClip[] roomOfflineVoices;
        [Tooltip("Memory CG sounds in order: the fade into the memory, the wormhole turbulence, the crash, the base deploying.")]
        public AudioClip[] memorySounds;
    }

    [System.Serializable]
    public struct CampaignVoice
    {
        public string line;
        public AudioClip clip;
    }
}
