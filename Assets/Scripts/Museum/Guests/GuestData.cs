using System;
using UnityEngine;

namespace ProjectMuseum.Guests
{
    /// <summary>What a guest is doing right now. Drives <see cref="GuestAgent"/>'s decisions.</summary>
    public enum GuestState
    {
        /// <summary>Outside, walking to a wander stop or its exit point. May still decide to visit.</summary>
        WalkingOutside,
        /// <summary>Outside, paused at a wander stop.</summary>
        PausingOutside,
        /// <summary>Decided to visit — walking to the museum door.</summary>
        HeadingToMuseum,
        /// <summary>Inside, walking to the front of an exhibit.</summary>
        WalkingToExhibit,
        /// <summary>Inside, standing in front of an exhibit looking at it.</summary>
        ViewingExhibit,
        /// <summary>Inside an empty (or fully seen) museum, drifting to a random floor cell.</summary>
        LookingAround,
        /// <summary>Seen enough — walking back out through the door.</summary>
        LeavingMuseum,
        /// <summary>Outside after (or instead of) a visit, walking to an exit point to be removed.</summary>
        HeadingToExit
    }

    /// <summary>
    /// Per-guest rolled data. Everything random about a guest is decided once at spawn and kept
    /// here, so a guest can be inspected (or later saved) without reading behaviour code.
    ///
    /// Godot's <c>GuestBuildingParameter</c> also rolled needs (hunger, thirst, bladder…) and
    /// interest tags. Those only mattered for shops and washrooms, which this port doesn't have
    /// yet; add them here when those systems land.
    /// </summary>
    [Serializable]
    public class GuestData
    {
        public int Id;

        [Tooltip("Chosen sheet index per appearance pool (-1 = layer hidden).")]
        public int[] AppearanceIndices = Array.Empty<int>();

        [Tooltip("Seconds to cross one tile.")]
        public float StepDuration = 0.45f;

        [Tooltip("Visual offset within a tile while outside, in cell units, so crowds don't walk in single file.")]
        public Vector2 LaneJitter;

        public Vector2Int SpawnCell;

        [Tooltip("Index of the spawn zone the guest came from (-1 = prewarmed mid-sidewalk).")]
        public int SpawnZone = -1;

        [Tooltip("How many exhibits this guest wants to see before leaving.")]
        public int ExhibitsToVisit;

        public int ExhibitsSeen;

        [Tooltip("Rolled against the museum-entry chance yet? Only ever once per guest.")]
        public bool EntryRolled;

        public bool VisitedMuseum;

        public GuestState State;

        public float SpawnTime;
    }
}
