using System;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.CollectableSystem.Service.CollectableSpriteProviders
{
    [Serializable]
    public sealed class CellPlaceableCollectableSpriteProvider :
        PlaceableCollectableSpriteProvider<CellPlaceable>
    {
        public CellPlaceableCollectableSpriteProvider(
            CellPlaceableSpriteScriptable sprites) :
            base(sprites.spriteDatas, sprites.fallbackSprite)
        {
        }
    }
}