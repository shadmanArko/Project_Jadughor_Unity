using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerDamageService : IPlayerDamageService
    {
        private readonly RuntimeDataScriptable _runtime;
        private readonly MinePlayerScriptable _player;
        private readonly PlayerActionService _actionService;
        private readonly PlayerDeathService _deathService;

        private PlayerRestrictionFlags _appliedRestrictions;
        private int _animationGeneration;

        public int DamageSequence { get; private set; }

        public PlayerDamageService(
            RuntimeDataScriptable runtime,
            MinePlayerScriptable player,
            PlayerActionService actionService,
            PlayerDeathService deathService)
        {
            _runtime = runtime;
            _player = player;
            _actionService = actionService;
            _deathService = deathService;
        }

        public bool ApplyDamage(float amount)
        {
            if (amount <= 0f ||
                _runtime.lifeState.Value == PlayerLifeState.Dead)
                return false;

            var health = _player.playerData.health;
            health.Value = Mathf.Max(0f, health.Value - amount);

            _actionService.InterruptForHurt();
            _runtime.isClimbing.Value = false;
            _runtime.isHurt.Value = true;
            DamageSequence++;

            var requestedRestrictions =
                PlayerRestrictionFlags.Movement |
                PlayerRestrictionFlags.Climbing |
                PlayerRestrictionFlags.Action;
            _appliedRestrictions |=
                requestedRestrictions & ~_runtime.restrictions.Value;
            _runtime.restrictions.Value |= requestedRestrictions;
            return true;
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
    }
}
