using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Animation.Scriptable;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View;
using Systems.MineSystem.EnemySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config
{
    /// <summary>
    /// Authored combat, projectile, phase, and reward values for the Hedgehog
    /// lair boss. Generic mine-enemy timing does not drive this singleton.
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
        private static readonly string[] RequiredAnimationIdsInternal =
        {
            HedgehogBossAnimationId.Idle,
            HedgehogBossAnimationId.Move,
            HedgehogBossAnimationId.GroundSmash,
            HedgehogBossAnimationId.SpikeThrow,
            HedgehogBossAnimationId.RollOut,
            HedgehogBossAnimationId.Roll,
            HedgehogBossAnimationId.Stunned,
            HedgehogBossAnimationId.Hurt,
            HedgehogBossAnimationId.Death
        };

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

        [Header("Recovery")]
        [Tooltip("Seconds the boss is vulnerable after an attack finishes.")]
        [Min(0f)] [SerializeField] private float attackRecoverySeconds = 0.6f;

        [Header("Ground Smash")]
        [Tooltip("World-space range checked when the Ground Smash marker fires.")]
        [Min(0.01f)] [SerializeField] private float groundSmashRange = 1.25f;
        [Tooltip("Duration of the small camera shake at the Ground Smash impact marker.")]
        [Min(0.01f)] [SerializeField] private float groundSmashScreenShakeDuration = 0.2f;
        [Tooltip("Camera-shake displacement used for Ground Smash.")]
        [Min(0f)] [SerializeField] private float groundSmashScreenShakeStrength = 0.025f;

        [Header("Spike Throw")]
        [Tooltip("Minimum horizontal world-space separation required to select Spike Throw.")]
        [Min(0f)] [SerializeField] private float spikeThrowMinimumRange = 1.5f;
        [Tooltip("Maximum horizontal world-space separation at which Spike Throw can be selected.")]
        [Min(0.01f)] [SerializeField] private float spikeThrowMaximumRange = 6f;
        [Tooltip("Pooled projectile prefab launched by the Spike Throw impact marker.")]
        [SerializeField] private HedgehogSpikeProjectileView spikeProjectilePrefab;
        [Tooltip("Number of spike projectiles prewarmed for this one boss encounter.")]
        [Min(1)] [SerializeField] private int initialSpikeProjectilePoolSize = 4;
        [Tooltip("World units per second travelled by a launched spike projectile.")]
        [Min(0.01f)] [SerializeField] private float spikeProjectileSpeed = 6f;
        [Tooltip("Seconds a spike remains alive before returning to its pool.")]
        [Min(0.01f)] [SerializeField] private float spikeProjectileLifetimeSeconds = 3f;
        [Tooltip("Multiplier applied to base boss damage when a spike hits the player.")]
        [Min(0f)] [SerializeField] private float spikeProjectileDamageMultiplier = 0.75f;

        [Header("Rolling Charge")]
        [Tooltip("Movement speed while rolling. Should exceed the base move speed.")]
        [Min(0f)] [SerializeField] private float chargeSpeed = 4f;
        [Tooltip("Maximum seconds a single roll lasts before the boss recovers.")]
        [Min(0f)] [SerializeField] private float chargeMaxDuration = 1.5f;
        [Tooltip("Seconds between roll attempts.")]
        [Min(0f)] [SerializeField] private float chargeCooldown = 4f;
        [Tooltip("Damage applied by a rolling contact hit.")]
        [Min(0f)] [SerializeField] private float chargeDamage = 2f;
        [Tooltip("Seconds the boss is stunned after rolling into a wall.")]
        [Min(0f)] [SerializeField] private float chargeWallStunSeconds = 1.5f;
        [Tooltip("Minimum horizontal world-space separation required to select Roll Charge.")]
        [Min(0.01f)] [SerializeField] private float chargeMinimumRange = 3f;
        [Tooltip("Maximum horizontal world-space separation at which Roll Charge can be selected.")]
        [Min(0.01f)] [SerializeField] private float chargeMaximumRange = 9f;
        [Tooltip("Forward world-space probe used to detect the arena wall during Roll Charge.")]
        [Min(0.01f)] [SerializeField] private float chargeObstacleProbeDistance = 0.45f;

        [Header("Phases")]
        [Tooltip(
            "Difficulty phases ordered from full health downwards. The first " +
            "entry should sit at 100 percent so there is always an active phase.")]
        [SerializeField] private List<HedgehogBossPhaseData> phases = new()
        {
            new HedgehogBossPhaseData()
        };

        [Header("Arena Collision")]
        [Tooltip("Physics layers considered ground by boss movement checks.")]
        [SerializeField] private LayerMask groundLayerMask;

        [Header("Rewards")]
        [Tooltip("Guaranteed rows always drop; optional rows roll independently.")]
        [SerializeField] private List<HedgehogBossRewardDropEntry> rewardDrops = new();

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
        public static IReadOnlyList<string> RequiredAnimationIds =>
            RequiredAnimationIdsInternal;
        public Color BossColor => bossColor;
        public float AggroDistance => aggroDistance;
        public float PositionTolerance => positionTolerance;
        public float AttackRecoverySeconds => attackRecoverySeconds;
        public float GroundSmashRange => groundSmashRange;
        public float GroundSmashScreenShakeDuration => groundSmashScreenShakeDuration;
        public float GroundSmashScreenShakeStrength => groundSmashScreenShakeStrength;
        public float SpikeThrowMinimumRange => spikeThrowMinimumRange;
        public float SpikeThrowMaximumRange => spikeThrowMaximumRange;
        public HedgehogSpikeProjectileView SpikeProjectilePrefab => spikeProjectilePrefab;
        public int InitialSpikeProjectilePoolSize => initialSpikeProjectilePoolSize;
        public float SpikeProjectileSpeed => spikeProjectileSpeed;
        public float SpikeProjectileLifetimeSeconds => spikeProjectileLifetimeSeconds;
        public float SpikeProjectileDamageMultiplier => spikeProjectileDamageMultiplier;
        public float ChargeSpeed => chargeSpeed;
        public float ChargeMaxDuration => chargeMaxDuration;
        public float ChargeCooldown => chargeCooldown;
        public float ChargeDamage => chargeDamage;
        public float ChargeWallStunSeconds => chargeWallStunSeconds;
        public float ChargeMinimumRange => chargeMinimumRange;
        public float ChargeMaximumRange => chargeMaximumRange;
        public float ChargeObstacleProbeDistance => chargeObstacleProbeDistance;
        public IReadOnlyList<HedgehogBossPhaseData> Phases => phases;
        public LayerMask GroundLayerMask => groundLayerMask;
        public IReadOnlyList<HedgehogBossRewardDropEntry> RewardDrops => rewardDrops;
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
            if (Damage <= 0f)
            {
                error = $"{name} requires positive base damage for Ground " +
                    "Smash and Spike Throw.";
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
            if (groundLayerMask.value == 0)
            {
                error = $"{name} requires a non-empty ground layer mask.";
                return false;
            }
            if (groundSmashRange <= 0f || groundSmashScreenShakeDuration <= 0f ||
                groundSmashScreenShakeStrength < 0f || spikeThrowMinimumRange < 0f ||
                spikeThrowMaximumRange < spikeThrowMinimumRange ||
                chargeMinimumRange <= 0f || chargeMaximumRange < chargeMinimumRange ||
                chargeObstacleProbeDistance <= 0f)
            {
                error = $"{name} contains invalid combat ranges.";
                return false;
            }
            if (spikeProjectilePrefab == null || initialSpikeProjectilePoolSize < 1 ||
                spikeProjectileSpeed <= 0f || spikeProjectileLifetimeSeconds <= 0f)
            {
                error = $"{name} requires a valid spike projectile setup.";
                return false;
            }
            if (ChargeCooldown < 0f || ChargeWallStunSeconds < 0f ||
                AttackCooldown < 0f || AttackRecoverySeconds < 0f)
            {
                error = $"{name} contains invalid recovery or cooldown values.";
                return false;
            }
            if (!ValidateAnimationIds(out error) || !ValidatePhases(out error) ||
                !ValidateRewards(out error))
                return false;
            return true;
        }

        private bool ValidateAnimationIds(out string error)
        {
            for (var i = 0; i < RequiredAnimationIdsInternal.Length; i++)
            {
                if (!animationProfile.TryGet(
                        RequiredAnimationIdsInternal[i], out var animation) ||
                    animation.animationSprites == null ||
                    animation.animationSprites.Count == 0)
                {
                    error = $"{name} requires populated animation '" +
                        $"{RequiredAnimationIdsInternal[i]}'.";
                    return false;
                }
            }
            error = null;
            return true;
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
                if (phase.groundSmashWeight < 0f || phase.spikeThrowWeight < 0f ||
                    phase.chargeWeight < 0f ||
                    phase.groundSmashWeight + phase.spikeThrowWeight +
                    (phase.allowChargeAttack ? phase.chargeWeight : 0f) <= 0f)
                {
                    error = $"{name} phase {i} requires a positive usable attack weight.";
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

        private bool ValidateRewards(out string error)
        {
            if (rewardDrops == null)
            {
                error = $"{name} reward drops list is null.";
                return false;
            }
            for (var i = 0; i < rewardDrops.Count; i++)
            {
                var entry = rewardDrops[i];
                if (entry == null)
                {
                    error = $"{name} reward drop {i} is empty.";
                    return false;
                }
                if (!entry.Validate(out var entryError))
                {
                    error = $"{name} reward drop {i} {entryError}.";
                    return false;
                }
            }
            error = null;
            return true;
        }
    }
}
