using System;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Model
{
    [Serializable]
    public sealed class EnemyWaveSpawnData
    {
        [SerializeField] private string waveId;
        [SerializeField] private EnemyType enemyType;
        [Tooltip(
            "Variant enum name declared by the assigned enemy config. " +
            "Spaces, hyphens, underscores, and letter casing are ignored.")]
        [SerializeField] private string variantId;
        [SerializeField] private EnemyConfigScriptable enemyConfig;

        [Header("Start Conditions (OR)")]
        [SerializeField] private bool useTimeTrigger = true;
        [Range(0, 23)] [SerializeField] private int startHour = 9;
        [Range(0, 59)] [SerializeField] private int startMinute;
        [SerializeField] private bool useWallBreakTrigger;
        [Min(0)] [SerializeField] private int requiredBrokenCells;

        [Header("Spawn Cadence")]
        [Min(1)] [SerializeField] private int enemyCount = 1;
        [Min(0)] [SerializeField] private int spawnIntervalInGameMinutes = 10;

        public string WaveId => waveId;
        public EnemyType EnemyType => enemyType;
        public string VariantId => variantId;
        public EnemyConfigScriptable EnemyConfig => enemyConfig;
        public bool UseTimeTrigger => useTimeTrigger;
        public int StartHour => startHour;
        public int StartMinute => startMinute;
        public bool UseWallBreakTrigger => useWallBreakTrigger;
        public int RequiredBrokenCells => requiredBrokenCells;
        public int EnemyCount => enemyCount;
        public int SpawnIntervalInGameMinutes => spawnIntervalInGameMinutes;
    }
}
