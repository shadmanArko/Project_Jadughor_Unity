using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.ResourceSystem.Model;
using Systems.MineSystem.ResourceSystem.Scriptable;
using UniRx;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem.ResourceSystem.Service
{
    [Serializable]
    public class ResourceVisualizerService : IInitializable, IDisposable
    {
        private readonly MineView _view;
        private readonly ResourceSpriteScriptable _resourceSpriteScriptable;
        private CompositeDisposable _disposable;
        
        private Dictionary<string, Tile> _resourceTiles;
        private ResourceSpriteData _resourceSpriteData;

        public ResourceVisualizerService(
            MineView view,
            ResourceSpriteScriptable resourceSpriteScriptable)
        {
            _view = view;
            _resourceSpriteScriptable = resourceSpriteScriptable;
        }

        public void Initialize()
        {
            _disposable = new CompositeDisposable();
            _resourceTiles = new Dictionary<string, Tile>();
            _resourceSpriteData = _resourceSpriteScriptable.resourceSpriteDatas[0]; // change it based on region
            
            CreateTileInstance();
        }

        private void CreateTileInstance()
        {
            foreach (var spriteData in _resourceSpriteScriptable.resourceSpriteDatas[0].spriteDatas)
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = spriteData.sprite;
                _resourceTiles.Add(spriteData.id, tile);
            }
        }

        public void UpdateResourceTile(Cell cell)
        {
            // Clear tile when no resource
            if (!cell.HasResource)
            {
                _view.resourceTileMap.SetTile(cell.GetPosition(), null);
                return;
            }

            // Find the appropriate sprite for the resource.
            // Here we simply pick the first sprite in the scriptable that matches the current region/site.
            // More sophisticated matching can be added later.
            var spriteData = _resourceSpriteData.spriteDatas.FirstOrDefault(data => data.id == cell.ItemId);

            if (spriteData == null)
            {
                Debug.LogWarning("ResourceSpriteScriptable does not contain any sprite data.");
                return;
            }
            
            _resourceTiles.TryGetValue(cell.ItemId, out var tile);
            if (tile == null)
            {
                Debug.LogError($"Fatal Error: tile not found for: {cell.ItemId}");
                return;
            }
            _view.resourceTileMap.SetTile(cell.GetPosition(), tile);
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}