using Systems.MineSystem.EnemySystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Config
{
    public abstract class EnemyConfigScriptable : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private EnemyType enemyType;
        [SerializeField] private GameObject prefab;

        [Header("Stats")]
        [Min(1)] [SerializeField] private int maxHealth = 10;
        [Min(0f)] [SerializeField] private float damage = 1f;
        [Min(0f)] [SerializeField] private float moveSpeed = 1f;
        [Min(0)] [SerializeField] private int aggroRangeInTiles = 4;
        [Min(0)] [SerializeField] private int attackRangeInTiles = 1;
        [Min(0f)] [SerializeField] private float attackCooldown = 1f;
        [Min(0)] [SerializeField] private int minimumSpawnDistanceInTiles = 4;

        public EnemyType EnemyType => enemyType;
        public abstract string VariantId { get; }
        public GameObject Prefab => prefab;
        public int MaxHealth => maxHealth;
        public float Damage => damage;
        public float MoveSpeed => moveSpeed;
        public int AggroRangeInTiles => aggroRangeInTiles;
        public int AttackRangeInTiles => attackRangeInTiles;
        public float AttackCooldown => attackCooldown;
        public int MinimumSpawnDistanceInTiles => minimumSpawnDistanceInTiles;

        public virtual bool Validate(out string error)
        {
            if (prefab == null)
            {
                error = $"{name} requires an enemy prefab.";
                return false;
            }

            if (maxHealth <= 0 || moveSpeed < 0f || attackRangeInTiles < 0 ||
                aggroRangeInTiles < attackRangeInTiles)
            {
                error = $"{name} contains invalid enemy stats or ranges.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
