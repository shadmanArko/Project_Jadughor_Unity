using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemyPathStep
    {
        public readonly GridPosition Position;
        public readonly EnemyPathStepType Type;

        public EnemyPathStep(GridPosition position, EnemyPathStepType type)
        {
            Position = position;
            Type = type;
        }
    }
}
