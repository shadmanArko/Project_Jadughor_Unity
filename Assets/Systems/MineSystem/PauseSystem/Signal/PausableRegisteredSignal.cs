using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Interface;

namespace Systems.MineSystem.PauseSystem.Signal
{
    public readonly struct PausableRegisteredSignal
    {
        /// <param name="area">
        /// Which area the pausable belongs to. Defaults to
        /// <see cref="PauseArea.MineView"/>, so anything that does not opt out
        /// is frozen by an area-scoped mine pause.
        /// </param>
        public PausableRegisteredSignal(
            IPausable pausable,
            PauseArea area = PauseArea.MineView)
        {
            Pausable = pausable;
            Area = area;
        }

        public IPausable Pausable { get; }
        public PauseArea Area { get; }
    }
}
