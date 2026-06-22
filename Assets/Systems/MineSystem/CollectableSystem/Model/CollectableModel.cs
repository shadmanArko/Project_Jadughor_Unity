using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.View;
using Systems.MineSystem.InventorySystem.Model;
using UniRx;

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
        public IDisposable AttractionDelaySubscription { get; set; }
        public IReadOnlyReactiveProperty<bool> IsAttractionAvailable =>
            _isAttractionAvailable;

        private readonly ReactiveProperty<bool> _isAttractionAvailable =
            new(false);

        public CollectableModel(
            Item item,
            CollectableView view,
            ICollectablePoolHandler poolHandler)
        {
            Item = item;
            View = view;
            PoolHandler = poolHandler;
        }

        public void EnableAttraction()
        {
            _isAttractionAvailable.Value = true;
            View.SetCollectionEnabled(true);
        }

        public void Dispose()
        {
            TriggerSubscription?.Dispose();
            TriggerSubscription = null;
            AttractionDelaySubscription?.Dispose();
            AttractionDelaySubscription = null;
            Target = null;
            IsBeingPulled = false;
            _isAttractionAvailable.Dispose();
        }
    }
}
