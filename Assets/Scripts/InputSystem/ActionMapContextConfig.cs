using System;
using System.Collections.Generic;
using InputSystem.Data;
using UnityEngine;

namespace InputSystem.Config
{
    /// <summary>
    /// Data-driven configuration that declares which action maps are active for
    /// each ActionMapContextId and whether the context is additive.
    ///
    /// Create one instance: Assets/Config/Input/ActionMapContextConfig.asset
    ///
    /// Add a new entry whenever you add a new ActionMapContextId enum value.
    /// ActionMapService reads this asset; no code changes are required for new contexts.
    ///
    /// Stack resolution rules (ActionMapService.ComputeActiveMaps):
    ///   The stack is replayed from bottom to top.
    ///   - IsAdditive = false → clears all maps accumulated so far, then adds its own.
    ///   - IsAdditive = true  → adds its maps on top of whatever is already active.
    /// </summary>
    [CreateAssetMenu(fileName = "ActionMapContextConfig", menuName = "Config/Input/ActionMapContextConfig")]
    public sealed class ActionMapContextConfig : ScriptableObject
    {
        [SerializeField]
        private ActionMapContextDefinition[] _contexts = Array.Empty<ActionMapContextDefinition>();

        // Built lazily at first access so the SO can be loaded from Addressables
        // without forcing a dictionary allocation at asset-import time.
        private Dictionary<ActionMapContextId, ActionMapContextDefinition> _lookup;

        private void OnEnable() => _lookup = null; // Rebuild if the asset is hot-reloaded

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the definition for the given context, or null if none is configured.
        /// </summary>
        public ActionMapContextDefinition GetContext(ActionMapContextId id)
        {
            BuildLookup();
            return _lookup.TryGetValue(id, out var def) ? def : null;
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        private void BuildLookup()
        {
            if (_lookup != null) return;

            _lookup = new Dictionary<ActionMapContextId, ActionMapContextDefinition>(_contexts.Length);

            foreach (var def in _contexts)
            {
                if (_lookup.ContainsKey(def.ContextId))
                {
                    Debug.LogWarning(
                        $"[ActionMapContextConfig] Duplicate context definition for '{def.ContextId}'. " +
                        "Only the first entry will be used.", this);
                    continue;
                }

                _lookup[def.ContextId] = def;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Rebuild lookup so Inspector edits are reflected immediately in Play Mode.
            _lookup = null;
        }
#endif
    }
}
