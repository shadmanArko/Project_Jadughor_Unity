using System.Collections.Generic;
using Systems.MineSystem.MinePlayerSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Scriptable
{
    [CreateAssetMenu(fileName = "PlayerAnimationLibraryScriptable", menuName = "Scriptable/PlayerAnimationLibraryScriptable")]
    public sealed class PlayerAnimationLibraryScriptable : ScriptableObject
    {
        [SerializeField] private List<AnimationProfile> animationProfiles = new();

        public IReadOnlyList<AnimationProfile> AnimationProfiles =>
            animationProfiles;

        public bool TryGetProfile(
            CharacterType characterType,
            out AnimationProfile profile)
        {
            for (var i = 0; i < animationProfiles.Count; i++)
            {
                var candidate = animationProfiles[i];
                if (candidate != null &&
                    candidate.characterType == characterType)
                {
                    profile = candidate;
                    return true;
                }
            }

            profile = null;
            return false;
        }
    }
}
