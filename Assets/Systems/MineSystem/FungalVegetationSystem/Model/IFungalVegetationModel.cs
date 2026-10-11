using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Model
{
    public interface IFungalVegetationModel
    {
        /// <summary>A growth has been decided on and should be drawn.</summary>
        IObservable<FungalGrowthPlacement> OnGrowthPlaced { get; }

        /// <summary>A growth's anchor wall broke and it should be erased.</summary>
        IObservable<FungalGrowthPlacement> OnGrowthRemoved { get; }

        int GrowthCount { get; }

        /// <summary>
        /// Drops all state and seeds the maturation queue from the cells that were already
        /// broken at generation time (cave interiors and the entrance).
        /// </summary>
        /// <param name="brokenCells">MineModel.BrokenCellPositions.</param>
        /// <param name="excludedCellIds">
        /// Cells a gameplay prop already occupies - the cave stalactite/stalagmite formations.
        /// </param>
        void ResetForMine(
            IReadOnlyList<GridPosition> brokenCells,
            HashSet<string> excludedCellIds);

        /// <summary>Queues a freshly broken cell for its growth roll.</summary>
        void RegisterBrokenCell(Vector3Int position);

        /// <summary>Advances the in-game clock and grows whatever has matured.</summary>
        void AdvanceTime(int gameMinutes);

        /// <summary>
        /// Erases every growth clinging to <paramref name="wallPosition"/>, which has just
        /// stopped being solid rock.
        /// </summary>
        void RemoveGrowthsAnchoredTo(Vector3Int wallPosition);
    }
}
