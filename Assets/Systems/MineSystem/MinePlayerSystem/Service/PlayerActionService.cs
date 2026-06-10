using System;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    [Serializable]
    public sealed class PlayerActionService : IPlayerFixedTickService, IDisposable
    {
        private readonly RuntimeDataScriptable _runtime;
        private readonly AnimationProfile _profile;
        private readonly Subject<PlayerAnimationMarkerEvent> _markerReached = new();

        private PlayerActionState _requestedAction;
        private PlayerRestrictionFlags _appliedRestrictions;
        private int _animationGeneration;

        public IObservable<PlayerAnimationMarkerEvent> MarkerReached =>
            _markerReached;

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
        }

        public void RequestInteraction()
        {
            _requestedAction = PlayerActionState.Interacting;
        }

        public void OnFixedTick()
        {
            if (_runtime.lifeState.Value == PlayerLifeState.Dead)
            {
                _requestedAction = PlayerActionState.None;
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
                return;

            BeginAction(requested);
        }

        public void RegisterAnimationGeneration(int generation)
        {
            _animationGeneration = generation;
        }

        public void HandleAnimationMarker(PlayerAnimationMarkerEvent animationEvent)
        {
            if (animationEvent.Generation != _animationGeneration ||
                animationEvent.AnimationId != GetAnimationId(
                    _runtime.actionState.Value))
                return;

            _markerReached.OnNext(animationEvent);
        }

        public void HandleAnimationCompleted(
            PlayerAnimationCompletedEvent animationEvent)
        {
            if (animationEvent.Generation != _animationGeneration ||
                animationEvent.AnimationId != GetAnimationId(
                    _runtime.actionState.Value))
                return;

            CancelAction();
        }

        private void BeginAction(PlayerActionState actionState)
        {
            var animationId = GetAnimationId(actionState);
            if (_profile == null ||
                !_profile.TryGet(animationId, out var animationData))
            {
                Debug.LogWarning(
                    $"Cannot start player action '{actionState}': animation " +
                    $"'{animationId}' is missing.");
                return;
            }

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
            _runtime.actionState.Value = PlayerActionState.None;
            _runtime.restrictions.Value &= ~_appliedRestrictions;
            _appliedRestrictions = PlayerRestrictionFlags.None;
        }

        private static string GetAnimationId(
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
        }
    }
}
