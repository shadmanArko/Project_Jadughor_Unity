using System;
using Core.EventBus;
using InputSystem.Data;
using InputSystem.Model;
using InputSystem.Service;
using UniRx;
using UnityEngine;
using Zenject;

namespace InputSystem.Controller
{
    /// <summary>
    /// Top-level controller that wires together the input subsystems on startup.
    ///
    /// Responsibilities:
    ///   - Load saved preferences (bindings, mobile mode) via InputSaveService.
    ///   - Apply the saved MobileInputMode to the model.
    ///   - Monitor MobileInputMode changes and persist them.
    ///   - Provide the façade method SetMobileInputMode() used by the Settings UI.
    ///
    /// This is intentionally lightweight — all domain logic lives in the Services.
    /// Think of this as the "wiring" class, not a logic class.
    /// </summary>
    public sealed class InputSystemController : IInitializable, IDisposable
    {
        // ─── Dependencies ──────────────────────────────────────────────────────

        private readonly IInputSystemModel _inputModel;
        private readonly InputSaveService  _saveService;
        private readonly EventBus          _eventBus;

        private readonly CompositeDisposable _disposables = new();

        // ─── Constructor ──────────────────────────────────────────────────────

        public InputSystemController(IInputSystemModel inputModel,
                                     InputSaveService  saveService,
                                     EventBus          eventBus)
        {
            _inputModel  = inputModel;
            _saveService = saveService;
            _eventBus    = eventBus;
        }

        // ─── IInitializable ───────────────────────────────────────────────────

        public void Initialize()
        {
            // 1. Apply saved mobile input mode before anything else renders
            var savedMode = _saveService.LoadMobileInputMode(_inputModel.MobileInputMode.Value);
            _inputModel.SetMobileInputMode(savedMode);

            // 2. Binding overrides are loaded by InputSaveService.Initialize() (runs first)

            // 3. Persist whenever the player changes mobile mode at runtime
            _inputModel.MobileInputMode
                .Skip(1) // Skip the initial value already applied above
                .Subscribe(mode => _saveService.SaveMobileInputMode(mode))
                .AddTo(_disposables);

            Debug.Log($"[InputSystemController] Initialized. MobileInputMode={savedMode}");
        }

        // ─── Public API (called by Settings UI) ───────────────────────────────

        /// <summary>
        /// Change the active mobile input mode. Persists automatically.
        /// </summary>
        public void SetMobileInputMode(MobileInputMode mode)
        {
            _inputModel.SetMobileInputMode(mode);
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
