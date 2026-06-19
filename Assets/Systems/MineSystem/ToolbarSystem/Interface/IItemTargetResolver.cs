using System;
using Systems.MineSystem.ToolbarSystem.Model;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IItemTargetResolver
    {
        IObservable<ItemActionTarget> PointerTargetChanged { get; }
        ItemActionTarget ResolveDirectionalTarget(int range);
        ItemActionTarget ResolvePointerCell();
    }
}
