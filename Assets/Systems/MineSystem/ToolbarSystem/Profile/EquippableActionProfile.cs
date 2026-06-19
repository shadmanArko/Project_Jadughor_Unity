using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    public abstract class EquippableActionProfile : ItemActionProfile
    {
        [Header("Targeting")]
        [Min(1)]
        [SerializeField] private int range = 1;

        [Header("Ground Animations")]
        [SerializeField] private DirectionalAnimationSet groundAnimations = new();

        [Header("Climb Animations")]
        [SerializeField] private DirectionalAnimationSet climbAnimations = new();

        public int Range => Mathf.Max(1, range);

        public string GetAnimationId(bool climbing, CardinalDirection direction)
        {
            return (climbing ? climbAnimations : groundAnimations).Get(direction);
        }
    }
}
