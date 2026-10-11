using System.Collections.Generic;
using Systems.MineSystem.BossLairSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Scriptable
{
    /// <summary>
    /// Decor tiles available to the boss lair's light randomization pass. Kept
    /// separate from the arena prefab so the same prop set can be shared across
    /// lairs and retuned without touching authored geometry.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BossLairDecor",
        menuName = "Boss/Boss Lair Decor")]
    public sealed class BossLairDecorScriptable : ScriptableObject
    {
        [Tooltip("Weighted decor tiles scattered on the arena floor.")]
        [SerializeField] private List<BossLairDecorEntry> entries = new();

        public IReadOnlyList<BossLairDecorEntry> Entries => entries;

        public float TotalWeight
        {
            get
            {
                var total = 0f;
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry?.tile != null)
                        total += Mathf.Max(0f, entry.weight);
                }
                return total;
            }
        }

        public bool HasUsableEntry => TotalWeight > 0f;
    }
}
