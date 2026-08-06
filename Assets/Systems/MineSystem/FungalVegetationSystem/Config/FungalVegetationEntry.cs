using System;
using Systems.MineSystem.FungalVegetationSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Config
{
    /// <summary>
    /// One authorable vegetation variant: a sprite plus the wall it is drawn against.
    /// Lives in a list on <see cref="FungalVegetationConfig"/> so new variants are an
    /// Inspector edit rather than a code change.
    /// </summary>
    [Serializable]
    public sealed class FungalVegetationEntry
    {
        [Tooltip("Stable unique id. Used as the tile cache key, so changing it is safe " +
                 "but duplicating it is not.")]
        public string id;

        [Tooltip("20x20 sprite whose artwork hugs the canvas edge matching 'anchor'. " +
                 "The 20x20 canvas is a registration frame equal to exactly one mine " +
                 "cell, so a centre-pivoted sprite lands flush against the right edge " +
                 "with no offset maths.")]
        public Sprite sprite;

        [Tooltip("Which solid neighbour this variant clings to. The growth is only " +
                 "placed when the cell in that direction is unbroken rock.")]
        public FungalAnchor anchor;

        [Min(0)]
        [Tooltip("Relative pick weight among the variants sharing this anchor. " +
                 "0 disables the entry without deleting it.")]
        public int weight = 1;
    }
}
