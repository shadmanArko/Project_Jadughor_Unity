using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Mob.Slime.Config;
using Systems.MineSystem.EnemySystem.Mob.Slime.Enum;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.Model
{
    public sealed class SlimeModel : IDisposable
    {
        private bool _disposed;

        public SlimeConfigScriptable Config { get; private set; }
        public int CurrentHealth { get; private set; }
        public GridPosition CurrentGridPosition { get; private set; }
        public GridPosition Destination { get; private set; }
        public SlimeState CurrentState { get; private set; }
        public IReadOnlyList<EnemyPathStep> CachedPath { get; private set; }
        public int PathIndex { get; private set; }
        public int PathGeneration { get; private set; }
        public bool PathPending { get; private set; }
        public bool WasChasing { get; private set; }
        public bool IsAggro { get; private set; }
        public float AttackCooldownRemaining { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        public void Initialize(
            SlimeConfigScriptable config,
            GridPosition spawnPosition)
        {
            Config = config;
            CurrentHealth = config.MaxHealth;
            CurrentGridPosition = spawnPosition;
            Destination = spawnPosition;
            CurrentState = SlimeState.Spawn;
            CachedPath = null;
            PathIndex = 0;
            PathGeneration = 0;
            PathPending = false;
            WasChasing = false;
            IsAggro = false;
            AttackCooldownRemaining = 0f;
        }

        public void SetState(SlimeState state) => CurrentState = state;

        public void SetAggro(bool isAggro) => IsAggro = isAggro;

        public void ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.CeilToInt(amount));
        }

        public int BeginPathRequest(GridPosition destination, bool chasing)
        {
            Destination = destination;
            WasChasing = chasing;
            CachedPath = null;
            PathIndex = 0;
            PathPending = true;
            return ++PathGeneration;
        }

        public bool CompletePath(PathResult result)
        {
            if (result.Generation != PathGeneration)
                return false;
            PathPending = false;
            CachedPath = result.Succeeded ? result.Steps : null;
            PathIndex = 0;
            return true;
        }

        public EnemyPathStep? CurrentPathStep =>
            CachedPath != null && PathIndex < CachedPath.Count
                ? CachedPath[PathIndex]
                : null;

        public void CompletePathStep(GridPosition position)
        {
            CurrentGridPosition = position;
            PathIndex++;
        }

        public void SetGridPosition(GridPosition position) =>
            CurrentGridPosition = position;

        public void ClearPath()
        {
            CachedPath = null;
            PathIndex = 0;
            PathPending = false;
            PathGeneration++;
        }

        public void TickCooldown(float deltaTime)
        {
            AttackCooldownRemaining = Mathf.Max(
                0f,
                AttackCooldownRemaining - Mathf.Max(0f, deltaTime));
        }

        public void ResetAttackCooldown() =>
            AttackCooldownRemaining = Config.AttackCooldown;

        public void ResetRuntime()
        {
            Config = null;
            CurrentHealth = 0;
            CachedPath = null;
            PathPending = false;
            PathIndex = 0;
            PathGeneration++;
            AttackCooldownRemaining = 0f;
            IsAggro = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            ResetRuntime();
        }
    }
}
