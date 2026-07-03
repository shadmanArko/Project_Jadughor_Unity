using System;
using UniRx;

namespace Systems.MineSystem.InventorySystem.Model
{
    [Serializable]
    public sealed class InventoryModel : IDisposable
    {
        public const int MaximumSlots = 36;

        private readonly Subject<int> _slotChanged = new();
        private readonly Subject<Item> _itemCollected = new();
        private readonly ReactiveProperty<InventoryStack> _heldStack = new();
        private readonly ReactiveProperty<bool> _isOpen = new(false);

        public InventorySlot[] Slots { get; }
        public InventoryStack HeldStack
        {
            get => _heldStack.Value;
            set => _heldStack.Value = value;
        }

        public IReadOnlyReactiveProperty<InventoryStack> HeldStackChanged =>
            _heldStack;
        public IReadOnlyReactiveProperty<bool> IsOpen => _isOpen;
        public IObservable<int> SlotChanged => _slotChanged;
        public IObservable<Item> ItemCollected => _itemCollected;

        public InventoryModel()
        {
            Slots = new InventorySlot[MaximumSlots];
            for (var i = 0; i < Slots.Length; i++)
                Slots[i] = new InventorySlot();
        }

        public void SetOpen(bool value)
        {
            _isOpen.Value = value;
        }

        public void NotifySlotChanged(int index)
        {
            _slotChanged.OnNext(index);
        }

        public void NotifyHeldStackChanged()
        {
            _heldStack.SetValueAndForceNotify(_heldStack.Value);
        }

        public void NotifyItemCollected(Item item)
        {
            if (item != null)
                _itemCollected.OnNext(item);
        }

        public void Dispose()
        {
            _slotChanged.Dispose();
            _itemCollected.Dispose();
            _heldStack.Dispose();
            _isOpen.Dispose();
        }
    }
}
