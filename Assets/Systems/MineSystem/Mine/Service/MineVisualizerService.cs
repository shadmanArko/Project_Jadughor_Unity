using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MineGenerationSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem.Mine.Service
{
    [Serializable]
    public class MineVisualizerService : IInitializable
    {
        private MineView _view;
        private MineRegionalTileScriptable _tileScriptable;
        private MinePlayerScriptable _playerScriptable;

        private MineRegionalTiles _currentRegionalTiles;

        private Dictionary<GeneralMineTile, Tile> _generalMineTiles;
        private Dictionary<BrokenEdges, Tile> _brokenEdgeTiles;

        public MineVisualizerService(
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
            InitializeVariables();
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

            foreach (var generalTile in _currentRegionalTiles.mineTiles)
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
                                cell.BrokenSides = BrokenEdges.Intact;
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
                return;
            }
            ////

            _view.wallTileMap.SetTile(cellPos, brokenEdgeTileInstance);
        }

        private void SetBlankTile(Cell cell)
        {
            var cellPos = cell.GetPosition();
            _view.wallTileMap.SetTile(cellPos, null);
        }

        private void SetUnrevealedTile(Cell cell)
        {
            var cellPos = cell.GetPosition();
            var unrevealedTileInstance = _generalMineTiles[GeneralMineTile.Unrevealed];
            _view.unrevealedTileMap.SetTile(cellPos, unrevealedTileInstance);
        }

        #endregion

        private List<Vector3Int> _adjacentTiles = new()
        {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right,
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(-1, -1, 0)
        };

        public void BreakWallTile(Vector3Int cellPos)
        {
            _view.wallTileMap.SetTile(cellPos, null);
            _view.artifactTileMap.SetTile(cellPos, null);
            _view.resourceTileMap.SetTile(cellPos, null);
            _view.unrevealedTileMap.SetTile(cellPos, null);
        }

        public void RevealAdjacentTiles(Cell cell)
        {
            foreach (var adjacentTilePos in _adjacentTiles)
            {
                var newCellPos = cell.GetPosition() + adjacentTilePos;
                RevealTile(cell);
                //TODO: finish it
                //Remove old input system
            }
        }

        public void RevealTile(Cell cell)
        {
            var cellPos = cell.GetPosition();
            _view.unrevealedTileMap.SetTile(cellPos, null);
        }
    }
}