using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Systems.MineSystem.ActorSystem.Interface
{
    /// <summary>
    /// A character a cutscene can move and animate without knowing its
    /// concrete type. Scoped to actors that actually participate in scripted
    /// sequences today (the player, the hedgehog boss) — mobs are not
    /// actors, since they move via grid pathfinding rather than tweening and
    /// have no cutscene role.
    /// </summary>
    public interface IActor
    {
        Vector2 Position { get; }

        UniTask MoveToAsync(
            Vector2 destination,
            float duration,
            Ease ease,
            CancellationToken cancellationToken);

        void PlayAnimation(string animationId, bool facesLeft);

        void ClearAnimation();
    }
}
