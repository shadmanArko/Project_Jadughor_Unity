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
using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.MineSystem.DayAndTimeSystem.Controllers;
using Systems.MineSystem.DayAndTimeSystem.Configs;
using Systems.MineSystem.DayAndTimeSystem.Structs;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MineTransitionSystem.Config;
using Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Controller;
using UnityEngine.SceneManagement;

namespace Systems.MineSystem.MineTransitionSystem.Controller
{
    public sealed class MineTransitionController :
        IPausable,
        IPauser,
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
        private readonly MineEntranceTriggerView _entrance;
        private readonly MineView _mineView;
        private readonly MineTransitionConfig _config;
        private readonly FadeTransitionController _fade;
        private readonly IDayAndTimeController _time;
        private readonly DayAndTimeConfig _timeConfig;
        private readonly CompositeDisposable _disposables = new();
        private readonly ReactiveProperty<MineTransitionState> _state =
            new(MineTransitionState.Idle);
        private CancellationTokenSource _lifetime;
        private CancellationTokenSource _activeTransition;
        private readonly MineTransitionPauseService _pause;
        private bool _isAffectedByPause = true;
        private bool _disposed;
        private bool _insideMine;
        private bool _entranceArmed;
        private bool _holdsMinePause;
        private bool _holdsEntranceClimb;

