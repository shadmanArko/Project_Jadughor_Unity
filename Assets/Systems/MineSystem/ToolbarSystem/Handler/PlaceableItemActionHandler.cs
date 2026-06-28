using System;
using Systems.MineSystem.InventorySystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script;
using Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Handler
{
    [Serializable]
    public sealed class PlaceableItemActionHandler :
        IItemActionHandler,
        IDisposable
    {
        private readonly IItemTargetResolver _targets;
        private readonly IPlaceableValidator _validator;
        private readonly IPlaceableFactory _factory;
        private readonly IInventoryService _inventory;
        private readonly IToolbarInventorySource _toolbarInventory;
        private readonly RuntimeDataScriptable _runtime;
        private readonly ElevatorPlacementValidator _elevatorValidator;
        private readonly IPileDriverPlacementValidator
            _pileDriverValidator;
        private readonly CompositeDisposable _disposables = new();

        private Item _item;
        private int _slotIndex = -1;
        private PlaceableActionProfile _profile;
        private ItemActionTarget _target;
        private GameObject _preview;
        private SpriteRenderer _previewRenderer;
        private bool _inventoryOpen;
        private PileDriverDirection _pileDriverDirection =
            PileDriverDirection.Down;
        private InputAction _rotateAction;

        public ItemActionKind ActionKind => ItemActionKind.Placeable;

        public PlaceableItemActionHandler(
            IItemTargetResolver targets,
            IPlaceableValidator validator,
            IPlaceableFactory factory,
            IInventoryService inventory,
            IToolbarInventorySource toolbarInventory,
            RuntimeDataScriptable runtime,
            ElevatorPlacementValidator elevatorValidator,
            IPileDriverPlacementValidator pileDriverValidator)
        {
            _targets = targets;
            _validator = validator;
            _factory = factory;
            _inventory = inventory;
            _toolbarInventory = toolbarInventory;
            _runtime = runtime;
            _elevatorValidator = elevatorValidator;
            _pileDriverValidator = pileDriverValidator;

            _targets.PointerTargetChanged
                .Subscribe(UpdatePreview)
                .AddTo(_disposables);
            _toolbarInventory.IsInventoryOpen
                .Subscribe(OnInventoryOpenChanged)
                .AddTo(_disposables);

            _rotateAction = new InputAction(
                "RotatePileDriver",
                InputActionType.Button);
            _rotateAction.AddBinding("<Keyboard>/r");
            _rotateAction.AddBinding("<Gamepad>/buttonWest");
            _rotateAction.performed += OnRotate;
        }

        public void Activate(
            Item item,
            int slotIndex,
            ItemActionProfile profile)
        {
            _item = item;
            _slotIndex = slotIndex;
            _profile = profile as PlaceableActionProfile;
            _pileDriverDirection = PileDriverDirection.Down;
            RefreshRotateInput();
            EnsurePreview();
            UpdatePreview(_targets.ResolveDirectionalTarget(1));
        }

        public void Deactivate()
        {
            _item = null;
            _profile = null;
            _slotIndex = -1;
            RefreshRotateInput();
            if (_preview != null)
                _preview.SetActive(false);
        }

        public void SetActionHeld(bool isHeld)
        {
        }

        public bool TryExecute()
        {
            if (_item == null ||
                _profile == null ||
                !CanPlace())
                return false;

            PersistHorizontalFacing(_target.Direction);
            var instanceId = Guid.NewGuid().ToString("N");
            if (!TryReserve(instanceId))
                return false;

            var context = new PlaceableSpawnContext(
                _profile.PlaceableId,
                instanceId,
                _item,
                _profile,
                _target.CellPosition,
                _target.WorldPosition,
                _pileDriverDirection);
            if (!_factory.TrySpawn(context, out var runtime))
            {
                ReleaseReservation(instanceId);
                return false;
            }

            if (!_inventory.TryRemoveOne(_slotIndex, _item))
            {
                _factory.Despawn(runtime);
                return false;
            }

            UpdatePreview(_targets.ResolveDirectionalTarget(1));
            return true;
        }

        private void PersistHorizontalFacing(CardinalDirection direction)
        {
            if (direction == CardinalDirection.Left)
                _runtime.facingDirection.Value = PlayerFacingDirection.Left;
            else if (direction == CardinalDirection.Right)
                _runtime.facingDirection.Value = PlayerFacingDirection.Right;
        }

        private void UpdatePreview(ItemActionTarget target)
        {
            if (_profile == null)
                return;

            _target = _targets.ResolveDirectionalTarget(1);
            EnsurePreview();
            _preview.transform.position = _target.WorldPosition;
            _previewRenderer.sprite = _profile.PreviewSprite;
            _previewRenderer.color = _validator.CanPlace(
                _target.CellPosition,
                _profile) && CanPlace()
                ? _profile.ValidColor
                : _profile.InvalidColor;
            _preview.transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    GetRotationDegrees(_pileDriverDirection));
            _preview.SetActive(
                !_inventoryOpen &&
                _profile.PreviewSprite != null);
        }

        private void EnsurePreview()
        {
            if (_preview != null)
                return;

            _preview = new GameObject("Placeable Preview");
            _previewRenderer = _preview.AddComponent<SpriteRenderer>();
            _previewRenderer.sortingOrder = 1000;
            _previewRenderer.sortingLayerName = "PlaceablePreview";
            _preview.SetActive(false);
        }

        private void OnInventoryOpenChanged(bool open)
        {
            _inventoryOpen = open;
            RefreshRotateInput();
            if (_preview == null)
                return;

            if (open)
                _preview.SetActive(false);
            else if (_profile != null)
                UpdatePreview(_targets.ResolveDirectionalTarget(1));
        }

        public void Dispose()
        {
            if (_rotateAction != null)
            {
                _rotateAction.performed -= OnRotate;
                _rotateAction.Disable();
                _rotateAction.Dispose();
            }
            _disposables.Dispose();
            if (_preview != null)
                UnityEngine.Object.Destroy(_preview);
        }

        private bool CanPlace()
        {
            if (_profile is ElevatorActionProfile elevatorProfile)
            {
                return _elevatorValidator.CanPlace(
                    _target.CellPosition,
                    elevatorProfile);
            }

            if (_profile is PileDriverActionProfile)
            {
                return _pileDriverValidator.CanPlace(
                    _target.CellPosition,
                    _pileDriverDirection);
            }

            return _validator.CanPlace(
                _target.CellPosition,
                _profile);
        }

        private bool TryReserve(string instanceId)
        {
            if (_profile is ElevatorActionProfile elevatorProfile)
            {
                return _elevatorValidator.TryReserve(
                    _target.CellPosition,
                    elevatorProfile,
                    _item,
                    instanceId);
            }

            return _validator.TryReserve(
                _target.CellPosition,
                _profile,
                _item,
                instanceId);
        }

        private void ReleaseReservation(string instanceId)
        {
            if (_profile is ElevatorActionProfile elevatorProfile)
            {
                _elevatorValidator.Release(
                    _target.CellPosition,
                    elevatorProfile,
                    instanceId);
                return;
            }

            _validator.Release(
                _target.CellPosition,
                _profile,
                instanceId);
        }

        private void OnRotate(InputAction.CallbackContext context)
        {
            if (_profile is not PileDriverActionProfile ||
                _inventoryOpen)
                return;

            _pileDriverDirection = _pileDriverDirection switch
            {
                PileDriverDirection.Left => PileDriverDirection.Down,
                PileDriverDirection.Down => PileDriverDirection.Right,
                PileDriverDirection.Right => PileDriverDirection.Up,
                _ => PileDriverDirection.Left
            };
            UpdatePreview(_targets.ResolveDirectionalTarget(1));
        }

        private void RefreshRotateInput()
        {
            if (_rotateAction == null)
                return;

            if (_profile is PileDriverActionProfile && !_inventoryOpen)
                _rotateAction.Enable();
            else
                _rotateAction.Disable();
        }

        private static float GetRotationDegrees(
            PileDriverDirection direction)
        {
            return direction switch
            {
                PileDriverDirection.Left => -90f,
                PileDriverDirection.Right => 90f,
                PileDriverDirection.Up => 180f,
                _ => 0f
            };
        }
    }
}
