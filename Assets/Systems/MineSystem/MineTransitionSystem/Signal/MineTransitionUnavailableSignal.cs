using Systems.MineSystem.MineTransitionSystem.Model;

namespace Systems.MineSystem.MineTransitionSystem.Signal
{
    public struct MineTransitionUnavailableSignal
    {
        public MineTransitionRoute Route;
        public string Reason;
    }
}
