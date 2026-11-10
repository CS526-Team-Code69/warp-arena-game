namespace AnomalyArena
{
    /// <summary>The run owns all values and commands. The HUD only displays a reusable snapshot.</summary>
    public interface IArenaUiSource
    {
        ArenaUiSnapshot UiSnapshot { get; }
        void ExecuteUiCommand(ArenaUiCommand command, int index = -1);
    }
}
