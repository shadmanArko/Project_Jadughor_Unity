using System;
using UnityEngine.Serialization;

namespace Systems.MineSystem.MinePlayerSystem.Config
{
    [Serializable]
    public struct PlayerFallDamageThreshold
    {
        [FormerlySerializedAs("minimumDistance")]
        public float minimumCells;
        public float damage;
    }
}
