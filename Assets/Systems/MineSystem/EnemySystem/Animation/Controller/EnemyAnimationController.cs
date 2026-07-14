using System;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Animation.Scriptable;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Animation.Controller
{
    public sealed class EnemyAnimationController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        private readonly Subject<EnemyAnimationMarkerEvent> _markers = new();
        private readonly Subject<EnemyAnimationCompletedEvent> _completed = new();
        private EnemyAnimationData _current;
        private int _spriteIndex;
        private int _generation;
        private bool _completionRaised;
        private bool _facesLeft;

        public IObservable<EnemyAnimationMarkerEvent> Markers => _markers;
        public IObservable<EnemyAnimationCompletedEvent> Completed => _completed;
        public int CurrentGeneration => _generation;
        public float Speed => animator != null ? animator.speed : 0f;
        public float CurrentCycleDuration =>
            _current == null || _current.speed <= 0f ||
            _current.animationSprites == null
                ? 0f
                : _current.animationSprites.Count / _current.speed;

        public bool ValidateReferences() =>
            spriteRenderer != null && animator != null;

        public void ApplyProfile(EnemyAnimationProfileScriptable profile)
        {
            if (profile == null)
                return;
            animator.runtimeAnimatorController = profile.AnimatorController;
            transform.localPosition = profile.VisualOffset;
            transform.localScale = profile.VisualScale;
        }

        public int Play(EnemyAnimationData data, bool restart = false)
        {
            if (data == null || (_current == data && !restart))
                return _generation;
            _current = data;
            _spriteIndex = 0;
            _completionRaised = false;
            _generation++;
            PresentSprite();
            ApplyFlip();
            animator.speed = Mathf.Max(0f, data.speed);
            if (data.AnimatorTriggerHash != 0)
                animator.SetTrigger(data.AnimatorTriggerHash);
            else if (data.AnimatorStateHash != 0)
                animator.Play(data.AnimatorStateHash, 0, 0f);
            return _generation;
        }

        public void SetFacing(bool facesLeft)
        {
            _facesLeft = facesLeft;
            ApplyFlip();
        }

        public void SetSpeed(float speed)
        {
            if (animator != null)
                animator.speed = Mathf.Max(0f, speed);
        }

        public void AnimationEvent_AdvanceFrame()
        {
            if (_current?.animationSprites == null ||
                _current.animationSprites.Count == 0)
                return;
            var last = _current.animationSprites.Count - 1;
            if (_spriteIndex < last)
                _spriteIndex++;
            else if (!_current.playOnlyOnce)
                _spriteIndex = 0;
            PresentSprite();
        }

        public void AnimationEvent_Marker(int marker)
        {
            if (_current != null)
            {
                _markers.OnNext(new EnemyAnimationMarkerEvent(
                    _current.id,
                    _generation,
                    marker));
            }
        }

        public void AnimationEvent_Complete()
        {
            if (_current == null || _completionRaised)
                return;
            _completionRaised = true;
            _completed.OnNext(new EnemyAnimationCompletedEvent(
                _current.id,
                _generation));
        }

        public void ResetRuntime()
        {
            _current = null;
            _spriteIndex = 0;
            _completionRaised = false;
            _facesLeft = false;
            if (animator != null)
            {
                animator.speed = 1f;
                animator.Rebind();
            }
            ApplyFlip();
        }

        private void PresentSprite()
        {
            if (spriteRenderer != null && _current?.animationSprites != null &&
                _current.animationSprites.Count > 0)
                spriteRenderer.sprite = _current.animationSprites[_spriteIndex];
        }

        private void ApplyFlip()
        {
            if (spriteRenderer == null)
                return;
            var animationFlip = _current?.flipX ?? false;
            var facingFlip = _facesLeft && (_current?.allowFacingFlip ?? true);
            spriteRenderer.flipX = animationFlip ^ facingFlip;
            spriteRenderer.flipY = _current?.flipY ?? false;
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
