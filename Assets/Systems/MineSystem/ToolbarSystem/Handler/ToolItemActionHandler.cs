using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;

namespace Systems.MineSystem.ToolbarSystem.Handler
{
    public sealed class ToolItemActionHandler :
        AnimatedItemActionHandler<ToolActionProfile>
    {
        private readonly MineModel _mine;
        private readonly IItemTargetResolver _targets;
        private readonly RuntimeDataScriptable _runtime;
        private readonly ItemActionProfileCatalog _catalog;

        public override ItemActionKind ActionKind => ItemActionKind.Tool;
        protected override bool ApplyImpactOnCompletion => true;
        protected override bool RepeatWhileActionHeld => true;

        public ToolItemActionHandler(
            MineModel mine,
            IItemTargetResolver targets,
            RuntimeDataScriptable runtime,
            ItemActionProfileCatalog catalog,
            IPlayerItemAnimationService animation,
            IToolbarNavigationLock navigationLock)
            : base(animation, navigationLock)
        {
            _mine = mine;
            _targets = targets;
            _runtime = runtime;
            _catalog = catalog;
        }

        protected override bool TryPrepareAction(
            ToolActionProfile profile,
            out string animationId,
            out int marker,
            out ItemActionTarget target)
        {
            target = _targets.ResolveDirectionalTarget(profile.Range);
            animationId = profile.GetAnimationId(
                _runtime.isClimbing.Value,
                target.Direction);
            marker = _catalog.EquippableImpactMarker;
            if (string.IsNullOrWhiteSpace(animationId))
                return false;

            PersistHorizontalFacing(target.Direction);
            return true;
        }

        private void PersistHorizontalFacing(CardinalDirection direction)
        {
            if (direction == CardinalDirection.Left)
                _runtime.facingDirection.Value =
                    MinePlayerSystem.Model.PlayerFacingDirection.Left;
            else if (direction == CardinalDirection.Right)
                _runtime.facingDirection.Value =
                    MinePlayerSystem.Model.PlayerFacingDirection.Right;
        }

        protected override void ApplyImpact(
            Item item,
            int slotIndex,
            ToolActionProfile profile,
            ItemActionTarget target)
        {
            _mine.TryHitCell(target.CellPosition, profile.WallDamage);
        }
    }
}
