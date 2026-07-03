using System;
using System.Collections.Generic;
using Systems.MineSystem.MinePlayerSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model
{
    [Serializable]
    public sealed class AnimationProfile
    {
        public string id;
        public string name;
        public RuntimeAnimatorController animatorController;
        public Vector2 visualScale = Vector2.one;
        public Vector2 visualOffset;
        public PlayerFacingDirection defaultFacing =
            PlayerFacingDirection.Right;
        public List<AnimationDatabase> animationDatabase = new();

        public bool TryGet(
            string animationId,
            out AnimationData data)
        {
            foreach (var database in animationDatabase)
            {
                if (database != null &&
                    database.TryGet(animationId, out data))
                    return true;
            }

            data = null;
            return false;
        }
    }
}
