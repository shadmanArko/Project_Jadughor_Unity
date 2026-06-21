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
        [Tooltip("Zero allows the pool to grow without a maximum.")]
        [Min(0)] public int maximumSize = 10;
    }
}
