using System;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Enum;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Scriptable
{
    [CreateAssetMenu(fileName = "ArtifactBossReward", menuName = "Boss/Hedgehog/Artifact Reward")]
    public sealed class ArtifactBossRewardItemScriptable : BossRewardItemDefinitionScriptable
    {
        [Tooltip("Artifact-catalog definition ID used to resolve identity and presentation.")]
        [SerializeField] private string artifactDefinitionId;
        [Tooltip("Material recorded on the created artifact item.")]
        [SerializeField] private string material;
        [Tooltip("Physical condition assigned to the created artifact item.")]
        [SerializeField] private Condition condition = Condition.Intact;
        [Tooltip("Rarity assigned to the created artifact item.")]
        [SerializeField] private Rarity rarity = Rarity.Rare;

        public override bool TryCreateItem(
            IArtifactCatalog artifactCatalog,
            out Item item,
            out string error)
        {
            item = null;
            if (artifactCatalog == null ||
                !artifactCatalog.TryGetDefinition(artifactDefinitionId, out var definition))
            {
                error = $"artifact definition '{artifactDefinitionId}' was not found.";
                return false;
            }

            artifactCatalog.TryGetDescription(artifactDefinitionId, out var description);
            item = new Artifact
            {
                Id = Guid.NewGuid().ToString(),
                DefinitionId = definition.Id,
                Variant = definition.Object,
                Name = description?.ArtifactName ?? definition.Object,
                Type = "Artifact",
                Category = definition.ObjectClass,
                Material = material,
                Condition = condition,
                Rarity = rarity
            };
            error = null;
            return true;
        }
    }
}
