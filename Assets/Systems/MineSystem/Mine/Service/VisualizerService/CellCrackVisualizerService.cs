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
        private const string SmallCrackId = "small";
        private const string MediumCrackId = "medium";
        private const string LargeCrackId = "large";

        private readonly MineView _mineView;
        private readonly CellCrackScriptable _cellCrackScriptable;
        private readonly MinePlayerScriptable _playerScriptable;
        private readonly Dictionary<string, Tile> _crackTiles = new();
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

            CacheCrackTile(crackData, SmallCrackId);
            CacheCrackTile(crackData, MediumCrackId);
            CacheCrackTile(crackData, LargeCrackId);
        }

        private void CacheCrackTile(CellCrackSpriteData crackData, string crackId)
        {
            var spriteData = crackData.cellCrackSpriteDataList?
                .FirstOrDefault(data => data.id == crackId);

            if (spriteData?.objectSprite == null)
            {
                Debug.LogError(
                    $"Cell crack sprite '{crackId}' is missing for region {crackData.region} and site {crackData.site}.");
                return;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = spriteData.objectSprite;
            _crackTiles[crackId] = tile;
        }

        public void UpdateCellWallCrack(Cell cell)
        {
            if (cell == null)
                return;

            var crackId = GetCrackId(cell);
            if (crackId == null || !_crackTiles.TryGetValue(crackId, out var crackTile))
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

        private static string GetCrackId(Cell cell)
        {
            if (!cell.IsBreakable || cell.IsBlank || cell.IsBroken ||
                cell.MaxHitPoint <= 0 || cell.HitPoint <= 0 || cell.HitPoint >= cell.MaxHitPoint)
            {
                return null;
            }

            var scaledHitPoint = (long)cell.HitPoint * 4;
            var maxHitPoint = (long)cell.MaxHitPoint;

            if (scaledHitPoint <= maxHitPoint)
                return LargeCrackId;

            if (scaledHitPoint <= maxHitPoint * 2)
                return MediumCrackId;

            if (scaledHitPoint <= maxHitPoint * 3)
                return SmallCrackId;

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
