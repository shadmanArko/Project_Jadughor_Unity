using System;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Controller
{
    /// <summary>
    /// Per-entry controller for a pooled spike. Its lifetime is advanced by the
    /// Hedgehog state machine's fixed tick, not by an Update method or task.
    /// </summary>
    public sealed class HedgehogSpikeProjectileController : IDisposable
    {
        private readonly HedgehogSpikeProjectileView _view;
        private readonly Action<HedgehogSpikeProjectileController> _release;
        private readonly CompositeDisposable _subscriptions = new();
        private IEnemyTargetProvider _target;
        private IEnemyAttackService _attack;
        private EnemyStatusEffectSpec _statusEffect;
        private float _damage;
        private float _remainingLifetime;
        private bool _active;
        private bool _paused;
        private bool _bodyWasSimulated;
        private Vector2 _velocity;
        private float _angularVelocity;
        private bool _disposed;

        public bool IsActive => _active;

        public HedgehogSpikeProjectileController(
            HedgehogSpikeProjectileView view,
            Action<HedgehogSpikeProjectileController> release)
        {
            _view = view;
            _release = release;
            _view.TriggerEntered
                .Subscribe(HandleTriggerEntered)
                .AddTo(_subscriptions);
        }

        public void Launch(
            Vector2 position,
            Vector2 direction,
            float speed,
            float lifetime,
            float damage,
            EnemyStatusEffectSpec statusEffect,
            IEnemyTargetProvider target,
            IEnemyAttackService attack)
        {
            if (_disposed || direction.sqrMagnitude <= 0.0001f ||
                speed <= 0f || lifetime <= 0f || damage <= 0f ||
                target == null || attack == null)
            {
                _active = true;
                Release();
                return;
            }

            _target = target;
            _attack = attack;
            _statusEffect = statusEffect;
            _damage = damage;
            _remainingLifetime = lifetime;
            _paused = false;
            _active = true;
            _view.gameObject.SetActive(true);
            _view.Launch(position, direction.normalized, speed);
        }

        public void OnFixedTick(float deltaTime)
        {
            if (!_active || _paused)
                return;

            _remainingLifetime -= Mathf.Max(0f, deltaTime);
            if (_remainingLifetime <= 0f)
                Release();
        }

        public void Pause()
        {
            if (!_active || _paused)
                return;
            _paused = true;
            var body = _view.Body;
            _bodyWasSimulated = body.simulated;
            _velocity = body.linearVelocity;
            _angularVelocity = body.angularVelocity;
            _view.Stop();
            body.simulated = false;
        }

        public void Resume()
        {
            if (!_active || !_paused)
                return;
            _paused = false;
            var body = _view.Body;
            body.simulated = _bodyWasSimulated;
            body.linearVelocity = _velocity;
            body.angularVelocity = _angularVelocity;
        }

        private void HandleTriggerEntered(Collider2D collider)
        {
            if (!_active || _paused || collider == null)
                return;

            if (_target.IsTargetCollider(collider))
                _attack.TryAttack(_damage, _statusEffect);
            Release();
        }

        public void Release()
        {
            if (!_active)
                return;
            _active = false;
            _paused = false;
            _remainingLifetime = 0f;
            _target = null;
            _attack = null;
            _damage = 0f;
            _view.ResetRuntime();
            _view.gameObject.SetActive(false);
            _release?.Invoke(this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Release();
            _subscriptions.Dispose();
        }
    }
}
