using System;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MineTransitionSystem.View
{
    public sealed class MineEntranceTriggerView : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D triggerCollider;
        private readonly Subject<Unit> _entered = new();
        private readonly Subject<Unit> _exited = new();
        private Collider2D _playerCollider;

        public IObservable<Unit> Entered => _entered;
        public IObservable<Unit> Exited => _exited;
        public bool IsPlayerInside { get; private set; }

        public void Configure(Grid grid, Collider2D playerCollider)
        {
            if (grid == null || playerCollider == null || triggerCollider == null ||
                !triggerCollider.isTrigger)
                throw new InvalidOperationException(
                    "Mine entrance requires a Grid, player collider, and authored trigger BoxCollider2D.");

            _playerCollider = playerCollider;
            var cell = grid.GetCellCenterWorld(Vector3Int.zero);
            var size = grid.cellSize;
            transform.position = new Vector3(cell.x,
                cell.y - size.y * 0.25f, cell.z);
            triggerCollider.size = new Vector2(size.x * 0.7f, size.y * 0.4f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other != _playerCollider || IsPlayerInside)
                return;
            IsPlayerInside = true;
            _entered.OnNext(Unit.Default);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != _playerCollider || !IsPlayerInside)
                return;
            IsPlayerInside = false;
            _exited.OnNext(Unit.Default);
        }

        private void OnDestroy()
        {
            _entered.OnCompleted();
            _exited.OnCompleted();
            _entered.Dispose();
            _exited.Dispose();
        }
    }
}
