using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Scriptable
{
    /// <summary>Creates one fresh runtime inventory item for a boss reward.</summary>
    public abstract class BossRewardItemDefinitionScriptable : ScriptableObject
    {
        public abstract bool TryCreateItem(
            IArtifactCatalog artifactCatalog,
            out Item item,
            out string error);
    }
}
