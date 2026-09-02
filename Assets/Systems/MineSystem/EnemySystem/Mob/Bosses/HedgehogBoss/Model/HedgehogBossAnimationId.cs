namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model
{
    /// <summary>
    /// Animation ids the boss's <c>EnemyAnimationProfileScriptable</c> must
    /// author clips for. Idle/Move/Roar are enough for the lair-entry
    /// cutscene; combat clips land with the boss's behaviour pass.
    /// </summary>
    public static class HedgehogBossAnimationId
    {
        public const string Idle = "Hedgehog.Idle";
        public const string Move = "Hedgehog.Move";
        public const string Roar = "Hedgehog.Roar";
    }
}
