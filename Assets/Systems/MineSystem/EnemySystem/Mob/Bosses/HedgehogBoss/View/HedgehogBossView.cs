using System;
using Systems.MineSystem.Damage;
using Systems.MineSystem.EnemySystem.Animation.Controller;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View
{
    /// <summary>
    /// Unity surface of the hedgehog boss. It owns the Rigidbody, terrain
    /// collider, hurtbox, visual facing, and animation presentation; health
    /// and combat decisions remain in the state machine.
    /// </summary>
    public sealed class HedgehogBossView : MonoBehaviour, IDamageable
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D hurtboxCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private EnemyAnimationController animationController;

        private readonly Subject<float> _damageRequested = new();
        private readonly Subject<Collider2D> _collisionEntered = new();
        private bool _damageEnabled;

        public Rigidbody2D Body => body;
        public float AnimatorSpeed => animationController.Speed;
        public bool DamageEnabled => _damageEnabled;
        public IObservable<float> DamageRequested => _damageRequested;
        public IObservable<EnemyAnimationMarkerEvent> AnimationMarkers =>
            animationController.Markers;
        public IObservable<EnemyAnimationCompletedEvent> AnimationCompleted =>
            animationController.Completed;
        public IObservable<Collider2D> CollisionEntered => _collisionEntered;

        public bool ValidateReferences()
        {
            if (body != null && hurtboxCollider != null &&
                hurtboxCollider.isTrigger && spriteRenderer != null &&
                animationController != null &&
                animationController.ValidateReferences())
                return true;

            Debug.LogError(
                "HedgehogBossView requires a Rigidbody2D, trigger hurtbox " +
                "Collider2D, SpriteRenderer, and EnemyAnimationController.",
                this);
            return false;
        }

        public void SetDamageEnabled(bool enabled)
        {
            _damageEnabled = enabled;
            if (hurtboxCollider != null)
                hurtboxCollider.enabled = enabled;
        }

        public void ApplyDamage(float amount)
        {
            if (_damageEnabled && amount > 0f)
                _damageRequested.OnNext(amount);
        }

        public void ApplyConfig(HedgehogBossConfigScriptable config)
        {
            animationController.ApplyProfile(config.AnimationProfile);
            ValidateConfiguredAnimationStates(config);
            spriteRenderer.color = config.BossColor;
        }

        public int Play(EnemyAnimationData animation, bool restart = false) =>
            animationController.Play(animation, restart);

        /// <summary>
        /// The Hedgehog source sprites face left when unflipped, opposite to
        /// the shared actor-animation convention. Keep the public contract in
        /// world terms and invert only at this presentation boundary.
        /// </summary>
        public void SetFacing(bool facesLeft) =>
            animationController.SetFacing(!facesLeft);

        public void SetAnimatorSpeed(float speed) =>
            animationController.SetSpeed(speed);

        public void Stop()
        {
            if (body == null)
                return;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        public void SetHorizontalVelocity(float speed)
        {
            if (body == null)
                return;
            var velocity = body.linearVelocity;
            velocity.x = speed;
            body.linearVelocity = velocity;
        }

        public void StopHorizontalMotion() => SetHorizontalVelocity(0f);

        public void SetPhysicsSimulation(bool simulated)
        {
            if (body == null)
                return;
            if (!simulated)
                Stop();
            body.simulated = simulated;
        }

        /// <summary>
        /// Tests the authored arena collision layers directly ahead of the
        /// body. The combat controller uses this only during a rolling charge;
        /// ordinary movement is intentionally simple arena-world movement.
        /// </summary>
        public bool HasObstacleAhead(
            Vector2 direction,
            float distance,
            LayerMask obstacleMask)
        {
            if (direction.sqrMagnitude <= 0.0001f || distance <= 0f ||
                obstacleMask.value == 0)
                return false;

            var hit = Physics2D.Raycast(
                body.position,
                direction.normalized,
                distance,
                obstacleMask);
            return hit.collider != null && hit.collider != hurtboxCollider;
        }

        public void Teleport(Vector2 position)
        {
            body.position = position;
            Stop();
        }

        public void ResetRuntime()
        {
            _damageEnabled = false;
            Stop();
            if (body != null)
                body.simulated = true;
            if (hurtboxCollider != null)
                hurtboxCollider.enabled = false;
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
            if (animationController != null)
                animationController.ResetRuntime();
        }

        private void ValidateConfiguredAnimationStates(
            HedgehogBossConfigScriptable config)
        {
            foreach (var animationId in
                     HedgehogBossConfigScriptable.RequiredAnimationIds)
            {
                if (!config.AnimationProfile.TryGet(animationId, out var animation) ||
                    !animationController.HasState(animation))
                {
                    throw new InvalidOperationException(
                        $"{name} requires animator state '{animationId}' " +
                        "from its Hedgehog animation profile.");
                }
            }
        }

        private void OnDestroy()
        {
            _damageRequested.OnCompleted();
            _damageRequested.Dispose();
            _collisionEntered.OnCompleted();
            _collisionEntered.Dispose();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision != null && collision.collider != null)
                _collisionEntered.OnNext(collision.collider);
        }
    }
}
