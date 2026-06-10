using System;

namespace Systems.MineSystem.MinePlayerSystem.Model
{
    public enum PlayerLocomotionState
    {
        Idle,
        Moving,
        Climbing,
        Falling
    }

    public enum PlayerActionState
    {
        None,
        PrimaryAction,
        Interacting
    }

    public enum PlayerLifeState
    {
        Alive,
        Dead
    }

    public enum PlayerFacingDirection
    {
        Left,
        Right
    }

    [Flags]
    public enum PlayerRestrictionFlags
    {
        None = 0,
        Movement = 1 << 0,
        Climbing = 1 << 1,
        Action = 1 << 2
    }
}
