using Systems.MineSystem.BossLairSystem.Scriptable;

namespace Systems.MineSystem.BossLairSystem.Signal
{
    /// <summary>
    /// Raised once the player is standing in the arena and control has been
    /// handed back. This is the point where mine simulation should be suspended
    /// and the boss fight begins.
    /// </summary>
    public readonly struct BossLairEnteredSignal
    {
        public BossLairEnteredSignal(BossProfileScriptable profile) =>
            Profile = profile;

        public BossProfileScriptable Profile { get; }
    }
}
