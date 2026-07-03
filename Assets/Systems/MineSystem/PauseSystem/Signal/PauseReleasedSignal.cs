using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.PauseSystem.Signal
{
    public readonly struct PauseReleasedSignal
    {
        public PauseReleasedSignal(IPauser pauser) => Pauser = pauser;

        public IPauser Pauser { get; }
    }
}
