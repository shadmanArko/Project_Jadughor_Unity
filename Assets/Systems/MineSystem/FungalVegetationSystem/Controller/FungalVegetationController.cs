using System;
using System.Collections.Generic;
using Systems.MineSystem.DayAndTimeSystem.Configs;
using Systems.MineSystem.DayAndTimeSystem.Signals;
using Systems.MineSystem.FungalVegetationSystem.Model;
using Systems.MineSystem.FungalVegetationSystem.Service;
using Systems.MineSystem.FungalVegetationSystem.Signal;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Signal;
using Systems.MineSystem.Mine.View;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem.FungalVegetationSystem.Controller
{
    /// <summary>
    /// Wires the clock and the mine to the fungal model, and renders the model's
    /// placements onto the two fungal tilemaps on <see cref="MineView"/>.
    /// </summary>
    [Serializable]
    public sealed class FungalVegetationController : IInitializable, IDisposable
    {
        private readonly MineView _mineView;
        private readonly MineModel _mineModel;
        private readonly IFungalVegetationModel _model;
        private readonly FungalTileCacheService _tileCache;
        private readonly DayAndTimeConfig _timeConfig;

        private CompositeDisposable _disposables;
        private bool _warnedMissingSecondaryTilemap;

        public FungalVegetationController(
            MineView mineView,
            MineModel mineModel,
            IFungalVegetationModel model,
            FungalTileCacheService tileCache,
            DayAndTimeConfig timeConfig)
        {
            _mineView = mineView;
            _mineModel = mineModel;
            _model = model;
            _tileCache = tileCache;
            _timeConfig = timeConfig;
        }

        public void Initialize()
        {
            _disposables = new CompositeDisposable();

            _model.OnGrowthPlaced
                .Subscribe(OnGrowthPlaced)
                .AddTo(_disposables);
            _model.OnGrowthRemoved
                .Subscribe(OnGrowthRemoved)
                .AddTo(_disposables);

            GlobalEventBus.OnSignal<MineGeneratedSignal>()
                .Subscribe(signal => ResetForMine(signal.MineData))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<MinuteEndSignal>()
                .Subscribe(_ => _model.AdvanceTime(_timeConfig.minuteStep))
                .AddTo(_disposables);

            _mineModel.OnCellBroken
                .Subscribe(OnCellBroken)
                .AddTo(_disposables);

            // MineController generates the mine from its own Initialize, so depending on
            // binding order MineGeneratedSignal may already have fired before we subscribed.
            if (_mineModel.MineData.Value != null)
                ResetForMine(_mineModel.MineData.Value);
        }

        private void ResetForMine(MineData mineData)
        {
            ClearTilemap(_mineView.fungalVegetation);
            ClearTilemap(_mineView.fungalVegetationSecondary);

            _model.ResetForMine(
                _mineModel.BrokenCellPositions,
                BuildFormationCellIds(mineData));
        }

        /// <summary>
        /// Cells occupied by the cave stalactite/stalagmite prefabs. Read straight off the
        /// cave data so nothing needs to couple to CaveVisualizerService.
        /// </summary>
        private static HashSet<string> BuildFormationCellIds(MineData mineData)
        {
            var cellIds = new HashSet<string>();
            if (mineData?.Caves == null)
                return cellIds;

            foreach (var cave in mineData.Caves)
            {
                if (cave == null)
                    continue;

                AddRange(cellIds, cave.StalactiteCellIds);
                AddRange(cellIds, cave.StalagmiteCellIds);
            }

            return cellIds;
        }

        private static void AddRange(HashSet<string> target, List<string> cellIds)
        {
            if (cellIds == null)
                return;

            foreach (var cellId in cellIds)
            {
                if (!string.IsNullOrEmpty(cellId))
                    target.Add(cellId);
            }
        }

        private void OnCellBroken(Cell cell)
        {
            if (cell == null)
                return;

            var position = cell.GetPosition();

            // Order matters: the wall first loses whatever was clinging to it, and only then
            // becomes a growth candidate in its own right.
            _model.RemoveGrowthsAnchoredTo(position);
            _model.RegisterBrokenCell(position);
        }

        private void OnGrowthPlaced(FungalGrowthPlacement placement)
        {
            var tilemap = TilemapFor(placement.Layer);
            if (tilemap == null)
                return;

            var tile = _tileCache.GetTile(placement.EntryId);
            if (tile == null)
            {
                Debug.LogWarning(
                    $"No cached fungal tile for entry id '{placement.EntryId}'.");
                return;
            }

            tilemap.SetTile(placement.Cell, tile);

            GlobalEventBus.Fire(new FungalGrowthPlacedSignal
            {
                Cell = placement.Cell,
                AnchorCell = placement.AnchorCell,
                Anchor = placement.Anchor,
                EntryId = placement.EntryId,
                Layer = placement.Layer
            });
        }

        private void OnGrowthRemoved(FungalGrowthPlacement placement)
        {
            var tilemap = TilemapFor(placement.Layer);
            if (tilemap == null)
                return;

            tilemap.SetTile(placement.Cell, null);

            GlobalEventBus.Fire(new FungalGrowthRemovedSignal
            {
                Cell = placement.Cell,
                AnchorCell = placement.AnchorCell,
                Anchor = placement.Anchor,
                EntryId = placement.EntryId,
                Layer = placement.Layer
            });
        }

        /// <summary>
        /// Layer 1 falls back to the primary tilemap when no secondary is wired, so the
        /// feature still works before the prefab gains its second tilemap. Paired growths
        /// then overwrite each other in that cell, which is why it warns.
        /// </summary>
        private Tilemap TilemapFor(int layer)
        {
            if (layer == 0)
                return _mineView.fungalVegetation;

            if (_mineView.fungalVegetationSecondary != null)
                return _mineView.fungalVegetationSecondary;

            if (!_warnedMissingSecondaryTilemap)
            {
                _warnedMissingSecondaryTilemap = true;
                Debug.LogWarning(
                    "MineView.fungalVegetationSecondary is not assigned - paired fungal " +
                    "growths will collapse onto the primary tilemap. Add the second " +
                    "tilemap under MineGrid in MineView.prefab.");
            }

            return _mineView.fungalVegetation;
        }

        private static void ClearTilemap(Tilemap tilemap) =>
            tilemap?.ClearAllTiles();

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}
