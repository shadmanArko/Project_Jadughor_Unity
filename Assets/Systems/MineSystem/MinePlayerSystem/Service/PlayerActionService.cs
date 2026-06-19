using System;
using System.Collections.Generic;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    [Serializable]
    public sealed class PlayerActionService :
        IPlayerItemAnimationService,
        IPlayerFixedTickService,
        IDisposable
    {
        private readonly RuntimeDataScriptable _runtime;
        private readonly AnimationProfile _profile;
        private readonly Subject<PlayerAnimationMarkerEvent> _markerReached = new();
        private readonly Subject<PlayerAnimationCompletedEvent> _actionCompleted = new();
        private readonly Subject<string> _actionFailed = new();

        private PlayerActionState _requestedAction;
        private string _requestedAnimationId;
        private string _activeAnimationId;
        private PlayerRestrictionFlags _appliedRestrictions;
        private int _animationGeneration;
        private int _actionSequence;
        private readonly HashSet<string> _invalidItemAnimations = new();

        public IObservable<PlayerAnimationMarkerEvent> MarkerReached =>
            _markerReached;
        public IObservable<PlayerAnimationCompletedEvent> ActionCompleted =>
            _actionCompleted;
        public IObservable<string> ActionFailed => _actionFailed;
        public string ActiveAnimationId =>
            _activeAnimationId ?? PlayerAnimationId.None;
        public int ActionSequence => _actionSequence;

        public PlayerActionService(
            RuntimeDataScriptable runtime,
            AnimationProfile profile)
        {
            _runtime = runtime;
            _profile = profile;
        }

        public void RequestAction()
        {
            _requestedAction = PlayerActionState.PrimaryAction;
            _requestedAnimationId = PlayerAnimationId.PrimaryAction;
        }

        public void RequestInteraction()
        {
            _requestedAction = PlayerActionState.Interacting;
            _requestedAnimationId = PlayerAnimationId.Interact;
        }

        public bool TryRequestItemAction(string animationId)
        {
            if (string.IsNullOrWhiteSpace(animationId) ||
                _profile == null ||
                !_profile.TryGet(animationId, out var animationData) ||
                animationData.animationSprites == null ||
                animationData.animationSprites.Count == 0)
            {
                if (_invalidItemAnimations.Add(animationId ?? string.Empty))
                {
                    Debug.LogWarning(
                        $"Cannot start toolbar item animation '{animationId}': " +
                        "it is missing or has no sprites.");
                }

                return false;
            }

            if (
                _runtime.lifeState.Value == PlayerLifeState.Dead ||
                !_runtime.canPerformAction.Value ||
                _runtime.actionState.Value != PlayerActionState.None ||
                _runtime.HasRestriction(PlayerRestrictionFlags.Action) ||
                _requestedAction != PlayerActionState.None)
                return false;

            _requestedAction = PlayerActionState.PrimaryAction;
            _requestedAnimationId = animationId;
            return true;
        }

        public void OnFixedTick()
        {
            if (_runtime.lifeState.Value == PlayerLifeState.Dead)
            {
                var interruptedAnimation =
                    _activeAnimationId ?? _requestedAnimationId;
                if (!string.IsNullOrEmpty(interruptedAnimation))
                    _actionFailed.OnNext(interruptedAnimation);

                _requestedAction = PlayerActionState.None;
                _requestedAnimationId = null;
                _activeAnimationId = null;
                _runtime.actionState.Value = PlayerActionState.None;
                _appliedRestrictions = PlayerRestrictionFlags.None;
                return;
            }

            if (_requestedAction == PlayerActionState.None ||
                _runtime.actionState.Value != PlayerActionState.None)
                return;

            var requested = _requestedAction;
            _requestedAction = PlayerActionState.None;

            if (!_runtime.canPerformAction.Value ||
                _runtime.HasRestriction(PlayerRestrictionFlags.Action))
            {
                _actionFailed.OnNext(_requestedAnimationId);
                _requestedAnimationId = null;
                return;
            }

            BeginAction(requested, _requestedAnimationId);
        }

        public void RegisterAnimationGeneration(int generation)
        {
            _animationGeneration = generation;
        }

        public void HandleAnimationMarker(PlayerAnimationMarkerEvent animationEvent)
        {
            if (animationEvent.Generation != _animationGeneration ||
                animationEvent.AnimationId != ActiveAnimationId)
                return;

            _markerReached.OnNext(animationEvent);
        }

        public void HandleAnimationCompleted(
            PlayerAnimationCompletedEvent animationEvent)
        {
            if (animationEvent.Generation != _animationGeneration ||
                animationEvent.AnimationId != ActiveAnimationId)
                return;

            CancelAction();
            _actionCompleted.OnNext(animationEvent);
        }

        private void BeginAction(
            PlayerActionState actionState,
            string requestedAnimationId)
        {
            var animationId = string.IsNullOrWhiteSpace(requestedAnimationId)
                ? GetDefaultAnimationId(actionState)
                : requestedAnimationId;
            if (_profile == null ||
                !_profile.TryGet(animationId, out var animationData))
            {
                Debug.LogWarning(
                    $"Cannot start player action '{actionState}': animation " +
                    $"'{animationId}' is missing.");
                _actionFailed.OnNext(animationId);
                _requestedAnimationId = null;
                return;
            }

            _activeAnimationId = animationId;
            _requestedAnimationId = null;
            _actionSequence++;
            _runtime.actionState.Value = actionState;
            var requestedRestrictions =
                PlayerRestrictionFlags.Action | animationData.restrictions;
            _appliedRestrictions =
                requestedRestrictions & ~_runtime.restrictions.Value;

            _runtime.restrictions.Value |= requestedRestrictions;
        }

        private void CancelAction()
        {
            _requestedAction = PlayerActionState.None;
            _requestedAnimationId = null;
            _activeAnimationId = null;
            _runtime.actionState.Value = PlayerActionState.None;
            _runtime.restrictions.Value &= ~_appliedRestrictions;
            _appliedRestrictions = PlayerRestrictionFlags.None;
        }

        private static string GetDefaultAnimationId(
            PlayerActionState actionState)
        {
            return actionState switch
            {
                PlayerActionState.PrimaryAction =>
                    PlayerAnimationId.PrimaryAction,
                PlayerActionState.Interacting =>
                    PlayerAnimationId.Interact,
                _ => PlayerAnimationId.None
            };
        }

        public void Dispose()
        {
            _markerReached.Dispose();
            _actionCompleted.Dispose();
            _actionFailed.Dispose();
        }
    }
}
