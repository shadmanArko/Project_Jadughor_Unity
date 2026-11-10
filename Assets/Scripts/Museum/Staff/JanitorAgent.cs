using System.Collections.Generic;
using ProjectMuseum.Builder;
using ProjectMuseum.Data;
using UnityEngine;

namespace ProjectMuseum.Staff
{
    /// <summary>
    /// Janitor decision making, on top of <see cref="StaffAgent"/> (movement, hunger, home, tasks).
    ///
    /// Priority every time he's free:
    /// <list type="number">
    /// <item>Hungry → eat (base class).</item>
    /// <item><b>Trash on the floor</b> → walk to the nearest unclaimed piece, sweep it, remove it.
    ///   New litter wakes him immediately (no waiting for the think timer), and it can divert him
    ///   while he's walking home.</item>
    /// <item><b>Sometimes</b> (cooldown + chance) → walk to an exhibit, clean its glass.
    ///   For now the exhibit is random — <see cref="PickExhibitsToClean"/> is the hook for a
    ///   real dirtiness value later.</item>
    /// <item>Nothing → back to his home tile and idle (with the odd "thinking" fidget).</item>
    /// </list>
    ///
    /// Setup: add this to the scene's <c>Janitor</c> root (the Body child keeps its
    /// SheetSpriteRenderer + Animator with <c>JanitorAnimatorController</c>). The tile he stands
    /// on at Play becomes his home unless you set one.
    /// </summary>
    [AddComponentMenu("Project Museum/Staff/Janitor")]
    public class JanitorAgent : StaffAgent
    {
        [Header("Sweeping trash")]
        [Tooltip("Seconds of sweeping per piece (the sweep loop is 0.6 s).")]
        [SerializeField, Min(0.2f)] private float sweepDuration = 2.4f;
        [SerializeField] private string sweepFrontState = "sweeping_front";
        [SerializeField] private string sweepBackState = "sweeping_back";
        [Tooltip("Nearest pieces to try pathing to before giving up this round.")]
        [SerializeField, Min(1)] private int maxTrashCandidates = 5;

        [Header("Cleaning exhibits")]
        [Tooltip("Chance to go clean an exhibit when free and the cooldown has passed.")]
        [SerializeField, Range(0f, 1f)] private float exhibitCleanChance = 0.4f;
        [Tooltip("Seconds between exhibit-cleaning rolls (random in range).")]
        [SerializeField] private Vector2 exhibitCleanCooldown = new Vector2(15f, 35f);
        [Tooltip("Seconds spent cleaning one exhibit.")]
        [SerializeField, Min(0.2f)] private float cleanDuration = 4f;
        [SerializeField] private string cleanFrontState = "cleaning_glass_front";
        [SerializeField] private string cleanBackState = "cleaning_glass_back";

        public override StaffRole Role => StaffRole.Janitor;

        private float _nextExhibitRoll;
        private readonly List<TrashData> _trashCandidates = new List<TrashData>();
        private readonly List<string> _exhibitIds = new List<string>();
        private readonly List<(Vector2Int stand, Vector2Int face)> _spots = new List<(Vector2Int, Vector2Int)>();
        private System.Comparison<TrashData> _byDistance;

        protected override void Awake()
        {
            base.Awake();
            _byDistance = (a, b) => Manhattan(Cell, a.Cell).CompareTo(Manhattan(Cell, b.Cell));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            StaffActions.OnTrashAdded += OnTrashAdded;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StaffActions.OnTrashAdded -= OnTrashAdded;
        }

        protected override void OnStarted()
        {
            // Don't rush to an exhibit the moment Play starts.
            _nextExhibitRoll = Time.time + Random.Range(exhibitCleanCooldown.x, exhibitCleanCooldown.y);
        }

        private void OnTrashAdded(TrashData trash) => RequestThink();

        protected override bool TryStartWork() => TrySweepTrash() || TryCleanExhibit();

        // ── Trash ──────────────────────────────────────────────────────────

