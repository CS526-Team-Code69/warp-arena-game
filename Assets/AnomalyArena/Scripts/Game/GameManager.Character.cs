namespace AnomalyArena
{
    /// <summary>
    /// The run's side of the character screen (V5CharacterScreen). While the screen is open the run is held exactly as the pause menu
    /// holds it, in the Paused phase: the clock stops, the run's sounds and music hold where they are, and nothing reaches the player.
    /// Only the pause menu itself stays hidden (V2Hud asks CharacterScreenHolds), and the menu keys are not read (ReadShownKeys asks
    /// V5CharacterScreen.HoldsInput), so Esc closes the screen instead of resuming or pausing. Closing it hands back the phase the pause
    /// would resume, the build phase's walk with its panel put away included.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The character screen holds the run (the Paused phase without the pause menu).</summary>
        public bool CharacterScreenHolds { get; private set; }

        /// <summary>The player is in play, the moments the character screen may open in: a run going on, the player alive and able to
        /// move (a wave, the void trial, the Tyrant, the story's day and night, the build phase walked with its panel put away or under
        /// its crate coming down). Every screen that freezes the run (a pick, a weapon card, a crate, the base, a cutscene, the pause
        /// menu, the result) has a phase in which the player cannot move, so the screen never opens over one.</summary>
        public bool CharacterScreenAllowed => State == GameState.Playing && CanPlayerMove && player && player.IsAlive && !CharacterScreenHolds;

        /// <summary>Holds the run for the character screen as the pause does; false when the screen may not open now.</summary>
        internal bool HoldForCharacterScreen()
        {
            if (!CharacterScreenAllowed) return false;
            resumePhase = RunPhase;
            resumeWalking = BuildWalking;
            snapshot.earlyStartConfirmation = false;
            // Set before the phase changes: the HUD draws the new phase at once (SetPhase) and must already leave the pause menu out.
            CharacterScreenHolds = true;
            SetPhase(ArenaUiState.Paused);
            return true;
        }

        /// <summary>The character screen closed: the run goes on from the phase it held, unless something else has taken the run over
        /// meanwhile (it ended, or another screen changed the phase).</summary>
        internal void ReleaseCharacterScreen()
        {
            if (!CharacterScreenHolds) return;
            CharacterScreenHolds = false;
            if (State != GameState.Playing || RunPhase != ArenaUiState.Paused) return;
            buildWalking = resumeWalking;
            SetPhase(resumePhase);
        }
    }
}
