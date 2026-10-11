using System;
using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Model;
using System.Text;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Config
{
    [CreateAssetMenu(fileName = "EnemyWaveConfig", menuName = "Enemy/Wave Config")]
    public sealed class EnemyWaveConfig : ScriptableObject
    {
        [SerializeField] private List<EnemyWaveSpawnData> spawnData = new();
        [Min(0)] [SerializeField]
        private int failedToSpawnDelayInGameMinutes = 10;
        [Min(0)] [SerializeField] private int outsideCameraMarginInTiles = 1;

        public IReadOnlyList<EnemyWaveSpawnData> SpawnData => spawnData;
        public int FailedToSpawnDelayInGameMinutes =>
            failedToSpawnDelayInGameMinutes;
        public int OutsideCameraMarginInTiles => outsideCameraMarginInTiles;

        public bool Validate(out string error)
        {
            if (failedToSpawnDelayInGameMinutes < 0 ||
                outsideCameraMarginInTiles < 0)
            {
                error = $"{name} contains a negative delay or camera margin.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < spawnData.Count; i++)
            {
                var entry = spawnData[i];
                if (entry == null)
                {
                    error = $"{name} wave entry {i} is null.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(entry.WaveId) ||
                    !ids.Add(entry.WaveId))
                {
                    error = $"{name} wave entry {i} has a missing or duplicate ID.";
                    return false;
                }
                if (!entry.UseTimeTrigger && !entry.UseWallBreakTrigger)
                {
                    error = $"Wave '{entry.WaveId}' requires at least one trigger.";
                    return false;
                }
                if (entry.StartHour < 0 || entry.StartHour > 23 ||
                    entry.StartMinute < 0 || entry.StartMinute > 59 ||
                    entry.RequiredBrokenCells < 0 || entry.EnemyCount <= 0 ||
                    entry.SpawnIntervalInGameMinutes < 0)
                {
                    error = $"Wave '{entry.WaveId}' contains invalid timing or count values.";
                    return false;
                }
                if (entry.EnemyConfig == null ||
                    entry.EnemyConfig.EnemyType != entry.EnemyType ||
                    !VariantIdsMatch(
                        entry.EnemyConfig.VariantId,
                        entry.VariantId))
                {
                    var actualType = entry.EnemyConfig != null
                        ? entry.EnemyConfig.EnemyType.ToString()
                        : "None";
                    var actualVariant = entry.EnemyConfig != null
                        ? entry.EnemyConfig.VariantId
                        : "None";
                    error =
                        $"Wave '{entry.WaveId}' expects " +
                        $"{entry.EnemyType}/{entry.VariantId}, but its config " +
                        $"provides {actualType}/{actualVariant}.";
                    return false;
                }
                if (!entry.EnemyConfig.Validate(out var configError))
                {
                    error = $"Wave '{entry.WaveId}' config is invalid: {configError}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool VariantIdsMatch(string left, string right)
        {
            return string.Equals(
                NormalizeVariantId(left),
                NormalizeVariantId(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeVariantId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }
            return builder.ToString();
        }
    }
}
