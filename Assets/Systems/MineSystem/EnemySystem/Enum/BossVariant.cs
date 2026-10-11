using System;

namespace Systems.MineSystem.EnemySystem.Enum
{
    /// <summary>
    /// Concrete boss identities. All bosses share
    /// <see cref="EnemyType.Boss"/>, so this is what distinguishes them in
    /// config validation, spawn tables and gate selection.
    /// </summary>
    [Serializable]
    public enum BossVariant
    {
        Hedgehog
    }
}
