using Systems.MineSystem.EnemySystem.Mob.BlackBat.Controller;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.View;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Model
{
    public sealed class BatPoolEntry
    {
        public GameObject Prefab { get; }
        public BatView View { get; }
        public BatController Controller { get; }

        public BatPoolEntry(
            GameObject prefab,
            BatView view,
            BatController controller)
        {
            Prefab = prefab;
            View = view;
            Controller = controller;
        }
    }
}
