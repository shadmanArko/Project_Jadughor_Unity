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
    /// Unity surface of the hedgehog boss. Movement and animation are enough
    /// to drive the lair-entry cutscene; <see cref="IDamageable"/> is here
    /// because it is a boss the player can hit — health, phases and death
    /// presentation are owned by <c>HedgehogBossController</c>, not this
    /// view. Full attack behaviour still lands with a later combat pass.
    /// </summary>
    public sealed class HedgehogBossView : MonoBehaviour, IDamageable
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D hurtboxCollider;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private EnemyAnimationController animationController;

        private readonly Subject<float> _damageRequested = new();
        private bool _damageEnabled;

        public Rigidbody2D Body => body;
        public float AnimatorSpeed => animationController.Speed;
        public bool DamageEnabled => _damageEnabled;
        public IObservable<float> DamageRequested => _damageRequested;

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

        public void SetDamageEnabled(bool enabled) => _damageEnabled = enabled;

        public void ApplyDamage(float amount)
        {
            if (_damageEnabled && amount > 0f)
                _damageRequested.OnNext(amount);
        }

        public void ApplyConfig(HedgehogBossConfigScriptable config)
        {
            animationController.ApplyProfile(config.AnimationProfile);
            spriteRenderer.color = config.BossColor;
        }

        public int Play(EnemyAnimationData animation, bool restart = false) =>
            animationController.Play(animation, restart);

        public void SetFacing(bool facesLeft) =>
            animationController.SetFacing(facesLeft);

        public void SetAnimatorSpeed(float speed) =>
            animationController.SetSpeed(speed);

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
            Stop();
            if (hurtboxCollider != null)
                hurtboxCollider.enabled = true;
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
            if (animationController != null)
                animationController.ResetRuntime();
        }

        private void OnDestroy()
        {
            _damageRequested.OnCompleted();
            _damageRequested.Dispose();
        }
    }
}
