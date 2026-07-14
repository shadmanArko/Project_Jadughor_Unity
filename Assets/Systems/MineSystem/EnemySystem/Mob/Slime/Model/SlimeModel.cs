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
        public SlimeMovementMode MovementMode { get; private set; }
        public bool EngagementActive { get; private set; }
        public bool AggroPlayedForEngagement { get; private set; }
        public bool HasReachabilityFailure { get; private set; }
        public GridPosition ReachabilityFailureTarget { get; private set; }
        public int ReachabilityFailureRevision { get; private set; }
        public float AttackCooldownRemaining { get; private set; }
        public int PatrolDirection { get; private set; } = 1;
        public bool MovementTimeoutActive { get; private set; }
        public float MovementTimeoutRemaining { get; private set; }
        public float TeleportCooldownRemaining { get; private set; }
        public float IdleRemaining { get; private set; }
        public int PatrolFailureCount { get; private set; }
        public bool CanTeleport => TeleportCooldownRemaining <= 0f;
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
            MovementMode = SlimeMovementMode.None;
            EngagementActive = false;
            AggroPlayedForEngagement = false;
            HasReachabilityFailure = false;
            ReachabilityFailureTarget = default;
            ReachabilityFailureRevision = -1;
            AttackCooldownRemaining = 0f;
            PatrolDirection = 1;
            MovementTimeoutActive = false;
            MovementTimeoutRemaining = 0f;
            TeleportCooldownRemaining = 0f;
            IdleRemaining = 0f;
            PatrolFailureCount = 0;
        }

        public void SetState(SlimeState state) => CurrentState = state;

        public void SetAggro(bool isAggro) => IsAggro = isAggro;

        public void SetMovementMode(SlimeMovementMode movementMode) =>
            MovementMode = movementMode;

        public void BeginEngagement()
        {
            EngagementActive = true;
        }

        public void MarkAggroPlayed()
        {
            EngagementActive = true;
            AggroPlayedForEngagement = true;
            IsAggro = true;
        }

        public void RequireAggroReplay()
        {
            if (EngagementActive)
                AggroPlayedForEngagement = false;
            IsAggro = false;
        }

        public void ResetEngagement()
        {
            EngagementActive = false;
            AggroPlayedForEngagement = false;
            IsAggro = false;
            ClearReachabilityFailure();
        }

        public void RecordReachabilityFailure(
            GridPosition target,
            int navigationRevision)
        {
            HasReachabilityFailure = true;
            ReachabilityFailureTarget = target;
            ReachabilityFailureRevision = navigationRevision;
        }

        public bool IsReachabilityFailureCurrent(
            GridPosition target,
            int navigationRevision) =>
            HasReachabilityFailure &&
            ReachabilityFailureTarget == target &&
            ReachabilityFailureRevision == navigationRevision;

        public void ClearReachabilityFailure()
        {
            HasReachabilityFailure = false;
            ReachabilityFailureTarget = default;
            ReachabilityFailureRevision = -1;
        }

        public void ReversePatrolDirection() =>
            PatrolDirection = PatrolDirection < 0 ? 1 : -1;

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
            if (result.Succeeded)
                Destination = result.Destination;
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
            ClearMovementTimeout();
        }

        public void StartIdle(float duration) =>
            IdleRemaining = Mathf.Max(0f, duration);

        public bool TickIdle(float deltaTime)
        {
            IdleRemaining = Mathf.Max(
                0f,
                IdleRemaining - Mathf.Max(0f, deltaTime));
            return IdleRemaining <= 0f;
        }

        public void RecordPatrolFailure() => PatrolFailureCount++;

        public void ResetPatrolFailures() => PatrolFailureCount = 0;

        public void StartMovementTimeout(float duration)
        {
            MovementTimeoutActive = true;
            MovementTimeoutRemaining = Mathf.Max(0f, duration);
        }

        public bool TickMovementTimeout(float deltaTime)
        {
            if (!MovementTimeoutActive)
                return false;
            MovementTimeoutRemaining = Mathf.Max(
                0f,
                MovementTimeoutRemaining - Mathf.Max(0f, deltaTime));
            return MovementTimeoutRemaining <= 0f;
        }

        public void ClearMovementTimeout()
        {
            MovementTimeoutActive = false;
            MovementTimeoutRemaining = 0f;
        }

        public void TickCooldown(float deltaTime)
        {
            var safeDeltaTime = Mathf.Max(0f, deltaTime);
            AttackCooldownRemaining = Mathf.Max(
                0f,
                AttackCooldownRemaining - safeDeltaTime);
            TeleportCooldownRemaining = Mathf.Max(
                0f,
                TeleportCooldownRemaining - safeDeltaTime);
        }

        public void ResetAttackCooldown() =>
            AttackCooldownRemaining = Config.AttackCooldown;

        public void StartTeleportCooldown(float duration)
        {
            TeleportCooldownRemaining = Mathf.Max(0f, duration);
        }

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
            MovementMode = SlimeMovementMode.None;
            EngagementActive = false;
            AggroPlayedForEngagement = false;
            HasReachabilityFailure = false;
            ReachabilityFailureTarget = default;
            ReachabilityFailureRevision = -1;
            PatrolDirection = 1;
            TeleportCooldownRemaining = 0f;
            IdleRemaining = 0f;
            PatrolFailureCount = 0;
            ClearMovementTimeout();
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
