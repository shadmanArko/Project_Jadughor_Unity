using System;
using Systems.MineSystem.ToolbarSystem.Model;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IPlaceableRuntime
    {
        IPlaceableDamageView DamageView { get; }
        void Initialize(PlaceableSpawnContext context);
        void SetReleaseAction(Action<IPlaceableRuntime> releaseAction);
        void Release();
    }
}
