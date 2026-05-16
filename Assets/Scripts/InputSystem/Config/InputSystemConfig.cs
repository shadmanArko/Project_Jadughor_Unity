using InputSystem.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InputSystem.Config
{
    /// <summary>
    /// Master configuration ScriptableObject for the entire Input System.
    /// Create one instance: Assets/Config/InputSystemConfig.asset
    /// Reference it in InputSystemInstaller.
    ///
    /// This asset is the ONLY place that holds a direct serialized reference
    /// to the InputActionAsset — everything else accesses actions through
    /// IInputSystemModel, keeping coupling minimal.
    /// </summary>
    [CreateAssetMenu(fileName = "InputSystemConfig", menuName = "Config/Input/InputSystemConfig")]
    public sealed class InputSystemConfig : ScriptableObject
    {
        [Header("Input Actions Asset")]
        [Tooltip("The project's single .inputactions asset. All action maps and bindings live here.")]
        public InputActionAsset ActionAsset;

        [Header("Mobile Defaults")]
        [Tooltip("Which touch input mode is active when the game first runs on a mobile device.")]
        public MobileInputMode DefaultMobileInputMode = MobileInputMode.VirtualControls;

        [Header("Haptics")]
        [Tooltip("How fast (per second) haptic intensity decays to zero when no new haptic event arrives.")]
        [Range(0f, 5f)]
        public float HapticDecayRate = 1.5f;

        [Tooltip("Default low-frequency motor speed for generic haptic pulses (0–1).")]
        [Range(0f, 1f)]
        public float DefaultLowFrequency = 0.4f;

        [Tooltip("Default high-frequency motor speed for generic haptic pulses (0–1).")]
        [Range(0f, 1f)]
        public float DefaultHighFrequency = 0.6f;

        [Header("Device Detection")]
        [Tooltip(
            "Minimum time in seconds before the system re-evaluates which device is active. " +
            "Prevents flickering when inputs from two devices arrive simultaneously.")]
        [Range(0.05f, 1f)]
        public float DeviceChangeCooldown = 0.2f;
    }
}
