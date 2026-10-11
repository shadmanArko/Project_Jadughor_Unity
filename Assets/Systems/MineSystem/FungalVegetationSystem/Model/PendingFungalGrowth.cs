using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Model
{
    /// <summary>
    /// A broken cell waiting for its single growth roll.
    /// </summary>
    /// <remarks>
    /// Every instance is created with a monotonically increasing elapsed-minute value
    /// plus a constant delay, so a queue of these is sorted by <see cref="MaturityMinute"/>
    /// purely by construction - which is what lets the model drain the ripe front instead
    /// of scanning every broken cell each tick. Adding per-cell jitter to the delay would
    /// break that invariant silently.
    /// </remarks>
    public readonly struct PendingFungalGrowth
    {
        public PendingFungalGrowth(
            Vector3Int position,
            int maturityMinute,
            bool isSeed)
        {
            Position = position;
            MaturityMinute = maturityMinute;
            IsSeed = isSeed;
        }

        public Vector3Int Position { get; }

        /// <summary>Elapsed in-game minute at which this cell becomes eligible.</summary>
        public int MaturityMinute { get; }

        /// <summary>
        /// True for cells that were already broken when the mine was generated (cave
        /// interiors), which roll caveSeedChance rather than growthChance.
        /// </summary>
        public bool IsSeed { get; }
    }
}
