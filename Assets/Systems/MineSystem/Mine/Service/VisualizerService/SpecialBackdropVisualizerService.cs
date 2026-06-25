using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MineGenerationSystem.Model;
using UniRx;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    [Serializable]
    public class SpecialBackdropVisualizerService : IInitializable, IDisposable
    {
        private CompositeDisposable _disposables;
        
        private readonly SpecialBackdropSpriteScriptable _scriptable;
        private readonly MineView _mineView;

        public SpecialBackdropVisualizerService(
            MineView mineView, 
            SpecialBackdropSpriteScriptable scriptable)
        {
            _mineView = mineView;
            _scriptable = scriptable;
        }

        public void Initialize()
        {
            _disposables = new CompositeDisposable();
        }

        public void SetSpecialBackdrops(List<SpecialBackdropData> datas, Region region, Site site)
        {
            var specialBackdropData = _scriptable.specialBackdropSpriteDatas
                .FirstOrDefault(data => data.region == region && data.site == site);
            if (specialBackdropData == null)
            {
                Debug.LogError($"Fatal error: specialBackdropData is null for region {region} and site {site}");
                return;
            }
            
            foreach (var backdropData in datas)
            {
                var backdropSpriteData = specialBackdropData.specialBackdropSprites
                    .FirstOrDefault(spriteData => spriteData.id == backdropData.Id);
                if (backdropSpriteData == null)
                {
                    Debug.LogError($"Fatal error: backdropSprite is null for id {backdropData.Id}");
                    continue;
                }
                
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = backdropSpriteData.objectSprite;
                _mineView.specialBackdropTileMap.SetTile(backdropData.TilePosition.ToVector3Int(), tile);
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }

    [Serializable]
    public sealed class VineVisualizerService : IInitializable, IDisposable
    {
        private readonly MineView _mineView;
        private readonly VineSpriteScriptable _scriptable;
        private readonly Dictionary<string, Tile> _tiles = new();
        private CompositeDisposable _disposables;

        public VineVisualizerService(
            MineView mineView,
            VineSpriteScriptable scriptable)
        {
            _mineView = mineView;
            _scriptable = scriptable;
        }

        public void Initialize()
        {
            _disposables = new CompositeDisposable();
        }

        public void SetVines(List<VineData> vineDatas, MineData mineData, Region region, Site site)
        {
            if (_mineView.vineTileMap == null)
            {
                Debug.LogError("Fatal error: MineView vineTileMap is not assigned.");
                return;
            }

            _mineView.vineTileMap.ClearAllTiles();
            if (vineDatas == null || vineDatas.Count == 0)
                return;

            var vineSpriteData = _scriptable?.vineSpriteDatas?
                .FirstOrDefault(data => data.region == region && data.site == site);
            if (vineSpriteData == null)
            {
                Debug.LogError($"Fatal error: vine sprite data is null for region {region} and site {site}");
                return;
            }

            foreach (var vineData in vineDatas)
            {
                if (vineData?.VineCellIds == null ||
                    !TryGetTile(vineSpriteData.vineSprites, vineData.SourceId, out var tile))
                    continue;

                foreach (var cellId in vineData.VineCellIds)
                {
                    var cell = mineData.GetCellById(cellId);
                    if (cell == null)
                        continue;

                    _mineView.vineTileMap.SetTile(cell.GetPosition(), tile);
                }
            }
        }

        private bool TryGetTile(
            List<SpriteData> sprites,
            string sourceId,
            out Tile tile)
        {
            tile = null;
            if (string.IsNullOrEmpty(sourceId))
                return false;

            if (_tiles.TryGetValue(sourceId, out tile))
                return tile != null;

            var spriteData = sprites?.FirstOrDefault(sprite => sprite.id == sourceId);
            if (spriteData?.objectSprite == null)
            {
                Debug.LogWarning($"VineSpriteScriptable does not contain a sprite for id '{sourceId}'.");
                return false;
            }

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = spriteData.objectSprite;
            _tiles[sourceId] = tile;
            return true;
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}
