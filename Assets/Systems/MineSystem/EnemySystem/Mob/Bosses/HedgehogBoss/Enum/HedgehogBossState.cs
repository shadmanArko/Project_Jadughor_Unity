namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Enum
{
    /// <summary>Runtime state of the lair-owned Hedgehog boss.</summary>
    public enum HedgehogBossState
    {
        Dormant,
        Idle,
        Chase,
        GroundSmash,
        SpikeThrow,
        RollWindup,
        RollCharge,
        Stunned,
        Hurt,
        Death,
        Defeated
    }
}
