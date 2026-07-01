namespace Systems.MineSystem.MineTransitionSystem.Model
{
    public readonly struct MineTransitionResult
    {
        public MineTransitionState State { get; }
        public string Error { get; }
        public bool Succeeded => State == MineTransitionState.Completed;

        public MineTransitionResult(MineTransitionState state, string error = null)
        {
            State = state;
            Error = error;
        }

        public static MineTransitionResult Completed() =>
            new(MineTransitionState.Completed);
        public static MineTransitionResult Cancelled() =>
            new(MineTransitionState.Cancelled);
        public static MineTransitionResult Unavailable(string error) =>
            new(MineTransitionState.Unavailable, error);
        public static MineTransitionResult Failed(string error) =>
            new(MineTransitionState.Failed, error);
    }
}
