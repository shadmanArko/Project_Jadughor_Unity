using System;
using System.Collections.Generic;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Model;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.PauseSystem.Controller
{
    public sealed class PauseController : IInitializable, IDisposable
    {
        private readonly PauseModel _model;
        private readonly CompositeDisposable _disposables = new();
        private bool _disposed;

        public PauseController(PauseModel model) => _model = model;

        public void Initialize()
        {
            GlobalEventBus.OnSignal<PausableRegisteredSignal>()
                .Subscribe(signal => Register(signal.Pausable))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<PausableUnregisteredSignal>()
                .Subscribe(signal => Unregister(signal.Pausable))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<PausableAffectationChangedSignal>()
                .Subscribe(signal => Reconcile(signal.Pausable))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<PauseRequestedSignal>()
                .Subscribe(signal => RequestPause(signal.Pauser))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<PauseReleasedSignal>()
                .Subscribe(signal => ReleasePause(signal.Pauser))
                .AddTo(_disposables);
        }

        private void Register(IPausable pausable)
        {
            if (!_model.Register(pausable))
                return;
            Reconcile(pausable);
        }

        private void Unregister(IPausable pausable) =>
            _model.Unregister(pausable);

        private void RequestPause(IPauser pauser)
        {
            var wasEmpty = _model.Pausers.Count == 0;
            if (!_model.AddPauser(pauser) || !wasEmpty)
                return;

            _model.SetPaused(true);
            var snapshot = new List<IPausable>(_model.Pausables);
            for (var i = 0; i < snapshot.Count; i++)
                Reconcile(snapshot[i]);
            GlobalEventBus.Fire(new PauseStateChangedSignal(true));
        }

        private void ReleasePause(IPauser pauser)
        {
            if (!_model.RemovePauser(pauser) || _model.Pausers.Count > 0)
                return;

            _model.SetPaused(false);
            var snapshot = new List<IPausable>(_model.PausedMembers);
            for (var i = 0; i < snapshot.Count; i++)
                TryUnpause(snapshot[i]);
            GlobalEventBus.Fire(new PauseStateChangedSignal(false));
        }

        private void Reconcile(IPausable pausable)
        {
            if (pausable == null || !_model.IsPaused.Value)
                return;

            if (pausable.IsAffectedByPause)
                TryPause(pausable);
            else
                TryUnpause(pausable);
        }

        private void TryPause(IPausable pausable)
        {
            if (_model.IsMemberPaused(pausable))
                return;
            try
            {
                pausable.OnPause();
                _model.MarkPaused(pausable);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void TryUnpause(IPausable pausable)
        {
            if (!_model.IsMemberPaused(pausable))
                return;
            try
            {
                pausable.OnUnpause();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _model.MarkUnpaused(pausable);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _disposables.Dispose();
        }
    }
}
