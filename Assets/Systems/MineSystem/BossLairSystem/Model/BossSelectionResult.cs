using Systems.MineSystem.BossLairSystem.Scriptable;

namespace Systems.MineSystem.BossLairSystem.Model
{
    /// <summary>
    /// Outcome of rolling for a boss gate in the current mine. A result without
    /// a profile is the normal case, not a failure: most runs have no boss.
    /// </summary>
    public readonly struct BossSelectionResult
    {
        private BossSelectionResult(BossProfileScriptable profile, string reason)
        {
            Profile = profile;
            Reason = reason;
        }

        public BossProfileScriptable Profile { get; }

        /// <summary>Why no boss was selected. Null on success.</summary>
        public string Reason { get; }

        public bool HasBoss => Profile != null;

        public static BossSelectionResult Selected(
            BossProfileScriptable profile) => new(profile, null);

        public static BossSelectionResult None(string reason) =>
            new(null, reason);
    }
}
