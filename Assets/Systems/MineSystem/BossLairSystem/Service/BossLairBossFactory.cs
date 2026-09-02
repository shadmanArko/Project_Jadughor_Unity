using System;
using Systems.MineSystem.BossLairSystem.View;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Controller;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Creates and destroys the hedgehog boss instance living inside a built
    /// lair, mirroring how <see cref="BossLairFactory"/> owns the arena's own
    /// lifetime — built once per mine generation, destroyed when the mine
    /// regenerates, so runs without a boss pay nothing.
    /// </summary>
    /// <remarks>
    /// Only one boss variant exists today, so this factory is hedgehog-specific
    /// rather than a variant-keyed registry. Generalize when a second boss
    /// variant actually needs one.
    /// </remarks>
    public sealed class BossLairBossFactory : IDisposable
    {
        private readonly DiContainer _container;
        private readonly HedgehogBossView _prefab;
        private HedgehogBossController _active;
        private HedgehogBossView _instance;
        private bool _disposed;

        public BossLairBossFactory(DiContainer container, HedgehogBossView prefab)
        {
            _container = container;
            _prefab = prefab;
        }

        public HedgehogBossController Active => _active;

        public HedgehogBossController Create(
            BossLairView lairView,
            HedgehogBossConfigScriptable config)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BossLairBossFactory));
            Destroy();
            if (_prefab == null || lairView.bossSpawnPoint == null)
                return null;

            _instance = _container.InstantiatePrefabForComponent<HedgehogBossView>(
                _prefab, lairView.transform);
            var controller = new HedgehogBossController(_instance);
            var spawnPosition = lairView.bossSpawnPoint.position;
            var spawnCell = lairView.grid.WorldToCell(spawnPosition);
            controller.Initialize(new EnemyInitializeData(
                config,
                new GridPosition(spawnCell.x, spawnCell.y),
                spawnPosition));
            _active = controller;
            return controller;
        }

        public void Destroy()
        {
            _active?.Dispose();
            _active = null;
            if (_instance != null)
                UnityEngine.Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Destroy();
        }
    }
}
