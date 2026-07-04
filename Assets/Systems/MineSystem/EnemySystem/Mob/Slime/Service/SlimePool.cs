using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Mob.Slime.Config;
using Systems.MineSystem.EnemySystem.Mob.Slime.Controller;
using Systems.MineSystem.EnemySystem.Mob.Slime.Model;
using Systems.MineSystem.EnemySystem.Mob.Slime.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.Service
{
    public sealed class SlimePool : IInitializable, IDisposable
    {
        private readonly DiContainer _container;
        private readonly SlimeConfigScriptable _initialConfig;
        private readonly Dictionary<GameObject, Stack<SlimePoolEntry>> _available = new();
        private readonly Dictionary<SlimeController, SlimePoolEntry> _active = new();
        private readonly List<SlimePoolEntry> _all = new();
        private Transform _root;
        private bool _disposed;

        public SlimePool(
            DiContainer container,
            SlimeConfigScriptable initialConfig)
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

        public SlimePoolEntry Acquire(SlimeConfigScriptable config)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SlimePool));
            EnsureRoot();
            var stack = GetOrCreateStack(config.Prefab);
            var entry = stack.Count > 0 ? stack.Pop() : Create(config.Prefab);
            entry.View.gameObject.SetActive(true);
            _active.Add(entry.Controller, entry);
            return entry;
        }

        public void Release(SlimeController controller)
        {
            if (controller == null || !_active.Remove(controller, out var entry))
                return;
            controller.Release();
            entry.View.gameObject.SetActive(false);
            entry.View.transform.SetParent(_root, false);
            _available[entry.Prefab].Push(entry);
        }

        private SlimePoolEntry Create(GameObject prefab)
        {
            var view = _container.InstantiatePrefabForComponent<SlimeView>(
                prefab,
                _root);
            var controller = _container.Instantiate<SlimeController>(
                new object[] { view });
            var entry = new SlimePoolEntry(prefab, view, controller);
            _all.Add(entry);
            return entry;
        }

        private Stack<SlimePoolEntry> GetOrCreateStack(GameObject prefab)
        {
            if (_available.TryGetValue(prefab, out var stack))
                return stack;
            stack = new Stack<SlimePoolEntry>();
            _available.Add(prefab, stack);
            return stack;
        }

        private void EnsureRoot()
        {
            if (_root != null)
                return;
            _root = new GameObject("Slime Pool").transform;
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
