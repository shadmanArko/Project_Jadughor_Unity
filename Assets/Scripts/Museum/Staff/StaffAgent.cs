using System.Collections.Generic;
using ProjectMuseum.Builder;
using ProjectMuseum.Characters;
using ProjectMuseum.Data;
using ProjectMuseum.Guests;
using ProjectMuseum.Narrative;
using UnityEngine;
using Zenject;

namespace ProjectMuseum.Staff
{
    public enum StaffActivity
    {
        /// <summary>Standing still (normally at home) and periodically looking for something to do.</summary>
        Idle,
        /// <summary>Walking to a task, to eat, or back home.</summary>
        Walking,
        /// <summary>Performing a task (sweeping, cleaning, eating…).</summary>
        Working,
        /// <summary>Dismissed — walking to the entrance to be removed.</summary>
        Leaving,
    }

    /// <summary>
    /// Everything museum staff have in common. Roles derive from this and only decide
    /// <i>what</i> to do (<see cref="TryStartWork"/>); this class does the rest:
    ///
    /// - <b>Movement</b>: tile-by-tile A* over walkable museum floor (re-paths if an object is
    ///   placed on the route), same cell-corner lerp and facing/flip rules as the player and guests.
    /// - <b>Sorting</b>: registers with <see cref="MuseumSortingSystem"/> like the player, so
    ///   exhibits in front draw over the character.
    /// - <b>Hunger</b>: rises every museum clock tick (so it pauses/speeds with the clock). Once
    ///   over the threshold, the next decision is to go eat (break cell, or home).
    /// - <b>Home</b>: the designated tile the character returns to and idles on, with an occasional
    ///   idle fidget (the janitor's "thinking" animation).
    /// - <b>Tasks</b>: <see cref="StaffTask"/> = walk → face → animate for N seconds → callback,
    ///   with claims so two staff never take the same job.
    /// - <b>Hiring</b>: optional <see cref="StaffManager"/> spawns, fires and pays staff. Staff placed
    ///   in the scene by hand work on their own and register with the manager if there is one.
    ///
    /// Decision loop (every <see cref="thinkInterval"/> s while idle or walking home, and instantly
    /// when a role calls <see cref="RequestThink"/>):
    /// <code>
    /// hungry? ─yes─► Eat task
    ///    │no
    /// role work? (TryStartWork) ─yes─► role task
    ///    │no
    /// go home ─► idle (+ fidget)
    /// </code>
    /// Waits are frame timers on scaled time, so pausing the museum freezes staff too.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class StaffAgent : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string staffName = "";

        [Header("Home (designated idle tile)")]
        [Tooltip("Use the tile the character stands on when Play starts as its home tile.")]
        [SerializeField] private bool homeIsStartPosition = true;
        [Tooltip("Home tile (cell coords) when 'Home Is Start Position' is off.")]
        [SerializeField] private Vector2Int homeCell;
        [Tooltip("Direction to face while idling at home. (0,-1) or (-1,0) face the camera.")]
        [SerializeField] private Vector2Int homeFacing = new Vector2Int(0, -1);

        [Header("Movement")]
        [Tooltip("Seconds per tile. The player walks ~0.53 s/tile.")]
        [SerializeField, Min(0.05f)] private float stepDuration = 0.5f;
        [Tooltip("Same switch as PlayerController/GuestController: which axis uses the mirrored art.")]
        [SerializeField] private bool flipOnXAxis = true;

        [Header("Hunger")]
        [Tooltip("Current hunger, 0 = full, 100 = starving. Editable for testing.")]
        [SerializeField, Range(0f, 100f)] private float hunger;
        [Tooltip("Hunger gained per museum clock tick (10 game minutes). 1.5 → hungry after ~8 game hours.")]
        [SerializeField, Min(0f)] private float hungerPerTick = 1.5f;
        [SerializeField, Range(1f, 100f)] private float hungryThreshold = 70f;
        [Tooltip("Real seconds spent eating.")]
        [SerializeField, Min(0.5f)] private float eatDuration = 6f;
        [Tooltip("Eat at a break tile instead of at home.")]
        [SerializeField] private bool hasBreakCell;
        [SerializeField] private Vector2Int breakCell;

