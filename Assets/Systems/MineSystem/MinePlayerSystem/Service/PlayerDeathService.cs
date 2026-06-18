using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using Systems.MineSystem.MinePlayerSystem.View;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerDeathService : IPlayerFixedTickService
    {
        private readonly PlayerView _view;
        private readonly RuntimeDataScriptable _runtime;
        private readonly MinePlayerScriptable _playerData;

        private int _animationGeneration;

        public PlayerDeathService(
            PlayerView view,
            RuntimeDataScriptable runtime,
            MinePlayerScriptable playerData)
        {
            _view = view;
            _runtime = runtime;
            _playerData = playerData;
        }

        public void OnFixedTick()
        {
            if (_runtime.lifeState.Value == PlayerLifeState.Dead ||
                _playerData.playerData.health.Value > 0f)
                return;

            _runtime.lifeState.Value = PlayerLifeState.Dead;
            _runtime.actionState.Value = PlayerActionState.None;
            _runtime.isClimbing.Value = false;
            _runtime.restrictions.Value =
                PlayerRestrictionFlags.Movement |
                PlayerRestrictionFlags.Climbing |
                PlayerRestrictionFlags.Action;
            _view.SetGravityScale(0f);
            _view.Stop();
        }

        public void RegisterAnimationGeneration(int generation)
        {
            _animationGeneration = generation;
        }

        public void HandleAnimationCompleted(
            PlayerAnimationCompletedEvent animationEvent)
        {
            if (animationEvent.AnimationId != PlayerAnimationId.Death ||
                animationEvent.Generation != _animationGeneration)
                return;

            _view.Stop();
        }
    }
}
