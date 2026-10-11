using System;
using System.Collections.Generic;
using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Model;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.PauseSystem.Controller
{
    /// <summary>
    /// Applies pause requests to registered pausables. Requests are
    /// reference-counted by pauser and scoped by <see cref="PauseArea"/>: an
    /// unscoped request (modal UI) freezes everything, while an area-scoped one
    /// (the boss lair freezing the mine) leaves the other areas running.
    /// </summary>
    public sealed class PauseController : IInitializable, IDisposable
    {
        private readonly PauseModel _model;
        private readonly CompositeDisposable _disposables = new();

        // Reused across transitions: OnPause/OnUnpause run user code that may
        // register or unregister synchronously, so the member list has to be
        // snapshotted before it is walked.
        private readonly List<IPausable> _reconcileBuffer = new();
        private bool _disposed;

        public PauseController(PauseModel model) => _model = model;

        public void Initialize()
        {
            GlobalEventBus.OnSignal<PausableRegisteredSignal>()
                .Subscribe(signal => Register(signal.Pausable, signal.Area))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<PausableUnregisteredSignal>()
                .Subscribe(signal => Unregister(signal.Pausable))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<PausableAffectationChangedSignal>()
                .Subscribe(signal => Reconcile(signal.Pausable))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<PauseRequestedSignal>()
                .Subscribe(signal => RequestPause(signal.Pauser, signal.TargetArea))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<PauseReleasedSignal>()
                .Subscribe(signal => ReleasePause(signal.Pauser))
                .AddTo(_disposables);
        }

        private void Register(IPausable pausable, PauseArea area)
        {
            if (pausable == null)
                return;

            // Reconcile even when this was an area update rather than a new
            // registration: a pooled object can come back in a different area.
            _model.Register(pausable, area);
            Reconcile(pausable);
        }

        private void Unregister(IPausable pausable) =>
            _model.Unregister(pausable);

        private void RequestPause(IPauser pauser, PauseArea? targetArea)
        {
            if (!_model.AddPauser(pauser, targetArea))
                return;
            ApplyPauserChange();
        }

        private void ReleasePause(IPauser pauser)
        {
            if (!_model.RemovePauser(pauser))
                return;
            ApplyPauserChange();
        }

        /// <summary>
        /// Re-evaluates every member against the current pauser set. All of
        /// them, not just on the first request or the last release: a second
        /// pauser with a wider scope has to reach members the first one left
        /// running.
        /// </summary>
        private void ApplyPauserChange()
        {
            var paused = _model.Pausers.Count > 0;
            var changed = _model.IsPaused.Value != paused;
            if (changed)
                _model.SetPaused(paused);

            _reconcileBuffer.Clear();
            var pausables = _model.Pausables;
            for (var i = 0; i < pausables.Count; i++)
                _reconcileBuffer.Add(pausables[i]);
            for (var i = 0; i < _reconcileBuffer.Count; i++)
                Reconcile(_reconcileBuffer[i]);
            _reconcileBuffer.Clear();

            if (changed)
                GlobalEventBus.Fire(new PauseStateChangedSignal(paused));
        }

        private void Reconcile(IPausable pausable)
        {
            if (pausable == null || !_model.TryGetArea(pausable, out var area))
                return;

            if (pausable.IsAffectedByPause && _model.ShouldBePaused(area))
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
