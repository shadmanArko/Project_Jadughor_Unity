namespace Systems.MineSystem.PauseSystem.Signal
{
    public readonly struct PauseStateChangedSignal
    {
        public PauseStateChangedSignal(bool isPaused) => IsPaused = isPaused;

        public bool IsPaused { get; }
    }
}
