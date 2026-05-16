using InputSystem.Data;
using InputSystem.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace InputSystem.Service
{
    /// <summary>
    /// Persists InputAction binding overrides using PlayerPrefs.
    /// The entire override set is stored as a single JSON string produced by
    /// Unity's built-in InputActionRebindingExtensions serialisation API,
    /// so any future additions to the .inputactions asset are handled automatically.
    ///
    /// Also persists the player's preferred MobileInputMode.
    ///
    /// Called by:
    ///   - InputSystemController.Initialize()  → LoadAll() on startup.
    ///   - RebindingController                 → SaveBindings() after each successful rebind.
    ///   - RebindingController                 → ResetBindings() when the player hits "Reset All".
    /// </summary>
    public sealed class InputSaveService : IInitializable
    {
        private readonly IInputSystemModel _inputModel;

        public InputSaveService(IInputSystemModel inputModel)
        {
            _inputModel = inputModel;
        }

        // ─── IInitializable ───────────────────────────────────────────────────

        public void Initialize()
        {
            LoadAll();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Serialise all current binding overrides on the InputActionAsset and save to PlayerPrefs.
        /// Call this after every successful interactive rebind.
        /// </summary>
        public void SaveBindings()
        {
            var json = _inputModel.Actions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(InputConstants.PrefsKeys.BindingOverrides, json);
            PlayerPrefs.Save();
            Debug.Log("[InputSaveService] Binding overrides saved.");
        }

        /// <summary>
        /// Load saved binding overrides from PlayerPrefs and apply them to the asset.
        /// No-op if no saved data exists.
        /// </summary>
        public void LoadBindings()
        {
            if (!PlayerPrefs.HasKey(InputConstants.PrefsKeys.BindingOverrides)) return;

            var json = PlayerPrefs.GetString(InputConstants.PrefsKeys.BindingOverrides);
            if (string.IsNullOrEmpty(json)) return;

            _inputModel.Actions.LoadBindingOverridesFromJson(json);
            Debug.Log("[InputSaveService] Binding overrides loaded.");
        }

        /// <summary>
        /// Remove all binding overrides from the asset AND delete the PlayerPrefs entry.
        /// </summary>
        public void ResetBindings()
        {
            _inputModel.Actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(InputConstants.PrefsKeys.BindingOverrides);
            PlayerPrefs.Save();
            Debug.Log("[InputSaveService] All binding overrides reset to defaults.");
        }

        /// <summary>
        /// Save a single action map's overrides is intentionally not exposed here —
        /// Unity's SaveBindingOverridesAsJson covers the entire asset atomically,
        /// which is simpler and prevents partial-save corruption.
        /// </summary>

        // ─── Mobile Mode Persistence ──────────────────────────────────────────

        public void SaveMobileInputMode(MobileInputMode mode)
        {
            PlayerPrefs.SetInt(InputConstants.PrefsKeys.MobileInputMode, (int)mode);
            PlayerPrefs.Save();
        }

        public MobileInputMode LoadMobileInputMode(MobileInputMode fallback = MobileInputMode.VirtualControls)
        {
            if (!PlayerPrefs.HasKey(InputConstants.PrefsKeys.MobileInputMode))
                return fallback;

            return (MobileInputMode)PlayerPrefs.GetInt(InputConstants.PrefsKeys.MobileInputMode);
        }

        // ─── Private ──────────────────────────────────────────────────────────

        private void LoadAll()
        {
            LoadBindings();
            // Mobile mode is loaded by InputSystemController, which passes it to the model.
        }
    }
}
