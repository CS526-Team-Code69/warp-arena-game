namespace AnomalyArena
{
    public enum ArenaUiState
    {
        Title,
        Loadout,
        WaveIntro,
        WaveActive,
        WaveEnd,
        Armory,
        Draft,
        ReplaceWeapon,
        ReplaceSkill,
        Ready,
        VoidTrial,
        FinalBoss,
        Paused,
        Result,
        SupplyDrop,
        WeaponIntro,

        // Short freeze after returning from the Void Trial mid-wave: the battlefield and the player hold still while the player absorbs
        // the void's power (document 12.5), then the wave resumes.
        TrialReturn,
        // World frozen for a short scripted moment the player only watches (first black hole seen).
        Cutscene,
        // Mid-wave card pick (a level gained during a wave): the world freezes while the player chooses or decides to pick later.
        LevelUp,
        // V2 day campaign (V2Campaign): the night (walk and mine, no fighting), the base panel (world frozen), a story beat (title card, CG).
        Night,
        Base,
        Story
    }
}