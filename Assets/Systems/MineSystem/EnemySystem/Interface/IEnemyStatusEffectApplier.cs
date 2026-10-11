using Systems.MineSystem.EnemySystem.Model;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyStatusEffectApplier
    {
        bool CanApply(EnemyStatusEffectSpec statusEffect);
        bool TryApply(EnemyStatusEffectSpec statusEffect);
    }
}
