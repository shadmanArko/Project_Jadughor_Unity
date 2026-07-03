using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MineGenerationSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    [Serializable]
    public class MineWallVisualizerService : IInitializable, IDisposable
    {
        private static readonly Color WallShadowColor =
            new(0.03f, 0f, 0.07f, 0.62f);
        private static readonly Vector2 WallShadowOffsetInCells =
            new(0.12f, -0.12f);

        private CompositeDisposable _disposable;
        
        private MineView _view;
        private MineRegionalTileScriptable _tileScriptable;
        private MinePlayerScriptable _playerScriptable;
        private bool _hasWarnedMissingWallShadowTileMap;

        private MineRegionalTiles _currentRegionalTiles;

        private Dictionary<GeneralMineTile, Tile> _generalMineTiles;
        private Dictionary<BrokenEdges, Tile> _brokenEdgeTiles;

        public MineWallVisualizerService(
            MineView view,
            MineRegionalTileScriptable scriptable,
            MinePlayerScriptable playerScriptable)
        {
            _view = view;
            _tileScriptable = scriptable;
            _playerScriptable = playerScriptable;
        }

        #region Initializers
        
        public void Initialize()
        {
            _disposable = new CompositeDisposable();
            
            InitializeVariables();
            EnsureWallShadowTileMap();
            CreateTileInstances();
        }

        private void InitializeVariables()
        {
            var region = _playerScriptable.region;
            _currentRegionalTiles = _tileScriptable.regionTiles.FirstOrDefault(tiles => tiles.region == region);

            if (_currentRegionalTiles == null)
                Debug.LogError($"Fatal Error: Cannot find regional tiles for region {region}");
        }

        private void CreateTileInstances()
        {
            _generalMineTiles = new Dictionary<GeneralMineTile, Tile>();
            _brokenEdgeTiles = new Dictionary<BrokenEdges, Tile>();

            foreach (var generalTile in _currentRegionalTiles.generalTiles)
            {
                if (_generalMineTiles.ContainsKey(generalTile.mineTile)) continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = generalTile.tileSprite;
                _generalMineTiles.Add(generalTile.mineTile, tile);
            }

            foreach (var brokenEdgeTile in _currentRegionalTiles.brokenEdgeTiles)
            {
                if (_brokenEdgeTiles.ContainsKey(brokenEdgeTile.brokenEdge)) continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = brokenEdgeTile.tileSprite;
                _brokenEdgeTiles.Add(brokenEdgeTile.brokenEdge, tile);
            }
        }

        #endregion

        public void GenerateMineFromData(MineData mineData)
        {
            _view.wallShadowTileMap?.ClearAllTiles();

            for (var i = 0; i < mineData.GridWidth; i++)
            {
                for (var j = 0; j < mineData.GridHeight; j++)
                {
                    var cellPos = new Vector3Int(i - mineData.GridWidth / 2, -j);
                    SetBackgroundTile(cellPos);

                    var cell = mineData.GetCell(cellPos);

                    if (cell.IsBlank)
                    {
                        SetBlankTile(cell);
                    }
                    else
                    {
                        if (cell.IsRevealed)
                        {
                            if (cell.IsBreakable)
                            {
                                if (cell.IsBroken)
                                {
                                    SetBackgroundTile(cellPos);
                                    SetBlankTile(cell);
                                }
                                else
                                {
                                    SetWallTile(cell);
                                }
                            }
                            else
                            {
                                // Even unbreakable walls (like borders) might need edge logic, 
                                // but if you want them strictly intact, you can skip CalculateBrokenEdges.
                                SetWallTile(cell);
                            }
                        }
                        else
                        {
                            SetUnrevealedTile(cell);
                        }
                    }
                }
            }
        }

        #region Tile Setters

        private void SetBackgroundTile(Vector3Int cellPos)
        {
            var backgroundTileInstance = _generalMineTiles[GeneralMineTile.Background];
            _view.backgroundTileMap.SetTile(cellPos, backgroundTileInstance);
        }

        private void SetWallTile(Cell cell)
        {
            var cellPos = cell.GetPosition();

            //to be removed
            if (!_brokenEdgeTiles.TryGetValue(cell.BrokenSides, out var brokenEdgeTileInstance))
            {
                var unrevealedInstance = _generalMineTiles[GeneralMineTile.Unrevealed];
                _view.wallTileMap.SetTile(cellPos, unrevealedInstance);
                SetWallShadowTile(cellPos, unrevealedInstance);
                return;
            }
            ////

            _view.wallTileMap.SetTile(cellPos, brokenEdgeTileInstance);
            SetWallShadowTile(cellPos, brokenEdgeTileInstance);
        }

        private void SetBlankTile(Cell cell)
        {
            var cellPos = cell.GetPosition();
            _view.wallTileMap.SetTile(cellPos, null);
            ClearWallShadowTile(cellPos);
        }

        private void SetUnrevealedTile(Cell cell)
        {
            var cellPos = cell.GetPosition();
            var unrevealedTileInstance = _generalMineTiles[GeneralMineTile.Unrevealed];
            _view.unrevealedTileMap.SetTile(cellPos, unrevealedTileInstance);
            ClearWallShadowTile(cellPos);
        }

        #endregion
        
        public void UpdateCellWall(Cell cell)
        {
            var cellPos = cell.GetPosition();
            _view.unrevealedTileMap.SetTile(cellPos, null);
            var tile = _brokenEdgeTiles.ContainsKey(cell.BrokenSides) 
                ? _brokenEdgeTiles[cell.BrokenSides] : _brokenEdgeTiles[BrokenEdges.Intact];
            _view.wallTileMap.SetTile(cellPos, cell.IsBroken ? null : tile);
            if (cell.IsBroken)
                ClearWallShadowTile(cellPos);
            else
                SetWallShadowTile(cellPos, tile);
        }

        private void EnsureWallShadowTileMap()
        {
            var wallShadowTileMap = _view.wallShadowTileMap;
            if (wallShadowTileMap == null)
            {
                if (!_hasWarnedMissingWallShadowTileMap)
                {
                    Debug.LogWarning(
                        "MineView is missing a WallShadow tilemap reference. " +
                        "Wall shadow rendering will be skipped.",
                        _view);
                    _hasWarnedMissingWallShadowTileMap = true;
                }
                return;
            }

            ConfigureWallShadowTileMap(wallShadowTileMap);
        }

        private void ConfigureWallShadowTileMap(Tilemap wallShadowTileMap)
        {
            var cellSize = _view.grid != null
                ? _view.grid.cellSize
                : Vector3.one;
            wallShadowTileMap.transform.localPosition = new Vector3(
                WallShadowOffsetInCells.x * cellSize.x,
                WallShadowOffsetInCells.y * cellSize.y,
                0f);
            wallShadowTileMap.color = WallShadowColor;

            var wallRenderer = _view.wallTileMap != null
                ? _view.wallTileMap.GetComponent<TilemapRenderer>()
                : null;
            var shadowRenderer =
                wallShadowTileMap.GetComponent<TilemapRenderer>();
            if (shadowRenderer == null)
                return;

            if (wallRenderer != null)
            {
                shadowRenderer.sharedMaterials = wallRenderer.sharedMaterials;
                shadowRenderer.sortingLayerID = wallRenderer.sortingLayerID;
                shadowRenderer.sortingOrder = wallRenderer.sortingOrder - 1;
            }

            shadowRenderer.mode = TilemapRenderer.Mode.Chunk;
            shadowRenderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
        }

        private void SetWallShadowTile(Vector3Int cellPos, TileBase tile)
        {
            if (_view.wallShadowTileMap == null)
                return;

            _view.wallShadowTileMap.SetTile(cellPos, tile);
        }

        private void ClearWallShadowTile(Vector3Int cellPos)
        {
            if (_view.wallShadowTileMap == null)
                return;

            _view.wallShadowTileMap.SetTile(cellPos, null);
        }
        
        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
