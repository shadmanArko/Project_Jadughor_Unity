using System;
using Systems.MineSystem.InventorySystem.Model;
using UniRx;
using Zenject;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    public sealed class ArtifactInventoryModel : IInitializable, IDisposable
    {
        private readonly ReactiveCollection<Item> _items = new();
        private readonly ReactiveProperty<Item> _hoveredItem = new();

        public IReadOnlyReactiveCollection<Item> Items => _items;
        public IReadOnlyReactiveProperty<Item> HoveredItem => _hoveredItem;
        public int Capacity { get; }

        public ArtifactInventoryModel()
        {
            Capacity = 30;
        }

        public bool TryAdd(Item item)
        {
            if (item == null || _items.Count >= Capacity)
                return false;

            _items.Add(item);
            return true;
        }

        public bool Remove(Item item)
        {
            return item != null && _items.Remove(item);
        }

        public void SetHoveredItem(Item item)
        {
            _hoveredItem.Value = item;
        }

        public void Initialize()
        {
        }

        public void Dispose()
        {
            _hoveredItem.Dispose();
            _items.Dispose();
        }
    }
}
