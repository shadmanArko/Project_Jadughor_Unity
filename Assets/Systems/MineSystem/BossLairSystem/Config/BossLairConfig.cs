using DG.Tweening;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Config
{
    /// <summary>
    /// Boss-lair values that do not vary between bosses. Arena geometry and
    /// camera zoom live on <see cref="BossProceduralLairConfig"/> instead, one
    /// asset per boss, so a bigger boss can have a bigger arena.
    /// </summary>
    [CreateAssetMenu(fileName = "BossLairConfig", menuName = "Config/Boss Lair Config")]
    public sealed class BossLairConfig : ScriptableObject
    {
        [Header("Transition")]
        [Tooltip("Seconds the player walks into the gate before the transition.")]
        [Min(0f)] [SerializeField] private float gateApproachDuration = 1f;
        [Tooltip("Seconds the player settles after arriving in the arena.")]
        [Min(0f)] [SerializeField] private float arenaEntryDuration = 1f;
        [Tooltip("Easing applied to scripted player movement during transitions.")]
        [SerializeField] private Ease playerMovementEase = Ease.Linear;

        [Header("Spawning")]
        [Tooltip(
            "Probes downward from the arena's spawn anchor and places the player " +
            "on the floor beneath it. The anchor still decides the spawn column; " +
            "only the height is corrected. Turn off to use the anchor position " +
            "exactly, at the risk of fall damage on arrival.")]
        [SerializeField] private bool snapSpawnPointsToGround = true;
        [Tooltip("How far down, in cells, to look for the arena floor.")]
        [Min(1)] [SerializeField] private int spawnGroundProbeDistanceInCells = 64;

        [Header("Decor Randomization")]
        [Tooltip("Minimum number of randomized decor props placed in the arena.")]
        [Min(0)] [SerializeField] private int minimumDecorCount = 6;
        [Tooltip("Maximum number of randomized decor props placed in the arena.")]
        [Min(0)] [SerializeField] private int maximumDecorCount = 14;
        [Tooltip(
            "Uses the fixed seed below instead of a per-run random seed, so a " +
            "layout can be reproduced while tuning the fight.")]
        [SerializeField] private bool useFixedDecorSeed;
        [Tooltip("Seed used when the fixed decor seed is enabled.")]
        [SerializeField] private int fixedDecorSeed;

        [Header("Boss Intro")]
        [Tooltip("World units the boss steps toward the player during its intro.")]
        [Min(0f)] [SerializeField] private float bossIntroStepDistance = 0.5f;
        [Tooltip("Seconds the boss's intro step takes.")]
        [Min(0f)] [SerializeField] private float bossIntroWalkDuration = 0.6f;
        [Tooltip("Seconds the boss's roar animation is held before control returns.")]
        [Min(0f)] [SerializeField] private float bossIntroRoarDuration = 1f;

        [Header("Interaction")]
        [Tooltip(
            "Interaction priority of the boss gate. Must stay above the " +
            "elevator's 100, otherwise a placed elevator on the gate cell wins " +
            "the interaction and permanently shadows the gate.")]
        [Min(0)] [SerializeField] private int gateInteractionPriority = 200;

        public float GateApproachDuration => gateApproachDuration;
        public float ArenaEntryDuration => arenaEntryDuration;
        public Ease PlayerMovementEase => playerMovementEase;
        public bool SnapSpawnPointsToGround => snapSpawnPointsToGround;
        public int SpawnGroundProbeDistanceInCells => spawnGroundProbeDistanceInCells;
        public int MinimumDecorCount => minimumDecorCount;
        public int MaximumDecorCount => maximumDecorCount;
        public bool UseFixedDecorSeed => useFixedDecorSeed;
        public int FixedDecorSeed => fixedDecorSeed;
        public int GateInteractionPriority => gateInteractionPriority;
        public float BossIntroStepDistance => bossIntroStepDistance;
        public float BossIntroWalkDuration => bossIntroWalkDuration;
        public float BossIntroRoarDuration => bossIntroRoarDuration;

        public bool Validate(out string error)
        {
            if (maximumDecorCount < minimumDecorCount)
            {
                error =
                    $"{name} maximum decor count ({maximumDecorCount}) must be " +
                    $"at least the minimum ({minimumDecorCount}).";
                return false;
            }
            // The elevator interaction handler sits at 100 and the first handler
            // that succeeds wins, so anything at or below that can be shadowed by
            // a placeable dropped on the gate cell.
            if (gateInteractionPriority <= 100)
            {
                error =
                    $"{name} gate interaction priority " +
                    $"({gateInteractionPriority}) must exceed the elevator " +
                    "handler priority of 100.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
