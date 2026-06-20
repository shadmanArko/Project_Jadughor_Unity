using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Scriptable
{
    [CreateAssetMenu(fileName = "RuntimeDataScriptable", menuName = "Scriptable/RuntimeDataScriptable")]
    public sealed class RuntimeDataScriptable : ScriptableObject
    {
        public ReactiveProperty<bool> canMove = new(true);
        public ReactiveProperty<bool> canClimb = new(true);
        public ReactiveProperty<bool> canPerformAction = new(true);
        public ReactiveProperty<bool> canUsePickaxe = new(true);
        public ReactiveProperty<bool> canUseWeapon = new(true);

        public ReactiveProperty<PlayerLocomotionState> locomotionState =
            new(PlayerLocomotionState.Idle);
        public ReactiveProperty<PlayerActionState> actionState =
            new(PlayerActionState.None);
        public ReactiveProperty<PlayerLifeState> lifeState =
            new(PlayerLifeState.Alive);
        public ReactiveProperty<PlayerFacingDirection> facingDirection =
            new(PlayerFacingDirection.Right);
        public ReactiveProperty<PlayerRestrictionFlags> restrictions =
            new(PlayerRestrictionFlags.None);
        public ReactiveProperty<string> activeAnimation =
            new(PlayerAnimationId.None);

        public ReactiveProperty<Vector2> movementInput = new(Vector2.zero);
        public ReactiveProperty<Vector2> velocity = new(Vector2.zero);
        public ReactiveProperty<Vector2> worldPosition = new(Vector2.zero);
        public ReactiveProperty<bool> isGrounded = new(false);
        public ReactiveProperty<bool> isInsideClimbable = new(false);
        public ReactiveProperty<bool> isClimbing = new(false);
        public ReactiveProperty<bool> isDamagingFall = new(false);
        public ReactiveProperty<bool> isHurt = new(false);
        public ReactiveProperty<bool> isInvincible = new(false);

        public float highestAirborneY;
        public float currentFallDistance;
        public float currentFallCells;
        public Collider2D groundCollider;
        public Vector2 groundNormal;

        public bool HasRestriction(PlayerRestrictionFlags flag)
        {
            return (restrictions.Value & flag) != 0;
        }

        public void SetRestriction(PlayerRestrictionFlags flag, bool enabled)
        {
            restrictions.Value = enabled
                ? restrictions.Value | flag
                : restrictions.Value & ~flag;
        }
    }
}
