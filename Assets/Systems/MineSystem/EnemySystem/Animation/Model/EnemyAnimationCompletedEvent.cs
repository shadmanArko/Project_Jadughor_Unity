namespace Systems.MineSystem.EnemySystem.Animation.Model
{
    public readonly struct EnemyAnimationCompletedEvent
    {
        public readonly string AnimationId;
        public readonly int Generation;

        public EnemyAnimationCompletedEvent(string animationId, int generation)
        {
            AnimationId = animationId;
            Generation = generation;
        }
    }
}
