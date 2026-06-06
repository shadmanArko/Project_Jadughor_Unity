using System;
using System.Collections.Generic;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Model;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Service
{
    public sealed class CollectableFactory : IDisposable
    {
        private readonly List<ICollectablePoolHandler> _handlers;
        private readonly CollectableSpriteResolver _spriteResolver;
        private readonly MinePlayerScriptable _player;
        private readonly Subject<CollectableModel> _spawned = new();

        public IObservable<CollectableModel> Spawned => _spawned;

        public CollectableFactory(
            List<ICollectablePoolHandler> handlers,
            CollectableSpriteResolver spriteResolver,
            MinePlayerScriptable player)
        {
            _handlers = handlers;
            _spriteResolver = spriteResolver;
            _player = player;
        }

        public bool CanSpawn(Item item)
        {
            return item != null &&
                   FindHandler(item) != null &&
                   _spriteResolver.Resolve(item, _player.region, _player.site) != null;
        }

        public bool TrySpawn(Item item, Vector3 position)
        {
            var handler = FindHandler(item);
            if (handler == null)
                return false;

            var sprite = _spriteResolver.Resolve(
                item,
                _player.region,
                _player.site);
            if (sprite == null)
                return false;

            try
            {
                var view = handler.Spawn(
                    new CollectableSpawnData(item, position, sprite));
                _spawned.OnNext(new CollectableModel(item, view, handler));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private ICollectablePoolHandler FindHandler(Item item)
        {
            for (var i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i].CanHandle(item))
                    return _handlers[i];
            }

            return null;
        }

        public void Dispose()
        {
            _spawned.Dispose();
        }
    }
}
