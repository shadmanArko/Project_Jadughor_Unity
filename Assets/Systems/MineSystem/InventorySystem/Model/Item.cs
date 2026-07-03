using System;

namespace Systems.MineSystem.InventorySystem.Model
{
    [Serializable]
    public class Item
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public string Variant { get; set; }
    }
}