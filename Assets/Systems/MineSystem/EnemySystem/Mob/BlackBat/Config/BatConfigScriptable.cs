using Systems.MineSystem.EnemySystem.Animation.Scriptable;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Enum;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Model;
using Systems.MineSystem.EnemySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Config
{
    [CreateAssetMenu(fileName = "BatConfig", menuName = "Enemy/Bat Config")]
    public sealed class BatConfigScriptable : EnemyConfigScriptable
    {
        [Header("Variant And Presentation")]
        [SerializeField] private BatVariant batVariant = BatVariant.BlackBat;
        [SerializeField] private EnemyAnimationProfileScriptable animationProfile;
        [SerializeField] private Color batColor = Color.white;

        [Header("Explore And Idle")]
        [Min(1)] [SerializeField] private int exploreRangeInTiles = 5;
        [Min(1)] [SerializeField] private int destinationRetries = 6;
        [Range(0f, 1f)] [SerializeField] private float idleChance = 0.25f;
        [Min(0f)] [SerializeField] private float minimumIdleDuration = 1.5f;
        [Min(0f)] [SerializeField] private float maximumIdleDuration = 3f;
        [Min(0f)] [SerializeField] private float idleCooldownSeconds = 5f;
        [Min(0f)] [SerializeField] private float decisionRetryDelay = 0.5f;

        [Header("Flight")]
        [Min(0.01f)] [SerializeField] private float chaseSpeed = 0.4f;
        [Min(0.001f)] [SerializeField] private float positionTolerance = 0.01f;
        [Min(0f)] [SerializeField] private float movementStuckBufferSeconds = 0.75f;
        [Min(0.01f)] [SerializeField] private float minimumMovementTimeoutSeconds = 0.5f;
        [Min(0f)] [SerializeField] private float flightWobbleAmplitude = 0.015f;
        [Min(0f)] [SerializeField] private float flightWobbleCyclesPerCell = 1f;
        [Min(0f)] [SerializeField] private float perchCeilingClearance = 0.01f;

        [Header("Combat")]
        [Min(0)] [SerializeField] private int chaseExitRangeInTiles = 8;
        [Min(0f)] [SerializeField] private float attackContactDistance = 0.25f;

        [Header("Diagnostics")]
        [SerializeField] private bool enableAiTraceLogs;
        [Min(0.1f)] [SerializeField] private float movementStallTimeoutSeconds = 1f;
        [SerializeField] private bool enableCombatDiagnosticLogs;
        [Min(0.02f)] [SerializeField]
        private float combatDiagnosticLogInterval = 0.1f;

        [Header("Status Effect")]
        [SerializeField] private StatusEffectType statusEffectType;
        [Min(0f)] [SerializeField] private float statusEffectDuration;
        [Min(0f)] [SerializeField] private float statusEffectPower;

        [Header("Pooling")]
        [Min(0)] [SerializeField] private int initialPoolSize = 4;

        public BatVariant BatVariant => batVariant;
        public override string VariantId => batVariant.ToString();
        public EnemyAnimationProfileScriptable AnimationProfile => animationProfile;
        public Color BatColor => batColor;
        public int ExploreRangeInTiles => exploreRangeInTiles;
        public int DestinationRetries => destinationRetries;
        public float IdleChance => idleChance;
        public float MinimumIdleDuration => minimumIdleDuration;
        public float MaximumIdleDuration => maximumIdleDuration;
        public float IdleCooldownSeconds => idleCooldownSeconds;
        public float DecisionRetryDelay => decisionRetryDelay;
        public float ChaseSpeed => chaseSpeed;
        public float PositionTolerance => positionTolerance;
        public float MovementStuckBufferSeconds => movementStuckBufferSeconds;
        public float MinimumMovementTimeoutSeconds => minimumMovementTimeoutSeconds;
        public float FlightWobbleAmplitude => flightWobbleAmplitude;
        public float FlightWobbleCyclesPerCell => flightWobbleCyclesPerCell;
        public float PerchCeilingClearance => perchCeilingClearance;
        public int ChaseExitRangeInTiles => chaseExitRangeInTiles;
        public float AttackContactDistance => attackContactDistance;
        public bool EnableAiTraceLogs => enableAiTraceLogs;
        public float MovementStallTimeoutSeconds => movementStallTimeoutSeconds;
        public bool EnableCombatDiagnosticLogs => enableCombatDiagnosticLogs;
        public float CombatDiagnosticLogInterval => combatDiagnosticLogInterval;
        public int InitialPoolSize => initialPoolSize;
        public EnemyStatusEffectSpec StatusEffect => new(
            statusEffectType,
            statusEffectDuration,
            statusEffectPower);

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
                return false;
            if (EnemyType != EnemyType.Bat ||
                MovementType != EnemyMovementType.Flying)
            {
                error = $"{name} must use EnemyType.Bat and Flying movement.";
                return false;
            }
            if (animationProfile == null ||
                animationProfile.AnimatorController == null)
            {
                error = $"{name} requires a configured animation profile.";
                return false;
            }
            if (!HasRequiredAnimations())
            {
                error = $"{name} animation profile is missing a required bat animation.";
                return false;
            }
            if (maximumIdleDuration < minimumIdleDuration ||
                idleCooldownSeconds < 0f ||
                chaseExitRangeInTiles < AggroRangeInTiles ||
                attackContactDistance < 0f ||
                movementStallTimeoutSeconds < 0.1f ||
                combatDiagnosticLogInterval < 0.02f ||
                chaseSpeed <= 0f ||
                positionTolerance <= 0f ||
                minimumMovementTimeoutSeconds <= 0f ||
                destinationRetries <= 0 ||
                exploreRangeInTiles <= 0)
            {
                error = $"{name} contains invalid bat timing, range, or movement values.";
                return false;
            }
            error = null;
            return true;
        }

        private bool HasRequiredAnimations() =>
            animationProfile.TryGet(BatAnimationId.Fly, out _) &&
            animationProfile.TryGet(BatAnimationId.Attack, out _) &&
            animationProfile.TryGet(BatAnimationId.Death, out _) &&
            animationProfile.TryGet(BatAnimationId.FlyToIdle, out _) &&
            animationProfile.TryGet(BatAnimationId.IdleToFly, out _) &&
            animationProfile.TryGet(BatAnimationId.Hurt, out _) &&
            animationProfile.TryGet(BatAnimationId.Idle, out _);
    }
}
