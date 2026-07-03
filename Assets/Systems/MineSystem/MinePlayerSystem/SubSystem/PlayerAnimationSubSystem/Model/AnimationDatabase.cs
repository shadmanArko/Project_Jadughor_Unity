using System;
using System.Collections.Generic;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model
{
    [Serializable]
    public sealed class AnimationDatabase
    {
        public AnimationCategory category;
        public List<AnimationData> animationData = new();

        public bool TryGet(
            string animationId,
            out AnimationData data)
        {
            for (var i = 0; i < animationData.Count; i++)
            {
                if (animationData[i] != null &&
                    string.Equals(
                        animationData[i].id,
                        animationId,
                        StringComparison.Ordinal))
                {
                    data = animationData[i];
                    return true;
                }
            }

            data = null;
            return false;
        }
    }
}
