using System;

namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum
{
    [Serializable]
    public enum AnimationCategory
    {
        Idle,
        Move,
        Climb,
        Fall,
        Action,
        Hurt,
        Interaction,
        Death
    }
}