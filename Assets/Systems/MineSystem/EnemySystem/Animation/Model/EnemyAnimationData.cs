using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Animation.Model
{
    [Serializable]
    public sealed class EnemyAnimationData
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
    }
}
