using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Config;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Enum;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Model;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Service;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.EnemySystem.Signal;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Service;
using Systems.Utilities.EventBus;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Controller
{
    public sealed class BatStateMachine : IDisposable
    {
        private readonly BatModel _model;
        private readonly BatView _view;
        private readonly IEnemyTargetProvider _target;
        private readonly IEnemyAttackService _attack;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly IEnemyPlacementValidator _placement;
        private readonly IEnemyChaseTargetResolver _chaseResolver;
        private readonly BatNavigationService _navigation;
        private readonly PauseGate _pauseGate = new();

        private BatConfigScriptable _config;
        private Guid _enemyId;
        private CancellationToken _lifetimeToken;
        private CancellationTokenSource _pathCancellation;
        private GridPosition _observedTargetGrid;
        private GridPosition _pathTargetGrid;
        private Vector2 _perchWorldPosition;
        private float _combatDiagnosticLogRemaining;
        private int _pathNavigationRevision;
        private int _animationGeneration;
        private bool _hasObservedTarget;
        private bool _isApproachingPerch;
        private bool _idleToFlyPlaying;
        private bool _attackApplied;
        private bool _deathSignalSent;
        private bool _despawnSignalSent;
        private bool _disposed;

        public BatStateMachine(
            BatModel model,
            BatView view,
            IEnemyTargetProvider target,
            IEnemyAttackService attack,
            IEnemyPathfindingService pathfinding,
            IEnemyPlacementValidator placement,
            IEnemyChaseTargetResolver chaseResolver,
            BatNavigationService navigation)
        {
            _model = model;
            _view = view;
            _target = target;
            _attack = attack;
            _pathfinding = pathfinding;
            _placement = placement;
            _chaseResolver = chaseResolver;
            _navigation = navigation;
        }

        public void Initialize(
            BatConfigScriptable config,
            Guid enemyId,
            CancellationToken lifetimeToken)
        {
            CancelPathRequest();
            _config = config;
            _enemyId = enemyId;
            _lifetimeToken = lifetimeToken;
            _hasObservedTarget = _target.IsTargetAvailable;
            _observedTargetGrid = _hasObservedTarget
                ? _target.GridPosition
                : default;
            _pathTargetGrid = default;
            _perchWorldPosition = default;
            _combatDiagnosticLogRemaining = 0f;
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            _isApproachingPerch = false;
            _idleToFlyPlaying = false;
            _attackApplied = false;
            _deathSignalSent = false;
            _despawnSignalSent = false;
            _pauseGate.Resume();
        }

        public UniTask SpawnAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _view.SetDamageEnabled(true);
            EnterExplore();
            return UniTask.CompletedTask;
        }

        public UniTask DespawnAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_despawnSignalSent)
                return UniTask.CompletedTask;
            _despawnSignalSent = true;
            _view.SetDamageEnabled(false);
            CancelPathRequest();
            _view.Stop();
            GlobalEventBus.Fire(new EnemyDespawnedSignal(_enemyId));
            return UniTask.CompletedTask;
        }

        public void OnFixedTick(EnemyTickContext context)
        {
            if (_disposed || _pauseGate.IsPaused || _config == null ||
                _model.CurrentState == BatState.Death)
                return;

            _model.TickCooldown(context.FixedDeltaTime);
            TickCombatDiagnostics(context.FixedDeltaTime);
            if (_model.CurrentState == BatState.Hurt ||
                _model.CurrentState == BatState.Attack)
                return;

            if (HandleCombatContext())
                return;

            switch (_model.CurrentState)
            {
                case BatState.Explore:
                    TickExplore(context.FixedDeltaTime);
                    break;
                case BatState.Chase:
                    TickChase(context.FixedDeltaTime);
                    break;
                case BatState.Idle:
                    TickIdle(context.FixedDeltaTime);
                    break;
            }
        }

        public void HandleNavigationChanged(GridPosition changedPosition)
        {
            if (_pauseGate.IsPaused || _config == null)
                return;
            _model.ClearReachabilityFailure();
            if (_model.CurrentState == BatState.Chase &&
                _model.EngagementActive &&
                _target.IsTargetAvailable)
            {
                RequestChaseRoute();
                return;
            }
            if (_model.CurrentState == BatState.Explore &&
                IsNavigationChangeRelevant(changedPosition))
                EnterExplore();
        }

        public void EnterHurt()
        {
            if (_model.CurrentState == BatState.Death)
                return;
            if (_model.CurrentState == BatState.Hurt)
                return;

            CancelPathRequest();
            _isApproachingPerch = false;
            _idleToFlyPlaying = false;
            _model.EndIdle();
            _view.Stop();
            ChangeState(BatState.Hurt);
        }

        public void HandleAnimationMarker(EnemyAnimationMarkerEvent animationEvent)
        {
            if (_pauseGate.IsPaused ||
                animationEvent.Generation != _animationGeneration ||
                _model.CurrentState != BatState.Attack ||
                animationEvent.AnimationId != BatAnimationId.Attack ||
                animationEvent.Marker != (int)EnemyAnimationMarker.AttackImpact ||
                _attackApplied)
                return;

            _attackApplied = true;
            var attackValid = IsAttackValid();
            var attackSucceeded = attackValid &&
                                  _attack.TryAttack(
                                      _config.Damage,
                                      _config.StatusEffect);
            if (_config.EnableCombatDiagnosticLogs)
            {
                Debug.Log(
                    $"[BlackBatAttack][{_enemyId:N}] " +
                    $"valid={attackValid} " +
                    $"damageApplied={attackSucceeded} " +
                    $"bodyDistance={Vector2.Distance(_view.Body.position, _target.BodyPosition):F4}",
                    _view);
            }
        }

        public void HandleAnimationCompleted(
            EnemyAnimationCompletedEvent animationEvent)
        {
            if (animationEvent.Generation != _animationGeneration)
                return;

            switch (_model.CurrentState)
            {
                case BatState.Idle:
                    HandleIdleAnimationCompleted(animationEvent.AnimationId);
                    break;
                case BatState.Attack:
                    if (animationEvent.AnimationId != BatAnimationId.Attack)
                        return;
                    _model.ResetAttackCooldown();
                    ResumeAfterAttack();
                    break;
                case BatState.Hurt:
                    if (animationEvent.AnimationId != BatAnimationId.Hurt)
                        return;
                    if (_model.PendingDeath)
                        EnterDeath();
                    else
                        EnterExplore();
                    break;
                case BatState.Death:
                    if (animationEvent.AnimationId == BatAnimationId.Death)
                        CompleteDeath();
                    break;
            }
        }

        public void Pause()
        {
            CancelPathRequest();
            _view.Stop();
            _pauseGate.Pause();
        }

        public void Resume() => _pauseGate.Resume();

        public void Release()
        {
            CancelPathRequest();
            _pauseGate.Resume();
            _isApproachingPerch = false;
            _idleToFlyPlaying = false;
            _combatDiagnosticLogRemaining = 0f;
            _config = null;
        }

        private void TickCombatDiagnostics(float deltaTime)
        {
            if (!_config.EnableCombatDiagnosticLogs ||
                !_target.IsTargetAvailable ||
                (!_model.EngagementActive &&
                 _model.PathPurpose != BatPathPurpose.Chase &&
                 !IsWithinAggroRange()))
                return;

            _combatDiagnosticLogRemaining = Mathf.Max(
                0f,
                _combatDiagnosticLogRemaining - Mathf.Max(0f, deltaTime));
            if (_combatDiagnosticLogRemaining > 0f)
                return;
            _combatDiagnosticLogRemaining = Mathf.Max(
                0.02f,
                _config.CombatDiagnosticLogInterval);

            var body = _view.Body;
            var batPosition = body.position;
            var batGrid = _placement.WorldToGrid(batPosition);
            var targetPosition = _target.BodyPosition;
            var targetGrid = _target.GridPosition;
            var worldDistance = Vector2.Distance(
                batPosition,
                targetPosition);
            var gridDistance = GridDistance(batGrid, targetGrid);
            var terrainCollider = _view.TerrainCollider;
            var colliderCenter = terrainCollider != null
                ? (Vector2)terrainCollider.bounds.center
                : Vector2.zero;
            var colliderSize = terrainCollider != null
                ? (Vector2)terrainCollider.bounds.size
                : Vector2.zero;
            var pathStep = _model.CurrentPathStep;
            var pathStepText = pathStep.HasValue
                ? $"{pathStep.Value.Type}:{pathStep.Value.Position}"
                : "none";
            var context = _enemyId.ToString("N");

            Debug.Log(
                $"[BlackBatDiagnostics][Bat][{context}] " +
                $"state={_model.CurrentState} " +
                $"rbPosition={batPosition.ToString("F4")} " +
                $"rbVelocity={body.linearVelocity.ToString("F4")} " +
                $"actualGrid={batGrid} " +
                $"modelGrid={_model.CurrentGridPosition} " +
                $"colliderCenter={colliderCenter.ToString("F4")} " +
                $"colliderSize={colliderSize.ToString("F4")} " +
                $"targetWorldPosition={targetPosition.ToString("F4")} " +
                $"targetGrid={targetGrid} " +
                $"worldDistance={worldDistance:F4} " +
                $"contactDistance={_config.AttackContactDistance:F4} " +
                $"gridDistance={gridDistance} " +
                $"attackGridRange={_config.AttackRangeInTiles} " +
                $"attackValid={IsAttackValid()} " +
                $"engaged={_model.EngagementActive} " +
                $"contactApproach={_model.ContactApproachActive} " +
                $"pathPurpose={_model.PathPurpose} " +
                $"pathPending={_model.PathPending} " +
                $"pathStep={pathStepText} " +
                $"attackCooldown={_model.AttackCooldownRemaining:F4} " +
                $"movementTimeoutActive={_model.MovementTimeoutActive} " +
                $"movementTimeout={_model.MovementTimeoutRemaining:F4}",
                _view);
        }

        private bool HandleCombatContext()
        {
            if (!_target.IsTargetAvailable)
            {
                _hasObservedTarget = false;
                if (_model.EngagementActive ||
                    _model.PathPurpose == BatPathPurpose.Chase)
                {
                    EndEngagementAndExplore();
                    return true;
                }
                return false;
            }

            var targetGrid = _target.GridPosition;
            var targetMoved = !_hasObservedTarget ||
                              targetGrid != _observedTargetGrid;
            _hasObservedTarget = true;
            _observedTargetGrid = targetGrid;
            if (targetMoved)
                _model.ClearReachabilityFailure();

            if (IsAttackValid())
            {
                CancelPathRequest();
                _model.BeginEngagement();
                _view.Stop();
                if (_model.AttackCooldownRemaining <= 0f)
                    EnterAttack();
                else if (_model.CurrentState != BatState.Chase)
                    ChangeState(BatState.Chase);
                return true;
            }

            if (_model.EngagementActive)
            {
                if (!_target.IsTargetAvailable ||
                    !IsWithinChaseExitRange())
                {
                    EndEngagementAndExplore();
                    return true;
                }

                if (targetMoved &&
                    _model.CurrentState == BatState.Chase &&
                    !_model.PathPending)
                {
                    RequestChaseRoute();
                    return true;
                }
                return false;
            }

            if (!_target.IsTargetAvailable ||
                !IsWithinAggroRange() ||
                _model.PathPending &&
                _model.PathPurpose == BatPathPurpose.Chase)
                return false;

            _model.BeginEngagement();
            if (_model.CurrentState != BatState.Chase)
                ChangeState(BatState.Chase);
            RequestChaseRoute();
            return true;
        }

        private void TickExplore(float deltaTime)
        {
            if (_isApproachingPerch)
            {
                TickPerchApproach(deltaTime);
                return;
            }
            if (_model.PathPending || _model.CurrentPathStep.HasValue)
            {
                TickMovement(deltaTime);
                return;
            }
            if (!_model.TickDecisionDelay(deltaTime))
                return;
            ChooseIdleOrRoam();
        }

        private void TickChase(float deltaTime)
        {
            if (!_model.EngagementActive ||
                !_target.IsTargetAvailable ||
                !IsWithinChaseExitRange())
            {
                EndEngagementAndExplore();
                return;
            }
            if (IsAttackValid())
            {
                CancelPathRequest();
                _view.Stop();
                if (_model.AttackCooldownRemaining <= 0f)
                    EnterAttack();
                return;
            }
            if (_model.ContactApproachActive)
            {
                TickContactApproach(deltaTime);
                return;
            }
            if (_model.PathPending || _model.CurrentPathStep.HasValue)
            {
                TickMovement(deltaTime);
                return;
            }
            if (!_model.TickDecisionDelay(deltaTime))
                return;
            RequestChaseRoute();
        }

        private void StartContactApproach()
        {
            CancelPathRequest();
            if (!CanUseContactApproach())
            {
                HandleContactApproachFailure();
                return;
            }

            var remainingDistance = Vector2.Distance(
                _view.Body.position,
                _target.BodyPosition);
            _model.BeginContactApproach();
            _model.StartMovementTimeout(
                GetWorldMovementTimeout(remainingDistance));
        }

        private void TickContactApproach(float deltaTime)
        {
            if (_model.TickMovementTimeout(deltaTime))
            {
                HandleContactApproachFailure();
                return;
            }
            if (!CanUseContactApproach())
            {
                RequestChaseRoute();
                return;
            }
            if (IsAttackValid())
            {
                _model.EndContactApproach();
                _view.Stop();
                if (_model.AttackCooldownRemaining <= 0f)
                    EnterAttack();
                return;
            }

            var bodyPosition = _view.Body.position;
            var targetPosition = _target.BodyPosition;
            var delta = targetPosition - bodyPosition;
            var distance = delta.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                _view.Stop();
                return;
            }

            if (Mathf.Abs(delta.x) > _config.PositionTolerance)
                _view.SetFacing(delta.x < 0f);
            var movementDistance = Mathf.Min(
                _config.MoveSpeed * Mathf.Max(0f, deltaTime),
                distance);
            _view.MovePosition(
                bodyPosition + delta / distance * movementDistance);
        }

        private bool CanUseContactApproach()
        {
            if (!_target.IsTargetAvailable ||
                !_placement.IsCurrentPlacementClear(_view.TerrainCollider))
                return false;

            var currentGrid = _placement.WorldToGrid(_view.Body.position);
            var targetGrid = _target.GridPosition;
            var maximumGridDistance = Mathf.Max(
                1,
                _config.AttackRangeInTiles);
            return GridDistance(currentGrid, targetGrid) <=
                   maximumGridDistance &&
                   _pathfinding.IsFlyable(currentGrid) &&
                   _pathfinding.IsFlyable(targetGrid);
        }

        private void HandleContactApproachFailure()
        {
            _model.EndContactApproach();
            _view.Stop();
            if (_target.IsTargetAvailable && IsWithinChaseExitRange())
            {
                _model.BeginEngagement();
                if (_model.CurrentState != BatState.Chase)
                    ChangeState(BatState.Chase);
                _model.StartDecisionDelay(_config.DecisionRetryDelay);
                return;
            }
            EndEngagementAndExplore();
        }

        private void TickIdle(float deltaTime)
        {
            if (!_model.IdleResting || _idleToFlyPlaying ||
                !_model.TickIdle(deltaTime))
                return;
            _model.EndIdle();
            _idleToFlyPlaying = true;
            PlayAnimation(BatAnimationId.IdleToFly, true);
        }

        private void StartExploreRoute()
        {
            CancelPathRequest();
            var current = _placement.WorldToGrid(_view.Body.position);
            _model.SetGridPosition(current);
            for (var attempt = 0;
                 attempt < _config.DestinationRetries;
                 attempt++)
            {
                var offset = UnityEngine.Random.Range(
                    0,
                    Mathf.Max(1, _pathfinding.FlyableCount));
                if (!_pathfinding.TryFindFlyableNear(
                        current,
                        1,
                        _config.ExploreRangeInTiles,
                        offset,
                        out var destination) ||
                    !_placement.TryGetPlacement(
                        _view.TerrainCollider,
                        destination,
                        out _))
                    continue;

                BeginDirectPathRequest(destination, BatPathPurpose.Explore);
                return;
            }
            _model.StartDecisionDelay(_config.DecisionRetryDelay);
        }

        private void ChooseIdleOrRoam()
        {
            if (UnityEngine.Random.value <= _config.IdleChance &&
                TryStartPerchRoute())
                return;
            StartExploreRoute();
        }

        private bool TryStartPerchRoute()
        {
            var current = _placement.WorldToGrid(_view.Body.position);
            if (!_navigation.TryFindPerch(
                    current,
                    _view.TerrainCollider,
                    _config.ExploreRangeInTiles,
                    _config.PerchCeilingClearance,
                    out var perchCell,
                    out _perchWorldPosition))
                return false;

            BeginDirectPathRequest(perchCell, BatPathPurpose.Perch);
            return true;
        }

        private void BeginDirectPathRequest(
            GridPosition destination,
            BatPathPurpose purpose)
        {
            CancelPathRequest();
            var current = _placement.WorldToGrid(_view.Body.position);
            _model.SetGridPosition(current);
            var generation = _model.BeginPathRequest(destination, purpose);
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            _pathCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            var destinations = new List<GridPosition>(1) { destination };
            var request = new EnemyMultiTargetPathRequest(
                current,
                destination,
                destination,
                destinations,
                EnemyMovementType.Flying,
                0,
                generation);
            FindDirectPathAsync(
                    request,
                    purpose,
                    _pathNavigationRevision,
                    _pathCancellation.Token)
                .Forget(HandlePathException);
        }

        private async UniTask FindDirectPathAsync(
            EnemyMultiTargetPathRequest request,
            BatPathPurpose purpose,
            int navigationRevision,
            CancellationToken cancellationToken)
        {
            var result = await _pathfinding.FindPathToAnyAsync(
                request,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || _disposed)
                return;
            HandleDirectPathResult(result, purpose, navigationRevision);
        }

        private void HandleDirectPathResult(
            PathResult result,
            BatPathPurpose purpose,
            int navigationRevision)
        {
            if (!_model.PathPending ||
                _model.PathPurpose != purpose ||
                result.Generation != _model.PathGeneration)
                return;
            if (_pathfinding.NavigationRevision != navigationRevision)
            {
                if (purpose == BatPathPurpose.Perch)
                    EnterExplore();
                else
                    StartExploreRoute();
                return;
            }
            if (!_model.CompletePath(result))
                return;
            DisposePathCancellation();
            if (!result.Succeeded)
            {
                _model.ClearPath();
                _model.StartDecisionDelay(_config.DecisionRetryDelay);
                return;
            }
            if (result.Steps == null || result.Steps.Count == 0)
            {
                FinishMovement();
                return;
            }
            _model.StartMovementTimeout(GetMovementTimeout(result.Steps));
        }

        private void RequestChaseRoute()
        {
            if (!_target.IsTargetAvailable || !IsWithinChaseExitRange())
            {
                EndEngagementAndExplore();
                return;
            }

            CancelPathRequest();
            _model.BeginEngagement();
            if (_model.CurrentState != BatState.Chase)
                ChangeState(BatState.Chase);
            var current = _placement.WorldToGrid(_view.Body.position);
            _model.SetGridPosition(current);
            _pathTargetGrid = _target.GridPosition;
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            var generation = _model.BeginPathRequest(
                _pathTargetGrid,
                BatPathPurpose.Chase);
            _pathCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            FindChasePathAsync(
                    generation,
                    current,
                    _pathTargetGrid,
                    _pathNavigationRevision,
                    _pathCancellation.Token)
                .Forget(HandlePathException);
        }

        private async UniTask FindChasePathAsync(
            int generation,
            GridPosition routeStart,
            GridPosition targetGrid,
            int navigationRevision,
            CancellationToken cancellationToken)
        {
            var result = await _chaseResolver.FindReachablePathAsync(
                _view.TerrainCollider,
                routeStart,
                targetGrid,
                _model.Destination,
                Mathf.Max(1, _config.AttackRangeInTiles),
                EnemyMovementType.Flying,
                0,
                generation,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || _disposed)
                return;
            HandleChasePathResult(result, targetGrid, navigationRevision);
        }

        private void HandleChasePathResult(
            PathResult result,
            GridPosition targetGrid,
            int navigationRevision)
        {
            if (!_model.PathPending ||
                _model.PathPurpose != BatPathPurpose.Chase ||
                result.Generation != _model.PathGeneration)
                return;
            if (!_target.IsTargetAvailable ||
                !IsWithinChaseExitRange())
            {
                EndEngagementAndExplore();
                return;
            }
            if (_pathfinding.NavigationRevision != navigationRevision ||
                _target.GridPosition != targetGrid)
            {
                RequestChaseRoute();
                return;
            }
            if (!_model.CompletePath(result))
                return;
            DisposePathCancellation();
            if (!result.Succeeded)
            {
                _model.ClearPath();
                _view.Stop();
                _model.StartDecisionDelay(_config.DecisionRetryDelay);
                return;
            }

            _model.BeginEngagement();
            ChangeState(BatState.Chase);
            if (result.Steps == null || result.Steps.Count == 0)
            {
                _model.ClearPath();
                if (IsAttackValid() &&
                    _model.AttackCooldownRemaining <= 0f)
                    EnterAttack();
                else if (!IsAttackValid())
                    StartContactApproach();
                return;
            }
            _model.StartMovementTimeout(GetMovementTimeout(result.Steps));
        }

        private void TickMovement(float deltaTime)
        {
            if (_model.TickMovementTimeout(deltaTime))
            {
                HandleMovementFailure();
                return;
            }
            var step = _model.CurrentPathStep;
            if (_model.PathPending && !step.HasValue)
                return;
            if (!step.HasValue)
            {
                FinishMovement();
                return;
            }
            if (!_pathfinding.IsFlyable(step.Value.Position) ||
                !_placement.TryGetPlacement(
                    _view.TerrainCollider,
                    step.Value.Position,
                    out var targetPosition))
            {
                HandleMovementFailure();
                return;
            }

            if (!_model.SegmentActive)
            {
                var startPosition = _placement.GridToWorld(
                    _model.CurrentGridPosition);
                _model.BeginSegment(startPosition, targetPosition);
            }

            var segmentDistance = Vector2.Distance(
                _model.SegmentStart,
                _model.SegmentTarget);
            var progress = _model.AdvanceSegment(
                _config.MoveSpeed * Mathf.Max(0f, deltaTime) /
                Mathf.Max(0.0001f, segmentDistance));
            var basePosition = Vector2.Lerp(
                _model.SegmentStart,
                _model.SegmentTarget,
                progress);
            var segmentDelta = _model.SegmentTarget - _model.SegmentStart;
            var wobble = Mathf.Sin(
                progress * Mathf.PI * 2f *
                _config.FlightWobbleCyclesPerCell) *
                _config.FlightWobbleAmplitude;
            var horizontal = Mathf.Abs(segmentDelta.x) >=
                             Mathf.Abs(segmentDelta.y);
            var desired = basePosition +
                          (horizontal
                              ? Vector2.up * wobble
                              : Vector2.right * wobble);
            if (Mathf.Abs(segmentDelta.x) > _config.PositionTolerance)
                _view.SetFacing(segmentDelta.x < 0f);
            _view.MovePosition(desired);

            if (progress < 1f)
                return;
            _view.MovePosition(_model.SegmentTarget);
            if (Vector2.Distance(
                    _view.Body.position,
                    _model.SegmentTarget) > _config.PositionTolerance)
                return;
            _model.CompletePathStep(step.Value.Position);
            if (!_model.CurrentPathStep.HasValue)
                FinishMovement();
        }

        private void FinishMovement()
        {
            var purpose = _model.PathPurpose;
            _model.ClearPath();
            _view.Stop();
            switch (purpose)
            {
                case BatPathPurpose.Chase:
                    if (IsAttackValid() &&
                        _model.AttackCooldownRemaining <= 0f)
                        EnterAttack();
                    else if (!IsAttackValid())
                        StartContactApproach();
                    else
                        _view.Stop();
                    break;
                case BatPathPurpose.Perch:
                    BeginPerchApproach();
                    break;
                default:
                    _model.StartDecisionDelay(0f);
                    break;
            }
        }

        private void BeginPerchApproach()
        {
            _isApproachingPerch = true;
            _model.StartMovementTimeout(GetWorldMovementTimeout(
                Vector2.Distance(
                    _view.Body.position,
                    _perchWorldPosition)));
        }

        private void TickPerchApproach(float deltaTime)
        {
            if (_model.TickMovementTimeout(deltaTime))
            {
                EnterExplore();
                return;
            }
            var next = Vector2.MoveTowards(
                _view.Body.position,
                _perchWorldPosition,
                _config.MoveSpeed * Mathf.Max(0f, deltaTime));
            _view.MovePosition(next);
            if (Vector2.Distance(
                    _view.Body.position,
                    _perchWorldPosition) > _config.PositionTolerance)
                return;
            _view.Teleport(_perchWorldPosition);
            _model.SetGridPosition(
                _placement.WorldToGrid(_perchWorldPosition));
            _isApproachingPerch = false;
            EnterIdle();
        }

        private void HandleMovementFailure()
        {
            var purpose = _model.PathPurpose;
            if (purpose == BatPathPurpose.Chase &&
                _target.IsTargetAvailable &&
                IsWithinChaseExitRange())
            {
                CancelPathRequest();
                _model.BeginEngagement();
                if (_model.CurrentState != BatState.Chase)
                    ChangeState(BatState.Chase);
                _view.Stop();
                _model.StartDecisionDelay(_config.DecisionRetryDelay);
                return;
            }
            EnterExplore();
            _model.StartDecisionDelay(_config.DecisionRetryDelay);
        }

        private void EnterExplore()
        {
            CancelPathRequest();
            _isApproachingPerch = false;
            _idleToFlyPlaying = false;
            _model.EndIdle();
            _model.SetGridPosition(
                _placement.WorldToGrid(_view.Body.position));
            _model.StartDecisionDelay(0f);
            ChangeState(BatState.Explore);
        }

        private void EnterIdle()
        {
            CancelPathRequest();
            _model.EndIdle();
            _idleToFlyPlaying = false;
            ChangeState(BatState.Idle);
        }

        private void EnterAttack()
        {
            CancelPathRequest();
            _view.Stop();
            ChangeState(BatState.Attack);
        }

        private void EnterDeath()
        {
            CancelPathRequest();
            _model.EndEngagement();
            _model.EndIdle();
            _view.SetDamageEnabled(false);
            _view.Stop();
            ChangeState(BatState.Death);
        }

        private void EndEngagementAndExplore()
        {
            _model.EndEngagement();
            EnterExplore();
        }

        private void ResumeAfterAttack()
        {
            if (_model.PendingDeath)
            {
                EnterHurt();
                return;
            }
            if (_model.EngagementActive &&
                _target.IsTargetAvailable &&
                IsWithinChaseExitRange())
            {
                ChangeState(BatState.Chase);
                if (!IsAttackValid())
                    RequestChaseRoute();
                return;
            }
            EndEngagementAndExplore();
        }

        private void HandleIdleAnimationCompleted(string animationId)
        {
            if (animationId == BatAnimationId.FlyToIdle)
            {
                PlayAnimation(BatAnimationId.Idle, false);
                _model.StartIdle(UnityEngine.Random.Range(
                    _config.MinimumIdleDuration,
                    _config.MaximumIdleDuration));
            }
            else if (animationId == BatAnimationId.IdleToFly)
            {
                EnterExplore();
            }
        }

        private void ChangeState(BatState state)
        {
            _attackApplied = false;
            _model.SetState(state);
            _view.Stop();
            if (state == BatState.Attack && _target.IsTargetAvailable)
            {
                _view.SetFacing(
                    _target.BodyPosition.x < _view.Body.position.x);
            }

            var animationId = state switch
            {
                BatState.Idle => BatAnimationId.FlyToIdle,
                BatState.Explore => BatAnimationId.Fly,
                BatState.Chase => BatAnimationId.Fly,
                BatState.Attack => BatAnimationId.Attack,
                BatState.Hurt => BatAnimationId.Hurt,
                _ => BatAnimationId.Death
            };
            var restart = state == BatState.Idle ||
                          state == BatState.Attack ||
                          state == BatState.Hurt ||
                          state == BatState.Death;
            PlayAnimation(animationId, restart);
        }

        private void PlayAnimation(string animationId, bool restart)
        {
            if (_config != null &&
                _config.AnimationProfile.TryGet(animationId, out var animation))
            {
                _animationGeneration = _view.Play(animation, restart);
                return;
            }
            HandleMissingAnimation(animationId);
        }

        private void HandleMissingAnimation(string animationId)
        {
            if (animationId == BatAnimationId.Death)
                CompleteDeath();
            else if (animationId == BatAnimationId.Hurt && _model.PendingDeath)
                EnterDeath();
            else if (animationId == BatAnimationId.FlyToIdle ||
                     animationId == BatAnimationId.IdleToFly ||
                     animationId == BatAnimationId.Attack ||
                     animationId == BatAnimationId.Hurt)
                EnterExplore();
        }

        private void CompleteDeath()
        {
            if (_deathSignalSent)
                return;
            _deathSignalSent = true;
            GlobalEventBus.Fire(new EnemyDiedSignal(_enemyId));
        }

        private bool IsWithinAggroRange() =>
            _target.IsTargetAvailable &&
            GridDistance(
                _placement.WorldToGrid(_view.Body.position),
                _target.GridPosition) <= _config.AggroRangeInTiles;

        private bool IsWithinChaseExitRange() =>
            _target.IsTargetAvailable &&
            GridDistance(
                _placement.WorldToGrid(_view.Body.position),
                _target.GridPosition) <= _config.ChaseExitRangeInTiles;

        private bool IsAttackValid()
        {
            if (!_target.IsTargetAvailable ||
                !IsWithinWorldDistance(_config.AttackContactDistance))
                return false;

            if (_config.AttackRangeInTiles <= 0)
                return true;

            return GridDistance(
                _placement.WorldToGrid(_view.Body.position),
                _target.GridPosition) <= _config.AttackRangeInTiles;
        }

        private bool IsWithinWorldDistance(float distance)
        {
            var delta = _target.BodyPosition - _view.Body.position;
            var safeDistance = Mathf.Max(0f, distance);
            return delta.sqrMagnitude <= safeDistance * safeDistance;
        }

        private bool IsNavigationChangeRelevant(GridPosition changedPosition)
        {
            if (_model.CurrentGridPosition == changedPosition ||
                _model.Destination == changedPosition)
                return true;
            var path = _model.CachedPath;
            if (path == null)
                return false;
            for (var i = _model.PathIndex; i < path.Count; i++)
            {
                if (path[i].Position == changedPosition)
                    return true;
            }
            return false;
        }

        private float GetMovementTimeout(IReadOnlyList<EnemyPathStep> steps)
        {
            var previous = _view.Body.position;
            var distance = 0f;
            for (var i = 0; i < steps.Count; i++)
            {
                var next = _placement.GridToWorld(steps[i].Position);
                distance += Vector2.Distance(previous, next);
                previous = next;
            }
            return GetWorldMovementTimeout(distance);
        }

        private float GetWorldMovementTimeout(float distance)
        {
            var speed = Mathf.Max(0.01f, _config.MoveSpeed);
            return Mathf.Max(
                _config.MinimumMovementTimeoutSeconds,
                Mathf.Max(0f, distance) / speed +
                _config.MovementStuckBufferSeconds);
        }

        private static int GridDistance(GridPosition a, GridPosition b) =>
            Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

        private void CancelPathRequest()
        {
            CancelPathComputation();
            _model.EndContactApproach();
            _model.ClearPath();
        }

        private void CancelPathComputation()
        {
            if (_pathCancellation == null)
                return;
            _pathCancellation.Cancel();
            _pathCancellation.Dispose();
            _pathCancellation = null;
        }

        private void DisposePathCancellation()
        {
            _pathCancellation?.Dispose();
            _pathCancellation = null;
        }

        private static void HandlePathException(Exception exception)
        {
            if (exception is not OperationCanceledException)
                Debug.LogException(exception);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Release();
            _pauseGate.Dispose();
        }
    }
}
