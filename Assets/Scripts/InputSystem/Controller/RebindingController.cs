using System;
using Core.EventBus;
using InputSystem.Data;
using InputSystem.Events;
using InputSystem.Model;
using InputSystem.Service;
using InputSystem.Utility;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace InputSystem.Controller
{
    /// <summary>
    /// Orchestrates the full lifecycle of an interactive rebinding operation:
    ///
    ///   StartRebind()  → disables the action, starts PerformInteractiveRebinding,
    ///                    populates RebindModel so UI shows "Press any key…"
    ///   On complete    → applies the override, re-enables the action, saves, publishes
    ///                    RebindCompletedEvent, updates RebindModel.
    ///   On cancel      → re-enables the action, publishes RebindCancelledEvent,
    ///                    resets RebindModel.
    ///   CancelRebind() → abort the in-progress operation programmatically.
    ///   ResetAction()  → remove the override for one binding slot.
    ///   ResetAll()     → remove all overrides and clear PlayerPrefs.
    ///
    /// Binding group constraints per DeviceScheme:
    ///   KeyboardMouse → excludes Gamepad and Touchscreen controls.
    ///   Gamepad       → excludes Keyboard, Mouse, and Touchscreen controls.
    ///   Touch         → excludes Keyboard, Mouse, and Gamepad controls.
    /// </summary>
    public sealed class RebindingController : IDisposable
    {
        // ─── Dependencies ──────────────────────────────────────────────────────

        private readonly IInputSystemModel _inputModel;
        private readonly RebindModel       _rebindModel;
        private readonly InputSaveService  _saveService;
        private readonly EventBus          _eventBus;

        private readonly CompositeDisposable _disposables = new();

        // ─── Constructor ──────────────────────────────────────────────────────

        public RebindingController(IInputSystemModel inputModel,
                                   RebindModel       rebindModel,
                                   InputSaveService  saveService,
                                   EventBus          eventBus)
        {
            _inputModel  = inputModel;
            _rebindModel = rebindModel;
            _saveService = saveService;
            _eventBus    = eventBus;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Begin an interactive rebind for one binding slot.
        /// Safe to call multiple times — any in-progress rebind is cancelled first.
        /// </summary>
        /// <param name="actionMapName">Name from InputConstants.Maps.</param>
        /// <param name="actionName">Name from InputConstants.Actions.*.</param>
        /// <param name="bindingIndex">
        ///   The exact binding index to rebind (including composite parts).
        ///   Use GamepadIconService.GetBindingIndicesForScheme() to find the right index.
        /// </param>
        /// <param name="scheme">Which device family this tab is for.</param>
        public void StartRebind(string actionMapName, string actionName,
                                int bindingIndex, DeviceScheme scheme)
        {
            // Cancel any existing operation
            if (_rebindModel.IsRebinding.Value)
                CancelRebind();

            var action = _inputModel.GetAction(actionMapName, actionName);
            if (action == null)
            {
                Debug.LogError($"[RebindingController] Action not found: {actionMapName}/{actionName}");
                return;
            }

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                Debug.LogError($"[RebindingController] Invalid bindingIndex {bindingIndex} " +
                               $"for action '{actionName}' (count={action.bindings.Count}).");
                return;
            }

            // Disable the action while rebinding (required by the Input System)
            var wasEnabled = action.enabled;
            action.Disable();

            var operation = action.PerformInteractiveRebinding(bindingIndex);

            // ── Constraints based on which tab the player is on ───────────────
            ApplySchemeConstraints(operation, scheme);

            // ── Cancellation triggers ─────────────────────────────────────────
            operation
                .WithCancelingThrough(InputConstants.ControlPaths.EscapeKey)
                .WithCancelingThrough(InputConstants.ControlPaths.GamepadStart);

            // ── Callbacks ─────────────────────────────────────────────────────
            operation
                .OnComplete(op => OnRebindComplete(op, action, actionMapName, actionName,
                                                   bindingIndex, wasEnabled))
                .OnCancel(op  => OnRebindCancel(op, action, actionMapName, actionName,
                                                bindingIndex, wasEnabled));

            operation.Start();

            _rebindModel.BeginRebind(actionName, bindingIndex, operation);

            Debug.Log($"[RebindingController] Rebinding started: {actionMapName}/{actionName}[{bindingIndex}] ({scheme})");
        }

        /// <summary>
        /// Programmatically cancel the current rebind (e.g. player closes the rebinding panel).
        /// </summary>
        public void CancelRebind()
        {
            _rebindModel.ActiveOperation?.Cancel();
        }

        /// <summary>
        /// Remove the binding override for a single slot and save.
        /// </summary>
        public void ResetAction(string actionMapName, string actionName, int bindingIndex)
        {
            var action = _inputModel.GetAction(actionMapName, actionName);
            if (action == null) return;

            action.RemoveBindingOverride(bindingIndex);
            _saveService.SaveBindings();

            _eventBus.Publish(new RebindCompletedEvent(
                actionMapName, actionName, bindingIndex,
                action.bindings[bindingIndex].effectivePath));

            Debug.Log($"[RebindingController] Reset binding: {actionMapName}/{actionName}[{bindingIndex}]");
        }

        /// <summary>
        /// Remove all binding overrides across the entire asset and clear PlayerPrefs.
        /// </summary>
        public void ResetAll()
        {
            _saveService.ResetBindings();
            Debug.Log("[RebindingController] All bindings reset to defaults.");
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        private static void ApplySchemeConstraints(
            InputActionRebindingExtensions.RebindingOperation op, DeviceScheme scheme)
        {
            switch (scheme)
            {
                case DeviceScheme.KeyboardMouse:
                    op.WithControlsExcluding(InputConstants.ControlPaths.Gamepad)
                      .WithControlsExcluding(InputConstants.ControlPaths.Touchscreen);
                    break;

                case DeviceScheme.Gamepad:
                    op.WithControlsExcluding(InputConstants.ControlPaths.Keyboard)
                      .WithControlsExcluding(InputConstants.ControlPaths.Mouse)
                      .WithControlsExcluding(InputConstants.ControlPaths.Touchscreen);
                    break;

                case DeviceScheme.Touch:
                    op.WithControlsExcluding(InputConstants.ControlPaths.Keyboard)
                      .WithControlsExcluding(InputConstants.ControlPaths.Mouse)
                      .WithControlsExcluding(InputConstants.ControlPaths.Gamepad);
                    break;
            }
        }

        private void OnRebindComplete(
            InputActionRebindingExtensions.RebindingOperation op,
            InputAction action,
            string actionMapName, string actionName,
            int bindingIndex, bool wasEnabled)
        {
            op.Dispose();
            _rebindModel.EndRebind();

            if (wasEnabled) action.Enable();

            var newPath = action.bindings[bindingIndex].effectivePath;

            _saveService.SaveBindings();
            _eventBus.Publish(new RebindCompletedEvent(actionMapName, actionName, bindingIndex, newPath));

            Debug.Log($"[RebindingController] Rebind complete: {actionMapName}/{actionName}[{bindingIndex}] → '{newPath}'");
        }

        private void OnRebindCancel(
            InputActionRebindingExtensions.RebindingOperation op,
            InputAction action,
            string actionMapName, string actionName,
            int bindingIndex, bool wasEnabled)
        {
            op.Dispose();
            _rebindModel.EndRebind();

            if (wasEnabled) action.Enable();

            _eventBus.Publish(new RebindCancelledEvent(actionMapName, actionName, bindingIndex));

            Debug.Log($"[RebindingController] Rebind cancelled: {actionMapName}/{actionName}[{bindingIndex}]");
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_rebindModel.IsRebinding.Value)
                CancelRebind();

            _disposables.Dispose();
        }
    }
}
