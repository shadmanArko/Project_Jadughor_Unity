using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Systems.MineSystem.ActorSystem.Interface;
using Systems.MineSystem.ActorSystem.Service;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Controller
{
    /// <summary>
    /// Minimal hedgehog boss controller: enough to be placed in its lair and
    /// driven by a cutscene (<see cref="IActor"/>). No AI, health or attacks —
    /// those land with the boss's own behaviour pass; this pass exists so the
    /// boss can move and animate during the lair-entry cutscene.
    /// </summary>
    public sealed class HedgehogBossController : IEnemyController, IActor
    {
        private readonly HedgehogBossView _view;
        private readonly ActorMovementTweenRunner _movement = new();
        private readonly HedgehogBossPauseStateData _pauseState = new();
        private HedgehogBossConfigScriptable _config;
        private bool _isAffectedByPause = true;
        private bool _disposed;

        public Guid EnemyId { get; private set; }
        public EnemyType EnemyType => EnemyType.Boss;
        public bool IsActive { get; private set; }
        public bool IsDead => false;
        public GridPosition CurrentGridPosition { get; private set; }
        public Vector2 Position => _view.Body.position;

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value)
                    return;
                _isAffectedByPause = value;
                if (IsActive)
                {
                    GlobalEventBus.Fire(
                        new PausableAffectationChangedSignal(this));
                }
            }
        }

        public HedgehogBossController(HedgehogBossView view)
        {
            _view = view;
        }

        public void Initialize(EnemyInitializeData initializeData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HedgehogBossController));
            if (IsActive)
                Release();
            if (initializeData.Config is not HedgehogBossConfigScriptable config)
            {
                throw new ArgumentException(
                    "HedgehogBossController requires HedgehogBossConfigScriptable.");
            }
            if (!_view.ValidateReferences())
                throw new InvalidOperationException(
                    "HedgehogBossView is not configured.");

            _config = config;
            EnemyId = Guid.NewGuid();
            CurrentGridPosition = initializeData.SpawnGridPosition;
            _view.ResetRuntime();
            _view.Teleport(initializeData.SpawnWorldPosition);
            _view.ApplyConfig(config);
            ClearAnimation();
            IsActive = true;
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));
        }

        public void OnFixedTick(EnemyTickContext tickContext)
        {
            // No AI this pass — behaviour lands with the boss's combat pass.
        }

        public UniTask SpawnAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public UniTask DespawnAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public UniTask MoveToAsync(
            Vector2 destination,
            float duration,
            Ease ease,
            CancellationToken cancellationToken) =>
            _movement.MoveAsync(
                _view.Body, destination, duration, ease, cancellationToken);

        public void PlayAnimation(string animationId, bool facesLeft)
        {
            _view.SetFacing(facesLeft);
            if (_config != null && _config.AnimationProfile != null &&
                _config.AnimationProfile.TryGet(animationId, out var data))
            {
                _view.Play(data);
            }
            else
            {
                Debug.LogWarning(
                    $"[HedgehogBoss] animation '{animationId}' missing from " +
                    "its animation profile.");
            }
        }

        public void ClearAnimation() =>
            PlayAnimation(HedgehogBossAnimationId.Idle, facesLeft: false);

        public void OnPause()
        {
            if (!IsActive || _pauseState.HasSnapshot)
                return;
            _pauseState.HasSnapshot = true;
            _pauseState.AnimatorSpeed = _view.AnimatorSpeed;
            _pauseState.MovementWasPlaying = _movement.Pause();
            _view.SetAnimatorSpeed(0f);
        }

        public void OnUnpause()
        {
            if (!IsActive || !_pauseState.HasSnapshot)
                return;
            _view.SetAnimatorSpeed(_pauseState.AnimatorSpeed);
            _movement.Resume(_pauseState.MovementWasPlaying);
            _pauseState.Clear();
        }

        public void Release()
        {
            if (!IsActive)
                return;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
            IsActive = false;
            _movement.Dispose();
            _pauseState.Clear();
            _view.ResetRuntime();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Release();
        }
    }
}
