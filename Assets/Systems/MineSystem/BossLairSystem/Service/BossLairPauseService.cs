using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.PauseSystem.Service;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Awaitable gate that suspends in-flight lair transitions while the game is
    /// paused by modal UI. Owned by the boss lair so the feature does not reach
    /// into another system's pause service.
    /// </summary>
    public sealed class BossLairPauseService : IDisposable
    {
        private readonly PauseGate _gate = new();

        public void Pause() => _gate.Pause();
        public void Resume() => _gate.Resume();
        public UniTask WaitAsync() => _gate.WaitAsync();
        public void Dispose() => _gate.Dispose();
    }
}
