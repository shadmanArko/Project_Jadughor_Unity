using System;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.ToolbarSystem.Interface;
using UniRx;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class ToolbarInventorySource : IToolbarInventorySource
    {
        private readonly InventoryModel _inventory;

        public IObservable<int> SlotChanged => _inventory.SlotChanged;
        public IReadOnlyReactiveProperty<bool> IsInventoryOpen => _inventory.IsOpen;

        public ToolbarInventorySource(InventoryModel inventory)
        {
            _inventory = inventory;
        }

        public InventoryStack GetStack(int slotIndex)
        {
            return IsValid(slotIndex) ? _inventory.Slots[slotIndex].Stack : null;
        }

        public Item GetItem(int slotIndex)
        {
            return GetStack(slotIndex)?.Representative;
        }

        public bool IsOccupied(int slotIndex)
        {
            return IsValid(slotIndex) && !_inventory.Slots[slotIndex].IsEmpty;
        }

        private static bool IsValid(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < InventoryModel.MaximumSlots;
        }
    }
}
