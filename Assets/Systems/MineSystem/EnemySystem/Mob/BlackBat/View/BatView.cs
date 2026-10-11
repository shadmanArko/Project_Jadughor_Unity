using System;
using Systems.MineSystem.Damage;
using Systems.MineSystem.EnemySystem.Animation.Controller;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Config;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.View
{
    public sealed class BatView : MonoBehaviour, IDamageable
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D terrainCollider;
        [SerializeField] private Collider2D hurtboxCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private EnemyAnimationController animationController;

        private readonly Subject<float> _damageRequested = new();
        private bool _damageEnabled;

        public Rigidbody2D Body => body;
        public Collider2D TerrainCollider => terrainCollider;
        public Collider2D HurtboxCollider => hurtboxCollider;
        public bool DamageEnabled => _damageEnabled;
        public float AnimatorSpeed => animationController.Speed;
        public IObservable<float> DamageRequested => _damageRequested;
        public IObservable<EnemyAnimationMarkerEvent> AnimationMarkers =>
            animationController.Markers;
        public IObservable<EnemyAnimationCompletedEvent> AnimationCompleted =>
            animationController.Completed;

        public bool ValidateReferences()
        {
            if (body != null && terrainCollider != null &&
                hurtboxCollider != null && !terrainCollider.isTrigger &&
                hurtboxCollider.isTrigger && spriteRenderer != null &&
                animationController != null &&
                animationController.ValidateReferences())
                return true;

            Debug.LogError(
                "BatView requires a Rigidbody2D, non-trigger terrain collider, " +
                "trigger hurtbox, SpriteRenderer, and EnemyAnimationController.",
                this);
            return false;
        }

        public void ApplyConfig(BatConfigScriptable config)
        {
            animationController.ApplyProfile(config.AnimationProfile);
            spriteRenderer.color = config.BatColor;
            IgnoreSelfLayerForTerrainCollision();
        }

        public int Play(EnemyAnimationData animation, bool restart = false) =>
            animationController.Play(animation, restart);

        public void SetFacing(bool facesLeft) =>
            animationController.SetFacing(facesLeft);

        public void SetAnimatorSpeed(float speed) =>
            animationController.SetSpeed(speed);

        public void SetFlightVisualOffset(Vector2 offset) =>
            animationController.SetRuntimeVisualOffset(offset);

        public void ClearFlightVisualOffset() =>
            animationController.SetRuntimeVisualOffset(Vector2.zero);

        public void SetDamageEnabled(bool enabled) =>
            _damageEnabled = enabled;

        public void ApplyDamage(float amount)
        {
            if (_damageEnabled && amount > 0f)
                _damageRequested.OnNext(amount);
        }

        public void MovePosition(Vector2 position) => body.MovePosition(position);

        public void Stop()
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        public void Teleport(Vector2 position)
        {
            body.position = position;
            Stop();
        }

        public void ResetRuntime()
        {
            _damageEnabled = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = true;
            }
            if (terrainCollider != null)
                terrainCollider.enabled = true;
            if (hurtboxCollider != null)
                hurtboxCollider.enabled = true;
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
            if (animationController != null)
                animationController.ResetRuntime();
        }

        private void IgnoreSelfLayerForTerrainCollision()
        {
            if (terrainCollider == null)
                return;
            var excludedLayers = terrainCollider.excludeLayers;
            excludedLayers.value |= 1 << terrainCollider.gameObject.layer;
            terrainCollider.excludeLayers = excludedLayers;
        }

        private void OnDestroy()
        {
            _damageRequested.OnCompleted();
            _damageRequested.Dispose();
        }
    }
}
