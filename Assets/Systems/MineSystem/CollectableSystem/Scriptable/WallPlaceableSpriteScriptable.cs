using System.Collections.Generic;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Scriptable
{
    [CreateAssetMenu(
        fileName = "WallPlaceableSpriteScriptable",
        menuName = "Scriptable/Wall Placeable Collectable Sprites")]
    public sealed class WallPlaceableSpriteScriptable : ScriptableObject
    {
        public Sprite fallbackSprite;
        public List<PlaceableCollectableSpriteData> spriteDatas = new();
    }
}
