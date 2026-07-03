using System;
using System.Collections.Generic;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Scriptable
{
    [CreateAssetMenu(fileName = "PlayerAnimationLibraryScriptable", menuName = "Scriptable/PlayerAnimationLibraryScriptable")]
    public sealed class PlayerAnimationLibraryScriptable : ScriptableObject
    {
        [SerializeField] private List<AnimationProfile> animationProfiles = new();

        public IReadOnlyList<AnimationProfile> AnimationProfiles =>
            animationProfiles;

        public bool TryGetProfile(
            string profileId,
            out AnimationProfile profile)
        {
            for (var i = 0; i < animationProfiles.Count; i++)
            {
                var candidate = animationProfiles[i];
                if (candidate != null &&
                    string.Equals(
                        candidate.id,
                        profileId,
                        StringComparison.Ordinal))
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
