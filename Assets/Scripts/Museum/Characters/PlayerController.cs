using ProjectMuseum.Builder;
using ProjectMuseum.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace ProjectMuseum.Characters
{
    /// <summary>
    /// Tile-based isometric player movement. The player always sits on a cell; a key press
    /// starts a step to the neighbouring cell and that step runs to completion before the next
    /// one starts, so the character never ends up between tiles.
    ///
    /// Because a blocked cell is simply never stepped into, the character always comes to rest a
    /// full tile clear of an exhibit — no separate stopping-distance tuning needed.
    ///
    /// Directions are in tile coordinates: W = +Y, S = -Y, A = -X, D = +X.
    /// On the museum's isometric grid that reads as W up-left, S down-right, A down-left,
    /// D up-right — which is exactly the two clips the character sheet provides:
    ///
    ///   walk_up_left    unflipped = W (up-left)      flipped = D (up-right)
    ///   walk_down_right unflipped = S (down-right)   flipped = A (down-left)
    ///
    /// so the sprite is flipped precisely when moving along the X axis.
    /// </summary>
    [AddComponentMenu("Project Museum/Player Controller")]
    public class PlayerController : MonoBehaviour
    {
        // Optional so the controller still runs (unblocked) if the scene context is missing.
        [InjectOptional] private MuseumDataModel _model;

        [Header("References")]
        [Tooltip("The museum's isometric Grid. Found in the scene if left empty.")]
        [SerializeField] private Grid grid;

        [Tooltip("Animator holding the walk/idle clips. Found in children if left empty.")]
        [SerializeField] private Animator animator;

        [Tooltip("Sheet sprite to flip. Falls back to a SpriteRenderer if empty.")]
        [SerializeField] private SheetSpriteRenderer sheetSprite;

        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Movement")]
        [Tooltip("Seconds to cross one tile. A step cannot be interrupted once started (except " +
                 "by doubling back), so this is also the worst-case lag between releasing a key " +
                 "and standing still — keep it short.")]
        [Min(0.01f)]
        [SerializeField] private float tileStepDuration = 0.22f;

        [Tooltip("Stop at tiles taken by exhibits and placed items, and at the museum edge.")]
        [SerializeField] private bool blockOnUnwalkableTiles = true;

        [Header("Animation states")]
        [Tooltip("Walking toward the camera. Unflipped this is S (down-right); flipped it is A. " +
                 "If the Animator has no state with this name, a known alias is used instead.")]
        [SerializeField] private string walkDownState = "walk_down_right";

        [Tooltip("Walking away from the camera. Unflipped this is W (up-left); flipped it is D.")]
        [SerializeField] private string walkUpState = "walk_up_left";

        [SerializeField] private string idleDownState = "idle_down_right";
        [SerializeField] private string idleUpState = "idle_up_left";

        [Header("Facing")]
        [Tooltip("Flip the sprite when moving along the X axis (A/D). Turn off only if the " +
                 "artwork is mirrored from the usual down-right / up-left pair.")]
        [SerializeField] private bool flipOnXAxis = true;

        [Tooltip("Face a new direction the moment the key is pressed, without waiting to finish " +
                 "the current tile.")]
        [SerializeField] private bool turnImmediately = true;

        [Header("Sorting")]
        [Tooltip("Register with MuseumSortingSystem so exhibits in front of the player draw over " +
                 "it and exhibits behind it do not.")]
        [SerializeField] private bool useMuseumSorting = true;

        [Tooltip("Sorting layer the player is moved onto. MUST match the layer placed objects " +
                 "use, or sorting order between them can never be compared.")]
        [SerializeField] private string placedObjectSortingLayer = "PlacedMuseumObject";

        [Header("Diagnostics")]
        [Tooltip("Logs why a step was refused (blocked tile / no tile data).")]
        [SerializeField] private bool logBlockedSteps;

        // Names actually used at runtime, after checking them against the Animator.
        private string _walkDown, _walkUp, _idleDown, _idleUp;

        private Vector3Int _currentCell;
        private Vector3Int _targetCell;
        private Vector3 _stepFrom;
        private Vector3 _stepTo;
        private float _stepTime;
        private bool _isMoving;

        private Vector2Int _inputDirection;
        private Vector2Int _stepDirection;
        private bool _facingDown = true;
        private bool _flipped;
        private string _currentState;
        private bool _warnedNoModel;

        private MuseumSortingSystem _sorting;
        private Vector2Int _sortedCell = new Vector2Int(int.MinValue, int.MinValue);

        /// <summary>The cell the player is standing on (or stepping away from mid-step).</summary>
        public Vector3Int CurrentCell => _currentCell;

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<Grid>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (sheetSprite == null) sheetSprite = GetComponentInChildren<SheetSpriteRenderer>(true);
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        private void Start()
        {
            // Snap onto whichever cell the object was placed on, so it starts tile-aligned
            // however roughly it was dragged into the scene.
            if (grid != null)
            {
                _currentCell = grid.WorldToCell(transform.position);
                transform.position = CellToWorld(_currentCell);
            }

            ResolveAnimatorStates();
            ApplySortingLayer();
            PlayState(_idleDown);
            UpdateSortingCell(_currentCell);
        }

        private void OnDestroy()
        {
            if (_sorting != null) _sorting.UnregisterObject(gameObject);
        }

        private void Update()
        {
            _inputDirection = ReadDirection();

            // Turning mid-step costs nothing but the sprite flip, and it is the difference
            // between the character answering the key press and answering it a tile later.
            if (turnImmediately && _isMoving && _inputDirection != Vector2Int.zero)
                Face(_inputDirection);

            // Doubling back is the one mid-step change that needs no tile boundary: the cell
            // being returned to is the one just left, so the character can turn round on the
            // spot instead of finishing a step in the direction the player already abandoned.
            if (_isMoving && _inputDirection != Vector2Int.zero && _inputDirection == -_stepDirection)
                ReverseStep();

            if (_isMoving) ContinueStep();

            // Deliberately not 'else': a step that finished this frame starts the next one
            // immediately instead of idling for a frame, which is what made a change of
            // direction look like it lagged behind the key press.
            if (!_isMoving) TryStartStep();

            UpdateAnimationState();
        }

        // ── Stepping ───────────────────────────────────────────────

        private void TryStartStep()
        {
            if (_inputDirection == Vector2Int.zero || grid == null) return;

            // Turn on the spot even when the way is blocked, so walking into a wall still faces
            // the wall rather than leaving the character staring the old way.
            Face(_inputDirection);

            var target = _currentCell + new Vector3Int(_inputDirection.x, _inputDirection.y, 0);
            if (!IsWalkable(target)) return;

            _targetCell = target;
            _stepDirection = _inputDirection;
            _stepFrom = transform.position;
            _stepTo = CellToWorld(target);
            _stepTime = 0f;
            _isMoving = true;
        }

        /// <summary>
        /// Turns the current step around without moving the character: the two endpoints swap and
        /// the elapsed time mirrors, so position is continuous across the reversal and the step
        /// finishes on the cell it originally started from.
        /// </summary>
        private void ReverseStep()
        {
            (_stepFrom, _stepTo) = (_stepTo, _stepFrom);
            (_currentCell, _targetCell) = (_targetCell, _currentCell);

            // Mirror the progress: t becomes 1 - t, which leaves Lerp(from, to, t) at the exact
            // position the character already occupies.
            _stepTime = Mathf.Max(0f, tileStepDuration - _stepTime);
            _stepDirection = -_stepDirection;
        }

        private void ContinueStep()
        {
            _stepTime += Time.deltaTime;
            var t = Mathf.Clamp01(_stepTime / tileStepDuration);
            transform.position = Vector3.Lerp(_stepFrom, _stepTo, t);

            // Hand depth over at the halfway mark — the point where the character visually
            // crosses the boundary — so it slips behind or in front of a neighbouring exhibit
            // at the right moment instead of popping a whole tile early or late.
            if (t >= 0.5f) UpdateSortingCell(_targetCell);

            if (t < 1f) return;

            // Land exactly on the cell rather than wherever the lerp finished, so rounding
            // never accumulates across a long walk.
            transform.position = _stepTo;
            _currentCell = _targetCell;
            _isMoving = false;
        }

        private bool IsWalkable(Vector3Int cell)
        {
            if (!blockOnUnwalkableTiles) return true;

            if (_model == null)
            {
                if (!_warnedNoModel)
                {
                    _warnedNoModel = true;
                    Debug.LogWarning("[PlayerController] No MuseumDataModel injected — blocked " +
                                     "tiles are not enforced. The player object must be in the " +
                                     "scene with a Zenject SceneContext for this to bind.", this);
                }

                return true;
            }

            // Placing an object sets Walkable = false on every tile of its footprint, and a cell
            // with no tile record at all is outside the museum — so this covers exhibits, items
            // and the museum edge without any extra colliders.
            var cell2D = new Vector2Int(cell.x, cell.y);
            if (!_model.TryGetTile(cell2D, out var tile))
            {
                if (logBlockedSteps) Debug.Log($"[PlayerController] {cell2D} is outside the museum.", this);
                return false;
            }

            if (!tile.Walkable)
            {
                if (logBlockedSteps)
                    Debug.Log($"[PlayerController] {cell2D} is blocked by '{tile.OccupantId}'.", this);
                return false;
            }

            return true;
        }

        // ── Sorting ────────────────────────────────────────────────

        /// <summary>
        /// Moves the player onto the same sorting layer as placed museum objects.
        ///
        /// This is not cosmetic. Sorting layer is compared before sorting order, and the project's
        /// layer list puts "Player" before "PlacedMuseumObject" — so while the character sits on
        /// the Player layer, every exhibit draws over it whatever order it is given, including
        /// exhibits standing behind it. <see cref="MuseumSortingSystem"/> only ever assigns
        /// sortingOrder, so the layers have to agree first.
        /// </summary>
        private void ApplySortingLayer()
        {
            if (!useMuseumSorting || string.IsNullOrEmpty(placedObjectSortingLayer)) return;

            var layerId = SortingLayer.NameToID(placedObjectSortingLayer);
            if (!SortingLayer.IsValid(layerId))
            {
                Debug.LogError($"[PlayerController] Sorting layer '{placedObjectSortingLayer}' does " +
                               "not exist. Exhibits will keep drawing over the player.", this);
                return;
            }

            // Every renderer under the player, so a layered character moves as one unit.
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                if (r != null) r.sortingLayerID = layerId;
        }

        /// <summary>
        /// Tells <see cref="MuseumSortingSystem"/> the player now occupies this cell. The player
        /// is registered as a 1x1 footprint, the same shape the placement ghost uses, so the
        /// existing pairwise footprint comparison handles it with no special cases.
        /// </summary>
        private void UpdateSortingCell(Vector3Int cell)
        {
            if (!useMuseumSorting) return;

            var cell2D = new Vector2Int(cell.x, cell.y);
            if (cell2D == _sortedCell && _sorting != null) return;

            // Looked up lazily: MuseumObjectPlacementSystem adds the component to itself, so it
            // may not exist yet when this component's Start runs.
            if (_sorting == null)
            {
                _sorting = FindFirstObjectByType<MuseumSortingSystem>();
                if (_sorting == null) return;
            }

            // YSortable would fight the sorting system over sortingOrder, on a different scale
            // and only ever computed at Awake. The placement system strips it from spawned
            // objects for the same reason.
            foreach (var y in GetComponentsInChildren<YSortable>(true))
            {
                if (!y.enabled) continue;
                y.enabled = false;
                Debug.Log("[PlayerController] Disabled YSortable — MuseumSortingSystem now owns " +
                          "this character's depth.", this);
            }

            _sortedCell = cell2D;
            _sorting.UpdateObjectFootprint(gameObject, cell2D, 1, 1);
        }

        // ── Input ──────────────────────────────────────────────────

        /// <summary>
        /// The held direction in tile coordinates. One axis at a time: a diagonal would need
        /// four more animations, so the first key in this order wins.
        /// </summary>
        private static Vector2Int ReadDirection()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2Int.zero;

            if (keyboard.wKey.isPressed) return new Vector2Int(0, 1);
            if (keyboard.sKey.isPressed) return new Vector2Int(0, -1);
            if (keyboard.aKey.isPressed) return new Vector2Int(-1, 0);
            if (keyboard.dKey.isPressed) return new Vector2Int(1, 0);

            return Vector2Int.zero;
        }

        // ── Facing and animation ───────────────────────────────────

        private void Face(Vector2Int direction)
        {
            // S (-Y) and A (-X) head toward the camera; W (+Y) and D (+X) head away.
            _facingDown = direction.x < 0 || direction.y < 0;

            // The art is drawn down-right and up-left, so the Y-axis keys (S, W) use it as-is
            // and the X-axis keys (A, D) are the mirrored halves of each pair.
            var flip = direction.x != 0;
            SetFlip(flipOnXAxis ? flip : !flip);
        }

        /// <summary>
        /// Walk whenever the character is actually travelling, or a key is held.
        ///
        /// Both halves matter. Releasing a key mid-step does not stop the character — it still
        /// has to finish the tile it is crossing — so keying off input alone played the idle
        /// pose while the sprite was still moving, which reads as the character sliding across
        /// the floor. Keying off <see cref="_isMoving"/> alone would drop to idle when walking
        /// into a wall, where the character is pushing but not travelling.
        /// </summary>
        private void UpdateAnimationState()
        {
            var walking = _isMoving || _inputDirection != Vector2Int.zero;

            if (walking) PlayState(_facingDown ? _walkDown : _walkUp);
            else PlayState(_facingDown ? _idleDown : _idleUp);
        }

        private void SetFlip(bool flip)
        {
            if (flip == _flipped) return;
            _flipped = flip;

            if (sheetSprite != null) sheetSprite.FlipX = flip;
            else if (spriteRenderer != null) spriteRenderer.flipX = flip;
        }

        // ── Helpers ────────────────────────────────────────────────

        private Vector3 CellToWorld(Vector3Int cell)
        {
            // CellToWorld, not GetCellCenterWorld: on an isometric grid the cell corner is the
            // tile's front vertex, which is where a bottom-pivoted character stands. This is the
            // same convention the placement system uses.
            var world = grid.CellToWorld(cell);
            world.z = 0f;
            return world;
        }

        // Animator.Play restarts a state it is already in, which would pin the walk cycle on its
        // first frame for as long as a key is held.
        private void PlayState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName) || stateName == _currentState) return;
            _currentState = stateName;
            animator.Play(stateName, 0, 0f);
        }

        /// <summary>
        /// Picks a state name the Animator actually has.
        ///
        /// Animator.Play on an unknown name fails silently — it looks exactly like "the animation
        /// never changes". The inspector values are also sticky: editing a field's default in code
        /// does NOT update a component already saved in a scene, so an object set up against older
        /// state names keeps them forever. Falling back through known aliases means the character
        /// animates whichever naming the controller was built with.
        /// </summary>
        private void ResolveAnimatorStates()
        {
            _walkDown = walkDownState;
            _walkUp = walkUpState;
            _idleDown = idleDownState;
            _idleUp = idleUpState;

            if (animator == null)
            {
                Debug.LogError("[PlayerController] No Animator found — animations cannot play.", this);
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError("[PlayerController] The Animator has no Controller assigned.", this);
                return;
            }

            _walkDown = Resolve(walkDownState, "walk_down_right", "walk_forward", "walk_down");
            _walkUp = Resolve(walkUpState, "walk_up_left", "walk_backward", "walk_up");
            _idleDown = Resolve(idleDownState, "idle_down_right", "idle_front_facing", "idle_down");
            _idleUp = Resolve(idleUpState, "idle_up_left", "idle_back_facing", "idle_up");
        }

        private string Resolve(string configured, params string[] aliases)
        {
            if (Has(configured)) return configured;

            foreach (var alias in aliases)
            {
                if (!Has(alias)) continue;

                Debug.LogWarning($"[PlayerController] The Animator has no state '{configured}', " +
                                 $"using '{alias}' instead. Update the field on this component to " +
                                 "silence this.", this);
                return alias;
            }

            Debug.LogError($"[PlayerController] No state matching '{configured}' exists on layer 0 " +
                           "of the Animator Controller — this animation will not play. Check the " +
                           "state names in the controller.", this);
            return configured;
        }

        private bool Has(string stateName) =>
            !string.IsNullOrEmpty(stateName) && animator.HasState(0, Animator.StringToHash(stateName));
    }
}
