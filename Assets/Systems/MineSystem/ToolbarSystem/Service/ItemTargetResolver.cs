using System;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class ItemTargetResolver :
        IItemTargetResolver,
        IInitializable,
        IDisposable
    {
        private readonly Camera _camera;
        private readonly PlayerView _player;
        private readonly MineView _mineView;
        private readonly Subject<ItemActionTarget> _pointerTargetChanged = new();
        private InputAction _pointAction;
        private Vector2 _screenPosition;

        public IObservable<ItemActionTarget> PointerTargetChanged =>
            _pointerTargetChanged;

        public ItemTargetResolver(
            Camera camera,
            PlayerView player,
            MineView mineView)
        {
            _camera = camera;
            _player = player;
            _mineView = mineView;
        }

        public void Initialize()
        {
            _pointAction = new InputAction(
                "ToolbarItemPointer",
                InputActionType.PassThrough,
                expectedControlType: "Vector2");
            _pointAction.AddBinding("<Pointer>/position");
            _pointAction.performed += OnPoint;
            _pointAction.Enable();

            if (Mouse.current != null)
                _screenPosition = Mouse.current.position.ReadValue();
        }

        public ItemActionTarget ResolveDirectionalTarget(int range)
        {
            var playerWorld = _player.transform.position;
            var pointerWorld = ScreenToWorld(_screenPosition);
            var delta = pointerWorld - playerWorld;

            CardinalDirection direction;
            Vector3Int offset;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                direction = delta.x < 0f
                    ? CardinalDirection.Left
                    : CardinalDirection.Right;
                offset = delta.x < 0f ? Vector3Int.left : Vector3Int.right;
            }
            else
            {
                direction = delta.y < 0f
                    ? CardinalDirection.Down
                    : CardinalDirection.Up;
                offset = delta.y < 0f ? Vector3Int.down : Vector3Int.up;
            }

            var playerCell = _mineView.wallTileMap.WorldToCell(playerWorld);
            var targetCell = playerCell + offset * Mathf.Max(1, range);
            return new ItemActionTarget(
                direction,
                targetCell,
                _mineView.grid.GetCellCenterWorld(targetCell));
        }

        public ItemActionTarget ResolvePointerCell()
        {
            var playerWorld = _player.transform.position;
            var pointerWorld = ScreenToWorld(_screenPosition);
            var delta = pointerWorld - playerWorld;
            var direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? delta.x < 0f
                    ? CardinalDirection.Left
                    : CardinalDirection.Right
                : delta.y < 0f
                    ? CardinalDirection.Down
                    : CardinalDirection.Up;
            var cell = _mineView.wallTileMap.WorldToCell(pointerWorld);
            return new ItemActionTarget(
                direction,
                cell,
                _mineView.grid.GetCellCenterWorld(cell));
        }

        private void OnPoint(InputAction.CallbackContext context)
        {
            _screenPosition = context.ReadValue<Vector2>();
            _pointerTargetChanged.OnNext(ResolvePointerCell());
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            var world = _camera.ScreenToWorldPoint(screenPosition);
            world.z = 0f;
            return world;
        }

        public void Dispose()
        {
            if (_pointAction != null)
            {
                _pointAction.performed -= OnPoint;
                _pointAction.Disable();
                _pointAction.Dispose();
            }

            _pointerTargetChanged.Dispose();
        }
    }
}
