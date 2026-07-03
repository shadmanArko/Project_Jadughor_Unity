using Systems.MineSystem.ToolbarSystem.Model;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IPlaceableFactory
    {
        bool TrySpawn(
            PlaceableSpawnContext context,
            out IPlaceableRuntime runtime);
        void Despawn(IPlaceableRuntime runtime);
    }
}
