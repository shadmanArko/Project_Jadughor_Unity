using System;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.InventorySystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.InventorySystem.Scriptable;
using Systems.MineSystem.InventorySystem.Service;
using Systems.MineSystem.InventorySystem.View;
using Systems.MineSystem.Mine.Service.MineArtifactService.Test;
using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zenject;

namespace Systems.MineSystem.InventorySystem.Controller
{
    public sealed class InventoryController :
        IInitializable,
        ITickable,
        IDisposable
    {
        private const int SlotsPerRow = 12;

        private readonly InventoryModel _model;
        private readonly IInventoryService _inventory;
        private readonly InventoryCanvasView _view;
        private readonly CollectableSpriteResolver _sprites;
        private readonly InventoryItemDescriptionService _descriptions;
        private readonly ArtifactSpriteScriptable _artifactSprites;
        private readonly InventorySystemConfig _config;
        private readonly MinePlayerScriptable _player;
        private readonly MinePlayerDataConfig _playerConfig;
        private readonly CompositeDisposable _disposables = new();

        private InputAction _toggleAction;
        private InputAction _navigateAction;
        private InputAction _primaryAction;
        private InputAction _secondaryAction;
        private InputAction _trashAction;
        private InputAction _cancelAction;
        private int _selectedIndex;
        private int _highestUnlocked;
        private int _heldNavigationDelta;
        private float _nextNavigationRepeatTime;
        private int _rightHeldSlot = -1;
        private float _nextRightTransferTime;

        public InventoryController(
            InventoryModel model,
            IInventoryService inventory,
            InventoryCanvasView view,
            CollectableSpriteResolver sprites,
            InventoryItemDescriptionService descriptions,
            ArtifactSpriteScriptable artifactSprites,
            InventorySystemConfig config,
            MinePlayerScriptable player,
            MinePlayerDataConfig playerConfig)
        {
            _model = model;
            _inventory = inventory;
            _view = view;
            _sprites = sprites;
            _descriptions = descriptions;
            _artifactSprites = artifactSprites;
            _config = config;
            _player = player;
            _playerConfig = playerConfig;
        }

        public void Initialize()
        {
            _highestUnlocked = Mathf.Clamp(
                Mathf.Max(
                    _player.playerData.unlockedInventorySlots.Value,
                    _playerConfig.unlockedInventorySlots),
                0,
                InventoryModel.MaximumSlots);
            _player.playerData.unlockedInventorySlots.Value = _highestUnlocked;

            BindView();
            BindModel();
            CreateInputActions();
            ApplyUnlockedSlots(_highestUnlocked);
            _model.SetOpen(false);
            _view.SetVisible(false);
            RefreshAllSlots();
            RefreshHeldStack(null);
        }

        private void BindView()
        {
            var slots = _view.AllSlots;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                slot.ConfigureSelectionSprite(_config.selectedSlotFrame);
                slot.Clicked
                    .Subscribe(button => OnSlotClicked(slot.Index, button))
                    .AddTo(_disposables);
                slot.PointerDown
                    .Subscribe(button => OnSlotPointerDown(slot.Index, button))
                    .AddTo(_disposables);
                slot.PointerUp
                    .Subscribe(button => OnSlotPointerUp(slot.Index, button))
                    .AddTo(_disposables);
                slot.PointerEntered
                    .Subscribe(_ => SelectSlot(slot.Index))
                    .AddTo(_disposables);
                slot.PointerExited
                    .Subscribe(_ => _view.PresentHovered(null, string.Empty))
                    .AddTo(_disposables);
            }

            _view.TrashClicked
                .Subscribe(_ => _inventory.TrashHeldStack())
                .AddTo(_disposables);
        }

        private void BindModel()
        {
            _model.SlotChanged
                .Subscribe(RefreshSlot)
                .AddTo(_disposables);
            _model.HeldStackChanged
                .Subscribe(RefreshHeldStack)
                .AddTo(_disposables);
            _player.playerData.unlockedInventorySlots
                .Subscribe(ApplyUnlockedSlots)
                .AddTo(_disposables);
        }

