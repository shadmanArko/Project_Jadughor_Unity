using Systems.MineSystem.InventorySystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Interface
{
    public interface ICollector
    {
        Transform CollectionPoint { get; }
        Collider2D CollectorCollider { get; }
        IReadOnlyReactiveProperty<float> PullRadius { get; }
        bool CanCollect(Item item);
        bool TryCollect(Item item);
    }
}
