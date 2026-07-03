using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.PauseSystem.Signal
{
    public readonly struct PausableAffectationChangedSignal
    {
        public PausableAffectationChangedSignal(IPausable pausable) =>
            Pausable = pausable;

        public IPausable Pausable { get; }
    }
}
