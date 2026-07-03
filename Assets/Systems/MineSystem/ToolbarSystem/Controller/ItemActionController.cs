using System;
using System.Collections.Generic;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Signal.InputSignal;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Profile;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Controller
{
    public sealed class ItemActionController : IInitializable, IDisposable
    {
        private readonly IToolbarSelectionSource _selection;
        private readonly ItemActionProfileCatalog _profiles;
        private readonly List<IItemActionHandler> _handlers;
        private readonly CompositeDisposable _disposables = new();
        private readonly HashSet<string> _missingProfiles = new();
        private IItemActionHandler _activeHandler;

        public ItemActionController(
            IToolbarSelectionSource selection,
            ItemActionProfileCatalog profiles,
            List<IItemActionHandler> handlers)
        {
            _selection = selection;
            _profiles = profiles;
            _handlers = handlers;
        }

        public void Initialize()
        {
            _selection.HighlightedItem
                .CombineLatest(
                    _selection.HighlightedSlot,
                    (item, slot) => (item, slot))
                .Subscribe(value => ActivateFor(value.item, value.slot))
                .AddTo(_disposables);

            GlobalEventBus.OnSignal<ActionInputSignal>()
                .Subscribe(HandleActionInput)
                .AddTo(_disposables);
        }

        private void HandleActionInput(ActionInputSignal signal)
        {
            _activeHandler?.SetActionHeld(signal.IsPressed);
            if (signal.IsPressed)
                _activeHandler?.TryExecute();
        }

        private void ActivateFor(Item item, int slot)
        {
            _activeHandler?.Deactivate();
            _activeHandler = null;

            if (item == null || !_profiles.TryGet(item, out var profile))
            {
                WarnMissingProfile(item);
                return;
            }

            for (var index = 0; index < _handlers.Count; index++)
            {
                if (_handlers[index].ActionKind != profile.ActionKind)
                    continue;

                _activeHandler = _handlers[index];
                _activeHandler.Activate(item, slot, profile);
                return;
            }

            Debug.LogError(
                $"No toolbar handler is registered for '{profile.ActionKind}'.");
        }

        private void WarnMissingProfile(Item item)
        {
            if (item == null)
                return;

            var key = $"{item.Type}|{item.Category}|{item.Variant}";
            if (_missingProfiles.Add(key))
                Debug.LogWarning($"No toolbar action profile matches '{key}'.");
        }

        public void Dispose()
        {
            _activeHandler?.Deactivate();
            _disposables.Dispose();
        }
    }
}
