using System.Collections.Generic;
using Systems.MineSystem.FungalVegetationSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Config
{
    /// <summary>
    /// Every tunable for decorative fungal growth, plus the authorable variant list.
    /// </summary>
    /// <remarks>
    /// Timing is expressed in in-game minutes so it stays meaningful if the clock is
    /// retuned. At the shipped DayAndTimeConfig values (tickIntervalSeconds 2,
    /// minuteStep 10) one tick is 10 game minutes / 2 real seconds, so one game hour
    /// is 12 real seconds and a full 7-day run is roughly 22 real minutes.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "FungalVegetationConfig",
        menuName = "Config/FungalVegetationConfig")]
    public sealed class FungalVegetationConfig : ScriptableObject
    {
        [Header("Growth Timing")]
        [Min(0)]
        [Tooltip("In-game minutes a cell must stay broken before it gets its one and " +
                 "only growth roll. 180 = 3 game hours = ~36 real seconds, long enough " +
                 "that the player has walked away before anything appears.")]
        public int maturationGameMinutes = 180;

        [Header("Growth Chance")]
        [Range(0f, 1f)]
        [Tooltip("Chance a matured player-broken cell grows something.")]
        public float growthChance = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("Chance a cell that already grew something also grows a second variant " +
                 "on a DIFFERENT anchor, drawn on the secondary tilemap.")]
        public float secondGrowthChance = 0.35f;

        [Range(0f, 1f)]
        [Tooltip("Chance used instead of growthChance for cells that were already broken " +
                 "when the mine was generated (cave interiors). Higher so natural caves " +
                 "read as ancient the moment the player breaks into them.")]
        public float caveSeedChance = 0.35f;

        [Header("Density Limits")]
        [Min(1)]
        [Tooltip("Maximum growths placed per clock tick. Staggers the generation-time " +
                 "cave burst so it never appears as one visible batch.")]
        public int maxGrowthsPerTick = 12;

        [Min(0)]
        [Tooltip("Hard ceiling on simultaneous growths. Safety valve, not a density " +
                 "lever - use growthChance and enforceSpacing for that. 0 = unlimited.")]
        public int maxTotalGrowths = 250;

        [Tooltip("Reject a cell if any of its 4 cardinal neighbours already has a " +
                 "growth. Caps density at 50% in a 1-wide corridor.")]
        public bool enforceSpacing = true;

        [Header("Camera Visibility")]
        [Tooltip("Tick to let vegetation appear inside the camera view. Unticked (the " +
                 "default) a growth never pops into existence on screen: an on-screen " +
                 "candidate is passed over, a different off-screen one grows instead, and " +
                 "the skipped cell is retried once the camera has moved away - so it keeps " +
                 "its growth roll rather than losing it.")]
        public bool allowGrowthInsideCameraBounds;

        [Min(0)]
        [Tooltip("Extra slack in tiles beyond the viewport edge, so a cell just off screen " +
                 "is not a legal spawn site that then immediately scrolls into view. " +
                 "Ignored when allowGrowthInsideCameraBounds is ticked.")]
        public int cameraBoundsMarginCells = 2;

        [Min(1)]
        [Tooltip("Maximum matured candidates examined per clock tick. Bounds how many cells " +
                 "can move into the camera-blocked retry list in one go - relevant on the " +
                 "tick where the whole cave pre-seed matures at once.")]
        public int maxCandidateScansPerTick = 96;

        [Header("Anchor Weights")]
        [Min(0)] public int floorWeight = 5;
        [Min(0)] public int ceilingWeight = 3;
        [Min(0)] public int leftWallWeight = 2;
        [Min(0)] public int rightWallWeight = 2;

        [Header("Vegetation Variants")]
        [Tooltip("Add entries here to introduce new vegetation. Each needs a unique id, " +
                 "a sprite, and the wall it clings to.")]
        public List<FungalVegetationEntry> vegetationEntries = new();

        /// <summary>
        /// Relative likelihood of picking this anchor when several are eligible.
        /// Weighted down for the walls because only two variants exist per side.
        /// </summary>
        public int GetAnchorWeight(FungalAnchor anchor) => anchor switch
        {
            FungalAnchor.Floor => Mathf.Max(0, floorWeight),
            FungalAnchor.Ceiling => Mathf.Max(0, ceilingWeight),
            FungalAnchor.LeftWall => Mathf.Max(0, leftWallWeight),
            FungalAnchor.RightWall => Mathf.Max(0, rightWallWeight),
            _ => 0
        };

        /// <summary>
        /// Catches the authoring mistakes that would otherwise fail silently at
        /// runtime: missing sprites, blank ids, and duplicate ids (which would make one
        /// variant permanently shadow another in the tile cache).
        /// </summary>
        public bool Validate(out string error)
        {
            if (vegetationEntries == null || vegetationEntries.Count == 0)
            {
                error = "vegetationEntries is empty - no growth can ever be placed.";
                return false;
            }

            var seenIds = new HashSet<string>();
            for (var i = 0; i < vegetationEntries.Count; i++)
            {
                var entry = vegetationEntries[i];
                if (entry == null)
                {
                    error = $"vegetationEntries[{i}] is null.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    error = $"vegetationEntries[{i}] has a blank id.";
                    return false;
                }

                if (!seenIds.Add(entry.id))
                {
                    error = $"vegetationEntries[{i}] repeats the id '{entry.id}'.";
                    return false;
                }

                if (entry.sprite == null)
                {
                    error = $"vegetationEntries[{i}] ('{entry.id}') has no sprite.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
