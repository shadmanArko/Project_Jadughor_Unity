using Systems.MineSystem.ToolbarSystem.View;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Interface
{
    public interface IToolbarView
    {
        void BuildSlots(
            ToolbarSlotView slotPrefab,
            int slotCount,
            Sprite selectedSlotSprite);
        void PresentSlot(int slotIndex, Sprite sprite, int count);
        void SetHighlighted(int slotIndex, bool highlighted);
        void SetVisible(bool visible);
    }
}
