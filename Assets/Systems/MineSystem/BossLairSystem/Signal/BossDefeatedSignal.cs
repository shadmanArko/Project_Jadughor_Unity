using System;
using Systems.MineSystem.EnemySystem.Enum;

namespace Systems.MineSystem.BossLairSystem.Signal
{
    /// <summary>
    /// Raised by a lair-owned boss only after its death presentation has
    /// completed and its rewards have been created. This is intentionally
    /// separate from <c>EnemyDiedSignal</c>, which belongs to EnemyManager's
    /// pooled mine-enemy lifecycle.
    /// </summary>
    public readonly struct BossDefeatedSignal
    {
        public BossDefeatedSignal(BossVariant variant, Guid bossId)
        {
            Variant = variant;
            BossId = bossId;
        }

        public BossVariant Variant { get; }
        public Guid BossId { get; }
    }
}
