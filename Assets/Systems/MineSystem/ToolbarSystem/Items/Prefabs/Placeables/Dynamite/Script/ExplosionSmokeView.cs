using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public sealed class ExplosionSmokeView : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        public async UniTask PlayAsync(
            Vector3 worldPosition,
            DynamiteConfig config,
            Action impact,
            CancellationToken cancellationToken)
        {
            transform.position = worldPosition;
            animator.Play(
                Animator.StringToHash(config.ExplosionState),
                0,
                0f);
            animator.Update(0f);

            var state = animator.GetCurrentAnimatorStateInfo(0);
            var speed = Mathf.Max(
                0.0001f,
                Mathf.Abs(state.speed * state.speedMultiplier));
            var duration = state.length > 0f
                ? state.length / speed
                : config.FallbackAnimationDuration;
            var impactDelay =
                duration * config.NormalizedImpactTime;

            if (impactDelay > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(impactDelay),
                    cancellationToken: cancellationToken);
            }

            impact?.Invoke();

            var remaining = duration - impactDelay;
            if (remaining > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(remaining),
                    cancellationToken: cancellationToken);
            }
        }

        public void ResetView()
        {
            if (animator != null)
                animator.Rebind();

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
