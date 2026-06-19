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
            return !string.IsNullOrWhiteSpace(animationId);
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
