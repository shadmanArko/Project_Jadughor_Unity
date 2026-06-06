using System;
using Systems.MineSystem.Mine.Service.MineArtifactService.Enum;

namespace Systems.MineSystem.InventorySystem.Model
{
    [Serializable]
    public class Artifact : Item
    {
        public string DefinitionId { get; set; }
        public string Material { get; set; }
        public Condition Condition { get; set; }
        public Rarity Rarity { get; set; }
    }
}
