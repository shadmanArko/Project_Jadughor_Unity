namespace Systems.MineSystem.MinePlayerSystem.Interface
{
    public interface IPlayerInteractionHandler
    {
        int Priority { get; }
        bool TryInteract();
    }
}
