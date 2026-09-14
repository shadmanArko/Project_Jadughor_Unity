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
    /// Directions are in tile coordinates: W = +Y, S = -Y, A = -X, D = +X.
    /// On the museum's isometric grid that reads as W up-left, S down-right, A down-left,
    /// D up-right.
    ///
    /// Only two walk animations are needed. S and A move toward the camera (walk down), W and D
    /// move away (walk up); within each pair the left-hand direction is the same clip flipped.
    /// </summary>
    [AddComponentMenu("Project Museum/Player Controller")]
    public class PlayerController : MonoBehaviour
    {
        // Optional so the controller still runs (unblocked) if the scene context is missing.
        [InjectOptional] private MuseumDataModel _model;

        [Header("References")]
        [Tooltip("The museum's isometric Grid. Found in the scene if left empty.")]
        [SerializeField] private Grid grid;

        [Tooltip("Animator holding the walk/idle clips. Taken from this object if left empty.")]
        [SerializeField] private Animator animator;

        [Tooltip("Sheet sprite to flip. Falls back to a SpriteRenderer if empty.")]
        [SerializeField] private SheetSpriteRenderer sheetSprite;

        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Movement")]
        [Tooltip("Seconds to cross one tile. Lower is faster — this is also how long a queued " +
                 "turn waits, so very high values feel unresponsive.")]
        [Min(0.01f)]
        [SerializeField] private float secondsPerTile = 0.3f;

        [Tooltip("Stop at tiles taken by exhibits and placed items, and at the museum edge.")]
        [SerializeField] private bool blockOnUnwalkableTiles = true;

        [Header("Animation states")]
        [Tooltip("Walking toward the camera — used by S and A.")]
        [SerializeField] private string walkDownState = "walk_forward";

        [Tooltip("Walking away from the camera — used by W and D.")]
        [SerializeField] private string walkUpState = "walk_backward";

        [SerializeField] private string idleDownState = "idle_front_facing";
        [SerializeField] private string idleUpState = "idle_back_facing";

        [Header("Facing")]
        [Tooltip("On if the artwork is drawn facing screen-right. Turn off if the character " +
                 "walks the wrong way round — it swaps which directions get flipped.")]
        [SerializeField] private bool spriteFacesScreenRight = true;

        [Tooltip("Face a new direction the moment the key is pressed, without waiting to finish " +
                 "the current tile. Responsive, but the character slides the old way for the rest " +
                 "of the step while already facing the new one. Turn off to only turn on arrival.")]
        [SerializeField] private bool turnImmediately = true;

        [Header("Diagnostics")]
        [Tooltip("Logs why a step was refused (blocked tile / no tile data).")]
        [SerializeField] private bool logBlockedSteps;

        private Vector3Int _currentCell;
        private Vector3Int _targetCell;
        private Vector3 _stepFrom;
        private Vector3 _stepTo;
        private float _stepTime;
        private bool _isMoving;

        private Vector2Int _inputDirection;
        private bool _facingDown = true;
        private bool _flipped;
        private string _currentState;
        private bool _warnedNoModel;

        /// <summary>The cell the player is standing on (or stepping away from mid-step).</summary>
        public Vector3Int CurrentCell => _currentCell;

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<Grid>();
            if (animator == null) animator = GetComponent<Animator>();
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

            ValidateAnimatorStates();
            PlayState(idleDownState);
        }

        private void Update()
        {
            _inputDirection = ReadDirection();

            // Turning mid-step costs nothing but the sprite flip, and it is the difference
            // between the character answering the key press and answering it a tile later.
            if (turnImmediately && _isMoving && _inputDirection != Vector2Int.zero)
                Face(_inputDirection);

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
            _stepFrom = transform.position;
            _stepTo = CellToWorld(target);
            _stepTime = 0f;
            _isMoving = true;
        }

        private void ContinueStep()
        {
            _stepTime += Time.deltaTime;
            var t = Mathf.Clamp01(_stepTime / secondsPerTile);
            transform.position = Vector3.Lerp(_stepFrom, _stepTo, t);

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

        // ── Input ──────────────────────────────────────────────────

        /// <summary>
        /// The held direction in tile coordinates. One axis at a time: a diagonal would need
        /// four more animations, so the vertical keys win when both are held.
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

            // A and W are the two screen-left directions, so they take the flipped art.
            var facingScreenLeft = direction.x < 0 || direction.y > 0;
            SetFlip(spriteFacesScreenRight ? facingScreenLeft : !facingScreenLeft);
        }

        /// <summary>
        /// Walk while a key is held, idle otherwise. Driven from the held input rather than from
        /// <see cref="_isMoving"/>, so holding a direction into a wall keeps the walk cycle
        /// running and releasing the key returns to idle on the same frame.
        /// </summary>
        private void UpdateAnimationState()
        {
            var walking = _inputDirection != Vector2Int.zero;

            if (walking) PlayState(_facingDown ? walkDownState : walkUpState);
            else PlayState(_facingDown ? idleDownState : idleUpState);
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
        /// Animator.Play on a name the controller does not have fails silently, which looks
        /// exactly like "the animation never changes". Check once at startup and say so.
        /// </summary>
        private void ValidateAnimatorStates()
        {
            if (animator == null)
            {
                Debug.LogError("[PlayerController] No Animator found — animations cannot play.", this);
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError("[PlayerController] The Animator has no Controller assigned. Generate " +
                               "the clips (Tools ▸ Project Museum ▸ Frame Animation Clip Builder) and " +
                               "assign the resulting Animator Controller.", this);
                return;
            }

            foreach (var state in new[] { walkDownState, walkUpState, idleDownState, idleUpState })
            {
                if (string.IsNullOrEmpty(state) || animator.HasState(0, Animator.StringToHash(state)))
                    continue;

                Debug.LogError($"[PlayerController] The Animator Controller has no state named " +
                               $"'{state}' on layer 0. Rename the state to match, or change the field " +
                               "on this component. (A state inside a sub-state machine needs its full " +
                               "path, e.g. 'SubMachine.walk_forward'.)", this);
            }
        }
    }
}
