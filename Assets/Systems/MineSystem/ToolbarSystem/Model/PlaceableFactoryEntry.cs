using System;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Model
{
    [Serializable]
    public sealed class PlaceableFactoryEntry
    {
        public string id;
        public GameObject prefab;
        [Min(0)] public int initialSize = 1;
        [Min(1)] public int maximumSize = 10;
    }
}
