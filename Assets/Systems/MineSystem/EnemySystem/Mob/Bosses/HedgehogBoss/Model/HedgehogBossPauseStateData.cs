namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model
{
    public sealed class HedgehogBossPauseStateData
    {
        public bool HasSnapshot;
        public bool MovementWasPlaying;
        public float AnimatorSpeed;

        public void Clear()
        {
            HasSnapshot = false;
            MovementWasPlaying = false;
            AnimatorSpeed = 1f;
        }
    }
}
