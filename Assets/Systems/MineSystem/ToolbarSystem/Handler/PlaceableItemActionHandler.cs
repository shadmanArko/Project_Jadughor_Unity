using System;
using Systems.MineSystem.InventorySystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using UniRx;
using UnityEngine;

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
        private readonly CompositeDisposable _disposables = new();

        private Item _item;
        private int _slotIndex = -1;
        private PlaceableActionProfile _profile;
        private ItemActionTarget _target;
        private GameObject _preview;
        private SpriteRenderer _previewRenderer;
        private bool _inventoryOpen;

        public ItemActionKind ActionKind => ItemActionKind.Placeable;

        public PlaceableItemActionHandler(
            IItemTargetResolver targets,
            IPlaceableValidator validator,
            IPlaceableFactory factory,
            IInventoryService inventory,
            IToolbarInventorySource toolbarInventory,
            RuntimeDataScriptable runtime)
        {
            _targets = targets;
            _validator = validator;
            _factory = factory;
            _inventory = inventory;
            _toolbarInventory = toolbarInventory;
            _runtime = runtime;

            _targets.PointerTargetChanged
                .Subscribe(UpdatePreview)
                .AddTo(_disposables);
            _toolbarInventory.IsInventoryOpen
                .Subscribe(OnInventoryOpenChanged)
                .AddTo(_disposables);
        }

        public void Activate(
            Item item,
            int slotIndex,
            ItemActionProfile profile)
        {
            _item = item;
            _slotIndex = slotIndex;
            _profile = profile as PlaceableActionProfile;
            EnsurePreview();
            UpdatePreview(_targets.ResolveDirectionalTarget(1));
        }

        public void Deactivate()
        {
            _item = null;
            _profile = null;
            _slotIndex = -1;
            if (_preview != null)
                _preview.SetActive(false);
        }

        public bool TryExecute()
        {
            if (_item == null ||
                _profile == null ||
                !_validator.CanPlace(_target.CellPosition, _profile))
                return false;

            PersistHorizontalFacing(_target.Direction);
            var instanceId = Guid.NewGuid().ToString("N");
            if (!_validator.TryReserve(
                    _target.CellPosition,
                    _profile,
                    _item,
                    instanceId))
                return false;

            var context = new PlaceableSpawnContext(
                _profile.PlaceableId,
                instanceId,
                _item,
                _profile,
                _target.CellPosition,
                _target.WorldPosition);
            if (!_factory.TrySpawn(context, out var runtime))
            {
                _validator.Release(
                    _target.CellPosition,
                    _profile,
                    instanceId);
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
                _profile)
                ? _profile.ValidColor
                : _profile.InvalidColor;
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
            if (_preview == null)
                return;

            if (open)
                _preview.SetActive(false);
            else if (_profile != null)
                UpdatePreview(_targets.ResolveDirectionalTarget(1));
        }

        public void Dispose()
        {
            _disposables.Dispose();
            if (_preview != null)
                UnityEngine.Object.Destroy(_preview);
        }
    }
}
