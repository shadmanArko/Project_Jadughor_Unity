using Systems.MineSystem.InventorySystem.Model;

namespace Systems.MineSystem.InventorySystem.Interface
{
    public interface IInventoryService
    {
        bool CanAdd(Item item);
        bool TryAdd(Item item);
        void LeftClick(int slotIndex);
        void RightClick(int slotIndex);
        void TrashHeldStack();
        bool TryRemoveOne(int slotIndex, Item expectedItem);
    }
}
