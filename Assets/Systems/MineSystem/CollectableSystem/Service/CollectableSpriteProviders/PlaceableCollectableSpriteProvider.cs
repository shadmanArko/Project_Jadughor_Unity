using System.Collections.Generic;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Service.CollectableSpriteProviders
{
    public abstract class PlaceableCollectableSpriteProvider<T> :
        ICollectableSpriteProvider where T : Placeable
    {
        private readonly List<PlaceableCollectableSpriteData> _spriteDatas;
        private readonly Sprite _fallbackSprite;
        public int Priority => -100;

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
}
