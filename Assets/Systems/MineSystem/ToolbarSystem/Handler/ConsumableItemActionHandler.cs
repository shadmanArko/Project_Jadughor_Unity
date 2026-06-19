using Systems.MineSystem.InventorySystem.Interface;
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
    public sealed class ConsumableItemActionHandler :
        AnimatedItemActionHandler<ConsumableActionProfile>
    {
        private readonly MinePlayerScriptable _player;
        private readonly IInventoryService _inventory;

        public override ItemActionKind ActionKind => ItemActionKind.Consumable;

        public ConsumableItemActionHandler(
            MinePlayerScriptable player,
            IInventoryService inventory,
            IPlayerItemAnimationService animation,
            IToolbarNavigationLock navigationLock)
            : base(animation, navigationLock)
        {
            _player = player;
            _inventory = inventory;
        }

        protected override bool TryPrepareAction(
            ConsumableActionProfile profile,
            out string animationId,
            out int marker,
            out ItemActionTarget target)
        {
            animationId = profile.AnimationId;
            marker = profile.ConsumeMarker;
            target = default;
            return !string.IsNullOrWhiteSpace(animationId) &&
                   CanRestore(profile);
        }

        protected override void ApplyImpact(
            Item item,
            int slotIndex,
            ConsumableActionProfile profile,
            ItemActionTarget target)
        {
            var property = profile.Stat == ConsumableStat.Health
                ? _player.playerData.health
                : _player.playerData.stamina;
            var maximum = profile.Stat == ConsumableStat.Health
                ? _player.playerData.maxHealth.Value
                : _player.playerData.maxStamina.Value;
            var previous = property.Value;
            var restored = Mathf.Clamp(
                previous + profile.RestoreAmount,
                0f,
                maximum);
            if (restored <= previous)
                return;

            property.Value = restored;
            if (!_inventory.TryRemoveOne(slotIndex, item))
                property.Value = previous;
        }

        private bool CanRestore(ConsumableActionProfile profile)
        {
            return profile.Stat == ConsumableStat.Health
                ? _player.playerData.health.Value <
                  _player.playerData.maxHealth.Value
                : _player.playerData.stamina.Value <
                  _player.playerData.maxStamina.Value;
        }
    }
}
