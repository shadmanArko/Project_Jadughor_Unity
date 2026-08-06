using System;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerDamageService :
        IPlayerDamageService,
        IDisposable
    {
        private readonly RuntimeDataScriptable _runtime;
        private readonly MinePlayerScriptable _player;
        private readonly PlayerActionService _actionService;
        private readonly PlayerDeathService _deathService;
        private readonly MinePlayerDataConfig _config;

        private PlayerRestrictionFlags _appliedRestrictions;
        private int _animationGeneration;
        private IDisposable _invincibilityTimer;

        public int DamageSequence { get; private set; }

        public PlayerDamageService(
            RuntimeDataScriptable runtime,
            MinePlayerScriptable player,
            PlayerActionService actionService,
            PlayerDeathService deathService,
            MinePlayerDataConfig config)
        {
            _runtime = runtime;
            _player = player;
            _actionService = actionService;
            _deathService = deathService;
            _config = config;
        }

        public bool ApplyDamage(
            float amount,
            PlayerDamageKind kind = PlayerDamageKind.Standard)
        {
            if (amount <= 0f ||
                _runtime.lifeState.Value == PlayerLifeState.Dead)
            {
                return false;
            }

            if (kind != PlayerDamageKind.Fall &&
                _runtime.isInvincible.Value)
            {
                return false;
            }

            var health = _player.playerData.health;
            health.Value = Mathf.Max(0f, health.Value - amount);

            var wasClimbing = _runtime.isClimbing.Value;
            _actionService.InterruptForHurt();
            _runtime.isHurt.Value = true;
            DamageSequence++;

            var requestedRestrictions =
                PlayerRestrictionFlags.Movement |
                PlayerRestrictionFlags.Action;
            if (!wasClimbing)
                requestedRestrictions |= PlayerRestrictionFlags.Climbing;

            _appliedRestrictions |=
                requestedRestrictions & ~_runtime.restrictions.Value;
            _runtime.restrictions.Value |= requestedRestrictions;

            if (health.Value > 0f)
                BeginInvincibility();
            else
                ClearInvincibility();

            return true;
        }

        private void BeginInvincibility()
        {
            ClearInvincibility();

            var duration = Mathf.Max(
                0f,
                _config.damageInvincibilityDuration);
            if (duration <= 0f)
                return;

            _runtime.isInvincible.Value = true;
            _invincibilityTimer = Observable
                .Timer(TimeSpan.FromSeconds(duration))
                .Subscribe(_ => ClearInvincibility());
        }

        private void ClearInvincibility()
        {
            _invincibilityTimer?.Dispose();
            _invincibilityTimer = null;
            _runtime.isInvincible.Value = false;
        }

        public void RegisterAnimationGeneration(int generation)
        {
            _animationGeneration = generation;
        }

        public void HandleAnimationCompleted(
            PlayerAnimationCompletedEvent animationEvent)
        {
            if (!_runtime.isHurt.Value ||
                animationEvent.AnimationId != PlayerAnimationId.Hurt ||
                animationEvent.Generation != _animationGeneration)
                return;

            _runtime.isHurt.Value = false;
            _runtime.restrictions.Value &= ~_appliedRestrictions;
            _appliedRestrictions = PlayerRestrictionFlags.None;
            if (_player.playerData.health.Value <= 0f)
                _deathService.BeginDeath();
        }

        public void Dispose()
        {
            ClearInvincibility();
        }

    }
}
