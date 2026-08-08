using Systems.MineSystem.EnemySystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Config
{
    public abstract class EnemyConfigScriptable : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The broad enemy family used to select factories and shared systems.")]
        [SerializeField] private EnemyType enemyType;
        [Tooltip("Prefab spawned for this enemy variant.")]
        [SerializeField] private GameObject prefab;

        [Header("Core Stats")]
        [Tooltip("Maximum health this enemy has when spawned.")]
        [Min(1)] [SerializeField] private int maxHealth = 10;
        [Tooltip("Base damage applied by this enemy's contact or attack logic.")]
        [Min(0f)] [SerializeField] private float damage = 1f;
        [Tooltip("Movement speed used by this enemy's movement controller.")]
        [Min(0f)] [SerializeField] private float moveSpeed = 1f;

        [Header("Combat Ranges")]
        [Tooltip("Distance in tiles at which this enemy starts pursuing the player.")]
        [Min(0)] [SerializeField] private int aggroRangeInTiles = 4;
        [Tooltip("Distance in tiles at which this enemy can attack the player.")]
        [Min(0)] [SerializeField] private int attackRangeInTiles = 1;
        [Tooltip("Seconds between attack attempts.")]
        [Min(0f)] [SerializeField] private float attackCooldown = 1f;

        [Header("Spawn Rules")]
        [Tooltip("Movement category used by generic spawn validation.")]
        [SerializeField] private EnemyMovementType movementType =
            EnemyMovementType.Grounded;
        [Tooltip("Minimum Manhattan distance from the player for random spawns.")]
        [Min(0)] [SerializeField] private int minimumSpawnDistanceInTiles = 4;
        [Tooltip(
            "Maximum Manhattan distance from the player for random spawns. " +
            "Set to 0 to allow any distance beyond the minimum.")]
        [Min(0)] [SerializeField] private int maximumSpawnDistanceInTiles;
        [Tooltip("Requires a solid, non-broken cell below the spawn cell.")]
        [SerializeField] private bool requiresSolidGroundBelow = true;
        [Tooltip("Requires the spawn cell to be valid for this enemy's navigation.")]
        [SerializeField] private bool requiresPathValidation = true;
        [Tooltip("Checks the enemy prefab collider can fit before factory spawn.")]
        [SerializeField] private bool requiresPlacementValidation = true;
        [Tooltip(
            "Allows wave spawns to appear inside the camera viewport. " +
            "Use only for enemies with suitable spawn/despawn presentation.")]
        [SerializeField] private bool allowCameraVisibleWaveSpawn;

        [Header("Relocation")]
        [Tooltip(
            "Despawns this enemy and respawns it near the player once the " +
            "player has stayed far away for the delay below. Leave off to " +
            "keep the enemy where it spawned for the whole mine session.")]
        [SerializeField] private bool relocateWhenPlayerDistant;
        [Tooltip(
            "Manhattan distance in tiles beyond which the relocation timer " +
            "runs. Must exceed the enemy's chase exit range.")]
        [Min(0)] [SerializeField] private int relocationDistanceInTiles;
        [Tooltip(
            "Seconds the player must stay beyond the relocation distance " +
            "before the enemy relocates.")]
        [Min(0f)] [SerializeField] private float relocationDelaySeconds;
        [Tooltip(
            "Relocates this enemy when its own stuck recovery is exhausted, " +
            "instead of silently despawning it.")]
        [SerializeField] private bool relocateWhenStuck;
        [Tooltip(
            "Camera margin in tiles applied to the relocation respawn so the " +
            "enemy does not pop in on screen.")]
        [Min(0)] [SerializeField] private int relocationOutsideCameraMarginInTiles;

        public EnemyType EnemyType => enemyType;
        public abstract string VariantId { get; }
        public GameObject Prefab => prefab;
        public int MaxHealth => maxHealth;
        public float Damage => damage;
        public float MoveSpeed => moveSpeed;
        public int AggroRangeInTiles => aggroRangeInTiles;
        public int AttackRangeInTiles => attackRangeInTiles;
        public float AttackCooldown => attackCooldown;
        public EnemyMovementType MovementType => movementType;
        public int MinimumSpawnDistanceInTiles => minimumSpawnDistanceInTiles;
        public int MaximumSpawnDistanceInTiles => maximumSpawnDistanceInTiles;
        public bool RequiresSolidGroundBelow => requiresSolidGroundBelow;
        public bool RequiresPathValidation => requiresPathValidation;
        public bool RequiresPlacementValidation => requiresPlacementValidation;
        public bool AllowCameraVisibleWaveSpawn =>
            allowCameraVisibleWaveSpawn;
        public bool RelocateWhenPlayerDistant => relocateWhenPlayerDistant;
        public int RelocationDistanceInTiles => relocationDistanceInTiles;
        public float RelocationDelaySeconds => relocationDelaySeconds;
        public bool RelocateWhenStuck => relocateWhenStuck;
        public int RelocationOutsideCameraMarginInTiles =>
            relocationOutsideCameraMarginInTiles;

        public virtual bool Validate(out string error)
        {
            if (prefab == null)
            {
                error = $"{name} requires an enemy prefab.";
                return false;
            }

            if (maxHealth <= 0 || moveSpeed < 0f || attackRangeInTiles < 0 ||
                aggroRangeInTiles < attackRangeInTiles ||
                minimumSpawnDistanceInTiles < 0 ||
                maximumSpawnDistanceInTiles < 0)
            {
                error = $"{name} contains invalid enemy stats or ranges.";
                return false;
            }
            if (maximumSpawnDistanceInTiles > 0 &&
                maximumSpawnDistanceInTiles < minimumSpawnDistanceInTiles)
            {
                error =
                    $"{name} maximum spawn distance " +
                    $"({maximumSpawnDistanceInTiles}) must be 0 or at least " +
                    $"the minimum ({minimumSpawnDistanceInTiles}).";
                return false;
            }

            if (relocateWhenPlayerDistant)
            {
                if (relocationDelaySeconds <= 0f)
                {
                    error =
                        $"{name} requires a positive relocation delay when " +
                        "distance relocation is enabled.";
                    return false;
                }
                if (relocationDistanceInTiles <= aggroRangeInTiles)
                {
                    error =
                        $"{name} relocation distance " +
                        $"({relocationDistanceInTiles}) must exceed the " +
                        $"aggro range ({aggroRangeInTiles}).";
                    return false;
                }
                // Without a spawn window the respawn can land anywhere in the
                // mine, which defeats the point of relocating near the player.
                if (maximumSpawnDistanceInTiles <= 0)
                {
                    error =
                        $"{name} requires a maximum spawn distance when " +
                        "distance relocation is enabled.";
                    return false;
                }
                if (maximumSpawnDistanceInTiles >= relocationDistanceInTiles)
                {
                    error =
                        $"{name} maximum spawn distance " +
                        $"({maximumSpawnDistanceInTiles}) must be closer than " +
                        $"the relocation distance ({relocationDistanceInTiles}) " +
                        "or the enemy will relocate again immediately.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
