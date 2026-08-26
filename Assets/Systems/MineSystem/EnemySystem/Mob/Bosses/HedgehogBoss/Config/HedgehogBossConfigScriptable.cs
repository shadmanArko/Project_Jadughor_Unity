using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Animation.Scriptable;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model;
using Systems.MineSystem.EnemySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config
{
    /// <summary>
    /// Authored values for the hedgehog boss. Behaviour scripts land in a later
    /// pass; this config exists so the numbers can be designed and reviewed
    /// first.
    /// </summary>
    /// <remarks>
    /// No pool size field, unlike the other mob configs: the boss is a
    /// singleton inside its lair, so pooling it would add a fourth copy of the
    /// pool/factory pair for no reuse benefit.
    /// <para>
    /// Relocation is inherited from the base but must stay disabled. The
    /// relocation leash despawns an enemy and respawns it near the player,
    /// which for a lair-bound boss would teleport it out of its arena.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "HedgehogBossConfig",
        menuName = "Enemy/Hedgehog Boss Config")]
    public sealed class HedgehogBossConfigScriptable : EnemyConfigScriptable
    {
        [Header("Variant And Presentation")]
        [Tooltip("Concrete boss identity represented by this config asset.")]
        [SerializeField] private BossVariant bossVariant = BossVariant.Hedgehog;
        [Tooltip("Animation profile containing the states and clips for this boss.")]
        [SerializeField] private EnemyAnimationProfileScriptable animationProfile;
        [Tooltip("Tint applied to the boss renderer when spawned.")]
        [SerializeField] private Color bossColor = Color.white;

        [Header("Detection And Engagement")]
        [Tooltip("World-space distance at which the boss notices the player.")]
        [Min(0f)] [SerializeField] private float aggroDistance = 4f;
        [Tooltip("World-space tolerance used when deciding the boss reached a target.")]
        [Min(0.001f)] [SerializeField] private float positionTolerance = 0.05f;
        [Tooltip("Seconds the boss waits before making its next decision.")]
        [Min(0f)] [SerializeField] private float idleDuration = 0.75f;

        [Header("Spawn And Death")]
        [Tooltip("Seconds the boss cannot be damaged for after its spawn animation.")]
        [Min(0f)] [SerializeField] private float spawnInvulnerabilitySeconds = 0.5f;
        [Tooltip("Seconds the death sequence plays before the fight is resolved.")]
        [Min(0f)] [SerializeField] private float deathDuration = 2f;

        [Header("Attack Timing")]
        [Tooltip("Seconds of telegraph before an attack becomes damaging.")]
        [Min(0f)] [SerializeField] private float attackWindupSeconds = 0.4f;
        [Tooltip("Seconds an attack stays damaging.")]
        [Min(0f)] [SerializeField] private float attackActiveSeconds = 0.2f;
        [Tooltip("Seconds the boss is vulnerable after an attack finishes.")]
        [Min(0f)] [SerializeField] private float attackRecoverySeconds = 0.6f;
        [Tooltip("World-space distance required before a melee attack can land.")]
        [Min(0f)] [SerializeField] private float attackContactDistance = 0.2f;

        [Header("Rolling Charge")]
        [Tooltip("Movement speed while rolling. Should exceed the base move speed.")]
        [Min(0f)] [SerializeField] private float chargeSpeed = 4f;
        [Tooltip("Seconds of windup before the roll launches.")]
        [Min(0f)] [SerializeField] private float chargeWindupSeconds = 0.5f;
        [Tooltip("Maximum seconds a single roll lasts before the boss recovers.")]
        [Min(0f)] [SerializeField] private float chargeMaxDuration = 1.5f;
        [Tooltip("Seconds between roll attempts.")]
        [Min(0f)] [SerializeField] private float chargeCooldown = 4f;
        [Tooltip("Damage applied by a rolling contact hit.")]
        [Min(0f)] [SerializeField] private float chargeDamage = 2f;
        [Tooltip("Seconds the boss is stunned after rolling into a wall.")]
        [Min(0f)] [SerializeField] private float chargeWallStunSeconds = 1.5f;

        [Header("Phases")]
        [Tooltip(
            "Difficulty phases ordered from full health downwards. The first " +
            "entry should sit at 100 percent so there is always an active phase.")]
        [SerializeField] private List<HedgehogBossPhaseData> phases = new()
        {
            new HedgehogBossPhaseData()
        };

        [Header("Movement And Grounding")]
        [Tooltip("Distance used by the ground probe beneath the boss collider.")]
        [Min(0f)] [SerializeField] private float groundProbeDistance = 0.1f;
        [Tooltip("Physics layers considered ground by boss movement checks.")]
        [SerializeField] private LayerMask groundLayerMask;

        [Header("Status Effect")]
        [Tooltip("Status effect applied by boss attacks, if any.")]
        [SerializeField] private StatusEffectType statusEffectType;
        [Tooltip("Duration in seconds for the applied status effect.")]
        [Min(0f)] [SerializeField] private float statusEffectDuration;
        [Tooltip("Power or magnitude for the applied status effect.")]
        [Min(0f)] [SerializeField] private float statusEffectPower;

        public BossVariant BossVariant => bossVariant;
        public override string VariantId => bossVariant.ToString();
        public EnemyAnimationProfileScriptable AnimationProfile => animationProfile;
        public Color BossColor => bossColor;
        public float AggroDistance => aggroDistance;
        public float PositionTolerance => positionTolerance;
        public float IdleDuration => idleDuration;
        public float SpawnInvulnerabilitySeconds => spawnInvulnerabilitySeconds;
        public float DeathDuration => deathDuration;
        public float AttackWindupSeconds => attackWindupSeconds;
        public float AttackActiveSeconds => attackActiveSeconds;
        public float AttackRecoverySeconds => attackRecoverySeconds;
        public float AttackContactDistance => attackContactDistance;
        public float ChargeSpeed => chargeSpeed;
        public float ChargeWindupSeconds => chargeWindupSeconds;
        public float ChargeMaxDuration => chargeMaxDuration;
        public float ChargeCooldown => chargeCooldown;
        public float ChargeDamage => chargeDamage;
        public float ChargeWallStunSeconds => chargeWallStunSeconds;
        public IReadOnlyList<HedgehogBossPhaseData> Phases => phases;
        public float GroundProbeDistance => groundProbeDistance;
        public LayerMask GroundLayerMask => groundLayerMask;
        public EnemyStatusEffectSpec StatusEffect => new(
            statusEffectType,
            statusEffectDuration,
            statusEffectPower);

        /// <summary>
        /// Resolves the active phase for a remaining-health percentage. Returns
        /// the last entry whose threshold has been reached, so authoring order
        /// is high health to low.
        /// </summary>
        public HedgehogBossPhaseData ResolvePhase(float remainingHealthPercent)
        {
            HedgehogBossPhaseData active = null;
            for (var i = 0; i < phases.Count; i++)
            {
                var candidate = phases[i];
                if (candidate == null ||
                    remainingHealthPercent > candidate.healthThresholdPercent)
                    continue;
                active = candidate;
            }
            return active ?? (phases.Count > 0 ? phases[0] : null);
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
                return false;
            if (EnemyType != EnemyType.Boss)
            {
                error = $"{name} must use EnemyType.Boss.";
                return false;
            }
            if (RelocateWhenPlayerDistant)
            {
                error =
                    $"{name} must not enable relocation: it would teleport a " +
                    "lair-bound boss out of its arena.";
                return false;
            }
            if (animationProfile == null ||
                animationProfile.AnimatorController == null)
            {
                error = $"{name} requires a configured animation profile.";
                return false;
            }
            if (chargeSpeed < MoveSpeed)
            {
                error =
                    $"{name} charge speed ({chargeSpeed}) should exceed the " +
                    $"base move speed ({MoveSpeed}).";
                return false;
            }
            if (phases == null || phases.Count == 0)
            {
                error = $"{name} requires at least one phase.";
                return false;
            }
            return ValidatePhases(out error);
        }

        private bool ValidatePhases(out string error)
        {
            var previousThreshold = float.MaxValue;
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase == null)
                {
                    error = $"{name} phase {i} is empty.";
                    return false;
                }
                if (phase.healthThresholdPercent > previousThreshold)
                {
                    error =
                        $"{name} phase {i} threshold " +
                        $"({phase.healthThresholdPercent}) must not exceed the " +
                        $"previous phase ({previousThreshold}). Author phases " +
                        "from high health to low.";
                    return false;
                }
                if (phase.attackCooldownMultiplier <= 0f)
                {
                    error =
                        $"{name} phase {i} requires a positive attack cooldown " +
                        "multiplier.";
                    return false;
                }
                previousThreshold = phase.healthThresholdPercent;
            }

            // Without a phase at full health the boss has no active phase the
            // moment it spawns.
            if (phases[0].healthThresholdPercent < 100f)
            {
                error =
                    $"{name} first phase must start at 100 percent health " +
                    $"(currently {phases[0].healthThresholdPercent}).";
                return false;
            }

            error = null;
            return true;
        }
    }
}
