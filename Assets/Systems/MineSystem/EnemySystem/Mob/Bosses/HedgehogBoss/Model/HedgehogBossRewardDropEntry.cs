using System;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Scriptable;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model
{
    /// <summary>
    /// One configurable reward roll. Guaranteed rows always spawn; every other
    /// row is rolled independently so several bonus items may drop together.
    /// </summary>
    [Serializable]
    public sealed class HedgehogBossRewardDropEntry
    {
        [Tooltip("Definition used to create the full runtime item spawned into the arena.")]
        [SerializeField] private BossRewardItemDefinitionScriptable item;
        [Tooltip("Number of this item created when this drop row succeeds.")]
        [Min(1)] [SerializeField] private int quantity = 1;
        [Tooltip("When enabled, this row always drops and ignores Drop Chance Percent.")]
        [SerializeField] private bool isGuaranteed;
        [Tooltip("Independent percentage roll for an optional drop row. Ignored for guaranteed rows.")]
        [Range(0f, 100f)] [SerializeField] private float dropChancePercent = 100f;

        public BossRewardItemDefinitionScriptable Item => item;
        public int Quantity => quantity;
        public bool IsGuaranteed => isGuaranteed;
        public float DropChancePercent => dropChancePercent;

        public bool Validate(out string error)
        {
            if (item == null)
            {
                error = "requires an item definition.";
                return false;
            }
            if (quantity < 1)
            {
                error = "requires a quantity of at least 1.";
                return false;
            }
            if (!isGuaranteed && (dropChancePercent <= 0f ||
                                   dropChancePercent > 100f))
            {
                error = "optional drop chance must be in (0, 100].";
                return false;
            }
            error = null;
            return true;
        }
    }
}
