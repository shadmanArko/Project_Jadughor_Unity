using Systems.MineSystem.InventorySystem.View;
using UnityEngine;

namespace Systems.MineSystem.InventorySystem.Scriptable
{
    [CreateAssetMenu(fileName = "InventorySystemConfig", menuName = "Config/InventorySystemConfig")]
    public sealed class InventorySystemConfig : ScriptableObject
    {
        [Header("Items")]
        [Min(1)] public int defaultStackLimit = 99;
        public int slotsPerRow = 12;

        [Header("Controller Navigation")]
        [Range(0.1f, 1f)] public float navigationDeadZone = 0.5f;
        [Min(0f)] public float navigationInitialRepeatDelay = 0.4f;
        [Min(0.02f)] public float navigationRepeatInterval = 0.15f;

        [Header("Pointer Transfer")]
        [Min(0f)] public float rightClickHoldThreshold = 0.25f;
        [Min(0.02f)] public float rightClickTransferInterval = 0.15f;

        [Header("Presentation")]
        public Sprite selectedSlotFrame;
        public InventoryCanvasView inventoryCanvasPrefab;
    }
}
