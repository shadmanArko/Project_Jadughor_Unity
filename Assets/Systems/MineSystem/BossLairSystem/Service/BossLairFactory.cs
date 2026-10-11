using System;
using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Creates and destroys the boss lair instance and owns its lifetime. Built
    /// during mine generation when a boss gate was placed, and destroyed when the
    /// mine regenerates — so entering costs nothing and re-entry is free, while
    /// runs without a boss pay nothing at all.
    /// </summary>
    /// <remarks>
    /// The prefab is passed as a constructor argument rather than bound as a
    /// <c>BossLairView</c>, because a container-lifetime binding would create the
    /// arena at startup on every run regardless of whether a boss exists.
    /// </remarks>
    public sealed class BossLairFactory : IDisposable
    {
        private const string RootName = "Boss Lair";

        private readonly DiContainer _container;
        private readonly BossLairView _prefab;
        private readonly BossLairConfig _config;
        private BossLairView _instance;
        private GameObject _root;
        private bool _disposed;

        public BossLairFactory(
            DiContainer container,
            BossLairView prefab,
            BossLairConfig config)
        {
            _container = container;
            _prefab = prefab;
            _config = config;
        }

        public BossLairView Active => _instance;

        /// <summary>
        /// Instantiates the arena with its local cell (0,0) at the interior's
        /// bottom-left corner, which is the contract the shell generator and the
        /// decor pass paint against.
        /// </summary>
        public BossLairView Create(BossLairPlacement placement)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BossLairFactory));
            if (_prefab == null)
                throw new InvalidOperationException(
                    "BossLairFactory requires a lair prefab.");
            if (!placement.IsValid)
                throw new ArgumentException(
                    "Boss lair placement is not valid.", nameof(placement));
            if (_instance != null)
                return _instance;

            _root = new GameObject(RootName)
            {
                transform =
                {
                    position = new Vector3(
                        placement.RootWorldPosition.x,
                        placement.RootWorldPosition.y,
                        0f)
                }
            };

            try
            {
                _instance = _container.InstantiatePrefabForComponent<BossLairView>(
                    _prefab, _root.transform);
                _instance.transform.localPosition = Vector3.zero;
                _instance.ValidateReferences();
                placement.GetPaddedWorldBounds(
                    _config.CameraBoundsPaddingInCells,
                    _config.CameraBoundsBottomPaddingInCells,
                    out var paddedCenter,
                    out var paddedSize);
                _instance.ApplyCameraBounds(paddedCenter, paddedSize);
                // Dormant until the player actually enters.
                _instance.SetArenaActive(false);
                return _instance;
            }
            catch
            {
                // Never leave a half-built arena behind: its colliders would sit
                // in the world with no owner.
                Destroy();
                throw;
            }
        }

        public void Destroy()
        {
            _instance = null;
            if (_root == null)
                return;
            UnityEngine.Object.Destroy(_root);
            _root = null;
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
