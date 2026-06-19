using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Profile;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IItemActionHandler
    {
        ItemActionKind ActionKind { get; }
        void Activate(Item item, int slotIndex, ItemActionProfile profile);
        void Deactivate();
        void SetActionHeld(bool isHeld);
        bool TryExecute();
    }
}
