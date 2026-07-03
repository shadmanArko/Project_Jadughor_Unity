using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.PauseSystem.Signal
{
    public readonly struct PausableUnregisteredSignal
    {
        public PausableUnregisteredSignal(IPausable pausable) =>
            Pausable = pausable;

        public IPausable Pausable { get; }
    }
}
