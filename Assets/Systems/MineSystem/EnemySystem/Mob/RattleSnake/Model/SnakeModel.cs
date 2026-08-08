using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Config;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Enum;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.RattleSnake.Model
{
    public sealed class SnakeModel : IDisposable
    {
        private readonly List<EnemyPathStep> _patrolCorridor = new();
        private bool _disposed;

        public SnakeConfigScriptable Config { get; private set; }
        public int CurrentHealth { get; private set; }
        public GridPosition CurrentGridPosition { get; private set; }
        public GridPosition Destination { get; private set; }
        public SnakeState CurrentState { get; private set; }
        public IReadOnlyList<EnemyPathStep> CachedPath { get; private set; }
        public int PathIndex { get; private set; }
        public IReadOnlyList<EnemyPathStep> PatrolCorridor => _patrolCorridor;
        public int PatrolCorridorIndex { get; private set; }
        public GridPosition PatrolCorridorOrigin { get; private set; }
        public int PathGeneration { get; private set; }
        public bool PathPending { get; private set; }
        public bool PathRefreshPending { get; private set; }
        public SnakeMovementMode MovementMode { get; private set; }
        public bool EngagementActive { get; private set; }
        public bool HasReachabilityFailure { get; private set; }
        public GridPosition ReachabilityFailureTarget { get; private set; }
        public int ReachabilityFailureRevision { get; private set; }
        public float AttackCooldownRemaining { get; private set; }
        public int PatrolDirection { get; private set; } = 1;
        public bool MovementTimeoutActive { get; private set; }
        public float MovementTimeoutRemaining { get; private set; }
        public float IdleRemaining { get; private set; }
        public int RepositionFailureCount { get; private set; }
        public int RepositionCount { get; private set; }
        public float GroundedFallSeconds { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        public void Initialize(
            SnakeConfigScriptable config,
            GridPosition spawnPosition)
        {
            Config = config;
            CurrentHealth = config.MaxHealth;
            CurrentGridPosition = spawnPosition;
            Destination = spawnPosition;
            CurrentState = SnakeState.Spawn;
            CachedPath = null;
            PathIndex = 0;
            _patrolCorridor.Clear();
            var patrolCorridorCapacity =
                Mathf.Max(1, config.PatrolRangeInTiles * 2 + 1);
            if (_patrolCorridor.Capacity < patrolCorridorCapacity)
                _patrolCorridor.Capacity = patrolCorridorCapacity;
            PatrolCorridorIndex = 0;
            PatrolCorridorOrigin = default;
            PathGeneration = 0;
            PathPending = false;
            PathRefreshPending = false;
            MovementMode = SnakeMovementMode.None;
            EngagementActive = false;
            HasReachabilityFailure = false;
            ReachabilityFailureTarget = default;
            ReachabilityFailureRevision = -1;
            AttackCooldownRemaining = 0f;
            PatrolDirection = 1;
            MovementTimeoutActive = false;
            MovementTimeoutRemaining = 0f;
            IdleRemaining = 0f;
            RepositionFailureCount = 0;
            RepositionCount = 0;
            GroundedFallSeconds = 0f;
        }

        public void SetState(SnakeState state) => CurrentState = state;

        public void SetMovementMode(SnakeMovementMode movementMode) =>
            MovementMode = movementMode;

        public void BeginEngagement() => EngagementActive = true;

        public void ResetEngagement()
        {
            EngagementActive = false;
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

        public void BeginPatrolCorridor(int minimumCapacity)
        {
            _patrolCorridor.Clear();
            if (_patrolCorridor.Capacity < minimumCapacity)
                _patrolCorridor.Capacity = minimumCapacity;
            PatrolCorridorIndex = 0;
        }

        public void AddPatrolCorridorCell(GridPosition position) =>
            _patrolCorridor.Add(new EnemyPathStep(
                position,
                EnemyPathStepType.Walk));

        public bool StartPatrolCorridor(int currentIndex)
        {
            if (currentIndex < 0 || currentIndex >= _patrolCorridor.Count)
                return false;
            PatrolCorridorIndex = currentIndex;
            CurrentGridPosition = _patrolCorridor[currentIndex].Position;
            PatrolCorridorOrigin = CurrentGridPosition;
            return true;
        }

        public bool TryGetNextPatrolStep(out EnemyPathStep step)
        {
            var nextIndex = PatrolCorridorIndex + PatrolDirection;
            if (nextIndex < 0 || nextIndex >= _patrolCorridor.Count)
            {
                step = default;
                return false;
            }
            step = _patrolCorridor[nextIndex];
            return true;
        }

        public bool CompletePatrolStep()
        {
            var nextIndex = PatrolCorridorIndex + PatrolDirection;
            if (nextIndex < 0 || nextIndex >= _patrolCorridor.Count)
                return false;
            PatrolCorridorIndex = nextIndex;
            CurrentGridPosition = _patrolCorridor[nextIndex].Position;
            return true;
        }

        public void ClearPatrolCorridor()
        {
            _patrolCorridor.Clear();
            PatrolCorridorIndex = 0;
            PatrolCorridorOrigin = default;
        }

        public void ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.CeilToInt(amount));
        }

        public int BeginPathRequest(GridPosition destination)
        {
            ClearPatrolCorridor();
            Destination = destination;
            CachedPath = null;
            PathIndex = 0;
            PathPending = true;
            PathRefreshPending = false;
            return ++PathGeneration;
        }

        public int BeginPathRefresh()
        {
            PathPending = true;
            PathRefreshPending = true;
            return ++PathGeneration;
        }

        public bool CompletePath(PathResult result)
        {
            if (result.Generation != PathGeneration)
                return false;
            PathPending = false;
            PathRefreshPending = false;
            if (result.Succeeded)
                Destination = result.Destination;
            CachedPath = result.Succeeded ? result.Steps : null;
            PathIndex = 0;
            return true;
        }

        public bool CompletePathRefresh(PathResult result)
        {
            if (result.Generation != PathGeneration || !PathRefreshPending)
                return false;
            PathPending = false;
            PathRefreshPending = false;
            if (!result.Succeeded)
                return true;
            Destination = result.Destination;
            CachedPath = result.Steps;
            PathIndex = 0;
            return true;
        }

        public void CancelPendingPathRequest()
        {
            if (!PathPending)
                return;
            PathPending = false;
            PathRefreshPending = false;
            PathGeneration++;
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
            PathRefreshPending = false;
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

        public void RecordRepositionFailure() => RepositionFailureCount++;

        public void ResetRepositionFailures() => RepositionFailureCount = 0;

        /// <summary>
        /// Counts successful quiet repositions. A snake that keeps needing to
        /// reposition is stuck even though no single attempt failed, so this is
        /// tracked separately from <see cref="RepositionFailureCount"/>.
        /// </summary>
        public void RecordReposition() => RepositionCount++;

        public void ResetRepositionCount() => RepositionCount = 0;

        /// <summary>
        /// Accumulates time spent in Fall while still physically grounded. The
        /// landing latch needs an airborne frame first, so this is the escape
        /// hatch for a Fall that was entered without ever leaving the ground.
        /// </summary>
        public void TickGroundedFall(float deltaTime) =>
            GroundedFallSeconds += Mathf.Max(0f, deltaTime);

        public void ClearGroundedFall() => GroundedFallSeconds = 0f;

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
            _patrolCorridor.Clear();
            PathPending = false;
            PathRefreshPending = false;
            PathIndex = 0;
            PatrolCorridorIndex = 0;
            PatrolCorridorOrigin = default;
            PathGeneration++;
            AttackCooldownRemaining = 0f;
            MovementMode = SnakeMovementMode.None;
            EngagementActive = false;
            HasReachabilityFailure = false;
            ReachabilityFailureTarget = default;
            ReachabilityFailureRevision = -1;
            PatrolDirection = 1;
            IdleRemaining = 0f;
            RepositionFailureCount = 0;
            RepositionCount = 0;
            GroundedFallSeconds = 0f;
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
