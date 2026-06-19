using Systems.MineSystem.InventorySystem.Model;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IDamageable
    {
        void ApplyDamage(float amount, Item source);
    }
}
