using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Systems.MineSystem.ActorSystem.Interface;
using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.MineSystem.MineTransitionSystem.View;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerTransitionService : IActor
    {
        private readonly PlayerModel _model;
        private readonly PlayerView _view;
        private readonly MinePlayerDataConfig _config;
        private readonly RuntimeDataScriptable _runtimeData;
        private readonly CampView _campView;

        public PlayerTransitionService(
            PlayerModel model, 
            PlayerView view,
            MinePlayerDataConfig config, 
            RuntimeDataScriptable runtimeData, 
            CampView campView)
        {
            _model = model;
            _view = view;
            _config = config;
            _runtimeData = runtimeData;
            _campView = campView;
        }

        public Transform PlayerTransform => _view.transform;
        public Vector2 Position => _view.Body.position;

        public void SpawnForTransition()
        {
            _view.gameObject.SetActive(true);
            _runtimeData.worldPosition.Value = _view.PlayerCollider.bounds.center;
            _view.Teleport(_campView.outsideCampSpawnPoint.position);
            _view.SetGravityScale(0f);
            _runtimeData.isSpawned.Value = true;
            SetManualControlsEnabled(false);
        }

        public void Teleport(Vector2 position)
        {
            _model.PrepareForTransport();
            _view.Teleport(position);
            if (!_runtimeData.canMove.Value)
                _view.SetGravityScale(0f);
            _runtimeData.worldPosition.Value = _view.PlayerCollider.bounds.center;
        }

        public void SetManualControlsEnabled(bool enabled)
        {
            _runtimeData.canMove.Value = enabled;
            _runtimeData.canClimb.Value = enabled;
            _runtimeData.canPerformAction.Value = enabled;
            _runtimeData.canUsePickaxe.Value = enabled;
            _runtimeData.canUseWeapon.Value = enabled;
            _view.SetGravityScale(enabled ? _config.normalGravityScale : 0f);
            if (!enabled)
            {
                _runtimeData.movementInput.Value = Vector2.zero;
                _view.Stop();
            }
        }

        public UniTask AutoMoveAsync(Vector2 destination, float duration,
            Ease ease, CancellationToken cancellationToken) =>
            _model.AutoMoveAsync(destination, duration, ease, cancellationToken);

        public void PlayForcedAnimation(string animationId,
            PlayerFacingDirection facing) =>
            _model.PlayForcedAnimation(animationId, facing);

        public void ClearForcedAnimation() => _model.ClearForcedAnimation();

        UniTask IActor.MoveToAsync(
            Vector2 destination,
            float duration,
            Ease ease,
            CancellationToken cancellationToken) =>
            AutoMoveAsync(destination, duration, ease, cancellationToken);

        void IActor.PlayAnimation(string animationId, bool facesLeft) =>
            PlayForcedAnimation(
                animationId,
                facesLeft ? PlayerFacingDirection.Left : PlayerFacingDirection.Right);

        void IActor.ClearAnimation() => ClearForcedAnimation();
    }
}
