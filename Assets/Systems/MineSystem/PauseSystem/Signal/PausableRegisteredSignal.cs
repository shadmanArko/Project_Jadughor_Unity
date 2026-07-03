using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.PauseSystem.Signal
{
    public readonly struct PausableRegisteredSignal
    {
        public PausableRegisteredSignal(IPausable pausable) =>
            Pausable = pausable;

        public IPausable Pausable { get; }
    }
}
