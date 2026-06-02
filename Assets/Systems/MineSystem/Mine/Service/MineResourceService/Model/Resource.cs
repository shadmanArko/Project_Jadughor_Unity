using System;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.Mine.Service.MineResourceService.Model
{
    [Serializable]
    public class Resource : Item
    {
        public bool IsStackable { get; set; }
        public int MaxStackAmount { get; set; }
        public GridPosition Position { get; set; }
        public string CellId { get; set; }
    }
}
