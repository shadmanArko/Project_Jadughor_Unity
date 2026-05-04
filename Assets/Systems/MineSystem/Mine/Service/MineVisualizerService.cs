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

namespace Systems.MineSystem.Mine.Service
{
    [Serializable]
    public class MineVisualizerService
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

        public void GenerateMineFromData(MineData mineData)
        {
            var region = _playerScriptable.region;
            _currentRegionalTiles = _tileScriptable.regionTiles.FirstOrDefault(tiles => tiles.region == region);
            if (_currentRegionalTiles == null)
            {
                Debug.LogError($"Fatal Error: Cannot find regional tiles for region {region}");
                return;
            }
            
            CreateTileInstances();

            var backgroundTileMap = _view.tilemaps.FirstOrDefault(tilemap => tilemap.name == "Background");
            if (backgroundTileMap == null)
            {
                Debug.LogError($"Background tile map not found");
                return;
            }
            
            for (var i = 0; i < mineData.GridWidth; i++)
            {
                for (var j = 0; j < mineData.GridHeight; j++)
                {
                    var cellPos = new Vector3Int(i - mineData.GridWidth / 2, -j);
                    var backgroundTile = _generalMineTiles[GeneralMineTile.Background];
                    backgroundTileMap.SetTile(cellPos, backgroundTile);
                }
            }
        }

        private void CreateTileInstances()
        {
            _generalMineTiles = new Dictionary<GeneralMineTile, Tile>();
            _brokenEdgeTiles = new Dictionary<BrokenEdges, Tile>();

            foreach (var generalTile in _currentRegionalTiles.mineTiles)
            {
                if(_generalMineTiles.ContainsKey(generalTile.mineTile)) continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = generalTile.tileSprite;
                _generalMineTiles.Add(generalTile.mineTile, tile);
            }

            foreach (var brokenEdgeTile in _currentRegionalTiles.brokenEdgeTiles)
            {
                if(_brokenEdgeTiles.ContainsKey(brokenEdgeTile.brokenEdge)) continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = brokenEdgeTile.tileSprite;
                _brokenEdgeTiles.Add(brokenEdgeTile.brokenEdge, tile);
            }
        }

        private void SetBackgroundTile(GridPosition gridPosition)
        {
            
        }
    }
}