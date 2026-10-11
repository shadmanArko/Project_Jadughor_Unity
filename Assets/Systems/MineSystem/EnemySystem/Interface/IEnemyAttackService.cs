using Systems.MineSystem.EnemySystem.Model;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyAttackService
    {
        bool TryAttack(float damage, EnemyStatusEffectSpec statusEffect);
    }
}
