using System;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model
{
    /// <summary>
    /// One authored difficulty phase for the hedgehog boss. Phases are selected
    /// by remaining health: the active phase is the last entry whose threshold
    /// the boss has dropped to or below. Multipliers scale the config's base
    /// stats rather than restating them, so retuning the base values carries
    /// through every phase.
    /// </summary>
    [Serializable]
    public sealed class HedgehogBossPhaseData
    {
        [Tooltip("Remaining health percent at or below which this phase starts.")]
        [Range(0f, 100f)] public float healthThresholdPercent = 100f;

        [Tooltip("Multiplier applied to the boss's base move speed.")]
        [Min(0f)] public float moveSpeedMultiplier = 1f;

        [Tooltip("Multiplier applied to the boss's base damage.")]
        [Min(0f)] public float damageMultiplier = 1f;

        [Tooltip(
            "Multiplier applied to attack cooldowns. Values below 1 make the " +
            "boss attack more often.")]
        [Min(0.01f)] public float attackCooldownMultiplier = 1f;

        [Header("Attack Weights")]
        [Tooltip("Relative chance of Ground Smash when it is currently valid. Zero disables it for this phase.")]
        [Min(0f)] public float groundSmashWeight = 1f;
        [Tooltip("Relative chance of Spike Throw when it is currently valid. Zero disables it for this phase.")]
        [Min(0f)] public float spikeThrowWeight = 1f;
        [Tooltip("Relative chance of Roll Charge when it is currently valid. Zero disables it for this phase.")]
        [Min(0f)] public float chargeWeight = 1f;

        [Tooltip("Enables the rolling charge attack during this phase.")]
        public bool allowChargeAttack = true;
    }
}
