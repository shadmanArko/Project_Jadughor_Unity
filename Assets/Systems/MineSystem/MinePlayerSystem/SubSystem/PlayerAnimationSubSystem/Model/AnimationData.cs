using System;
using System.Collections.Generic;
using Systems.MineSystem.ActorSystem.Animation;
using Systems.MineSystem.MinePlayerSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model
{
    [Serializable]
    public sealed class AnimationData : IActorAnimationClip
    {
        public string id;
        public string animationName;
        public string stateName;
        public string triggerName;
        public string folderPath;
        public List<Sprite> animationSprites = new();

        [Min(0f)] public float speed = 1f;
        [Min(0f)] public float frameRate = 12f;
        public bool playOnlyOnce;
        public bool flipX;
        public bool flipY;
        public bool isReversed;

        public bool interruptible = true;
        public bool allowFacingFlip = true;
        public PlayerRestrictionFlags restrictions;

        public int AnimatorStateHash =>
            string.IsNullOrEmpty(stateName)
                ? 0
                : Animator.StringToHash(stateName);

        public int AnimatorTriggerHash =>
            string.IsNullOrEmpty(triggerName)
                ? 0
                : Animator.StringToHash(triggerName);

        string IActorAnimationClip.Id => id;
        IReadOnlyList<Sprite> IActorAnimationClip.AnimationSprites => animationSprites;
        float IActorAnimationClip.Speed => speed;
        bool IActorAnimationClip.PlayOnlyOnce => playOnlyOnce;
        bool IActorAnimationClip.FlipX => flipX;
        bool IActorAnimationClip.FlipY => flipY;
        bool IActorAnimationClip.AllowFacingFlip => allowFacingFlip;
        bool IActorAnimationClip.IsReversed => isReversed;
    }
}
