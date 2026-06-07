using System;
using Systems.MineSystem.InventorySystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.InventorySystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Scriptable;

namespace Systems.MineSystem.InventorySystem.Service
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly InventoryModel _model;
        private readonly InventorySystemConfig _config;
        private readonly MinePlayerScriptable _player;

        public InventoryService(
            InventoryModel model,
            InventorySystemConfig config,
            MinePlayerScriptable player)
        {
            _model = model;
            _config = config;
            _player = player;
        }

        public bool CanAdd(Item item)
        {
            if (item == null)
                return false;

            var unlocked = GetUnlockedSlotCount();
            for (var i = 0; i < unlocked; i++)
            {
                var stack = _model.Slots[i].Stack;
                if (stack == null)
                    return true;

                if (CanStack(stack.Representative, item) &&
                    stack.Count < GetStackLimit(item))
                    return true;
            }

            return false;
        }

        public bool TryAdd(Item item)
        {
            if (!CanAdd(item))
                return false;

            var unlocked = GetUnlockedSlotCount();
            var stackLimit = GetStackLimit(item);
            for (var i = 0; i < unlocked; i++)
            {
                var stack = _model.Slots[i].Stack;
                if (stack == null ||
                    !CanStack(stack.Representative, item) ||
                    stack.Count >= stackLimit)
                    continue;

                stack.Add(item);
                _model.NotifySlotChanged(i);
                return true;
            }

            for (var i = 0; i < unlocked; i++)
            {
                if (!_model.Slots[i].IsEmpty)
                    continue;

                _model.Slots[i].Stack = new InventoryStack(item);
                _model.NotifySlotChanged(i);
                return true;
            }

            return false;
        }

        public void LeftClick(int slotIndex)
        {
            if (!IsUnlocked(slotIndex))
                return;

            var slot = _model.Slots[slotIndex];
            var held = _model.HeldStack;
            if (held == null)
            {
                _model.HeldStack = slot.Stack;
                slot.Stack = null;
                _model.NotifySlotChanged(slotIndex);
                return;
            }

            if (slot.IsEmpty)
            {
                slot.Stack = held;
                _model.HeldStack = null;
                _model.NotifySlotChanged(slotIndex);
                return;
            }

            if (CanStack(held.Representative, slot.Stack.Representative))
            {
                held.TransferTo(
                    slot.Stack,
                    GetStackLimit(slot.Stack.Representative));
                if (held.IsEmpty)
                    _model.HeldStack = null;
                else
                    _model.NotifyHeldStackChanged();

                _model.NotifySlotChanged(slotIndex);
                return;
            }

            var previous = slot.Stack;
            slot.Stack = held;
            _model.HeldStack = previous;
            _model.NotifySlotChanged(slotIndex);
        }

        public void RightClick(int slotIndex)
        {
            if (!IsUnlocked(slotIndex))
                return;

            var slot = _model.Slots[slotIndex];
            var held = _model.HeldStack;
            if (held == null)
            {
                if (slot.IsEmpty)
                    return;

                _model.HeldStack = slot.Stack.TakeOne();
                if (slot.Stack.IsEmpty)
                    slot.Stack = null;

                _model.NotifySlotChanged(slotIndex);
                return;
            }

            if (slot.IsEmpty)
            {
                slot.Stack = held.TakeOne();
                if (held.IsEmpty)
                    _model.HeldStack = null;
                else
                    _model.NotifyHeldStackChanged();

                _model.NotifySlotChanged(slotIndex);
                return;
            }

            if (!CanStack(held.Representative, slot.Stack.Representative) ||
                held.Count >= GetStackLimit(held.Representative))
                return;

            held.Add(slot.Stack.RemoveOne());
            if (slot.Stack.IsEmpty)
                slot.Stack = null;

            _model.NotifyHeldStackChanged();
            _model.NotifySlotChanged(slotIndex);
        }

        public void TrashHeldStack()
        {
            _model.HeldStack = null;
        }

        private int GetUnlockedSlotCount()
        {
            return Math.Clamp(
                _player.playerData.unlockedInventorySlots.Value,
                0,
                InventoryModel.MaximumSlots);
        }

        private bool IsUnlocked(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < GetUnlockedSlotCount();
        }

        private int GetStackLimit(Item item)
        {
            return item is Artifact ? 1 : _config.defaultStackLimit;
        }

        private static bool CanStack(Item first, Item second)
        {
            return first != null &&
                   second != null &&
                   first is not Artifact &&
                   second is not Artifact &&
                   first.GetType() == second.GetType() &&
                   string.Equals(first.Type, second.Type, StringComparison.Ordinal) &&
                   string.Equals(first.Category, second.Category, StringComparison.Ordinal) &&
                   string.Equals(first.Variant, second.Variant, StringComparison.Ordinal);
        }
    }
}
