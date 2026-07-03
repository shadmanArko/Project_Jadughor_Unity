using System;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.CollectableSystem.Service.CollectableSpriteProviders
{
    [Serializable]
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
