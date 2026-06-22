using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Model;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.CollectableSystem.View;
using Systems.MineSystem.InventorySystem.Model;

namespace Systems.MineSystem.CollectableSystem.Service
{
    public sealed class CommonCollectablePoolHandler : ICollectablePoolHandler
    {
        private readonly CommonCollectablePool _pool;
        private readonly CollectableSystemConfig _config;

        public Type ItemType => typeof(Item);

        public CommonCollectablePoolHandler(
            CommonCollectablePool pool,
            CollectableSystemConfig config)
        {
            _pool = pool;
            _config = config;
        }

        public bool CanHandle(Item item) => item != null;

        public CollectableView Spawn(CollectableSpawnData data)
        {
            var view = _pool.Spawn();
            view.Present(
                data.Item,
                data.Position,
                data.Sprite,
                _config.droppedItemGravityScale);
            return view;
        }

        public void Despawn(CollectableView view)
        {
            _pool.Despawn(view);
        }
    }
}
