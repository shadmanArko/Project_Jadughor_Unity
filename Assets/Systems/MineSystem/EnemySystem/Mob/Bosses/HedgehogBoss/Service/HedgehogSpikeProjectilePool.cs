using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Controller;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Service
{
    /// <summary>Pool for transient Hedgehog spikes belonging to one lair boss.</summary>
    public sealed class HedgehogSpikeProjectilePool : IDisposable
    {
        private const string RootName = "Hedgehog Spikes";

        private readonly DiContainer _container;
        private readonly Transform _arenaRoot;
        private HedgehogSpikeProjectileView _prefab;
        private readonly Stack<HedgehogSpikeProjectileController> _available = new();
        private readonly List<HedgehogSpikeProjectileController> _active = new();
        private readonly List<HedgehogSpikeProjectileController> _all = new();
        private readonly List<HedgehogSpikeProjectileController> _tickSnapshot = new();
        private readonly GameObject _root;
        private bool _disposed;

        public HedgehogSpikeProjectilePool(
            DiContainer container,
            Transform arenaRoot,
            HedgehogSpikeProjectileView prefab,
            int initialSize)
        {
            _container = container;
            _arenaRoot = arenaRoot;
            _prefab = prefab;
            _root = new GameObject(RootName);
            _root.transform.SetParent(_arenaRoot, false);

            if (_prefab != null)
                Configure(_prefab, initialSize);
        }

        /// <summary>
        /// Applies the per-boss projectile configuration once, after the lair
        /// factory has selected the concrete boss profile. Construction cannot
        /// take this asset earlier because the generic IEnemy facade is created
        /// before its EnemyInitializeData is available.
        /// </summary>
        public void Configure(
            HedgehogSpikeProjectileView prefab,
            int initialSize)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HedgehogSpikeProjectilePool));
            if (_all.Count > 0)
                throw new InvalidOperationException(
                    "Hedgehog spike projectile pool has already been configured.");
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            _prefab = prefab;
            for (var i = 0; i < Mathf.Max(0, initialSize); i++)
                _available.Push(CreateEntry());
        }

        public bool TryLaunch(
            HedgehogBossConfigScriptable config,
            Vector2 position,
            Vector2 targetPosition,
            float phaseDamageMultiplier,
            IEnemyTargetProvider target,
            IEnemyAttackService attack)
        {
            if (_disposed || config == null || _prefab == null)
                return false;

            var direction = targetPosition - position;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            var controller = _available.Count > 0 ? _available.Pop() : CreateEntry();
            _active.Add(controller);
            controller.Launch(
                position,
                direction,
                config.SpikeProjectileSpeed,
                config.SpikeProjectileLifetimeSeconds,
                config.Damage * phaseDamageMultiplier * config.SpikeProjectileDamageMultiplier,
                config.StatusEffect,
                target,
                attack);
            return controller.IsActive;
        }

        public void OnFixedTick(float deltaTime)
        {
            if (_disposed || _active.Count == 0)
                return;

            _tickSnapshot.Clear();
            _tickSnapshot.AddRange(_active);
            for (var i = 0; i < _tickSnapshot.Count; i++)
                _tickSnapshot[i].OnFixedTick(deltaTime);
            _tickSnapshot.Clear();
        }

        public void Pause()
        {
            for (var i = 0; i < _active.Count; i++)
                _active[i].Pause();
        }

        public void Resume()
        {
            for (var i = 0; i < _active.Count; i++)
                _active[i].Resume();
        }

        public void ReleaseAll()
        {
            _tickSnapshot.Clear();
            _tickSnapshot.AddRange(_active);
            for (var i = 0; i < _tickSnapshot.Count; i++)
                _tickSnapshot[i].Release();
            _tickSnapshot.Clear();
        }

        private HedgehogSpikeProjectileController CreateEntry()
        {
            if (_prefab == null)
                throw new InvalidOperationException(
                    "HedgehogSpikeProjectilePool requires a projectile prefab.");

            var view = _container.InstantiatePrefabForComponent<HedgehogSpikeProjectileView>(
                _prefab, _root.transform);
            if (!view.ValidateReferences())
            {
                UnityEngine.Object.Destroy(view.gameObject);
                throw new InvalidOperationException(
                    "Hedgehog spike projectile prefab is not configured.");
            }
            view.ResetRuntime();
            view.gameObject.SetActive(false);
            var controller = new HedgehogSpikeProjectileController(view, Return);
            _all.Add(controller);
            return controller;
        }

        private void Return(HedgehogSpikeProjectileController controller)
        {
            if (controller == null || _disposed)
                return;
            _active.Remove(controller);
            if (!_available.Contains(controller))
                _available.Push(controller);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _active.Clear();
            _available.Clear();
            _tickSnapshot.Clear();
            for (var i = 0; i < _all.Count; i++)
                _all[i].Dispose();
            _all.Clear();
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }
    }
}
