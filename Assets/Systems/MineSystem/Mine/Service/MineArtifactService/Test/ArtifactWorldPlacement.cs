using System;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    [Serializable]
    public sealed class ArtifactWorldPlacement
    {
        public string ArtifactInstanceId { get; set; }
        public GridPosition Position { get; set; }
        public string CellId { get; set; }
    }
}
