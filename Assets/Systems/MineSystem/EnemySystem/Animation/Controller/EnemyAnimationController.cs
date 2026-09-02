using System;
using Systems.MineSystem.ActorSystem.Animation;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Animation.Scriptable;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Animation.Controller
{
    public sealed class EnemyAnimationController :
        ActorAnimationControllerBase<EnemyAnimationData>
    {
        private readonly Subject<EnemyAnimationMarkerEvent> _markers = new();
        private readonly Subject<EnemyAnimationCompletedEvent> _completed = new();
        private Vector2 _profileVisualOffset;
        private Vector2 _runtimeVisualOffset;

        public IObservable<EnemyAnimationMarkerEvent> Markers => _markers;
        public IObservable<EnemyAnimationCompletedEvent> Completed => _completed;
        public int CurrentGeneration => CurrentGenerationCore;
        public float Speed => AnimatorSpeedCore;
        public float CurrentCycleDuration =>
            CurrentClip == null || CurrentClip.speed <= 0f ||
            CurrentClip.animationSprites == null
                ? 0f
                : CurrentClip.animationSprites.Count / CurrentClip.speed;

        public void ApplyProfile(EnemyAnimationProfileScriptable profile)
        {
            if (profile == null)
                return;
            animator.runtimeAnimatorController = profile.AnimatorController;
            _profileVisualOffset = profile.VisualOffset;
            ApplyVisualOffset();
            transform.localScale = profile.VisualScale;
        }

        public void SetRuntimeVisualOffset(Vector2 offset)
        {
            _runtimeVisualOffset = offset;
            ApplyVisualOffset();
        }

        public int Play(EnemyAnimationData data, bool restart = false) =>
            PlayCore(data, restart);

        public void SetSpeed(float speed) =>
            SetAnimatorSpeedCore(Mathf.Max(0f, speed));

        public void AnimationEvent_AdvanceFrame() => AdvanceFrameCore();

        public void AnimationEvent_Marker(int marker)
        {
            if (TryRaiseMarker(out var animationId, out var generation))
                _markers.OnNext(new EnemyAnimationMarkerEvent(
                    animationId, generation, marker));
        }

        public void AnimationEvent_Complete()
        {
            if (TryRaiseCompletion(out var animationId, out var generation))
                _completed.OnNext(new EnemyAnimationCompletedEvent(
                    animationId, generation));
        }

        public void ResetRuntime()
        {
            ResetRuntimeCore();
            _runtimeVisualOffset = Vector2.zero;
            ApplyVisualOffset();
        }

        private void ApplyVisualOffset()
        {
            var offset = _profileVisualOffset + _runtimeVisualOffset;
            transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        }

        private void OnDestroy()
        {
            _markers.OnCompleted();
            _markers.Dispose();
            _completed.OnCompleted();
            _completed.Dispose();
        }
    }
}
