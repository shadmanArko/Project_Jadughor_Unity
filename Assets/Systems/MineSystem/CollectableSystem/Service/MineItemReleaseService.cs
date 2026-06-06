using System;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.CollectableSystem.Service
{
    public sealed class MineItemReleaseService : IInitializable, IDisposable
    {
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly CollectableFactory _factory;
        private readonly CompositeDisposable _disposables = new();

        public MineItemReleaseService(
            MineModel mine,
            MineView mineView,
            CollectableFactory factory)
        {
            _mine = mine;
            _mineView = mineView;
            _factory = factory;
        }

        public void Initialize()
        {
            _mine.OnCellBroken
                .Subscribe(ReleaseCellContents)
                .AddTo(_disposables);
        }

        private void ReleaseCellContents(Cell cell)
        {
            var mineData = _mine.MineData.Value;
            if (mineData == null)
                return;

            var position = _mineView.wallTileMap.GetCellCenterWorld(
                cell.Position.ToVector3Int());
            var resource = mineData.GetResource(cell.Id);
            if (resource != null)
            {
                if (TrySpawn(resource, position))
                {
                    mineData.Resources.Remove(resource);
                    resource.CellId = null;
                    resource.Position = default;
                    cell.HasResource = false;
                    cell.ItemId = null;
                    Commit(mineData, cell);
                }

                return;
            }

            var artifact = mineData.GetArtifact(cell.Id);
            if (artifact != null)
            {
                if (TrySpawn(artifact, position))
                {
                    var placement = mineData.GetArtifactPlacement(cell.Id);
                    mineData.Artifacts.Remove(artifact);
                    if (placement != null)
                        mineData.ArtifactPlacements.Remove(placement);

                    cell.HasArtifact = false;
                    cell.ItemId = null;
                    Commit(mineData, cell);
                }

                return;
            }

            var cellPlaceable = mineData.GetCellPlaceable(cell.Id);
            var wallPlaceable = FindAnchoredWallPlaceable(mineData, cell.Id);
            if (cellPlaceable != null)
            {
                if (TrySpawn(cellPlaceable, position))
                {
                    mineData.CellPlaceables.Remove(cellPlaceable);
                    cellPlaceable.OccupiedCellId = null;
                    cellPlaceable.Position = default;
                    cell.HasCellPlaceable = false;
                    if (cell.ItemId == cellPlaceable.Id)
                        cell.ItemId = null;
                    Commit(mineData, cell);
                }
            }

            if (wallPlaceable != null)
            {
                if (TrySpawn(wallPlaceable, position))
                {
                    mineData.WallPlaceables.Remove(wallPlaceable);
                    ClearWallPlaceableCells(mineData, wallPlaceable);
                    wallPlaceable.AnchorCellId = null;
                    wallPlaceable.OccupiedCellIds?.Clear();
                    wallPlaceable.Position = default;
                    mineData.InitializeLookupCache();
                }
            }
        }

        private bool TrySpawn(
            InventorySystem.Model.Item item,
            Vector3 position)
        {
            if (!_factory.CanSpawn(item))
            {
                Debug.LogWarning(
                    $"Collectable spawn is not configured for " +
                    $"'{item.GetType().Name}' variant '{item.Variant}'.");
                return false;
            }

            return _factory.TrySpawn(item, position);
        }

        private void Commit(MineData mineData, Cell cell)
        {
            mineData.InitializeLookupCache();
            _mine.NotifyCellModified(cell);
        }

        private static WallPlaceable FindAnchoredWallPlaceable(
            MineData mineData,
            string cellId)
        {
            if (mineData.WallPlaceables == null)
                return null;

            for (var i = 0; i < mineData.WallPlaceables.Count; i++)
            {
                var placeable = mineData.WallPlaceables[i];
                if (placeable.AnchorCellId == cellId)
                    return placeable;
            }

            return null;
        }

        private void ClearWallPlaceableCells(
            MineData mineData,
            WallPlaceable placeable)
        {
            if (placeable.OccupiedCellIds == null)
                return;

            for (var i = 0; i < placeable.OccupiedCellIds.Count; i++)
            {
                var occupiedCellId = placeable.OccupiedCellIds[i];
                Cell occupiedCell = null;
                for (var j = 0; j < mineData.Cells.Count; j++)
                {
                    if (mineData.Cells[j].Id != occupiedCellId)
                        continue;

                    occupiedCell = mineData.Cells[j];
                    break;
                }

                if (occupiedCell == null)
                    continue;

                occupiedCell.HasWallPlaceable = false;
                if (occupiedCell.ItemId == placeable.Id)
                    occupiedCell.ItemId = null;
                _mine.NotifyCellModified(occupiedCell);
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
