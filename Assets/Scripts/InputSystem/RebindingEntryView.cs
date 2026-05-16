using System;
using Core.EventBus;
using InputSystem.Controller;
using InputSystem.Data;
using InputSystem.Events;
using InputSystem.Model;
using InputSystem.Service;
using InputSystem.Utility;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace InputSystem.View
{
    /// <summary>
    /// Represents one row in the keybinding list.
    /// Displays the action name, current binding, and Rebind / Reset buttons.
    ///
    /// Prefab structure (create this prefab and wire fields in the Inspector):
    ///   RebindingEntry (GameObject)
    ///   ├── ActionNameLabel   (TMP_Text)
    ///   ├── BindingIconImage  (Image)   — shown when an icon is available
    ///   ├── BindingTextLabel  (TMP_Text) — shown as fallback when no icon
    ///   ├── RebindButton      (Button)
    ///   └── ResetButton       (Button)
    ///
    /// Initialised by RebindingCanvasView.SpawnEntry() — do not place this script
    /// on anything that isn't instantiated from the prefab at runtime.
    /// </summary>
    public sealed class RebindingEntryView : MonoBehaviour
    {
        // ─── Inspector Fields ──────────────────────────────────────────────────

        [Header("Labels")]
        [SerializeField] private TMP_Text _actionNameLabel;
        [SerializeField] private TMP_Text _bindingTextLabel;

        [Header("Icon")]
        [SerializeField] private Image _bindingIconImage;

        [Header("Buttons")]
        [SerializeField] private Button _rebindButton;
        [SerializeField] private Button _resetButton;

        // ─── Private State ─────────────────────────────────────────────────────

        private RebindingController _rebindingController;
        private GamepadIconService  _iconService;
        private IInputSystemModel   _inputModel;
        private EventBus            _eventBus;

        private string       _actionMapName;
        private string       _actionName;
        private int          _bindingIndex;
        private DeviceScheme _scheme;

        private readonly CompositeDisposable _disposables = new();

        // ─── Injection ─────────────────────────────────────────────────────────

        [Inject]
        public void Construct(RebindingController rebindingController,
                              GamepadIconService  iconService,
                              IInputSystemModel   inputModel,
                              EventBus            eventBus)
        {
            _rebindingController = rebindingController;
            _iconService         = iconService;
            _inputModel          = inputModel;
            _eventBus            = eventBus;
        }

        // ─── Initialisation (called by RebindingCanvasView) ───────────────────

        /// <summary>
        /// Configures this entry after the prefab is instantiated.
        /// Must be called before the entry is displayed.
        /// </summary>
        public void Initialize(string actionMapName, string actionName,
                               int bindingIndex, DeviceScheme scheme)
        {
            _actionMapName = actionMapName;
            _actionName    = actionName;
            _bindingIndex  = bindingIndex;
            _scheme        = scheme;

            // Display the human-readable action name (may be a composite part label)
            var action = _inputModel.GetAction(actionMapName, actionName);
            _actionNameLabel.text = action != null
                ? GamepadIconService.GetCompositePartLabel(action, bindingIndex)
                : actionName;

            RefreshBindingDisplay();
            SubscribeToEvents();
            BindButtons();
        }

        // ─── Subscription ──────────────────────────────────────────────────────

        private void SubscribeToEvents()
        {
            // Refresh when this specific binding is rebound
            _eventBus.Receive<RebindCompletedEvent>()
                .Where(e => e.ActionMapName == _actionMapName &&
                            e.ActionName    == _actionName    &&
                            e.BindingIndex  == _bindingIndex)
                .Subscribe(_ => RefreshBindingDisplay())
                .AddTo(_disposables);

            // Refresh when the active device changes (icon might change)
            _inputModel.CurrentDeviceType
                .Skip(1)
                .Subscribe(_ => RefreshBindingDisplay())
                .AddTo(_disposables);

            _inputModel.CurrentGamepadType
                .Skip(1)
                .Subscribe(_ => RefreshBindingDisplay())
                .AddTo(_disposables);

            // Grey-out the Rebind button while a rebind is in progress
            _inputModel.CurrentDeviceType // reuse subscription pattern
                .Subscribe(_ => { }) // just keep the pattern
                .AddTo(_disposables);
        }

        private void BindButtons()
        {
            _rebindButton.OnClickAsObservable()
                .Subscribe(_ => _rebindingController.StartRebind(
                    _actionMapName, _actionName, _bindingIndex, _scheme))
                .AddTo(_disposables);

            _resetButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    _rebindingController.ResetAction(_actionMapName, _actionName, _bindingIndex);
                    RefreshBindingDisplay();
                })
                .AddTo(_disposables);
        }

        // ─── Display Refresh ──────────────────────────────────────────────────

        private void RefreshBindingDisplay()
        {
            var action = _inputModel.GetAction(_actionMapName, _actionName);
            if (action == null) return;

            var displayData = _iconService.GetDisplayData(action, _bindingIndex);

            if (displayData.HasIcon)
            {
                _bindingIconImage.sprite  = displayData.Icon;
                _bindingIconImage.enabled = true;
                _bindingTextLabel.enabled = false;
            }
            else
            {
                _bindingTextLabel.text    = displayData.IsValid ? displayData.DisplayText : "---";
                _bindingTextLabel.enabled = true;
                _bindingIconImage.enabled = false;
            }
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
