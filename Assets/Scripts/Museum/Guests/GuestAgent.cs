using System.Collections.Generic;
using ProjectMuseum.Builder;
using ProjectMuseum.Characters;
using UnityEngine;

namespace ProjectMuseum.Guests
{
    /// <summary>
    /// One guest: decisions, tile-to-tile walking, animation and depth sorting. Added to each
    /// clone by <see cref="GuestController"/>, which owns everything shared.
    ///
    /// Life of a guest:
    /// <code>
    /// spawn at a road end ─► WalkingOutside ──(near door, enterChance)──► HeadingToMuseum
    ///      │  (optional PausingOutside stops)                                  │ crosses door
    ///      ▼                                                                   ▼
    /// reaches exit point ◄── HeadingToExit ◄── LeavingMuseum ◄── WalkingToExhibit ⇄ ViewingExhibit
    ///   (despawn)                                   ▲                (seen enough / none left)
    ///                                               └── LookingAround (museum has no exhibits)
    /// </code>
    ///
    /// Movement is the same cell-corner lerp as <see cref="PlayerController"/>, so guests and the
    /// player line up with placed objects identically. Unlike Godot's version, every wait is a
    /// frame timer rather than an async Task.Delay — it respects Time.timeScale and can't fire on
    /// a removed guest.
    /// </summary>
    [DisallowMultipleComponent]
    public class GuestAgent : MonoBehaviour
    {
        private GuestController _c;
        private SheetSpriteGroup _group;
        private Animator _animator;
        private SpriteRenderer[] _layerRenderers;

        private readonly List<Vector2Int> _path = new List<Vector2Int>(64);
        private readonly List<string> _exhibitQueue = new List<string>();
        private readonly List<(Vector2Int view, Vector2Int face)> _spots = new List<(Vector2Int, Vector2Int)>();

        private int _pathIndex;
        private Vector2Int _goal;
        private bool _goalIsExit;
        private RectInt _exitZone;

        // Current step.
        private bool _stepping;
        private Vector2Int _stepTarget;
        private Vector3 _stepFrom, _stepTo;
        private float _stepTime;

        // Waiting (pauses, exhibit viewing).
        private float _waitTimer;
        private float _reactionTimer;
        private string _reactionState;

        // Viewing.
        private string _exhibitId;
        private Vector2Int _faceCell;
        private bool _hasReservation;
        private Vector2Int _reservedCell;
        private int _wanderStopsLeft;
        private int _lookAroundLeft;

        // Presentation.
        private bool _facingDown = true;
        private bool _flipped;
        private string _currentState;
        private bool _sortedInside;
        private bool _sortingModeApplied;
        private int _lastOutsideOrder = int.MinValue;

        public GuestData Data { get; private set; }
        public GuestState State => Data?.State ?? GuestState.WalkingOutside;
        public Vector2Int Cell { get; private set; }
        public bool IsInside { get; private set; }

        // ── Setup (called by GuestController) ──────────────────────────────

        public void Bind(GuestController controller)
        {
            _c = controller;
            _group = GetComponent<SheetSpriteGroup>();
            _animator = GetComponentInChildren<Animator>(true);

            // YSortable would fight both sorting modes over sortingOrder.
            foreach (var y in GetComponentsInChildren<YSortable>(true)) y.enabled = false;

            var layers = _group != null ? _group.Layers : null;
            var count = layers?.Count ?? 0;
            _layerRenderers = new SpriteRenderer[count];
            for (var i = 0; i < count; i++)
            {
                var r = layers[i]?.Renderer != null ? layers[i].Renderer.Renderer : null;
                _layerRenderers[i] = r;
                if (r == null) continue;

                // Inside, MuseumSortingSystem gives the whole guest one order; these offsets keep
                // the layers stacked (shadow 0 … over-cloth 7) within the guest's band.
                var offset = r.GetComponent<MuseumSortOffset>();
                if (offset == null) offset = r.gameObject.AddComponent<MuseumSortOffset>();
                offset.offset = i;
            }
        }

