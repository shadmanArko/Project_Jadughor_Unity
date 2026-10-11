using System;
using Systems.MineSystem.BossLairSystem.Signal;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Enum;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Service;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.PauseSystem.Service;
using Systems.Utilities.ScreenShake;
using Systems.Utilities.EventBus;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Controller
{
    /// <summary>
    /// Fixed-tick combat brain for the lair-owned Hedgehog. It deliberately
    /// knows nothing about mine grids, enemy waves, relocation, or EnemyManager:
    /// the arena is a small authored physics space and BossLairController owns
    /// its lifecycle and single fixed tick.
    /// </summary>
    public sealed class HedgehogBossStateMachine : IDisposable
    {
        private readonly HedgehogBossModel _model;
        private readonly HedgehogBossView _view;
        private readonly IEnemyTargetProvider _target;
        private readonly IEnemyAttackService _attack;
        private readonly HedgehogBossRewardService _rewards;
        private readonly HedgehogSpikeProjectilePool _projectiles;
        private readonly PauseGate _pauseGate = new();

        private HedgehogBossConfigScriptable _config;
        private Guid _enemyId;
        private int _animationGeneration;
        private bool _stunAnimationComplete;
        private bool _disposed;

        public HedgehogBossStateMachine(
            HedgehogBossModel model,
            HedgehogBossView view,
            IEnemyTargetProvider target,
            IEnemyAttackService attack,
            HedgehogBossRewardService rewards,
            HedgehogSpikeProjectilePool projectiles)
        {
            _model = model;
            _view = view;
            _target = target;
            _attack = attack;
            _rewards = rewards;
            _projectiles = projectiles;
        }

        public void Initialize(HedgehogBossConfigScriptable config, Guid enemyId)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HedgehogBossStateMachine));

            _config = config ?? throw new ArgumentNullException(nameof(config));
            _enemyId = enemyId;
            _animationGeneration = 0;
            _stunAnimationComplete = false;
            _model.Initialize(config);
            EnterState(HedgehogBossState.Dormant, null);
        }

        public void ActivateCombat()
        {
            if (_disposed || _config == null ||
                _model.State != HedgehogBossState.Dormant)
                return;

            _view.SetPhysicsSimulation(true);
            EnterState(HedgehogBossState.Idle, HedgehogBossAnimationId.Idle);
        }

        public void OnFixedTick(EnemyTickContext tickContext)
        {
            if (_disposed || _config == null || _pauseGate.IsPaused)
                return;

            var deltaTime = Mathf.Max(0f, tickContext.FixedDeltaTime);
            _model.Tick(deltaTime);
            _projectiles.OnFixedTick(deltaTime);

            switch (_model.State)
            {
                case HedgehogBossState.Idle:
                    TickIdle();
                    break;
                case HedgehogBossState.Chase:
                    TickChase();
                    break;
                case HedgehogBossState.RollCharge:
                    TickCharge();
                    break;
                case HedgehogBossState.Stunned:
                    if (_stunAnimationComplete &&
                        _model.RecoveryRemaining <= 0f)
                    {
                        EnterState(HedgehogBossState.Idle,
                            HedgehogBossAnimationId.Idle);
                    }
                    break;
            }
        }

        public void HandleAnimationMarker(EnemyAnimationMarkerEvent animationEvent)
        {
            if (_disposed || animationEvent.Generation != _animationGeneration ||
                animationEvent.Marker != (int)EnemyAnimationMarker.AttackImpact ||
                _model.AttackImpactApplied)
                return;

            switch (_model.State)
            {
                case HedgehogBossState.GroundSmash:
                    _model.AttackImpactApplied = true;
                    FaceCombatTarget();
                    TryApplyGroundSmash();
                    break;
                case HedgehogBossState.SpikeThrow:
                    _model.AttackImpactApplied = true;
                    FaceCombatTarget();
                    TryLaunchSpike();
                    break;
                case HedgehogBossState.RollWindup:
                    _model.AttackImpactApplied = true;
                    FaceCombatTarget();
                    StartCharge();
                    break;
            }
        }

        public void HandleAnimationCompleted(EnemyAnimationCompletedEvent animationEvent)
        {
            if (_disposed || animationEvent.Generation != _animationGeneration)
                return;

            switch (_model.State)
            {
                case HedgehogBossState.GroundSmash:
                case HedgehogBossState.SpikeThrow:
                    CompleteAttack();
                    break;
                case HedgehogBossState.RollWindup:
                    // A missing marker must not strand the boss in windup. The
                    // authored Roll Out marker normally starts this transition.
                    StartCharge();
                    break;
                case HedgehogBossState.Stunned:
                    _stunAnimationComplete = true;
                    break;
                case HedgehogBossState.Hurt:
                    EnterState(HedgehogBossState.Idle,
                        HedgehogBossAnimationId.Idle);
                    break;
                case HedgehogBossState.Death:
                    FinishDeath();
                    break;
            }
        }

        public void HandleCollision(Collider2D collider)
        {
            if (_disposed || _model.State != HedgehogBossState.RollCharge ||
                _model.ChargeContactApplied || collider == null ||
                !_target.IsTargetCollider(collider))
                return;

            _model.ChargeContactApplied = true;
            _attack.TryAttack(
                _config.ChargeDamage * GetDamageMultiplier(),
                _config.StatusEffect);
        }

        public void ApplyDamage(float amount)
        {
            if (_disposed || !CanReceiveDamage() ||
                !_model.ApplyDamage(amount, _config))
                return;

            if (_model.IsDead)
            {
                EnterState(HedgehogBossState.Death,
                    HedgehogBossAnimationId.Death);
                return;
            }

            EnterState(HedgehogBossState.Hurt, HedgehogBossAnimationId.Hurt);
        }

        public void Pause()
        {
            if (_disposed)
                return;
            _pauseGate.Pause();
            _projectiles.Pause();
        }

        public void Resume()
        {
            if (_disposed)
                return;
            _pauseGate.Resume();
            _projectiles.Resume();
        }

        public void Release()
        {
            if (_disposed)
                return;
            _projectiles.ReleaseAll();
            _config = null;
            _enemyId = Guid.Empty;
            _animationGeneration = 0;
            _stunAnimationComplete = false;
        }

        private void TickIdle()
        {
            _view.StopHorizontalMotion();
            if (!HasCombatTarget() || _model.RecoveryRemaining > 0f)
                return;

            var distance = HorizontalDistanceToTarget();
            if (TryStartAttack(distance))
                return;
            if (distance > _config.PositionTolerance)
                EnterState(HedgehogBossState.Chase, HedgehogBossAnimationId.Move);
        }

        private void TickChase()
        {
            if (!HasCombatTarget())
            {
                EnterState(HedgehogBossState.Idle, HedgehogBossAnimationId.Idle);
                return;
            }

            var horizontalDelta = _target.BodyPosition.x - _view.Body.position.x;
            var distance = Mathf.Abs(horizontalDelta);
            if (_model.RecoveryRemaining <= 0f && TryStartAttack(distance))
                return;

            if (distance <= _config.PositionTolerance ||
                distance > _config.AggroDistance)
            {
                EnterState(HedgehogBossState.Idle, HedgehogBossAnimationId.Idle);
                return;
            }

            SetFacingFromHorizontalDelta(horizontalDelta);
            _view.SetHorizontalVelocity(
                Mathf.Sign(horizontalDelta) * _config.MoveSpeed *
                GetMoveSpeedMultiplier());
        }

        private void TickCharge()
        {
            var direction = _model.FacesLeft ? Vector2.left : Vector2.right;
            _view.SetHorizontalVelocity(
                direction.x * _config.ChargeSpeed * GetMoveSpeedMultiplier());

            if (_view.HasObstacleAhead(
                    direction,
                    _config.ChargeObstacleProbeDistance,
                    _config.GroundLayerMask))
            {
                EnterStunned();
                return;
            }

            // The wall collision is the intended end of a charge. This timeout
            // only guarantees recovery if broken level geometry leaves no wall.
            if (_model.ChargeRemaining <= 0f)
            {
                _model.StartAttackCooldown(GetChargeCooldown());
                EnterState(HedgehogBossState.Idle, HedgehogBossAnimationId.Idle);
            }
        }

        private bool TryStartAttack(float horizontalDistance)
        {
            if (_model.AttackCooldownRemaining > 0f ||
                !HasCombatTarget() || _model.ActivePhase == null)
                return false;

            var canGroundSmash = horizontalDistance <= _config.GroundSmashRange;
            var canSpikeThrow =
                horizontalDistance >= _config.SpikeThrowMinimumRange &&
                horizontalDistance <= _config.SpikeThrowMaximumRange;
            var canCharge = _model.ActivePhase.allowChargeAttack &&
                            horizontalDistance >= _config.ChargeMinimumRange &&
                            horizontalDistance <= _config.ChargeMaximumRange &&
                            HasClearChargeLane();

            var attack = PickAttack(canGroundSmash, canSpikeThrow, canCharge);
            switch (attack)
            {
                case HedgehogBossAttack.GroundSmash:
                    FaceCombatTarget();
                    EnterState(HedgehogBossState.GroundSmash,
                        HedgehogBossAnimationId.GroundSmash);
                    return true;
                case HedgehogBossAttack.SpikeThrow:
                    FaceCombatTarget();
                    EnterState(HedgehogBossState.SpikeThrow,
                        HedgehogBossAnimationId.SpikeThrow);
                    return true;
                case HedgehogBossAttack.RollCharge:
                    FaceCombatTarget();
                    EnterState(HedgehogBossState.RollWindup,
                        HedgehogBossAnimationId.RollOut);
                    return true;
                default:
                    return false;
            }
        }

        private HedgehogBossAttack PickAttack(
            bool canGroundSmash,
            bool canSpikeThrow,
            bool canCharge)
        {
            var phase = _model.ActivePhase;
            var groundWeight = canGroundSmash ? phase.groundSmashWeight : 0f;
            var spikeWeight = canSpikeThrow ? phase.spikeThrowWeight : 0f;
            var chargeWeight = canCharge ? phase.chargeWeight : 0f;
            var total = groundWeight + spikeWeight + chargeWeight;
            if (total <= 0f)
                return HedgehogBossAttack.None;

            var roll = UnityEngine.Random.value * total;
            if ((roll -= groundWeight) <= 0f)
                return HedgehogBossAttack.GroundSmash;
            if ((roll -= spikeWeight) <= 0f)
                return HedgehogBossAttack.SpikeThrow;
            return HedgehogBossAttack.RollCharge;
        }

        private void TryApplyGroundSmash()
        {
            ScreenShakeController.RandomShake(
                _config.GroundSmashScreenShakeDuration,
                _config.GroundSmashScreenShakeStrength);

            if (!IsGroundSmashHitValid())
                return;

            _attack.TryAttack(
                _config.Damage * GetDamageMultiplier(),
                _config.StatusEffect);
        }

        private void TryLaunchSpike()
        {
            if (!HasCombatTarget())
                return;

            var direction = _model.FacesLeft ? Vector2.left : Vector2.right;
            var spawnPosition = (Vector2)_view.Body.position + direction * 0.45f;
            _projectiles.TryLaunch(
                _config,
                spawnPosition,
                _target.BodyPosition,
                GetDamageMultiplier(),
                _target,
                _attack);
        }

        private void StartCharge()
        {
            if (_model.State != HedgehogBossState.RollWindup)
                return;

            _model.StartCharge(_config.ChargeMaxDuration);
            EnterState(HedgehogBossState.RollCharge, HedgehogBossAnimationId.Roll);
        }

        private void EnterStunned()
        {
            _view.StopHorizontalMotion();
            _stunAnimationComplete = false;
            _model.StartRecovery(_config.ChargeWallStunSeconds);
            _model.StartAttackCooldown(GetChargeCooldown());
            EnterState(HedgehogBossState.Stunned, HedgehogBossAnimationId.Stunned);
        }

        private void CompleteAttack()
        {
            _model.StartAttackCooldown(GetAttackCooldown());
            _model.StartRecovery(_config.AttackRecoverySeconds);
            EnterState(HedgehogBossState.Idle, HedgehogBossAnimationId.Idle);
        }

        private void FinishDeath()
        {
            if (_model.RewardsDropped)
                return;

            _model.RewardsDropped = true;
            _view.StopHorizontalMotion();
            _view.SetDamageEnabled(false);
            _rewards.Drop(_config, _view.Body.position);
            GlobalEventBus.Fire(new BossDefeatedSignal(
                _config.BossVariant,
                _enemyId));
            _model.SetState(HedgehogBossState.Defeated);
        }

        private void EnterState(HedgehogBossState state, string animationId)
        {
            _model.SetState(state);
            _view.StopHorizontalMotion();
            _view.SetDamageEnabled(CanReceiveDamageInState(state));

            if (state == HedgehogBossState.Death)
                _projectiles.ReleaseAll();

            if (string.IsNullOrWhiteSpace(animationId))
            {
                _animationGeneration = 0;
                return;
            }

            if (_config.AnimationProfile.TryGet(animationId, out var animation))
            {
                _animationGeneration = _view.Play(animation, restart: true);
                return;
            }

            Debug.LogError(
                $"[HedgehogBoss] Missing required animation '{animationId}'.",
                _view);
            _animationGeneration = 0;
        }

        private bool HasCombatTarget() =>
            _target != null && _target.IsCombatTargetAvailable &&
            HorizontalDistanceToTarget() <= _config.AggroDistance;

        private bool IsGroundSmashHitValid()
        {
            if (!HasCombatTarget())
                return false;

            var delta = _target.BodyPosition - _view.Body.position;
            if (delta.sqrMagnitude >
                _config.GroundSmashRange * _config.GroundSmashRange)
                return false;

            var targetIsLeft = delta.x < -_config.PositionTolerance;
            var targetIsRight = delta.x > _config.PositionTolerance;
            return (!targetIsLeft && !targetIsRight) ||
                   targetIsLeft == _model.FacesLeft;
        }

        private bool HasClearChargeLane()
        {
            var horizontalDelta = _target.BodyPosition.x - _view.Body.position.x;
            if (Mathf.Abs(horizontalDelta) <= _config.PositionTolerance)
                return false;

            return !_view.HasObstacleAhead(
                horizontalDelta < 0f ? Vector2.left : Vector2.right,
                Mathf.Abs(horizontalDelta),
                _config.GroundLayerMask);
        }

        private float HorizontalDistanceToTarget() => _target == null
            ? float.MaxValue
            : Mathf.Abs(_target.BodyPosition.x - _view.Body.position.x);

        private void SetFacingFromHorizontalDelta(float horizontalDelta)
        {
            if (Mathf.Abs(horizontalDelta) <= _config.PositionTolerance)
                return;
            _model.FacesLeft = horizontalDelta < 0f;
            _view.SetFacing(_model.FacesLeft);
        }

        private void FaceCombatTarget()
        {
            if (!HasCombatTarget())
                return;
            SetFacingFromHorizontalDelta(
                _target.BodyPosition.x - _view.Body.position.x);
        }

        private bool CanReceiveDamage() =>
            CanReceiveDamageInState(_model.State);

        private static bool CanReceiveDamageInState(HedgehogBossState state) =>
            state == HedgehogBossState.Idle || state == HedgehogBossState.Chase;

        private float GetMoveSpeedMultiplier() =>
            _model.ActivePhase?.moveSpeedMultiplier ?? 1f;

        private float GetDamageMultiplier() =>
            _model.ActivePhase?.damageMultiplier ?? 1f;

        private float GetAttackCooldown() =>
            _config.AttackCooldown *
            (_model.ActivePhase?.attackCooldownMultiplier ?? 1f);

        private float GetChargeCooldown() =>
            _config.ChargeCooldown *
            (_model.ActivePhase?.attackCooldownMultiplier ?? 1f);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _projectiles.ReleaseAll();
            _pauseGate.Dispose();
            _config = null;
        }
    }
}
