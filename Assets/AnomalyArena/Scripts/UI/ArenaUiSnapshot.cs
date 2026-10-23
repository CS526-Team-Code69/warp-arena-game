using System;

namespace AnomalyArena
{
                                                                                                            
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

                                                                                       
        public bool buildWalking;

        public bool staminaExhausted;

                                                                                   
        public int[] bossImmunityIndices = { -1, -1, -1 };
        public float hp = 50f, maxHp = 50f, shield, stamina = 1f;
        public float energy, maxEnergy = 100f, xp, xpNeeded = 10f;
        public float waveTime, buildTime, charge01;
                                                                                                                                                    
        public float chargeWindowStart, chargeWindowEnd;
        public bool waveTimeExpired;
        public int waveBatch, waveLiving, wavePending;
        public float bossHp, bossMaxHp, bossShield;
                                                                      
        public float bossStagger;
        public int level = 1, maxLevel = 12, wave = 1, maxWaves = 14, kills, bankedDrafts, rerolls = 1;
        public int draftIndex = 1, draftCount;
        public string banner = "";
        public string hint = "";
        public string playerStatuses = "";
        public string bossImmunities = "";
        public string resultTitle = "RUN ENDED";
        public string resultDetails = "";

        public string buildSummary = "";

                                                              
        public string weaponIntroTitle = "", weaponIntroBody = "", weaponIntroHint = "", weaponIntroContinue = "";

        public int weaponIntroForm = -1;

                                                                                                   
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