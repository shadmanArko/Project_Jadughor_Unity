using System;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Scriptable;
using Systems.MineSystem.ToolbarSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Controller
{
    public sealed class ToolbarController : IInitializable, IDisposable
    {
        private readonly ToolbarModel _model;
        private readonly IToolbarInventorySource _inventory;
        private readonly IToolbarInputService _input;
        private readonly IToolbarView _view;
        private readonly ToolbarConfig _config;
        private readonly CollectableSpriteResolver _spriteResolver;
        private readonly MinePlayerScriptable _player;
        private readonly ReactiveProperty<Item> _highlightedItem = new();
        private readonly ReactiveProperty<int> _highlightedSlot = new(0);
        private readonly CompositeDisposable _disposables = new();

        public IReadOnlyReactiveProperty<Item> HighlightedItem => _highlightedItem;
        public IReadOnlyReactiveProperty<int> HighlightedSlot => _highlightedSlot;

        public ToolbarController(
            ToolbarModel model,
            IToolbarInventorySource inventory,
            IToolbarInputService input,
            IToolbarView view,
            ToolbarConfig config,
            CollectableSpriteResolver spriteResolver,
            MinePlayerScriptable player)
        {
            _model = model;
            _inventory = inventory;
            _input = input;
            _view = view;
            _config = config;
            _spriteResolver = spriteResolver;
            _player = player;
        }

        public void Initialize()
        {
            _view.BuildSlots(
                _config.ToolbarSlotPrefab,
                _model.SlotCount,
                _config.SelectedSlotSprite);

            for (var index = 0; index < _model.SlotCount; index++)
                RefreshSlot(index);

            ApplySelection(0, false);

            _inventory.SlotChanged
                .Where(index => index >= 0 && index < _model.SlotCount)
                .Subscribe(OnInventorySlotChanged)
                .AddTo(_disposables);

            _inventory.IsInventoryOpen
                .Subscribe(OnInventoryVisibilityChanged)
                .AddTo(_disposables);

            _input.NavigationRequested
                .Subscribe(TryNavigate)
                .AddTo(_disposables);
        }

        private void TryNavigate(int direction)
        {
            if (!_model.TryFindOccupiedSlot(
                    direction,
                    _inventory.IsOccupied,
                    out var destination))
                return;

            ApplySelection(destination, true);
        }

        private void ApplySelection(int slotIndex, bool publishSignal)
        {
            var previous = _model.SelectedSlot;
            _model.Select(slotIndex);

            if (previous != slotIndex)
                _view.SetHighlighted(previous, false);

            _view.SetHighlighted(slotIndex, true);

            var item = _inventory.GetItem(slotIndex);
            _highlightedSlot.Value = slotIndex;
            _highlightedItem.Value = item;

            if (!publishSignal)
                return;

            GlobalEventBus.Fire(new ToolbarSlotChangedSignal
            {
                SlotNumber = slotIndex,
                Item = item
            });
        }

        private void OnInventorySlotChanged(int slotIndex)
        {
            RefreshSlot(slotIndex);

            if (slotIndex == _model.SelectedSlot)
                _highlightedItem.Value = _inventory.GetItem(slotIndex);
        }

        private void RefreshSlot(int slotIndex)
        {
            var stack = _inventory.GetStack(slotIndex);
            var item = stack?.Representative;
            var sprite = item == null
                ? null
                : _spriteResolver.Resolve(item, _player.region, _player.site);

            _view.PresentSlot(slotIndex, sprite, stack?.Count ?? 0);
        }

        private void OnInventoryVisibilityChanged(bool inventoryOpen)
        {
            _view.SetVisible(!inventoryOpen);
            _input.SetEnabled(!inventoryOpen);
        }

        public void Dispose()
        {
            _input.SetEnabled(false);
            _disposables.Dispose();
            _highlightedItem.Dispose();
            _highlightedSlot.Dispose();
        }
    }
}
