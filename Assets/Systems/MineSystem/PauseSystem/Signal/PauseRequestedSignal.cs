using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.PauseSystem.Signal
{
    public readonly struct PauseRequestedSignal
    {
        public PauseRequestedSignal(IPauser pauser) => Pauser = pauser;

        public IPauser Pauser { get; }
    }
}
