using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script
{
    [CreateAssetMenu(
        fileName = "PileDriverConfig",
        menuName = "Toolbar Actions/PileDriver Config")]
    public sealed class PileDriverConfig : ScriptableObject
    {
        [Header("Mining")]
        [Min(1)] [SerializeField] private int processedCellCount = 5;
        [Min(1)] [SerializeField] private int firstTargetOffset = 2;
        [Min(1)] [SerializeField] private int damagePerStomp = 10;

        [Header("Motion")]
        [Min(0.01f)] [SerializeField] private float secondsPerCell = 2f;
        [Min(0.01f)]
        [SerializeField] private float hardStompSecondsPerCell = 0.15f;
        [Min(0f)] [SerializeField] private float delayAfterStomp = 0.25f;

        [Header("Core Animation")]
        [SerializeField] private string turnOnState = "TurnOn";
        [SerializeField] private string activeState = "Active";
        [SerializeField] private string turnOffState = "TurnOff";
        [Min(0f)] [SerializeField] private float turnOnFallbackDuration = 0.02f;
        [Min(0f)] [SerializeField] private float turnOffFallbackDuration = 0.02f;

        [Header("Screen Shake Distance (Cells)")]
        [Min(0f)] [SerializeField] private float extremeShakeDistance = 3f;
        [Min(0f)] [SerializeField] private float heavyShakeDistance = 6f;
        [Min(0f)] [SerializeField] private float mediumShakeDistance = 9f;
        [Min(0f)] [SerializeField] private float lightShakeDistance = 12f;

        [Header("Fallback")]
        [Min(0.001f)] [SerializeField] private float fallbackCellWorldSize = 0.2f;

        public int ProcessedCellCount => Mathf.Max(1, processedCellCount);
        public int FirstTargetOffset => Mathf.Max(1, firstTargetOffset);
        public int DamagePerStomp => Mathf.Max(1, damagePerStomp);
        public float SecondsPerCell => Mathf.Max(0.01f, secondsPerCell);
        public float HardStompSecondsPerCell =>
            Mathf.Max(0.01f, hardStompSecondsPerCell);
        public float DelayAfterStomp => Mathf.Max(0f, delayAfterStomp);
        public string TurnOnState => turnOnState;
        public string ActiveState => activeState;
        public string TurnOffState => turnOffState;
        public float TurnOnFallbackDuration =>
            Mathf.Max(0f, turnOnFallbackDuration);
        public float TurnOffFallbackDuration =>
            Mathf.Max(0f, turnOffFallbackDuration);
        public float ExtremeShakeDistance =>
            Mathf.Max(0f, extremeShakeDistance);
        public float HeavyShakeDistance =>
            Mathf.Max(ExtremeShakeDistance, heavyShakeDistance);
        public float MediumShakeDistance =>
            Mathf.Max(HeavyShakeDistance, mediumShakeDistance);
        public float LightShakeDistance =>
            Mathf.Max(MediumShakeDistance, lightShakeDistance);
        public float FallbackCellWorldSize =>
            Mathf.Max(0.001f, fallbackCellWorldSize);
    }
}
