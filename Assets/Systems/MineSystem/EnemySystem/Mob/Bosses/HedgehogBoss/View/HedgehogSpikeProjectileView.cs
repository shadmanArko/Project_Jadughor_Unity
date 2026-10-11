using System;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View
{
    /// <summary>Physics and presentation surface for one pooled Hedgehog spike.</summary>
    public sealed class HedgehogSpikeProjectileView : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D triggerCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private readonly Subject<Collider2D> _triggerEntered = new();

        public Rigidbody2D Body => body;
        public IObservable<Collider2D> TriggerEntered => _triggerEntered;

        public bool ValidateReferences()
        {
            if (body != null && triggerCollider != null && triggerCollider.isTrigger &&
                spriteRenderer != null)
                return true;

            Debug.LogError(
                "HedgehogSpikeProjectileView requires a Rigidbody2D, trigger " +
                "Collider2D, and SpriteRenderer.", this);
            return false;
        }

        public void Launch(Vector2 position, Vector2 direction, float speed)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;
            body.position = position;
            body.rotation = Mathf.Atan2(
                normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            body.linearVelocity = normalizedDirection * speed;
            body.angularVelocity = 0f;
            body.simulated = true;
        }

        public void Stop()
        {
            if (body == null)
                return;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        public void ResetRuntime()
        {
            Stop();
            if (body != null)
            {
                body.simulated = false;
                body.position = Vector2.zero;
                body.rotation = 0f;
            }
            if (triggerCollider != null)
                triggerCollider.enabled = true;
            if (spriteRenderer != null)
                spriteRenderer.enabled = true;
        }

        private void OnTriggerEnter2D(Collider2D other) =>
            _triggerEntered.OnNext(other);

        private void OnDestroy()
        {
            _triggerEntered.OnCompleted();
            _triggerEntered.Dispose();
        }
    }
}
