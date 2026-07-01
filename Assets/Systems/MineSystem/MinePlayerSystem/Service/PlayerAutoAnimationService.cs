using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerAutoAnimationService
    {
        private readonly RuntimeDataScriptable _runtime;

        public PlayerAutoAnimationService(RuntimeDataScriptable runtime) =>
            _runtime = runtime;

        public void Play(string animationId, PlayerFacingDirection facing)
        {
            _runtime.facingDirection.Value = facing;
            _runtime.forcedAnimation.Value = animationId;
        }

        public void Clear() =>
            _runtime.forcedAnimation.Value = PlayerAnimationId.None;
    }
}
