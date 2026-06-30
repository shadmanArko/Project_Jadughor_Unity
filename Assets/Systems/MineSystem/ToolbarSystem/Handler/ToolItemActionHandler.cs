using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Handler
{
    public sealed class ToolItemActionHandler :
        AnimatedItemActionHandler<ToolActionProfile>
    {
        private readonly MineModel _mine;
        private readonly IItemTargetResolver _targets;
        private readonly RuntimeDataScriptable _runtime;
        private readonly ItemActionProfileCatalog _catalog;
        private readonly IPlaceableRuntimeResolver _placeables;
        private float _nextAllowedActionTime;

        public override ItemActionKind ActionKind => ItemActionKind.Tool;
        protected override bool RepeatWhileActionHeld => true;
        protected override int? RecoveryHandoffMarker => 2;

        public ToolItemActionHandler(
            MineModel mine,
            IItemTargetResolver targets,
            RuntimeDataScriptable runtime,
            ItemActionProfileCatalog catalog,
            IPlaceableRuntimeResolver placeables,
            IPlayerItemAnimationService animation,
            IToolbarNavigationLock navigationLock)
            : base(animation, navigationLock)
        {
            _mine = mine;
            _targets = targets;
            _runtime = runtime;
            _catalog = catalog;
            _placeables = placeables;
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

        protected override bool CanStartAction(ToolActionProfile profile)
        {
            return Time.time >= _nextAllowedActionTime;
        }

        protected override void OnActionStarted(ToolActionProfile profile)
        {
            _nextAllowedActionTime = Time.time + profile.CooldownSeconds;
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
            if (_placeables.TryResolve(
                    target.CellPosition,
                    out var placeable) &&
                placeable.DamageView != null)
            {
                placeable.DamageView.ApplyDamage(profile.WallDamage);
                return;
            }

            _mine.TryHitCell(
                target.CellPosition,
                profile.WallDamage,
                GetImpactSide(target.Direction));
        }

        private static Direction GetImpactSide(CardinalDirection direction)
        {
            return direction switch
            {
                CardinalDirection.Left => Direction.Right,
                CardinalDirection.Right => Direction.Left,
                CardinalDirection.Up => Direction.Down,
                _ => Direction.Up
            };
        }
    }
}
