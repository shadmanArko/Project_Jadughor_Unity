using System;
using System.Collections.Generic;
using Systems.MineSystem.ToolbarSystem.Interface;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.View
{
    public sealed class ToolbarCanvasView : MonoBehaviour, IToolbarView
    {
        [SerializeField] private Transform slotContainer;

        private readonly List<ToolbarSlotView> _slots = new(12);

        public IReadOnlyList<ToolbarSlotView> Slots => _slots;

        public void BuildSlots(
            ToolbarSlotView slotPrefab,
            int slotCount,
            Sprite selectedSlotSprite)
        {
            if (slotPrefab == null)
                throw new InvalidOperationException("Toolbar slot prefab is not configured.");

            var parent = slotContainer != null ? slotContainer : transform;
            ClearRuntimeSlots(parent);

            for (var index = 0; index < slotCount; index++)
            {
                var slot = Instantiate(slotPrefab, parent, false);
                slot.name = $"ToolbarSlot_{index}";
                slot.Initialize(index, selectedSlotSprite);
                _slots.Add(slot);
            }
        }

        public void PresentSlot(int slotIndex, Sprite sprite, int count)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return;

            _slots[slotIndex].Present(sprite, count);
        }

        public void SetHighlighted(int slotIndex, bool highlighted)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return;

            _slots[slotIndex].SetHighlighted(highlighted);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void ClearRuntimeSlots(Transform parent)
        {
            _slots.Clear();
            for (var index = parent.childCount - 1; index >= 0; index--)
                Destroy(parent.GetChild(index).gameObject);
        }
    }
}