        public void Begin(GuestData data, Vector2Int startCell)
        {
            Data = data;
            Cell = startCell;
            IsInside = false;
            _path.Clear();
            _pathIndex = 0;
            _stepping = false;
            _waitTimer = 0f;
            _reactionTimer = 0f;
            _hasReservation = false;
            _exhibitId = null;
            _currentState = null;
            _sortingModeApplied = false;
            _lastOutsideOrder = int.MinValue;

            _c.ApplyAppearance(_group, data);
            transform.position = _c.CellToWorld(startCell, JitterFor(startCell));
            gameObject.SetActive(true);
            ApplySortingMode(inside: false);

            var stops = _c.WanderStopsRange;
            _wanderStopsLeft = Random.Range(stops.x, stops.y + 1);
            PlanOutsideWalk();
        }

        public void End()
        {
            ReleaseReservation();
            if (_sortedInside && _c.Sorting != null) _c.Sorting.UnregisterObjectDeferred(gameObject);
            _sortedInside = false;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_c != null && _sortedInside && _c.Sorting != null) _c.Sorting.UnregisterObjectDeferred(gameObject);
            if (_c != null) ReleaseReservation();
        }

        // ── Main loop ──────────────────────────────────────────────────────

        private void Update()
        {
            if (Data == null) return;
            var dt = Time.deltaTime;

            if (_reactionTimer > 0f) _reactionTimer -= dt;

            if (_stepping)
            {
                ContinueStep(dt);

                // Chain straight into the next tile in the same frame. Otherwise the frame between
                // steps reads as "not stepping", plays idle for one frame, and the walk clip
                // restarts from frame 0 on every tile — only its first few frames ever show.
                if (!_stepping && _waitTimer <= 0f && _pathIndex < _path.Count && gameObject.activeSelf)
                    StartNextStep();
            }
            else if (_waitTimer > 0f)
            {
                _waitTimer -= dt;
                if (_waitTimer <= 0f) OnWaitFinished();
            }
            else if (_pathIndex < _path.Count)
            {
                StartNextStep();
            }
            else
            {
                OnArrived();
            }

            // Despawned during this update — the object is inactive and back in the pool.
            if (!gameObject.activeSelf) return;
            UpdateAnimation();
        }

        private void LateUpdate()
        {
            if (Data != null && !_sortedInside) ApplyOutsideOrder();
        }

        // ── Walking ────────────────────────────────────────────────────────

        private bool SetDestination(Vector2Int goal, bool isExit = false)
        {
            _goal = goal;
            _goalIsExit = isExit;
            _pathIndex = 0;
            var ok = _c.FindPath(Cell, goal, _path, Data.Id);
            if (!ok && _c.LogDecisions)
                Debug.Log($"[Guest {Data.Id}] No path {Cell} → {goal} ({Data.State}).", this);
            return ok;
        }

        private void StartNextStep()
        {
            var next = _path[_pathIndex];

            // The museum can change under a walking guest (an exhibit placed on the route).
            if (!_c.CanStep(Cell, next))
            {
                if (!SetDestination(_goal, _goalIsExit) || _path.Count == 0)
                {
                    OnPathFailed();
                    return;
                }

                next = _path[0];
            }

            _stepTarget = next;
            _stepFrom = transform.position;
            _stepTo = _c.CellToWorld(next, JitterFor(next));
            _stepTime = 0f;
            _stepping = true;
            Face(next - Cell);
        }

