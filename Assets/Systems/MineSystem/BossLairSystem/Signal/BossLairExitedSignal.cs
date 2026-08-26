using Systems.MineSystem.BossLairSystem.Scriptable;

namespace Systems.MineSystem.BossLairSystem.Signal
{
    /// <summary>
    /// Raised once the player is back in the mine and the arena has been torn
    /// down. Fired for every exit path, including death, so listeners can undo
    /// whatever they set up on entry.
    /// </summary>
    public readonly struct BossLairExitedSignal
    {
        public BossLairExitedSignal(
            BossProfileScriptable profile,
            bool bossDefeated,
            bool playerDied)
        {
            Profile = profile;
            BossDefeated = bossDefeated;
            PlayerDied = playerDied;
        }

        public BossProfileScriptable Profile { get; }
        public bool BossDefeated { get; }
        public bool PlayerDied { get; }
    }
}
