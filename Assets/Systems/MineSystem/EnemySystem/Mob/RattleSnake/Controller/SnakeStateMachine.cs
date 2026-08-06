using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Damage;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Config;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Enum;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Model;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.EnemySystem.Signal;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Service;
using Systems.Utilities.EventBus;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.RattleSnake.Controller
{
    public sealed class SnakeStateMachine : IDisposable
    {
        private readonly SnakeModel _model;
        private readonly SnakeView _view;
        private readonly IEnemyTargetProvider _target;
        private readonly IEnemyAttackService _attack;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly IEnemyPlacementValidator _placement;
        private readonly IEnemyChaseTargetResolver _chaseResolver;
        private readonly PauseGate _pauseGate = new();

        private SnakeConfigScriptable _config;
        private Guid _enemyId;
        private CancellationToken _lifetimeToken;
        private CancellationTokenSource _pathCancellation;
        private UniTaskCompletionSource _stateCompletion;
        private IDamageable _placeableTarget;
        private Vector2 _placeableWorldPosition;
        private GridPosition _chaseTargetGrid;
        private GridPosition _observedTargetGrid;
        private GridPosition _fallTarget;
        private GridPosition _fallStartGrid;
        private int _pathNavigationRevision;
        private int _animationGeneration;
        private bool _hasObservedTarget;
        private bool _observedCombatAvailable;
        private bool _fallHasLeftSupport;
        private bool _fallIsDirected;
        private bool _attackApplied;
        private bool _deathSignalSent;
        private bool _despawnSignalSent;
        private bool _disposed;

        public SnakeStateMachine(
            SnakeModel model,
            SnakeView view,
            IEnemyTargetProvider target,
            IEnemyAttackService attack,
            IEnemyPathfindingService pathfinding,
            IEnemyPlacementValidator placement,
            IEnemyChaseTargetResolver chaseResolver)
        {
            _model = model;
            _view = view;
            _target = target;
            _attack = attack;
            _pathfinding = pathfinding;
            _placement = placement;
            _chaseResolver = chaseResolver;
        }

        public void Initialize(
            SnakeConfigScriptable config,
            Guid enemyId,
            CancellationToken lifetimeToken)
        {
            CancelPathRequest();
            _config = config;
            _enemyId = enemyId;
            _lifetimeToken = lifetimeToken;
            _placeableTarget = null;
            _placeableWorldPosition = default;
            _chaseTargetGrid = default;
            _fallTarget = default;
            _fallStartGrid = default;
            _fallHasLeftSupport = false;
            _fallIsDirected = false;
            _attackApplied = false;
            _deathSignalSent = false;
            _despawnSignalSent = false;
            _hasObservedTarget = _target.IsTargetAvailable;
            _observedTargetGrid = _hasObservedTarget
                ? _target.GridPosition
                : default;
            _observedCombatAvailable = _target.IsCombatTargetAvailable;
            _pauseGate.Resume();
        }

        public UniTask SpawnAsync(CancellationToken cancellationToken) =>
            EnterLifecycleStateAsync(SnakeState.Spawn, cancellationToken);

        public async UniTask DespawnAsync(CancellationToken cancellationToken)
        {
            var cycleDuration = _view.CurrentAnimationCycleDuration;
            if (cycleDuration > 0.01f &&
                _model.CurrentState != SnakeState.Despawn &&
                _model.CurrentState != SnakeState.Death)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(cycleDuration),
                    cancellationToken: cancellationToken);
            }
            await EnterLifecycleStateAsync(
                SnakeState.Despawn,
                cancellationToken);
        }

        public void OnFixedTick(EnemyTickContext context)
        {
            if (_disposed || _pauseGate.IsPaused ||
                _model.IsDead && _model.CurrentState != SnakeState.Death)
                return;
            _model.TickCooldown(context.FixedDeltaTime);
            if (HandleTargetContextChanges())
                return;

            switch (_model.CurrentState)
            {
                case SnakeState.Idle:
                    if (!_model.PathPending &&
                        _model.TickIdle(context.FixedDeltaTime))
                        EvaluateDecision();
                    break;
                case SnakeState.Move:
                    TickMove(context.FixedDeltaTime);
                    break;
                case SnakeState.Fall:
                    TickFall(context.FixedDeltaTime);
                    break;
            }
        }

        public void HandleNavigationChanged(GridPosition changedPosition)
        {
            if (_pauseGate.IsPaused)
                return;
            if (_model.CurrentState == SnakeState.Fall)
            {
                if (_fallIsDirected &&
                    IsFallNavigationChangeRelevant(changedPosition))
                {
                    _fallIsDirected = false;
                    _view.SetVelocity(new Vector2(
                        0f,
                        _view.Body.linearVelocity.y));
                }
                return;
            }

            if (_model.HasReachabilityFailure)
                _model.ClearReachabilityFailure();
            if ((_model.CurrentState != SnakeState.Move &&
                 !_model.PathPending) ||
                !IsNavigationChangeRelevant(changedPosition))
                return;

            if (!_view.IsGrounded(
                    _config.GroundLayerMask,
                    _config.GroundProbeDistance))
            {
                EnterFall();
                return;
            }
            if (_model.CurrentState == SnakeState.Move &&
                IsCombatMovement() &&
                _model.EngagementActive &&
                _target.IsCombatTargetAvailable)
            {
                RefreshChaseRoute(true);
                return;
            }

            if (_model.CurrentState == SnakeState.Move &&
                _model.MovementMode == SnakeMovementMode.Patrol)
            {
                StartPatrolCorridor(true);
                return;
            }

            CancelPathRequest();
            _view.SetVelocity(Vector2.zero);
            EnterIdle();
        }

        public void HandleHorizontalCollision(Collider2D collider)
        {
            if (_pauseGate.IsPaused || collider == null || _config == null ||
                _model.CurrentState != SnakeState.Move)
                return;

            if (_view.IsTerrainWall(collider, _config.GroundLayerMask))
            {
                if (_model.MovementMode == SnakeMovementMode.Patrol)
                {
                    _model.ReversePatrolDirection();
                    _view.SetFacing(_model.PatrolDirection < 0);
                    StartPatrolCorridor(true);
                }
                else if (IsCombatMovement())
                {
                    RecordCurrentReachabilityFailure();
                    EndChaseAndPatrol();
                }
                return;
            }

            if (_model.MovementMode != SnakeMovementMode.Patrol ||
                _config.PlaceableCollisionBehavior !=
                    PlaceableCollisionBehavior.StopAndAttack ||
                _target.IsTargetCollider(collider) ||
                collider.GetComponentInParent<SnakeView>() != null ||
                !_view.TryGetDamageable(collider, out var damageable) ||
                _model.AttackCooldownRemaining > 0f)
                return;

            _placeableTarget = damageable;
            _placeableWorldPosition = collider.bounds.center;
            EnterAttack();
        }

        public void EnterHurt()
        {
            CancelPathRequest();
            _placeableTarget = null;
            _placeableWorldPosition = default;
            _model.SetMovementMode(SnakeMovementMode.None);
            _view.SetVelocity(Vector2.zero);
            ChangeState(SnakeState.Hurt);
        }

        public void EnterDeath()
        {
            CancelPathRequest();
            _placeableTarget = null;
            _model.SetMovementMode(SnakeMovementMode.None);
            _model.ResetEngagement();
            _view.SetDamageEnabled(false);
            _view.SetVelocity(Vector2.zero);
            ChangeState(SnakeState.Death);
        }

        public void HandleAnimationMarker(EnemyAnimationMarkerEvent animationEvent)
        {
            if (_pauseGate.IsPaused ||
                animationEvent.Generation != _animationGeneration ||
                _model.CurrentState != SnakeState.Attack ||
                animationEvent.AnimationId != SnakeAnimationId.Attack ||
                animationEvent.Marker != (int)EnemyAnimationMarker.AttackImpact ||
                _attackApplied)
                return;

            _attackApplied = true;
            if (_placeableTarget != null)
            {
                _placeableTarget.ApplyDamage(_config.Damage);
                return;
            }

            if (IsAttackValid())
                _attack.TryAttack(_config.Damage, _config.StatusEffect);
        }

        public void HandleAnimationCompleted(
            EnemyAnimationCompletedEvent animationEvent)
        {
            if (animationEvent.Generation != _animationGeneration)
                return;

            switch (_model.CurrentState)
            {
                case SnakeState.Spawn:
                    _view.SetDamageEnabled(true);
                    _stateCompletion?.TrySetResult();
                    EnterIdle();
                    break;
                case SnakeState.Attack:
                    _model.ResetAttackCooldown();
                    _placeableTarget = null;
                    _placeableWorldPosition = default;
                    EvaluateDecision();
                    break;
                case SnakeState.Hurt:
                    EvaluateDecision();
                    break;
                case SnakeState.Death:
                    if (!_deathSignalSent)
                    {
                        _deathSignalSent = true;
                        _stateCompletion?.TrySetResult();
                        GlobalEventBus.Fire(new EnemyDiedSignal(_enemyId));
                    }
                    break;
                case SnakeState.Despawn:
                    if (!_despawnSignalSent)
                    {
                        _despawnSignalSent = true;
                        _stateCompletion?.TrySetResult();
                        GlobalEventBus.Fire(new EnemyDespawnedSignal(_enemyId));
                    }
                    break;
            }
        }

        public void Pause() => _pauseGate.Pause();

        public void Resume() => _pauseGate.Resume();

        public void Release()
        {
            CancelPathRequest();
            _pauseGate.Resume();
            _stateCompletion?.TrySetCanceled();
            _stateCompletion = null;
            _placeableTarget = null;
            _placeableWorldPosition = default;
            _config = null;
        }

        private bool HandleTargetContextChanges()
        {
            var targetAvailable = _target.IsTargetAvailable;
            if (!targetAvailable)
            {
                var hadTarget = _hasObservedTarget;
                _hasObservedTarget = false;
                _observedCombatAvailable = false;
                _model.ResetEngagement();
                if (hadTarget &&
                    (IsCombatMovement() ||
                     _model.PathPending &&
                     _model.CurrentState == SnakeState.Idle))
                {
                    EndChaseAndPatrol();
                    return true;
                }
                return false;
            }

            var targetGrid = _target.GridPosition;
            var combatAvailable = _target.IsCombatTargetAvailable;
            var targetGridChanged = !_hasObservedTarget ||
                                    targetGrid != _observedTargetGrid;
            var combatAvailabilityChanged = !_hasObservedTarget ||
                                            combatAvailable !=
                                            _observedCombatAvailable;
            _hasObservedTarget = true;
            _observedTargetGrid = targetGrid;
            _observedCombatAvailable = combatAvailable;

            if (_model.EngagementActive && !IsWithinChaseExitRange())
            {
                _model.ResetEngagement();
                if (IsCombatMovement() ||
                    _model.PathPending &&
                    _model.CurrentState == SnakeState.Idle)
                {
                    EndChaseAndPatrol();
                    return true;
                }
                return false;
            }

            var beganEngagement = false;
            if (!_model.EngagementActive && IsWithinAggroDistance())
            {
                _model.BeginEngagement();
                beganEngagement = true;
            }

            if (targetGridChanged || combatAvailabilityChanged)
            {
                _model.ClearReachabilityFailure();
            }

            if (IsAnimationLockedState())
                return false;
            if (!combatAvailable)
            {
                if (IsCombatMovement() ||
                    _model.PathPending && _model.CurrentState == SnakeState.Idle)
                {
                    EnterIdle();
                    return true;
                }
                return false;
            }

            if (!_model.EngagementActive)
                return false;
            if (beganEngagement || combatAvailabilityChanged)
            {
                if (_model.CurrentState == SnakeState.Idle ||
                    _model.CurrentState == SnakeState.Move)
                {
                    EvaluateDecision();
                    return true;
                }
            }
            if (!targetGridChanged)
                return false;
            if (_model.CurrentState == SnakeState.Idle)
            {
                if (_model.PathPending)
                    return false;
                EvaluateDecision();
                return true;
            }
            if (_model.CurrentState != SnakeState.Move)
                return false;
            if (!IsCombatMovement())
            {
                EvaluateDecision();
                return true;
            }
            return HandleActiveChaseTargetMoved();
        }

        private bool HandleActiveChaseTargetMoved()
        {
            if (IsAttackValid())
            {
                if (_model.AttackCooldownRemaining <= 0f)
                    EnterAttack();
                else
                    EnterAttackCooldownHold();
                return true;
            }

            if (_model.MovementMode == SnakeMovementMode.ContactApproach ||
                _model.MovementMode == SnakeMovementMode.AttackCooldownHold)
            {
                if (CanUseContactApproach())
                    StartContactApproach(true);
                else
                    RefreshChaseRoute(true);
                return true;
            }
            if (IsApproachDestinationValid(
                    _model.Destination,
                    _target.GridPosition))
                return false;

            RefreshChaseRoute(false);
            return true;
        }

        private void EvaluateDecision()
        {
            if (_disposed || _config == null || _model.IsDead)
                return;
            CancelPathRequest();
            _model.SetMovementMode(SnakeMovementMode.None);
            _view.SetVelocity(Vector2.zero);

            if (!_placement.IsCurrentPlacementClear(_view.TerrainCollider))
            {
                EnterIdle();
                return;
            }
            _model.SetGridPosition(
                _placement.WorldToGrid(_view.Body.position));
            if (!_view.IsGrounded(
                    _config.GroundLayerMask,
                    _config.GroundProbeDistance))
            {
                EnterFall();
                return;
            }

            RefreshEngagement();
            if (_model.EngagementActive &&
                _target.IsCombatTargetAvailable)
            {
                if (IsAttackValid())
                {
                    if (_model.AttackCooldownRemaining <= 0f)
                        EnterAttack();
                    else
                        EnterAttackCooldownHold();
                    return;
                }

                if (!_model.IsReachabilityFailureCurrent(
                        _target.GridPosition,
                        _pathfinding.NavigationRevision))
                {
                    RequestChaseRoute();
                    return;
                }
            }

            StartPatrolCorridor();
        }

        private void RefreshEngagement()
        {
            if (!_target.IsTargetAvailable)
            {
                _model.ResetEngagement();
                return;
            }
            if (_model.EngagementActive)
            {
                if (!IsWithinChaseExitRange())
                    _model.ResetEngagement();
                return;
            }
            if (IsWithinAggroDistance())
                _model.BeginEngagement();
        }

        private void EnterIdle()
        {
            CancelPathRequest();
            _model.SetMovementMode(SnakeMovementMode.None);
            _placeableTarget = null;
            _placeableWorldPosition = default;
            _model.ResetRepositionFailures();
            _model.StartIdle(_config != null ? _config.IdleDuration : 0f);
            ChangeState(SnakeState.Idle);
        }

        private void EnterFallRecoveryIdle()
        {
            _model.SetMovementMode(SnakeMovementMode.None);
            _placeableTarget = null;
            _placeableWorldPosition = default;
            _model.StartIdle(_config != null ? _config.IdleDuration : 0f);
            ChangeState(SnakeState.Idle, true);
        }

        private void StartPatrolCorridor(bool preserveMoveAnimation = false)
        {
            if (_config == null)
                return;
            CancelPathRequest();
            var current = _placement.WorldToGrid(_view.Body.position);
            _model.SetGridPosition(current);
            _model.SetMovementMode(SnakeMovementMode.Patrol);
            if (!_pathfinding.IsWalkable(current))
            {
                EnterFall();
                return;
            }
            if (!_placement.TryGetPlacement(
                    _view.TerrainCollider,
                    current,
                    out _))
            {
                EnterIdle();
                return;
            }

            var range = Mathf.Max(0, _config.PatrolRangeInTiles);
            var leftCount = CountPatrolCells(current, -1, range);
            var rightCount = CountPatrolCells(current, 1, range);
            _model.BeginPatrolCorridor(range * 2 + 1);
            for (var offset = -leftCount; offset <= rightCount; offset++)
            {
                _model.AddPatrolCorridorCell(new GridPosition(
                    current.X + offset,
                    current.Y));
            }

            if (!_model.StartPatrolCorridor(leftCount))
            {
                HandlePatrolFailure();
                return;
            }

            if (_model.PatrolCorridor.Count <= 1)
            {
                HandlePatrolBoundary();
                return;
            }

            if (!preserveMoveAnimation ||
                _model.CurrentState != SnakeState.Move)
                ChangeState(SnakeState.Move);
            PrepareNextPatrolStep();
        }

        private int CountPatrolCells(
            GridPosition origin,
            int direction,
            int range)
        {
            var count = 0;
            for (var distance = 1; distance <= range; distance++)
            {
                var candidate = new GridPosition(
                    origin.X + direction * distance,
                    origin.Y);
                if (!IsValidPatrolCell(candidate))
                    break;
                count++;
            }
            return count;
        }

        private bool IsValidPatrolCell(GridPosition position) =>
            _pathfinding.IsWalkable(position) &&
            _placement.TryGetPlacement(
                _view.TerrainCollider,
                position,
                out _);

        private void PrepareNextPatrolStep()
        {
            if (!_model.TryGetNextPatrolStep(out var step))
            {
                HandlePatrolBoundary();
                return;
            }

            var targetPosition = _placement.GridToWorld(step.Position);
            _model.StartMovementTimeout(GetWorldMovementTimeout(
                Vector2.Distance(_view.Body.position, targetPosition)));
            var horizontalDelta = targetPosition.x - _view.Body.position.x;
            if (Mathf.Abs(horizontalDelta) <= _config.PositionTolerance)
                return;
            var direction = Mathf.Sign(horizontalDelta);
            _view.SetFacing(direction < 0f);
            _view.SetVelocity(new Vector2(
                direction * _config.MoveSpeed,
                _view.Body.linearVelocity.y));
        }

        private void HandlePatrolBoundary()
        {
            if (TryChoosePatrolFall(out var fallLanding))
            {
                BeginFall(fallLanding);
                return;
            }

            if (_model.PatrolCorridor.Count <= 1)
            {
                HandlePatrolFailure();
                return;
            }

            _model.ReversePatrolDirection();
            _view.SetFacing(_model.PatrolDirection < 0);
            PrepareNextPatrolStep();
        }

        private bool TryChoosePatrolFall(out GridPosition landing)
        {
            landing = default;
            if (Mathf.Abs(
                    _model.CurrentGridPosition.X -
                    _model.PatrolCorridorOrigin.X) >=
                Mathf.Max(0, _config.PatrolRangeInTiles))
                return false;
            if (!_pathfinding.TryFindFallLanding(
                    _model.CurrentGridPosition,
                    _model.PatrolDirection,
                    _config.MaxFallDistanceInTiles,
                    out var candidate) ||
                !_placement.TryGetPlacement(
                    _view.TerrainCollider,
                    candidate,
                    out _) ||
                UnityEngine.Random.value >= 0.5f)
                return false;
            landing = candidate;
            return true;
        }

        private void RequestChaseRoute()
        {
            var preferredDestination = _model.Destination;
            CancelPathRequest();
            _chaseTargetGrid = _target.GridPosition;
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            _model.SetMovementMode(SnakeMovementMode.Chase);
            var generation = _model.BeginPathRequest(_chaseTargetGrid);
            var routeStart = _model.CurrentGridPosition;
            ChangeState(SnakeState.Move);
            _pathCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            FindChasePathAsync(
                    generation,
                    routeStart,
                    _chaseTargetGrid,
                    preferredDestination,
                    _pathNavigationRevision,
                    false,
                    _pathCancellation.Token)
                .Forget(HandlePathException);
        }

        private void RefreshChaseRoute(bool stopIfNoActivePath)
        {
            if (_model.CurrentState != SnakeState.Move ||
                !_model.EngagementActive ||
                !_target.IsCombatTargetAvailable ||
                !IsWithinChaseExitRange())
            {
                EndChaseAndPatrol();
                return;
            }

            var hasActivePath = _model.CurrentPathStep.HasValue;
            var preferredDestination = _model.Destination;
            CancelPendingPathRequestPreservingPath();
            _chaseTargetGrid = _target.GridPosition;
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            _model.SetMovementMode(SnakeMovementMode.Chase);
            var generation = _model.BeginPathRefresh();
            var routeStart = _model.CurrentGridPosition;
            if (!hasActivePath)
            {
                _model.ClearMovementTimeout();
                if (stopIfNoActivePath)
                    _view.SetVelocity(Vector2.zero);
            }
            _pathCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            FindChasePathAsync(
                    generation,
                    routeStart,
                    _chaseTargetGrid,
                    preferredDestination,
                    _pathNavigationRevision,
                    true,
                    _pathCancellation.Token)
                .Forget(HandlePathException);
        }

        private async UniTask FindChasePathAsync(
            int generation,
            GridPosition routeStart,
            GridPosition targetGrid,
            GridPosition preferredDestination,
            int navigationRevision,
            bool pathRefresh,
            CancellationToken cancellationToken)
        {
            var result = await _chaseResolver.FindReachablePathAsync(
                _view.TerrainCollider,
                routeStart,
                targetGrid,
                preferredDestination,
                Mathf.Max(1, _config.AttackRangeInTiles),
                EnemyMovementType.Crawling,
                _config.MaxFallDistanceInTiles,
                generation,
                0,
                false,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || _disposed)
                return;
            HandleChasePathResult(
                result,
                routeStart,
                targetGrid,
                navigationRevision,
                pathRefresh);
        }

        private void HandleChasePathResult(
            PathResult result,
            GridPosition routeStart,
            GridPosition targetGrid,
            int navigationRevision,
            bool pathRefresh)
        {
            if (result.Generation != _model.PathGeneration ||
                !_model.PathPending ||
                _model.CurrentState != SnakeState.Move ||
                _model.MovementMode != SnakeMovementMode.Chase)
                return;

            if (!_target.IsCombatTargetAvailable ||
                !_model.EngagementActive ||
                !IsWithinChaseExitRange())
            {
                EndChaseAndPatrol();
                return;
            }

            var targetContextIsUsable =
                _target.GridPosition == targetGrid ||
                (result.Succeeded &&
                 IsApproachDestinationValid(
                     result.Destination,
                     _target.GridPosition));
            if (_pathfinding.NavigationRevision != navigationRevision ||
                !IsPathStartCompatible(result, routeStart) ||
                !targetContextIsUsable)
            {
                RefreshChaseRoute(!_model.CurrentPathStep.HasValue);
                return;
            }

            var completed = pathRefresh
                ? _model.CompletePathRefresh(result)
                : _model.CompletePath(result);
            if (!completed)
                return;
            DisposePathCancellation();

            if (!result.Succeeded)
            {
                _model.RecordReachabilityFailure(
                    targetGrid,
                    navigationRevision);
                EndChaseAndPatrol();
                return;
            }

            _model.ClearReachabilityFailure();

            if (result.Steps == null || result.Steps.Count == 0)
            {
                _model.ClearPath();
                if (IsAttackValid())
                {
                    if (_model.AttackCooldownRemaining <= 0f)
                        EnterAttack();
                    else
                        EnterAttackCooldownHold();
                }
                else
                    StartContactApproach(true);
                return;
            }

            _model.StartMovementTimeout(GetMovementTimeout(result.Steps));
        }

        private void TickMove(float deltaTime)
        {
            if (!_placement.IsCurrentPlacementClear(_view.TerrainCollider))
            {
                EnterIdle();
                return;
            }
            if (!_view.IsGrounded(
                    _config.GroundLayerMask,
                    _config.GroundProbeDistance))
            {
                EnterFall();
                return;
            }

            if (_model.MovementMode == SnakeMovementMode.Patrol)
            {
                TickPatrolCorridor(deltaTime);
                return;
            }

            switch (_model.MovementMode)
            {
                case SnakeMovementMode.AttackCooldownHold:
                    TickAttackCooldownHold();
                    return;
                case SnakeMovementMode.ContactApproach:
                    if (_model.TickMovementTimeout(deltaTime))
                    {
                        RecordCurrentReachabilityFailure();
                        EndChaseAndPatrol();
                    }
                    else
                        TickContactApproach();
                    return;
                case SnakeMovementMode.Chase:
                    if (IsAttackValid())
                    {
                        if (_model.AttackCooldownRemaining <= 0f)
                            EnterAttack();
                        else
                            EnterAttackCooldownHold();
                        return;
                    }
                    break;
            }

            if (_model.TickMovementTimeout(deltaTime))
            {
                HandleMovementTimeout();
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
            if (step.Value.Type == EnemyPathStepType.Fall)
            {
                BeginFall(step.Value.Position);
                return;
            }

            if (!_placement.TryGetPlacement(
                    _view.TerrainCollider,
                    step.Value.Position,
                    out var targetPosition))
            {
                HandlePatrolOrChasePathFailure();
                return;
            }

            var bodyPosition = _view.Body.position;
            var direction = Mathf.Sign(targetPosition.x - bodyPosition.x);
            if (Mathf.Abs(targetPosition.x - bodyPosition.x) <=
                _config.PositionTolerance)
            {
                _view.SetVelocity(Vector2.zero);
                _model.CompletePathStep(step.Value.Position);
                FinishMovementStep();
                return;
            }

            _view.SetFacing(direction < 0f);
            _view.SetVelocity(new Vector2(
                direction * _config.MoveSpeed,
                _view.Body.linearVelocity.y));
        }

        private void TickPatrolCorridor(float deltaTime)
        {
            if (_model.TickMovementTimeout(deltaTime))
            {
                HandleMovementTimeout();
                return;
            }
            if (!_model.TryGetNextPatrolStep(out var step))
            {
                HandlePatrolBoundary();
                return;
            }
            if (!_placement.TryGetPlacement(
                    _view.TerrainCollider,
                    step.Position,
                    out var targetPosition) ||
                !_pathfinding.IsWalkable(step.Position))
            {
                StartPatrolCorridor(true);
                return;
            }

            var bodyPosition = _view.Body.position;
            var horizontalDelta = targetPosition.x - bodyPosition.x;
            if (Mathf.Abs(horizontalDelta) <= _config.PositionTolerance)
            {
                if (!_model.CompletePatrolStep())
                {
                    HandlePatrolFailure();
                    return;
                }
                PrepareNextPatrolStep();
                return;
            }

            var direction = Mathf.Sign(horizontalDelta);
            _view.SetFacing(direction < 0f);
            _view.SetVelocity(new Vector2(
                direction * _config.MoveSpeed,
                _view.Body.linearVelocity.y));
        }

        private void TickAttackCooldownHold()
        {
            _view.SetVelocity(Vector2.zero);
            FaceTarget();
            if (!IsAttackValid())
            {
                ContinueChaseFromCurrentMove();
                return;
            }
            if (_model.AttackCooldownRemaining <= 0f)
                EnterAttack();
        }

        private void StartContactApproach(bool preserveMoveAnimation = false)
        {
            CancelPathRequest();
            _chaseTargetGrid = _target.GridPosition;
            _model.SetMovementMode(SnakeMovementMode.ContactApproach);
            _model.StartMovementTimeout(GetWorldMovementTimeout(
                Vector2.Distance(
                    _view.Body.position,
                    _target.WorldPosition)));
            if (!preserveMoveAnimation ||
                _model.CurrentState != SnakeState.Move)
                ChangeState(SnakeState.Move);
        }

        private void TickContactApproach()
        {
            if (IsAttackValid())
            {
                if (_model.AttackCooldownRemaining <= 0f)
                    EnterAttack();
                else
                    EnterAttackCooldownHold();
                return;
            }

            var currentGrid = _placement.WorldToGrid(_view.Body.position);
            if (currentGrid.Y != _target.GridPosition.Y)
            {
                RefreshChaseRoute(true);
                return;
            }
            var deltaX = _target.WorldPosition.x - _view.Body.position.x;
            var direction = Mathf.Sign(deltaX);
            if (Mathf.Approximately(direction, 0f))
            {
                RecordCurrentReachabilityFailure();
                EndChaseAndPatrol();
                return;
            }
            var forward = new GridPosition(
                currentGrid.X + (direction < 0f ? -1 : 1),
                currentGrid.Y);
            if (_target.GridPosition != currentGrid &&
                !_pathfinding.IsWalkable(forward))
            {
                RefreshChaseRoute(true);
                return;
            }

            _view.SetFacing(direction < 0f);
            _view.SetVelocity(new Vector2(
                direction * _config.MoveSpeed,
                _view.Body.linearVelocity.y));
        }

        private void EnterAttackCooldownHold()
        {
            var preserveMoveAnimation =
                _model.CurrentState == SnakeState.Move;
            CancelPathRequest();
            _model.SetMovementMode(SnakeMovementMode.AttackCooldownHold);
            _chaseTargetGrid = _target.GridPosition;
            _view.SetVelocity(Vector2.zero);
            FaceTarget();
            if (!preserveMoveAnimation)
                ChangeState(SnakeState.Move);
        }

        private void FinishMovementStep()
        {
            var nextStep = _model.CurrentPathStep;
            if (nextStep.HasValue)
            {
                if (nextStep.Value.Type == EnemyPathStepType.Fall)
                    BeginFall(nextStep.Value.Position);
                return;
            }
            FinishMovement();
        }

        private void FinishMovement()
        {
            var completedMode = _model.MovementMode;
            if (completedMode == SnakeMovementMode.Chase &&
                _model.PathRefreshPending)
            {
                _model.ClearMovementTimeout();
                _view.SetVelocity(Vector2.zero);
                return;
            }
            _model.ClearPath();
            _view.SetVelocity(Vector2.zero);
            if (completedMode == SnakeMovementMode.Chase)
                ContinueChaseFromCurrentMove();
            else
                EnterIdle();
        }

        private void ContinueChaseFromCurrentMove()
        {
            if (!_model.EngagementActive ||
                !_target.IsCombatTargetAvailable ||
                !IsWithinChaseExitRange())
            {
                EndChaseAndPatrol();
                return;
            }
            if (IsAttackValid())
            {
                if (_model.AttackCooldownRemaining <= 0f)
                    EnterAttack();
                else
                    EnterAttackCooldownHold();
                return;
            }
            if (CanUseContactApproach())
                StartContactApproach(true);
            else
                RefreshChaseRoute(true);
        }

        private void BeginFall(GridPosition targetPosition)
        {
            CancelPathRequest();
            _fallTarget = targetPosition;
            _fallStartGrid = _placement.WorldToGrid(_view.Body.position);
            _fallHasLeftSupport = false;
            _fallIsDirected = targetPosition != _fallStartGrid;
            _model.SetMovementMode(SnakeMovementMode.None);
            _model.StartMovementTimeout(GetWorldMovementTimeout(
                Vector2.Distance(
                    _view.Body.position,
                    _placement.GridToWorld(targetPosition))));
            ChangeState(SnakeState.Fall, true);
        }

        private void EnterFall()
        {
            CancelPathRequest();
            _fallStartGrid = _placement.WorldToGrid(_view.Body.position);
            _fallTarget = _fallStartGrid;
            _fallHasLeftSupport = !_view.IsGrounded(
                _config.GroundLayerMask,
                _config.GroundProbeDistance);
            _fallIsDirected = false;
            _model.SetMovementMode(SnakeMovementMode.None);
            var maximumFallWorld = Vector2.Distance(
                _placement.GridToWorld(_fallStartGrid),
                _placement.GridToWorld(new GridPosition(
                    _fallStartGrid.X,
                    _fallStartGrid.Y - _config.MaxFallDistanceInTiles)));
            _model.StartMovementTimeout(
                GetWorldMovementTimeout(maximumFallWorld));
            ChangeState(SnakeState.Fall, true);
        }

        private void TickFall(float deltaTime)
        {
            if (_model.TickMovementTimeout(deltaTime))
            {
                TryQuietReposition();
                return;
            }

            var currentGrid = _placement.WorldToGrid(_view.Body.position);
            var grounded = _view.IsGrounded(
                _config.GroundLayerMask,
                _config.GroundProbeDistance);
            if (!grounded &&
                (!_fallIsDirected ||
                 currentGrid.X == _fallTarget.X ||
                 currentGrid.Y < _fallStartGrid.Y))
                _fallHasLeftSupport = true;

            if (_fallHasLeftSupport && grounded)
            {
                _model.SetGridPosition(currentGrid);
                _model.ClearMovementTimeout();
                EnterIdle();
                return;
            }

            if (_fallStartGrid.Y - currentGrid.Y >
                _config.MaxFallDistanceInTiles)
            {
                TryQuietReposition();
                return;
            }

            if (!_fallIsDirected)
                return;
            var targetX = _placement.GridToWorld(_fallTarget).x;
            var deltaX = targetX - _view.Body.position.x;
            var horizontalVelocity = Mathf.Abs(deltaX) <=
                                     _config.PositionTolerance
                ? 0f
                : Mathf.Sign(deltaX) * _config.MoveSpeed;
            if (!Mathf.Approximately(horizontalVelocity, 0f))
                _view.SetFacing(horizontalVelocity < 0f);
            _view.SetVelocity(new Vector2(
                horizontalVelocity,
                _view.Body.linearVelocity.y));
        }

        private void TryQuietReposition()
        {
            var attempts = Mathf.Max(1, _config.DestinationRetries);
            for (var i = 0; i < attempts; i++)
            {
                var offset = UnityEngine.Random.Range(
                    0,
                    Mathf.Max(1, _pathfinding.WalkableCount));
                if (_pathfinding.TryFindWalkableNear(
                        _fallStartGrid,
                        0,
                        Mathf.Max(1, _config.MaxFallDistanceInTiles * 2),
                        offset,
                        out var candidate) &&
                    _placement.TryGetPlacement(
                        _view.TerrainCollider,
                        candidate,
                        out var worldPosition))
                {
                    CompleteQuietReposition(candidate, worldPosition);
                    return;
                }
            }

            for (var i = 0; i < attempts; i++)
            {
                var offset = UnityEngine.Random.Range(
                    0,
                    Mathf.Max(1, _pathfinding.WalkableCount));
                if (_pathfinding.TryFindAnyWalkable(offset, out var candidate) &&
                    _placement.TryGetPlacement(
                        _view.TerrainCollider,
                        candidate,
                        out var worldPosition))
                {
                    CompleteQuietReposition(candidate, worldPosition);
                    return;
                }
            }

            _model.RecordRepositionFailure();
            if (_model.RepositionFailureCount >= attempts)
            {
                DespawnWithoutAnimation();
                return;
            }
            EnterFallRecoveryIdle();
        }

        private void CompleteQuietReposition(
            GridPosition candidate,
            Vector2 worldPosition)
        {
            _model.ResetRepositionFailures();
            _view.Teleport(worldPosition);
            _model.SetGridPosition(candidate);
            _fallStartGrid = candidate;
            _fallTarget = candidate;
            _fallHasLeftSupport = false;
            _fallIsDirected = false;
            _model.StartMovementTimeout(GetWorldMovementTimeout(
                Vector2.Distance(
                    worldPosition,
                    _placement.GridToWorld(new GridPosition(
                        candidate.X,
                        candidate.Y - _config.MaxFallDistanceInTiles)))));
        }

        private void DespawnWithoutAnimation()
        {
            if (_despawnSignalSent)
                return;
            _despawnSignalSent = true;
            _model.ResetRepositionFailures();
            _stateCompletion?.TrySetResult();
            GlobalEventBus.Fire(new EnemyDespawnedSignal(_enemyId));
        }

        private void EndChaseAndPatrol()
        {
            CancelPathRequest();
            _model.SetMovementMode(SnakeMovementMode.None);
            EnterIdle();
        }

        private void HandlePatrolFailure()
        {
            _model.ReversePatrolDirection();
            _view.SetFacing(_model.PatrolDirection < 0);
            EnterIdle();
        }

        private void HandlePatrolOrChasePathFailure()
        {
            var mode = _model.MovementMode;
            _model.ClearPath();
            _view.SetVelocity(Vector2.zero);
            if (mode == SnakeMovementMode.Chase)
            {
                RecordCurrentReachabilityFailure();
                EndChaseAndPatrol();
            }
            else
                HandlePatrolFailure();
        }

        private void HandleMovementTimeout()
        {
            var mode = _model.MovementMode;
            _model.ClearPath();
            _model.ClearPatrolCorridor();
            _view.SetVelocity(Vector2.zero);
            if (!_placement.IsCurrentPlacementClear(_view.TerrainCollider))
                EnterIdle();
            else if (mode == SnakeMovementMode.Chase ||
                     mode == SnakeMovementMode.ContactApproach)
            {
                RecordCurrentReachabilityFailure();
                EndChaseAndPatrol();
            }
            else if (mode == SnakeMovementMode.Patrol)
                HandlePatrolFailure();
            else
                EnterIdle();
        }

        private void RecordCurrentReachabilityFailure()
        {
            if (_target.IsTargetAvailable)
            {
                _model.RecordReachabilityFailure(
                    _target.GridPosition,
                    _pathfinding.NavigationRevision);
            }
        }

        private bool IsWithinAggroDistance() =>
            _target.IsTargetAvailable &&
            IsWithinWorldDistance(_config.AggroDistance);

        private bool IsWithinChaseExitRange() =>
            _target.IsTargetAvailable &&
            GridDistance(
                _placement.WorldToGrid(_view.Body.position),
                _target.GridPosition) <= _config.ChaseExitRangeInTiles;

        private bool IsAttackValid()
        {
            if (!_target.IsCombatTargetAvailable ||
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
            var delta = _target.WorldPosition - _view.Body.position;
            var safeDistance = Mathf.Max(0f, distance);
            return delta.sqrMagnitude <= safeDistance * safeDistance;
        }

        private bool IsApproachDestinationValid(
            GridPosition destination,
            GridPosition targetPosition) =>
            _pathfinding.IsWalkable(destination) &&
            GridDistance(destination, targetPosition) <=
            Mathf.Max(1, _config.AttackRangeInTiles);

        private bool IsPathStartCompatible(
            PathResult result,
            GridPosition routeStart)
        {
            if (_model.CurrentGridPosition == routeStart)
                return true;
            return result.Succeeded &&
                   result.Steps != null &&
                   result.Steps.Count > 0 &&
                   result.Steps[0].Position == _model.CurrentGridPosition;
        }

        private bool CanUseContactApproach()
        {
            var currentGrid = _placement.WorldToGrid(_view.Body.position);
            var targetGrid = _target.GridPosition;
            if (currentGrid.Y != targetGrid.Y)
                return false;
            var deltaX = _target.WorldPosition.x - _view.Body.position.x;
            if (Mathf.Abs(deltaX) <= _config.PositionTolerance)
                return false;
            if (targetGrid == currentGrid)
                return true;
            var forward = new GridPosition(
                currentGrid.X + (deltaX < 0f ? -1 : 1),
                currentGrid.Y);
            return _pathfinding.IsWalkable(forward);
        }

        private void EnterAttack()
        {
            if (_model.AttackCooldownRemaining > 0f)
            {
                EnterAttackCooldownHold();
                return;
            }
            CancelPathRequest();
            _model.SetMovementMode(SnakeMovementMode.None);
            _view.SetVelocity(Vector2.zero);
            ChangeState(SnakeState.Attack);
        }

        private void FaceTarget()
        {
            if (_target.IsTargetAvailable)
            {
                _view.SetFacing(
                    _target.WorldPosition.x < _view.Body.position.x);
            }
        }

        private bool IsNavigationChangeRelevant(GridPosition changedPosition)
        {
            if (_model.CurrentGridPosition == changedPosition ||
                _model.Destination == changedPosition)
                return true;
            var corridor = _model.PatrolCorridor;
            for (var i = 0; i < corridor.Count; i++)
            {
                var position = corridor[i].Position;
                if (position == changedPosition ||
                    position.X == changedPosition.X &&
                    position.Y - 1 == changedPosition.Y)
                    return true;
            }
            var path = _model.CachedPath;
            if (path != null)
            {
                for (var i = _model.PathIndex; i < path.Count; i++)
                {
                    if (path[i].Position == changedPosition)
                        return true;
                }
            }
            return Mathf.Abs(
                       _model.CurrentGridPosition.X - changedPosition.X) <= 1 &&
                   Mathf.Abs(
                       _model.CurrentGridPosition.Y - changedPosition.Y) <= 1;
        }

        private bool IsFallNavigationChangeRelevant(
            GridPosition changedPosition) =>
            changedPosition.X == _fallTarget.X &&
            changedPosition.Y <= _fallStartGrid.Y &&
            changedPosition.Y >= _fallTarget.Y - 1;

        private float GetMovementTimeout(
            IReadOnlyList<EnemyPathStep> steps)
        {
            var previous = _view.Body.position;
            var distance = 0f;
            for (var i = 0; i < steps.Count; i++)
            {
                var next = _placement.GridToWorld(steps[i].Position);
                distance += Mathf.Abs(next.x - previous.x) +
                            Mathf.Abs(next.y - previous.y);
                previous = next;
            }
            return GetWorldMovementTimeout(distance);
        }

        private float GetWorldMovementTimeout(float distance)
        {
            var speed = Mathf.Max(0.01f, _config.MoveSpeed);
            var duration = Mathf.Max(0f, distance) / speed +
                           _config.MovementStuckBufferSeconds;
            return Mathf.Max(
                _config.MinimumMovementTimeoutSeconds,
                duration);
        }

        private bool IsCombatMovement() =>
            _model.MovementMode == SnakeMovementMode.Chase ||
            _model.MovementMode == SnakeMovementMode.ContactApproach ||
            _model.MovementMode == SnakeMovementMode.AttackCooldownHold;

        private bool IsAnimationLockedState() =>
            _model.CurrentState == SnakeState.Spawn ||
            _model.CurrentState == SnakeState.Attack ||
            _model.CurrentState == SnakeState.Hurt ||
            _model.CurrentState == SnakeState.Fall ||
            _model.CurrentState == SnakeState.Despawn ||
            _model.CurrentState == SnakeState.Death;

        private static int GridDistance(GridPosition a, GridPosition b) =>
            Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

        private async UniTask EnterLifecycleStateAsync(
            SnakeState state,
            CancellationToken cancellationToken)
        {
            _stateCompletion = new UniTaskCompletionSource();
            CancelPathRequest();
            _model.SetMovementMode(SnakeMovementMode.None);
            _view.SetVelocity(Vector2.zero);
            ChangeState(state);
            using var registration = cancellationToken.Register(
                () => _stateCompletion.TrySetCanceled(cancellationToken));
            await _stateCompletion.Task;
            _stateCompletion = null;
            if (state == SnakeState.Despawn && !_despawnSignalSent)
                _despawnSignalSent = true;
        }

        private void ChangeState(SnakeState state, bool preserveVelocity = false)
        {
            _attackApplied = false;
            _model.SetState(state);
            if (!preserveVelocity)
                _view.SetVelocity(Vector2.zero);
            if (state == SnakeState.Attack)
            {
                var facesLeft = _placeableTarget != null
                    ? _view.Body.position.x > _placeableWorldPosition.x
                    : _target.IsTargetAvailable &&
                      _target.WorldPosition.x < _view.Body.position.x;
                _view.SetFacing(facesLeft);
            }

            var animationId = GetAnimationId(state);
            if (_config == null ||
                !_config.AnimationProfile.TryGet(animationId, out var animation))
            {
                HandleMissingAnimation(state);
                return;
            }
            _animationGeneration = _view.Play(animation, true);
        }

        private static string GetAnimationId(SnakeState state)
        {
            return state switch
            {
                SnakeState.Spawn => SnakeAnimationId.Spawn,
                SnakeState.Idle => SnakeAnimationId.Idle,
                SnakeState.Move => SnakeAnimationId.Move,
                SnakeState.Attack => SnakeAnimationId.Attack,
                SnakeState.Hurt => SnakeAnimationId.Hurt,
                SnakeState.Fall => SnakeAnimationId.Fall,
                SnakeState.Despawn => SnakeAnimationId.Despawn,
                _ => SnakeAnimationId.Death
            };
        }

        private void HandleMissingAnimation(SnakeState state)
        {
            if (state == SnakeState.Spawn)
            {
                _view.SetDamageEnabled(true);
                _stateCompletion?.TrySetResult();
                EnterIdle();
            }
            else if (state == SnakeState.Death && !_deathSignalSent)
            {
                _deathSignalSent = true;
                _stateCompletion?.TrySetResult();
                GlobalEventBus.Fire(new EnemyDiedSignal(_enemyId));
            }
            else if (state == SnakeState.Despawn && !_despawnSignalSent)
            {
                _despawnSignalSent = true;
                _stateCompletion?.TrySetResult();
                GlobalEventBus.Fire(new EnemyDespawnedSignal(_enemyId));
            }
            else
                EnterIdle();
        }

        private static void HandlePathException(Exception exception)
        {
            if (exception is not OperationCanceledException)
                Debug.LogException(exception);
        }

        private void CancelPathRequest()
        {
            CancelPathComputation();
            _model.ClearPath();
            _model.ClearPatrolCorridor();
        }

        private void CancelPendingPathRequestPreservingPath()
        {
            CancelPathComputation();
            _model.CancelPendingPathRequest();
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
