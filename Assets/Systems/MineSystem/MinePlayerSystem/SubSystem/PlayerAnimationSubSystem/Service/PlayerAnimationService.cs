using System;
using System.Collections.Generic;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Service
{
    [Serializable]
    public sealed class PlayerAnimationService : IInitializable, ITickable
    {
        private readonly PlayerView _view;
        private readonly RuntimeDataScriptable _runtime;
        private readonly AnimationProfile _profile;
        private readonly PlayerActionService _actionService;
        private readonly PlayerDeathService _deathService;
        private readonly PlayerDamageService _damageService;
        private readonly PlayerPauseStateData _pauseState;

        private string _currentAnimation = PlayerAnimationId.None;
        private int _currentActionSequence = -1;
        private int _currentDamageSequence = -1;
        private readonly HashSet<string> _missingAnimations =
            new(StringComparer.Ordinal);

        public PlayerAnimationService(
            PlayerView view,
            RuntimeDataScriptable runtime,
            AnimationProfile profile,
            PlayerActionService actionService,
            PlayerDeathService deathService,
            PlayerDamageService damageService,
            PlayerPauseStateData pauseState)
        {
            _view = view;
            _runtime = runtime;
            _profile = profile;
            _actionService = actionService;
            _deathService = deathService;
            _damageService = damageService;
            _pauseState = pauseState;
        }

        public void Initialize()
        {
            _view.AnimationController.ApplyProfile(_profile);
            _runtime.facingDirection.Value = _profile.defaultFacing;
        }

        public void Tick()
        {
            if (_pauseState.IsPaused)
                return;
            _view.AnimationController.SetFacing(
                _runtime.facingDirection.Value);

            var animationId = ResolveAnimation();
            var isActionAnimation =
                _runtime.actionState.Value == PlayerActionState.PrimaryAction;
            var restartAction =
                isActionAnimation &&
                _actionService.ActionSequence != _currentActionSequence;
            var isHurtAnimation =
                animationId == PlayerAnimationId.Hurt;
            var restartHurt =
                isHurtAnimation &&
                _damageService.DamageSequence != _currentDamageSequence;

            if (animationId == _currentAnimation &&
                !restartAction &&
                !restartHurt)
                return;

            if (!_profile.TryGet(animationId, out var animationData))
            {
                if (_missingAnimations.Add(animationId))
                {
                    Debug.LogWarning(
                        $"Player animation '{animationId}' is missing from " +
                        $"profile '{_profile.name}'.");
                }
                return;
            }

            _currentAnimation = animationId;
            _runtime.activeAnimation.Value = animationId;
            var generation = _view.AnimationController.Play(
                animationData,
                restartAction || restartHurt);

            if (isActionAnimation)
                _currentActionSequence = _actionService.ActionSequence;
            if (isHurtAnimation)
                _currentDamageSequence = _damageService.DamageSequence;

            if (animationId == PlayerAnimationId.PrimaryAction ||
                animationId == PlayerAnimationId.Interact ||
                isActionAnimation)
            {
                _actionService.RegisterAnimationGeneration(generation);
            }
            else if (animationId == PlayerAnimationId.Death)
            {
                _deathService.RegisterAnimationGeneration(generation);
            }
            else if (isHurtAnimation)
            {
                _damageService.RegisterAnimationGeneration(generation);
            }
        }

        private string ResolveAnimation()
        {
            if (_runtime.lifeState.Value == PlayerLifeState.Dead)
                return PlayerAnimationId.Death;

            if (_runtime.isHurt.Value)
                return PlayerAnimationId.Hurt;

            if (_runtime.isDamagingFall.Value)
                return PlayerAnimationId.Fall;

            if (!string.IsNullOrEmpty(_runtime.forcedAnimation.Value))
                return _runtime.forcedAnimation.Value;

            switch (_runtime.actionState.Value)
            {
                case PlayerActionState.PrimaryAction:
                    return _actionService.ActiveAnimationId;
                case PlayerActionState.Interacting:
                    return PlayerAnimationId.Interact;
            }

            if (_runtime.isClimbing.Value)
            {
                var velocity = _runtime.velocity.Value;
                if (velocity.sqrMagnitude <= 0.0001f)
                    return PlayerAnimationId.ClimbIdle;

                if (Mathf.Abs(velocity.y) > 0.01f)
                    return PlayerAnimationId.ClimbVertical;

                return PlayerAnimationId.ClimbHorizontal;
            }

            return _runtime.locomotionState.Value switch
            {
                PlayerLocomotionState.Moving => PlayerAnimationId.Move,
                _ => _runtime.facingDirection.Value ==
                     PlayerFacingDirection.Left
                    ? PlayerAnimationId.IdleLeft
                    : PlayerAnimationId.IdleRight
            };
        }
    }
}
