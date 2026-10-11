using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.PauseSystem.Signal
{
    public readonly struct PauseRequestedSignal
    {
        /// <param name="targetArea">
        /// Area to freeze, or <c>null</c> to freeze every area. Modal UI leaves
        /// this null so it pauses the whole game; the boss lair targets
        /// <see cref="PauseArea.MineView"/> so the arena keeps running.
        /// </param>
        public PauseRequestedSignal(IPauser pauser, PauseArea? targetArea = null)
        {
            Pauser = pauser;
            TargetArea = targetArea;
        }

        public IPauser Pauser { get; }
        public PauseArea? TargetArea { get; }
    }
}
