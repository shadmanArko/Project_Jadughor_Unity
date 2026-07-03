using System;
using Systems.MineSystem.CollectableSystem.Model;
using Systems.MineSystem.CollectableSystem.View;
using Systems.MineSystem.InventorySystem.Model;

namespace Systems.MineSystem.CollectableSystem.Interface
{
    public interface ICollectablePoolHandler
    {
        Type ItemType { get; }
        bool CanHandle(Item item);
        CollectableView Spawn(CollectableSpawnData data);
        void Despawn(CollectableView view);
    }
}
