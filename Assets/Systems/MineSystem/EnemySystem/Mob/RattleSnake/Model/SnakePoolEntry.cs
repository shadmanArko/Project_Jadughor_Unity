using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Controller;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.View;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.RattleSnake.Model
{
    public sealed class SnakePoolEntry
    {
        public readonly GameObject Prefab;
        public readonly SnakeView View;
        public readonly SnakeController Controller;

        public SnakePoolEntry(
            GameObject prefab,
            SnakeView view,
            SnakeController controller)
        {
            Prefab = prefab;
            View = view;
            Controller = controller;
        }
    }
}
