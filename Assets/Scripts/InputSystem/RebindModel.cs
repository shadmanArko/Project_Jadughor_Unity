using System;
using UniRx;
using UnityEngine.InputSystem;

namespace InputSystem.Model
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Interface
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Read-only view of the current rebinding state.
    /// The UI layer depends on this to show/hide the "Press any key…" overlay.
    /// </summary>
    public interface IRebindModel
    {
        /// <summary>True while an interactive rebind is waiting for player input.</summary>
        IReadOnlyReactiveProperty<bool>   IsRebinding           { get; }

        /// <summary>Name of the action currently being rebound, or empty if none.</summary>
        IReadOnlyReactiveProperty<string> RebindingActionName   { get; }

        /// <summary>Binding index within the action that is being rebound.</summary>
        IReadOnlyReactiveProperty<int>    RebindingBindingIndex { get; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Concrete Implementation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mutable rebinding state owned by RebindingController.
    /// Holds the live operation reference so it can be cancelled on demand.
    /// </summary>
    public sealed class RebindModel : IRebindModel, IDisposable
    {
        // ─── IRebindModel (read-only reactive properties) ─────────────────────

        public IReadOnlyReactiveProperty<bool>   IsRebinding           => _isRebinding;
        public IReadOnlyReactiveProperty<string> RebindingActionName   => _rebindingActionName;
        public IReadOnlyReactiveProperty<int>    RebindingBindingIndex => _rebindingBindingIndex;

        // ─── Internal Mutable State ───────────────────────────────────────────

        private readonly ReactiveProperty<bool>   _isRebinding           = new(false);
        private readonly ReactiveProperty<string> _rebindingActionName   = new(string.Empty);
        private readonly ReactiveProperty<int>    _rebindingBindingIndex = new(-1);

        /// <summary>
        /// The live operation. RebindingController stores it here so any call
        /// to CancelRebind() can cleanly dispose it.
        /// </summary>
        public InputActionRebindingExtensions.RebindingOperation ActiveOperation { get; private set; }

        // ─── Mutations (called only by RebindingController) ──────────────────

        public void BeginRebind(string actionName, int bindingIndex,
                                InputActionRebindingExtensions.RebindingOperation operation)
        {
            ActiveOperation          = operation;
            _isRebinding.Value       = true;
            _rebindingActionName.Value   = actionName;
            _rebindingBindingIndex.Value = bindingIndex;
        }

        public void EndRebind()
        {
            ActiveOperation              = null;
            _isRebinding.Value           = false;
            _rebindingActionName.Value   = string.Empty;
            _rebindingBindingIndex.Value = -1;
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            ActiveOperation?.Cancel();
            ActiveOperation?.Dispose();
            _isRebinding.Dispose();
            _rebindingActionName.Dispose();
            _rebindingBindingIndex.Dispose();
        }
    }
}
