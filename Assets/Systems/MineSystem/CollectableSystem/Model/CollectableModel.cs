using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.View;
using Systems.MineSystem.InventorySystem.Model;

namespace Systems.MineSystem.CollectableSystem.Model
{
    public sealed class CollectableModel : IDisposable
    {
        public Item Item { get; }
        public CollectableView View { get; }
        public ICollectablePoolHandler PoolHandler { get; }
        public ICollector Target { get; set; }
        public bool IsBeingPulled { get; set; }
        public float NextCollectorScanTime { get; set; }
        public IDisposable TriggerSubscription { get; set; }

        public CollectableModel(
            Item item,
            CollectableView view,
            ICollectablePoolHandler poolHandler)
        {
            Item = item;
            View = view;
            PoolHandler = poolHandler;
        }

        public void Dispose()
        {
            TriggerSubscription?.Dispose();
            TriggerSubscription = null;
            Target = null;
            IsBeingPulled = false;
        }
    }
}
