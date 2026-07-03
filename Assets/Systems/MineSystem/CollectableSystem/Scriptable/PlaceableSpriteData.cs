using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.CollectableSystem.Scriptable
{
    [Serializable]
    public sealed class PlaceableCollectableSpriteEntry
    {
        public string variant;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class PlaceableCollectableSpriteData
    {
        public Region region;
        public Site site;
        public List<PlaceableCollectableSpriteEntry> sprites = new();
    }
}
