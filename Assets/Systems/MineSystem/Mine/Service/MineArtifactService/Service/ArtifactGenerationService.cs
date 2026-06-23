using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Config;
using Systems.MineSystem.Mine.Service.MineArtifactService.Enum;
using Systems.MineSystem.Mine.Service.MineArtifactService.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Service
{
    [Serializable]
    public class ArtifactGenerationService : IDisposable
    {
        private static readonly System.Random Rand = new();
        private readonly IArtifactCatalog _catalog;

        public ArtifactGenerationService(IArtifactCatalog catalog)
        {
            _catalog = catalog;
        }

        public async UniTask GenerateArtifacts(MineData mineData, ArtifactGenerationConfig config)
        {
            await UniTask.SwitchToThreadPool();

            if (_catalog.Definitions.Count == 0)
            {
                await UniTask.SwitchToMainThread();
                Debug.LogWarning("Artifact catalog contains no functional definitions.");
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
            if (mineData.VineDatas != null)
            {
                foreach (var vine in mineData.VineDatas)
                {
                    if (vine?.VineCellIds == null)
                        continue;

                    foreach (var cellId in vine.VineCellIds)
                    {
                        if (!string.IsNullOrEmpty(cellId))
                            occupiedCellIds.Add(cellId);
                    }
                }
            }

            var validCells = new List<Cell>();
            foreach (var cell in mineData.Cells)
            {
                if (cell.HasArtifact)
                    cell.ItemId = null;

                cell.HasArtifact = false;

                if (!string.IsNullOrEmpty(cell.CaveId) ||
                    cell.IsBroken || !cell.IsBreakable ||
                    cell.IsBlank || cell.HasVine || occupiedCellIds.Contains(cell.Id)) continue;

                validCells.Add(cell);
            }

            mineData.Artifacts ??= new List<Artifact>();
            mineData.Artifacts.Clear();
            mineData.ArtifactPlacements ??= new List<ArtifactWorldPlacementData>();
            mineData.ArtifactPlacements.Clear();

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
                var definition = _catalog.GetDefinition(artifactVariant);
                _catalog.TryGetDescription(artifactVariant, out var description);

                var artifact = new Artifact
                {
                    Id = Guid.NewGuid().ToString(),
                    DefinitionId = definition.Id,
                    Variant = definition.Object,
                    Name = description?.ArtifactName ?? definition.Object,
                    Type = "Artifact",
                    Category = definition.ObjectClass,
                    Material = GetRandomMaterial(definition),
                    Condition = GetRandomCondition(),
                    Rarity = GetRarity(i, rareCount, legendaryCount)
                };

                var placement = new ArtifactWorldPlacementData
                {
                    ArtifactInstanceId = artifact.Id,
                    Position = cell.Position,
                    CellId = cell.Id
                };

                mineData.Artifacts.Add(artifact);
                mineData.ArtifactPlacements.Add(placement);
                cell.HasArtifact = true;
                cell.ItemId = artifact.Id;

                Debug.LogWarning(
                    $"Generated artifact '{artifact.DefinitionId}' " +
                    $"(instance: {artifact.Id}) at cell '{cell.Id}', " +
                    $"position: {cell.Position}.");
            }

            await UniTask.SwitchToMainThread();
        }

        private List<string> GetArtifactVariants(ArtifactGenerationConfig config, int totalArtifactCount)
        {
            var artifactVariants = new List<string>(totalArtifactCount);

            var configuredArtifacts = config.artifactGenerationDatas?
                .FindAll(data =>
                    data != null &&
                    !string.IsNullOrWhiteSpace(data.id) &&
                    _catalog.TryGetDefinition(data.id, out _));

            while (configuredArtifacts != null &&
                   configuredArtifacts.Count > 0 &&
                   artifactVariants.Count < totalArtifactCount)
            {
                var artifactData = configuredArtifacts[Rand.Next(configuredArtifacts.Count)];
                var artifactCount = GetRandomInRange(artifactData.minRange, artifactData.maxRange);
                artifactCount = Math.Max(1, artifactCount);

                for (var i = 0; i < artifactCount && artifactVariants.Count < totalArtifactCount; i++)
                {
                    artifactVariants.Add(artifactData.id);
                }
            }

            while (artifactVariants.Count < totalArtifactCount)
            {
                var definition = _catalog.Definitions[Rand.Next(_catalog.Definitions.Count)];
                artifactVariants.Add(definition.Id);
            }

            return artifactVariants;
        }

        private static string GetRandomMaterial(ArtifactDefinition definition)
        {
            if (definition.Materials == null || definition.Materials.Length == 0)
                return string.Empty;

            return definition.Materials[Rand.Next(definition.Materials.Length)];
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
