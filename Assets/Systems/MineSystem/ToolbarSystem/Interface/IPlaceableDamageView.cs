using System;
using Systems.MineSystem.Damage;
using Systems.MineSystem.InventorySystem.Interface;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IPlaceableDamageView : IDamageable, IItemizable
    {
        bool DamageEnabled { get; }
        IObservable<float> DamageRequested { get; }
        void SetDamageEnabled(bool enabled);
    }
}
