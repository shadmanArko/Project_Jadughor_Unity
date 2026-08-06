namespace Systems.MineSystem.EnemySystem.Animation.Model
{
    public readonly struct EnemyAnimationMarkerEvent
    {
        public readonly string AnimationId;
        public readonly int Generation;
        public readonly int Marker;

        public EnemyAnimationMarkerEvent(
            string animationId,
            int generation,
            int marker)
        {
            AnimationId = animationId;
            Generation = generation;
            Marker = marker;
        }
    }
}
