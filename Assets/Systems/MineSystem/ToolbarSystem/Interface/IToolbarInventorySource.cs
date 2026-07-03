using System;
using Systems.MineSystem.InventorySystem.Model;
using UniRx;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IToolbarInventorySource
    {
        IObservable<int> SlotChanged { get; }
        IReadOnlyReactiveProperty<bool> IsInventoryOpen { get; }
        InventoryStack GetStack(int slotIndex);
        Item GetItem(int slotIndex);
        bool IsOccupied(int slotIndex);
    }
}
