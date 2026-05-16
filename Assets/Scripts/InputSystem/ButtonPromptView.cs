using Core.EventBus;
using InputSystem.Data;
using InputSystem.Events;
using InputSystem.Model;
using InputSystem.Service;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using DeviceType = InputSystem.Data.DeviceType;

namespace InputSystem.View
{
    /// <summary>
    /// Displays the correct button icon or label for one InputAction on the current device.
    /// Automatically refreshes whenever:
    ///   - The player switches input device.
    ///   - The player rebinds the action.
    ///
    /// Place this component on any UI element that shows a contextual hint
    /// (e.g. "[E] Interact", "[A] Confirm").
    ///
    /// Inspector setup:
    ///   1. Set ActionMapName to an InputConstants.Maps value.
    ///   2. Set ActionName to an InputConstants.Actions.* value.
    ///   3. (Optional) Set PreferredScheme to override auto-detection.
    ///   4. Wire IconImage and/or TextLabel in the Inspector.
    ///
    /// Precedence: icon > text. If no icon is configured for this control, text is shown.
    /// </summary>
    public sealed class ButtonPromptView : MonoBehaviour
    {
        // ─── Inspector Fields ──────────────────────────────────────────────────

        [Header("Action Identity")]
        [Tooltip("Must match InputConstants.Maps exactly.")]
        [SerializeField] private string _actionMapName;

        [Tooltip("Must match InputConstants.Actions.* exactly.")]
        [SerializeField] private string _actionName;

        [Header("UI Components")]
        [SerializeField] private Image    _iconImage;
        [SerializeField] private TMP_Text _textLabel;

        [Header("Optional Override")]
        [Tooltip("Leave as default to auto-detect from the active device.")]
        [SerializeField] private bool        _overrideScheme;
        [SerializeField] private DeviceType  _forcedDeviceType  = DeviceType.KeyboardMouse;
        [SerializeField] private GamepadType _forcedGamepadType = GamepadType.Generic;

        // ─── Injected Dependencies ────────────────────────────────────────────

        private IInputSystemModel  _inputModel;
        private GamepadIconService _iconService;
        private EventBus           _eventBus;

        [Inject]
        public void Construct(IInputSystemModel  inputModel,
                              GamepadIconService iconService,
                              EventBus           eventBus)
        {
            _inputModel  = inputModel;
            _iconService = iconService;
            _eventBus    = eventBus;
        }

        // ─── Private State ─────────────────────────────────────────────────────

        private readonly CompositeDisposable _disposables = new();

        // ─── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            Refresh();

            // Refresh on device change
            _inputModel.CurrentDeviceType
                .Skip(1)
                .Subscribe(_ => Refresh())
                .AddTo(_disposables);

            _inputModel.CurrentGamepadType
                .Skip(1)
                .Subscribe(_ => Refresh())
                .AddTo(_disposables);

            // Refresh when this specific action is rebound
            _eventBus.Receive<RebindCompletedEvent>()
                .Where(e => e.ActionMapName == _actionMapName && e.ActionName == _actionName)
                .Subscribe(_ => Refresh())
                .AddTo(_disposables);
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        private void Refresh()
        {
            var action = _inputModel.GetAction(_actionMapName, _actionName);
            if (action == null)
            {
                SetEmpty();
                return;
            }

            var deviceType  = _overrideScheme ? _forcedDeviceType  : _inputModel.CurrentDeviceType.Value;
            var gamepadType = _overrideScheme ? _forcedGamepadType : _inputModel.CurrentGamepadType.Value;

            // Find the first binding index that belongs to the correct scheme group
            var schemeGroup = ResolveSchemeGroup(deviceType);
            var idx         = GamepadIconService.GetFirstBindingIndexForScheme(action, schemeGroup);

            if (idx < 0)
            {
                SetEmpty();
                return;
            }

            var data = _iconService.GetDisplayData(action, idx, deviceType, gamepadType);

            if (data.HasIcon)
            {
                if (_iconImage  != null) { _iconImage.sprite  = data.Icon; _iconImage.enabled  = true; }
                if (_textLabel  != null) _textLabel.enabled   = false;
            }
            else if (data.IsValid)
            {
                if (_textLabel  != null) { _textLabel.text   = data.DisplayText; _textLabel.enabled = true; }
                if (_iconImage  != null) _iconImage.enabled  = false;
            }
            else
            {
                SetEmpty();
            }
        }

        private void SetEmpty()
        {
            if (_iconImage  != null) _iconImage.enabled  = false;
            if (_textLabel  != null) { _textLabel.text   = "---"; _textLabel.enabled = true; }
        }

        private static string ResolveSchemeGroup(DeviceType deviceType) => deviceType switch
        {
            DeviceType.KeyboardMouse => InputConstants.Schemes.KeyboardMouse,
            DeviceType.Gamepad       => InputConstants.Schemes.Gamepad,
            DeviceType.Mobile        => InputConstants.Schemes.Touch,
            DeviceType.Handheld      => InputConstants.Schemes.Touch,
            _                        => InputConstants.Schemes.KeyboardMouse
        };

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
