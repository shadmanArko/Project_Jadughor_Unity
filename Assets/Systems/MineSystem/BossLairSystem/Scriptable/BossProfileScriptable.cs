using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Scriptable
{
    /// <summary>
    /// Authored identity for one boss: which gate appears in the mine, which
    /// config drives the boss, and optionally a lair layout of its own.
    /// </summary>
    /// <remarks>
    /// Region and site eligibility deliberately lives in
    /// <c>BossSpawnTableScriptable</c> rather than here, so the whole
    /// distribution is authored in one asset instead of being spread across
    /// every profile.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "BossProfile",
        menuName = "Boss/Boss Profile")]
    public sealed class BossProfileScriptable : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Concrete boss this profile represents.")]
        [SerializeField] private BossVariant bossVariant;
        [Tooltip("Name shown in UI and notifications.")]
        [SerializeField] private string displayName;

        [Header("Presentation")]
        [Tooltip(
            "Gate placed in the mine for this boss. Each boss has its own gate " +
            "design, so this prefab is what tells the player which boss waits " +
            "behind it.")]
        [SerializeField] private GameObject gatePrefab;

        [Header("Data")]
        [Tooltip("Enemy config driving this boss's stats and behaviour.")]
        [SerializeField] private EnemyConfigScriptable bossConfig;
        [Tooltip(
            "Arena geometry and camera zoom for this boss's lair. Required, " +
            "because arena size is per boss.")]
        [SerializeField] private BossProceduralLairConfig proceduralLairConfig;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? bossVariant.ToString()
                : displayName;
        public GameObject GatePrefab => gatePrefab;
        public EnemyConfigScriptable BossConfig => bossConfig;
        public BossProceduralLairConfig ProceduralLairConfig => proceduralLairConfig;

        public bool Validate(out string error)
        {
            if (gatePrefab == null)
            {
                error = $"{name} requires a gate prefab.";
                return false;
            }
            if (bossConfig == null)
            {
                error = $"{name} requires a boss config.";
                return false;
            }
            if (proceduralLairConfig == null)
            {
                error = $"{name} requires a procedural lair config.";
                return false;
            }
            if (!proceduralLairConfig.Validate(out error))
                return false;
            if (bossConfig.EnemyType != EnemyType.Boss)
            {
                error =
                    $"{name} boss config must use EnemyType.Boss " +
                    $"(currently {bossConfig.EnemyType}).";
                return false;
            }
            if (bossConfig.VariantId != bossVariant.ToString())
            {
                error =
                    $"{name} variant ({bossVariant}) does not match its boss " +
                    $"config variant ({bossConfig.VariantId}).";
                return false;
            }
            error = null;
            return true;
        }
    }
}
