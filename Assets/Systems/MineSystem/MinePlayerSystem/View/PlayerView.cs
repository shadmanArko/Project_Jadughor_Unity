using System;
using System.Collections.Generic;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Controller;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.View
{
    public sealed class PlayerView : MonoBehaviour
    {
        private static PhysicsMaterial2D _frictionlessMaterial;

        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private Transform collectionPoint;
        [SerializeField] private PlayerAnimationController animationController;

        private readonly HashSet<Collider2D> _climbableOverlaps = new();
        private LayerMask _climbableLayerMask;

        public Rigidbody2D Body => body;
        public Collider2D PlayerCollider => playerCollider;
        public Transform CollectionPoint =>
            collectionPoint != null ? collectionPoint : transform;
        public PlayerAnimationController AnimationController =>
            animationController;
        public IObservable<PlayerAnimationMarkerEvent> AnimationMarkers =>
            animationController.MarkerRaised;
        public IObservable<PlayerAnimationCompletedEvent> AnimationCompleted =>
            animationController.Completed;
        public bool IsInsideClimbable => _climbableOverlaps.Count > 0;

        public void Configure(LayerMask climbableLayerMask)
        {
            _climbableLayerMask = climbableLayerMask;
            playerCollider.sharedMaterial = GetFrictionlessMaterial();

            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                gameObject.layer = playerLayer;
        }

        public bool ValidateReferences()
        {
            if (body != null &&
                playerCollider != null &&
                animationController != null)
                return true;

            Debug.LogError(
                "PlayerView requires a Rigidbody2D, main Collider2D, and " +
                "child PlayerAnimationController.",
                this);
            return false;
        }

        public void SetVelocity(Vector2 velocity)
        {
            body.linearVelocity = velocity;
        }

        public void SetGravityScale(float gravityScale)
        {
            body.gravityScale = gravityScale;
        }

        public void Teleport(Vector2 position)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }

        public void Stop()
        {
            body.linearVelocity = Vector2.zero;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsInLayerMask(other.gameObject.layer, _climbableLayerMask))
                _climbableOverlaps.Add(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            _climbableOverlaps.Remove(other);
        }

        private static bool IsInLayerMask(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }

        private static PhysicsMaterial2D GetFrictionlessMaterial()
        {
            if (_frictionlessMaterial != null)
                return _frictionlessMaterial;

            _frictionlessMaterial = new PhysicsMaterial2D(
                "Player Frictionless")
            {
                friction = 0f,
                bounciness = 0f,
                hideFlags = HideFlags.HideAndDontSave
            };
            return _frictionlessMaterial;
        }
    }
}
