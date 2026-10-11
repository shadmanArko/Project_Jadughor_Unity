using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Systems.MineSystem.BossLairSystem.Model
{
    /// <summary>
    /// One decor tile the lair can scatter, with a relative selection weight.
    /// </summary>
    [Serializable]
    public sealed class BossLairDecorEntry
    {
        [Tooltip("Tile painted onto the arena's decor tilemap.")]
        public TileBase tile;

        [Tooltip(
            "Relative selection weight. Higher values appear more often. " +
            "Entries at zero are never chosen.")]
        [Min(0f)] public float weight = 1f;
    }
}