        private void ContinueStep(float dt)
        {
            _stepTime += dt;
            var t = Mathf.Clamp01(_stepTime / Mathf.Max(0.01f, Data.StepDuration));
            transform.position = Vector3.Lerp(_stepFrom, _stepTo, t);

            // Hand depth over halfway, where the guest visually crosses into the next tile —
            // same rule as the player. This is also where the door swaps sorting mode.
            if (t >= 0.5f) UpdateSortingFor(_stepTarget);

            if (t < 1f) return;

            transform.position = _stepTo;
            _stepping = false;
            _pathIndex++;

            var wasInside = IsInside;
            Cell = _stepTarget;
            IsInside = _c.IsMuseumCell(Cell);

            // Vanish on the first cell of the exit zone rather than walking on to the exact
            // target cell — the target is only there to spread guests across the road end.
            if (_goalIsExit && !IsInside && _exitZone.Contains(Cell))
            {
                _c.Despawn(this);
                return;
            }

            if (IsInside != wasInside) OnCrossedDoor(IsInside);
            else if (!IsInside) CheckEntryDecision();
        }

        private Vector2 JitterFor(Vector2Int cell)
        {
            // Inside, sorting is per cell, so an offset could poke a guest into a neighbouring
            // exhibit's tile visually. The door cell stays centred so the walk-in lines up.
            if (_c.IsMuseumCell(cell) || cell == _c.DoorOutsideCell) return Vector2.zero;
            return Data.LaneJitter;
        }

        // ── Decisions ──────────────────────────────────────────────────────

        /// <summary>Outside: next wander stop, or straight to the exit point.</summary>
        private void PlanOutsideWalk()
        {
            if (_wanderStopsLeft > 0 && _c.TryGetRandomOutsideCell(out var stop))
            {
                _wanderStopsLeft--;
                Data.State = GuestState.WalkingOutside;
                if (SetDestination(stop)) return;
            }

            HeadToExit(Data.VisitedMuseum ? GuestState.HeadingToExit : GuestState.WalkingOutside);
        }

        private void HeadToExit(GuestState state)
        {
            Data.State = state;
            _wanderStopsLeft = 0;
            // Non-visitors measure from where they spawned and never leave by the zone they came
            // in through; visitors leave by any zone far enough from the museum door.
            var from = Data.VisitedMuseum ? Cell : Data.SpawnCell;
            var exclude = Data.VisitedMuseum ? -1 : Data.SpawnZone;
            if (!_c.ChooseExitZone(from, exclude, out _exitZone, out var exit) ||
                !SetDestination(exit, isExit: true))
                OnPathFailed();
        }

        /// <summary>"When they come close to the museum they have a chance to go inside."</summary>
        private void CheckEntryDecision()
        {
            if (Data.EntryRolled || Data.State != GuestState.WalkingOutside) return;
            if (GuestController.Manhattan(Cell, _c.DoorOutsideCell) > _c.ApproachRadius) return;

            Data.EntryRolled = true;
            if (!_c.RollEntry()) return;

            Data.State = GuestState.HeadingToMuseum;
            if (_c.LogDecisions) Debug.Log($"[Guest {Data.Id}] Decided to visit the museum.", this);
            if (!SetDestination(_c.DoorInsideCell)) PlanOutsideWalk();
        }

        private void OnCrossedDoor(bool nowInside)
        {
            _c.SetInside(this, nowInside);

            if (nowInside)
            {
                Data.VisitedMuseum = true;
                Data.ExhibitsSeen = 0;
                _c.GetShuffledExhibitIds(_exhibitQueue);
                Data.ExhibitsToVisit = Mathf.Min(Data.ExhibitsToVisit, _exhibitQueue.Count);
                var range = _c.LookAroundRange;
                _lookAroundLeft = _exhibitQueue.Count == 0 ? Random.Range(range.x, range.y + 1) : 0;
            }
        }

        private void DecideNextInMuseum()
        {
            ReleaseReservation();

            if (Data.ExhibitsSeen < Data.ExhibitsToVisit)
            {
                while (_exhibitQueue.Count > 0)
                {
                    var id = _exhibitQueue[_exhibitQueue.Count - 1];
                    _exhibitQueue.RemoveAt(_exhibitQueue.Count - 1);
                    if (TryWalkToExhibit(id)) return;
                }
            }

            if (_lookAroundLeft > 0 && _c.TryGetRandomMuseumFloorCell(out var cell))
            {
                _lookAroundLeft--;
                Data.State = GuestState.LookingAround;
                if (SetDestination(cell)) return;
            }

            LeaveMuseum();
        }

