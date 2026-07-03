using System;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Controller
{
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        private readonly Subject<PlayerAnimationMarkerEvent> _markerRaised = new();
        private readonly Subject<PlayerAnimationCompletedEvent> _completed = new();

        private AnimationData _currentAnimation;
        private int _spriteIndex;
        private int _generation;
        private bool _completionRaised;
        private PlayerFacingDirection _facingDirection =
            PlayerFacingDirection.Right;

        public IObservable<PlayerAnimationMarkerEvent> MarkerRaised =>
            _markerRaised;
        public IObservable<PlayerAnimationCompletedEvent> Completed =>
            _completed;
        public string CurrentAnimationId =>
            _currentAnimation?.id ?? PlayerAnimationId.None;
        public int CurrentGeneration => _generation;
        public float AnimatorSpeed => animator != null ? animator.speed : 0f;

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
            bool restartCurrent = false)
        {
            if (animationData == null ||
                animationData.animationSprites == null ||
                animationData.animationSprites.Count == 0)
                return _generation;

            if (spriteRenderer == null)
            {
                Debug.LogError(
                    "PlayerAnimationController requires a SpriteRenderer.");
                return _generation;
            }

            if (_currentAnimation == animationData && !restartCurrent)
                return _generation;

            _currentAnimation = animationData;
            _spriteIndex = animationData.isReversed
                ? animationData.animationSprites.Count - 1
                : 0;
            _completionRaised = false;
            _generation++;
            PresentCurrentSprite();
            ApplyFlips();

            if (animator != null)
            {
                animator.speed = Mathf.Max(0f, animationData.speed);
                if (animationData.AnimatorTriggerHash != 0)
                    animator.SetTrigger(animationData.AnimatorTriggerHash);
                else if (animationData.AnimatorStateHash != 0)
                    animator.Play(animationData.AnimatorStateHash, 0, 0f);
            }

            return _generation;
        }

        public void SetFacing(PlayerFacingDirection direction)
        {
            _facingDirection = direction;
            ApplyFlips();
        }

        public void SetAnimatorSpeed(float speed)
        {
            if (animator != null)
                animator.speed = speed;
        }

        public void AnimationEvent_AdvanceFrame()
        {
            if (_currentAnimation?.animationSprites == null ||
                _currentAnimation.animationSprites.Count == 0)
                return;

            var lastIndex = _currentAnimation.animationSprites.Count - 1;
            if (_currentAnimation.isReversed)
            {
                if (_spriteIndex > 0)
                    _spriteIndex--;
                else if (!_currentAnimation.playOnlyOnce)
                    _spriteIndex = lastIndex;
            }
            else
            {
                if (_spriteIndex < lastIndex)
                    _spriteIndex++;
                else if (!_currentAnimation.playOnlyOnce)
                    _spriteIndex = 0;
            }

            PresentCurrentSprite();
        }

        public void AnimationEvent_Marker(int marker)
        {
            if (_currentAnimation == null)
                return;

            _markerRaised.OnNext(new PlayerAnimationMarkerEvent(
                _currentAnimation.id,
                _generation,
                marker));
        }

        public void AnimationEvent_Complete()
        {
            if (_currentAnimation == null || _completionRaised)
                return;

            _completionRaised = true;
            _completed.OnNext(new PlayerAnimationCompletedEvent(
                _currentAnimation.id,
                _generation));
        }

        private void PresentCurrentSprite()
        {
            spriteRenderer.sprite =
                _currentAnimation.animationSprites[_spriteIndex];
        }

        private void ApplyFlips()
        {
            if (spriteRenderer == null)
                return;

            var animationFlipX = _currentAnimation?.flipX ?? false;
            var facingFlip =
                _facingDirection == PlayerFacingDirection.Left &&
                (_currentAnimation?.allowFacingFlip ?? true);
            spriteRenderer.flipX = facingFlip ^ animationFlipX;
            spriteRenderer.flipY = _currentAnimation?.flipY ?? false;
        }

        private void OnDestroy()
        {
            _markerRaised.Dispose();
            _completed.Dispose();
        }
    }
}
