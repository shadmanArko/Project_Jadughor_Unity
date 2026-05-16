using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InputSystem.Utility
{
    /// <summary>
    /// Extension methods that bridge Unity's InputAction callback events
    /// to UniRx IObservable streams.
    ///
    /// Usage example (in a PlayerMovementController):
    /// <code>
    ///   _inputModel.GetAction(Maps.MuseumScene, Actions.MuseumScene.Move)
    ///       .PerformedAsObservable()
    ///       .Select(ctx => ctx.ReadValue&lt;Vector2&gt;())
    ///       .Subscribe(v => _moveDirection = v)
    ///       .AddTo(_disposables);
    /// </code>
    /// </summary>
    public static class InputActionExtensions
    {
        // ─── Per-phase observables ────────────────────────────────────────────

        /// <summary>
        /// Emits every time the action transitions into the Performed phase.
        /// For buttons: fires once per press. For axes: fires on every value change.
        /// </summary>
        public static IObservable<InputAction.CallbackContext> PerformedAsObservable(this InputAction action)
        {
            return Observable.FromEvent<InputAction.CallbackContext>(
                h => action.performed += h,
                h => action.performed -= h);
        }

        /// <summary>Emits when the action first becomes active (initial press / threshold cross).</summary>
        public static IObservable<InputAction.CallbackContext> StartedAsObservable(this InputAction action)
        {
            return Observable.FromEvent<InputAction.CallbackContext>(
                h => action.started += h,
                h => action.started -= h);
        }

        /// <summary>Emits when the action is released / deactivated.</summary>
        public static IObservable<InputAction.CallbackContext> CanceledAsObservable(this InputAction action)
        {
            return Observable.FromEvent<InputAction.CallbackContext>(
                h => action.canceled += h,
                h => action.canceled -= h);
        }

        // ─── Typed value helpers ──────────────────────────────────────────────

        /// <summary>Shortcut that reads a Vector2 value every time the action is performed.</summary>
        public static IObservable<Vector2> AsVector2Observable(this InputAction action)
        {
            return action.PerformedAsObservable()
                         .Select(ctx => ctx.ReadValue<Vector2>());
        }

        /// <summary>Shortcut that reads a float value every time the action is performed.</summary>
        public static IObservable<float> AsFloatObservable(this InputAction action)
        {
            return action.PerformedAsObservable()
                         .Select(ctx => ctx.ReadValue<float>());
        }

        /// <summary>
        /// Emits true on started and false on canceled — useful for hold-to-sprint style inputs.
        /// </summary>
        public static IObservable<bool> AsHoldObservable(this InputAction action)
        {
            var started  = action.StartedAsObservable().Select(_ => true);
            var canceled = action.CanceledAsObservable().Select(_ => false);
            return started.Merge(canceled);
        }

        /// <summary>
        /// Emits Unit on every performed event — useful for one-shot button actions
        /// where you only care that it happened, not the value.
        /// </summary>
        public static IObservable<Unit> AsButtonObservable(this InputAction action)
        {
            return action.PerformedAsObservable().AsUnitObservable();
        }
    }
}