        private bool TryWalkToExhibit(string id)
        {
            var exhibit = _c.FindExhibit(id);
            if (exhibit == null) return false; // removed since the guest walked in

            _c.GetViewingSpots(exhibit, _spots);

            // Pathing is the expensive bit — a handful of candidates is plenty.
            var tries = Mathf.Min(_spots.Count, 6);
            for (var i = 0; i < tries; i++)
            {
                var (view, face) = _spots[i];
                if (!SetDestination(view)) continue;

                _exhibitId = id;
                _faceCell = face;
                _reservedCell = view;
                _hasReservation = true;
                _c.ReserveViewCell(view);
                Data.State = GuestState.WalkingToExhibit;
                return true;
            }

            return false;
        }

        private void LeaveMuseum()
        {
            ReleaseReservation();
            Data.State = GuestState.LeavingMuseum;
            if (_c.LogDecisions) Debug.Log($"[Guest {Data.Id}] Seen enough ({Data.ExhibitsSeen}), leaving.", this);
            if (!SetDestination(_c.DoorOutsideCell)) OnPathFailed();
        }

        private void OnArrived()
        {
            switch (Data.State)
            {
                case GuestState.WalkingOutside:
                    if (_goalIsExit)
                    {
                        _c.Despawn(this);
                        break;
                    }

                    Data.State = GuestState.PausingOutside;
                    _waitTimer = Random.Range(_c.WanderPauseRange.x, _c.WanderPauseRange.y);
                    break;

                case GuestState.HeadingToExit:
                    _c.Despawn(this);
                    break;

                case GuestState.HeadingToMuseum:
                    // Standing on the inside door cell; OnCrossedDoor already built the queue.
                    DecideNextInMuseum();
                    break;

                case GuestState.WalkingToExhibit:
                    if (_c.FindExhibit(_exhibitId) == null)
                    {
                        DecideNextInMuseum();
                        break;
                    }

                    Data.State = GuestState.ViewingExhibit;
                    Face(_faceCell - Cell);
                    _waitTimer = Random.Range(_c.ViewTimeRange.x, _c.ViewTimeRange.y);

                    var reaction = _c.PickReaction();
                    if (reaction != null)
                    {
                        _reactionState = _facingDown ? reaction.frontState : reaction.backState;
                        _reactionTimer = Mathf.Min(reaction.duration, _waitTimer);
                    }
                    break;

                case GuestState.LookingAround:
                    Data.State = GuestState.ViewingExhibit; // reuse the timed idle
                    _exhibitId = null;
                    _waitTimer = Random.Range(_c.WanderPauseRange.x, _c.WanderPauseRange.y);
                    break;

                case GuestState.LeavingMuseum:
                    HeadToExit(GuestState.HeadingToExit);
                    break;

                default:
                    // PausingOutside / ViewingExhibit only get here with a zero-length wait.
                    OnWaitFinished();
                    break;
            }
        }

        private void OnWaitFinished()
        {
            _waitTimer = 0f;

            switch (Data.State)
            {
                case GuestState.PausingOutside:
                    PlanOutsideWalk();
                    break;

                case GuestState.ViewingExhibit:
                    if (_exhibitId != null) Data.ExhibitsSeen++;
                    _exhibitId = null;
                    DecideNextInMuseum();
                    break;
            }
        }

