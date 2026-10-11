using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Config;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Controller;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Model;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Service
{
    public sealed class BatPool : IInitializable, IDisposable
    {
        private readonly DiContainer _container;
        private readonly BatConfigScriptable _initialConfig;
        private readonly Dictionary<GameObject, Stack<BatPoolEntry>> _available =
            new();
        private readonly Dictionary<BatController, BatPoolEntry> _active = new();
        private readonly List<BatPoolEntry> _all = new();

        private Transform _root;
        private bool _disposed;

        public BatPool(
            DiContainer container,
            BatConfigScriptable initialConfig)
        {
            _container = container;
            _initialConfig = initialConfig;
        }

        public void Initialize()
        {
            if (_initialConfig == null || _initialConfig.Prefab == null)
                return;
            EnsureRoot();
            var stack = GetOrCreateStack(_initialConfig.Prefab);
            for (var i = 0; i < _initialConfig.InitialPoolSize; i++)
            {
                var entry = Create(_initialConfig.Prefab);
                entry.View.gameObject.SetActive(false);
                stack.Push(entry);
            }
        }

        public BatPoolEntry Acquire(BatConfigScriptable config)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BatPool));
            EnsureRoot();
            var stack = GetOrCreateStack(config.Prefab);
            var entry = stack.Count > 0 ? stack.Pop() : Create(config.Prefab);
            entry.View.gameObject.SetActive(true);
            _active.Add(entry.Controller, entry);
            return entry;
        }

        public void Release(BatController controller)
        {
            if (controller == null ||
                !_active.Remove(controller, out var entry))
                return;
            controller.Release();
            if (entry.View == null)
            {
                _all.Remove(entry);
                return;
            }
            entry.View.gameObject.SetActive(false);
            entry.View.transform.SetParent(_root, false);
            _available[entry.Prefab].Push(entry);
        }

        private BatPoolEntry Create(GameObject prefab)
        {
            var view = _container.InstantiatePrefabForComponent<BatView>(
                prefab,
                _root);
            var controller = _container.Instantiate<BatController>(
                new object[] { view });
            var entry = new BatPoolEntry(prefab, view, controller);
            _all.Add(entry);
            return entry;
        }

        private Stack<BatPoolEntry> GetOrCreateStack(GameObject prefab)
        {
            if (_available.TryGetValue(prefab, out var stack))
                return stack;
            stack = new Stack<BatPoolEntry>();
            _available.Add(prefab, stack);
            return stack;
        }

        private void EnsureRoot()
        {
            if (_root != null)
                return;
            _root = new GameObject("Bat Pool").transform;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            for (var i = 0; i < _all.Count; i++)
            {
                var entry = _all[i];
                entry.Controller.Dispose();
                if (entry.View != null)
                    UnityEngine.Object.Destroy(entry.View.gameObject);
            }
            _all.Clear();
            _active.Clear();
            _available.Clear();
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
        }
    }
}
