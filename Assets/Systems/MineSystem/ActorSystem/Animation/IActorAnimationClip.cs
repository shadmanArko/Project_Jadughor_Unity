using System.Collections.Generic;
using UnityEngine;

namespace Systems.MineSystem.ActorSystem.Animation
{
    /// <summary>
    /// Shared shape of one sprite-sheet animation clip, implemented by the
    /// player's <c>AnimationData</c> and the enemy system's
    /// <c>EnemyAnimationData</c> so <see cref="ActorAnimationControllerBase{TClip}"/>
    /// can drive either without knowing which concrete type it holds.
    /// </summary>
    public interface IActorAnimationClip
    {
        string Id { get; }
        IReadOnlyList<Sprite> AnimationSprites { get; }
        float Speed { get; }
        bool PlayOnlyOnce { get; }
        bool FlipX { get; }
        bool FlipY { get; }
        bool AllowFacingFlip { get; }
        bool IsReversed { get; }
        int AnimatorStateHash { get; }
        int AnimatorTriggerHash { get; }
    }
}
