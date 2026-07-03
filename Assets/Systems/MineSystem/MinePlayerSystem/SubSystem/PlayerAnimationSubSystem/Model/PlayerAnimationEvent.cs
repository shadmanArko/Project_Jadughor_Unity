namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model
{
    public readonly struct PlayerAnimationMarkerEvent
    {
        public readonly string AnimationId;
        public readonly int Generation;
        public readonly int Marker;

        public PlayerAnimationMarkerEvent(
            string animationId,
            int generation,
            int marker)
        {
            AnimationId = animationId;
            Generation = generation;
            Marker = marker;
        }
    }

    public readonly struct PlayerAnimationCompletedEvent
    {
        public readonly string AnimationId;
        public readonly int Generation;

        public PlayerAnimationCompletedEvent(
            string animationId,
            int generation)
        {
            AnimationId = animationId;
            Generation = generation;
        }
    }
}
