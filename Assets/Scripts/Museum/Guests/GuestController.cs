using System;
using System.Collections.Generic;
using ProjectMuseum.Builder;
using ProjectMuseum.Characters;
using ProjectMuseum.Data;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace ProjectMuseum.Guests
{
    /// <summary>
    /// Spawns, pools and removes guests, and owns everything guests share: the walk graph
    /// (outside sidewalks + museum floor joined only at the door), appearance pools, spawn/exit
    /// points, viewing-spot reservations and museum population counts.
    ///
    /// Port of Godot's <c>GuestsController</c> + the shared parts of <c>Guest.cs</c>. Behaviour
    /// per guest lives in <see cref="GuestAgent"/>.
    ///
    /// Setup: add this to an empty GameObject inside the Museum scene (it needs the SceneContext
    /// to inject <see cref="MuseumDataModel"/>). The existing scene object named "Guest" is used as
    /// the template if Guest Template is empty — it gets hidden and cloned. All cell values below
    /// can be read off the scene view with the World/Cell mouse overlay.
    /// </summary>
    [AddComponentMenu("Project Museum/Guest Controller")]
    public class GuestController : MonoBehaviour
    {
        [Serializable]
        public class AppearancePool
        {
            [Tooltip("Just a label (e.g. Hair).")]
            public string partName;

            [Tooltip("Index of the layer in the guest's SheetSpriteGroup that this pool drives.")]
            public int layerIndex;

            [Tooltip("Chance (0-1) the layer is hidden instead, e.g. some guests without an over-cloth.")]
            [Range(0f, 1f)] public float hiddenChance;

            public List<Texture2D> sheets = new List<Texture2D>();
        }

        [Serializable]
        public class ReactionClip
        {
            [Tooltip("State played when the exhibit is toward the camera (guest faces down).")]
            public string frontState = "excited_front";

            [Tooltip("State played when the exhibit is away from the camera (guest faces up).")]
            public string backState = "excited_back";

            [Tooltip("Seconds the reaction holds before returning to idle.")]
            [Min(0.1f)] public float duration = 1.5f;
        }

        [InjectOptional] private MuseumDataModel _model;

        // ── References ─────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The museum's isometric Grid. Found in the scene if empty.")]
        [SerializeField] private Grid grid;

        [Tooltip("Guest rig to clone (SheetSpriteGroup + Animator). A prefab or a scene object. " +
                 "Empty = the scene object named 'Guest'; a scene template is hidden on Start.")]
        [SerializeField] private GameObject guestTemplate;

        [Tooltip("Parent for spawned guests. This transform if empty.")]
        [SerializeField] private Transform guestParent;

        // ── Appearance ─────────────────────────────────────────────────────

        [Header("Appearance")]
        [Tooltip("One pool per body part; each guest picks one sheet from every pool. Use the " +
                 "'Auto-fill From Guest Sheets' button to fill these from GUEST_ANIMATION_ASSETS.")]
        [SerializeField] private List<AppearancePool> appearancePools = new List<AppearancePool>();

        // ── Spawning ───────────────────────────────────────────────────────

        [Header("Spawning")]
        [Tooltip("Cells where guests appear AND are removed (the road ends). Drag the handles in " +
                 "the Scene view with this object selected.")]
        [SerializeField] private List<Vector2Int> spawnPoints = new List<Vector2Int>
        {
            new Vector2Int(-2, 60), new Vector2Int(62, -2), new Vector2Int(-19, -2), new Vector2Int(-2, -19)
        };

        [Tooltip("Seconds between spawns (random in range).")]
        [SerializeField] private Vector2 spawnIntervalRange = new Vector2(2f, 5f);

        [Tooltip("Most guests alive at once, inside and outside.")]
        [Min(0)] [SerializeField] private int maxGuests = 30;

        [Tooltip("Guests placed mid-walk on Start so the street isn't empty for the first minute.")]
        [Min(0)] [SerializeField] private int prewarmGuests = 6;

        [Tooltip("An exit point must be at least this many cells (Manhattan) from where the guest " +
                 "spawned — otherwise guests pop in and straight back out.")]
        [Min(0)] [SerializeField] private int minTravelDistance = 20;

        // ── Outside walking ────────────────────────────────────────────────

        [Header("Outside Walking")]
        [Tooltip("Walkable outside areas (sidewalks, crossings) in cells. xMin/yMin + width/height. " +
                 "Drawn in the Scene view. Museum floor cells inside a rect are ignored — the museum " +
                 "is only entered through the door.")]
        [SerializeField] private List<RectInt> outsideWalkAreas = new List<RectInt>
        {
            new RectInt(-20, -3, 84, 3), // along the SE (front-right) museum edge, road end to road end
            new RectInt(-3, -20, 3, 82)  // along the SW (front-left) museum edge
        };

        [Tooltip("Optional: any painted cell on this Tilemap is also walkable outside. Handy for " +
                 "odd shapes — paint any tile and disable its TilemapRenderer.")]
        [SerializeField] private Tilemap outsideWalkTilemap;

        [Tooltip("Random outside stops before heading to the exit (x = min, y = max).")]
        [SerializeField] private Vector2Int wanderStopsRange = new Vector2Int(0, 1);

        [Tooltip("Seconds paused at a wander stop.")]
        [SerializeField] private Vector2 wanderPauseRange = new Vector2(1f, 3f);

        [Tooltip("Seconds per tile (random per guest). Lower = faster.")]
        [SerializeField] private Vector2 stepDurationRange = new Vector2(0.38f, 0.55f);

        [Tooltip("Max visual offset inside a tile while outside (cell units), so crowds spread across " +
                 "the sidewalk. Zero inside the museum, where depth sorting is per cell.")]
        [Range(0f, 0.45f)] [SerializeField] private float laneJitter = 0.3f;

        // ── Museum ─────────────────────────────────────────────────────────

        [Header("Museum")]
        [Tooltip("Closed = nobody decides to come in (guests already inside finish their visit).")]
        [SerializeField] private bool museumOpen = true;

        [Tooltip("Museum floor cell just inside the entrance.")]
        [SerializeField] private Vector2Int doorInsideCell = new Vector2Int(5, 0);

        [Tooltip("Sidewalk cell just outside the entrance. Must be orthogonally next to the inside " +
                 "cell — this pair is the only link between outside and the museum.")]
        [SerializeField] private Vector2Int doorOutsideCell = new Vector2Int(5, -1);

        [Tooltip("Chance (0-1) a passer-by decides to go in when they get near the door. Rolled once per guest.")]
        [Range(0f, 1f)] [SerializeField] private float enterChance = 0.05f;

        [Tooltip("'Near the museum' = within this many cells (Manhattan) of the outside door cell.")]
        [Min(0)] [SerializeField] private int approachRadius = 6;

        [Tooltip("Nobody else decides to come in while this many are inside or on their way.")]
        [Min(0)] [SerializeField] private int maxGuestsInside = 15;

        [Tooltip("How many exhibits a guest wants to see (random in range, capped by what exists).")]
        [SerializeField] private Vector2Int exhibitsToVisitRange = new Vector2Int(2, 5);

        [Tooltip("Seconds spent in front of each exhibit.")]
        [SerializeField] private Vector2 viewTimeRange = new Vector2(3f, 8f);

        [Tooltip("With no exhibits to see, a visitor wanders to this many random floor cells, then leaves.")]
        [SerializeField] private Vector2Int lookAroundRange = new Vector2Int(1, 3);

        [Tooltip("Chance (0-1) of playing a reaction when arriving at an exhibit.")]
        [Range(0f, 1f)] [SerializeField] private float reactionChance = 0.4f;

        [SerializeField] private List<ReactionClip> reactions = new List<ReactionClip>
        {
            new ReactionClip { frontState = "intrigue_front", backState = "intrigue_back", duration = 1.2f },
            new ReactionClip { frontState = "excited_front", backState = "excited_back", duration = 2.4f }
        };

        // ── Sorting / animation ────────────────────────────────────────────

        [Header("Sorting")]
        [Tooltip("Layer used outside. Last in the layer list, so guests draw over the street, " +
                 "forest and the museum's front walls; guests Y-sort among themselves.")]
        [SerializeField] private string outsideSortingLayer = "Guest";

        [Tooltip("Layer used inside. MUST match placed objects (same as PlayerController) so " +
                 "MuseumSortingSystem can order guests against exhibits.")]
        [SerializeField] private string insideSortingLayer = "PlacedMuseumObject";

        [Tooltip("Outside sorting order per world unit of Y. Keep (y range × this × 8) under 32767.")]
        [Min(1)] [SerializeField] private int outsideOrderPerUnit = 40;

        [Header("Animation")]
        [SerializeField] private string walkDownState = "walk_forward";
        [SerializeField] private string walkUpState = "walk_backward";
        [SerializeField] private string idleDownState = "idle_front_facing";
        [SerializeField] private string idleUpState = "idle_back_facing";

        [Tooltip("Flip when moving along the cell X axis (same convention as the player). Turn off " +
                 "if guests moonwalk.")]
        [SerializeField] private bool flipOnXAxis = true;

        [Header("Diagnostics")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool logDecisions;

        // ── Runtime ────────────────────────────────────────────────────────

        private readonly List<GuestAgent> _active = new List<GuestAgent>();
        private readonly Stack<GuestAgent> _pool = new Stack<GuestAgent>();
        private readonly HashSet<Vector2Int> _reservedViewCells = new HashSet<Vector2Int>();
        private readonly List<(Vector2Int, Vector2Int)> _frontSpots = new List<(Vector2Int, Vector2Int)>();
        private readonly List<(Vector2Int, Vector2Int)> _sideSpots = new List<(Vector2Int, Vector2Int)>();
        private readonly List<(Vector2Int, Vector2Int)> _takenSpots = new List<(Vector2Int, Vector2Int)>();
        private GuestPathfinder _pathfinder;
        private MuseumSortingSystem _sorting;
        private float _spawnTimer;
        private int _nextId;
        private int _outsideLayerId, _insideLayerId;
        private bool _warnedNoModel;

        public event Action<GuestAgent> GuestSpawned;
        public event Action<GuestAgent> GuestEnteredMuseum;
        public event Action<GuestAgent> GuestExitedMuseum;
        public event Action<GuestAgent> GuestDespawned;

        public IReadOnlyList<GuestAgent> ActiveGuests => _active;
        public int GuestsInMuseum { get; private set; }
        public bool MuseumOpen { get => museumOpen; set => museumOpen = value; }

        public Grid Grid => grid;
        public Vector2Int DoorInsideCell => doorInsideCell;
        public Vector2Int DoorOutsideCell => doorOutsideCell;
        public int ApproachRadius => approachRadius;
        public float LaneJitterAmount => laneJitter;
        public bool LogDecisions => logDecisions;
        public int OutsideLayerId => _outsideLayerId;
        public int InsideLayerId => _insideLayerId;
        public int OutsideOrderPerUnit => outsideOrderPerUnit;
        public string WalkDownState => walkDownState;
        public string WalkUpState => walkUpState;
        public string IdleDownState => idleDownState;
        public string IdleUpState => idleUpState;
        public bool FlipOnXAxis => flipOnXAxis;
        public Vector2 ViewTimeRange => viewTimeRange;
        public Vector2 WanderPauseRange => wanderPauseRange;
        public Vector2Int WanderStopsRange => wanderStopsRange;
        public Vector2Int LookAroundRange => lookAroundRange;
        public List<Vector2Int> SpawnPoints => spawnPoints; // exposed for the editor handles

        /// <summary>Found lazily: MuseumObjectPlacementSystem adds it at runtime.</summary>
        public MuseumSortingSystem Sorting
        {
            get
            {
                if (_sorting == null) _sorting = FindFirstObjectByType<MuseumSortingSystem>();
                return _sorting;
            }
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<Grid>();
            if (guestParent == null) guestParent = transform;
            _pathfinder = new GuestPathfinder(CanStep);
        }

        private void Start()
        {
            _model?.EnsureInitialized();
            ResolveTemplate();
            ResolveSortingLayers();
            ValidateSetup();

            for (var i = 0; i < prewarmGuests && _active.Count < maxGuests; i++)
                Spawn(prewarm: true);

            _spawnTimer = RandomRange(spawnIntervalRange);
        }

        private void Update()
        {
            if (guestTemplate == null || spawnPoints.Count == 0) return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;
            _spawnTimer = RandomRange(spawnIntervalRange);

            if (_active.Count < maxGuests) Spawn(prewarm: false);
        }

        // ── Spawning ───────────────────────────────────────────────────────

        /// <summary>Spawns one guest at a random spawn point (or mid-sidewalk when prewarming).</summary>
        public GuestAgent Spawn(bool prewarm)
        {
            if (guestTemplate == null || grid == null) return null;

            Vector2Int startCell;
            if (prewarm && TryGetRandomOutsideCell(out var mid)) startCell = mid;
            else if (spawnPoints.Count > 0) startCell = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];
            else return null;

            var agent = _pool.Count > 0 ? _pool.Pop() : CreateAgent();
            var data = RollGuestData(startCell);

            _active.Add(agent);
            agent.Begin(data, startCell);
            GuestSpawned?.Invoke(agent);
            return agent;
        }

        private GuestAgent CreateAgent()
        {
            // The template may be inactive (hidden scene object) — the clone starts inactive too
            // and Begin() activates it after it's been dressed.
            var go = Instantiate(guestTemplate, guestParent);
            go.name = "Guest (runtime)";
            var agent = go.GetComponent<GuestAgent>();
            if (agent == null) agent = go.AddComponent<GuestAgent>();
            agent.Bind(this);
            return agent;
        }

        private GuestData RollGuestData(Vector2Int spawnCell)
        {
            var data = new GuestData
            {
                Id = _nextId++,
                SpawnCell = spawnCell,
                StepDuration = RandomRange(stepDurationRange),
                LaneJitter = new Vector2(UnityEngine.Random.Range(-laneJitter, laneJitter),
                                         UnityEngine.Random.Range(-laneJitter, laneJitter)),
                ExhibitsToVisit = UnityEngine.Random.Range(exhibitsToVisitRange.x, exhibitsToVisitRange.y + 1),
                SpawnTime = Time.time,
                AppearanceIndices = new int[appearancePools.Count]
            };

            for (var i = 0; i < appearancePools.Count; i++)
            {
                var pool = appearancePools[i];
                var hidden = pool.sheets.Count == 0 || UnityEngine.Random.value < pool.hiddenChance;
                data.AppearanceIndices[i] = hidden ? -1 : UnityEngine.Random.Range(0, pool.sheets.Count);
            }

            return data;
        }

        /// <summary>Applies the rolled sheets to a guest's layers.</summary>
        public void ApplyAppearance(SheetSpriteGroup group, GuestData data)
        {
            if (group == null) return;

            for (var i = 0; i < appearancePools.Count && i < data.AppearanceIndices.Length; i++)
            {
                var pool = appearancePools[i];
                if (pool.layerIndex < 0 || pool.layerIndex >= group.Layers.Count) continue;

                var layer = group.Layers[pool.layerIndex];
                if (layer?.Renderer == null) continue;

                var index = data.AppearanceIndices[i];
                layer.Renderer.Renderer.enabled = index >= 0;
                if (index >= 0) group.SetLayerSheet(pool.layerIndex, pool.sheets[index]);
            }
        }

        /// <summary>Called by the agent when it reaches its exit point (or gets hopelessly stuck).</summary>
        public void Despawn(GuestAgent agent)
        {
            if (agent == null || !_active.Remove(agent)) return;

            if (agent.IsInside) SetInside(agent, false);
            agent.End();
            _pool.Push(agent);
            GuestDespawned?.Invoke(agent);
        }

        // ── Museum population ──────────────────────────────────────────────

        /// <summary>The entry roll: open, under capacity, and lucky.</summary>
        public bool RollEntry()
        {
            if (!museumOpen || _model == null) return false;
            if (CountVisitors() >= maxGuestsInside) return false;
            return UnityEngine.Random.value < enterChance;
        }

        private int CountVisitors()
        {
            var count = 0;
            foreach (var g in _active)
                if (g.IsInside || g.State == GuestState.HeadingToMuseum) count++;
            return count;
        }

        /// <summary>Agent crossed the door. Keeps the count and raises the events.</summary>
        public void SetInside(GuestAgent agent, bool inside)
        {
            if (inside)
            {
                GuestsInMuseum++;
                GuestEnteredMuseum?.Invoke(agent);
            }
            else
            {
                GuestsInMuseum = Mathf.Max(0, GuestsInMuseum - 1);
                GuestExitedMuseum?.Invoke(agent);
            }
        }

        // ── Walk graph ─────────────────────────────────────────────────────

        /// <summary>A museum floor cell (developed, whether or not something stands on it).</summary>
        public bool IsMuseumCell(Vector2Int cell) => _model != null && _model.TryGetTile(cell, out _);

        public bool IsOutsideWalkable(Vector2Int cell)
        {
            if (IsMuseumCell(cell)) return false;

            for (var i = 0; i < outsideWalkAreas.Count; i++)
                if (outsideWalkAreas[i].Contains(cell)) return true;

            if (outsideWalkTilemap != null && outsideWalkTilemap.HasTile(new Vector3Int(cell.x, cell.y, 0)))
                return true;

            // Spawn points count even if they sit a tile off the drawn sidewalk.
            return spawnPoints.Contains(cell);
        }

        /// <summary>
        /// The walk graph's only rule. Outside ↔ museum is allowed solely across the door pair —
        /// that's what stops guests walking through walls, since walls are edges, not cells.
        /// </summary>
        public bool CanStep(Vector2Int from, Vector2Int to)
        {
            var fromIn = IsMuseumCell(from);
            var toIn = IsMuseumCell(to);

            if (fromIn != toIn)
            {
                var isDoor = (from == doorOutsideCell && to == doorInsideCell) ||
                             (from == doorInsideCell && to == doorOutsideCell);
                if (!isDoor) return false;
            }

            if (toIn) return _model.TryGetTile(to, out var tile) && tile.Walkable;
            return IsOutsideWalkable(to);
        }

        public bool FindPath(Vector2Int from, Vector2Int to, List<Vector2Int> result, int seed) =>
            _pathfinder.FindPath(from, to, result, seed);

        public bool TryGetRandomOutsideCell(out Vector2Int cell)
        {
            cell = default;
            if (outsideWalkAreas.Count == 0) return false;

            for (var attempt = 0; attempt < 30; attempt++)
            {
                var r = outsideWalkAreas[UnityEngine.Random.Range(0, outsideWalkAreas.Count)];
                if (r.width <= 0 || r.height <= 0) continue;

                cell = new Vector2Int(UnityEngine.Random.Range(r.xMin, r.xMax), UnityEngine.Random.Range(r.yMin, r.yMax));
                if (IsOutsideWalkable(cell)) return true;
            }

            return false;
        }

        public bool TryGetRandomMuseumFloorCell(out Vector2Int cell)
        {
            cell = default;
            if (_model == null || _model.Tiles.Count == 0) return false;

            for (var attempt = 0; attempt < 30; attempt++)
            {
                var tile = _model.Tiles[UnityEngine.Random.Range(0, _model.Tiles.Count)];
                if (!tile.Walkable) continue;
                cell = tile.Cell;
                return true;
            }

            return false;
        }

        /// <summary>A random spawn point far enough from <paramref name="from"/>; the farthest if none is.</summary>
        public Vector2Int ChooseExitPoint(Vector2Int from)
        {
            if (spawnPoints.Count == 0) return from;

            var start = UnityEngine.Random.Range(0, spawnPoints.Count);
            var farthest = spawnPoints[0];
            var farthestDist = -1;

            for (var i = 0; i < spawnPoints.Count; i++)
            {
                var p = spawnPoints[(start + i) % spawnPoints.Count];
                var d = Manhattan(p, from);
                if (d >= minTravelDistance) return p;
                if (d > farthestDist) { farthestDist = d; farthest = p; }
            }

            return farthest;
        }

        // ── Exhibits ───────────────────────────────────────────────────────

        /// <summary>Current exhibits, shuffled, into <paramref name="ids"/>.</summary>
        public void GetShuffledExhibitIds(List<string> ids)
        {
            ids.Clear();
            if (_model == null) return;

            foreach (var placed in _model.PlacedObjects)
                if (placed.Type == BuilderCardType.Exhibit) ids.Add(placed.Id);

            for (var i = ids.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }
        }

        public PlacedObjectData FindExhibit(string id)
        {
            if (_model == null || string.IsNullOrEmpty(id)) return null;
            foreach (var placed in _model.PlacedObjects)
                if (placed.Id == id) return placed;
            return null;
        }

        /// <summary>
        /// Walkable cells touching an exhibit's footprint, each paired with the footprint cell it
        /// faces. Ordered best-first: free front-side spots (low X / low Y — the sides facing the
        /// camera, so the exhibit reads clearly past the guest) shuffled, then free back/side
        /// spots, then spots another guest already reserved.
        /// </summary>
        public void GetViewingSpots(PlacedObjectData exhibit, List<(Vector2Int view, Vector2Int face)> spots)
        {
            spots.Clear();
            _frontSpots.Clear();
            _sideSpots.Clear();
            _takenSpots.Clear();
            if (exhibit == null || _model == null) return;

            var min = exhibit.AnchorCell;
            var max = min + new Vector2Int(Mathf.Max(1, exhibit.WidthInTiles) - 1, Mathf.Max(1, exhibit.LengthInTiles) - 1);

            for (var x = min.x; x <= max.x; x++)
            {
                ConsiderSpot(new Vector2Int(x, min.y - 1), new Vector2Int(x, min.y), true);
                ConsiderSpot(new Vector2Int(x, max.y + 1), new Vector2Int(x, max.y), false);
            }

            for (var y = min.y; y <= max.y; y++)
            {
                ConsiderSpot(new Vector2Int(min.x - 1, y), new Vector2Int(min.x, y), true);
                ConsiderSpot(new Vector2Int(max.x + 1, y), new Vector2Int(max.x, y), false);
            }

            Shuffle(_frontSpots);
            Shuffle(_sideSpots);
            spots.AddRange(_frontSpots);
            spots.AddRange(_sideSpots);
            spots.AddRange(_takenSpots);
        }

        private void ConsiderSpot(Vector2Int view, Vector2Int face, bool isFront)
        {
            if (!_model.TryGetTile(view, out var tile) || !tile.Walkable) return;
            if (_reservedViewCells.Contains(view)) _takenSpots.Add((view, face));
            else (isFront ? _frontSpots : _sideSpots).Add((view, face));
        }

        public void ReserveViewCell(Vector2Int cell) => _reservedViewCells.Add(cell);
        public void ReleaseViewCell(Vector2Int cell) => _reservedViewCells.Remove(cell);

        public ReactionClip PickReaction()
        {
            if (reactions.Count == 0 || UnityEngine.Random.value >= reactionChance) return null;
            return reactions[UnityEngine.Random.Range(0, reactions.Count)];
        }

        // ── Coordinates ────────────────────────────────────────────────────

        /// <summary>
        /// World position a guest stands at for a cell. Cell corner (front vertex), matching the
        /// player and the placement system, plus an optional in-tile offset in cell units.
        /// </summary>
        public Vector3 CellToWorld(Vector2Int cell, Vector2 offset)
        {
            var local = grid.CellToLocalInterpolated(new Vector3(cell.x + offset.x, cell.y + offset.y, 0f));
            var world = grid.transform.TransformPoint(local);
            world.z = 0f;
            return world;
        }

        // ── Setup helpers ──────────────────────────────────────────────────

        private void ResolveTemplate()
        {
            if (guestTemplate == null)
            {
                foreach (var group in FindObjectsByType<SheetSpriteGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (group.gameObject.name != "Guest" || group.GetComponent<GuestAgent>() != null) continue;
                    guestTemplate = group.gameObject;
                    break;
                }
            }

            if (guestTemplate == null)
            {
                Debug.LogError("[GuestController] No guest template — assign one, or keep a scene " +
                               "object named 'Guest' with a SheetSpriteGroup.", this);
                return;
            }

            // A scene template would otherwise stand around as a frozen extra guest.
            if (guestTemplate.scene.IsValid()) guestTemplate.SetActive(false);
        }

        private void ResolveSortingLayers()
        {
            _outsideLayerId = SortingLayer.NameToID(outsideSortingLayer);
            _insideLayerId = SortingLayer.NameToID(insideSortingLayer);

            if (!SortingLayer.IsValid(_outsideLayerId))
                Debug.LogError($"[GuestController] Sorting layer '{outsideSortingLayer}' does not exist.", this);
            if (!SortingLayer.IsValid(_insideLayerId))
                Debug.LogError($"[GuestController] Sorting layer '{insideSortingLayer}' does not exist.", this);
        }

        private void ValidateSetup()
        {
            if (grid == null) Debug.LogError("[GuestController] No Grid in the scene.", this);

            if (_model == null && !_warnedNoModel)
            {
                _warnedNoModel = true;
                Debug.LogWarning("[GuestController] No MuseumDataModel injected — guests will walk " +
                                 "the street but never enter. Keep this object in the Museum scene " +
                                 "under the SceneContext.", this);
            }

            if (appearancePools.Count == 0)
                Debug.LogWarning("[GuestController] No appearance pools — every guest will look like " +
                                 "the template. Use 'Auto-fill From Guest Sheets' on this component.", this);

            if (Manhattan(doorInsideCell, doorOutsideCell) != 1)
                Debug.LogError("[GuestController] Door inside/outside cells must be orthogonal " +
                               "neighbours, or nobody can get in.", this);
            else if (_model != null)
            {
                if (!IsMuseumCell(doorInsideCell))
                    Debug.LogError($"[GuestController] Door inside cell {doorInsideCell} is not museum floor.", this);
                if (IsMuseumCell(doorOutsideCell))
                    Debug.LogError($"[GuestController] Door outside cell {doorOutsideCell} is museum floor — it must be the sidewalk.", this);
                else if (!IsOutsideWalkable(doorOutsideCell))
                    Debug.LogWarning($"[GuestController] Door outside cell {doorOutsideCell} isn't inside any " +
                                     "Outside Walk Area — guests can't reach the door.", this);
            }

            foreach (var p in spawnPoints)
                if (IsMuseumCell(p))
                    Debug.LogWarning($"[GuestController] Spawn point {p} is on museum floor.", this);

            if (guestTemplate != null && guestTemplate.GetComponentInChildren<Animator>(true) is { } animator &&
                animator.runtimeAnimatorController != null)
            {
                foreach (var state in new[] { walkDownState, walkUpState, idleDownState, idleUpState })
                    if (!animator.HasState(0, Animator.StringToHash(state)))
                        Debug.LogError($"[GuestController] Guest Animator has no state '{state}'.", this);

                foreach (var reaction in reactions)
                    foreach (var state in new[] { reaction.frontState, reaction.backState })
                        if (!string.IsNullOrEmpty(state) && !animator.HasState(0, Animator.StringToHash(state)))
                            Debug.LogWarning($"[GuestController] Guest Animator has no reaction state '{state}'.", this);
            }
        }

        public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private static float RandomRange(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);

        private static void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ── Gizmos ─────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            var g = grid != null ? grid : FindFirstObjectByType<Grid>();
            if (g == null) return;

            Vector3 Corner(int x, int y) => g.CellToWorld(new Vector3Int(x, y, 0));

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            foreach (var r in outsideWalkAreas)
            {
                var a = Corner(r.xMin, r.yMin);
                var b = Corner(r.xMax, r.yMin);
                var c = Corner(r.xMax, r.yMax);
                var d = Corner(r.xMin, r.yMax);
                Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
            }

            Gizmos.color = new Color(1f, 0.35f, 0.35f);
            foreach (var p in spawnPoints) DrawCell(g, p);

            Gizmos.color = new Color(0.3f, 1f, 0.4f);
            DrawCell(g, doorInsideCell);
            Gizmos.color = new Color(1f, 0.9f, 0.2f);
            DrawCell(g, doorOutsideCell);
        }

        private static void DrawCell(Grid g, Vector2Int cell)
        {
            var a = g.CellToWorld(new Vector3Int(cell.x, cell.y, 0));
            var b = g.CellToWorld(new Vector3Int(cell.x + 1, cell.y, 0));
            var c = g.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, 0));
            var d = g.CellToWorld(new Vector3Int(cell.x, cell.y + 1, 0));
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
            Gizmos.DrawLine(a, c); Gizmos.DrawLine(b, d);
        }

