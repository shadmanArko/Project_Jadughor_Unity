using System.Collections.Generic;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Damage;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.View;
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
        private readonly PlayerView _player;
        private readonly MineView _mineView;
        private readonly List<Collider2D> _hitResults = new(32);

        public override ItemActionKind ActionKind => ItemActionKind.Weapon;

        public WeaponItemActionHandler(
            IItemTargetResolver targets,
            RuntimeDataScriptable runtime,
            ItemActionProfileCatalog catalog,
            PlayerView player,
            MineView mineView,
            IPlayerItemAnimationService animation,
            IToolbarNavigationLock navigationLock)
            : base(animation, navigationLock)
        {
            _targets = targets;
            _runtime = runtime;
            _catalog = catalog;
            _player = player;
            _mineView = mineView;
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
            var direction = ToVector(target.Direction);
            var cellSize = GetDirectionalCellSize(direction);
            var origin = (Vector2)_player.PlayerCollider.bounds.center;
            var center =
                origin + direction * (profile.Range * cellSize);
            var radius = profile.HitRadiusCells * cellSize;
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = profile.TargetLayers,
                useTriggers = true
            };
            _hitResults.Clear();
            Physics2D.OverlapCircle(
                center,
                radius,
                filter,
                _hitResults);
            var damaged = new HashSet<IDamageable>();
            for (var index = 0; index < _hitResults.Count; index++)
            {
                var collider = _hitResults[index];
                if (collider == null)
                    continue;

                var behaviours = collider.GetComponentsInParent<MonoBehaviour>();
                foreach (var behaviour in behaviours)
                {
                    if (behaviour is IDamageable damageable &&
                        !ReferenceEquals(damageable, _player) &&
                        damaged.Add(damageable))
                    {
                        damageable.ApplyDamage(profile.Damage);
                    }
                }
            }
        }

        private float GetDirectionalCellSize(Vector2 direction)
        {
            var grid = _mineView.grid;
            var origin = grid.CellToWorld(Vector3Int.zero);
            var adjacent = grid.CellToWorld(
                direction.x != 0f
                    ? Vector3Int.right
                    : Vector3Int.up);
            return Mathf.Max(
                0.0001f,
                direction.x != 0f
                    ? Mathf.Abs(adjacent.x - origin.x)
                    : Mathf.Abs(adjacent.y - origin.y));
        }

        private static Vector2 ToVector(CardinalDirection direction)
        {
            return direction switch
            {
                CardinalDirection.Up => Vector2.up,
                CardinalDirection.Down => Vector2.down,
                CardinalDirection.Left => Vector2.left,
                _ => Vector2.right
            };
        }
    }
}
