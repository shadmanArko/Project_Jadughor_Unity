namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model
{
    /// <summary>
    /// Animation ids the boss's <c>EnemyAnimationProfileScriptable</c> must
    /// author clips for. Idle/Move/Roar drive the lair-entry cutscene; the
    /// rest are here for the combat pass to use once it exists.
    /// </summary>
    public static class HedgehogBossAnimationId
    {
        public const string Idle = "Hedgehog.Idle";
        public const string Move = "Hedgehog.Move";
        public const string GroundSmash = "Hedgehog.GroundSmash";
        public const string SpikeThrow = "Hedgehog.SpikeThrow";
        public const string Roar = "Hedgehog.Roar";
        public const string Death = "Hedgehog.Death";
        public const string RollOut = "Hedgehog.RollOut";
    }
}
