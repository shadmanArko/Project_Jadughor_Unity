using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Service
{
    /// <summary>
    /// Tracks how long each enemy has been beyond its configured relocation
    /// distance from the player. Pure bookkeeping — it decides *whether* an
    /// enemy should relocate, never how; <c>EnemyManager</c> owns the actual
    /// despawn/respawn so there is no dependency cycle between the two.
    /// </summary>
    public sealed class EnemyRelocationService : IDisposable
    {
        private readonly Dictionary<Guid, float> _dwellSeconds = new();
        private bool _disposed;

        /// <summary>
        /// Accumulates dwell time for one enemy and reports whether it is due
        /// to relocate. Returns true at most once per trigger — the timer is
        /// cleared so the caller cannot re-fire while the relocation runs.
        /// </summary>
        public bool ShouldRelocate(
            Guid enemyId,
            EnemyConfigScriptable config,
            GridPosition enemyPosition,
            GridPosition playerPosition,
            bool targetAvailable,
            float deltaTime)
        {
            if (_disposed || config == null ||
                !config.RelocateWhenPlayerDistant)
                return false;

            // No player to measure against (dead, unspawned): hold the timer
            // rather than relocating into an unknown position.
            if (!targetAvailable)
            {
                Forget(enemyId);
                return false;
            }

            var distance =
                Mathf.Abs(enemyPosition.X - playerPosition.X) +
                Mathf.Abs(enemyPosition.Y - playerPosition.Y);
            if (distance <= config.RelocationDistanceInTiles)
            {
                Forget(enemyId);
                return false;
            }

            _dwellSeconds.TryGetValue(enemyId, out var elapsed);
            elapsed += Mathf.Max(0f, deltaTime);
            if (elapsed < config.RelocationDelaySeconds)
            {
                _dwellSeconds[enemyId] = elapsed;
                return false;
            }

            _dwellSeconds.Remove(enemyId);
            return true;
        }

        /// <summary>
        /// Drops any tracked dwell time for an enemy. Must be called when an
        /// enemy is released so the dictionary cannot grow unbounded.
        /// </summary>
        public void Forget(Guid enemyId) => _dwellSeconds.Remove(enemyId);

        public void Clear() => _dwellSeconds.Clear();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _dwellSeconds.Clear();
        }
    }
}
