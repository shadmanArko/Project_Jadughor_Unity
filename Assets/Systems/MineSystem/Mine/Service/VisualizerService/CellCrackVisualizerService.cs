using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    [Serializable]
    public class CellCrackVisualizerService : IInitializable, IDisposable
    {
        private readonly MineView _mineView;
        private readonly CellCrackScriptable _cellCrackScriptable;
        private readonly MinePlayerScriptable _playerScriptable;
        private readonly Dictionary<(Direction, CellCrackSize), Tile>
            _crackTiles = new();
        private CompositeDisposable _disposable;

        public CellCrackVisualizerService(
            MineView mineView,
            CellCrackScriptable cellCrackScriptable,
            MinePlayerScriptable playerScriptable)
        {
            _mineView = mineView;
            _cellCrackScriptable = cellCrackScriptable;
            _playerScriptable = playerScriptable;
        }
        
        public void Initialize()
        {
            _disposable = new CompositeDisposable();
            CacheCrackTiles();
        }

        private void CacheCrackTiles()
        {
            var crackData = _cellCrackScriptable?.cellCrackSpriteDatas?
                .FirstOrDefault(data =>
                    data.region == _playerScriptable.region &&
                    data.site == _playerScriptable.site);

            if (crackData == null)
            {
                Debug.LogError(
                    $"Cell crack data is missing for region {_playerScriptable.region} and site {_playerScriptable.site}.");
                return;
            }

            foreach (Direction direction in
                     System.Enum.GetValues(typeof(Direction)))
            {
                var directionalData = crackData.cellCrackSpriteDataList?
                    .FirstOrDefault(data => data.direction == direction);

                if (directionalData == null)
                {
                    Debug.LogError(
                        $"Cell crack data for direction '{direction}' is missing for region {crackData.region} and site {crackData.site}.");
                    continue;
                }

                foreach (CellCrackSize size in
                         System.Enum.GetValues(typeof(CellCrackSize)))
                {
                    CacheCrackTile(
                        crackData,
                        directionalData,
                        direction,
                        size);
                }
            }
        }

        private void CacheCrackTile(
            CellCrackData crackData,
            DirectionalCellCrackSpriteData directionalData,
            Direction direction,
            CellCrackSize size)
        {
            var spriteData = directionalData.crackSpriteDataList?
                .FirstOrDefault(data => data.size == size);

            if (spriteData?.sprite?.objectSprite == null)
            {
                Debug.LogError(
                    $"Cell crack sprite for direction '{direction}' and size '{size}' is missing for region {crackData.region} and site {crackData.site}.");
                return;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = spriteData.sprite.objectSprite;
            _crackTiles[(direction, size)] = tile;
        }

        public void UpdateCellWallCrack(Cell cell)
        {
            if (cell == null)
                return;

            var crackSize = GetCrackSize(cell);
            var direction = cell.LatestImpactDirection ?? Direction.Left;
            if (crackSize == null ||
                !_crackTiles.TryGetValue(
                    (direction, crackSize.Value),
                    out var crackTile))
            {
                _mineView.cellCrackTilemap.SetTile(cell.GetPosition(), null);
                return;
            }

            _mineView.cellCrackTilemap.SetTile(cell.GetPosition(), crackTile);
        }

        public void RefreshCellCracks(MineData mineData)
        {
            _mineView.cellCrackTilemap.ClearAllTiles();

            if (mineData?.Cells == null)
                return;

            foreach (var cell in mineData.Cells)
                UpdateCellWallCrack(cell);
        }

        private static CellCrackSize? GetCrackSize(Cell cell)
        {
            if (!cell.IsBreakable || cell.IsBlank || cell.IsBroken ||
                cell.MaxHitPoint <= 0 || cell.HitPoint <= 0 || cell.HitPoint >= cell.MaxHitPoint)
            {
                return null;
            }

            var scaledHitPoint = (long)cell.HitPoint * 4;
            var maxHitPoint = (long)cell.MaxHitPoint;

            if (scaledHitPoint <= maxHitPoint)
                return CellCrackSize.Large;

            if (scaledHitPoint <= maxHitPoint * 2)
                return CellCrackSize.Medium;

            if (scaledHitPoint <= maxHitPoint * 3)
                return CellCrackSize.Small;

            return null;
        }

        public void Dispose()
        {
            _disposable?.Dispose();

            foreach (var tile in _crackTiles.Values)
            {
                if (tile != null)
                    UnityEngine.Object.Destroy(tile);
            }

            _crackTiles.Clear();
        }
    }
}
