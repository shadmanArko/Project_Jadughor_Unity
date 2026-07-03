using System.Collections.Generic;

namespace Systems.MineSystem.InventorySystem.Model
{
    public sealed class InventoryStack
    {
        private readonly List<Item> _items;

        public Item Representative => _items.Count > 0 ? _items[0] : null;
        public int Count => _items.Count;
        public bool IsEmpty => _items.Count == 0;

        public InventoryStack(Item item)
        {
            _items = new List<Item>(1) { item };
        }

        private InventoryStack(List<Item> items)
        {
            _items = items;
        }

        public void Add(Item item)
        {
            _items.Add(item);
        }

        public Item RemoveOne()
        {
            var index = _items.Count - 1;
            var item = _items[index];
            _items.RemoveAt(index);
            return item;
        }

        public InventoryStack TakeOne()
        {
            return new InventoryStack(RemoveOne());
        }

        public int TransferTo(InventoryStack destination, int maximumCount)
        {
            var transferCount = maximumCount - destination.Count;
            if (transferCount > Count)
                transferCount = Count;

            for (var i = 0; i < transferCount; i++)
                destination.Add(RemoveOne());

            return transferCount;
        }
    }
}
