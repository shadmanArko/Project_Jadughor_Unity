namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemyTickContext
    {
        public readonly float FixedDeltaTime;

        public EnemyTickContext(float fixedDeltaTime)
        {
            FixedDeltaTime = fixedDeltaTime;
        }
    }
}
