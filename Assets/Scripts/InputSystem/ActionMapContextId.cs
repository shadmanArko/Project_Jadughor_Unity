namespace InputSystem.Data
{
    /// <summary>
    /// Every distinct gameplay context that can activate or deactivate action maps.
    /// Extend this enum whenever a new scene or UI state is added.
    ///
    /// ActionMapService maintains a stack of these contexts.
    /// Each context maps to an ActionMapContextDefinition in ActionMapContextConfig,
    /// which declares which action maps should be enabled and whether the context
    /// is additive (layers on top of the previous context) or exclusive (replaces all).
    /// </summary>
    public enum ActionMapContextId
    {
        None = 0,
        MainMenu,
        MuseumScene,
        MineScene,
        Dialogue,
        PauseMenu,
        Inventory,
        RebindingScreen,
        Cutscene
        // Add new contexts here. Then add a matching definition in ActionMapContextConfig.
    }
}
