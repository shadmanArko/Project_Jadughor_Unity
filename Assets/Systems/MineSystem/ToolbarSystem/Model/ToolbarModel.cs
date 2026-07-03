using System;
using Systems.MineSystem.ToolbarSystem.Scriptable;

namespace Systems.MineSystem.ToolbarSystem.Model
{
    public sealed class ToolbarModel
    {
        public int SlotCount { get; }
        public int SelectedSlot { get; private set; }

        public ToolbarModel(ToolbarConfig config)
        {
            SlotCount = config.ClampedSlotCount;
            SelectedSlot = 0;
        }

        public void Select(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            SelectedSlot = slotIndex;
        }

        public bool TryFindOccupiedSlot(
            int direction,
            Func<int, bool> isOccupied,
            out int slotIndex)
        {
            if (direction == 0)
            {
                slotIndex = SelectedSlot;
                return false;
            }

            var step = direction > 0 ? 1 : -1;
            for (var distance = 1; distance < SlotCount; distance++)
            {
                var candidate = Wrap(SelectedSlot + step * distance);
                if (!isOccupied(candidate))
                    continue;

                slotIndex = candidate;
                return true;
            }

            slotIndex = SelectedSlot;
            return false;
        }

        private int Wrap(int index)
        {
            return (index % SlotCount + SlotCount) % SlotCount;
        }
    }
}
