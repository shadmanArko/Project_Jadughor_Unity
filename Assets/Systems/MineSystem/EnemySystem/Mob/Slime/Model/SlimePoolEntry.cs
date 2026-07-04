using UnityEngine;
using Systems.MineSystem.EnemySystem.Mob.Slime.Controller;
using Systems.MineSystem.EnemySystem.Mob.Slime.View;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.Model
{
    public sealed class SlimePoolEntry
    {
        public readonly GameObject Prefab;
        public readonly SlimeView View;
        public readonly SlimeController Controller;

        public SlimePoolEntry(
            GameObject prefab,
            SlimeView view,
            SlimeController controller)
        {
            Prefab = prefab;
            View = view;
            Controller = controller;
        }
    }
}