        [Header("Decisions")]
        [Tooltip("How often an idle (or homeward) character looks for work, in seconds.")]
        [SerializeField, Min(0.1f)] private float thinkInterval = 1.5f;

        [Header("Idle fidget (at home)")]
        [SerializeField] private Vector2 fidgetIntervalRange = new Vector2(8f, 18f);
        [SerializeField, Min(0f)] private float fidgetDuration = 2.5f;
        [SerializeField] private string fidgetFrontState = "thinking_transition_front";
        [SerializeField] private string fidgetBackState = "thinking_transition_back";

        [Header("Animator states")]
        [SerializeField] private string walkDownState = "walk_forward";
        [SerializeField] private string walkUpState = "walk_backward";
        [SerializeField] private string idleDownState = "idle_front_facing";
        [SerializeField] private string idleUpState = "idle_back_facing";
        [SerializeField] private string eatFrontState = "eat_front";
        [SerializeField] private string eatBackState = "eat_back";

        [Header("Sorting")]
        [Tooltip("Must match the placed-object layer, like PlayerController.")]
        [SerializeField] private string placedObjectSortingLayer = "PlacedMuseumObject";

        [Header("Debug")]
        [SerializeField] protected bool logDecisions;

        [InjectOptional] protected MuseumDataModel Model;

        // Components.
        private Grid _grid;
        private Animator _animator;
        private SheetSpriteRenderer _sheet;
        private SpriteRenderer[] _renderers;
        private MuseumSortingSystem _sorting;
        private StaffManager _manager;

        // Navigation.
        private GuestPathfinder _pathfinder;
        private readonly List<Vector2Int> _path = new List<Vector2Int>(64);
        private readonly List<Vector2Int> _scratchPath = new List<Vector2Int>(64);
        private int _pathIndex;
        private Vector2Int _goal;
        private int _seed;

        // Current step.
        private bool _stepping;
        private Vector2Int _stepTarget;
        private Vector3 _stepFrom, _stepTo;
        private float _stepTime;

        // Brain.
        private StaffActivity _activity = StaffActivity.Idle;
        private StaffTask _task;
        private float _taskTimer;
        private float _thinkTimer;
        private float _fidgetTimer;
        private float _fidgetLeft;
        private System.Action _onLeft;
        private bool _placedByManager;
        private bool _started;

