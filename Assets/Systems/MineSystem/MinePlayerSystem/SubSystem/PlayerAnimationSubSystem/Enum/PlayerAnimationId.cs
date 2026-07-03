namespace Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum
{
    public static class PlayerAnimationId
    {
        public const string None = "";
        public const string IdleLeft = "locomotion.idle.left";
        public const string IdleRight = "locomotion.idle.right";
        public const string Move = "locomotion.move";
        public const string ClimbIdle = "climb.idle";
        public const string ClimbVertical = "climb.vertical";
        public const string ClimbHorizontal = "climb.horizontal";
        public const string Fall = "locomotion.fall";
        public const string PrimaryAction = "action.primary";
        public const string Interact = "interaction.default";
        public const string Hurt = "hurt.damage";
        public const string Death = "life.death";
    }
}
