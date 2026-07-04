using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Service;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemyAttackService : IEnemyAttackService
    {
        private readonly IPlayerDamageService _damage;
        private readonly IEnemyStatusEffectApplier _statusEffects;

        public EnemyAttackService(
            IPlayerDamageService damage,
            IEnemyStatusEffectApplier statusEffects)
        {
            _damage = damage;
            _statusEffects = statusEffects;
        }

        public bool TryAttack(float damage, EnemyStatusEffectSpec statusEffect)
        {
            if (damage <= 0f || !_statusEffects.CanApply(statusEffect) ||
                !_damage.ApplyDamage(damage))
                return false;
            return _statusEffects.TryApply(statusEffect);
        }
    }
}
