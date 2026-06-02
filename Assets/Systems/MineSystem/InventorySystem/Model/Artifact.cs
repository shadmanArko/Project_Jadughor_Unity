using System;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.InventorySystem.Model
{
    [Serializable]
    public class Artifact : Item
    {
        public string Material { get; set; }
        public GridPosition Position { get; set; }
        public string CellId { get; set; }
    }
}
