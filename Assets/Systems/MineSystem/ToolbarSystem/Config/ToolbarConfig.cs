using System.Collections.Generic;
using Systems.MineSystem.ToolbarSystem.View;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Scriptable
{
    [CreateAssetMenu(fileName = "ToolbarConfig", menuName = "Config/Toolbar Config")]
    public sealed class ToolbarConfig : ScriptableObject
    {
        [Header("Presentation")]
        [SerializeField] private ToolbarCanvasView toolbarCanvasPrefab;
        [SerializeField] private ToolbarSlotView toolbarSlotPrefab;
        [SerializeField] private Sprite selectedSlotSprite;

        [Header("Slots")]
        [Range(1, 12)]
        [SerializeField] private int toolbarSlotCount = 12;

        [Header("Input")]
        [SerializeField] private bool mouseWheelUpSelectsNext;
        [Min(0.01f)]
        [SerializeField] private float mouseWheelThreshold = 0.1f;

        [Header("Starting Items")]
        [SerializeField] private List<DefaultToolbarItem> defaultItems = new();

        public ToolbarCanvasView ToolbarCanvasPrefab => toolbarCanvasPrefab;
        public ToolbarSlotView ToolbarSlotPrefab => toolbarSlotPrefab;
        public Sprite SelectedSlotSprite => selectedSlotSprite;
        public int ClampedSlotCount => Mathf.Clamp(toolbarSlotCount, 1, 12);
        public bool MouseWheelUpSelectsNext => mouseWheelUpSelectsNext;
        public float MouseWheelThreshold => Mathf.Max(0.01f, mouseWheelThreshold);
        public IReadOnlyList<DefaultToolbarItem> DefaultItems => defaultItems;
    }
}
