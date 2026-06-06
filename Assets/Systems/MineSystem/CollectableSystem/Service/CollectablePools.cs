using Systems.MineSystem.CollectableSystem.View;
using Zenject;

namespace Systems.MineSystem.CollectableSystem.Service
{
    public abstract class CollectablePool : MonoMemoryPool<CollectableView>
    {
        protected override void OnDespawned(CollectableView item)
        {
            item.ResetView();
            base.OnDespawned(item);
        }
    }

    public sealed class ResourceCollectablePool : CollectablePool
    {
    }

    public sealed class ArtifactCollectablePool : CollectablePool
    {
    }

    public sealed class CellPlaceableCollectablePool : CollectablePool
    {
    }

    public sealed class WallPlaceableCollectablePool : CollectablePool
    {
    }
}