        public string PauserId => "MineEntranceTransition";

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
            MineEntranceTriggerView entrance,
            MineView mineView,
            MineTransitionConfig config,
            FadeTransitionController fade,
            IDayAndTimeController time,
            DayAndTimeConfig timeConfig,
            MineTransitionPauseService pause)
        {
            _mineController = mineController;
            _campToMine = campToMine;
            _campToMuseum = campToMuseum;
            _mineToCamp = mineToCamp;
            _playerTransitionService = playerTransitionService;
            _camera = camera;
            _canvas = canvas;
            _entrance = entrance;
            _mineView = mineView;
            _config = config;
            _fade = fade;
            _time = time;
            _timeConfig = timeConfig;
            _pause = pause;
        }

        public void Initialize()
        {
            ValidateReferences();
            _lifetime = new CancellationTokenSource();
            _entrance.Configure(_mineView.grid,
                _playerTransitionService.PlayerCollider);
            _entrance.Entered.Subscribe(_ => OpenMinePopup())
                .AddTo(_disposables);
            _entrance.Exited.Subscribe(_ => _entranceArmed = true)
                .AddTo(_disposables);
            _playerTransitionService.LifeState
                .Where(state => state == PlayerLifeState.Dead)
                .Subscribe(_ => HandlePlayerDeath())
                .AddTo(_disposables);
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
            _canvas.backToMineButton.OnClickAsObservable()
                .Subscribe(_ => RunMineChoiceAsync(
                    MineTransitionRoute.BackToMine, BackToMineAsync)
                    .Forget(Debug.LogException))
                .AddTo(_disposables);
            _canvas.sleepForTheNightButton.OnClickAsObservable()
                .Subscribe(_ => RunMineChoiceAsync(
                    IsFinalDay ? MineTransitionRoute.MineToMuseum :
                    MineTransitionRoute.SleepForNight,
                    IsFinalDay ? ReturnToMuseumAsync : SleepAsync)
                    .Forget(Debug.LogException))
                .AddTo(_disposables);
            _canvas.mineToCampButton.interactable = false;
            // Global: OnPause arms a gate the transition tasks await, so a
            // mine-scoped freeze would soft-lock a transition begun in the lair.
            GlobalEventBus.Fire(
                new PausableRegisteredSignal(this, PauseArea.Global));
        }

        public void OnPause() => _pause.Pause();
        public void OnUnpause() => _pause.Resume();

        private bool IsFinalDay =>
            _time.GetCurrentTime().day >= _timeConfig.totalDays;

        private void ValidateReferences()
        {
            if (_canvas == null || _canvas.campTransitionPanel == null ||
                _canvas.mineTransitionPanel == null ||
                _canvas.campToMineButton == null ||
                _canvas.campToMuseumButton == null ||
                _canvas.sleepForTheNightButton == null ||
                _canvas.mineToCampButton == null ||
                _canvas.backToMineButton == null ||
                _canvas.sleepButtonLabel == null ||
                _mineView == null || _mineView.grid == null ||
                _timeConfig == null || _timeConfig.totalDays < 1 ||
                _timeConfig.dayStartHour != 8)
                throw new InvalidOperationException(
                    "Mine transition UI, grid, or day/time configuration is incomplete.");
        }

        private void PrepareCamp(MineData mineData)
        {
            _activeTransition?.Cancel();
            _fade.Hide();
            ReleaseEntranceClimbHold();
            ReleaseMinePause();
            _insideMine = false;
            _entranceArmed = false;
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
            UniTask.FromResult(MineTransitionResult.Unavailable(
                "Go To Camp is not available yet."));

        private async UniTask<MineTransitionResult> ExecuteAsync(
            MineTransitionRoute route,
            Func<CancellationToken, UniTask<MineTransitionResult>> execute,
            CancellationToken cancellationToken)
        {
            if (_state.Value == MineTransitionState.Running)
                return MineTransitionResult.Unavailable(
                    "Another mine transition is already running.");

            var source = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetime.Token, cancellationToken);
            _activeTransition = source;
            _state.Value = MineTransitionState.Running;
            SetPanels(false, false);
            GlobalEventBus.Fire(new MineTransitionStartedSignal { Route = route });

            MineTransitionResult result;
            try
            {
                result = await execute(source.Token);
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

            if (ReferenceEquals(_activeTransition, source))
                _activeTransition = null;
            var cancelled = source.IsCancellationRequested;
            source.Dispose();
            if (_disposed || cancelled)
                return result;
            _state.Value = result.State;
            PublishResult(route, result);
            if (result.Succeeded && route == MineTransitionRoute.CampToMine)
            {
                _insideMine = true;
                _entranceArmed = !_entrance.IsPlayerInside;
            }
            else if (!result.Succeeded && route == MineTransitionRoute.CampToMine)
                RestoreCampPresentation();
            else if (result.State == MineTransitionState.Unavailable &&
                     route != MineTransitionRoute.MineToCamp)
                SetPanels(true, false);
            return result;
        }

        private void OpenMinePopup()
        {
            if (_disposed || !_insideMine || !_entranceArmed ||
                _state.Value == MineTransitionState.Running || _holdsMinePause)
                return;

            if (!_playerTransitionService.TryHoldMineEntranceClimb())
                return;

            _holdsEntranceClimb = true;
            _entranceArmed = false;
            _playerTransitionService.SetManualControlsEnabled(false);
            _camera.ClearFollowTarget();
            _canvas.sleepButtonLabel.text = IsFinalDay
                ? "Go Back to Museum" : "Sleep For The Night";
            HoldMinePause();
            SetPanels(false, true);
            _state.Value = MineTransitionState.Idle;
        }

        private async UniTask RunMineChoiceAsync(MineTransitionRoute route,
            Func<CancellationToken, UniTask<MineTransitionResult>> execute)
        {
            if (_disposed || !_holdsMinePause ||
                _state.Value == MineTransitionState.Running)
                return;

            ReleaseEntranceClimbHold();
            var source = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetime.Token);
            _activeTransition = source;
            _state.Value = MineTransitionState.Running;
            SetPanels(false, false);
            GlobalEventBus.Fire(new MineTransitionStartedSignal { Route = route });
            MineTransitionResult result;
            try
            {
                result = await execute(source.Token);
            }
            catch (OperationCanceledException)
            {
                result = MineTransitionResult.Cancelled();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = MineTransitionResult.Failed(exception.Message);
            }
            finally
            {
                if (ReferenceEquals(_activeTransition, source))
                    _activeTransition = null;
                source.Dispose();
            }

            if (_disposed || !_insideMine)
                return;
            _state.Value = result.State;
            PublishResult(route, result);
            if (!result.Succeeded)
            {
                _fade.Hide();
                _playerTransitionService.Teleport(Cell(0, -1));
            }
            if (route != MineTransitionRoute.MineToMuseum || !result.Succeeded)
            {
                _playerTransitionService.ClearForcedAnimation();
                _camera.SetFollowTarget(
                    _playerTransitionService.PlayerTransform);
                _playerTransitionService.SetManualControlsEnabled(true);
                _playerTransitionService.TryBeginClimbImmediately();
                ReleaseMinePause();
                _entranceArmed = !_entrance.IsPlayerInside;
                _state.Value = MineTransitionState.Idle;
            }
        }

        private async UniTask<MineTransitionResult> BackToMineAsync(
            CancellationToken token)
        {
            _playerTransitionService.PlayForcedAnimation(
                PlayerAnimationId.ClimbVertical, PlayerFacingDirection.Right);
            await _playerTransitionService.AutoMoveAsync(Cell(0, -1),
                _config.backToMineDuration, _config.playerMovementEase, token);
            return MineTransitionResult.Completed();
        }

        private async UniTask<MineTransitionResult> SleepAsync(
            CancellationToken token)
        {
            await ClimbOutAsync(token);
            var nextDay = _time.GetCurrentTime().day + 1;
            await _fade.FadeInAsync($"Day {nextDay:D2}", token);
            _time.SetTime(new MineTime(nextDay,
                _timeConfig.dayStartHour, 0));
            await _fade.HoldDayAsync(token);
            _playerTransitionService.Teleport(
                Cell(_config.mineEntryStartCell.x,
                    _config.mineEntryStartCell.y));
            await _fade.FadeOutAsync(token);
            _playerTransitionService.PlayForcedAnimation(
                PlayerAnimationId.ClimbVertical, PlayerFacingDirection.Right);
            await _playerTransitionService.AutoMoveAsync(Cell(0, -2),
                _config.overnightReturnDuration,
                _config.playerMovementEase, token);
            return MineTransitionResult.Completed();
        }

        private async UniTask<MineTransitionResult> ReturnToMuseumAsync(
            CancellationToken token)
        {
            if (!Application.CanStreamedLevelBeLoaded("Museum"))
                return MineTransitionResult.Unavailable(
                    "Museum scene is missing from Build Settings.");
            await ClimbOutAsync(token);
            await _fade.FadeInAsync(null, token);
            token.ThrowIfCancellationRequested();
            SceneManager.LoadScene("Museum");
            return MineTransitionResult.Completed();
        }

        private async UniTask ClimbOutAsync(CancellationToken token)
        {
            _camera.ClearFollowTarget();
            _playerTransitionService.PlayForcedAnimation(
                PlayerAnimationId.ClimbVertical, PlayerFacingDirection.Right);
            await _playerTransitionService.AutoMoveAsync(
                Cell(_config.mineEntryStartCell.x,
                    _config.mineEntryStartCell.y),
                _config.mineExitDuration, _config.playerMovementEase, token);
        }

        private Vector2 Cell(int x, int y) =>
            _mineView.grid.GetCellCenterWorld(new Vector3Int(x, y, 0));

        private void ReleaseEntranceClimbHold()
        {
            if (!_holdsEntranceClimb)
                return;

            _holdsEntranceClimb = false;
            _playerTransitionService.ReleaseMineEntranceClimbHold();
        }

        private void HandlePlayerDeath()
        {
            if (_disposed || !_holdsMinePause)
                return;

            _activeTransition?.Cancel();
            ReleaseEntranceClimbHold();
            ReleaseMinePause();
            SetPanels(false, false);
            _insideMine = false;
            _entranceArmed = false;
            _state.Value = MineTransitionState.Cancelled;
        }

        private void HoldMinePause()
        {
            if (_holdsMinePause) return;
            _holdsMinePause = true;
            GlobalEventBus.Fire(new PauseRequestedSignal(this,
                PauseArea.MineView));
        }

        private void ReleaseMinePause()
        {
            if (!_holdsMinePause) return;
            _holdsMinePause = false;
            GlobalEventBus.Fire(new PauseReleasedSignal(this));
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
            if (_lifetime != null && !_lifetime.IsCancellationRequested)
                _lifetime.Cancel();
            _activeTransition?.Cancel();
            _fade.Hide();
            ReleaseEntranceClimbHold();
            ReleaseMinePause();
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
            _lifetime?.Dispose();
            _disposables.Dispose();
            _state.Dispose();
        }
    }
}
