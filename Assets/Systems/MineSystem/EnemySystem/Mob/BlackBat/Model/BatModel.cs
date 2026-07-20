using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Config;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Enum;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Model
{
    public sealed class BatModel : IDisposable
    {
        private bool _disposed;

        public BatConfigScriptable Config { get; private set; }
        public int CurrentHealth { get; private set; }
        public GridPosition CurrentGridPosition { get; private set; }
        public GridPosition Destination { get; private set; }
        public BatState CurrentState { get; private set; }
        public BatPathPurpose PathPurpose { get; private set; }
        public IReadOnlyList<EnemyPathStep> CachedPath { get; private set; }
        public int PathIndex { get; private set; }
        public int PathGeneration { get; private set; }
        public bool PathPending { get; private set; }
        public bool EngagementActive { get; private set; }
        public bool ContactApproachActive { get; private set; }
        public bool HasReachabilityFailure { get; private set; }
        public GridPosition ReachabilityFailureTarget { get; private set; }
        public int ReachabilityFailureRevision { get; private set; }
        public bool PendingDeath { get; private set; }
        public float AttackCooldownRemaining { get; private set; }
        public float DecisionDelayRemaining { get; private set; }
        public float IdleRemaining { get; private set; }
        public float IdleCooldownRemaining { get; private set; }
        public bool IdleResting { get; private set; }
        public bool MovementTimeoutActive { get; private set; }
        public float MovementTimeoutRemaining { get; private set; }
        public bool SegmentActive { get; private set; }
        public Vector2 SegmentStart { get; private set; }
        public Vector2 SegmentTarget { get; private set; }
        public float SegmentProgress { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        public EnemyPathStep? CurrentPathStep =>
            CachedPath != null && PathIndex < CachedPath.Count
                ? CachedPath[PathIndex]
                : null;

        public void Initialize(
            BatConfigScriptable config,
            GridPosition spawnPosition)
        {
            Config = config;
            CurrentHealth = config.MaxHealth;
            CurrentGridPosition = spawnPosition;
            Destination = spawnPosition;
            CurrentState = BatState.Explore;
            PathPurpose = BatPathPurpose.None;
            CachedPath = null;
            PathIndex = 0;
            PathGeneration = 0;
            PathPending = false;
            EngagementActive = false;
            ContactApproachActive = false;
            ClearReachabilityFailure();
            PendingDeath = false;
            AttackCooldownRemaining = 0f;
            DecisionDelayRemaining = 0f;
            IdleRemaining = 0f;
            IdleCooldownRemaining = 0f;
            IdleResting = false;
            ClearMovementTimeout();
            ClearSegment();
        }

        public void SetState(BatState state) => CurrentState = state;

        public void SetGridPosition(GridPosition position) =>
            CurrentGridPosition = position;

        public void ApplyDamage(float amount)
        {
            if (CurrentState == BatState.Death || amount <= 0f)
                return;
            CurrentHealth = Mathf.Max(
                0,
                CurrentHealth - Mathf.CeilToInt(amount));
            if (CurrentHealth <= 0)
                PendingDeath = true;
        }

        public void BeginEngagement()
        {
            EngagementActive = true;
            ClearReachabilityFailure();
        }

        public void EndEngagement()
        {
            EngagementActive = false;
            EndContactApproach();
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

        public int BeginPathRequest(
            GridPosition destination,
            BatPathPurpose purpose)
        {
            EndContactApproach();
            Destination = destination;
            PathPurpose = purpose;
            CachedPath = null;
            PathIndex = 0;
            PathPending = true;
            ClearSegment();
            ClearMovementTimeout();
            return ++PathGeneration;
        }

        public bool CompletePath(PathResult result)
        {
            if (!PathPending || result.Generation != PathGeneration)
                return false;
            PathPending = false;
            if (result.Succeeded)
                Destination = result.Destination;
            CachedPath = result.Succeeded ? result.Steps : null;
            PathIndex = 0;
            return true;
        }

        public void CompletePathStep(GridPosition position)
        {
            CurrentGridPosition = position;
            PathIndex++;
            ClearSegment();
        }

        public void ClearPath()
        {
            CachedPath = null;
            PathIndex = 0;
            PathPending = false;
            PathPurpose = BatPathPurpose.None;
            PathGeneration++;
            ClearSegment();
            ClearMovementTimeout();
        }

        public void BeginContactApproach()
        {
            ContactApproachActive = true;
            ClearSegment();
        }

        public void EndContactApproach() => ContactApproachActive = false;

        public void BeginSegment(Vector2 start, Vector2 target)
        {
            SegmentActive = true;
            SegmentStart = start;
            SegmentTarget = target;
            SegmentProgress = 0f;
        }

        public float AdvanceSegment(float normalizedDelta)
        {
            SegmentProgress = Mathf.Clamp01(
                SegmentProgress + Mathf.Max(0f, normalizedDelta));
            return SegmentProgress;
        }

        public void ClearSegment()
        {
            SegmentActive = false;
            SegmentStart = default;
            SegmentTarget = default;
            SegmentProgress = 0f;
        }

        public void StartDecisionDelay(float duration) =>
            DecisionDelayRemaining = Mathf.Max(0f, duration);

        public bool TickDecisionDelay(float deltaTime)
        {
            DecisionDelayRemaining = Mathf.Max(
                0f,
                DecisionDelayRemaining - Mathf.Max(0f, deltaTime));
            return DecisionDelayRemaining <= 0f;
        }

        public void StartIdle(float duration)
        {
            IdleResting = true;
            IdleRemaining = Mathf.Max(0f, duration);
        }

        public bool TickIdle(float deltaTime)
        {
            if (!IdleResting)
                return false;
            IdleRemaining = Mathf.Max(
                0f,
                IdleRemaining - Mathf.Max(0f, deltaTime));
            return IdleRemaining <= 0f;
        }

        public void EndIdle()
        {
            IdleResting = false;
            IdleRemaining = 0f;
        }

        public void ResetIdleCooldown() =>
            IdleCooldownRemaining = Config != null
                ? Config.IdleCooldownSeconds
                : 0f;

        public void TickIdleCooldown(float deltaTime)
        {
            IdleCooldownRemaining = Mathf.Max(
                0f,
                IdleCooldownRemaining - Mathf.Max(0f, deltaTime));
        }

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
            AttackCooldownRemaining = Config != null
                ? Config.AttackCooldown
                : 0f;

        public void ResetRuntime()
        {
            Config = null;
            CurrentHealth = 0;
            CurrentGridPosition = default;
            Destination = default;
            CurrentState = BatState.Explore;
            CachedPath = null;
            PathIndex = 0;
            PathPending = false;
            PathPurpose = BatPathPurpose.None;
            PathGeneration++;
            EngagementActive = false;
            EndContactApproach();
            ClearReachabilityFailure();
            PendingDeath = false;
            AttackCooldownRemaining = 0f;
            DecisionDelayRemaining = 0f;
            EndIdle();
            IdleCooldownRemaining = 0f;
            ClearMovementTimeout();
            ClearSegment();
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
