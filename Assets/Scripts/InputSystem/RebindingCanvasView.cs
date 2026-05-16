using System.Collections.Generic;
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
    /// Master rebinding screen controller.
    ///
    /// Spawns one RebindingEntryView per bindable slot per scheme, groups them
    /// under action-map section headers, and manages the "Waiting for input…"
    /// overlay and Reset-All button.
    ///
    /// Canvas / Prefab structure expected:
    ///   RebindingCanvas
    ///   ├── DeviceTabView               (DeviceTabView component)
    ///   ├── ContentScrollView
    ///   │   └── ContentParent           (Vertical Layout Group — assign to _contentParent)
    ///   ├── RebindingOverlay            (assign to _rebindingOverlay; initially inactive)
    ///   │   └── WaitingLabel (TMP_Text) (assign to _waitingLabel)
    ///   └── ResetAllButton (Button)     (assign to _resetAllButton)
    ///
    /// Prefabs needed:
    ///   - RebindingEntryPrefab   (RebindingEntryView component)
    ///   - SectionHeaderPrefab    (TMP_Text component — label only)
    /// </summary>
    public sealed class RebindingCanvasView : MonoBehaviour
    {
        // ─── Inspector Fields ──────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private DeviceTabView _deviceTabView;
        [SerializeField] private Transform     _contentParent;
        [SerializeField] private GameObject    _rebindingOverlay;
        [SerializeField] private TMP_Text      _waitingLabel;
        [SerializeField] private Button        _resetAllButton;

        [Header("Prefabs")]
        [SerializeField] private RebindingEntryView _entryPrefab;
        [SerializeField] private TMP_Text           _sectionHeaderPrefab;

        [Header("Waiting Text")]
        [SerializeField] private string _waitingForInputText = "Press any key…\n(Esc / Start to cancel)";

        // ─── Injected Dependencies ────────────────────────────────────────────

        private IInputSystemModel   _inputModel;
        private IRebindModel        _rebindModel;
        private RebindingController _rebindingController;
        private GamepadIconService  _iconService;
        private EventBus            _eventBus;
        private DiContainer         _container;

        [Inject]
        public void Construct(IInputSystemModel   inputModel,
                              IRebindModel        rebindModel,
                              RebindingController rebindingController,
                              GamepadIconService  iconService,
                              EventBus            eventBus,
                              DiContainer         container)
        {
            _inputModel          = inputModel;
            _rebindModel         = rebindModel;
            _rebindingController = rebindingController;
            _iconService         = iconService;
            _eventBus            = eventBus;
            _container           = container;
        }

        // ─── Private State ─────────────────────────────────────────────────────

        private readonly CompositeDisposable _disposables  = new();
        private readonly List<GameObject>    _spawnedItems = new();
        private DeviceScheme                 _currentScheme = DeviceScheme.KeyboardMouse;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            // Overlay: react to rebinding state changes
            _rebindModel.IsRebinding
                .Subscribe(isRebinding =>
                {
                    _rebindingOverlay.SetActive(isRebinding);
                    if (isRebinding && _waitingLabel != null)
                        _waitingLabel.text = _waitingForInputText;
                })
                .AddTo(_disposables);

            // Reset All button
            _resetAllButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    _rebindingController.ResetAll();
                    BuildEntriesForScheme(_currentScheme);
                })
                .AddTo(_disposables);

            // Tab switching
            _deviceTabView.OnSchemeChanged
                .Subscribe(scheme =>
                {
                    _currentScheme = scheme;
                    BuildEntriesForScheme(scheme);
                })
                .AddTo(_disposables);

            // Cancel overlay click passes through to the rebind controller
            _rebindingOverlay
                .GetComponent<Button>()
                ?.OnClickAsObservable()
                .Subscribe(_ => _rebindingController.CancelRebind())
                .AddTo(_disposables);

            // Build initial list
            BuildEntriesForScheme(_deviceTabView.ActiveScheme);
        }

        // ─── Entry Building ───────────────────────────────────────────────────

        /// <summary>
        /// Destroys all existing entry rows and rebuilds them for the given scheme.
        /// Iterates every action map → every action → every binding index belonging
        /// to the selected scheme (including composite parts individually).
        /// </summary>
        private void BuildEntriesForScheme(DeviceScheme scheme)
        {
            ClearSpawnedItems();

            var schemeGroup = SchemeToGroupName(scheme);
            var asset       = _inputModel.Actions;

            foreach (var actionMap in asset.actionMaps)
            {
                bool headerSpawned = false;

                foreach (var action in actionMap.actions)
                {
                    var indices = GamepadIconService.GetBindingIndicesForScheme(action, schemeGroup);
                    if (indices.Count == 0) continue;

                    if (!headerSpawned)
                    {
                        SpawnSectionHeader(actionMap.name);
                        headerSpawned = true;
                    }

                    foreach (var bindingIndex in indices)
                        SpawnEntry(actionMap.name, action.name, bindingIndex, scheme);
                }
            }
        }

        private void SpawnSectionHeader(string mapName)
        {
            var header = Instantiate(_sectionHeaderPrefab, _contentParent);
            header.text = mapName; // Replace with localised string if needed
            _spawnedItems.Add(header.gameObject);
        }

        private void SpawnEntry(string actionMapName, string actionName,
                                int bindingIndex, DeviceScheme scheme)
        {
            // Use Zenject to instantiate so [Inject] fields are resolved
            var entry = _container.InstantiatePrefabForComponent<RebindingEntryView>(
                _entryPrefab, _contentParent);

            entry.Initialize(actionMapName, actionName, bindingIndex, scheme);
            _spawnedItems.Add(entry.gameObject);
        }

        private void ClearSpawnedItems()
        {
            foreach (var item in _spawnedItems)
                if (item != null) Destroy(item);

            _spawnedItems.Clear();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static string SchemeToGroupName(DeviceScheme scheme) => scheme switch
        {
            DeviceScheme.KeyboardMouse => InputConstants.Schemes.KeyboardMouse,
            DeviceScheme.Gamepad       => InputConstants.Schemes.Gamepad,
            DeviceScheme.Touch         => InputConstants.Schemes.Touch,
            _                          => InputConstants.Schemes.KeyboardMouse
        };

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            _rebindingController.CancelRebind();
            _disposables.Dispose();
        }
    }
}
