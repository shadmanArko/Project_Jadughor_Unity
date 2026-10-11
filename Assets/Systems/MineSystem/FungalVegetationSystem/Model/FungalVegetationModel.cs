using System;
using System.Collections.Generic;
using Systems.MineSystem.FungalVegetationSystem.Config;
using Systems.MineSystem.FungalVegetationSystem.Service;
using Systems.MineSystem.Mine.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Model
{
    /// <summary>
    /// All mutable state for decorative fungal growth. Knows nothing about tilemaps -
    /// it publishes placements and the controller renders them.
    /// </summary>
    /// <remarks>
    /// Growth is driven by a maturation queue rather than by scanning the broken-cell list
    /// each tick. Because every entry is stamped with a monotonic elapsed-minute value plus
    /// a constant delay, the queue is sorted by construction, so draining the ripe front is
    /// amortised O(newly matured) and each cell gets exactly one roll. That also gives a hard
    /// minimum age, which a per-tick reroll cannot: rerolling is memoryless, so growths could
    /// appear the instant after a pickaxe swing, and cells would reroll forever until the
    /// whole mine saturated.
    /// </remarks>
    [Serializable]
    public sealed class FungalVegetationModel : IFungalVegetationModel, IDisposable
    {
        private readonly FungalGrowthPlacementService _placementService;
        private readonly FungalVegetationConfig _config;

        private readonly Queue<PendingFungalGrowth> _pending = new(512);

        // Cells that matured while the player was looking at them. They have NOT spent their
        // growth roll - they are re-tested every tick and grow once the camera moves away.
        // Self-draining, and bounded by the total broken-cell count.
        private readonly List<PendingFungalGrowth> _cameraBlocked = new(64);

        private readonly Dictionary<Vector3Int, FungalGrowthRecord> _growthsByCell =
            new(256);

        private readonly Subject<FungalGrowthPlacement> _onGrowthPlaced = new();
        private readonly Subject<FungalGrowthPlacement> _onGrowthRemoved = new();

        private int _elapsedMinutes;
        private bool _disposed;

        public FungalVegetationModel(
            FungalGrowthPlacementService placementService,
            FungalVegetationConfig config)
        {
            _placementService = placementService;
            _config = config;
        }

        public IObservable<FungalGrowthPlacement> OnGrowthPlaced => _onGrowthPlaced;
        public IObservable<FungalGrowthPlacement> OnGrowthRemoved => _onGrowthRemoved;
        public int GrowthCount => _growthsByCell.Count;

        public void ResetForMine(
            IReadOnlyList<GridPosition> brokenCells,
            HashSet<string> excludedCellIds)
        {
            if (_disposed)
                return;

            _pending.Clear();
            _cameraBlocked.Clear();
            _growthsByCell.Clear();
            _elapsedMinutes = 0;
            _placementService.SetExcludedCellIds(excludedCellIds);

            if (brokenCells == null)
                return;

            // Cave interiors are already broken when the mine is generated, so they never
            // fire OnCellBroken. Seeding them here is what makes natural caves read as
            // pre-grown - and they sit behind the unrevealed tilemap until discovered, so
            // the player never watches them appear.
            var maturity = Mathf.Max(0, _config.maturationGameMinutes);
            for (var i = 0; i < brokenCells.Count; i++)
            {
                _pending.Enqueue(new PendingFungalGrowth(
                    brokenCells[i].ToVector3Int(),
                    maturity,
                    isSeed: true));
            }
        }

        public void RegisterBrokenCell(Vector3Int position)
        {
            if (_disposed)
                return;

            _pending.Enqueue(new PendingFungalGrowth(
                position,
                _elapsedMinutes + Mathf.Max(0, _config.maturationGameMinutes),
                isSeed: false));
        }

        public void AdvanceTime(int gameMinutes)
        {
            if (_disposed)
                return;

            _elapsedMinutes += Mathf.Max(1, gameMinutes);

            // The budget caps growths, not dequeues, so cells that fail the roll drain
            // immediately while actual growths stay staggered across ticks. Both passes share
            // it, and previously-blocked cells get first claim so they cannot starve.
            var budget = Mathf.Max(1, _config.maxGrowthsPerTick);
            var grown = RetryCameraBlocked(budget);
            DrainMatured(budget, ref grown);
        }

        /// <summary>
        /// Re-tests the cells that were passed over for being on screen. Compacts survivors in
        /// place with a read/write index so the pass allocates nothing and keeps oldest-first
        /// order.
        /// </summary>
        private int RetryCameraBlocked(int budget)
        {
            if (_cameraBlocked.Count == 0)
                return 0;

            var grown = 0;
            var write = 0;

            for (var read = 0; read < _cameraBlocked.Count; read++)
            {
                var pending = _cameraBlocked[read];

                // Out of budget: keep the rest for the next tick untouched.
                if (grown >= budget)
                {
                    _cameraBlocked[write++] = pending;
                    continue;
                }

                var outcome = Resolve(pending);
                if (outcome == FungalGrowthOutcome.CameraBlocked)
                {
                    _cameraBlocked[write++] = pending;
                    continue;
                }

                // Placed or Rejected - either way the roll is spent, so stop holding it.
                if (outcome == FungalGrowthOutcome.Placed)
                    grown++;
            }

            _cameraBlocked.RemoveRange(write, _cameraBlocked.Count - write);
            return grown;
        }

        private void DrainMatured(int budget, ref int grown)
        {
            // Bounds how many cells can migrate into the retry list in one tick - relevant on
            // the tick where the whole cave pre-seed matures while the player stands in a cave.
            var maxScans = Mathf.Max(1, _config.maxCandidateScansPerTick);
            var scans = 0;

            while (grown < budget &&
                   scans < maxScans &&
                   _pending.Count > 0 &&
                   _pending.Peek().MaturityMinute <= _elapsedMinutes)
            {
                scans++;
                var pending = _pending.Dequeue();

                switch (Resolve(pending))
                {
                    case FungalGrowthOutcome.Placed:
                        grown++;
                        break;
                    case FungalGrowthOutcome.CameraBlocked:
                        _cameraBlocked.Add(pending);
                        break;
                }
            }
        }

        private FungalGrowthOutcome Resolve(PendingFungalGrowth pending)
        {
            if (_config.maxTotalGrowths > 0 &&
                _growthsByCell.Count >= _config.maxTotalGrowths)
                return FungalGrowthOutcome.Rejected;

            var chance = pending.IsSeed
                ? _config.caveSeedChance
                : _config.growthChance;

            var outcome = _placementService.TryResolveGrowth(
                pending.Position,
                chance,
                _growthsByCell,
                out var layer0,
                out var layer1);

            if (outcome != FungalGrowthOutcome.Placed)
                return outcome;

            _growthsByCell[pending.Position] =
                new FungalGrowthRecord(layer0, layer1);

            if (layer0.HasGrowth)
                _onGrowthPlaced.OnNext(
                    new FungalGrowthPlacement(pending.Position, layer0, 0));

            if (layer1.HasGrowth)
                _onGrowthPlaced.OnNext(
                    new FungalGrowthPlacement(pending.Position, layer1, 1));

            return FungalGrowthOutcome.Placed;
        }

        public void RemoveGrowthsAnchoredTo(Vector3Int wallPosition)
        {
            if (_disposed || _growthsByCell.Count == 0)
                return;

            // Inverting the relationship here - probing the broken wall's four neighbours -
            // avoids maintaining a second anchor-to-cells index. One wall can anchor up to
            // four different neighbours, so that index would need a List per entry.
            var offsets = FungalGrowthPlacementService.CardinalOffsets;
            for (var i = 0; i < offsets.Length; i++)
            {
                var cell = wallPosition + offsets[i];
                if (!_growthsByCell.TryGetValue(cell, out var record))
                    continue;

                var changed = false;
                for (var layer = 0; layer < 2; layer++)
                {
                    var growth = record.GetLayer(layer);
                    if (!growth.HasGrowth || growth.AnchorCell != wallPosition)
                        continue;

                    _onGrowthRemoved.OnNext(
                        new FungalGrowthPlacement(cell, growth, layer));
                    record = record.WithoutLayer(layer);
                    changed = true;
                }

                if (!changed)
                    continue;

                if (record.HasAny)
                    _growthsByCell[cell] = record;
                else
                    _growthsByCell.Remove(cell);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pending.Clear();
            _cameraBlocked.Clear();
            _growthsByCell.Clear();
            _onGrowthPlaced.Dispose();
            _onGrowthRemoved.Dispose();
        }
    }
}
