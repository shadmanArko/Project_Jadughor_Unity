using Systems.MineSystem.EnemySystem.Mob.GreenSlime.Controller;
using Systems.MineSystem.EnemySystem.Mob.GreenSlime.View;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.GreenSlime.Model
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
