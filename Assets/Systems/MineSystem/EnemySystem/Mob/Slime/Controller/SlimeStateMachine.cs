using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Damage;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.Slime.Config;
using Systems.MineSystem.EnemySystem.Mob.Slime.Enum;
using Systems.MineSystem.EnemySystem.Mob.Slime.Model;
using Systems.MineSystem.EnemySystem.Mob.Slime.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.EnemySystem.Signal;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Service;
using Systems.Utilities.EventBus;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.Controller
{
    public sealed class SlimeStateMachine : IDisposable
    {
        private readonly SlimeModel _model;
        private readonly SlimeView _view;
        private readonly IEnemyTargetProvider _target;
        private readonly IEnemyAttackService _attack;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly IEnemyPlacementValidator _placement;
        private readonly IEnemyChaseTargetResolver _chaseResolver;
        private readonly PauseGate _pauseGate = new();

        private SlimeConfigScriptable _config;
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
        private Vector2 _teleportWorldPosition;
        private int _pathNavigationRevision;
        private int _animationGeneration;
        private bool _hasObservedTarget;
        private bool _observedCombatAvailable;
        private bool _fallHasLeftSupport;
        private bool _fallIsDirected;
        private bool _attackApplied;
        private bool _hasTeleportDestination;
        private bool _deathSignalSent;
        private bool _despawnSignalSent;
        private bool _disposed;

        public SlimeStateMachine(
            SlimeModel model,
            SlimeView view,
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
            SlimeConfigScriptable config,
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
            _hasTeleportDestination = false;
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
            EnterLifecycleStateAsync(SlimeState.Spawn, cancellationToken);

        public async UniTask DespawnAsync(CancellationToken cancellationToken)
        {
            var cycleDuration = _view.CurrentAnimationCycleDuration;
            if (cycleDuration > 0.01f &&
                _model.CurrentState != SlimeState.Despawn &&
                _model.CurrentState != SlimeState.Death)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(cycleDuration),
                    cancellationToken: cancellationToken);
            }
            await EnterLifecycleStateAsync(
                SlimeState.Despawn,
                cancellationToken);
        }

        public void OnFixedTick(EnemyTickContext context)
        {
            if (_disposed || _pauseGate.IsPaused ||
                _model.IsDead && _model.CurrentState != SlimeState.Death)
                return;

            _model.TickCooldown(context.FixedDeltaTime);
            if (HandleTargetContextChanges())
                return;

            switch (_model.CurrentState)
            {
                case SlimeState.Idle:
                    if (!_model.PathPending &&
                        _model.TickIdle(context.FixedDeltaTime))
                        EvaluateDecision();
                    break;
                case SlimeState.Move:
                    TickMove(context.FixedDeltaTime);
                    break;
                case SlimeState.Fall:
                    TickFall(context.FixedDeltaTime);
                    break;
            }
        }

        public void HandleNavigationChanged(GridPosition changedPosition)
        {
            if (_pauseGate.IsPaused)
                return;
            if (_model.CurrentState == SlimeState.Fall)
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
            if ((_model.CurrentState != SlimeState.Move &&
                 !_model.PathPending) ||
                !IsNavigationChangeRelevant(changedPosition))
                return;

            CancelPathRequest();
            _view.SetVelocity(Vector2.zero);
            if (!_view.IsGrounded(
                    _config.GroundLayerMask,
                    _config.GroundProbeDistance))
                EnterFall();
            else
                EnterIdle();
        }

        public void HandleHorizontalCollision(Collider2D collider)
        {
            if (_pauseGate.IsPaused || collider == null || _config == null ||
                _model.CurrentState != SlimeState.Move)
                return;

            if (_view.IsTerrainWall(collider, _config.GroundLayerMask))
            {
                if (_model.MovementMode == SlimeMovementMode.Patrol)
                {
                    _model.ReversePatrolDirection();
                    CancelPathRequest();
                    StartPatrolPath();
                }
                else if (IsCombatMovement())
                {
                    RecordCurrentReachabilityFailure();
                    EndChaseAndPatrol();
                }
                return;
            }

            if (_model.MovementMode != SlimeMovementMode.Patrol ||
                _config.PlaceableCollisionBehavior !=
                    PlaceableCollisionBehavior.StopAndAttack ||
                _target.IsTargetCollider(collider) ||
                collider.GetComponentInParent<SlimeView>() != null ||
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
            _model.SetMovementMode(SlimeMovementMode.None);
            _model.RequireAggroReplay();
            _view.SetVelocity(Vector2.zero);
            ChangeState(SlimeState.Hurt);
        }

        public void EnterDeath()
        {
            CancelPathRequest();
            _placeableTarget = null;
            _model.SetMovementMode(SlimeMovementMode.None);
            _model.ResetEngagement();
            _view.SetDamageEnabled(false);
            _view.SetVelocity(Vector2.zero);
            ChangeState(SlimeState.Death);
        }

        public void HandleAnimationMarker(EnemyAnimationMarkerEvent animationEvent)
        {
            if (_pauseGate.IsPaused ||
                animationEvent.Generation != _animationGeneration ||
                _model.CurrentState != SlimeState.Attack ||
                animationEvent.AnimationId != SlimeAnimationId.Attack ||
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
                case SlimeState.Spawn:
                    _view.SetDamageEnabled(true);
                    _stateCompletion?.TrySetResult();
                    EnterIdle();
                    break;
                case SlimeState.Aggro:
                    _model.MarkAggroPlayed();
                    EvaluateDecision();
                    break;
                case SlimeState.Attack:
                    _model.ResetAttackCooldown();
                    _placeableTarget = null;
                    _placeableWorldPosition = default;
                    EvaluateDecision();
                    break;
                case SlimeState.Hurt:
                    EvaluateDecision();
                    break;
                case SlimeState.TeleportDespawn:
                    if (_hasTeleportDestination)
                    {
                        _view.Teleport(_teleportWorldPosition);
                        _model.SetGridPosition(
                            _placement.WorldToGrid(_teleportWorldPosition));
                        ChangeState(SlimeState.TeleportSpawn);
                    }
                    else
                        EnterIdle();
                    break;
                case SlimeState.TeleportSpawn:
                    _model.StartTeleportCooldown(
                        _config.TeleportCooldownSeconds);
                    _hasTeleportDestination = false;
                    EnterIdle();
                    break;
                case SlimeState.Death:
                    if (!_deathSignalSent)
                    {
                        _deathSignalSent = true;
                        _stateCompletion?.TrySetResult();
                        GlobalEventBus.Fire(new EnemyDiedSignal(_enemyId));
                    }
                    break;
                case SlimeState.Despawn:
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
                if (hadTarget && IsCombatMovement())
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
                if (IsCombatMovement())
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
                _model.ClearReachabilityFailure();

            if (IsAnimationLockedState())
                return false;
            if (!combatAvailable)
            {
                if (IsCombatMovement() ||
                    _model.PathPending && _model.CurrentState == SlimeState.Idle)
                {
                    EnterIdle();
                    return true;
                }
                return false;
            }

            if (!_model.EngagementActive)
                return false;
            if (beganEngagement || targetGridChanged ||
                combatAvailabilityChanged)
            {
                if (_model.CurrentState == SlimeState.Idle ||
                    _model.CurrentState == SlimeState.Move)
                {
                    EvaluateDecision();
                    return true;
                }
            }
            return false;
        }

        private void EvaluateDecision()
        {
            if (_disposed || _config == null || _model.IsDead)
                return;
            CancelPathRequest();
            _model.SetMovementMode(SlimeMovementMode.None);
            _view.SetVelocity(Vector2.zero);

            if (!_placement.IsCurrentPlacementClear(_view.TerrainCollider))
            {
                StartEmergencyTeleport();
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
                    if (!_model.AggroPlayedForEngagement)
                    {
                        EnterAggro();
                        return;
                    }
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
                    RequestChaseRoute(
                        !_model.AggroPlayedForEngagement);
                    return;
                }
            }

            if (ShouldTeleport())
                StartTeleport(false);
            else
                StartPatrolPath();
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
            _model.SetMovementMode(SlimeMovementMode.None);
            _placeableTarget = null;
            _placeableWorldPosition = default;
            _model.StartIdle(_config != null ? _config.IdleDuration : 0f);
            ChangeState(SlimeState.Idle);
        }

        private void EnterAggro()
        {
            CancelPathRequest();
            _model.SetMovementMode(SlimeMovementMode.None);
            ChangeState(SlimeState.Aggro);
        }

        private void StartPatrolPath()
        {
            if (_config == null)
                return;
            CancelPathRequest();
            _model.SetGridPosition(
                _placement.WorldToGrid(_view.Body.position));
            _model.SetMovementMode(SlimeMovementMode.Patrol);
            if (!_pathfinding.TryFindFarthestDirectional(
                    _model.CurrentGridPosition,
                    _model.PatrolDirection,
                    _config.PatrolRangeInTiles,
                    out var destination))
            {
                HandlePatrolFailure();
                return;
            }

            _model.ResetPatrolFailures();
            RequestPatrolPath(destination);
        }

        private void RequestPatrolPath(GridPosition destination)
        {
            CancelPathRequest();
            _model.SetMovementMode(SlimeMovementMode.Patrol);
            var generation = _model.BeginPathRequest(destination, false);
            ChangeState(SlimeState.Move);
            _pathCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            var request = new EnemyPathRequest(
                _model.CurrentGridPosition,
                destination,
                _config.MaxFallDistanceInTiles,
                generation,
                false);
            FindPatrolPathAsync(request, _pathCancellation.Token).Forget(
                HandlePathException);
        }

        private async UniTask FindPatrolPathAsync(
            EnemyPathRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _pathfinding.FindPathAsync(
                request,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || _disposed)
                return;
            HandlePatrolPathResult(result);
        }

        private void HandlePatrolPathResult(PathResult result)
        {
            if (_model.CurrentState != SlimeState.Move ||
                _model.MovementMode != SlimeMovementMode.Patrol ||
                !_model.CompletePath(result))
                return;
            DisposePathCancellation();
            if (!result.Succeeded || result.Steps == null ||
                result.Steps.Count == 0)
            {
                HandlePatrolFailure();
                return;
            }
            _model.StartMovementTimeout(GetMovementTimeout(result.Steps));
        }

        private void RequestChaseRoute(bool aggroProbe)
        {
            CancelPathRequest();
            _chaseTargetGrid = _target.GridPosition;
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            _model.SetMovementMode(
                aggroProbe
                    ? SlimeMovementMode.None
                    : SlimeMovementMode.Chase);
            var generation = _model.BeginPathRequest(
                _chaseTargetGrid,
                true);
            if (!aggroProbe)
                ChangeState(SlimeState.Move);
            _pathCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            FindChasePathAsync(
                    generation,
                    _chaseTargetGrid,
                    _pathNavigationRevision,
                    aggroProbe,
                    _pathCancellation.Token)
                .Forget(HandlePathException);
        }

        private async UniTask FindChasePathAsync(
            int generation,
            GridPosition targetGrid,
            int navigationRevision,
            bool aggroProbe,
            CancellationToken cancellationToken)
        {
            var result = await _chaseResolver.FindReachablePathAsync(
                _view.TerrainCollider,
                _model.CurrentGridPosition,
                targetGrid,
                Mathf.Max(1, _config.AttackRangeInTiles),
                _config.MaxFallDistanceInTiles,
                generation,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || _disposed)
                return;
            HandleChasePathResult(
                result,
                targetGrid,
                navigationRevision,
                aggroProbe);
        }

        private void HandleChasePathResult(
            PathResult result,
            GridPosition targetGrid,
            int navigationRevision,
            bool aggroProbe)
        {
            if (result.Generation != _model.PathGeneration ||
                !_model.PathPending ||
                aggroProbe && _model.CurrentState != SlimeState.Idle ||
                !aggroProbe &&
                (_model.CurrentState != SlimeState.Move ||
                 _model.MovementMode != SlimeMovementMode.Chase))
                return;
            if (!_model.CompletePath(result))
                return;
            DisposePathCancellation();

            var contextIsCurrent = _target.IsCombatTargetAvailable &&
                                   _target.GridPosition == targetGrid &&
                                   _pathfinding.NavigationRevision ==
                                   navigationRevision &&
                                   _model.EngagementActive;
            if (!contextIsCurrent)
            {
                _model.ClearPath();
                EvaluateDecision();
                return;
            }

            if (!result.Succeeded)
            {
                _model.RecordReachabilityFailure(
                    targetGrid,
                    navigationRevision);
                EndChaseAndPatrol();
                return;
            }

            _model.ClearReachabilityFailure();
            if (aggroProbe)
            {
                _model.ClearPath();
                EnterAggro();
                return;
            }

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
                    StartContactApproach();
                return;
            }

            _model.StartMovementTimeout(GetMovementTimeout(result.Steps));
        }

        private void TickMove(float deltaTime)
        {
            if (!_placement.IsCurrentPlacementClear(_view.TerrainCollider))
            {
                StartEmergencyTeleport();
                return;
            }
            if (!_view.IsGrounded(
                    _config.GroundLayerMask,
                    _config.GroundProbeDistance))
            {
                EnterFall();
                return;
            }

            switch (_model.MovementMode)
            {
                case SlimeMovementMode.AttackCooldownHold:
                    TickAttackCooldownHold();
                    return;
                case SlimeMovementMode.ContactApproach:
                    if (_model.TickMovementTimeout(deltaTime))
                    {
                        RecordCurrentReachabilityFailure();
                        EndChaseAndPatrol();
                    }
                    else
                        TickContactApproach();
                    return;
                case SlimeMovementMode.Chase:
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
            if (_model.PathPending)
                return;
            var step = _model.CurrentPathStep;
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

        private void TickAttackCooldownHold()
        {
            _view.SetVelocity(Vector2.zero);
            FaceTarget();
            if (!IsAttackValid())
            {
                EvaluateDecision();
                return;
            }
            if (_model.AttackCooldownRemaining <= 0f)
                EnterAttack();
        }

        private void StartContactApproach()
        {
            CancelPathRequest();
            _chaseTargetGrid = _target.GridPosition;
            _model.SetMovementMode(SlimeMovementMode.ContactApproach);
            _model.StartMovementTimeout(GetWorldMovementTimeout(
                Vector2.Distance(
                    _view.Body.position,
                    _target.WorldPosition)));
            ChangeState(SlimeState.Move);
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
                RecordCurrentReachabilityFailure();
                EndChaseAndPatrol();
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
                RecordCurrentReachabilityFailure();
                EndChaseAndPatrol();
                return;
            }

            _view.SetFacing(direction < 0f);
            _view.SetVelocity(new Vector2(
                direction * _config.MoveSpeed,
                _view.Body.linearVelocity.y));
        }

        private void EnterAttackCooldownHold()
        {
            CancelPathRequest();
            _model.SetMovementMode(SlimeMovementMode.AttackCooldownHold);
            _chaseTargetGrid = _target.GridPosition;
            _view.SetVelocity(Vector2.zero);
            FaceTarget();
            ChangeState(SlimeState.Move);
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
            _model.ClearPath();
            _view.SetVelocity(Vector2.zero);
            if (completedMode == SlimeMovementMode.Chase)
                EvaluateDecision();
            else
            {
                _model.ResetPatrolFailures();
                EnterIdle();
            }
        }

        private void BeginFall(GridPosition targetPosition)
        {
            CancelPathRequest();
            _fallTarget = targetPosition;
            _fallStartGrid = _placement.WorldToGrid(_view.Body.position);
            _fallHasLeftSupport = false;
            _fallIsDirected = targetPosition != _fallStartGrid;
            _model.SetMovementMode(SlimeMovementMode.None);
            _model.StartMovementTimeout(GetWorldMovementTimeout(
                Vector2.Distance(
                    _view.Body.position,
                    _placement.GridToWorld(targetPosition))));
            ChangeState(SlimeState.Fall, true);
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
            _model.SetMovementMode(SlimeMovementMode.None);
            var maximumFallWorld = Vector2.Distance(
                _placement.GridToWorld(_fallStartGrid),
                _placement.GridToWorld(new GridPosition(
                    _fallStartGrid.X,
                    _fallStartGrid.Y - _config.MaxFallDistanceInTiles)));
            _model.StartMovementTimeout(
                GetWorldMovementTimeout(maximumFallWorld));
            ChangeState(SlimeState.Fall, true);
        }

        private void TickFall(float deltaTime)
        {
            if (_model.TickMovementTimeout(deltaTime))
            {
                StartEmergencyTeleport();
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
                if (_placement.IsCurrentPlacementClear(_view.TerrainCollider))
                    EnterIdle();
                else
                    StartEmergencyTeleport();
                return;
            }

            if (_fallStartGrid.Y - currentGrid.Y >
                _config.MaxFallDistanceInTiles)
            {
                StartEmergencyTeleport();
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

        private void EndChaseAndPatrol()
        {
            CancelPathRequest();
            _model.SetMovementMode(SlimeMovementMode.None);
            EnterIdle();
        }

        private void HandlePatrolFailure()
        {
            _model.RecordPatrolFailure();
            _model.ReversePatrolDirection();
            _view.SetFacing(_model.PatrolDirection < 0);
            if (_model.PatrolFailureCount >= _config.DestinationRetries &&
                _model.CanTeleport)
            {
                StartTeleport(false);
                return;
            }
            EnterIdle();
        }

        private void HandlePatrolOrChasePathFailure()
        {
            var mode = _model.MovementMode;
            _model.ClearPath();
            _view.SetVelocity(Vector2.zero);
            if (mode == SlimeMovementMode.Chase)
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
            _view.SetVelocity(Vector2.zero);
            if (!_placement.IsCurrentPlacementClear(_view.TerrainCollider))
                StartEmergencyTeleport();
            else if (mode == SlimeMovementMode.Chase ||
                     mode == SlimeMovementMode.ContactApproach)
            {
                RecordCurrentReachabilityFailure();
                EndChaseAndPatrol();
            }
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
            Vector2.Distance(_view.Body.position, _target.WorldPosition) <=
            _config.AggroDistance;

        private bool IsWithinChaseExitRange() =>
            _target.IsTargetAvailable &&
            GridDistance(
                _placement.WorldToGrid(_view.Body.position),
                _target.GridPosition) <= _config.ChaseExitRangeInTiles;

        private bool IsAttackValid()
        {
            if (!_target.IsCombatTargetAvailable ||
                Vector2.Distance(_view.Body.position, _target.WorldPosition) >
                _config.AttackContactDistance)
                return false;
            if (_config.AttackRangeInTiles <= 0)
                return true;
            return GridDistance(
                       _placement.WorldToGrid(_view.Body.position),
                       _target.GridPosition) <= _config.AttackRangeInTiles;
        }

        private bool ShouldTeleport()
        {
            if (!_model.CanTeleport)
                return false;
            if (_model.PatrolFailureCount >= _config.DestinationRetries)
                return true;
            if (!_target.IsTargetAvailable)
                return false;
            return GridDistance(
                       _placement.WorldToGrid(_view.Body.position),
                       _target.GridPosition) >=
                   _config.TeleportTriggerDistanceInTiles &&
                   UnityEngine.Random.value <= _config.TeleportChance;
        }

        private void EnterAttack()
        {
            if (_model.AttackCooldownRemaining > 0f)
            {
                EnterAttackCooldownHold();
                return;
            }
            CancelPathRequest();
            _model.SetMovementMode(SlimeMovementMode.None);
            _view.SetVelocity(Vector2.zero);
            ChangeState(SlimeState.Attack);
        }

        private void FaceTarget()
        {
            if (_target.IsTargetAvailable)
            {
                _view.SetFacing(
                    _target.WorldPosition.x < _view.Body.position.x);
            }
        }

        private void StartEmergencyTeleport() => StartTeleport(true);

        private void StartTeleport(bool emergency)
        {
            if (!emergency && !_model.CanTeleport)
            {
                EnterIdle();
                return;
            }
            CancelPathRequest();
            _model.SetMovementMode(SlimeMovementMode.None);
            if (!TryFindTeleportDestination(out var worldPosition))
            {
                EnterIdle();
                return;
            }
            _teleportWorldPosition = worldPosition;
            _hasTeleportDestination = true;
            _view.SetVelocity(Vector2.zero);
            ChangeState(SlimeState.TeleportDespawn);
        }

        private bool TryFindTeleportDestination(out Vector2 worldPosition)
        {
            worldPosition = default;
            var attempts = Mathf.Max(1, _config.MaxTeleportAttempts);
            var playerPosition = _target.IsTargetAvailable
                ? _target.GridPosition
                : _model.CurrentGridPosition;
            for (var i = 0; i < attempts; i++)
            {
                var offset = UnityEngine.Random.Range(
                    0,
                    Mathf.Max(1, _pathfinding.WalkableCount));
                if (!_target.IsTargetAvailable ||
                    !_pathfinding.TryFindWalkableNear(
                        playerPosition,
                        _config.MinimumTeleportDistanceInTiles,
                        _config.MaximumTeleportDistanceInTiles,
                        offset,
                        out var candidate))
                    continue;
                if (_placement.TryGetPlacement(
                        _view.TerrainCollider,
                        candidate,
                        out worldPosition))
                    return true;
            }

            for (var i = 0; i < attempts; i++)
            {
                var offset = UnityEngine.Random.Range(
                    0,
                    Mathf.Max(1, _pathfinding.WalkableCount));
                if (!_pathfinding.TryFindAnyWalkable(offset, out var candidate))
                    continue;
                if (_placement.TryGetPlacement(
                        _view.TerrainCollider,
                        candidate,
                        out worldPosition))
                    return true;
            }
            return false;
        }

        private bool IsNavigationChangeRelevant(GridPosition changedPosition)
        {
            if (_model.CurrentGridPosition == changedPosition ||
                _model.Destination == changedPosition)
                return true;
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
            _model.MovementMode == SlimeMovementMode.Chase ||
            _model.MovementMode == SlimeMovementMode.ContactApproach ||
            _model.MovementMode == SlimeMovementMode.AttackCooldownHold;

        private bool IsAnimationLockedState() =>
            _model.CurrentState == SlimeState.Spawn ||
            _model.CurrentState == SlimeState.Aggro ||
            _model.CurrentState == SlimeState.Attack ||
            _model.CurrentState == SlimeState.Hurt ||
            _model.CurrentState == SlimeState.Fall ||
            _model.CurrentState == SlimeState.TeleportDespawn ||
            _model.CurrentState == SlimeState.TeleportSpawn ||
            _model.CurrentState == SlimeState.Despawn ||
            _model.CurrentState == SlimeState.Death;

        private static int GridDistance(GridPosition a, GridPosition b) =>
            Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

        private async UniTask EnterLifecycleStateAsync(
            SlimeState state,
            CancellationToken cancellationToken)
        {
            _stateCompletion = new UniTaskCompletionSource();
            CancelPathRequest();
            _model.SetMovementMode(SlimeMovementMode.None);
            _view.SetVelocity(Vector2.zero);
            ChangeState(state);
            using var registration = cancellationToken.Register(
                () => _stateCompletion.TrySetCanceled(cancellationToken));
            await _stateCompletion.Task;
            _stateCompletion = null;
            if (state == SlimeState.Despawn && !_despawnSignalSent)
                _despawnSignalSent = true;
        }

        private void ChangeState(SlimeState state, bool preserveVelocity = false)
        {
            _attackApplied = false;
            _model.SetState(state);
            if (!preserveVelocity)
                _view.SetVelocity(Vector2.zero);
            if (state == SlimeState.Attack)
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

        private static string GetAnimationId(SlimeState state)
        {
            return state switch
            {
                SlimeState.Spawn => SlimeAnimationId.Spawn,
                SlimeState.Idle => SlimeAnimationId.Idle,
                SlimeState.Aggro => SlimeAnimationId.Aggro,
                SlimeState.Move => SlimeAnimationId.Move,
                SlimeState.Attack => SlimeAnimationId.Attack,
                SlimeState.Hurt => SlimeAnimationId.Hurt,
                SlimeState.Fall => SlimeAnimationId.Fall,
                SlimeState.TeleportDespawn => SlimeAnimationId.Despawn,
                SlimeState.TeleportSpawn => SlimeAnimationId.Spawn,
                SlimeState.Despawn => SlimeAnimationId.Despawn,
                _ => SlimeAnimationId.Death
            };
        }

        private void HandleMissingAnimation(SlimeState state)
        {
            if (state == SlimeState.Spawn)
            {
                _view.SetDamageEnabled(true);
                _stateCompletion?.TrySetResult();
                EnterIdle();
            }
            else if (state == SlimeState.Death && !_deathSignalSent)
            {
                _deathSignalSent = true;
                _stateCompletion?.TrySetResult();
                GlobalEventBus.Fire(new EnemyDiedSignal(_enemyId));
            }
            else if (state == SlimeState.Despawn && !_despawnSignalSent)
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
            if (_pathCancellation != null)
            {
                _pathCancellation.Cancel();
                _pathCancellation.Dispose();
                _pathCancellation = null;
            }
            _model.ClearPath();
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