        private bool TrySweepTrash()
        {
            if (Model == null || Model.Trash.Count == 0) return false;

            _trashCandidates.Clear();
            foreach (var t in Model.Trash)
                if (!StaffTaskBoard.IsClaimed(TrashKey(t), this)) _trashCandidates.Add(t);
            if (_trashCandidates.Count == 0) return false;

            // Nearest by tile distance first; the path check below handles walls in the way.
            _trashCandidates.Sort(_byDistance);

            var tries = Mathf.Min(maxTrashCandidates, _trashCandidates.Count);
            for (var i = 0; i < tries; i++)
                if (TryAssign(BuildSweepTask(_trashCandidates[i]))) return true;

            return false;
        }

        private StaffTask BuildSweepTask(TrashData trash)
        {
            // Stand on the litter itself; if something now blocks that tile, sweep from beside it.
            var stand = trash.Cell;
            Vector2Int? face = null;
            if (!IsWalkable(stand))
            {
                if (!TryGetWalkableNeighbour(trash.Cell, out stand)) return null;
                face = trash.Cell;
            }

            var id = trash.Id;
            return new StaffTask
            {
                Name = "Sweep",
                StandCell = stand,
                FaceCell = face,
                FrontState = sweepFrontState,
                BackState = sweepBackState,
                Duration = sweepDuration,
                ClaimKey = TrashKey(trash),
                IsStillValid = () => Model.FindTrash(id) != null,
                OnCompleted = () => Model.RemoveTrash(id),
            };
        }

        private static string TrashKey(TrashData t) => "trash:" + t.Id;

        // ── Exhibits ───────────────────────────────────────────────────────

        private bool TryCleanExhibit()
        {
            if (Model == null || Time.time < _nextExhibitRoll) return false;

            // "Sometimes": one roll per cooldown window, whether or not it succeeds.
            _nextExhibitRoll = Time.time + Random.Range(exhibitCleanCooldown.x, exhibitCleanCooldown.y);
            if (Random.value > exhibitCleanChance) return false;

            PickExhibitsToClean(_exhibitIds);
            var exhibitTries = Mathf.Min(_exhibitIds.Count, 4);
            for (var i = 0; i < exhibitTries; i++)
            {
                var id = _exhibitIds[i];
                var key = "exhibit:" + id;
                if (StaffTaskBoard.IsClaimed(key, this)) continue;

                var exhibit = FindExhibit(id);
                GetSpotsAround(exhibit, _spots);

                var spotTries = Mathf.Min(_spots.Count, 4);
                for (var s = 0; s < spotTries; s++)
                {
                    var (stand, face) = _spots[s];
                    var task = new StaffTask
                    {
                        Name = "Clean exhibit",
                        StandCell = stand,
                        FaceCell = face,
                        FrontState = cleanFrontState,
                        BackState = cleanBackState,
                        Duration = cleanDuration,
                        ClaimKey = key,
                        IsStillValid = () => FindExhibit(id) != null,
                        OnCompleted = () => StaffActions.OnExhibitCleaned?.Invoke(id),
                    };
                    if (TryAssign(task)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Which exhibits to consider, best first. Random for now; when exhibits get a dirt value,
        /// sort dirtiest-first here and the rest of the behaviour stays the same.
        /// </summary>
        protected virtual void PickExhibitsToClean(List<string> ids)
        {
            ids.Clear();
            foreach (var placed in Model.PlacedObjects)
                if (placed.Type == BuilderCardType.Exhibit) ids.Add(placed.Id);
            Shuffle(ids, 0, ids.Count);
        }

        private PlacedObjectData FindExhibit(string id)
        {
            foreach (var placed in Model.PlacedObjects)
                if (placed.Id == id) return placed;
            return null;
        }

        protected override IEnumerable<string> AnimatorStates()
        {
            foreach (var s in base.AnimatorStates()) yield return s;
            yield return sweepFrontState;
            yield return sweepBackState;
            yield return cleanFrontState;
            yield return cleanBackState;
        }
    }
}
