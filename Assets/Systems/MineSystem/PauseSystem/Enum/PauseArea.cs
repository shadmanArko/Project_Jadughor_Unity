namespace Systems.MineSystem.PauseSystem.Enum
{
    /// <summary>
    /// Which part of the game an <c>IPausable</c> belongs to. A pause request
    /// may target one area (freezing only its members) or no area at all
    /// (freezing everything), which is what lets the boss lair freeze the mine
    /// while modal UI still freezes the whole game.
    /// </summary>
    public enum PauseArea
    {
        /// <summary>
        /// Simulated in the mine view. The default, so anything that does not
        /// opt out is frozen while the player is away fighting a boss.
        /// </summary>
        MineView = 0,

        /// <summary>Lives inside the boss arena and runs during the fight.</summary>
        BossLair = 1,

        /// <summary>
        /// Belongs to no area. Reached only by an unscoped request, never by an
        /// area-scoped one - this means "no area", not "always paused". The
        /// player, the shared camera and the lair's own entry/exit driver live
        /// here: they must survive the mine freeze but still obey modal UI.
        /// </summary>
        Global = 2
    }
}
