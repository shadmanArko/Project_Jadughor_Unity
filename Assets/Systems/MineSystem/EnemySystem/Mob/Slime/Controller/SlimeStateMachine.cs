using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        private const int PatrolDirectionAttempts = 2;

        private readonly SlimeModel _model;
        private readonly SlimeView _view;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly IEnemyTargetProvider _target;
        private readonly IEnemyAttackService _attack;
        private readonly IEnemyPlacementValidator _placement;
        private readonly IEnemyChaseTargetResolver _chaseTargetResolver;
        private readonly PauseGate _pauseGate = new();

        private SlimeConfigScriptable _config;
        private Guid _enemyId;
        private CancellationToken _lifetimeToken;
        private CancellationTokenSource _stateCancellation;
        private UniTaskCompletionSource _stateCompletion;
        private float _stateElapsed;
        private int _animationGeneration;
        private int _teleportSearchOffset;
        private bool _attackApplied;
        private bool _fallObservedAirborne;
        private bool _deathSignalSent;
        private bool _despawnSignalSent;
        private GridPosition? _teleportDestination;
        private GridPosition _lastChaseTargetPosition;
        private bool _hasLastChaseTargetPosition;
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
            IEnemyChaseTargetResolver chaseTargetResolver)
        {
            _model = model;
            _view = view;
            _pathfinding = pathfinding;
            _target = target;
            _attack = attack;
            _placement = placement;
            _chaseTargetResolver = chaseTargetResolver;
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
            _teleportSearchOffset = Math.Abs(enemyId.GetHashCode());
            _attackApplied = false;
            _deathSignalSent = false;
            _despawnSignalSent = false;
            _teleportDestination = null;
            _lastChaseTargetPosition = default;
            _hasLastChaseTargetPosition = false;
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
            if ((_model.CurrentState == SlimeState.Move ||
                 _model.CurrentState == SlimeState.Fall) &&
                _model.TickMovementTimeout(context.FixedDeltaTime))
            {
                HandleMovementTimeout();
                return;
            }

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

        public void HandleHorizontalCollision(Collider2D collider)
        {
            if (_pauseGate.IsPaused ||
                _model.CurrentState != SlimeState.Move ||
                _model.IsAggro ||
                !IsTerrainWallCollider(collider))
                return;
            _model.ReversePatrolDirection();
            _view.SetFacing(_model.PatrolDirection < 0);
            StartPatrolBehavior();
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
                if (IsTargetAttackable())
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
                    RestartDecisionCycle();
                    break;
                case SlimeState.Attack:
                    _model.ResetAttackCooldown();
                    _model.SetAggro(false);
                    _model.ClearPath();
                    RestartDecisionCycle();
                    break;
                case SlimeState.Hurt:
                    ResumeAfterAction();
                    break;
                case SlimeState.Aggro:
                    BeginChaseMovement();
                    break;
                case SlimeState.TeleportDespawn:
                    CompleteTeleportDespawn();
                    break;
                case SlimeState.TeleportSpawn:
                    _teleportDestination = null;
                    _view.SetDamageEnabled(true);
                    _model.StartTeleportCooldown(_config.TeleportCooldownSeconds);
                    RestartDecisionCycle();
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
            _teleportDestination = null;
            _lastChaseTargetPosition = default;
            _hasLastChaseTargetPosition = false;
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
                _despawnSignalSent = true;
        }

        private void RestartDecisionCycle()
        {
            if (_config == null || _model.IsDead)
                return;
            if (!RevalidateCurrentPlacement())
            {
                if (!TryStartTeleportBehavior(true, true))
                    ChangeState(SlimeState.Idle);
                return;
            }

            if (!_target.IsTargetAvailable)
            {
                StartPatrolBehavior();
                return;
            }

            var distance = Distance(
                _model.CurrentGridPosition,
                _target.GridPosition);
            if (distance <= _config.AggroRangeInTiles)
            {
                StartChaseBehavior();
                return;
            }

            if (distance >= _config.TeleportTriggerDistanceInTiles &&
                UnityEngine.Random.value < _config.TeleportChance)
            {
                if (TryStartTeleportBehavior(true, false))
                    return;
            }

            StartPatrolBehavior();
        }

        private void StartPatrolBehavior()
        {
            if (!PrepareBehavior(SlimeState.Idle))
            {
                if (!TryStartTeleportBehavior(true, true))
                    ChangeState(SlimeState.Idle);
                return;
            }
            _model.SetAggro(false);
            _hasLastChaseTargetPosition = false;
            if (TryRequestPatrolStep())
                return;
            _model.ReversePatrolDirection();
            _view.SetFacing(_model.PatrolDirection < 0);
        }

        private void StartChaseBehavior()
        {
            if (!_target.IsTargetAvailable ||
                !PrepareBehavior(SlimeState.Aggro))
            {
                if (!TryStartTeleportBehavior(true, true))
                    ChangeState(SlimeState.Idle);
                return;
            }
            _model.SetAggro(true);
            RequestChasePath();
        }

        private bool TryStartTeleportBehavior(
            bool allowAnyWalkableFallback,
            bool bypassCooldown)
        {
            if (!bypassCooldown && !_model.CanTeleport)
                return false;
            _model.ClearPath();
            _hasLastChaseTargetPosition = false;
            _view.SetVelocity(Vector2.zero);
            if (!TryChooseTeleportDestination(
                    allowAnyWalkableFallback,
                    out var destination))
            {
                return false;
            }
            _teleportDestination = destination;
            ChangeState(SlimeState.TeleportDespawn);
            return true;
        }

        private bool PrepareBehavior(SlimeState state)
        {
            _model.ClearPath();
            _teleportDestination = null;
            _view.SetVelocity(Vector2.zero);
            ChangeState(state);
            return RevalidateCurrentPlacement();
        }

        private void ChangeState(SlimeState state, bool cancelPendingWork = true)
        {
            if (cancelPendingWork || _stateCancellation == null)
            {
                CancelState();
                _stateCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    _lifetimeToken);
            }
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
            if (state == SlimeState.Attack && _target.IsTargetAvailable)
                _view.SetFacing(_target.WorldPosition.x < _view.Body.position.x);

            var animationId = GetAnimationId(state);
            if (!_config.AnimationProfile.TryGet(animationId, out var animation))
            {
                Debug.LogError(
                    $"Slime animation '{animationId}' is missing from {_config.AnimationProfile.name}.");
                HandleMissingAnimation(state);
                return;
            }
            _animationGeneration = _view.Play(animation, true);
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
                chasing);
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
                if (!result.Succeeded)
                {
                    if (request.Chasing)
                        HandleChasePathFailed();
                    else
                        StartPatrolBehavior();
                    return;
                }
                if (!IsPathPlacementClear(result.Steps))
                {
                    if (request.Chasing)
                        HandleChasePathFailed();
                    else
                        StartPatrolBehavior();
                    return;
                }
                StartMovementTimeout(result.Steps);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void TickIdle()
        {
            if (_model.PathPending)
                return;
            if (_teleportDestination.HasValue)
            {
                ChangeState(SlimeState.TeleportDespawn);
                return;
            }
            var step = _model.CurrentPathStep;
            if (step.HasValue)
            {
                ChangeState(step.Value.Type == EnemyPathStepType.Fall
                    ? SlimeState.Fall
                    : SlimeState.Move);
                return;
            }
            if (_stateElapsed < _config.IdleDuration)
                return;
            RestartDecisionCycle();
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
                if (IsTargetAttackable())
                {
                    _view.SetVelocity(Vector2.zero);
                    if (_model.AttackCooldownRemaining <= 0f)
                        ChangeState(SlimeState.Attack);
                    return;
                }
                if (!_model.PathPending &&
                    HasChaseTargetChanged())
                {
                    RequestChasePath();
                    return;
                }
            }
            if (_model.PathPending)
                return;
            var step = _model.CurrentPathStep;
            if (!step.HasValue)
            {
                if (_model.IsAggro)
                    RequestChasePath();
                else
                    StartPatrolBehavior();
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
                HasChaseTargetChanged())
                RequestChasePath();
            else if (!_model.CurrentPathStep.HasValue && _model.IsAggro)
                RequestChasePath();
            else if (!_model.CurrentPathStep.HasValue)
                StartPatrolBehavior();
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
                RestartDecisionCycle();
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
                StartPatrolBehavior();
        }

        private void BeginChaseMovement()
        {
            if (ShouldEndChase())
            {
                EndChase();
                return;
            }
            if (IsTargetAttackable() &&
                _model.AttackCooldownRemaining <= 0f)
            {
                ChangeState(SlimeState.Attack);
                return;
            }
            if (!_model.PathPending && !_model.CurrentPathStep.HasValue)
            {
                if (!RequestChasePath())
                    return;
            }
            ChangeState(SlimeState.Move, false);
        }

        private void ResumeAfterAction()
        {
            if (_model.IsAggro)
                BeginChaseMovement();
            else
                StartPatrolBehavior();
        }

        private void EndChase()
        {
            _model.SetAggro(false);
            _model.ClearPath();
            _hasLastChaseTargetPosition = false;
            StartPatrolBehavior();
        }

        private void HandleChasePathFailed()
        {
            if (!_model.IsAggro &&
                _model.CurrentState != SlimeState.Move &&
                _model.CurrentState != SlimeState.Fall &&
                _model.CurrentState != SlimeState.Aggro)
            {
                _model.ClearPath();
                return;
            }
            EndChase();
        }

        private void HandlePathStepBlocked()
        {
            if (_model.IsAggro)
            {
                EndChase();
                return;
            }
            _model.ReversePatrolDirection();
            StartPatrolBehavior();
        }

        private void HandleMovementTimeout()
        {
            _view.SetVelocity(Vector2.zero);
            _model.ClearPath();
            _hasLastChaseTargetPosition = false;
            if (_model.IsAggro)
            {
                EndChase();
                return;
            }
            if (!RevalidateCurrentPlacement())
            {
                if (!TryStartTeleportBehavior(true, true))
                    ChangeState(SlimeState.Idle);
                return;
            }
            StartPatrolBehavior();
        }

        private bool TryRequestPatrolStep()
        {
            for (var i = 0; i < PatrolDirectionAttempts; i++)
            {
                var destination = new GridPosition(
                    _model.CurrentGridPosition.X + _model.PatrolDirection,
                    _model.CurrentGridPosition.Y);
                if (IsPatrolStepValid(destination) &&
                    RequestPath(destination, false))
                {
                    _view.SetFacing(_model.PatrolDirection < 0);
                    return true;
                }
                _model.ReversePatrolDirection();
                _view.SetFacing(_model.PatrolDirection < 0);
            }
            return false;
        }

        private bool IsPatrolStepValid(GridPosition destination) =>
            _pathfinding.IsWalkable(destination) &&
            TryGetPlacement(destination, out _);

        private bool RevalidateCurrentPlacement()
        {
            var position = _placement.WorldToGrid(_view.Body.position);
            _model.SetGridPosition(position);
            if (!_pathfinding.IsWalkable(position) ||
                !_placement.IsCurrentPlacementClear(_view.TerrainCollider))
                return false;
            RecordSafePlacement(position, _view.Body.position);
            return true;
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

        private bool RequestChasePath()
        {
            if (!_target.IsTargetAvailable ||
                !_chaseTargetResolver.TryResolve(
                    _view.TerrainCollider,
                    _model.CurrentGridPosition,
                    _target.GridPosition,
                    EffectiveAttackDecisionRangeInTiles,
                    out var destination))
            {
                HandleChasePathFailed();
                return false;
            }

            _lastChaseTargetPosition = _target.GridPosition;
            _hasLastChaseTargetPosition = true;
            return RequestPath(destination, true);
        }

        private bool HasChaseTargetChanged() =>
            !_hasLastChaseTargetPosition ||
            _target.GridPosition != _lastChaseTargetPosition;

        private bool TryChooseTeleportDestination(
            bool allowAnyWalkableFallback,
            out GridPosition destination)
        {
            if (_target.IsTargetAvailable &&
                TryChooseNearbyTeleportDestination(out destination))
                return true;
            if (allowAnyWalkableFallback)
                return TryChooseAnyWalkableTeleportDestination(out destination);
            destination = default;
            return false;
        }

        private bool TryChooseNearbyTeleportDestination(
            out GridPosition destination)
        {
            destination = default;
            var attempts = Math.Max(1, _config.MaxTeleportAttempts);
            for (var i = 0; i < attempts; i++)
            {
                if (!_pathfinding.TryFindWalkableNear(
                        _target.GridPosition,
                        _config.MinimumTeleportDistanceInTiles,
                        _config.MaximumTeleportDistanceInTiles,
                        _teleportSearchOffset++,
                        out var candidate))
                    return false;
                if (!IsTeleportCandidateValid(candidate))
                    continue;
                destination = candidate;
                return true;
            }
            return false;
        }

        private bool TryChooseAnyWalkableTeleportDestination(
            out GridPosition destination)
        {
            destination = default;
            var count = _pathfinding.WalkableCount;
            if (count <= 0)
                return false;

            var fallback = default(GridPosition);
            var hasFallback = false;
            for (var i = 0; i < count; i++)
            {
                if (!_pathfinding.TryFindAnyWalkable(
                        _teleportSearchOffset + i,
                        out var candidate) ||
                    !IsTeleportCandidateValid(candidate))
                    continue;
                if (candidate == _model.CurrentGridPosition)
                {
                    fallback = candidate;
                    hasFallback = true;
                    continue;
                }
                destination = candidate;
                _teleportSearchOffset += i + 1;
                return true;
            }
            if (!hasFallback)
                return false;
            destination = fallback;
            _teleportSearchOffset++;
            return true;
        }

        private bool IsTeleportCandidateValid(GridPosition destination) =>
            _pathfinding.IsWalkable(destination) &&
            TryGetPlacement(destination, out _);

        private bool IsTerrainWallCollider(Collider2D collider) =>
            collider != null &&
            !collider.isTrigger &&
            (_config.GroundLayerMask.value & (1 << collider.gameObject.layer)) != 0;

        private void CompleteTeleportDespawn()
        {
            if (_teleportDestination.HasValue &&
                IsTeleportCandidateValid(_teleportDestination.Value))
            {
                var destination = _teleportDestination.Value;
                TryGetPlacement(destination, out var worldPosition);
                _view.Teleport(worldPosition);
                _model.SetGridPosition(destination);
                RecordSafePlacement(destination, worldPosition);
            }
            ChangeState(SlimeState.TeleportSpawn);
        }

        private bool IsTargetInRange(int range) =>
            _target.IsTargetAvailable &&
            Distance(_model.CurrentGridPosition, _target.GridPosition) <= range;

        private bool IsTargetAttackable() =>
            IsTargetInRange(EffectiveAttackDecisionRangeInTiles) &&
            Vector2.Distance(_view.Body.position, _target.WorldPosition) <=
            _config.AttackContactDistance;

        private int EffectiveAttackDecisionRangeInTiles =>
            Math.Max(1, _config.AttackRangeInTiles);

        private bool TryGetPlacement(
            GridPosition position,
            out Vector2 worldPosition) =>
            _placement.TryGetPlacement(
                _view.TerrainCollider,
                position,
                out worldPosition);

        private bool IsPathPlacementClear(IReadOnlyList<EnemyPathStep> steps)
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

        private void StartMovementTimeout(IReadOnlyList<EnemyPathStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                _model.ClearMovementTimeout();
                return;
            }

            var distance = 0f;
            var previous = _view.Body.position;
            for (var i = 0; i < steps.Count; i++)
            {
                if (!TryGetPlacement(steps[i].Position, out var next))
                    continue;
                distance += Vector2.Distance(previous, next);
                previous = next;
            }
            var speed = Mathf.Max(0.001f, _config.MoveSpeed);
            var timeout = distance / speed +
                          _config.MovementStuckBufferSeconds;
            timeout = Mathf.Max(timeout, _config.MinimumMovementTimeoutSeconds);
            _model.StartMovementTimeout(timeout);
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
                RestartDecisionCycle();
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
            else if (state == SlimeState.TeleportDespawn)
            {
                CompleteTeleportDespawn();
            }
            else if (state == SlimeState.TeleportSpawn)
            {
                _teleportDestination = null;
                _view.SetDamageEnabled(true);
                _model.StartTeleportCooldown(_config.TeleportCooldownSeconds);
                RestartDecisionCycle();
            }
            else if (state == SlimeState.Aggro)
            {
                BeginChaseMovement();
            }
            else if (state != SlimeState.Idle)
            {
                RestartDecisionCycle();
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
