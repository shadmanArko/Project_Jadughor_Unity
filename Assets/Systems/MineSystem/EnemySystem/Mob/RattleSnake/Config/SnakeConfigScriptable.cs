using Systems.MineSystem.EnemySystem.Animation.Scriptable;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Enum;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Model;
using Systems.MineSystem.EnemySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.RattleSnake.Config
{
    [CreateAssetMenu(fileName = "SnakeConfig", menuName = "Enemy/Snake Config")]
    public sealed class SnakeConfigScriptable : EnemyConfigScriptable
    {
        [Header("Variant And Presentation")]
        [Tooltip("Concrete snake variant represented by this config asset.")]
        [SerializeField] private SnakeVariant snakeVariant;
        [Tooltip("Animation profile containing the states and sprites for this snake.")]
        [SerializeField] private EnemyAnimationProfileScriptable animationProfile;
        [Tooltip("Tint applied to the snake renderer when spawned.")]
        [SerializeField] private Color snakeColor = Color.white;
        [Tooltip("How this snake responds when it touches an IDamageable placeable.")]
        [SerializeField] private PlaceableCollisionBehavior placeableCollisionBehavior;

        [Header("Detection And Engagement")]
        [Tooltip("World-space distance at which the snake notices the player.")]
        [Min(0f)] [SerializeField] private float aggroDistance = 1f;
        [Tooltip("Distance in tiles at which the snake stops chasing and returns to idle.")]
        [Min(0)] [SerializeField] private int chaseExitRangeInTiles = 8;

        [Header("Idle And Patrol")]
        [Tooltip("Seconds the snake waits before making its next idle decision.")]
        [Min(0f)] [SerializeField] private float idleDuration = 1f;
        [Tooltip("Maximum horizontal patrol distance in tiles from its current cell.")]
        [Min(1)] [SerializeField] private int patrolRangeInTiles = 3;
        [Tooltip("Number of destination attempts before giving up on a move choice.")]
        [Min(1)] [SerializeField] private int destinationRetries = 4;
        [Tooltip("World-space tolerance used when deciding the snake reached a target.")]
        [Min(0.001f)] [SerializeField] private float positionTolerance = 0.05f;
        [Tooltip("Extra seconds added to estimated movement time before treating the snake as stuck.")]
        [Min(0f)] [SerializeField] private float movementStuckBufferSeconds = 1.5f;
        [Tooltip("Minimum allowed movement duration before stuck recovery can trigger.")]
        [Min(0.01f)] [SerializeField] private float minimumMovementTimeoutSeconds = 0.75f;

        [Header("Movement And Grounding")]
        [Tooltip("Maximum number of open tiles the snake may fall during pathing.")]
        [Min(1)] [SerializeField] private int maxFallDistanceInTiles = 8;
        [Tooltip("Distance used by the ground probe beneath the snake collider.")]
        [Min(0f)] [SerializeField] private float groundProbeDistance = 0.1f;
        [Tooltip("Physics layers considered ground by snake movement checks.")]
        [SerializeField] private LayerMask groundLayerMask;

        [Header("Attack Contact")]
        [Tooltip("World-space distance required before the snake can start or apply an attack.")]
        [Min(0f)] [SerializeField] private float attackContactDistance = 0.1f;

        [Header("Status Effect")]
        [Tooltip("Status effect applied by snake attacks, if any.")]
        [SerializeField] private StatusEffectType statusEffectType;
        [Tooltip("Duration in seconds for the applied status effect.")]
        [Min(0f)] [SerializeField] private float statusEffectDuration;
        [Tooltip("Power or magnitude for the applied status effect.")]
        [Min(0f)] [SerializeField] private float statusEffectPower;

        [Header("Pooling")]
        [Tooltip("Number of snake instances pre-created for this variant.")]
        [Min(0)] [SerializeField] private int initialPoolSize = 2;

        public SnakeVariant SnakeVariant => snakeVariant;
        public override string VariantId => snakeVariant.ToString();
        public EnemyAnimationProfileScriptable AnimationProfile => animationProfile;
        public Color SnakeColor => snakeColor;
        public PlaceableCollisionBehavior PlaceableCollisionBehavior =>
            placeableCollisionBehavior;
        public float AggroDistance => aggroDistance;
        public int ChaseExitRangeInTiles => chaseExitRangeInTiles;
        public float IdleDuration => idleDuration;
        public int PatrolRangeInTiles => patrolRangeInTiles;
        public int DestinationRetries => destinationRetries;
        public float PositionTolerance => positionTolerance;
        public float MovementStuckBufferSeconds => movementStuckBufferSeconds;
        public float MinimumMovementTimeoutSeconds => minimumMovementTimeoutSeconds;
        public int MaxFallDistanceInTiles => maxFallDistanceInTiles;
        public float GroundProbeDistance => groundProbeDistance;
        public LayerMask GroundLayerMask => groundLayerMask;
        public float AttackContactDistance => attackContactDistance;
        public int InitialPoolSize => initialPoolSize;
        public EnemyStatusEffectSpec StatusEffect => new(
            statusEffectType,
            statusEffectDuration,
            statusEffectPower);

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
                return false;
            if (EnemyType != EnemyType.RattleSnake ||
                MovementType != EnemyMovementType.Crawling)
            {
                error = $"{name} must use EnemyType.RattleSnake and Crawling movement.";
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
                error = $"{name} animation profile is missing a required snake animation.";
                return false;
            }
            if (chaseExitRangeInTiles <= AggroRangeInTiles)
            {
                error = $"{name} chase exit range ({chaseExitRangeInTiles}) " +
                        $"must exceed aggro range ({AggroRangeInTiles}).";
                return false;
            }
            if (attackContactDistance < 0f)
            {
                error =
                    $"{name} attack contact distance must be zero or greater.";
                return false;
            }
            if (aggroDistance < 0f)
            {
                error =
                    $"{name} aggro distance must be zero or greater.";
                return false;
            }
            if (minimumMovementTimeoutSeconds <= 0f)
            {
                error =
                    $"{name} requires a positive minimum movement timeout.";
                return false;
            }
            if (RelocateWhenPlayerDistant &&
                RelocationDistanceInTiles <= chaseExitRangeInTiles)
            {
                error = $"{name} relocation distance " +
                        $"({RelocationDistanceInTiles}) must exceed the chase " +
                        $"exit range ({chaseExitRangeInTiles}).";
                return false;
            }
            error = null;
            return true;
        }

        private bool HasRequiredAnimations() =>
            animationProfile.TryGet(SnakeAnimationId.Spawn, out _) &&
            animationProfile.TryGet(SnakeAnimationId.Idle, out _) &&
            animationProfile.TryGet(SnakeAnimationId.Move, out _) &&
            animationProfile.TryGet(SnakeAnimationId.Attack, out _) &&
            animationProfile.TryGet(SnakeAnimationId.Hurt, out _) &&
            animationProfile.TryGet(SnakeAnimationId.Fall, out _) &&
            animationProfile.TryGet(SnakeAnimationId.Despawn, out _) &&
            animationProfile.TryGet(SnakeAnimationId.Death, out _);
    }
}
