using System;
using System.Collections.Generic;
using Systems.MineSystem.ActorSystem.Animation;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Animation.Model
{
    [Serializable]
    public sealed class EnemyAnimationData : IActorAnimationClip
    {
        public string id;
        public string stateName;
        public string triggerName;
        public List<Sprite> animationSprites = new();
        [Min(0f)] public float speed = 1f;
        public bool playOnlyOnce;
        public bool flipX;
        public bool flipY;
        public bool allowFacingFlip = true;

        public int AnimatorStateHash => string.IsNullOrEmpty(stateName)
            ? 0
            : Animator.StringToHash(stateName);
        public int AnimatorTriggerHash => string.IsNullOrEmpty(triggerName)
            ? 0
            : Animator.StringToHash(triggerName);

        string IActorAnimationClip.Id => id;
        IReadOnlyList<Sprite> IActorAnimationClip.AnimationSprites => animationSprites;
        float IActorAnimationClip.Speed => speed;
        bool IActorAnimationClip.PlayOnlyOnce => playOnlyOnce;
        bool IActorAnimationClip.FlipX => flipX;
        bool IActorAnimationClip.FlipY => flipY;
        bool IActorAnimationClip.AllowFacingFlip => allowFacingFlip;
        bool IActorAnimationClip.IsReversed => false;
    }
}
