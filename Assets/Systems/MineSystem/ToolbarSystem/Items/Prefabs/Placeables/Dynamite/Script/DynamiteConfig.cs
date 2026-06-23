using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public enum DynamiteBlastPattern
    {
        Cross,
        AdjacentEight
    }

    [CreateAssetMenu(
        fileName = "DynamiteConfig",
        menuName = "Toolbar Actions/Dynamite Config")]
    public sealed class DynamiteConfig : ScriptableObject
    {
        [Header("Countdown")]
        [Min(1)] [SerializeField] private int countdownSeconds = 5;
        [Min(0.01f)] [SerializeField] private float tickInterval = 1f;

        [Header("Explosion Damage")]
        [Min(0f)] [SerializeField] private float objectDamage = 25f;
        [Min(1)] [SerializeField] private int wallDamage = 40;
        [Min(0.001f)] [SerializeField] private float overlapRadius = 0.1f;
        [SerializeField] private LayerMask targetLayers = ~0;

        [Header("Explosion Animation")]
        [SerializeField] private string explosionState = "Explode";
        [Range(0f, 1f)]
        [SerializeField] private float normalizedImpactTime = 0.5f;
        [Min(0.01f)]
        [SerializeField] private float fallbackAnimationDuration = 1.0167f;
        [Min(0f)]
        [SerializeField] private float delayBetweenStages;
        [SerializeField] private DynamiteBlastPattern blastPattern;

        [Header("Screen Shake")]
        [Min(0.01f)] [SerializeField] private float shakeDuration = 0.25f;
        [Min(0f)] [SerializeField] private float shakeStrength = 0.08f;

        [Header("Timer Presentation")]
        [SerializeField] private Vector3 timerOffset =
            new(0.15f, 0.08f, 0f);
        [Min(0.1f)] [SerializeField] private float timerFontSize = 2f;
        [SerializeField] private Color timerColor = Color.white;

        [Header("Pooling")]
        [SerializeField] private ExplosionSmokeView explosionSmokePrefab;
        [Min(1)] [SerializeField] private int dynamitePrewarmSize = 10;
        [Min(1)] [SerializeField] private int smokePrewarmSize = 20;

        [Header("Dynamite Collider")]
        [Min(0.01f)] [SerializeField] private float colliderRadius = 0.08f;

        public int CountdownSeconds => Mathf.Max(1, countdownSeconds);
        public float TickInterval => Mathf.Max(0.01f, tickInterval);
        public float ObjectDamage => Mathf.Max(0f, objectDamage);
        public int WallDamage => Mathf.Max(1, wallDamage);
        public float OverlapRadius => Mathf.Max(0.001f, overlapRadius);
        public LayerMask TargetLayers => targetLayers;
        public string ExplosionState => explosionState;
        public float NormalizedImpactTime =>
            Mathf.Clamp01(normalizedImpactTime);
        public float FallbackAnimationDuration =>
            Mathf.Max(0.01f, fallbackAnimationDuration);
        public float DelayBetweenStages =>
            Mathf.Max(0f, delayBetweenStages);
        public DynamiteBlastPattern BlastPattern => blastPattern;
        public float ShakeDuration => Mathf.Max(0.01f, shakeDuration);
        public float ShakeStrength => Mathf.Max(0f, shakeStrength);
        public Vector3 TimerOffset => timerOffset;
        public float TimerFontSize => Mathf.Max(0.1f, timerFontSize);
        public Color TimerColor => timerColor;
        public ExplosionSmokeView ExplosionSmokePrefab =>
            explosionSmokePrefab;
        public int DynamitePrewarmSize =>
            Mathf.Max(1, dynamitePrewarmSize);
        public int SmokePrewarmSize => Mathf.Max(1, smokePrewarmSize);
        public float ColliderRadius => Mathf.Max(0.01f, colliderRadius);
    }
}
