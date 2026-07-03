using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.MinePlayerSystem.Interface;
using Systems.MineSystem.MinePlayerSystem.Signal.InputSignal;
using Systems.Utilities.EventBus;
using UniRx;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerInteractionService : IInitializable, IDisposable
    {
        private readonly IReadOnlyList<IPlayerInteractionHandler> _handlers;
        private readonly PlayerActionService _actionService;
        private readonly CompositeDisposable _disposables = new();
        private bool _interactionQueued;

        public PlayerInteractionService(
            List<IPlayerInteractionHandler> handlers,
            PlayerActionService actionService)
        {
            _actionService = actionService;
            _handlers = handlers
                .OrderByDescending(handler => handler.Priority)
                .ToArray();
        }

        public void Initialize()
        {
            GlobalEventBus.OnSignal<InteractInputSignal>()
                .Subscribe(_ => HandleInteractionInput())
                .AddTo(_disposables);
            _actionService.RecoveryHandedOff
                .Subscribe(_ => ExecuteQueuedInteraction())
                .AddTo(_disposables);
            _actionService.ActionFailed
                .Subscribe(_ => _interactionQueued = false)
                .AddTo(_disposables);
        }

        private void HandleInteractionInput()
        {
            _interactionQueued = true;
            if (_actionService.RequestRecoveryHandoff())
                return;

            _interactionQueued = false;
            TryInteract();
        }

        private void ExecuteQueuedInteraction()
        {
            if (!_interactionQueued)
                return;

            _interactionQueued = false;
            TryInteract();
        }

        private void TryInteract()
        {
            for (var i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i].TryInteract())
                    return;
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
