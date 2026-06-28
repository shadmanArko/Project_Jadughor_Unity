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
        private readonly CompositeDisposable _disposables = new();

        public PlayerInteractionService(
            List<IPlayerInteractionHandler> handlers)
        {
            _handlers = handlers
                .OrderByDescending(handler => handler.Priority)
                .ToArray();
        }

        public void Initialize()
        {
            GlobalEventBus.OnSignal<InteractInputSignal>()
                .Subscribe(_ => TryInteract())
                .AddTo(_disposables);
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
