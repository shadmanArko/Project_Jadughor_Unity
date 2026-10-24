using System;

namespace Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Model
{
    public sealed class FadeTransitionModel : IDisposable
    {
        public bool IsVisible { get; private set; }
        public void SetVisible(bool visible) => IsVisible = visible;
        public void Dispose() => IsVisible = false;
    }
}
