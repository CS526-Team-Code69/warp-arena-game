using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Art of the V2 day campaign, filled by the editor installer (Anomaly Arena/V2 Campaign/Install). Every entry may be empty: the campaign
    /// then draws its whitebox stand-in (boxes, capsules and coloured lights), so the loop plays before any art is in.
    /// </summary>
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
        [Tooltip("Memory pictures in order: the soldier in the cabin, the wormhole, the crash, the base deploying, the pull-back to the present. In the illustrated art they show for the memory's M4, M5, M6, M8 and M9 where the shot has no picture of its own below.")]
        public Texture2D[] memoryShots;
        [Tooltip("The transparent foreground layer of each memory picture, drawn over it with a little parallax; may be empty.")]
        public Texture2D[] memoryForegrounds;

        [Header("Story cutscenes (V2Cutscene: the memory M1 to M9, the failed ignition L1 to L5, the ending E1 to E3)")]
        [Tooltip("A picture for one cutscene shot, by the shot's id (M1 to M9, L1 to L5, E1 to E3), shown in place of the whitebox staging while the illustrated art is on. The course's English builds (BuildEdition) always stage in whitebox. A shot without one is staged in whitebox, or shows its memory picture.")]
        public CutscenePicture[] cutscenePictures;

        [Header("Voice (D9: English in both editions) and CG sounds")]
        [Tooltip("Voiced subtitle lines by line id (T01, S07, ...); a line without a clip shows its subtitle only.")]
        public CampaignVoice[] voices;
        [Tooltip("Base system line S04 by room, in CampaignRoom order (Control, Armament, Energy, Ship): its subtitle names the broken room.")]
        public AudioClip[] roomOfflineVoices;
        [Tooltip("Memory CG sounds in order: the fade into the memory, the wormhole turbulence, the crash, the base deploying. The 2026-11-10 takes of the same four in sounds play instead when they are there.")]
        public AudioClip[] memorySounds;

        [Header("Sound effects (the 2026-11-10 set, played by CampaignAudio)")]
        [Tooltip("One entry per CampaignSfx, filled from Audio/Campaign by file name (Anomaly Arena/V2 Campaign/Install Sounds). An entry without a clip stays silent. Gain is the sound's own level; running the install again keeps it.")]
        public CampaignSound[] sounds;
    }

    /// <summary>One campaign sound effect: which one, its clip and its own level (times CampaignAudio's effects or loops gain).</summary>
    [System.Serializable]
    public struct CampaignSound
    {
        public CampaignSfx sound;
        public AudioClip clip;
        [Range(0f, 1f)] public float gain;
    }

    /// <summary>A picture for one shot of a story cutscene: the shot's id (V2Cutscene: M1, L3, E2...) and the picture.</summary>
    [System.Serializable]
    public struct CutscenePicture
    {
        public string shot;
        public Texture picture;
    }

    /// <summary>One voiced subtitle line: the line id (the name of V2Campaign's Line) and its clip.</summary>
    [System.Serializable]
    public struct CampaignVoice
    {
        public string line;
        public AudioClip clip;
    }
}
