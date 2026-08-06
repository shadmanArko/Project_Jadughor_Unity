using System;
using System.Collections.Generic;
using Systems.MineSystem.FungalVegetationSystem.Config;
using Systems.MineSystem.FungalVegetationSystem.Enum;
using Systems.MineSystem.FungalVegetationSystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.FungalVegetationSystem.Service
{
    /// <summary>
    /// Decides whether a matured cell grows anything, and if so what and where.
    /// All the eligibility rules live here so the model can stay about state.
    /// </summary>
    public sealed class FungalGrowthPlacementService : IInitializable, IDisposable
    {
        private const int AnchorCount = 4;

        /// <summary>
        /// Offset from the decorated cell to the solid cell each anchor requires.
        /// Indexed by <see cref="FungalAnchor"/>, so the order must match that enum.
        /// </summary>
        private static readonly Vector3Int[] AnchorOffsets =
        {
            Vector3Int.down,  // Floor
            Vector3Int.up,    // Ceiling
            Vector3Int.left,  // LeftWall
            Vector3Int.right  // RightWall
        };

        public static readonly Vector3Int[] CardinalOffsets =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left
        };

        private readonly MineModel _mineModel;
        private readonly MineView _mineView;
        private readonly Camera _camera;
        private readonly FungalVegetationConfig _config;
        private readonly System.Random _random = new();

        // Built once from the config so the per-tick path never touches LINQ.
        private readonly List<FungalVegetationEntry>[] _entriesByAnchor =
            new List<FungalVegetationEntry>[AnchorCount];
        private readonly int[] _entryWeightTotals = new int[AnchorCount];

        // Reused every roll, so picking an anchor allocates nothing.
        private readonly FungalAnchor[] _eligibleBuffer = new FungalAnchor[AnchorCount];

        private HashSet<string> _excludedCellIds = new();

        public FungalGrowthPlacementService(
            MineModel mineModel,
            MineView mineView,
            Camera camera,
            FungalVegetationConfig config)
        {
            _mineModel = mineModel;
            _mineView = mineView;
            _camera = camera;
            _config = config;
        }

        public void Initialize()
        {
            for (var i = 0; i < AnchorCount; i++)
                _entriesByAnchor[i] = new List<FungalVegetationEntry>();

            if (!_config.Validate(out var error))
            {
                Debug.LogError($"FungalVegetationConfig is invalid: {error}");
                return;
            }

            foreach (var entry in _config.vegetationEntries)
            {
                if (entry.weight <= 0)
                    continue;

                var index = (int)entry.anchor;
                _entriesByAnchor[index].Add(entry);
                _entryWeightTotals[index] += entry.weight;
            }
        }

        /// <summary>
        /// Cells that must never be decorated because a gameplay prop already occupies
        /// them - the cave stalactite/stalagmite formations. Rebuilt per mine.
        /// </summary>
        public void SetExcludedCellIds(HashSet<string> excludedCellIds)
        {
            _excludedCellIds = excludedCellIds ?? new HashSet<string>();
        }

        /// <summary>
        /// Rolls for growth in <paramref name="cell"/>. Returns false if the cell is
        /// ineligible, the chance roll fails, or no anchor has a solid neighbour.
        /// </summary>
        /// <param name="chance">growthChance, or caveSeedChance for generation-time cells.</param>
        public FungalGrowthOutcome TryResolveGrowth(
            Vector3Int cell,
            float chance,
            IReadOnlyDictionary<Vector3Int, FungalGrowthRecord> existingGrowths,
            out FungalGrowthLayer layer0,
            out FungalGrowthLayer layer1)
        {
            layer0 = FungalGrowthLayer.Empty;
            layer1 = FungalGrowthLayer.Empty;

            // Cheapest test first, and it is the one that identifies permanently hopeless
            // cells - keeping them out of the camera-blocked retry list.
            if (!IsDecorable(cell, existingGrowths))
                return FungalGrowthOutcome.Rejected;

            // Deliberately BEFORE the chance roll. If the roll came first, a cell that passed
            // it and was then camera-blocked would roll again on every retry, inflating the
            // effective growth chance in exactly the areas the player lingers in. Gating
            // first means a held cell has not yet rolled, so it rolls exactly once - when it
            // finally grows off screen.
            if (!_config.allowGrowthInsideCameraBounds && IsInsideCameraBounds(cell))
                return FungalGrowthOutcome.CameraBlocked;

            if (_random.NextDouble() >= chance)
                return FungalGrowthOutcome.Rejected;

            var eligibleCount = CollectEligibleAnchors(cell);
            if (eligibleCount == 0)
                return FungalGrowthOutcome.Rejected;

            if (!TryPickAnchor(eligibleCount, out var primaryAnchor) ||
                !TryPickEntry(primaryAnchor, out var primaryEntry))
                return FungalGrowthOutcome.Rejected;

            var primary = BuildGrowth(cell, primaryAnchor, primaryEntry);

            // A second growth is only safe on a DIFFERENT anchor: the sprite canvases are
            // authored so that left (x0-5), right (x14-19), ceiling (y15-19) and floor
            // (y0-10) regions never overlap, but two variants sharing an anchor do.
            var secondary = FungalGrowthLayer.Empty;
            if (eligibleCount > 1 &&
                _random.NextDouble() < _config.secondGrowthChance &&
                TryPickAnchorExcluding(eligibleCount, primaryAnchor, out var secondAnchor) &&
                TryPickEntry(secondAnchor, out var secondEntry))
            {
                secondary = BuildGrowth(cell, secondAnchor, secondEntry);
            }

            // Which tilemap receives which growth is cosmetically irrelevant (both sit on
            // the same sorting layer and the sprites never overlap), but randomising it
            // keeps a lone growth from always living on the same tilemap.
            if (_random.Next(2) == 0)
            {
                layer0 = primary;
                layer1 = secondary;
            }
            else
            {
                layer0 = secondary;
                layer1 = primary;
            }

            return FungalGrowthOutcome.Placed;
        }

        /// <summary>
        /// True when this cell is currently visible to the player, so growing there would be
        /// a visible pop-in.
        /// </summary>
        /// <remarks>
        /// Mirrors EnemySpawnLocator.IsVisibilityValid, which already solves exactly this for
        /// EnemySpawnVisibilityRule.OutsideCameraViewport.
        ///
        /// WorldToViewportPoint rather than a hand-built orthographicSize/aspect rect centred
        /// on the camera transform: the Camera prefab carries a CinemachineBrain (whose
        /// LensModeOverride pushes the vcam lens onto the camera) AND a PixelPerfectCamera
        /// (which rewrites orthographicSize every frame to snap to the PPU grid), so the
        /// authored ortho size is not what runs. Projecting through the camera reads whatever
        /// those writers actually settled on, and stays truthful during transition pans where
        /// the camera is unfollowed and tweened directly.
        ///
        /// Note this is NOT MineView.cameraBoundaryCollider - that is the Cinemachine confiner
        /// bounding shape, sized to the whole mine, i.e. where the camera may travel rather
        /// than what it can see.
        /// </remarks>
        public bool IsInsideCameraBounds(Vector3Int cell)
        {
            // Fail open: with no camera or grid to consult, do not block growth forever.
            if (_camera == null || _mineView?.grid == null)
                return false;

            // An unrevealed cell sits under the Unrevealed tilemap, the topmost sorting layer,
            // so it cannot be seen wherever the camera is pointing. Without this, standing in
            // a cave would defer that cave's whole pre-seed pass.
            if (!_mineModel.TryGetCell(new GridPosition(cell.x, cell.y), out var target) ||
                !target.IsRevealed)
                return false;

            var world = _mineView.grid.GetCellCenterWorld(cell);
            var viewport = _camera.WorldToViewportPoint(world);

            // Margin is authored in tiles, so convert it to normalised viewport units. Cell
            // size comes from CellToWorld differences rather than grid.cellSize to stay
            // correct under a scaled grid.
            var marginTiles = Mathf.Max(0, _config.cameraBoundsMarginCells);
            var cellOrigin = _mineView.grid.CellToWorld(Vector3Int.zero);
            var cellRight = _mineView.grid.CellToWorld(Vector3Int.right);
            var cellUp = _mineView.grid.CellToWorld(Vector3Int.up);
            var cellWidth = Mathf.Abs(cellRight.x - cellOrigin.x);
            var cellHeight = Mathf.Abs(cellUp.y - cellOrigin.y);
            var verticalSize = Mathf.Max(0.0001f, _camera.orthographicSize * 2f);
            var horizontalSize = Mathf.Max(0.0001f, verticalSize * _camera.aspect);
            var marginX = marginTiles * cellWidth / horizontalSize;
            var marginY = marginTiles * cellHeight / verticalSize;

            return viewport.z > 0f &&
                   viewport.x >= -marginX && viewport.x <= 1f + marginX &&
                   viewport.y >= -marginY && viewport.y <= 1f + marginY;
        }

        private FungalGrowthLayer BuildGrowth(
            Vector3Int cell,
            FungalAnchor anchor,
            FungalVegetationEntry entry) =>
            new(cell + AnchorOffsets[(int)anchor], anchor, entry.id);

        /// <summary>
        /// A cell can host a growth if it is open space the player can see into, is not
        /// already decorated, and - when spacing is on - has no decorated neighbour.
        /// </summary>
        public bool IsDecorable(
            Vector3Int cell,
            IReadOnlyDictionary<Vector3Int, FungalGrowthRecord> existingGrowths)
        {
            if (!_mineModel.TryGetCell(new GridPosition(cell.x, cell.y), out var target))
                return false;

            if (!target.IsBroken || target.IsBlank)
                return false;

            if (target.HasCellPlaceable || target.HasWallPlaceable)
                return false;

            if (!string.IsNullOrEmpty(target.Id) &&
                _excludedCellIds.Contains(target.Id))
                return false;

            if (existingGrowths.ContainsKey(cell))
                return false;

            if (!_config.enforceSpacing)
                return true;

            for (var i = 0; i < CardinalOffsets.Length; i++)
            {
                if (existingGrowths.ContainsKey(cell + CardinalOffsets[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Fills <see cref="_eligibleBuffer"/> with the anchors whose required neighbour is
        /// solid rock and which actually have variants authored. Returns how many.
        /// </summary>
        private int CollectEligibleAnchors(Vector3Int cell)
        {
            var count = 0;
            for (var i = 0; i < AnchorCount; i++)
            {
                if (_entryWeightTotals[i] <= 0)
                    continue;

                if (!IsSolidWall(cell + AnchorOffsets[i]))
                    continue;

                _eligibleBuffer[count++] = (FungalAnchor)i;
            }

            return count;
        }

        /// <summary>
        /// Solid rock, i.e. something a growth can cling to. Out-of-bounds returns false
        /// because TryGetCell yields no cell there, and the single blank entrance cell is
        /// excluded explicitly.
        /// </summary>
        private bool IsSolidWall(Vector3Int position)
        {
            if (!_mineModel.TryGetCell(
                    new GridPosition(position.x, position.y),
                    out var cell))
                return false;

            return !cell.IsBroken && !cell.IsBlank;
        }

        private bool TryPickAnchor(int eligibleCount, out FungalAnchor anchor) =>
            TryPickAnchorExcluding(eligibleCount, null, out anchor);

        private bool TryPickAnchorExcluding(
            int eligibleCount,
            FungalAnchor? excluded,
            out FungalAnchor anchor)
        {
            anchor = default;

            var totalWeight = 0;
            for (var i = 0; i < eligibleCount; i++)
            {
                if (excluded.HasValue && _eligibleBuffer[i] == excluded.Value)
                    continue;
                totalWeight += _config.GetAnchorWeight(_eligibleBuffer[i]);
            }

            if (totalWeight <= 0)
                return false;

            var roll = _random.Next(totalWeight);
            var running = 0;
            for (var i = 0; i < eligibleCount; i++)
            {
                var candidate = _eligibleBuffer[i];
                if (excluded.HasValue && candidate == excluded.Value)
                    continue;

                running += _config.GetAnchorWeight(candidate);
                if (roll >= running)
                    continue;

                anchor = candidate;
                return true;
            }

            return false;
        }

        private bool TryPickEntry(FungalAnchor anchor, out FungalVegetationEntry entry)
        {
            entry = null;

            var index = (int)anchor;
            var entries = _entriesByAnchor[index];
            var totalWeight = _entryWeightTotals[index];
            if (entries == null || entries.Count == 0 || totalWeight <= 0)
                return false;

            var roll = _random.Next(totalWeight);
            var running = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                running += entries[i].weight;
                if (roll >= running)
                    continue;

                entry = entries[i];
                return true;
            }

            entry = entries[^1];
            return true;
        }

        public void Dispose()
        {
            for (var i = 0; i < AnchorCount; i++)
                _entriesByAnchor[i]?.Clear();

            _excludedCellIds.Clear();
        }
    }
}
