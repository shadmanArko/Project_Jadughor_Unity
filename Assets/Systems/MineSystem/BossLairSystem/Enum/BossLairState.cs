namespace Systems.MineSystem.BossLairSystem.Enum
{
    /// <summary>
    /// Lifecycle of a boss lair visit. Used to reject re-entrant transitions
    /// and to decide whether lair-owned work should run.
    /// </summary>
    public enum BossLairState
    {
        /// <summary>No lair exists; the player is in the mine.</summary>
        Idle,

        /// <summary>Entry choreography is running.</summary>
        Entering,

        /// <summary>The player is in the arena and the fight can run.</summary>
        Active,

        /// <summary>
        /// The boss is defeated and its reward drops are available. The player
        /// may use the arena exit from this state, but cannot enter a cleared
        /// lair again after leaving it.
        /// </summary>
        Rewards,

        /// <summary>Exit choreography is running.</summary>
        Exiting
    }
}
