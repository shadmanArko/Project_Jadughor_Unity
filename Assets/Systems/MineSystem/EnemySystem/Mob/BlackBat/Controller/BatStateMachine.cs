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
        private const int RouteVariantCount = 8;
        private const int ContactDirectionAttempts = 8;
        private const float ContactRadiusSafetyFactor = 0.9f;
        private const float GoldenAngleRadians = 2.39996323f;

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
        private Vector2 _movementObservationPosition;
        private float _combatDiagnosticLogRemaining;
        private float _movementStallElapsed;
        private int _formationSlot = -1;
        private int _consecutiveRouteFailures;
        private int _pathNavigationRevision;
        private int _animationGeneration;
        private bool _movementObservationActive;
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
            int formationSlot,
            CancellationToken lifetimeToken)
        {
            CancelPathRequest();
            _config = config;
            _enemyId = enemyId;
            _formationSlot = formationSlot;
            _lifetimeToken = lifetimeToken;
            _hasObservedTarget = _target.IsTargetAvailable;
            _observedTargetGrid = _hasObservedTarget
                ? _target.GridPosition
                : default;
            _pathTargetGrid = default;
            _perchWorldPosition = default;
            _movementObservationPosition = _view.Body.position;
            _combatDiagnosticLogRemaining = 0f;
            _movementStallElapsed = 0f;
            _consecutiveRouteFailures = 0;
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            _movementObservationActive = false;
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
            TraceAi(
                "Spawn",
                $"grid={_model.CurrentGridPosition} " +
                $"world={_view.Body.position.ToString("F4")}");
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
            if (_model.CurrentState == BatState.Attack)
            {
                FaceTargetHorizontally();
                return;
            }
            if (_model.CurrentState == BatState.Hurt)
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

            FaceTargetHorizontally();
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
            CancelPathRequest(true);
            _view.Stop();
            _pauseGate.Pause();
        }

        public void Resume() => _pauseGate.Resume();

        public void Release()
        {
            TraceAi(
                "PoolRelease",
                $"state={_model.CurrentState} " +
                $"world={_view.Body.position.ToString("F4")}");
            CancelPathRequest();
            _pauseGate.Resume();
            _isApproachingPerch = false;
            _idleToFlyPlaying = false;
            _combatDiagnosticLogRemaining = 0f;
            _movementStallElapsed = 0f;
            _consecutiveRouteFailures = 0;
            _movementObservationActive = false;
            _formationSlot = -1;
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
            if (!_model.EngagementActive)
                _model.TickIdleCooldown(deltaTime);
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

            var contactPosition = GetContactApproachPosition();
            var remainingDistance = Vector2.Distance(
                _view.Body.position,
                contactPosition);
            _model.BeginContactApproach();
            _model.StartMovementTimeout(
                GetWorldMovementTimeout(
                    remainingDistance,
                    _config.ChaseSpeed));
            BeginMovementObservation();
            TraceAi(
                "ContactApproach",
                $"target={contactPosition.ToString("F4")} " +
                $"distance={remainingDistance:F4}");
        }

        private void TickContactApproach(float deltaTime)
        {
            if (_model.TickMovementTimeout(deltaTime))
            {
                ReportMovementError("Contact approach movement timeout.");
                HandleContactApproachFailure();
                return;
            }
            if (TickMovementStall(deltaTime))
            {
                ReportMovementError("Contact approach made no progress.");
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
                ClearMovementObservation();
                _view.Stop();
                if (_model.AttackCooldownRemaining <= 0f)
                    EnterAttack();
                return;
            }

            var bodyPosition = _view.Body.position;
            var targetPosition = GetContactApproachPosition();
            var delta = targetPosition - bodyPosition;
            var distance = delta.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                _view.Stop();
                return;
            }

            FaceHorizontally(delta.x);
            var movementDistance = Mathf.Min(
                _config.ChaseSpeed * Mathf.Max(0f, deltaTime),
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
            ClearMovementObservation();
            _model.EndContactApproach();
            _view.Stop();
            if (_target.IsTargetAvailable && IsWithinChaseExitRange())
            {
                TraceAi(
                    "Recovery",
                    "source=ContactApproach action=RetryChase");
                _model.BeginEngagement();
                if (_model.CurrentState != BatState.Chase)
                    ChangeState(BatState.Chase);
                _model.StartDecisionDelay(_config.DecisionRetryDelay);
                return;
            }
            TraceAi(
                "Recovery",
                "source=ContactApproach action=Explore");
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
            var destinations = new List<GridPosition>(
                _config.DestinationRetries);
            var candidateCount = Mathf.Max(1, _pathfinding.FlyableCount);
            var baseOffset = UnityEngine.Random.Range(0, candidateCount) +
                             Mathf.Max(0, _formationSlot);
            for (var attempt = 0;
                 attempt < _config.DestinationRetries;
                 attempt++)
            {
                if (!_pathfinding.TryFindFlyableNear(
                        current,
                        1,
                        _config.ExploreRangeInTiles,
                        baseOffset + attempt,
                        out var destination) ||
                    !_placement.TryGetPlacement(
                        _view.TerrainCollider,
                        destination,
                        out _) ||
                    Contains(destinations, destination))
                    continue;
                destinations.Add(destination);
            }

            if (destinations.Count > 0)
            {
                BeginPathRequest(
                    destinations[0],
                    destinations,
                    BatPathPurpose.Explore,
                    false);
                return;
            }
            RecordRouteFailure(
                "Explore",
                "No distinct placement-valid roam destination was found.");
            _model.StartDecisionDelay(_config.DecisionRetryDelay);
        }

        private void ChooseIdleOrRoam()
        {
            if (_model.IdleCooldownRemaining <= 0f &&
                UnityEngine.Random.value <= _config.IdleChance &&
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
            var destinations = new List<GridPosition>(1) { destination };
            BeginPathRequest(
                destination,
                destinations,
                purpose,
                false);
        }

        private void BeginPathRequest(
            GridPosition preferredDestination,
            IReadOnlyList<GridPosition> destinations,
            BatPathPurpose purpose,
            bool prioritizePreferredDestination)
        {
            CancelPathRequest();
            var current = _placement.WorldToGrid(_view.Body.position);
            _model.SetGridPosition(current);
            var generation = _model.BeginPathRequest(
                preferredDestination,
                purpose);
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            _pathCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            var request = new EnemyMultiTargetPathRequest(
                current,
                preferredDestination,
                preferredDestination,
                destinations,
                EnemyMovementType.Flying,
                0,
                generation,
                GetRouteVariant(),
                prioritizePreferredDestination);
            TraceAi(
                "PathRequest",
                $"purpose={purpose} generation={generation} " +
                $"start={current} preferred={preferredDestination} " +
                $"candidates={destinations.Count} " +
                $"navRevision={_pathNavigationRevision}");
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
            TraceAi(
                "PathResult",
                $"purpose={purpose} generation={result.Generation} " +
                $"success={result.Succeeded} destination={result.Destination} " +
                $"steps={result.Steps?.Count ?? 0} error={result.Error ?? "none"}");
            if (!result.Succeeded)
            {
                RecordRouteFailure(purpose.ToString(), result.Error);
                _model.ClearPath();
                _model.StartDecisionDelay(_config.DecisionRetryDelay);
                return;
            }
            ResetRouteFailures();
            if (result.Steps == null || result.Steps.Count == 0)
            {
                FinishMovement();
                return;
            }
            _model.StartMovementTimeout(GetMovementTimeout(
                result.Steps,
                purpose));
            BeginMovementObservation();
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
            var preferredDestination = GetPreferredChaseDestination(
                _pathTargetGrid);
            _pathNavigationRevision = _pathfinding.NavigationRevision;
            var generation = _model.BeginPathRequest(
                preferredDestination,
                BatPathPurpose.Chase);
            _pathCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeToken);
            TraceAi(
                "PathRequest",
                $"purpose=Chase generation={generation} start={current} " +
                $"target={_pathTargetGrid} preferred={preferredDestination} " +
                $"routeVariant={GetRouteVariant()} " +
                $"navRevision={_pathNavigationRevision}");
            FindChasePathAsync(
                    generation,
                    current,
                    _pathTargetGrid,
                    preferredDestination,
                    _pathNavigationRevision,
                    _pathCancellation.Token)
                .Forget(HandlePathException);
        }

        private async UniTask FindChasePathAsync(
            int generation,
            GridPosition routeStart,
            GridPosition targetGrid,
            GridPosition preferredDestination,
            int navigationRevision,
            CancellationToken cancellationToken)
        {
            var result = await _chaseResolver.FindReachablePathAsync(
                _view.TerrainCollider,
                routeStart,
                targetGrid,
                preferredDestination,
                Mathf.Max(1, _config.AttackRangeInTiles),
                EnemyMovementType.Flying,
                0,
                generation,
                GetRouteVariant(),
                true,
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
            TraceAi(
                "PathResult",
                $"purpose=Chase generation={result.Generation} " +
                $"success={result.Succeeded} destination={result.Destination} " +
                $"steps={result.Steps?.Count ?? 0} error={result.Error ?? "none"}");
            if (!result.Succeeded)
            {
                RecordRouteFailure("Chase", result.Error);
                _model.ClearPath();
                _view.Stop();
                _model.StartDecisionDelay(_config.DecisionRetryDelay);
                return;
            }

            ResetRouteFailures();
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
            _model.StartMovementTimeout(GetMovementTimeout(
                result.Steps,
                BatPathPurpose.Chase));
            BeginMovementObservation();
        }

        private void TickMovement(float deltaTime)
        {
            var step = _model.CurrentPathStep;
            if (_model.PathPending && !step.HasValue)
                return;
            if (_model.TickMovementTimeout(deltaTime))
            {
                ReportMovementError("Path movement timeout.");
                HandleMovementFailure();
                return;
            }
            if (TickMovementStall(deltaTime))
            {
                ReportMovementError("Path movement made no progress.");
                HandleMovementFailure();
                return;
            }
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
                RecordRouteFailure(
                    _model.PathPurpose.ToString(),
                    $"Path step {step.Value.Position} is no longer flyable or placement-valid.");
                HandleMovementFailure();
                return;
            }

            if (!_model.SegmentActive)
            {
                var startPosition = _view.Body.position;
                _model.BeginSegment(startPosition, targetPosition);
            }

            var segmentDistance = Vector2.Distance(
                _model.SegmentStart,
                _model.SegmentTarget);
            var movementSpeed = GetMovementSpeed(_model.PathPurpose);
            var progress = _model.AdvanceSegment(
                movementSpeed * Mathf.Max(0f, deltaTime) /
                Mathf.Max(0.0001f, segmentDistance));
            var basePosition = Vector2.Lerp(
                _model.SegmentStart,
                _model.SegmentTarget,
                progress);
            var segmentDelta = _model.SegmentTarget - _model.SegmentStart;
            var wobbleEnvelope = Mathf.Sin(progress * Mathf.PI);
            var wobble = Mathf.Sin(
                progress * Mathf.PI * 2f *
                _config.FlightWobbleCyclesPerCell +
                GetFormationPhase()) * wobbleEnvelope *
                _config.FlightWobbleAmplitude;
            var horizontal = Mathf.Abs(segmentDelta.x) >=
                             Mathf.Abs(segmentDelta.y);
            var visualOffset = horizontal
                ? Vector2.up * wobble
                : Vector2.right * wobble;
            FaceHorizontally(segmentDelta.x);
            _view.SetFlightVisualOffset(visualOffset);
            _view.MovePosition(basePosition);

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
            ClearMovementObservation();
            _model.ClearPath();
            _view.ClearFlightVisualOffset();
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
                    _perchWorldPosition),
                _config.MoveSpeed));
            BeginMovementObservation();
        }

        private void TickPerchApproach(float deltaTime)
        {
            if (_model.TickMovementTimeout(deltaTime))
            {
                ReportMovementError("Perch approach movement timeout.");
                TraceAi("Recovery", "source=Perch action=Explore");
                EnterExplore();
                return;
            }
            if (TickMovementStall(deltaTime))
            {
                ReportMovementError("Perch approach made no progress.");
                TraceAi("Recovery", "source=Perch action=Explore");
                EnterExplore();
                return;
            }
            var bodyPosition = _view.Body.position;
            FaceHorizontally(_perchWorldPosition.x - bodyPosition.x);
            var next = Vector2.MoveTowards(
                bodyPosition,
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
            ClearMovementObservation();
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
                TraceAi(
                    "Recovery",
                    "source=ChasePath action=RetryChase");
                CancelPathRequest();
                _model.BeginEngagement();
                if (_model.CurrentState != BatState.Chase)
                    ChangeState(BatState.Chase);
                _view.Stop();
                _model.StartDecisionDelay(_config.DecisionRetryDelay);
                return;
            }
            TraceAi(
                "Recovery",
                $"source={purpose} action=Explore");
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
            _model.ResetIdleCooldown();
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
            var previousState = _model.CurrentState;
            _attackApplied = false;
            _model.SetState(state);
            if (previousState != state)
            {
                TraceAi(
                    "State",
                    $"from={previousState} to={state} " +
                    $"grid={_placement.WorldToGrid(_view.Body.position)} " +
                    $"world={_view.Body.position.ToString("F4")}");
            }
            _view.ClearFlightVisualOffset();
            _view.Stop();
            if (state == BatState.Attack && _target.IsTargetAvailable)
                FaceTargetHorizontally();

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

        private float GetMovementTimeout(
            IReadOnlyList<EnemyPathStep> steps,
            BatPathPurpose purpose)
        {
            var previous = _view.Body.position;
            var distance = 0f;
            for (var i = 0; i < steps.Count; i++)
            {
                var next = _placement.GridToWorld(steps[i].Position);
                distance += Vector2.Distance(previous, next);
                previous = next;
            }
            return GetWorldMovementTimeout(
                distance,
                GetMovementSpeed(purpose));
        }

        private float GetWorldMovementTimeout(float distance, float movementSpeed)
        {
            var speed = Mathf.Max(0.01f, movementSpeed);
            return Mathf.Max(
                _config.MinimumMovementTimeoutSeconds,
                Mathf.Max(0f, distance) / speed +
                _config.MovementStuckBufferSeconds);
        }

        private float GetMovementSpeed(BatPathPurpose purpose) =>
            purpose == BatPathPurpose.Chase
                ? _config.ChaseSpeed
                : _config.MoveSpeed;

        private int GetRouteVariant() =>
            NormalizeIndex(_formationSlot, RouteVariantCount);

        private float GetFormationPhase() =>
            Mathf.Repeat(
                Mathf.Max(0, _formationSlot) * GoldenAngleRadians,
                Mathf.PI * 2f);

        private GridPosition GetPreferredChaseDestination(
            GridPosition targetGrid)
        {
            var startDirection = GetPreferredCardinalDirection();
            for (var i = 0; i < 4; i++)
            {
                var offset = GetCardinalDirection(startDirection + i);
                var candidate = new GridPosition(
                    targetGrid.X + Mathf.RoundToInt(offset.x),
                    targetGrid.Y + Mathf.RoundToInt(offset.y));
                if (_pathfinding.IsFlyable(candidate) &&
                    _placement.TryGetPlacement(
                        _view.TerrainCollider,
                        candidate,
                        out _))
                    return candidate;
            }
            return targetGrid;
        }

        private int GetPreferredCardinalDirection()
        {
            var direction = GetFormationDirection(_formationSlot);
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
                return direction.x < 0f ? 0 : 1;
            return direction.y < 0f ? 2 : 3;
        }

        private Vector2 GetContactApproachPosition()
        {
            var targetPosition = _target.BodyPosition;
            var radius = Mathf.Max(
                0f,
                _config.AttackContactDistance * ContactRadiusSafetyFactor);
            if (radius <= Mathf.Epsilon)
                return targetPosition;

            var startDirection = Mathf.Max(0, _formationSlot);
            for (var i = 0; i < ContactDirectionAttempts; i++)
            {
                var direction = GetFormationDirection(startDirection + i);
                var candidate = targetPosition + direction * radius;
                if (_placement.IsPlacementClear(
                        _view.TerrainCollider,
                        candidate))
                    return candidate;
            }
            return targetPosition;
        }

        private static Vector2 GetCardinalDirection(int directionIndex) =>
            NormalizeIndex(directionIndex, 4) switch
            {
                0 => Vector2.left,
                1 => Vector2.right,
                2 => Vector2.down,
                _ => Vector2.up
            };

        private static Vector2 GetFormationDirection(int directionIndex)
        {
            var angle = Mathf.PI +
                        Mathf.Max(0, directionIndex) * GoldenAngleRadians;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void BeginMovementObservation()
        {
            _movementObservationActive = true;
            _movementObservationPosition = _view.Body.position;
            _movementStallElapsed = 0f;
        }

        private bool TickMovementStall(float deltaTime)
        {
            if (!_movementObservationActive)
            {
                BeginMovementObservation();
                return false;
            }

            var currentPosition = _view.Body.position;
            var expectedStallDistance = GetObservedMovementSpeed() *
                                        _config.MovementStallTimeoutSeconds;
            var tolerance = Mathf.Max(
                0.0001f,
                Mathf.Min(
                    _config.PositionTolerance,
                    expectedStallDistance * 0.25f));
            if ((currentPosition - _movementObservationPosition).sqrMagnitude >
                tolerance * tolerance)
            {
                _movementObservationPosition = currentPosition;
                _movementStallElapsed = 0f;
                ResetRouteFailures();
                return false;
            }

            _movementStallElapsed += Mathf.Max(0f, deltaTime);
            return _movementStallElapsed >=
                   _config.MovementStallTimeoutSeconds;
        }

        private void ClearMovementObservation()
        {
            _movementObservationActive = false;
            _movementObservationPosition = _view.Body.position;
            _movementStallElapsed = 0f;
        }

        private void ReportMovementError(string reason)
        {
            var body = _view.Body;
            var step = _model.CurrentPathStep;
            var movementSpeed = GetObservedMovementSpeed();
            Debug.LogError(
                $"[BlackBatAI][MovementFailure][{_enemyId:N}] " +
                $"slot={_formationSlot} reason={reason} " +
                $"state={_model.CurrentState} purpose={_model.PathPurpose} " +
                $"rbPosition={body.position.ToString("F4")} " +
                $"actualGrid={_placement.WorldToGrid(body.position)} " +
                $"modelGrid={_model.CurrentGridPosition} " +
                $"destination={_model.Destination} " +
                $"step={(step.HasValue ? step.Value.Position.ToString() : "none")} " +
                $"pathIndex={_model.PathIndex} generation={_model.PathGeneration} " +
                $"pending={_model.PathPending} contact={_model.ContactApproachActive} " +
                $"speed={movementSpeed:F4} " +
                $"stallSeconds={_movementStallElapsed:F3} " +
                $"timeout={_model.MovementTimeoutRemaining:F3} " +
                $"simulated={body.simulated} awake={body.IsAwake()} " +
                $"navRevision={_pathfinding.NavigationRevision}",
                _view);
            ClearMovementObservation();
        }

        private float GetObservedMovementSpeed() =>
            _model.ContactApproachActive
                ? _config.ChaseSpeed
                : GetMovementSpeed(_model.PathPurpose);

        private void RecordRouteFailure(string context, string reason)
        {
            _consecutiveRouteFailures++;
            var message =
                $"[BlackBatAI][PathFailure][{_enemyId:N}] " +
                $"slot={_formationSlot} context={context} " +
                $"count={_consecutiveRouteFailures} " +
                $"reason={reason ?? "unknown"} state={_model.CurrentState} " +
                $"rbPosition={_view.Body.position.ToString("F4")} " +
                $"actualGrid={_placement.WorldToGrid(_view.Body.position)} " +
                $"modelGrid={_model.CurrentGridPosition} " +
                $"destination={_model.Destination} " +
                $"generation={_model.PathGeneration} " +
                $"navRevision={_pathfinding.NavigationRevision}";
            if (_consecutiveRouteFailures == 1)
                Debug.LogWarning(message, _view);
            else if (_consecutiveRouteFailures == 3)
                Debug.LogError(message, _view);
            else if (_config != null && _config.EnableAiTraceLogs)
                Debug.Log(message, _view);
        }

        private void ResetRouteFailures() => _consecutiveRouteFailures = 0;

        private void TraceAi(string eventName, string details)
        {
            if (_config == null || !_config.EnableAiTraceLogs)
                return;
            Debug.Log(
                $"[BlackBatAI][{eventName}][{_enemyId:N}] " +
                $"slot={_formationSlot} {details}",
                _view);
        }

        private void FaceTargetHorizontally()
        {
            if (!_target.IsTargetAvailable)
                return;
            FaceHorizontally(
                _target.BodyPosition.x - _view.Body.position.x);
        }

        private void FaceHorizontally(float horizontalDelta)
        {
            if (Mathf.Abs(horizontalDelta) <= _config.PositionTolerance)
                return;
            _view.SetFacing(horizontalDelta < 0f);
        }

        private static int GridDistance(GridPosition a, GridPosition b) =>
            Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

        private static int NormalizeIndex(int value, int count)
        {
            if (count <= 0)
                return 0;
            var normalized = value % count;
            return normalized < 0 ? normalized + count : normalized;
        }

        private static bool Contains(
            IReadOnlyList<GridPosition> positions,
            GridPosition position)
        {
            if (positions == null)
                return false;
            for (var i = 0; i < positions.Count; i++)
            {
                if (positions[i] == position)
                    return true;
            }
            return false;
        }

        private void CancelPathRequest(bool preserveFlightVisualOffset = false)
        {
            CancelPathComputation();
            ClearMovementObservation();
            _model.EndContactApproach();
            _model.ClearPath();
            if (!preserveFlightVisualOffset)
                _view.ClearFlightVisualOffset();
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

        private void HandlePathException(Exception exception)
        {
            if (exception is OperationCanceledException || _disposed ||
                _config == null)
                return;
            RecordRouteFailure("Exception", exception.Message);
            Debug.LogException(exception, _view);
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