        private void CreateInputActions()
        {
            _toggleAction = new InputAction("Toggle Inventory", binding: "<Keyboard>/i");
            _toggleAction.AddBinding("<Gamepad>/start");

            _navigateAction = new InputAction(
                "Navigate Inventory",
                InputActionType.PassThrough);
            _navigateAction.AddBinding("<Gamepad>/dpad");
            _navigateAction.AddBinding("<Gamepad>/leftStick");

            _primaryAction = new InputAction(
                "Inventory Primary",
                binding: "<Gamepad>/buttonSouth");
            _secondaryAction = new InputAction(
                "Inventory Secondary",
                binding: "<Gamepad>/buttonWest");
            _trashAction = new InputAction(
                "Trash Held Stack",
                binding: "<Gamepad>/buttonNorth");
            _cancelAction = new InputAction(
                "Close Inventory",
                binding: "<Gamepad>/buttonEast");

            _toggleAction.performed += TogglePerformed;
            _primaryAction.performed += PrimaryPerformed;
            _secondaryAction.performed += SecondaryPerformed;
            _trashAction.performed += TrashPerformed;
            _cancelAction.performed += CancelPerformed;

            _toggleAction.Enable();
            _navigateAction.Enable();
            _primaryAction.Enable();
            _secondaryAction.Enable();
            _trashAction.Enable();
            _cancelAction.Enable();
        }

        private void TogglePerformed(InputAction.CallbackContext context)
        {
            SetOpen(!_model.IsOpen.Value);
        }

        public void Tick()
        {
            if (!_model.IsOpen.Value)
                return;

            TickNavigation();
            TickRightClickTransfer();

            if (_model.HeldStack != null && Mouse.current != null)
                _view.SetHeldScreenPosition(Mouse.current.position.ReadValue());
        }

        private void TickNavigation()
        {
            var direction = _navigateAction.ReadValue<Vector2>();
            var delta = GetNavigationDelta(direction);
            if (delta == 0)
            {
                _heldNavigationDelta = 0;
                return;
            }

            var now = Time.unscaledTime;
            if (delta != _heldNavigationDelta)
            {
                _heldNavigationDelta = delta;
                SelectSlot(_selectedIndex + delta);
                _nextNavigationRepeatTime =
                    now + _config.navigationInitialRepeatDelay;
                return;
            }

            if (now < _nextNavigationRepeatTime)
                return;

            SelectSlot(_selectedIndex + delta);
            _nextNavigationRepeatTime =
                now + _config.navigationRepeatInterval;
        }

        private int GetNavigationDelta(Vector2 direction)
        {
            var deadZone = _config.navigationDeadZone;
            if (direction.sqrMagnitude < deadZone * deadZone)
                return 0;

            var horizontal = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
            return horizontal
                ? (direction.x > 0f ? 1 : -1)
                : (direction.y > 0f ? -SlotsPerRow : SlotsPerRow);
        }

        private void TickRightClickTransfer()
        {
            if (_rightHeldSlot < 0 || Mouse.current == null)
                return;

            if (!Mouse.current.rightButton.isPressed)
            {
                _rightHeldSlot = -1;
                return;
            }

            if (!CanTakeFromSlot(_rightHeldSlot))
            {
                _rightHeldSlot = -1;
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextRightTransferTime)
                return;

            _inventory.RightClick(_rightHeldSlot);
            _nextRightTransferTime =
                now + _config.rightClickTransferInterval;
        }

        private void PrimaryPerformed(InputAction.CallbackContext context)
        {
            if (_model.IsOpen.Value)
                _inventory.LeftClick(_selectedIndex);
        }

        private void SecondaryPerformed(InputAction.CallbackContext context)
        {
            if (_model.IsOpen.Value)
                _inventory.RightClick(_selectedIndex);
        }

        private void TrashPerformed(InputAction.CallbackContext context)
        {
            if (_model.IsOpen.Value)
                _inventory.TrashHeldStack();
        }

        private void CancelPerformed(InputAction.CallbackContext context)
        {
            if (_model.IsOpen.Value)
                SetOpen(false);
        }

        private void OnSlotClicked(
            int index,
            PointerEventData.InputButton button)
        {
            SelectSlot(index);
            if (button == PointerEventData.InputButton.Left)
                _inventory.LeftClick(index);
        }

        private void OnSlotPointerDown(
            int index,
            PointerEventData.InputButton button)
        {
            if (button != PointerEventData.InputButton.Right)
                return;

            SelectSlot(index);
            var canRepeat = CanTakeFromSlot(index);
            _inventory.RightClick(index);
            if (canRepeat)
            {
                _rightHeldSlot = index;
                _nextRightTransferTime =
                    Time.unscaledTime + _config.rightClickTransferInterval;
            }
            else
            {
                _rightHeldSlot = -1;
            }
        }

