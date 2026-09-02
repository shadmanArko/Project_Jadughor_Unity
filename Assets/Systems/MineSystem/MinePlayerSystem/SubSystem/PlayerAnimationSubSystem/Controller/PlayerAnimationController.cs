using System;
using Systems.MineSystem.ActorSystem.Animation;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Controller
{
    public sealed class PlayerAnimationController :
        ActorAnimationControllerBase<AnimationData>
    {
        private readonly Subject<PlayerAnimationMarkerEvent> _markerRaised = new();
        private readonly Subject<PlayerAnimationCompletedEvent> _completed = new();

        public IObservable<PlayerAnimationMarkerEvent> MarkerRaised =>
            _markerRaised;
        public IObservable<PlayerAnimationCompletedEvent> Completed =>
            _completed;
        public string CurrentAnimationId =>
            CurrentClip?.id ?? PlayerAnimationId.None;
        public int CurrentGeneration => CurrentGenerationCore;
        public float AnimatorSpeed => AnimatorSpeedCore;

        public void ApplyProfile(AnimationProfile profile)
        {
            if (profile == null)
                return;

            if (animator != null)
                animator.runtimeAnimatorController = profile.animatorController;
            transform.localPosition = profile.visualOffset;
            transform.localScale = profile.visualScale;
            SetFacing(profile.defaultFacing);
        }

        public int Play(
            AnimationData animationData,
            bool restartCurrent = false) =>
            PlayCore(animationData, restartCurrent);

        public void SetFacing(PlayerFacingDirection direction) =>
            SetFacing(direction == PlayerFacingDirection.Left);

        public void SetAnimatorSpeed(float speed) => SetAnimatorSpeedCore(speed);

        public void AnimationEvent_AdvanceFrame() => AdvanceFrameCore();

        public void AnimationEvent_Marker(int marker)
        {
            if (TryRaiseMarker(out var animationId, out var generation))
                _markerRaised.OnNext(new PlayerAnimationMarkerEvent(
                    animationId, generation, marker));
        }

        public void AnimationEvent_Complete()
        {
            if (TryRaiseCompletion(out var animationId, out var generation))
                _completed.OnNext(new PlayerAnimationCompletedEvent(
                    animationId, generation));
        }

        private void OnDestroy()
        {
            _markerRaised.Dispose();
            _completed.Dispose();
        }
    }
}
