using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Animation.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Animation.Scriptable
{
    [CreateAssetMenu(
        fileName = "EnemyAnimationProfile",
        menuName = "Enemy/Animation Profile")]
    public sealed class EnemyAnimationProfileScriptable : ScriptableObject
    {
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private Vector2 visualScale = Vector2.one;
        [SerializeField] private Vector2 visualOffset;
        [SerializeField] private List<EnemyAnimationData> animations = new();

        public RuntimeAnimatorController AnimatorController => animatorController;
        public Vector2 VisualScale => visualScale;
        public Vector2 VisualOffset => visualOffset;

        public bool TryGet(string animationId, out EnemyAnimationData data)
        {
            for (var i = 0; i < animations.Count; i++)
            {
                var candidate = animations[i];
                if (candidate != null && string.Equals(
                        candidate.id,
                        animationId,
                        StringComparison.Ordinal))
                {
                    data = candidate;
                    return true;
                }
            }
            data = null;
            return false;
        }
    }
}
