using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Controller;
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
using Zenject;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.Controller
{
    public sealed class SlimeStateMachine : IDisposable
    {
        private readonly SlimeModel _model;
        private readonly SlimeView _view;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly IEnemyTargetProvider _target;
        private readonly IEnemyAttackService _attack;
        private readonly IEnemyPlacementValidator _placement;
        private readonly LazyInject<EnemyManager> _enemyManager;
        private readonly PauseGate _pauseGate = new();

        private SlimeConfigScriptable _config;
        private Guid _enemyId;
        private CancellationToken _lifetimeToken;
        private CancellationTokenSource _stateCancellation;
        private UniTaskCompletionSource _stateCompletion;
        private float _stateElapsed;
        private int _animationGeneration;
        private bool _attackApplied;
        private bool _fallObservedAirborne;
        private bool _deathSignalSent;
        private bool _despawnSignalSent;
        private bool _aggroRequested;
        private GridPosition? _teleportDestination;
        private GridPosition _lastSafeGridPosition;
        private Vector2 _lastSafeWorldPosition;
        private bool _disposed;

        public SlimeStateMachine(
            SlimeModel model,
            SlimeView view,
            IEnemyPathfindingService pathfinding,
            IEnemyTargetProvider target,
            IEnemyAttackService attack,
            IEnemyPlacementValidator placement,
            LazyInject<EnemyManager> enemyManager)
        {
            _model = model;
            _view = view;
            _pathfinding = pathfinding;
            _target = target;
            _attack = attack;
            _placement = placement;
            _enemyManager = enemyManager;
        }

        public void Initialize(
            SlimeConfigScriptable config,
            Guid enemyId,
            CancellationToken lifetimeToken)
        {
            CancelState();
            _config = config;
            _enemyId = enemyId;
            _lifetimeToken = lifetimeToken;
            _stateElapsed = 0f;
            _attackApplied = false;
            _deathSignalSent = false;
            _despawnSignalSent = false;
            _aggroRequested = false;
            _teleportDestination = null;
            _lastSafeGridPosition = _model.CurrentGridPosition;
            _lastSafeWorldPosition = _view.Body.position;
            _pauseGate.Resume();
        }

        public UniTask SpawnAsync(CancellationToken cancellationToken) =>
            EnterLifecycleStateAsync(SlimeState.Spawn, cancellationToken);

        public UniTask DespawnAsync(CancellationToken cancellationToken) =>
            EnterLifecycleStateAsync(SlimeState.Despawn, cancellationToken);

        public void OnFixedTick(EnemyTickContext context)
        {
            if (_disposed || _pauseGate.IsPaused || _model.IsDead &&
                _model.CurrentState != SlimeState.Death)
                return;
            _stateElapsed += context.FixedDeltaTime;
            _model.TickCooldown(context.FixedDeltaTime);
            switch (_model.CurrentState)
            {
                case SlimeState.Idle:
                    TickIdle();
                    break;
                case SlimeState.Aggro:
                    TickAggro();
                    break;
                case SlimeState.Move:
                    TickMove();
                    break;
                case SlimeState.Fall:
                    TickFall();
                    break;
            }
        }

        public void EnterHurt()
        {
            _model.ClearPath();
            ChangeState(SlimeState.Hurt);
        }

        public void EnterDeath()
        {
            _model.ClearPath();
            _model.SetAggro(false);
            _view.SetDamageEnabled(false);
            _view.SetVelocity(Vector2.zero);
            ChangeState(SlimeState.Death);
        }

        public void HandleAnimationMarker(EnemyAnimationMarkerEvent animationEvent)
        {
            if (_pauseGate.IsPaused ||
                animationEvent.Generation != _animationGeneration)
                return;
            if (_model.CurrentState == SlimeState.Attack &&
                animationEvent.AnimationId == SlimeAnimationId.Attack &&
                animationEvent.Marker == (int)EnemyAnimationMarker.AttackImpact &&
                !_attackApplied)
            {
                _attackApplied = true;
                if (IsTargetInRange(_config.AttackRangeInTiles))
                    _attack.TryAttack(_config.Damage, _config.StatusEffect);
            }
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
                    ChangeState(SlimeState.Idle);
                    break;
                case SlimeState.Attack:
                    _model.ResetAttackCooldown();
                    ResumeAfterAction();
                    break;
                case SlimeState.Hurt:
                    ResumeAfterAction();
                    break;
                case SlimeState.Aggro:
                    _model.SetAggro(true);
                    BeginChaseMovement();
                    break;
                case SlimeState.TeleportDespawn:
                    CompleteTeleportDespawn();
                    break;
                case SlimeState.TeleportSpawn:
                    _teleportDestination = null;
                    _view.SetDamageEnabled(true);
                    ChangeState(SlimeState.Idle);
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

        public void Pause()
        {
            _pauseGate.Pause();
        }

        public void Resume()
        {
            _pauseGate.Resume();
        }

        public void Release()
        {
            CancelState();
            _pauseGate.Resume();
            _stateCompletion?.TrySetCanceled();
            _stateCompletion = null;
            _aggroRequested = false;
            _teleportDestination = null;
            _lastSafeGridPosition = default;
            _lastSafeWorldPosition = default;
            _config = null;
        }

        private async UniTask EnterLifecycleStateAsync(
            SlimeState state,
            CancellationToken cancellationToken)
        {
            _stateCompletion = new UniTaskCompletionSource();
            ChangeState(state);
            using var registration = cancellationToken.Register(
                () => _stateCompletion.TrySetCanceled(cancellationToken));
            await _stateCompletion.Task;
            _stateCompletion = null;
            if (state == SlimeState.Death)
                return;
            if (state == SlimeState.Despawn && !_despawnSignalSent)
            {
                _despawnSignalSent = true;
            }
        }

        private void ChangeState(SlimeState state)
        {
            CancelState();
            _stateCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            _stateElapsed = 0f;
            _attackApplied = false;
            _fallObservedAirborne = false;
            _model.SetState(state);
            _view.SetVelocity(Vector2.zero);
            if (state == SlimeState.TeleportDespawn ||
                state == SlimeState.TeleportSpawn)
            {
                _view.SetDamageEnabled(false);
            }
            else if (state != SlimeState.Spawn &&
                     state != SlimeState.Despawn &&
                     state != SlimeState.Death)
            {
                _teleportDestination = null;
            }
            if (state == SlimeState.Attack && _target.IsTargetAvailable)
            {
                _view.SetFacing(
                    _target.WorldPosition.x < _view.Body.position.x);
            }
            var animationId = GetAnimationId(state);
            if (!_config.AnimationProfile.TryGet(animationId, out var animation))
            {
                Debug.LogError(
                    $"Slime animation '{animationId}' is missing from {_config.AnimationProfile.name}.");
                HandleMissingAnimation(state);
                return;
            }
            _animationGeneration = _view.Play(animation, true);
            if (state == SlimeState.Idle)
                BeginIdleDecision();
        }

        private void BeginIdleDecision()
        {
            _model.ClearPath();
            _aggroRequested = false;
            _teleportDestination = null;
            if (!_target.IsTargetAvailable)
                return;

            var distance = Distance(
                _model.CurrentGridPosition,
                _target.GridPosition);
            if (_model.IsAggro)
            {
                if (distance >= _config.ChaseExitRangeInTiles)
                    _model.SetAggro(false);
                else
                    RequestPath(_target.GridPosition, true);
                return;
            }

            if (distance <= _config.AggroRangeInTiles)
            {
                if (RequestPath(_target.GridPosition, true))
                    return;
                if (TryChooseDestination(false, out var patrolDestination))
                    RequestPath(patrolDestination, false);
                return;
            }

            if (distance >= _config.TeleportTriggerDistanceInTiles &&
                UnityEngine.Random.value < _config.TeleportChance &&
                TryChooseTeleportDestination(out var teleportDestination))
            {
                _teleportDestination = teleportDestination;
                return;
            }

            if (TryChooseDestination(false, out var destination))
                RequestPath(destination, false);
        }

        private bool RequestPath(GridPosition destination, bool chasing)
        {
            if (!_pathfinding.IsWalkable(destination) ||
                !TryGetPlacement(destination, out _))
            {
                if (chasing)
                    HandleChasePathFailed();
                return false;
            }
            _view.SetVelocity(Vector2.zero);
            var generation = _model.BeginPathRequest(destination, chasing);
            var request = new EnemyPathRequest(
                _model.CurrentGridPosition,
                destination,
                _config.MaxFallDistanceInTiles,
                generation,
                chasing,
                _enemyManager.Value.GetOccupiedPositions(_enemyId));
            CalculatePathAsync(request, _stateCancellation.Token).Forget(
                exception => Debug.LogException(exception));
            return true;
        }

        private async UniTask CalculatePathAsync(
            EnemyPathRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _pathfinding.FindPathAsync(
                    request,
                    cancellationToken);
                await _pauseGate.WaitAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (!_model.CompletePath(result))
                    return;
                if (!result.Succeeded && request.Chasing)
                {
                    HandleChasePathFailed();
                    return;
                }
                if (result.Succeeded &&
                    !IsPathPlacementClear(result.Steps))
                {
                    if (request.Chasing)
                        HandleChasePathFailed();
                    else
                        _model.ClearPath();
                    return;
                }
                if (result.Succeeded &&
                    request.Chasing &&
                    !_model.IsAggro &&
                    _model.CurrentState == SlimeState.Idle)
                {
                    if (_target.IsTargetAvailable &&
                        _target.GridPosition == request.Destination &&
                        IsTargetInRange(_config.AggroRangeInTiles))
                    {
                        _aggroRequested = true;
                    }
                    else
                    {
                        _model.ClearPath();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void TickIdle()
        {
            if (_stateElapsed < _config.IdleDuration)
                return;
            if (_aggroRequested)
            {
                ChangeState(SlimeState.Aggro);
                return;
            }
            if (_model.IsAggro && ShouldEndChase())
            {
                _model.SetAggro(false);
                ChangeState(SlimeState.Idle);
                return;
            }
            if (_model.IsAggro &&
                IsTargetInRange(_config.AttackRangeInTiles) &&
                _model.AttackCooldownRemaining <= 0f)
            {
                ChangeState(SlimeState.Attack);
                return;
            }
            if (_model.PathPending)
                return;
            if (_model.CurrentPathStep.HasValue)
            {
                if (!TryGetPlacement(
                        _model.CurrentPathStep.Value.Position,
                        out _))
                {
                    HandlePathStepBlocked();
                    return;
                }
                ChangeState(_model.CurrentPathStep.Value.Type ==
                            EnemyPathStepType.Fall
                    ? SlimeState.Fall
                    : SlimeState.Move);
            }
            else if (_teleportDestination.HasValue)
                ChangeState(SlimeState.TeleportDespawn);
            else
                ChangeState(SlimeState.Idle);
        }

        private void TickAggro()
        {
            _view.SetVelocity(Vector2.zero);
        }

        private void TickMove()
        {
            if (_model.IsAggro)
            {
                if (ShouldEndChase())
                {
                    EndChase();
                    return;
                }
                if (IsTargetInRange(_config.AttackRangeInTiles))
                {
                    _view.SetVelocity(Vector2.zero);
                    if (_model.AttackCooldownRemaining <= 0f)
                        ChangeState(SlimeState.Attack);
                    return;
                }
                if (!_model.PathPending &&
                    _target.GridPosition != _model.Destination)
                {
                    RequestPath(_target.GridPosition, true);
                    return;
                }
            }
            if (_model.PathPending)
                return;
            var step = _model.CurrentPathStep;
            if (!step.HasValue)
            {
                if (_model.IsAggro)
                    RequestPath(_target.GridPosition, true);
                else
                    ChangeState(SlimeState.Idle);
                return;
            }
            if (step.Value.Type == EnemyPathStepType.Fall)
            {
                ChangeState(SlimeState.Fall);
                return;
            }
            if (!TryGetPlacement(step.Value.Position, out var targetWorld))
            {
                HandlePathStepBlocked();
                return;
            }
            var offset = targetWorld.x - _view.Body.position.x;
            if (Mathf.Abs(offset) > _config.PositionTolerance)
            {
                _view.SetFacing(offset < 0f);
                var velocity = _view.Body.linearVelocity;
                velocity.x = Mathf.Sign(offset) * _config.MoveSpeed;
                _view.SetVelocity(velocity);
                return;
            }
            _view.SetVelocity(new Vector2(0f, _view.Body.linearVelocity.y));
            if (!_placement.IsCurrentPlacementClear(_view.TerrainCollider))
            {
                RestoreLastSafePlacement();
                HandlePathStepBlocked();
                return;
            }
            _model.CompletePathStep(step.Value.Position);
            RecordSafePlacement(step.Value.Position, targetWorld);
            if (_model.IsAggro &&
                _target.GridPosition != _model.Destination)
                RequestPath(_target.GridPosition, true);
            else if (!_model.CurrentPathStep.HasValue && _model.IsAggro)
                RequestPath(_target.GridPosition, true);
            else if (!_model.CurrentPathStep.HasValue)
                ChangeState(SlimeState.Idle);
            else if (_model.CurrentPathStep.Value.Type == EnemyPathStepType.Fall)
                ChangeState(SlimeState.Fall);
        }

        private void TickFall()
        {
            if (_model.IsAggro && ShouldEndChase())
            {
                EndChase();
                return;
            }
            var step = _model.CurrentPathStep;
            if (!step.HasValue)
            {
                ChangeState(SlimeState.Idle);
                return;
            }
            if (!TryGetPlacement(step.Value.Position, out var targetWorld))
            {
                HandlePathStepBlocked();
                return;
            }
            var offset = targetWorld.x - _view.Body.position.x;
            var velocity = _view.Body.linearVelocity;
            velocity.x = Mathf.Abs(offset) <= _config.PositionTolerance
                ? 0f
                : Mathf.Sign(offset) * _config.MoveSpeed;
            _view.SetFacing(velocity.x < 0f);
            _view.SetVelocity(velocity);
            var grounded = _view.IsGrounded(
                _config.GroundLayerMask,
                _config.GroundProbeDistance);
            if (!grounded)
                _fallObservedAirborne = true;
            if (!_fallObservedAirborne || !grounded)
                return;
            _view.SetVelocity(Vector2.zero);
            if (!_placement.IsCurrentPlacementClear(_view.TerrainCollider))
            {
                RestoreLastSafePlacement();
                HandlePathStepBlocked();
                return;
            }
            _model.CompletePathStep(step.Value.Position);
            RecordSafePlacement(step.Value.Position, targetWorld);
            if (_model.IsAggro)
                BeginChaseMovement();
            else
                ChangeState(SlimeState.Idle);
        }

        private void BeginChaseMovement()
        {
            if (ShouldEndChase())
            {
                EndChase();
                return;
            }
            ChangeState(SlimeState.Move);
            if (IsTargetInRange(_config.AttackRangeInTiles) &&
                _model.AttackCooldownRemaining <= 0f)
            {
                ChangeState(SlimeState.Attack);
                return;
            }
            RequestPath(_target.GridPosition, true);
        }

        private void ResumeAfterAction()
        {
            if (_model.IsAggro)
                BeginChaseMovement();
            else
                ChangeState(SlimeState.Idle);
        }

        private void EndChase()
        {
            _model.SetAggro(false);
            _model.ClearPath();
            ChangeState(SlimeState.Idle);
        }

        private void HandleChasePathFailed()
        {
            _aggroRequested = false;
            if (_model.IsAggro ||
                _model.CurrentState == SlimeState.Move ||
                _model.CurrentState == SlimeState.Fall ||
                _model.CurrentState == SlimeState.Aggro)
            {
                EndChase();
                return;
            }
            _model.SetAggro(false);
            _model.ClearPath();
        }

        private void HandlePathStepBlocked()
        {
            _aggroRequested = false;
            if (_model.IsAggro)
            {
                EndChase();
                return;
            }
            _model.ClearPath();
            ChangeState(SlimeState.Idle);
        }

        private void RecordSafePlacement(
            GridPosition gridPosition,
            Vector2 worldPosition)
        {
            _lastSafeGridPosition = gridPosition;
            _lastSafeWorldPosition = worldPosition;
        }

        private void RestoreLastSafePlacement()
        {
            _view.Teleport(_lastSafeWorldPosition);
            _model.SetGridPosition(_lastSafeGridPosition);
        }

        private bool ShouldEndChase() =>
            !_target.IsTargetAvailable ||
            Distance(_model.CurrentGridPosition, _target.GridPosition) >=
            _config.ChaseExitRangeInTiles;

        private bool TryChooseDestination(
            bool chasing,
            out GridPosition destination)
        {
            if (chasing && _target.IsTargetAvailable &&
                _pathfinding.IsWalkable(_target.GridPosition) &&
                TryGetPlacement(_target.GridPosition, out _))
            {
                destination = _target.GridPosition;
                return destination != _model.CurrentGridPosition;
            }
            for (var attempt = 0; attempt < _config.DestinationRetries; attempt++)
            {
                var offset = UnityEngine.Random.Range(
                    -_config.PatrolRangeInTiles,
                    _config.PatrolRangeInTiles + 1);
                if (offset == 0)
                    continue;
                destination = new GridPosition(
                    _model.CurrentGridPosition.X + offset,
                    _model.CurrentGridPosition.Y);
                if (_pathfinding.IsWalkable(destination) &&
                    TryGetPlacement(destination, out _) &&
                    !_enemyManager.Value.IsPositionOccupied(destination, _enemyId))
                    return true;
            }
            destination = default;
            return false;
        }

        private bool TryChooseTeleportDestination(out GridPosition destination)
        {
            var target = _target.GridPosition;
            var currentDistance = Distance(_model.CurrentGridPosition, target);
            for (var attempt = 0; attempt < _config.MaxTeleportAttempts; attempt++)
            {
                var offset = UnityEngine.Random.Range(
                    -_config.MaximumTeleportDistanceInTiles,
                    _config.MaximumTeleportDistanceInTiles + 1);
                var candidate = new GridPosition(target.X + offset, target.Y);
                if (!_pathfinding.IsWalkable(candidate) ||
                    !TryGetPlacement(candidate, out _) ||
                    _enemyManager.Value.IsPositionOccupied(candidate, _enemyId) ||
                    Distance(candidate, target) <
                    _config.MinimumTeleportDistanceInTiles ||
                    Distance(candidate, target) >
                    _config.MaximumTeleportDistanceInTiles ||
                    Distance(candidate, target) >= currentDistance)
                    continue;
                destination = candidate;
                return true;
            }
            destination = default;
            return false;
        }

        private void CompleteTeleportDespawn()
        {
            if (_teleportDestination.HasValue &&
                IsTeleportDestinationValid(_teleportDestination.Value))
            {
                var destination = _teleportDestination.Value;
                TryGetPlacement(destination, out var worldPosition);
                _view.Teleport(worldPosition);
                _model.SetGridPosition(destination);
                RecordSafePlacement(destination, worldPosition);
            }
            ChangeState(SlimeState.TeleportSpawn);
        }

        private bool IsTeleportDestinationValid(GridPosition destination)
        {
            return _target.IsTargetAvailable &&
                   _pathfinding.IsWalkable(destination) &&
                   TryGetPlacement(destination, out _) &&
                   !_enemyManager.Value.IsPositionOccupied(
                       destination,
                       _enemyId) &&
                   Distance(destination, _target.GridPosition) >=
                   _config.MinimumTeleportDistanceInTiles &&
                   Distance(destination, _target.GridPosition) <=
                   _config.MaximumTeleportDistanceInTiles;
        }

        private bool IsTargetInRange(int range) =>
            _target.IsTargetAvailable &&
            Distance(_model.CurrentGridPosition, _target.GridPosition) <= range;

        private bool TryGetPlacement(
            GridPosition position,
            out Vector2 worldPosition) =>
            _placement.TryGetPlacement(
                _view.TerrainCollider,
                position,
                out worldPosition);

        private bool IsPathPlacementClear(
            IReadOnlyList<EnemyPathStep> steps)
        {
            if (steps == null)
                return false;
            for (var i = 0; i < steps.Count; i++)
            {
                if (!TryGetPlacement(steps[i].Position, out _))
                    return false;
            }
            return true;
        }

        private static int Distance(GridPosition a, GridPosition b) =>
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

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
                ChangeState(SlimeState.Idle);
            }
            else if (state == SlimeState.Death || state == SlimeState.Despawn)
            {
                if (state == SlimeState.Death && !_deathSignalSent)
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
            }
            else if (state == SlimeState.TeleportDespawn ||
                     state == SlimeState.TeleportSpawn)
            {
                _teleportDestination = null;
                _view.SetDamageEnabled(true);
                ChangeState(SlimeState.Idle);
            }
            else if (state != SlimeState.Idle)
            {
                ChangeState(SlimeState.Idle);
            }
        }

        private void CancelState()
        {
            if (_stateCancellation == null)
                return;
            _stateCancellation.Cancel();
            _stateCancellation.Dispose();
            _stateCancellation = null;
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
