using System;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.ResourceSystem.Model
{
    [Serializable]
    public class Resource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public string Variant { get; set; }
        public GridPosition Position { get; set; }
        public string PNGPath { get; set; }
        public string CellId { get; set; }
    }
}