        private void OnSlotPointerUp(
            int index,
            PointerEventData.InputButton button)
        {
            if (button == PointerEventData.InputButton.Right &&
                _rightHeldSlot == index)
                _rightHeldSlot = -1;
        }

        private bool CanTakeFromSlot(int index)
        {
            if (index < 0 || index >= _model.Slots.Length)
                return false;

            var source = _model.Slots[index].Stack;
            if (source == null || source.IsEmpty)
                return false;

            var held = _model.HeldStack;
            if (held == null)
                return true;

            var sourceItem = source.Representative;
            var heldItem = held.Representative;
            return sourceItem != null &&
                   heldItem != null &&
                   sourceItem is not Artifact &&
                   heldItem is not Artifact &&
                   sourceItem.GetType() == heldItem.GetType() &&
                   string.Equals(
                       sourceItem.Type,
                       heldItem.Type,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       sourceItem.Category,
                       heldItem.Category,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       sourceItem.Variant,
                       heldItem.Variant,
                       StringComparison.Ordinal) &&
                   held.Count < _config.defaultStackLimit;
        }

        private void SetOpen(bool open)
        {
            if (!open && _model.HeldStack != null)
                return;

            _model.SetOpen(open);
            _view.SetVisible(open);
            if (!open)
            {
                _heldNavigationDelta = 0;
                _rightHeldSlot = -1;
            }
            if (open)
                SelectSlot(_selectedIndex);
        }

        private void ApplyUnlockedSlots(int requested)
        {
            var clamped = Mathf.Clamp(
                requested,
                _highestUnlocked,
                InventoryModel.MaximumSlots);
            if (clamped != requested)
            {
                _player.playerData.unlockedInventorySlots.Value = clamped;
                return;
            }

            _highestUnlocked = clamped;
            _view.SetUnlockedSlots(clamped);
            if (_selectedIndex >= clamped)
                SelectSlot(Mathf.Max(0, clamped - 1));
        }

        private void SelectSlot(int requestedIndex)
        {
            var unlocked = _player.playerData.unlockedInventorySlots.Value;
            if (unlocked <= 0)
                return;

            var previous = _selectedIndex;
            _selectedIndex = Mathf.Clamp(requestedIndex, 0, unlocked - 1);
            if (previous < _view.AllSlots.Count)
                _view.AllSlots[previous].SetSelected(false);
            _view.AllSlots[_selectedIndex].SetSelected(true);
            ShowSlotDetails(_selectedIndex);
        }

        private void RefreshAllSlots()
        {
            for (var i = 0; i < _view.AllSlots.Count; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int index)
        {
            if (index < 0 || index >= _view.AllSlots.Count)
                return;

            var stack = _model.Slots[index].Stack;
            var item = stack?.Representative;
            _view.AllSlots[index].Present(
                ResolveSprite(item),
                stack?.Count ?? 0);
        }

        private void RefreshHeldStack(InventoryStack stack)
        {
            _view.PresentHeld(
                ResolveSprite(stack?.Representative),
                stack?.Count ?? 0);
        }

        private void ShowSlotDetails(int index)
        {
            if (index < 0 || index >= _model.Slots.Length)
                return;

            var item = _model.Slots[index].Stack?.Representative;
            _view.PresentHovered(
                ResolveDetailSprite(item),
                _descriptions.Build(item));
        }

        private Sprite ResolveDetailSprite(Item item)
        {
            if (item is Artifact artifact)
            {
                return _artifactSprites.GetDetailSprite(
                    artifact.DefinitionId,
                    _player.region,
                    _player.site);
            }

            return ResolveSprite(item);
        }

        private Sprite ResolveSprite(Item item)
        {
            return item == null
                ? null
                : _sprites.Resolve(item, _player.region, _player.site);
        }

        public void Dispose()
        {
            _toggleAction.performed -= TogglePerformed;
            _primaryAction.performed -= PrimaryPerformed;
            _secondaryAction.performed -= SecondaryPerformed;
            _trashAction.performed -= TrashPerformed;
            _cancelAction.performed -= CancelPerformed;

            _toggleAction.Dispose();
            _navigateAction.Dispose();
            _primaryAction.Dispose();
            _secondaryAction.Dispose();
            _trashAction.Dispose();
            _cancelAction.Dispose();
            _disposables.Dispose();
        }
    }
}