        // Presentation.
        private bool _facingDown = true;
        private bool _flipped;
        private string _currentState;
        private Vector2Int _sortedCell;
        private bool _sortRegistered;

        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1)
        };

        // ── Public surface ─────────────────────────────────────────────────

        public abstract StaffRole Role { get; }
        public string StaffName => string.IsNullOrEmpty(staffName) ? $"{Role} {GetInstanceID()}" : staffName;
        public Vector2Int Cell { get; private set; }
        public Vector2Int HomeCell { get; private set; }
        public StaffActivity Activity => _activity;
        public string CurrentTaskName => _task?.Name;
        public float Hunger => hunger;
        public bool IsHungry => hunger >= hungryThreshold;
        public StaffManager Manager => _manager;

        /// <summary>Move the designated idle tile (e.g. from a staff UI). Walks there when free.</summary>
        public void SetHome(Vector2Int cell)
        {
            HomeCell = SnapToWalkable(cell);
            if (_activity == StaffActivity.Idle && _task == null) RequestThink();
        }

        /// <summary>Look for work on the next frame instead of waiting for the think timer.</summary>
        public void RequestThink() => _thinkTimer = 0f;

        // ── Lifecycle ──────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            _sheet = GetComponentInChildren<SheetSpriteRenderer>(true);
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _pathfinder = new GuestPathfinder(CanStep);
            _seed = GetInstanceID();
        }

        protected virtual void OnEnable()
        {
            MuseumActions.OnTimeUpdated += OnClockTick;
            BuilderActions.OnMuseumDataReloaded += OnMuseumReloaded;
        }

        protected virtual void OnDisable()
        {
            MuseumActions.OnTimeUpdated -= OnClockTick;
            BuilderActions.OnMuseumDataReloaded -= OnMuseumReloaded;
        }

        /// <summary>Called by <see cref="StaffManager.Hire"/> right after instantiating, before Start.</summary>
        public void BeginHired(Vector2Int startCell, Vector2Int home, StaffManager manager)
        {
            _placedByManager = true;
            _manager = manager;
            hunger = Random.Range(0f, hungryThreshold * 0.4f);
            Cell = startCell;
            HomeCell = home;
        }

        protected virtual void Start()
        {
            Model?.EnsureInitialized();
            if (_grid == null) _grid = FindFirstObjectByType<Grid>();
            if (_grid == null)
            {
                Debug.LogError($"[{GetType().Name}] No Grid in the scene — disabled.", this);
                enabled = false;
                return;
            }
            if (Model == null)
                Debug.LogWarning($"[{GetType().Name}] No MuseumDataModel injected — keep '{name}' under " +
                                 "the SceneContext in Museum.unity. The character will stand still.", this);

            if (!_placedByManager)
            {
                Cell = SnapToWalkable(WorldToCell(transform.position));
                HomeCell = homeIsStartPosition ? Cell : SnapToWalkable(homeCell);
                _manager = FindFirstObjectByType<StaffManager>();
                if (_manager != null) _manager.Register(this);
            }
            else
            {
                HomeCell = SnapToWalkable(HomeCell);
            }

            transform.position = CellToWorld(Cell);
            ApplySortingLayer();
            UpdateSorting(Cell);
            ValidateAnimator();

            _activity = StaffActivity.Idle;
            if (Cell == HomeCell) Face(homeFacing);
            _thinkTimer = Random.Range(0f, thinkInterval); // stagger staff so they don't all think on one frame
            _fidgetTimer = RandomRange(fidgetIntervalRange);
            _started = true;
            OnStarted();
        }

        /// <summary>Hook for roles, after position/home/sorting are set up.</summary>
        protected virtual void OnStarted() { }

        protected virtual void OnDestroy()
        {
            if (_task != null) StaffTaskBoard.Release(_task.ClaimKey, this);
            if (_sortRegistered && _sorting != null) _sorting.UnregisterObjectDeferred(gameObject);
            if (_manager != null) _manager.Unregister(this);
        }

        // ── Main loop ──────────────────────────────────────────────────────

        private void Update()
        {
            if (!_started) return;
            var dt = Time.deltaTime;

            if (_stepping)
            {
                ContinueStep(dt);
                // Chain into the next tile (or the arrival) on the same frame — a gap frame would
                // flash idle and restart the walk clip on every tile.
                if (!_stepping)
                {
                    if (_pathIndex < _path.Count) StartNextStep();
                    else if (_activity == StaffActivity.Walking || _activity == StaffActivity.Leaving) OnArrived();
                }
            }
            else if (_pathIndex < _path.Count)
            {
                StartNextStep();
            }
            else if (_activity == StaffActivity.Walking || _activity == StaffActivity.Leaving)
            {
                OnArrived();
            }
            else if (_activity == StaffActivity.Working)
            {
                TickWork(dt);
            }

            // Free (no task): look for work — also while walking home, so new trash diverts him.
            if (_task == null && _activity != StaffActivity.Leaving && _activity != StaffActivity.Working)
            {
                _thinkTimer -= dt;
                if (_thinkTimer <= 0f)
                {
                    _thinkTimer = thinkInterval;
                    // Nothing to do and standing somewhere other than home (route got blocked,
                    // home tile moved) → head back.
                    if (!TryFindSomethingToDo() && _activity == StaffActivity.Idle && Cell != HomeCell)
                        GoHome();
                }
            }

            if (_activity == StaffActivity.Idle) TickFidget(dt);
            UpdateAnimation();
        }

        private void TickWork(float dt)
        {
            if (_task == null)
            {
                EnterIdle();
                return;
            }

            // Cheap re-check (the trash got covered by a new exhibit, the exhibit was sold).
            if (_task.IsStillValid != null && !_task.IsStillValid())
            {
                AbortTask("target gone");
                Decide();
                return;
            }

            _taskTimer -= dt;
            if (_taskTimer <= 0f) CompleteTask();
        }

        private void TickFidget(float dt)
        {
            if (_fidgetLeft > 0f)
            {
                _fidgetLeft -= dt;
                return;
            }

            if (Cell != HomeCell || fidgetDuration <= 0f) return;
            _fidgetTimer -= dt;
            if (_fidgetTimer > 0f) return;
            _fidgetTimer = RandomRange(fidgetIntervalRange);
            _fidgetLeft = fidgetDuration;
        }

        // ── Decisions ──────────────────────────────────────────────────────

        /// <summary>
        /// The role's job: pick work and start it with <see cref="TryAssign"/>. Return true if a
        /// task was assigned. Called only when the character has no task and isn't hungry.
        /// </summary>
        protected abstract bool TryStartWork();

        /// <summary>Hungry → eat, otherwise role work. True if something was assigned.</summary>
        protected bool TryFindSomethingToDo()
        {
            if (_task != null) return true;
            if (IsHungry && TryAssign(BuildEatTask())) return true;
            return TryStartWork();
        }

        /// <summary>After a task: next job, or back to the home tile.</summary>
        protected void Decide()
        {
            if (!TryFindSomethingToDo()) GoHome();
        }

        private void GoHome()
        {
            if (Cell == HomeCell)
            {
                EnterIdle();
                return;
            }

            if (SetDestination(HomeCell))
            {
                _activity = StaffActivity.Walking;
                Log($"heading home to {HomeCell}");
            }
            else
            {
                Log($"can't reach home {HomeCell}, idling at {Cell}");
                EnterIdle();
            }
        }

        private void EnterIdle()
        {
            _activity = StaffActivity.Idle;
            _path.Clear();
            _pathIndex = 0;
            if (Cell == HomeCell) Face(homeFacing);
        }

        private StaffTask BuildEatTask()
        {
            var spot = hasBreakCell && IsWalkable(breakCell) ? breakCell : HomeCell;
            return new StaffTask
            {
                Name = "Eat",
                StandCell = spot,
                FaceCell = spot == HomeCell ? spot + homeFacing : (Vector2Int?)null,
                FrontState = eatFrontState,
                BackState = eatBackState,
                Duration = eatDuration,
                OnCompleted = () => hunger = 0f,
            };
        }

        // ── Tasks ──────────────────────────────────────────────────────────

        /// <summary>
        /// Claim the task and start walking to it. False (and nothing changes) if it's claimed by
        /// someone else, unreachable, or this character is already busy.
        /// </summary>
        protected bool TryAssign(StaffTask task)
        {
            if (task == null || _task != null || _activity == StaffActivity.Leaving) return false;
            if (!StaffTaskBoard.TryClaim(task.ClaimKey, this)) return false;

            if (!SetDestination(task.StandCell))
            {
                StaffTaskBoard.Release(task.ClaimKey, this);
                return false;
            }

            _task = task;
            _activity = StaffActivity.Walking;
            _fidgetLeft = 0f;
            Log($"→ {task.Name} at {task.StandCell} ({_path.Count} tiles)");
            return true;
        }

        private void OnArrived()
        {
            if (_activity == StaffActivity.Leaving)
            {
                FinishLeaving();
                return;
            }

            if (_task == null)
            {
                EnterIdle();
                return;
            }

            if (_task.IsStillValid != null && !_task.IsStillValid())
            {
                AbortTask("target gone on arrival");
                Decide();
                return;
            }

            if (_task.FaceCell.HasValue) Face(_task.FaceCell.Value - Cell);
            _activity = StaffActivity.Working;
            _taskTimer = _task.Duration;
        }

        private void CompleteTask()
        {
            var task = _task;
            _task = null;
            StaffTaskBoard.Release(task.ClaimKey, this);
            _activity = StaffActivity.Idle;
            Log($"done: {task.Name}");
            task.OnCompleted?.Invoke();
            OnTaskCompleted(task);
            Decide();
        }

        private void AbortTask(string reason)
        {
            if (_task == null) return;
            var task = _task;
            _task = null;
            StaffTaskBoard.Release(task.ClaimKey, this);
            _activity = StaffActivity.Idle;
            Log($"aborted {task.Name}: {reason}");
            task.OnAborted?.Invoke();
        }

        /// <summary>Hook after any task finishes (before the next decision).</summary>
        protected virtual void OnTaskCompleted(StaffTask task) { }

        // ── Dismissal (StaffManager.Fire) ─────────────────────────────────

        /// <summary>Drop everything, walk to <paramref name="exitCell"/>, then call <paramref name="onLeft"/>.</summary>
        public void Dismiss(Vector2Int exitCell, System.Action onLeft)
        {
            AbortTask("dismissed");
            _onLeft = onLeft;
            _activity = StaffActivity.Leaving;
            if (Cell == exitCell || !SetDestination(exitCell)) FinishLeaving();
        }

        private void FinishLeaving()
        {
            _activity = StaffActivity.Idle;
            var callback = _onLeft;
            _onLeft = null;
            if (callback != null) callback();
            else Destroy(gameObject);
        }

        // ── Walking ────────────────────────────────────────────────────────

        /// <summary>
        /// Plans into a scratch list and only replaces the current route on success, so a failed
        /// lookup (e.g. unreachable trash while walking home) never strands the character.
        /// </summary>
        private bool SetDestination(Vector2Int goal)
        {
            if (Model == null) return false;

            // Mid-step, plan from the tile being entered so the next step stays adjacent.
            var origin = _stepping ? _stepTarget : Cell;
            if (!_pathfinder.FindPath(origin, goal, _scratchPath, _seed)) return false;

            _path.Clear();
            _path.AddRange(_scratchPath);
            _goal = goal;
            _pathIndex = 0;
            return true;
        }

        private void StartNextStep()
        {
            var next = _path[_pathIndex];

            // The museum can change under a walking character (an exhibit placed on the route).
            if (!CanStep(Cell, next))
            {
                if (!SetDestination(_goal) || _path.Count == 0)
                {
                    OnPathFailed();
                    return;
                }
                next = _path[0];
            }

            _stepTarget = next;
            _stepFrom = transform.position;
            _stepTo = CellToWorld(next);
            _stepTime = 0f;
            _stepping = true;
            Face(next - Cell);
        }

        private void ContinueStep(float dt)
        {
            _stepTime += dt;
            var t = Mathf.Clamp01(_stepTime / stepDuration);
            transform.position = Vector3.Lerp(_stepFrom, _stepTo, t);

            // Hand depth over halfway, where the character visually crosses tiles (player rule).
            if (t >= 0.5f) UpdateSorting(_stepTarget);
            if (t < 1f) return;

            transform.position = _stepTo;
            _stepping = false;
            _pathIndex++;
            Cell = _stepTarget;
        }

        private void OnPathFailed()
        {
            _path.Clear();
            _pathIndex = 0;

            if (_activity == StaffActivity.Leaving)
            {
                FinishLeaving();
                return;
            }

            if (_task != null)
            {
                AbortTask("route blocked");
                Decide();
                return;
            }

            EnterIdle();
        }

        private bool CanStep(Vector2Int from, Vector2Int to) => IsWalkable(to);

        protected bool IsWalkable(Vector2Int cell) =>
            Model != null && Model.TryGetTile(cell, out var tile) && tile.Walkable;

        /// <summary>The cell itself if walkable, else the nearest walkable museum tile.</summary>
        protected Vector2Int SnapToWalkable(Vector2Int cell)
        {
            if (Model == null || IsWalkable(cell)) return cell;

            var best = cell;
            var bestDist = int.MaxValue;
            foreach (var tile in Model.Tiles)
            {
                if (!tile.Walkable) continue;
                var d = Manhattan(cell, tile.Cell);
                if (d >= bestDist) continue;
                bestDist = d;
                best = tile.Cell;
            }
            return best;
        }

        /// <summary>
        /// Walkable tiles touching an object's footprint, each with the footprint cell it faces.
        /// Camera-facing sides (low X / low Y) first, so the character's work reads clearly.
        /// </summary>
        protected void GetSpotsAround(PlacedObjectData obj, List<(Vector2Int stand, Vector2Int face)> spots)
        {
            spots.Clear();
            if (obj == null) return;

            var min = obj.AnchorCell;
            var max = min + new Vector2Int(Mathf.Max(1, obj.WidthInTiles) - 1, Mathf.Max(1, obj.LengthInTiles) - 1);
            var frontCount = 0;

            for (var x = min.x; x <= max.x; x++)
            {
                AddSpot(spots, new Vector2Int(x, min.y - 1), new Vector2Int(x, min.y), true, ref frontCount);
                AddSpot(spots, new Vector2Int(x, max.y + 1), new Vector2Int(x, max.y), false, ref frontCount);
            }
            for (var y = min.y; y <= max.y; y++)
            {
                AddSpot(spots, new Vector2Int(min.x - 1, y), new Vector2Int(min.x, y), true, ref frontCount);
                AddSpot(spots, new Vector2Int(max.x + 1, y), new Vector2Int(max.x, y), false, ref frontCount);
            }

            Shuffle(spots, 0, frontCount);
            Shuffle(spots, frontCount, spots.Count);
        }

        private void AddSpot(List<(Vector2Int, Vector2Int)> spots, Vector2Int stand, Vector2Int face, bool front, ref int frontCount)
        {
            if (!IsWalkable(stand)) return;
            if (front) spots.Insert(frontCount++, (stand, face));
            else spots.Add((stand, face));
        }

        /// <summary>A walkable tile next to <paramref name="cell"/>, or false.</summary>
        protected bool TryGetWalkableNeighbour(Vector2Int cell, out Vector2Int neighbour)
        {
            foreach (var d in Neighbours)
            {
                neighbour = cell + d;
                if (IsWalkable(neighbour)) return true;
            }
            neighbour = cell;
            return false;
        }

        // ── Needs ──────────────────────────────────────────────────────────

        private void OnClockTick(int minutes, int hours, int days, int months, int years)
        {
            if (_task != null && _task.Name == "Eat" && _activity == StaffActivity.Working) return;
            hunger = Mathf.Min(100f, hunger + hungerPerTick);
        }

        private void OnMuseumReloaded()
        {
            if (!_started) return;
            AbortTask("museum reloaded");
            _stepping = false;
            Cell = SnapToWalkable(Cell);
            HomeCell = SnapToWalkable(HomeCell);
            transform.position = CellToWorld(Cell);
            UpdateSorting(Cell);
            EnterIdle();
            RequestThink();
        }

        // ── Sorting ────────────────────────────────────────────────────────

        private void ApplySortingLayer()
        {
            // YSortable would fight MuseumSortingSystem over sortingOrder (same as the player).
            foreach (var y in GetComponentsInChildren<YSortable>(true)) y.enabled = false;

            var layerId = SortingLayer.NameToID(placedObjectSortingLayer);
            if (!SortingLayer.IsValid(layerId))
            {
                Debug.LogError($"[{GetType().Name}] Sorting layer '{placedObjectSortingLayer}' does not exist.", this);
                return;
            }
            foreach (var r in _renderers)
                if (r != null) r.sortingLayerID = layerId;
        }

        private void UpdateSorting(Vector2Int cell)
        {
            if (_sortRegistered && cell == _sortedCell) return;

            // Added at runtime by MuseumObjectPlacementSystem, so look it up lazily.
            if (_sorting == null)
            {
                _sorting = FindFirstObjectByType<MuseumSortingSystem>();
                if (_sorting == null) return;
            }

            _sortedCell = cell;
            _sortRegistered = true;
            _sorting.UpdateObjectFootprintDeferred(gameObject, cell, 1, 1);
        }

        // ── Facing / animation ─────────────────────────────────────────────

        private void Face(Vector2Int direction)
        {
            if (direction == Vector2Int.zero) return;
            direction = new Vector2Int(System.Math.Sign(direction.x), System.Math.Sign(direction.y));
            if (direction.x != 0 && direction.y != 0) direction.y = 0; // diagonal → pick one axis

            // -X / -Y head toward the camera; the art is drawn down-right / up-left, so X-axis
            // directions are the mirrored halves (same as PlayerController).
            _facingDown = direction.x < 0 || direction.y < 0;
            var flip = direction.x != 0;
            SetFlip(flipOnXAxis ? flip : !flip);
        }

        private void SetFlip(bool flip)
        {
            if (flip == _flipped) return;
            _flipped = flip;
            if (_sheet != null) _sheet.FlipX = flip;
            else if (_renderers.Length > 0 && _renderers[0] != null) _renderers[0].flipX = flip;
        }

        private void UpdateAnimation()
        {
            if (_stepping)
                PlayState(_facingDown ? walkDownState : walkUpState);
            else if (_activity == StaffActivity.Working && _task != null)
                PlayState(Pick(_facingDown ? _task.FrontState : _task.BackState, _facingDown ? idleDownState : idleUpState));
            else if (_activity == StaffActivity.Idle && _fidgetLeft > 0f)
                PlayState(Pick(_facingDown ? fidgetFrontState : fidgetBackState, _facingDown ? idleDownState : idleUpState));
            else
                PlayState(_facingDown ? idleDownState : idleUpState);
        }

        private static string Pick(string preferred, string fallback) =>
            string.IsNullOrEmpty(preferred) ? fallback : preferred;

        // Animator.Play restarts a state it's already in, which would pin loops on frame 0.
        private void PlayState(string state)
        {
            if (_animator == null || string.IsNullOrEmpty(state) || state == _currentState) return;
            _currentState = state;
            _animator.Play(state, 0, 0f);
        }

        /// <summary>Every state this character may play; roles add theirs.</summary>
        protected virtual IEnumerable<string> AnimatorStates()
        {
            yield return walkDownState;
            yield return walkUpState;
            yield return idleDownState;
            yield return idleUpState;
            yield return eatFrontState;
            yield return eatBackState;
            yield return fidgetFrontState;
            yield return fidgetBackState;
        }

        private void ValidateAnimator()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"[{GetType().Name}] '{name}' has no Animator/controller — it will move but not animate.", this);
                return;
            }

            foreach (var state in AnimatorStates())
                if (!string.IsNullOrEmpty(state) && !_animator.HasState(0, Animator.StringToHash(state)))
                    Debug.LogWarning($"[{GetType().Name}] Animator '{_animator.runtimeAnimatorController.name}' " +
                                     $"has no state '{state}'.", this);
        }

        // ── Coordinates & helpers ──────────────────────────────────────────

        /// <summary>Cell corner (front vertex) — where a bottom-pivoted character stands.</summary>
        protected Vector3 CellToWorld(Vector2Int cell)
        {
            var local = _grid.CellToLocalInterpolated(new Vector3(cell.x, cell.y, 0f));
            var world = _grid.transform.TransformPoint(local);
            world.z = 0f;
            return world;
        }

        private Vector2Int WorldToCell(Vector3 world)
        {
            var c = _grid.LocalToCellInterpolated(_grid.transform.InverseTransformPoint(world));
            return new Vector2Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y));
        }

        protected static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static float RandomRange(Vector2 r) => Random.Range(r.x, r.y);

        protected static void Shuffle<T>(List<T> list, int from, int to)
        {
            for (var i = to - 1; i > from; i--)
            {
                var j = Random.Range(from, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        protected void Log(string message)
        {
            if (logDecisions) Debug.Log($"[{StaffName}] {message}", this);
        }

        // ── Gizmos ─────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            var grid = _grid != null ? _grid : FindFirstObjectByType<Grid>();
            if (grid == null) return;

            Vector2Int home;
            if (Application.isPlaying && _started) home = HomeCell;
            else if (homeIsStartPosition)
            {
                var c = grid.LocalToCellInterpolated(grid.transform.InverseTransformPoint(transform.position));
                home = new Vector2Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y));
            }
            else home = homeCell;

            DrawCell(grid, home, new Color(0.3f, 1f, 0.4f, 0.9f));
            if (hasBreakCell) DrawCell(grid, breakCell, new Color(1f, 0.7f, 0.2f, 0.9f));
        }

        private static void DrawCell(Grid grid, Vector2Int cell, Color color)
        {
            Vector3 P(float x, float y) => grid.transform.TransformPoint(grid.CellToLocalInterpolated(new Vector3(x, y, 0f)));
            Gizmos.color = color;
            var a = P(cell.x, cell.y);
            var b = P(cell.x + 1, cell.y);
            var c = P(cell.x + 1, cell.y + 1);
            var d = P(cell.x, cell.y + 1);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
