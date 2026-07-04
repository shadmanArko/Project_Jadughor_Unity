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
        [Header("Variant")]
        [SerializeField] private SlimeVariant slimeVariant;
        [SerializeField] private EnemyAnimationProfileScriptable animationProfile;
        [SerializeField] private Color slimeColor = Color.white;

        [Header("Behaviour")]
        [Min(0f)] [SerializeField] private float idleDuration = 1f;
        [Min(1)] [SerializeField] private int patrolRangeInTiles = 3;
        [Min(1)] [SerializeField] private int maxFallDistanceInTiles = 8;
        [Min(0)] [SerializeField] private int maxTeleportAttempts = 4;
        [Min(1)] [SerializeField] private int destinationRetries = 4;
        [Min(0.001f)] [SerializeField] private float positionTolerance = 0.05f;
        [Min(0f)] [SerializeField] private float groundProbeDistance = 0.1f;
        [SerializeField] private LayerMask groundLayerMask;

        [Header("Status Effect")]
        [SerializeField] private StatusEffectType statusEffectType;
        [Min(0f)] [SerializeField] private float statusEffectDuration;
        [Min(0f)] [SerializeField] private float statusEffectPower;

        [Header("Pooling")]
        [Min(0)] [SerializeField] private int initialPoolSize = 2;

        public SlimeVariant SlimeVariant => slimeVariant;
        public override string VariantId => slimeVariant.ToString();
        public EnemyAnimationProfileScriptable AnimationProfile => animationProfile;
        public Color SlimeColor => slimeColor;
        public float IdleDuration => idleDuration;
        public int PatrolRangeInTiles => patrolRangeInTiles;
        public int MaxFallDistanceInTiles => maxFallDistanceInTiles;
        public int MaxTeleportAttempts => maxTeleportAttempts;
        public int DestinationRetries => destinationRetries;
        public float PositionTolerance => positionTolerance;
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
            error = null;
            return true;
        }
    }
}
