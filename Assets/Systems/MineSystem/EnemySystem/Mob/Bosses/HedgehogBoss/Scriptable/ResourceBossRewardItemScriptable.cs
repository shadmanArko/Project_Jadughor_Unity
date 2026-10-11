using System;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Scriptable
{
    [CreateAssetMenu(fileName = "ResourceBossReward", menuName = "Boss/Hedgehog/Resource Reward")]
    public sealed class ResourceBossRewardItemScriptable : BossRewardItemDefinitionScriptable
    {
        [Tooltip("Authoritative resource variant used for item and sprite resolution.")]
        [SerializeField] private string resourceVariant;
        [Tooltip("Optional player-facing name. The variant is used when this is blank.")]
        [SerializeField] private string displayName;
        [Tooltip("Whether collected copies of this reward can occupy the same inventory stack.")]
        [SerializeField] private bool isStackable = true;
        [Tooltip("Maximum number of copies allowed in one inventory stack.")]
        [Min(1)] [SerializeField] private int maxStackAmount = 99;

        public override bool TryCreateItem(
            IArtifactCatalog artifactCatalog,
            out Item item,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(resourceVariant))
            {
                item = null;
                error = "resource variant is empty.";
                return false;
            }

            item = new Resource
            {
                Id = Guid.NewGuid().ToString(),
                Variant = resourceVariant,
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? resourceVariant
                    : displayName,
                Type = "Mineral",
                Category = "Resource",
                IsStackable = isStackable,
                MaxStackAmount = maxStackAmount
            };
            error = null;
            return true;
        }
    }
}
