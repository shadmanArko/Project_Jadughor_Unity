using System;
using Systems.MineSystem.Damage;
using Systems.MineSystem.InventorySystem.Interface;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IPlaceableDamageView : IDamageable, IItemizable
    {
        IObservable<float> DamageRequested { get; }
    }
}
