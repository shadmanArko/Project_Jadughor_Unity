using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.Stalactite.Script;
using Systems.MineSystem.Mine.Service.Stalagmite.Script;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Service;
using UnityEngine;
using Zenject;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    public sealed class CaveFormationPool : IDisposable
    {
        private sealed class Pool
        {
            public readonly GameObject Prefab;
            public readonly Stack<CaveFormationRuntime> Available = new();
            public readonly int MaximumSize;
            public readonly Type RuntimeType;
            public int Created;

            public Pool(
                GameObject prefab,
                int maximumSize,
                Type runtimeType)
            {
                Prefab = prefab;
                MaximumSize = maximumSize;
                RuntimeType = runtimeType;
            }
        }

        private readonly DiContainer _container;
        private readonly MineView _mineView;
        private readonly CaveFormationConfig _config;
        private readonly PlaceableRuntimeRegistry _registry;
        private readonly Dictionary<CaveFormationRuntime, Pool> _active = new();
        private readonly Transform _root;
        private readonly Pool _stalactites;
        private readonly Pool _stalagmites;

        public event Action<CaveFormationRuntime> Despawned;

        public CaveFormationPool(
            DiContainer container,
            MineView mineView,
            CaveFormationConfig config,
            PlaceableRuntimeRegistry registry)
        {
            _container = container;
            _mineView = mineView;
            _config = config;
            _registry = registry;
            _root = new GameObject("Cave Formations").transform;
            _stalactites = new Pool(
                config.stalactitePrefab,
                config.stalactiteMaxPoolSize,
                typeof(StalactiteRuntime));
            _stalagmites = new Pool(
                config.stalagmitePrefab,
                config.stalagmiteMaxPoolSize,
                typeof(StalagmiteRuntime));
            Prewarm(_stalactites, config.stalactiteInitialPoolSize);
            Prewarm(_stalagmites, config.stalagmiteInitialPoolSize);
        }

        public CaveFormationRuntime SpawnStalactite(
            MineData mineData,
            Cell cell,
            string rootCellId)
        {
            return Spawn(_stalactites, mineData, cell, rootCellId);
        }

        public CaveFormationRuntime SpawnStalagmite(
            MineData mineData,
            Cell cell,
            string rootCellId)
        {
            return Spawn(_stalagmites, mineData, cell, rootCellId);
        }

        public void Despawn(CaveFormationRuntime runtime)
        {
            if (runtime == null ||
                !_active.Remove(runtime, out var pool))
                return;

            GlobalEventBus.Fire(new PausableUnregisteredSignal(runtime));
            _registry.Unregister(runtime);
            Despawned?.Invoke(runtime);
            runtime.transform.SetParent(_root, false);
            runtime.gameObject.SetActive(false);
            pool.Available.Push(runtime);
        }

        public void DespawnAll()
        {
            var active = new List<CaveFormationRuntime>(_active.Keys);
            foreach (var runtime in active)
                Despawn(runtime);
        }

        public void Dispose()
        {
            DespawnAll();
        }

        private CaveFormationRuntime Spawn(
            Pool pool,
            MineData mineData,
            Cell cell,
            string rootCellId)
        {
            if (pool.Prefab == null || cell == null)
                return null;

            var runtime = Get(pool);
            if (runtime == null)
                return null;

            var cellPosition = cell.GetPosition();
            var worldPosition =
                _mineView.grid.GetCellCenterWorld(cellPosition);
            var context = new PlaceableSpawnContext(
                pool.RuntimeType.Name,
                cell.Id,
                null,
                null,
                cellPosition,
                worldPosition,
                PileDriverDirection.Down);

            runtime.SetReleaseAction(value =>
            {
                if (value is CaveFormationRuntime formation)
                    Despawn(formation);
            });
            runtime.InitializeFormation(
                context,
                mineData,
                _mineView,
                _config,
                cell,
                rootCellId);
            _active[runtime] = pool;
            _registry.RegisterCell(runtime, cellPosition);
            GlobalEventBus.Fire(new PausableRegisteredSignal(runtime));
            return runtime;
        }

        private CaveFormationRuntime Get(Pool pool)
        {
            if (pool.Available.Count > 0)
                return pool.Available.Pop();

            if (pool.MaximumSize > 0 && pool.Created >= pool.MaximumSize)
                return null;

            return Create(pool);
        }

        private void Prewarm(Pool pool, int count)
        {
            if (pool.Prefab == null)
                return;

            var target = Mathf.Max(0, count);
            if (pool.MaximumSize > 0)
                target = Mathf.Min(target, pool.MaximumSize);

            for (var i = 0; i < target; i++)
                pool.Available.Push(Create(pool));
        }

        private CaveFormationRuntime Create(Pool pool)
        {
            var instance = _container.InstantiatePrefab(pool.Prefab, _root);
            instance.SetActive(false);
            var runtime =
                instance.GetComponentInChildren(pool.RuntimeType, true)
                as CaveFormationRuntime;
            if (runtime == null)
                runtime = instance.AddComponent(pool.RuntimeType)
                    as CaveFormationRuntime;
            pool.Created++;
            return runtime;
        }
    }
}