#if UNITY_EDITOR
        private const string GuestSheetRoot = "Assets/2D/Museum/Museum Character Animations/GUEST_ANIMATION_ASSETS";

        // Folder → keyword matched against the template's layer GameObject names, with the
        // layer order of the existing Guest rig as the fallback index.
        private static readonly (string folder, string key, int fallbackIndex)[] PartFolders =
        {
            ("SHADOW", "shadow", 0), ("SKIN", "skin", 1), ("EYES", "eye", 2), ("HAIR", "hair", 3),
            ("SHOES", "shoe", 4), ("PANT", "pant", 5), ("SHIRT", "shirt", 6), ("OVER_CLOTH", "over", 7)
        };

        private void Reset()
        {
            if (appearancePools.Count == 0) EditorAutoFillAppearance();
        }

        /// <summary>Rebuilds the appearance pools from the GUEST_ANIMATION_ASSETS part folders.</summary>
        [ContextMenu("Auto-fill From Guest Sheets")]
        public void EditorAutoFillAppearance()
        {
            UnityEditor.Undo.RecordObject(this, "Auto-fill Guest Appearance");

            var template = guestTemplate;
            if (template == null)
                foreach (var group in FindObjectsByType<SheetSpriteGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (group.gameObject.name == "Guest") { template = group.gameObject; break; }

            var layers = template != null ? template.GetComponent<SheetSpriteGroup>()?.Layers : null;

            appearancePools.Clear();
            foreach (var (folder, key, fallback) in PartFolders)
            {
                var path = $"{GuestSheetRoot}/{folder}";
                if (!UnityEditor.AssetDatabase.IsValidFolder(path)) continue;

                var pool = new AppearancePool { partName = folder, layerIndex = fallback };

                if (layers != null)
                    for (var i = 0; i < layers.Count; i++)
                        if (layers[i]?.Renderer != null &&
                            layers[i].Renderer.gameObject.name.ToLowerInvariant().Contains(key))
                        {
                            pool.layerIndex = i;
                            break;
                        }

                var guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { path });
                var paths = new List<string>();
                foreach (var guid in guids) paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                paths.Sort(StringComparer.Ordinal);

                foreach (var p in paths)
                {
                    var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                    if (tex != null) pool.sheets.Add(tex);
                }

                appearancePools.Add(pool);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[GuestController] Filled {appearancePools.Count} appearance pools from {GuestSheetRoot}.", this);
        }
#endif
    }
}
