using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Model;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.CollectableSystem.View;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;

namespace Systems.MineSystem.CollectableSystem.Service
{
    public abstract class CollectablePoolHandler<TItem, TPool> :
        ICollectablePoolHandler
        where TItem : Item
        where TPool : CollectablePool
    {
        private readonly TPool _pool;
        private readonly CollectableSystemConfig _config;

        public Type ItemType => typeof(TItem);

        protected CollectablePoolHandler(
            TPool pool,
            CollectableSystemConfig config)
        {
            _pool = pool;
            _config = config;
        }

        public bool CanHandle(Item item) => item is TItem;

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

    public sealed class ResourceCollectablePoolHandler :
        CollectablePoolHandler<Resource, ResourceCollectablePool>
    {
        public ResourceCollectablePoolHandler(
            ResourceCollectablePool pool,
            CollectableSystemConfig config) : base(pool, config)
        {
        }
    }

    public sealed class ArtifactCollectablePoolHandler :
        CollectablePoolHandler<Artifact, ArtifactCollectablePool>
    {
        public ArtifactCollectablePoolHandler(
            ArtifactCollectablePool pool,
            CollectableSystemConfig config) : base(pool, config)
        {
        }
    }

    public sealed class CellPlaceableCollectablePoolHandler :
        CollectablePoolHandler<CellPlaceable, CellPlaceableCollectablePool>
    {
        public CellPlaceableCollectablePoolHandler(
            CellPlaceableCollectablePool pool,
            CollectableSystemConfig config) : base(pool, config)
        {
        }
    }

    public sealed class WallPlaceableCollectablePoolHandler :
        CollectablePoolHandler<WallPlaceable, WallPlaceableCollectablePool>
    {
        public WallPlaceableCollectablePoolHandler(
            WallPlaceableCollectablePool pool,
            CollectableSystemConfig config) : base(pool, config)
        {
        }
    }
}
