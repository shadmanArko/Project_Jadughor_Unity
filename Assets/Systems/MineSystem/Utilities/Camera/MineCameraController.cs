using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;

namespace Systems.MineSystem.Utilities.Camera
{
    public sealed class MineCameraController :
        IPausable,
        IInitializable,
        IDisposable
    {
        private readonly CinemachineCamera _camera;
        private readonly MineView _mineView;
        private readonly MineCameraConfig _config;
        private CinemachineConfiner2D _confiner;
        private Tween _activeTween;
        private bool _isAffectedByPause = true;
        private bool _tweenWasPlaying;
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

        public Vector3 Position => _camera.transform.position;
        public float OrthographicSize => _camera.Lens.OrthographicSize;
        public bool IsFreeMovement { get; private set; }

        public MineCameraController(CinemachineCamera camera, MineView mineView,
            MineCameraConfig config)
        {
            _camera = camera;
            _mineView = mineView;
            _config = config;
        }

        public void Initialize()
        {
            _confiner = _camera.GetComponent<CinemachineConfiner2D>();
            if (_confiner == null)
                throw new InvalidOperationException("CinemachineCamera prefab requires CinemachineConfiner2D.");
            if (_mineView.cameraBoundaryCollider == null)
                throw new InvalidOperationException("MineView prefab requires cameraBoundaryCollider.");

            _camera.Lens.OrthographicSize = _config.orthographicSize;
            _confiner.Damping = _config.confinerDamping;
            _confiner.BoundingShape2D = _mineView.cameraBoundaryCollider;
            ClearFollowTarget();
            SetFreeMovement(true);
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));
        }

        public void ConfigureMineBounds(MineData mineData)
        {
            var minimumCell = new Vector3Int(-mineData.GridWidth / 2,
                -(mineData.GridHeight - 1), 0);
            var maximumCell = new Vector3Int(
                minimumCell.x + mineData.GridWidth - 1, 0, 0);
            var minimumCenter = _mineView.grid.GetCellCenterWorld(minimumCell);
            var maximumCenter = _mineView.grid.GetCellCenterWorld(maximumCell);
            var cellSize = _mineView.grid.cellSize;
            var worldCenter = (minimumCenter + maximumCenter) * 0.5f;
            var worldSize = new Vector2(
                Mathf.Abs(maximumCenter.x - minimumCenter.x) + cellSize.x,
                Mathf.Abs(maximumCenter.y - minimumCenter.y) + cellSize.y);
            var boundaryTransform = _mineView.cameraBoundaryCollider.transform;
            var localCenter = boundaryTransform.InverseTransformPoint(worldCenter);
            var scale = boundaryTransform.lossyScale;

            _mineView.cameraBoundaryCollider.offset = localCenter;
            _mineView.cameraBoundaryCollider.size = new Vector2(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
            _confiner.InvalidateBoundingShapeCache();
        }

        public void SetFollowTarget(Transform target) => _camera.Follow = target;
        public void ClearFollowTarget() => _camera.Follow = null;

        public void SetFreeMovement(bool enabled)
        {
            IsFreeMovement = enabled;
            _confiner.enabled = !enabled;
        }

        public void SetPosition(Vector3 position)
        {
            _activeTween?.Kill();
            _camera.transform.position = position;
        }

        public async UniTask PanAsync(Vector3 from, Vector3 to, float duration,
            CancellationToken cancellationToken)
        {
            SetPosition(from);
            if (duration <= 0f)
            {
                SetPosition(to);
                return;
            }

            var tween = _camera.transform.DOMove(to, duration)
                .SetEase(_config.panEase);
            await AwaitTweenAsync(tween, cancellationToken);
        }

        public UniTask PanAsync(Vector3 from, Vector3 to,
            CancellationToken cancellationToken) =>
            PanAsync(from, to, _config.defaultPanDuration,
                cancellationToken);

        private async UniTask AwaitTweenAsync(Tween tween,
            CancellationToken cancellationToken)
        {
            _activeTween?.Kill();
            _activeTween = tween;
            var completion = new UniTaskCompletionSource();
            var finished = false;
            CancellationTokenRegistration registration = default;
            tween.OnComplete(() =>
            {
                if (finished) return;
                finished = true;
                registration.Dispose();
                completion.TrySetResult();
            });
            tween.OnKill(() =>
            {
                if (finished) return;
                finished = true;
                registration.Dispose();
                if (cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else completion.TrySetResult();
            });
            if (cancellationToken.CanBeCanceled)
                registration = cancellationToken.Register(() => tween.Kill());
            await completion.Task;
            if (ReferenceEquals(_activeTween, tween)) _activeTween = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
            _activeTween?.Kill();
            _activeTween = null;
        }

        public void OnPause()
        {
            _tweenWasPlaying = _activeTween != null &&
                               _activeTween.IsActive() &&
                               _activeTween.IsPlaying();
            if (_tweenWasPlaying) _activeTween.Pause();
        }

        public void OnUnpause()
        {
            if (_tweenWasPlaying && _activeTween != null &&
                _activeTween.IsActive()) _activeTween.Play();
            _tweenWasPlaying = false;
        }
    }
}
