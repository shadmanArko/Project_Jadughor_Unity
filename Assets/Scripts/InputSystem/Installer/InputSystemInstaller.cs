using InputSystem.Config;
using InputSystem.Controller;
using InputSystem.Model;
using InputSystem.Service;
using UnityEngine;
using Zenject;

namespace InputSystem.Installer
{
    /// <summary>
    /// Zenject MonoInstaller that registers every input system class into the DI container.
    ///
    /// ── Setup ──────────────────────────────────────────────────────────────────
    ///
    ///  1. Create a ProjectContext prefab (Zenject menu → "Create Project Context").
    ///  2. Add this MonoInstaller to the ProjectContext prefab's "Mono Installers" list.
    ///  3. Create the three ScriptableObject config assets:
    ///       Assets/Config/Input/InputSystemConfig.asset      → assign InputActionAsset
    ///       Assets/Config/Input/GamepadIconConfig.asset      → fill icon arrays
    ///       Assets/Config/Input/ActionMapContextConfig.asset → define all contexts
    ///  4. Drag those assets into the three serialized fields below.
    ///  5. Also add CoreInstaller.asset to ProjectContext → ScriptableObject Installers.
    ///
    /// ── Binding Architecture ───────────────────────────────────────────────────
    ///
    ///  All core input classes are bound in the ProjectContext so they persist
    ///  across scene loads. Views (RebindingCanvasView, MobileInputContainerView,
    ///  ButtonPromptView) live in scenes — they are MonoBehaviours that receive
    ///  their dependencies via [Inject] from the global container automatically.
    ///
    ///  RebindingController is also bound here but is non-lazy so it is always
    ///  ready even when the rebinding screen is not open.
    /// </summary>
    
    [CreateAssetMenu(fileName = "InputSystemInstaller", menuName = "Installers/InputSystem/InputSystemInstaller")]

    public sealed class InputSystemInstaller : ScriptableObjectInstaller<InputSystemInstaller>
    {
        // ─── Inspector Config References ──────────────────────────────────────

        [Header("Config Assets  (create these in Assets/Config/Input/)")]
        [SerializeField] private InputSystemConfig      _inputSystemConfig;
        [SerializeField] private GamepadIconConfig      _gamepadIconConfig;
        [SerializeField] private ActionMapContextConfig _actionMapContextConfig;

        // ─── InstallBindings ──────────────────────────────────────────────────

        public override void InstallBindings()
        {
            ValidateConfigs();

            BindConfigs();
            BindModels();
            BindServices();
            BindControllers();
        }

        // ─── Configs ──────────────────────────────────────────────────────────

        private void BindConfigs()
        {
            Container.BindInstance(_inputSystemConfig).AsSingle();
            Container.BindInstance(_gamepadIconConfig).AsSingle();
            Container.BindInstance(_actionMapContextConfig).AsSingle();
        }

        // ─── Models ───────────────────────────────────────────────────────────

        private void BindModels()
        {
            // IInputSystemModel / InputSystemModel — owns the InputActionAsset lifetime
            Container.Bind<IInputSystemModel>()
                     .To<InputSystemModel>()
                     .AsSingle()
                     .NonLazy();

            // IDeviceModel — read-only device inventory (DeviceModel is mutable internally)
            Container.Bind<DeviceModel>().AsSingle();
            Container.Bind<IDeviceModel>().To<DeviceModel>().FromResolve();

            // IRebindModel — rebinding state observable by the UI
            Container.Bind<RebindModel>().AsSingle();
            Container.Bind<IRebindModel>().To<RebindModel>().FromResolve();
        }

        // ─── Services ─────────────────────────────────────────────────────────

        private void BindServices()
        {
            // ActionMapService — context stack, drives which maps are enabled
            Container.BindInterfacesAndSelfTo<ActionMapService>()
                     .AsSingle()
                     .NonLazy();

            // DeviceDetectionService — subscribes to InputSystem events
            Container.BindInterfacesAndSelfTo<DeviceDetectionService>()
                     .AsSingle()
                     .NonLazy();

            // HapticsService — motor speed management
            Container.BindInterfacesAndSelfTo<HapticsService>()
                     .AsSingle()
                     .NonLazy();

            // InputSaveService — PlayerPrefs persistence
            Container.BindInterfacesAndSelfTo<InputSaveService>()
                     .AsSingle()
                     .NonLazy();

            // GamepadIconService — icon/text lookup (stateless, no IInitializable needed)
            Container.Bind<GamepadIconService>().AsSingle();
        }

        // ─── Controllers ──────────────────────────────────────────────────────

        private void BindControllers()
        {
            // InputSystemController — wires save preferences on startup
            Container.BindInterfacesAndSelfTo<InputSystemController>()
                     .AsSingle()
                     .NonLazy();

            // DeviceDetectionController — logs device changes, extension point for UI skins
            Container.BindInterfacesAndSelfTo<DeviceDetectionController>()
                     .AsSingle()
                     .NonLazy();

            // HapticsController — bridges HapticRequestEvent → HapticsService
            Container.BindInterfacesAndSelfTo<HapticsController>()
                     .AsSingle()
                     .NonLazy();

            // RebindingController — not IInitializable; constructed on demand
            // Bound as AsSingle so the same instance is shared between the installer
            // and any scene that injects it (e.g. RebindingCanvasView).
            Container.Bind<RebindingController>().AsSingle().NonLazy();
        }

        // ─── Validation ───────────────────────────────────────────────────────

        private void ValidateConfigs()
        {
            if (_inputSystemConfig == null)
                Debug.LogError("[InputSystemInstaller] InputSystemConfig is not assigned!", this);
            else if (_inputSystemConfig.ActionAsset == null)
                Debug.LogError("[InputSystemInstaller] InputSystemConfig.ActionAsset is not assigned!", this);

            if (_gamepadIconConfig == null)
                Debug.LogError("[InputSystemInstaller] GamepadIconConfig is not assigned!", this);

            if (_actionMapContextConfig == null)
                Debug.LogError("[InputSystemInstaller] ActionMapContextConfig is not assigned!", this);
        }
    }
}
