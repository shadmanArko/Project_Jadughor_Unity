using Systems.MineSystem.EnemySystem.Animation.Controller;
using Systems.MineSystem.EnemySystem.Animation.Model;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View
{
    /// <summary>
    /// Unity surface of the hedgehog boss. Deliberately minimal — no
    /// <c>IDamageable</c>, no hurtbox — because this pass only needs the boss
    /// to be movable and animatable for the lair-entry cutscene; combat lands
    /// with the boss's behaviour pass.
    /// </summary>
    public sealed class HedgehogBossView : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private EnemyAnimationController animationController;

        public Rigidbody2D Body => body;
        public float AnimatorSpeed => animationController.Speed;

        public bool ValidateReferences()
        {
            if (body != null && spriteRenderer != null &&
                animationController != null &&
                animationController.ValidateReferences())
                return true;

            Debug.LogError(
                "HedgehogBossView requires a Rigidbody2D, SpriteRenderer, and " +
                "EnemyAnimationController.",
                this);
            return false;
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
            Stop();
            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
            if (animationController != null)
                animationController.ResetRuntime();
        }
    }
}
