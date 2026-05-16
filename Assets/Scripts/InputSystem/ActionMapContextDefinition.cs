using System;
using UnityEngine;

namespace InputSystem.Data
{
    /// <summary>
    /// Defines which action maps are enabled when a given ActionMapContextId is on the stack,
    /// and whether this context layers on top of the previous one (additive) or replaces it.
    ///
    /// Configured in: ActionMapContextConfig ScriptableObject.
    ///
    /// Examples:
    ///   MuseumScene  → IsAdditive=false, Maps=["MuseumScene","UI"]
    ///   Dialogue     → IsAdditive=true,  Maps=["Dialogue"]           (Museum maps stay active)
    ///   PauseMenu    → IsAdditive=false, Maps=["UI"]                  (everything else disabled)
    /// </summary>
    [Serializable]
    public sealed class ActionMapContextDefinition
    {
        [Tooltip("The context identifier this definition applies to.")]
        public ActionMapContextId ContextId;

        [Tooltip("Names of action maps to enable. Must match map names in the .inputactions asset.")]
        public string[] EnabledActionMapNames = Array.Empty<string>();

        [Tooltip(
            "If true, the maps from contexts below this one in the stack remain active. " +
            "If false, all previous maps are deactivated and only this context's maps run.")]
        public bool IsAdditive;
    }
}
