using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Scriptable;
using Systems.MineSystem.Mine.View;
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
                tile.sprite = backdropSpriteData.sprite;
                _mineView.specialBackdropTileMap.SetTile(backdropData.TilePosition.ToVector3Int(), tile);
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}