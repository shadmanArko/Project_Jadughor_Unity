using Systems.MineSystem.InventorySystem.Model;

namespace Systems.MineSystem.InventorySystem.Interface
{
    public interface IItemizable
    {
        Item ToInventoryItem();
        Item ToCollectableItem();
    }
}