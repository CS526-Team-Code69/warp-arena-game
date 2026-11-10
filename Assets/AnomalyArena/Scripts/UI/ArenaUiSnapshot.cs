using System;

namespace AnomalyArena
{
    /// <summary>Reuse this object and its entries. Numeric clocks are supplied by the actual run.</summary>
    [Serializable]
    public class ArenaUiSnapshot
    {
        public bool selectionUnlimited;
        public bool supplyLanded;
        public ArenaUiState state;
        public bool illustrated;
        public bool energyUnlocked;
        public bool voidEnergy;
        public bool voidLevel;
        public bool earlyStartConfirmation;

        public bool isGreaterReward;

        // Build phase with the panel put away (Tab): the player walks the empty arena.
        public bool buildWalking;

        public bool staminaExhausted;

        // Zero-based ArtSet order; -1 means no immunity is disclosed in this slot.
        public int[] bossImmunityIndices = { -1, -1, -1 };
        public float hp = 50f, maxHp = 50f, shield, stamina = 1f;
        public float energy, maxEnergy = 100f, xp, xpNeeded = 10f;
        public float waveTime, buildTime, charge01;
        public bool waveTimeExpired;
        public int waveBatch, waveLiving, wavePending;
        public float bossHp, bossMaxHp, bossShield;
        public int level = 1, maxLevel = 12, wave = 1, maxWaves = 14, kills, bankedDrafts, rerolls = 1;
        public int draftIndex = 1, draftCount;
        public string banner = "";
        /// <summary>V2 day campaign: what the wave label shows instead of the wave (day, phase, time left); empty in the classic run.</summary>
        public string campaignLabel = "";
        /// <summary>V2 day campaign since 2026-11-06: kills give no XP (every upgrade is bought at the base), so the level line shows the
        /// level alone and V2Hud hides the XP bar and the banked-pick markers. The HUD also words its texts without cards while it is set.</summary>
        public bool hideXp;
        /// <summary>V2 day campaign since 2026-11-10: the bottom row's auto-weapon slots and skill slots stay hidden until the run holds
        /// its first auto weapon or skill (empty slots told of things the story had not shown yet). Always false in the open mode.</summary>
        public bool hideAutoSlots, hideSkillSlots;
        public string hint = "";
        public string playerStatuses = "";
        public string bossImmunities = "";
        public string resultTitle = "RUN ENDED";
        public string resultDetails = "";

        public string buildSummary = "";

        // The weapon card shown the moment a weapon is taken.
        public string weaponIntroTitle = "", weaponIntroBody = "", weaponIntroHint = "", weaponIntroContinue = "";

        public int weaponIntroForm = -1;

        // Card picks: the view plays a flourish on the picked card whenever the sequence moves on.
        public int cardPickSequence, cardPickIndex = -1;
        public ArenaUiState cardPickState;
        public CardRarity cardPickRarity;
        public ArenaUiSlot[] handSlots = { new(), new() };
        public ArenaUiSlot[] autoSlots = { new(), new() };
        public ArenaUiSlot[] skillSlots = { new(), new(), new() };
        public ArenaUiSlot[] replacementSlots = { new(), new(), new() };
        public int replacementCount = 2;
        public ArenaUiCard[] loadoutChoices = { new(), new(), new() };
        public ArenaUiCard[] armoryOffers = { new(), new(), new() };
        public ArenaUiCard[] draftCards = { new(), new(), new() };
    }
}