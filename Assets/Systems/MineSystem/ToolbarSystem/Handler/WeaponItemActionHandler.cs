using System.Collections.Generic;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Handler
{
    public sealed class WeaponItemActionHandler :
        AnimatedItemActionHandler<WeaponActionProfile>
    {
        private readonly IItemTargetResolver _targets;
        private readonly RuntimeDataScriptable _runtime;
        private readonly ItemActionProfileCatalog _catalog;

        public override ItemActionKind ActionKind => ItemActionKind.Weapon;

        public WeaponItemActionHandler(
            IItemTargetResolver targets,
            RuntimeDataScriptable runtime,
            ItemActionProfileCatalog catalog,
            IPlayerItemAnimationService animation,
            IToolbarNavigationLock navigationLock)
            : base(animation, navigationLock)
        {
            _targets = targets;
            _runtime = runtime;
            _catalog = catalog;
        }

        protected override bool TryPrepareAction(
            WeaponActionProfile profile,
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
            WeaponActionProfile profile,
            ItemActionTarget target)
        {
            var colliders = Physics2D.OverlapCircleAll(
                target.WorldPosition,
                profile.HitRadius,
                profile.TargetLayers);
            var damaged = new HashSet<IDamageable>();
            foreach (var collider in colliders)
            {
                var behaviours = collider.GetComponentsInParent<MonoBehaviour>();
                foreach (var behaviour in behaviours)
                {
                    if (behaviour is IDamageable damageable &&
                        damaged.Add(damageable))
                    {
                        damageable.ApplyDamage(profile.Damage, item);
                    }
                }
            }
        }
    }
}
