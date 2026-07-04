using System;
using Systems.MineSystem.Damage;
using Systems.MineSystem.EnemySystem.Animation.Controller;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Mob.Slime.Config;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.View
{
    public sealed class SlimeView : MonoBehaviour, IDamageable
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D terrainCollider;
        [SerializeField] private Collider2D hurtboxCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private EnemyAnimationController animationController;

        private readonly Subject<float> _damageRequested = new();
        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];
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
                "SlimeView requires a Rigidbody2D, non-trigger terrain collider, " +
                "trigger hurtbox, SpriteRenderer, and EnemyAnimationController.",
                this);
            return false;
        }

        public void ApplyConfig(SlimeConfigScriptable config)
        {
            animationController.ApplyProfile(config.AnimationProfile);
            spriteRenderer.color = config.SlimeColor;
        }

        public int Play(
            EnemyAnimationData animation,
            bool restart = false) =>
            animationController.Play(animation, restart);

        public void SetFacing(bool facesLeft) =>
            animationController.SetFacing(facesLeft);

        public void SetAnimatorSpeed(float speed) =>
            animationController.SetSpeed(speed);

        public void SetDamageEnabled(bool enabled) =>
            _damageEnabled = enabled;

        public void ApplyDamage(float amount)
        {
            if (_damageEnabled && amount > 0f)
                _damageRequested.OnNext(amount);
        }

        public void SetVelocity(Vector2 velocity) =>
            body.linearVelocity = velocity;

        public void Teleport(Vector2 position)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        public bool IsGrounded(LayerMask layerMask, float distance)
        {
            var bounds = terrainCollider.bounds;
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = layerMask,
                useTriggers = false
            };
            var count = Physics2D.BoxCast(
                new Vector2(bounds.center.x, bounds.min.y),
                new Vector2(Mathf.Max(0.01f, bounds.size.x * 0.8f), 0.02f),
                0f,
                Vector2.down,
                filter,
                _groundHits,
                Mathf.Max(0.01f, distance));
            return count > 0;
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
            animationController.ResetRuntime();
        }

        private void OnDestroy()
        {
            _damageRequested.OnCompleted();
            _damageRequested.Dispose();
        }
    }
}
