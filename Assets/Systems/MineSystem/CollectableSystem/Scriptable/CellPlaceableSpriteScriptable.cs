using System.Collections.Generic;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Scriptable
{
    [CreateAssetMenu(
        fileName = "CellPlaceableSpriteScriptable",
        menuName = "Scriptable/Cell Placeable Collectable Sprites")]
    public sealed class CellPlaceableSpriteScriptable : ScriptableObject
    {
        public Sprite fallbackSprite;
        public List<PlaceableCollectableSpriteData> spriteDatas = new();
    }
}
