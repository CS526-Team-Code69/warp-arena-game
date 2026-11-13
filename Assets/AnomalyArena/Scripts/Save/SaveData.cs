using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What one save slot holds, written as JSON (JsonUtility) by ArenaSaves. summary is what the title's save windows show; run is what
    /// both modes keep (the player's progression, skills, weapons, HP and stamina, the waves fought and the map laid); story or open holds
    /// the mode's own part, and the other part stays at its defaults. A save is the state just before a phase (story) or a wave (open
    /// mode) begins, so continuing it goes through that entry again. version says how the fields are meant: a build does not read a save
    /// of a later version than its own.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public SaveSummary summary = new();
        public RunSave run = new();
        public StorySave story = new();
        public OpenSave open = new();
    }

    /// <summary>What a run of either mode keeps.</summary>
    [Serializable]
    public sealed class RunSave
    {
        // the progression (RunProgression)
        public int level = 1;
        public float xp, energy;
        public bool energyUnlocked, voidEnergy, voidBadge;
        public RunStats stats = new();
        /// <summary>Stat cards drawn in a row below their wave band's high tier (the rarity pity).</summary>
        public int statCardPity;
        /// <summary>The wave the card draws are banded by.</summary>
        public int draftWave;
        /// <summary>Card picks banked, in order (true: stat cards only).</summary>
        public bool[] bankedPicks = Array.Empty<bool>();

        // the skills (SkillLoadout), one entry per slot: the SkillId, -1 for an empty slot
        public int[] skills = { -1, -1, -1 };
        public int[] skillLevels = { 1, 1, 1 };
        public int skillCardPity;

        // the weapons (RunWeapons)
        public WeaponSave[] hand = { new(), new() };
        public WeaponSave[] auto = { new(), new() };
        public int activeHand;

        // the player
        public float hp, stamina = 1f;

        // the waves and the map
        /// <summary>The last wave fought, 0-based (WaveManager.WaveIndex); -1 before the first.</summary>
        public int waveIndex = -1;
        public int kills;
        /// <summary>Seconds of fighting so far (GameManager.Elapsed).</summary>
        public float elapsed;
        /// <summary>The map laid, by its place in WorldMap.biomes; -1 when unknown.</summary>
        public int mapIndex = -1;
        public bool trialWon;
        /// <summary>The first black hole's cutscene has played in this run.</summary>
        public bool holeIntroDone;
    }

    /// <summary>One weapon slot; held is false for an empty one.</summary>
    [Serializable]
    public sealed class WeaponSave
    {
        public bool held;
        public WeaponForm form;
        public int variant;
        public bool revealed;
        public UpgradeAxis axes;
        public int mk = 1;
        public ElementKind element;
        public bool builtInElement;
        /// <summary>The story mode's void enchant is on this weapon.</summary>
        public bool voidEnchant;

        public static WeaponSave Of(RunWeapon weapon, bool voidEnchant = false) => weapon == null ? new WeaponSave() : new WeaponSave
        {
            held = true, form = weapon.form, variant = weapon.variant, revealed = weapon.revealed, axes = weapon.axes, mk = weapon.mk,
            element = weapon.element, builtInElement = weapon.builtInElement, voidEnchant = voidEnchant,
        };

        /// <summary>The weapon to put back, or null for an empty slot. Its clip is filled where it is put (RunWeapons.RestoreSlots).</summary>
        public RunWeapon ToWeapon() => !held ? null : new RunWeapon
        {
            form = form, variant = variant, revealed = revealed, axes = axes, mk = Mathf.Max(1, mk), element = element, builtInElement = builtInElement,
        };
    }

    /// <summary>One map black hole: where it stands, its feeds and its phase.</summary>
    [Serializable]
    public sealed class HoleSave
    {
        public Vector3 position;
        public int feeds;
        public HolePhase phase;
        public int openedWave;
        public int wavesLeft = 3;

        public static HoleSave Of(BlackHole hole) => !hole ? new HoleSave() : new HoleSave
        {
            position = hole.Centre, feeds = hole.Feeds, phase = hole.Phase, openedWave = hole.OpenedWave, wavesLeft = hole.WavesLeft,
        };
    }

    /// <summary>The story mode's own part of a save, taken just before the saved phase's entry.</summary>
    [Serializable]
    public sealed class StorySave
    {
        /// <summary>V2Campaign.Day when the save was taken. A morning is saved as the night before ends, so for a morning it is the day
        /// before the one that begins (the entry counts it up).</summary>
        public int day;
        public SavePhase phase;
        public bool overtime;
        public int waveOfDay;
        /// <summary>The terrain's seed (V2Campaign.MapSeed): the same seed on the same map lays the same terrain and black hole.</summary>
        public int mapSeed;

        // the currencies, the drops lying on the ground counted as picked up
        public int alloy, energy, dark, earnedAlloy, earnedEnergy, earnedDark;
        public int lampStock, nightAlloy, nightDark;
        public int voidEnergy, voidAlloy;
        public bool voidWalletSeen, prophetBeaten, voidArmor;
        public bool hasVoidOreCentre;
        public Vector3 voidOreCentre;

        // the base, its rooms in CampaignRoom order
        public int[] roomLevels = new int[4];
        public bool[] roomDamaged = new bool[4];
        public int durability = 1, shieldLevel;
        public float baseHp, baseShield;

        // the ship
        public bool engineFixed, armorFixed;
        public int fuel;

        // what was bought
        public int newWeaponsBought;
        public int[] upgradesBought = new int[5];
        public int teamMk = 1;
        public int shipOreFromDay = int.MaxValue;
        public int[] researchBought = new int[3];
        /// <summary>The body items bought: the StatId statsBoughtStat[i] at the CardRarity statsBoughtRarity[i].</summary>
        public int[] statsBoughtStat = Array.Empty<int>();
        public int[] statsBoughtRarity = Array.Empty<int>();
        public int overloadShieldBuys, overloadDamageBuys;
        public bool overloadShieldGiven;

        // the weapon crates lying on the map
        public int[] crateKinds = Array.Empty<int>();
        public Vector3[] cratePositions = Array.Empty<Vector3>();
        public int cratesOpened;

        // lines said and hints given once in a run
        public bool changedWeaponSaid, switchTaught, skillTaught, skillTeachPending, voidLineSaid;

        // the black hole's side quest
        public bool holeSeen, holeFailedToday, holeAwake;
        public int holeCheckDay = -1, holeFed;
        public HoleCheckState checkState;
        public float checkRequired, checkSeconds, checkDealt, checkElapsed;
        public HoleSave hole = new();
        /// <summary>Where the ore trail heads: the black hole, or where it stood once it is gone.</summary>
        public bool hasTrailTarget;
        public Vector3 trailTarget;

        // the morning's kills by region, which raise the night's shadows: killRegion[i] holds killCount[i] kills, and a kindKill[j]
        // (EnemyKind) fell in kindRegion[j]
        public int[] killRegion = Array.Empty<int>();
        public int[] killCount = Array.Empty<int>();
        public int[] kindRegion = Array.Empty<int>();
        public int[] kindKill = Array.Empty<int>();
    }

    /// <summary>The open mode's own part of a save: the map's black holes (the map itself is RunSave.mapIndex).</summary>
    [Serializable]
    public sealed class OpenSave
    {
        public HoleSave[] holes = Array.Empty<HoleSave>();
    }
}
