using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Controller;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MineTransitionSystem.Model;
using Systems.MineSystem.MineTransitionSystem.Service;
using Systems.MineSystem.MineTransitionSystem.Signal;
using Systems.MineSystem.MineTransitionSystem.View;
using Systems.MineSystem.Utilities.Camera;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;

namespace Systems.MineSystem.MineTransitionSystem.Controller
{
    public sealed class MineTransitionController :
        IPausable,
        IInitializable,
        IDisposable
    {
        private readonly MineController _mineController;
        private readonly CampToMineService _campToMine;
        private readonly CampToMuseumService _campToMuseum;
        private readonly MineToCampService _mineToCamp;
        private readonly PlayerTransitionService _playerTransitionService;
        private readonly MineCameraController _camera;
        private readonly MineTransitionCanvasView _canvas;
        private readonly CompositeDisposable _disposables = new();
        private readonly ReactiveProperty<MineTransitionState> _state =
            new(MineTransitionState.Idle);
        private CancellationTokenSource _lifetime;
        private CancellationTokenSource _activeTransition;
        private readonly MineTransitionPauseService _pause;
        private bool _isAffectedByPause = true;
        private bool _disposed;

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value) return;
                _isAffectedByPause = value;
                GlobalEventBus.Fire(new PausableAffectationChangedSignal(this));
            }
        }

        public IReadOnlyReactiveProperty<MineTransitionState> State => _state;

        public MineTransitionController(MineController mineController,
            CampToMineService campToMine,
            CampToMuseumService campToMuseum,
            MineToCampService mineToCamp,
            PlayerTransitionService playerTransitionService,
            MineCameraController camera,
            MineTransitionCanvasView canvas,
            MineTransitionPauseService pause)
        {
            _mineController = mineController;
            _campToMine = campToMine;
            _campToMuseum = campToMuseum;
            _mineToCamp = mineToCamp;
            _playerTransitionService = playerTransitionService;
            _camera = camera;
            _canvas = canvas;
            _pause = pause;
        }

        public void Initialize()
        {
            _lifetime = new CancellationTokenSource();
            SetPanels(false, false);
            _mineController.MineGenerated
                .Subscribe(PrepareCamp)
                .AddTo(_disposables);
            _canvas.campToMineButton.OnClickAsObservable()
                .Subscribe(_ => CampToMineAsync(_lifetime.Token)
                    .Forget(Debug.LogException))
                .AddTo(_disposables);
            _canvas.campToMuseumButton.OnClickAsObservable()
                .Subscribe(_ => CampToMuseumAsync(_lifetime.Token)
                    .Forget(Debug.LogException))
                .AddTo(_disposables);
            _canvas.mineToCampButton.OnClickAsObservable()
                .Subscribe(_ => MineToCampAsync(_lifetime.Token)
                    .Forget(Debug.LogException))
                .AddTo(_disposables);
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));
        }

        public void OnPause() => _pause.Pause();
        public void OnUnpause() => _pause.Resume();

        private void PrepareCamp(MineData mineData)
        {
            _camera.ConfigureMineBounds(mineData);
            RestoreCampPresentation();
        }

        private void RestoreCampPresentation()
        {
            _playerTransitionService.SpawnForTransition();
            _camera.SetFreeMovement(true);
            var cameraPosition = _camera.Position;
            cameraPosition.x = _playerTransitionService.Position.x;
            cameraPosition.y = _playerTransitionService.Position.y;
            _camera.SetPosition(cameraPosition);
            _camera.SetFollowTarget(_playerTransitionService.PlayerTransform);
            SetPanels(true, false);
            _state.Value = MineTransitionState.Idle;
        }

        public UniTask<MineTransitionResult> CampToMineAsync(
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(MineTransitionRoute.CampToMine,
                _campToMine.ExecuteAsync, cancellationToken);

        public UniTask<MineTransitionResult> CampToMuseumAsync(
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(MineTransitionRoute.CampToMuseum,
                _campToMuseum.ExecuteAsync, cancellationToken);

        public UniTask<MineTransitionResult> MineToCampAsync(
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(MineTransitionRoute.MineToCamp,
                _mineToCamp.ExecuteAsync, cancellationToken);

        private async UniTask<MineTransitionResult> ExecuteAsync(
            MineTransitionRoute route,
            Func<CancellationToken, UniTask<MineTransitionResult>> execute,
            CancellationToken cancellationToken)
        {
            if (_state.Value == MineTransitionState.Running)
                return MineTransitionResult.Unavailable(
                    "Another mine transition is already running.");

            _activeTransition?.Dispose();
            _activeTransition = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetime.Token, cancellationToken);
            _state.Value = MineTransitionState.Running;
            SetPanels(false, false);
            GlobalEventBus.Fire(new MineTransitionStartedSignal { Route = route });

            MineTransitionResult result;
            try
            {
                result = await execute(_activeTransition.Token);
            }
            catch (OperationCanceledException)
            {
                result = MineTransitionResult.Cancelled();
            }
            catch (Exception exception)
            {
                result = MineTransitionResult.Failed(exception.Message);
                Debug.LogException(exception);
            }

            _activeTransition?.Dispose();
            _activeTransition = null;
            _state.Value = result.State;
            PublishResult(route, result);
            if (!result.Succeeded && route == MineTransitionRoute.CampToMine)
                RestoreCampPresentation();
            else if (result.State == MineTransitionState.Unavailable &&
                     route != MineTransitionRoute.MineToCamp)
                SetPanels(true, false);
            return result;
        }

        private static void PublishResult(MineTransitionRoute route,
            MineTransitionResult result)
        {
            switch (result.State)
            {
                case MineTransitionState.Completed:
                    GlobalEventBus.Fire(new MineTransitionCompletedSignal { Route = route });
                    break;
                case MineTransitionState.Cancelled:
                    GlobalEventBus.Fire(new MineTransitionCancelledSignal { Route = route });
                    break;
                case MineTransitionState.Unavailable:
                    GlobalEventBus.Fire(new MineTransitionUnavailableSignal
                        { Route = route, Reason = result.Error });
                    break;
                case MineTransitionState.Failed:
                    GlobalEventBus.Fire(new MineTransitionFailedSignal
                        { Route = route, Error = result.Error });
                    break;
            }
        }

        private void SetPanels(bool camp, bool mine)
        {
            _canvas.campTransitionPanel.SetActive(camp);
            _canvas.mineTransitionPanel.SetActive(mine);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
            if (_lifetime != null && !_lifetime.IsCancellationRequested)
                _lifetime.Cancel();
            _activeTransition?.Cancel();
            _activeTransition?.Dispose();
            _lifetime?.Dispose();
            _disposables.Dispose();
            _state.Dispose();
        }
    }
}
