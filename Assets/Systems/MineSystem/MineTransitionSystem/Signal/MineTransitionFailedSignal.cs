using Systems.MineSystem.MineTransitionSystem.Model;

namespace Systems.MineSystem.MineTransitionSystem.Signal
{
    public struct MineTransitionFailedSignal
    {
        public MineTransitionRoute Route;
        public string Error;
    }
}
