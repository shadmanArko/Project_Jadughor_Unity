using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.PauseSystem.Service;

namespace Systems.MineSystem.MineTransitionSystem.Service
{
    public sealed class MineTransitionPauseService : IDisposable
    {
        private readonly PauseGate _gate = new();
        public void Pause() => _gate.Pause();
        public void Resume() => _gate.Resume();
        public UniTask WaitAsync() => _gate.WaitAsync();
        public void Dispose() => _gate.Dispose();
    }
}
