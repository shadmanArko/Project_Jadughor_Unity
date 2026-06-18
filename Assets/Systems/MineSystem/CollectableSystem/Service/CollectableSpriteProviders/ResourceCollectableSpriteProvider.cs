using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;
using Systems.MineSystem.Mine.Service.MineResourceService.Scriptable;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Service.CollectableSpriteProviders
{
    [Serializable]
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
}