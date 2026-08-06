using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Config;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Controller;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Model;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Mob.RattleSnake.Service
{
    public sealed class SnakePool : IInitializable, IDisposable
    {
        private readonly DiContainer _container;
        private readonly SnakeConfigScriptable _initialConfig;
        private readonly Dictionary<GameObject, Stack<SnakePoolEntry>> _available = new();
        private readonly Dictionary<SnakeController, SnakePoolEntry> _active = new();
        private readonly List<SnakePoolEntry> _all = new();
        private Transform _root;
        private bool _disposed;

        public SnakePool(
            DiContainer container,
            SnakeConfigScriptable initialConfig)
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

        public SnakePoolEntry Acquire(SnakeConfigScriptable config)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SnakePool));
            EnsureRoot();
            var stack = GetOrCreateStack(config.Prefab);
            var entry = stack.Count > 0 ? stack.Pop() : Create(config.Prefab);
            entry.View.gameObject.SetActive(true);
            _active.Add(entry.Controller, entry);
            return entry;
        }

        public void Release(SnakeController controller)
        {
            if (controller == null || !_active.Remove(controller, out var entry))
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

        private SnakePoolEntry Create(GameObject prefab)
        {
            var view = _container.InstantiatePrefabForComponent<SnakeView>(
                prefab,
                _root);
            var controller = _container.Instantiate<SnakeController>(
                new object[] { view });
            var entry = new SnakePoolEntry(prefab, view, controller);
            _all.Add(entry);
            return entry;
        }

        private Stack<SnakePoolEntry> GetOrCreateStack(GameObject prefab)
        {
            if (_available.TryGetValue(prefab, out var stack))
                return stack;
            stack = new Stack<SnakePoolEntry>();
            _available.Add(prefab, stack);
            return stack;
        }

        private void EnsureRoot()
        {
            if (_root != null)
                return;
            _root = new GameObject("Snake Pool").transform;
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
