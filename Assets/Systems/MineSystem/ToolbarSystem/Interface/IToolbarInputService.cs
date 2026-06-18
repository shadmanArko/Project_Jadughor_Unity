using System;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IToolbarInputService
    {
        IObservable<int> NavigationRequested { get; }
        void SetEnabled(bool enabled);
    }
}
