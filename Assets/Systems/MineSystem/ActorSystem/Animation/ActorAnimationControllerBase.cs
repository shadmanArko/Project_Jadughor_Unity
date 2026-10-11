using UnityEngine;

namespace Systems.MineSystem.ActorSystem.Animation
{
    /// <summary>
    /// Sprite-sheet animation driving shared by the player and enemy animation
    /// controllers: a raw <see cref="Animator"/> is used only to kick off the
    /// clip that carries per-frame Animation Events, while the actual frames are
    /// stepped manually on a <see cref="SpriteRenderer"/>.
    /// </summary>
    /// <remarks>
    /// Subclasses keep their own public method names/signatures and their own
    /// differently-typed/named completion events (real, distinct subscribers
    /// depend on those exact shapes) — this base only holds the logic that was
    /// byte-for-byte duplicated between them.
    /// </remarks>
    public abstract class ActorAnimationControllerBase<TClip> : MonoBehaviour
        where TClip : class, IActorAnimationClip
    {
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Animator animator;

        private TClip _current;
        private int _spriteIndex;
        private int _generation;
        private bool _completionRaised;
        private bool _facesLeft;

        protected int CurrentGenerationCore => _generation;
        protected TClip CurrentClip => _current;
        protected float AnimatorSpeedCore => animator != null ? animator.speed : 0f;

        public bool ValidateReferences() =>
            spriteRenderer != null && animator != null;

        protected void SetAnimatorSpeedCore(float speed)
        {
            if (animator != null)
                animator.speed = speed;
        }

        protected int PlayCore(TClip data, bool restart)
        {
            if (data?.AnimationSprites == null || data.AnimationSprites.Count == 0)
                return _generation;
            if (spriteRenderer == null)
            {
                Debug.LogError(
                    $"{GetType().Name} requires a SpriteRenderer.", this);
                return _generation;
            }
            if (ReferenceEquals(_current, data) && !restart)
                return _generation;

            _current = data;
            _spriteIndex = data.IsReversed ? data.AnimationSprites.Count - 1 : 0;
            _completionRaised = false;
            _generation++;
            PresentSprite();
            ApplyFlip();

            if (animator != null)
            {
                animator.speed = Mathf.Max(0f, data.Speed);
                if (data.AnimatorTriggerHash != 0)
                    animator.SetTrigger(data.AnimatorTriggerHash);
                else if (data.AnimatorStateHash != 0)
                    animator.Play(data.AnimatorStateHash, 0, 0f);
            }

            return _generation;
        }

        public void SetFacing(bool facesLeft)
        {
            _facesLeft = facesLeft;
            ApplyFlip();
        }

        protected void AdvanceFrameCore()
        {
            if (_current?.AnimationSprites == null ||
                _current.AnimationSprites.Count == 0)
                return;

            var lastIndex = _current.AnimationSprites.Count - 1;
            if (_current.IsReversed)
            {
                if (_spriteIndex > 0)
                    _spriteIndex--;
                else if (!_current.PlayOnlyOnce)
                    _spriteIndex = lastIndex;
            }
            else
            {
                if (_spriteIndex < lastIndex)
                    _spriteIndex++;
                else if (!_current.PlayOnlyOnce)
                    _spriteIndex = 0;
            }

            PresentSprite();
        }

        protected bool TryRaiseMarker(out string animationId, out int generation)
        {
            animationId = _current?.Id;
            generation = _generation;
            return _current != null;
        }

        protected bool TryRaiseCompletion(out string animationId, out int generation)
        {
            animationId = _current?.Id;
            generation = _generation;
            if (_current == null || _completionRaised)
                return false;
            _completionRaised = true;
            return true;
        }

        protected virtual void ResetRuntimeCore()
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
            if (spriteRenderer != null && _current?.AnimationSprites != null &&
                _current.AnimationSprites.Count > 0)
                spriteRenderer.sprite = _current.AnimationSprites[_spriteIndex];
        }

        private void ApplyFlip()
        {
            if (spriteRenderer == null)
                return;
            var animationFlip = _current?.FlipX ?? false;
            var facingFlip = _facesLeft && (_current?.AllowFacingFlip ?? true);
            spriteRenderer.flipX = animationFlip ^ facingFlip;
            spriteRenderer.flipY = _current?.FlipY ?? false;
        }
    }
}
