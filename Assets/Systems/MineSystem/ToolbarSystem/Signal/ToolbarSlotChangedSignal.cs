using Systems.MineSystem.InventorySystem.Model;

namespace Systems.MineSystem.ToolbarSystem.Signal
{
    public struct ToolbarSlotChangedSignal
    {
        public int SlotNumber;
        public Item Item;
    }
}
