using System;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Model
{
    [Serializable]
    public sealed class ArtifactWorldPlacementData
    {
        public string ArtifactInstanceId { get; set; }
        public GridPosition Position { get; set; }
        public string CellId { get; set; }
    }
}
