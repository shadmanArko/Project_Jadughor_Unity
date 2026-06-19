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

        private string _currentAnimation = PlayerAnimationId.None;
        private readonly HashSet<string> _missingAnimations =
            new(StringComparer.Ordinal);

        public PlayerAnimationService(
            PlayerView view,
            RuntimeDataScriptable runtime,
            AnimationProfile profile,
            PlayerActionService actionService,
            PlayerDeathService deathService)
        {
            _view = view;
            _runtime = runtime;
            _profile = profile;
            _actionService = actionService;
            _deathService = deathService;
        }

        public void Initialize()
        {
            _view.AnimationController.ApplyProfile(_profile);
            _runtime.facingDirection.Value = _profile.defaultFacing;
        }

        public void Tick()
        {
            _view.AnimationController.SetFacing(
                _runtime.facingDirection.Value);

            var animationId = ResolveAnimation();
            if (animationId == _currentAnimation)
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
            var generation = _view.AnimationController.Play(animationData);

            if (animationId == PlayerAnimationId.PrimaryAction ||
                animationId == PlayerAnimationId.Interact ||
                _runtime.actionState.Value == PlayerActionState.PrimaryAction)
            {
                _actionService.RegisterAnimationGeneration(generation);
            }
            else if (animationId == PlayerAnimationId.Death)
            {
                _deathService.RegisterAnimationGeneration(generation);
            }
        }

        private string ResolveAnimation()
        {
            if (_runtime.lifeState.Value == PlayerLifeState.Dead)
                return PlayerAnimationId.Death;

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
                PlayerLocomotionState.Falling => PlayerAnimationId.Fall,
                PlayerLocomotionState.Moving => PlayerAnimationId.Move,
                _ => PlayerAnimationId.Idle
            };
        }
    }
}
