using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class NoOpEnemyStatusEffectApplier : IEnemyStatusEffectApplier
    {
        public bool CanApply(EnemyStatusEffectSpec statusEffect) =>
            statusEffect.Type == StatusEffectType.None;

        public bool TryApply(EnemyStatusEffectSpec statusEffect) =>
            statusEffect.Type == StatusEffectType.None;
    }
}
