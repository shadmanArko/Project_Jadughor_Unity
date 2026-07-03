namespace Systems.MineSystem.InventorySystem.Model
{
    public sealed class InventorySlot
    {
        public InventoryStack Stack { get; set; }
        public bool IsEmpty => Stack == null || Stack.IsEmpty;
    }
}
