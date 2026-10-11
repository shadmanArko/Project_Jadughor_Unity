using Systems.MineSystem.EnemySystem.Enum;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemyStatusEffectSpec
    {
        public readonly StatusEffectType Type;
        public readonly float Duration;
        public readonly float Power;

        public EnemyStatusEffectSpec(
            StatusEffectType type,
            float duration,
            float power)
        {
            Type = type;
            Duration = duration;
            Power = power;
        }
    }
}
