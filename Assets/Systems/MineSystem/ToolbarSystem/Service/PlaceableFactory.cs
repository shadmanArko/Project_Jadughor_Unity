using System;
using System.Collections.Generic;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class PlaceableFactory : IPlaceableFactory, IInitializable
    {
        private sealed class Pool
        {
            public readonly PlaceableFactoryEntry Entry;
            public readonly Stack<GameObject> Available = new();
            public int Created;

            public Pool(PlaceableFactoryEntry entry)
            {
                Entry = entry;
            }
        }

        private readonly PlaceableFactoryCatalog _catalog;
        private readonly IPlaceableValidator _validator;
        private readonly DiContainer _container;
        private readonly Dictionary<string, Pool> _pools =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<IPlaceableRuntime, (Pool Pool, PlaceableSpawnContext Context)> _active = new();
        private Transform _root;

        public PlaceableFactory(
            PlaceableFactoryCatalog catalog,
            IPlaceableValidator validator,
            DiContainer container)
        {
            _catalog = catalog;
            _validator = validator;
            _container = container;
        }

        public void Initialize()
        {
            var root = new GameObject("Placed Objects");
            _root = root.transform;

            foreach (var entry in _catalog.Entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.id) ||
                    entry.prefab == null)
                    continue;

                var pool = new Pool(entry);
                _pools[entry.id.Trim()] = pool;
                var initialSize = Mathf.Max(0, entry.initialSize);
                if (entry.maximumSize > 0)
                {
                    initialSize = Mathf.Min(
                        initialSize,
                        entry.maximumSize);
                }
                for (var index = 0; index < initialSize; index++)
                    pool.Available.Push(Create(pool));
            }
        }

        public bool TrySpawn(
            PlaceableSpawnContext context,
            out IPlaceableRuntime runtime)
        {
            runtime = null;
            if (string.IsNullOrWhiteSpace(context.PlaceableId) ||
                !_pools.TryGetValue(context.PlaceableId.Trim(), out var pool))
                return false;

            GameObject instance;
            if (pool.Available.Count > 0)
                instance = pool.Available.Pop();
            else if (pool.Entry.maximumSize <= 0 ||
                     pool.Created < pool.Entry.maximumSize)
                instance = Create(pool);
            else
                return false;

            runtime = FindRuntime(instance);
            if (runtime == null)
            {
                instance.SetActive(false);
                pool.Available.Push(instance);
                Debug.LogError(
                    $"Placeable prefab '{pool.Entry.prefab.name}' requires an IPlaceableRuntime component.");
                return false;
            }

            runtime.SetReleaseAction(Despawn);
            runtime.Initialize(context);
            _active[runtime] = (pool, context);
            return true;
        }

        public void Despawn(IPlaceableRuntime runtime)
        {
            if (runtime == null || !_active.Remove(runtime, out var active))
                return;

            var behaviour = runtime as MonoBehaviour;
            if (behaviour == null)
                return;

            behaviour.gameObject.SetActive(false);
            behaviour.transform.SetParent(_root, false);
            active.Pool.Available.Push(behaviour.gameObject);
            _validator.Release(
                active.Context.CellPosition,
                active.Context.Profile,
                active.Context.InstanceId);
        }

        private GameObject Create(Pool pool)
        {
            var instance = _container.InstantiatePrefab(
                pool.Entry.prefab,
                _root);
            instance.SetActive(false);
            pool.Created++;
            return instance;
        }

        private static IPlaceableRuntime FindRuntime(GameObject instance)
        {
            var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IPlaceableRuntime runtime)
                    return runtime;
            }

            return null;
        }
    }
}
