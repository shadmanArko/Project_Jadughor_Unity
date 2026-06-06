using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Config;
using Systems.MineSystem.Mine.Service.MineArtifactService.Enum;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Service
{
    [Serializable]
    public class ArtifactGenerationService : IDisposable
    {
        private static readonly System.Random Rand = new();

        public async UniTask GenerateArtifacts(MineData mineData, ArtifactGenerationConfig config)
        {
            await UniTask.SwitchToThreadPool();

            if (config.artifactGenerationDatas == null || config.artifactGenerationDatas.Count == 0)
            {
                await UniTask.SwitchToMainThread();
                Debug.LogWarning("ArtifactGenerationConfig has no artifacts defined!");
                return;
            }

            var occupiedCellIds = new HashSet<string>();

            if (mineData.Resources != null)
            {
                foreach (var resource in mineData.Resources)
                {
                    if (!string.IsNullOrEmpty(resource.CellId))
                        occupiedCellIds.Add(resource.CellId);
                }
            }

            if (mineData.CellPlaceables != null)
            {
                foreach (var placeable in mineData.CellPlaceables)
                {
                    if (!string.IsNullOrEmpty(placeable.OccupiedCellId))
                        occupiedCellIds.Add(placeable.OccupiedCellId);
                }
            }

            var validCells = new List<Cell>();
            foreach (var cell in mineData.Cells)
            {
                cell.HasArtifact = false;

                if (!string.IsNullOrEmpty(cell.CaveId) ||
                    cell.IsBroken || !cell.IsBreakable ||
                    cell.IsBlank || occupiedCellIds.Contains(cell.Id)) continue;

                validCells.Add(cell);
            }

            mineData.Artifacts ??= new List<Artifact>();
            mineData.Artifacts.Clear();

            var totalArtifactCount = GetRandomInRange(config.minNumberOfArtifacts, config.maxNumberOfArtifacts);
            totalArtifactCount = Math.Min(totalArtifactCount, validCells.Count);

            var legendaryCount =
                GetRandomInRange(config.minNumberOfLegendaryArtifacts, config.maxNumberOfLegendaryArtifacts);
            legendaryCount = Math.Min(legendaryCount, totalArtifactCount);

            var rareCount = GetRandomInRange(config.minNumberOfRareArtifacts, config.maxNumberOfRareArtifacts);
            rareCount = Math.Min(rareCount, totalArtifactCount - legendaryCount);

            var artifactVariants = GetArtifactVariants(config, totalArtifactCount);

            for (var i = 0; i < totalArtifactCount; i++)
            {
                var cellIndex = Rand.Next(validCells.Count);
                var cell = validCells[cellIndex];

                validCells[cellIndex] = validCells[^1];
                validCells.RemoveAt(validCells.Count - 1);

                var artifactVariant = artifactVariants[i];

                var artifact = new Artifact
                {
                    Id = Guid.NewGuid().ToString(),
                    Variant = artifactVariant,
                    Name = artifactVariant,
                    Type = "Artifact",
                    Category = "Artifact",
                    Condition = GetRandomCondition(),
                    Rarity = GetRarity(i, rareCount, legendaryCount),
                    Position = cell.Position,
                    CellId = cell.Id
                };

                mineData.Artifacts.Add(artifact);
                cell.HasArtifact = true;
                cell.ItemId = artifact.Variant;
            }

            await UniTask.SwitchToMainThread();
        }

        private static List<string> GetArtifactVariants(ArtifactGenerationConfig config, int totalArtifactCount)
        {
            var artifactVariants = new List<string>(totalArtifactCount);

            while (artifactVariants.Count < totalArtifactCount)
            {
                var artifactData = config.artifactGenerationDatas[Rand.Next(config.artifactGenerationDatas.Count)];
                var artifactCount = GetRandomInRange(artifactData.minRange, artifactData.maxRange);
                artifactCount = Math.Max(1, artifactCount);

                for (var i = 0; i < artifactCount && artifactVariants.Count < totalArtifactCount; i++)
                {
                    artifactVariants.Add(artifactData.id);
                }
            }

            return artifactVariants;
        }

        private static int GetRandomInRange(int min, int max)
        {
            var clampedMin = Math.Max(0, min);
            var clampedMax = Math.Max(clampedMin, max);

            return Rand.Next(clampedMin, clampedMax + 1);
        }

        private static Condition GetRandomCondition()
        {
            var conditionValue = Rand.Next(0, 101);
            return conditionValue switch
            {
                <= 75 => Condition.Decrepit,
                <= 98 => Condition.Intact,
                _ => Condition.Pristine
            };
        }

        private static Rarity GetRarity(int index, int rareCount, int legendaryCount)
        {
            if (index < legendaryCount) return Rarity.Legendary;
            if (index < legendaryCount + rareCount) return Rarity.Rare;

            return Rand.Next(0, 101) <= 80 ? Rarity.Common : Rarity.Uncommon;
        }

        public void Dispose()
        {
        }
    }
}