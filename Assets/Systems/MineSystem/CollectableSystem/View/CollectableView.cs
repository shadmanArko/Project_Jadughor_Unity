using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.View
{
    public sealed class CollectableView : MonoBehaviour, ICollectable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D triggerCollider;
        [SerializeField] private Collider2D solidCollider;

        private readonly Subject<Collider2D> _triggerEntered = new();

        public Item Item { get; private set; }
        public Transform Transform => transform;
        public IObservable<Collider2D> TriggerEntered => _triggerEntered;

        public void Present(
            Item item,
            Vector3 position,
            Sprite sprite,
            float gravityScale)
        {
            Item = item;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            spriteRenderer.sprite = sprite;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = gravityScale;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            triggerCollider.enabled = true;
            if (solidCollider != null)
                solidCollider.enabled = true;
        }

        public void BeginPull()
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            if (solidCollider != null)
                solidCollider.enabled = false;
        }

        public void EndPull(float gravityScale)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = gravityScale;
            if (solidCollider != null)
                solidCollider.enabled = true;
        }

        public void SetPullPosition(Vector2 position)
        {
            body.position = position;
        }

        public void ResetView()
        {
            Item = null;
            spriteRenderer.sprite = null;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            triggerCollider.enabled = false;
            if (solidCollider != null)
                solidCollider.enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _triggerEntered.OnNext(other);
        }

        private void OnDestroy()
        {
            _triggerEntered.Dispose();
        }
    }
}