        private void OnPathFailed()
        {
            if (_c.LogDecisions) Debug.Log($"[Guest {Data.Id}] Path failed in {Data.State}.", this);

            switch (Data.State)
            {
                case GuestState.HeadingToMuseum:
                    PlanOutsideWalk();
                    return;

                case GuestState.WalkingToExhibit:
                case GuestState.LookingAround:
                    DecideNextInMuseum();
                    return;

                case GuestState.WalkingOutside:
                case GuestState.PausingOutside:
                    Data.State = GuestState.HeadingToExit;
                    if (_c.ChooseExitZone(Cell, -1, out _exitZone, out var exit) &&
                        SetDestination(exit, isExit: true)) return;
                    break;
            }

            // Walled in (door blocked by an exhibit, or no route to any exit). Removing the
            // guest beats Godot's behaviour of freezing them in place forever.
            Debug.LogWarning($"[Guest {Data.Id}] Stuck at {Cell} ({Data.State}) — removing.", this);
            _c.Despawn(this);
        }

        private void ReleaseReservation()
        {
            if (!_hasReservation) return;
            _hasReservation = false;
            _c.ReleaseViewCell(_reservedCell);
        }

        // ── Sorting ────────────────────────────────────────────────────────

        private void UpdateSortingFor(Vector2Int cell)
        {
            var inside = _c.IsMuseumCell(cell);
            if (inside != _sortedInside || !_sortingModeApplied) ApplySortingMode(inside);
            if (inside && _c.Sorting != null) _c.Sorting.UpdateObjectFootprintDeferred(gameObject, cell, 1, 1);
        }

        /// <summary>
        /// Inside: the placed-object layer + MuseumSortingSystem, exactly like the player, so
        /// exhibits in front hide the guest and exhibits behind don't. Outside: the Guest layer,
        /// Y-sorted among guests only (see <see cref="ApplyOutsideOrder"/>).
        /// </summary>
        private void ApplySortingMode(bool inside)
        {
            _sortingModeApplied = true;
            _sortedInside = inside;
            var layerId = inside ? _c.InsideLayerId : _c.OutsideLayerId;

            foreach (var r in _layerRenderers)
                if (r != null) r.sortingLayerID = layerId;

            var sorting = _c.Sorting;
            if (inside)
            {
                if (sorting != null) sorting.UpdateObjectFootprintDeferred(gameObject, Cell, 1, 1);
            }
            else
            {
                if (sorting != null) sorting.UnregisterObjectDeferred(gameObject);
                _lastOutsideOrder = int.MinValue;
            }
        }

        private void ApplyOutsideOrder()
        {
            var baseOrder = -Mathf.RoundToInt(transform.position.y * _c.OutsideOrderPerUnit) * 8;
            if (baseOrder == _lastOutsideOrder) return;
            _lastOutsideOrder = baseOrder;

            for (var i = 0; i < _layerRenderers.Length; i++)
                if (_layerRenderers[i] != null) _layerRenderers[i].sortingOrder = baseOrder + i;
        }

        // ── Facing / animation ─────────────────────────────────────────────

        private void Face(Vector2Int direction)
        {
            if (direction == Vector2Int.zero) return;

            // -X / -Y head toward the camera; the sheet's art is down-right / up-left, so X-axis
            // moves are the mirrored halves (same as PlayerController).
            _facingDown = direction.x < 0 || direction.y < 0;
            var flip = direction.x != 0;
            SetFlip(_c.FlipOnXAxis ? flip : !flip);
        }

        private void SetFlip(bool flip)
        {
            if (flip == _flipped || _group == null) return;
            _flipped = flip;
            _group.FlipX = flip;
        }

        private void UpdateAnimation()
        {
            if (_stepping) PlayState(_facingDown ? _c.WalkDownState : _c.WalkUpState);
            else if (_reactionTimer > 0f && !string.IsNullOrEmpty(_reactionState)) PlayState(_reactionState);
            else PlayState(_facingDown ? _c.IdleDownState : _c.IdleUpState);
        }

        // Animator.Play restarts a state it's already in, which would freeze the walk on frame 0.
        private void PlayState(string state)
        {
            if (_animator == null || state == _currentState) return;
            _currentState = state;
            _animator.Play(state, 0, 0f);
        }
    }
}
