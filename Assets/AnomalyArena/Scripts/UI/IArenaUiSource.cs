namespace AnomalyArena
{
    public interface IArenaUiSource
    {
        ArenaUiSnapshot UiSnapshot { get; }
        void ExecuteUiCommand(ArenaUiCommand command, int index = -1);
    }
}
