using System.Collections.Generic;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;
using Systems.MineSystem.Mine.Service.MineResourceService.Scriptable;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Service
{
    public sealed class ResourceCollectableSpriteProvider : ICollectableSpriteProvider
    {
        private readonly ResourceSpriteScriptable _sprites;

        public ResourceCollectableSpriteProvider(ResourceSpriteScriptable sprites)
        {
            _sprites = sprites;
        }

        public bool CanResolve(Item item) => item is Resource;

        public Sprite Resolve(Item item, Region region, Site site)
        {
            var resource = (Resource)item;
            for (var i = 0; i < _sprites.resourceSpriteDatas.Count; i++)
            {
                var data = _sprites.resourceSpriteDatas[i];
                if (data.region != region || data.site != site)
                    continue;

                for (var j = 0; j < data.spriteDatas.Count; j++)
                {
                    var spriteData = data.spriteDatas[j];
                    if (spriteData.id == resource.Variant)
                        return spriteData.sprite;
                }
            }

            return null;
        }
    }

    public sealed class ArtifactCollectableSpriteProvider : ICollectableSpriteProvider
    {
        private readonly ArtifactSpriteScriptable _sprites;

        public ArtifactCollectableSpriteProvider(ArtifactSpriteScriptable sprites)
        {
            _sprites = sprites;
        }

        public bool CanResolve(Item item) => item is Artifact;

        public Sprite Resolve(Item item, Region region, Site site)
        {
            return _sprites.GetInventorySprite(
                ((Artifact)item).DefinitionId,
                region,
                site);
        }
    }

    public abstract class PlaceableCollectableSpriteProvider<T> :
        ICollectableSpriteProvider where T : Placeable
    {
        private readonly List<PlaceableCollectableSpriteData> _spriteDatas;
        private readonly Sprite _fallbackSprite;

        protected PlaceableCollectableSpriteProvider(
            List<PlaceableCollectableSpriteData> spriteDatas,
            Sprite fallbackSprite)
        {
            _spriteDatas = spriteDatas;
            _fallbackSprite = fallbackSprite;
        }

        public bool CanResolve(Item item) => item is T;

        public Sprite Resolve(Item item, Region region, Site site)
        {
            for (var i = 0; i < _spriteDatas.Count; i++)
            {
                var data = _spriteDatas[i];
                if (data.region != region || data.site != site)
                    continue;

                for (var j = 0; j < data.sprites.Count; j++)
                {
                    var entry = data.sprites[j];
                    if (entry.variant == item.Variant)
                        return entry.sprite;
                }
            }

            return _fallbackSprite;
        }
    }

    public sealed class CellPlaceableCollectableSpriteProvider :
        PlaceableCollectableSpriteProvider<CellPlaceable>
    {
        public CellPlaceableCollectableSpriteProvider(
            CellPlaceableSpriteScriptable sprites) :
            base(sprites.spriteDatas, sprites.fallbackSprite)
        {
        }
    }

    public sealed class WallPlaceableCollectableSpriteProvider :
        PlaceableCollectableSpriteProvider<WallPlaceable>
    {
        public WallPlaceableCollectableSpriteProvider(
            WallPlaceableSpriteScriptable sprites) :
            base(sprites.spriteDatas, sprites.fallbackSprite)
        {
        }
    }
}
