namespace AnomalyArena
{
    public enum ArenaUiCommand
    {
        Start,
        ToggleArt,
        ChooseLoadout,
        TakeArmory,
        SkipArmory,
        ReplaceWeapon,
        ChooseCard,
        Reroll,
        ReplaceSkill,
        StartNextWave,
        ConfirmEarlyStart,
        Cancel,
        Pause,
        Resume,
        Restart,
        QuitToTitle,
        EnterPortal,
        OpenSupply,
        DismissWeaponIntro,
        ToggleBuildWalk,
        // The title's story mode: the V2 day campaign (V2Campaign). Start is the classic 15-wave run on the map already laid; the title's
        // open mode reaches it through ChooseMap.
        StartCampaign,
        // The open mode's map pick on the title: the index is the map, GameManager.OpenModeMaps (0 the Dunes, 1 the
        // Ruins, 2 at random); the classic 15-wave run then starts on it. Last, so the commands saved in the scenes keep their numbers.
        ChooseMap
    }
}