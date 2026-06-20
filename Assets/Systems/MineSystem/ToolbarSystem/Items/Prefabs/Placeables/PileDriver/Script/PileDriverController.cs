using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.Utilities.ScreenShake;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script
{
    public sealed class PileDriverController : IDisposable
    {
        private readonly PileDriverModel _model;
        private readonly PileDriverView _view;
        private readonly PileDriverConfig _config;
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly PlayerView _player;
        private readonly CancellationTokenSource _lifetime = new();

        private readonly Vector2 _baseExtensionSize;
        private readonly Vector3 _baseHeadLocalPosition;
        private readonly Quaternion _baseRootRotation;
        private Tween _activeTween;

        public PileDriverController(
            PileDriverModel model,
            PileDriverView view,
            PileDriverConfig config,
            MineModel mine,
            MineView mineView,
            PlayerView player)
        {
            _model = model;
            _view = view;
            _config = config;
            _mine = mine;
            _mineView = mineView;
            _player = player;

            _baseExtensionSize = _view.Extension.size;
            _baseHeadLocalPosition = _view.Head.transform.localPosition;
            _baseRootRotation = _view.transform.localRotation;
        }

        public void Start(PileDriverDirection direction)
        {
            ResetPresentation();
            _view.transform.localRotation =
                _baseRootRotation * Quaternion.Euler(
                    0f,
                    0f,
                    GetRotationDegrees(direction));
            _model.RunAsync(this, _lifetime.Token)
                .Forget(exception =>
                {
                    if (exception is not OperationCanceledException)
                        Debug.LogException(exception);
                });
        }

        public async UniTask PlayTurnOnAsync(
            CancellationToken cancellationToken)
        {
            var animationDuration = PlayAnimatorState(
                _config.TurnOnState,
                _config.TurnOnFallbackDuration);
            await DelayAsync(
                animationDuration,
                cancellationToken);
        }

        public void PlayActive()
        {
            PlayAnimatorState(_config.ActiveState);
        }

        public async UniTask PlayTurnOffAsync(
            CancellationToken cancellationToken)
        {
            var animationDuration = PlayAnimatorState(
                _config.TurnOffState,
                _config.TurnOffFallbackDuration);
            await DelayAsync(
                animationDuration,
                cancellationToken);
        }

        public async UniTask ExtendAsync(
            Vector3Int precedingCell,
            CancellationToken cancellationToken)
        {
            var targetWorld = _mineView.grid.GetCellCenterWorld(
                precedingCell);
            var targetLocal =
                _view.transform.InverseTransformPoint(targetWorld);
            var targetHead = _baseHeadLocalPosition;
            targetHead.y = targetLocal.y;

            var rootAxisScale = Mathf.Max(
                0.0001f,
                Mathf.Abs(_view.transform.lossyScale.y));
            var worldDistance =
                Mathf.Abs(targetHead.y - _baseHeadLocalPosition.y) *
                rootAxisScale;
            var duration =
                worldDistance /
                Mathf.Max(0.0001f, GetCellWorldSize()) *
                _config.SecondsPerCell;

            var extensionScale = Mathf.Max(
                0.0001f,
                Mathf.Abs(_view.Extension.transform.lossyScale.y));
            var targetSize = _baseExtensionSize;
            targetSize.y += worldDistance / extensionScale;

            if (duration <= 0f)
            {
                _view.Extension.size = targetSize;
                _view.Head.transform.localPosition = targetHead;
                return;
            }

            var sequence = DOTween.Sequence();
            sequence.Join(DOTween.To(
                () => _view.Extension.size,
                value => _view.Extension.size = value,
                targetSize,
                duration));
            sequence.Join(_view.Head.transform.DOLocalMove(
                targetHead,
                duration));
            sequence.SetEase(Ease.Linear);
            await AwaitTweenAsync(sequence, cancellationToken);
        }

        public async UniTask StompAsync(
            Vector3Int targetCell,
            int damage,
            CancellationToken cancellationToken)
        {
            var capturedShake = ResolveShakeLevel();
            var start = _view.Head.transform.localPosition;
            var rootScale = Mathf.Max(
                0.0001f,
                Mathf.Abs(_view.transform.lossyScale.y));
            var travel = GetCellWorldSize() / rootScale;
            var impact = start + Vector3.down * travel;
            var impactDuration = _config.HardStompSecondsPerCell;

            var sequence = DOTween.Sequence();
            sequence.Append(_view.Head.transform.DOLocalMove(
                    impact,
                    impactDuration)
                .SetEase(Ease.InCubic));
            sequence.AppendCallback(() =>
            {
                _mine.TryHitCell(targetCell, damage);
                if (capturedShake.HasValue)
                {
                    ScreenShakeController.VerticalShake(
                        capturedShake.Value);
                }
            });
            if (_config.DelayAfterStomp > 0f)
                sequence.AppendInterval(_config.DelayAfterStomp);
            sequence.Append(_view.Head.transform.DOLocalMove(
                    start,
                    impactDuration)
                .SetEase(Ease.OutQuad));
            await AwaitTweenAsync(sequence, cancellationToken);
        }

        public async UniTask RetractAsync(
            CancellationToken cancellationToken)
        {
            var currentExtraWorld =
                Mathf.Max(
                    0f,
                    (_view.Extension.size.y -
                     _baseExtensionSize.y) *
                    Mathf.Abs(
                        _view.Extension.transform.lossyScale.y));
            var cells = currentExtraWorld /
                        Mathf.Max(0.0001f, GetCellWorldSize());
            var duration = cells * _config.SecondsPerCell;

            if (duration <= 0f)
            {
                _view.Extension.size = _baseExtensionSize;
                _view.Head.transform.localPosition =
                    _baseHeadLocalPosition;
                return;
            }

            var sequence = DOTween.Sequence();
            sequence.Join(DOTween.To(
                () => _view.Extension.size,
                value => _view.Extension.size = value,
                _baseExtensionSize,
                duration));
            sequence.Join(_view.Head.transform.DOLocalMove(
                _baseHeadLocalPosition,
                duration));
            sequence.SetEase(Ease.Linear);
            await AwaitTweenAsync(sequence, cancellationToken);
        }

        private ScreenShakeLevel? ResolveShakeLevel()
        {
            var distance = Vector2.Distance(
                _player.PlayerCollider.bounds.center,
                _view.transform.position);
            var cells = distance /
                        Mathf.Max(0.0001f, GetCellWorldSize());

            if (cells <= _config.ExtremeShakeDistance)
                return ScreenShakeLevel.Extreme;
            if (cells <= _config.HeavyShakeDistance)
                return ScreenShakeLevel.Heavy;
            if (cells <= _config.MediumShakeDistance)
                return ScreenShakeLevel.Medium;
            if (cells <= _config.LightShakeDistance)
                return ScreenShakeLevel.Light;
            return null;
        }

        private float GetCellWorldSize()
        {
            var grid = _mineView.grid;
            if (grid == null)
                return _config.FallbackCellWorldSize;

            var origin = grid.CellToWorld(Vector3Int.zero);
            var down = grid.CellToWorld(Vector3Int.down);
            return Vector3.Distance(origin, down);
        }

        private float PlayAnimatorState(
            string stateName,
            float fallbackDuration = 0f)
        {
            if (_view.CoreAnimator == null ||
                string.IsNullOrWhiteSpace(stateName))
                return fallbackDuration;

            _view.CoreAnimator.Play(
                Animator.StringToHash(stateName),
                0,
                0f);
            _view.CoreAnimator.Update(0f);

            var state = _view.CoreAnimator.GetCurrentAnimatorStateInfo(0);
            var speed = Mathf.Max(
                0.0001f,
                Mathf.Abs(state.speed * state.speedMultiplier));
            return state.length > 0f
                ? state.length / speed
                : fallbackDuration;
        }

        private static float GetRotationDegrees(
            PileDriverDirection direction)
        {
            return direction switch
            {
                PileDriverDirection.Left => -90f,
                PileDriverDirection.Right => 90f,
                PileDriverDirection.Up => 180f,
                _ => 0f
            };
        }

        private static async UniTask DelayAsync(
            float seconds,
            CancellationToken cancellationToken)
        {
            if (seconds <= 0f)
                return;

            await UniTask.Delay(
                TimeSpan.FromSeconds(seconds),
                cancellationToken: cancellationToken);
        }

        private async UniTask AwaitTweenAsync(
            Tween tween,
            CancellationToken cancellationToken)
        {
            _activeTween?.Kill();
            _activeTween = tween;
            var completion = new UniTaskCompletionSource();
            var finished = false;
            CancellationTokenRegistration registration = default;

            tween.OnComplete(() =>
            {
                if (finished)
                    return;

                finished = true;
                registration.Dispose();
                completion.TrySetResult();
            });
            tween.OnKill(() =>
            {
                if (finished)
                    return;

                finished = true;
                registration.Dispose();
                if (cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else
                    completion.TrySetResult();
            });

            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(
                    () => tween.Kill());
            }

            await completion.Task;
            if (ReferenceEquals(_activeTween, tween))
                _activeTween = null;
        }

        private void ResetPresentation()
        {
            _activeTween?.Kill();
            _activeTween = null;
            _view.Extension.size = _baseExtensionSize;
            _view.Head.transform.localPosition =
                _baseHeadLocalPosition;
        }

        public void Dispose()
        {
            if (!_lifetime.IsCancellationRequested)
                _lifetime.Cancel();
            _activeTween?.Kill();
            _activeTween = null;
            ResetPresentation();
            _view.transform.localRotation = _baseRootRotation;
            _model.Dispose();
            _lifetime.Dispose();
        }
    }
}
