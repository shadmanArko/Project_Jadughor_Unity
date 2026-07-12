using Systems.MineSystem.EnemySystem.Animation.Scriptable;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Mob.Slime.Enum;
using Systems.MineSystem.EnemySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.Config
{
    [CreateAssetMenu(fileName = "SlimeConfig", menuName = "Enemy/Slime Config")]
    public sealed class SlimeConfigScriptable : EnemyConfigScriptable
    {
        [Header("Variant And Presentation")]
        [Tooltip("Concrete slime variant represented by this config asset.")]
        [SerializeField] private SlimeVariant slimeVariant;
        [Tooltip("Animation profile containing the states and sprites for this slime.")]
        [SerializeField] private EnemyAnimationProfileScriptable animationProfile;
        [Tooltip("Tint applied to the slime renderer when spawned.")]
        [SerializeField] private Color slimeColor = Color.white;

        [Header("Idle And Patrol")]
        [Tooltip("Seconds the slime waits before making its next idle decision.")]
        [Min(0f)] [SerializeField] private float idleDuration = 1f;
        [Tooltip("Maximum horizontal patrol distance in tiles from its current cell.")]
        [Min(1)] [SerializeField] private int patrolRangeInTiles = 3;
        [Tooltip("Number of destination attempts before giving up on a move choice.")]
        [Min(1)] [SerializeField] private int destinationRetries = 4;
        [Tooltip("World-space tolerance used when deciding the slime reached a target.")]
        [Min(0.001f)] [SerializeField] private float positionTolerance = 0.05f;
        [Tooltip("Extra seconds added to estimated movement time before treating the slime as stuck.")]
        [Min(0f)] [SerializeField] private float movementStuckBufferSeconds = 1.5f;
        [Tooltip("Minimum allowed movement duration before stuck recovery can trigger.")]
        [Min(0.01f)] [SerializeField] private float minimumMovementTimeoutSeconds = 0.75f;

        [Header("Movement And Grounding")]
        [Tooltip("Maximum number of open tiles the slime may fall during pathing.")]
        [Min(1)] [SerializeField] private int maxFallDistanceInTiles = 8;
        [Tooltip("Distance used by the ground probe beneath the slime collider.")]
        [Min(0f)] [SerializeField] private float groundProbeDistance = 0.1f;
        [Tooltip("Physics layers considered ground by slime movement checks.")]
        [SerializeField] private LayerMask groundLayerMask;

        [Header("Teleport Behavior")]
        [Tooltip("Maximum attempts to find a valid teleport destination.")]
        [Min(0)] [SerializeField] private int maxTeleportAttempts = 4;
        [Tooltip("Minimum teleport landing distance from the player in tiles.")]
        [Min(1)] [SerializeField] private int minimumTeleportDistanceInTiles = 2;
        [Tooltip("Maximum teleport landing distance from the player in tiles.")]
        [Min(1)] [SerializeField] private int maximumTeleportDistanceInTiles = 5;
        [Tooltip("Distance at which the slime stops chasing and returns to idle.")]
        [Min(0)] [SerializeField] private int chaseExitRangeInTiles = 8;
        [Tooltip("Distance at which idle logic may consider teleporting closer.")]
        [Min(0)] [SerializeField] private int teleportTriggerDistanceInTiles = 10;
        [Tooltip("Chance that an eligible idle decision chooses teleporting.")]
        [Range(0f, 1f)] [SerializeField] private float teleportChance = 0.4f;
        [Tooltip("Seconds after a teleport before normal relocation can teleport again.")]
        [Min(0f)] [SerializeField] private float teleportCooldownSeconds = 6f;

        [Header("Attack Contact")]
        [Tooltip("World-space distance required before the slime can start or apply an attack.")]
        [Min(0f)] [SerializeField] private float attackContactDistance = 0.5f;

        [Header("Status Effect")]
        [Tooltip("Status effect applied by slime attacks, if any.")]
        [SerializeField] private StatusEffectType statusEffectType;
        [Tooltip("Duration in seconds for the applied status effect.")]
        [Min(0f)] [SerializeField] private float statusEffectDuration;
        [Tooltip("Power or magnitude for the applied status effect.")]
        [Min(0f)] [SerializeField] private float statusEffectPower;

        [Header("Pooling")]
        [Tooltip("Number of slime instances pre-created for this variant.")]
        [Min(0)] [SerializeField] private int initialPoolSize = 2;

        public SlimeVariant SlimeVariant => slimeVariant;
        public override string VariantId => slimeVariant.ToString();
        public EnemyAnimationProfileScriptable AnimationProfile => animationProfile;
        public Color SlimeColor => slimeColor;
        public float IdleDuration => idleDuration;
        public int PatrolRangeInTiles => patrolRangeInTiles;
        public int MaxFallDistanceInTiles => maxFallDistanceInTiles;
        public int MaxTeleportAttempts => maxTeleportAttempts;
        public int MinimumTeleportDistanceInTiles =>
            minimumTeleportDistanceInTiles;
        public int MaximumTeleportDistanceInTiles =>
            maximumTeleportDistanceInTiles;
        public int ChaseExitRangeInTiles => chaseExitRangeInTiles;
        public int TeleportTriggerDistanceInTiles =>
            teleportTriggerDistanceInTiles;
        public float TeleportChance => teleportChance;
        public float TeleportCooldownSeconds => teleportCooldownSeconds;
        public float AttackContactDistance => attackContactDistance;
        public int DestinationRetries => destinationRetries;
        public float PositionTolerance => positionTolerance;
        public float MovementStuckBufferSeconds => movementStuckBufferSeconds;
        public float MinimumMovementTimeoutSeconds => minimumMovementTimeoutSeconds;
        public float GroundProbeDistance => groundProbeDistance;
        public LayerMask GroundLayerMask => groundLayerMask;
        public int InitialPoolSize => initialPoolSize;
        public EnemyStatusEffectSpec StatusEffect => new(
            statusEffectType,
            statusEffectDuration,
            statusEffectPower);

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
                return false;
            if (EnemyType != EnemyType.Slime)
            {
                error = $"{name} must use EnemyType.Slime.";
                return false;
            }
            if (animationProfile == null)
            {
                error = $"{name} requires an enemy animation profile.";
                return false;
            }
            if (minimumTeleportDistanceInTiles < 1)
            {
                error = $"{name} requires a minimum teleport distance of at least one tile.";
                return false;
            }
            if (maximumTeleportDistanceInTiles <
                minimumTeleportDistanceInTiles)
            {
                error = $"{name} maximum teleport distance " +
                        $"({maximumTeleportDistanceInTiles}) must be at least " +
                        $"its minimum ({minimumTeleportDistanceInTiles}).";
                return false;
            }
            if (chaseExitRangeInTiles <= AggroRangeInTiles)
            {
                error = $"{name} chase exit range ({chaseExitRangeInTiles}) " +
                        $"must exceed aggro range ({AggroRangeInTiles}).";
                return false;
            }
            if (teleportTriggerDistanceInTiles < chaseExitRangeInTiles)
            {
                error = $"{name} teleport trigger distance " +
                        $"({teleportTriggerDistanceInTiles}) must be at least " +
                        $"the chase exit range ({chaseExitRangeInTiles}).";
                return false;
            }
            if (teleportChance < 0f || teleportChance > 1f)
            {
                error = $"{name} teleport chance ({teleportChance}) must be " +
                        "between zero and one.";
                return false;
            }
            if (teleportCooldownSeconds < 0f)
            {
                error =
                    $"{name} teleport cooldown must be zero or greater.";
                return false;
            }
            if (attackContactDistance < 0f)
            {
                error =
                    $"{name} attack contact distance must be zero or greater.";
                return false;
            }
            if (minimumMovementTimeoutSeconds <= 0f)
            {
                error =
                    $"{name} requires a positive minimum movement timeout.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
