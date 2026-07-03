using System;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IPlaceableRuntime : IPausable
    {
        IPlaceableDamageView DamageView { get; }
        void Initialize(PlaceableSpawnContext context);
        void SetReleaseAction(Action<IPlaceableRuntime> releaseAction);
        void Release();
    }
}
