using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Scriptable;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Service
{
    /// <summary>
    /// Turns configured boss drops into ordinary world collectables. Area
    /// classification remains in CollectableController, the source of truth for
    /// lair versus mine pause behavior.
    /// </summary>
    public sealed class HedgehogBossRewardService
    {
        private readonly CollectableFactory _collectables;
        private readonly IArtifactCatalog _artifactCatalog;
        private readonly System.Random _random = new();

        public HedgehogBossRewardService(
            CollectableFactory collectables,
            IArtifactCatalog artifactCatalog)
        {
            _collectables = collectables;
            _artifactCatalog = artifactCatalog;
        }

        public void Drop(HedgehogBossConfigScriptable config, Vector2 origin)
        {
            if (config == null)
                return;

            var drops = config.RewardDrops;
            for (var i = 0; i < drops.Count; i++)
            {
                var entry = drops[i];
                if (entry == null || (!entry.IsGuaranteed && !Roll(entry.DropChancePercent)))
                    continue;

                for (var count = 0; count < entry.Quantity; count++)
                    TryDrop(entry.Item, origin);
            }
        }

        private void TryDrop(BossRewardItemDefinitionScriptable definition, Vector2 origin)
        {
            if (definition == null)
            {
                Debug.LogError("[HedgehogBoss] Reward skipped: item definition is missing.");
                return;
            }
            if (!definition.TryCreateItem(_artifactCatalog, out Item item, out var error))
            {
                Debug.LogError($"[HedgehogBoss] Reward skipped: {error}");
                return;
            }

            if (!_collectables.CanSpawn(item))
            {
                Debug.LogError(
                    $"[HedgehogBoss] Reward '{item.Name}' cannot resolve a collectable sprite.");
                return;
            }

            var angle = (float)(_random.NextDouble() * Mathf.PI * 2f);
            var radius = 0.2f + (float)_random.NextDouble() * 0.45f;
            var position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            _collectables.TrySpawn(item, position);
        }

        private bool Roll(float chancePercent) =>
            chancePercent >= 100f ||
            (chancePercent > 0f && _random.NextDouble() * 100d < chancePercent);
    }
}
