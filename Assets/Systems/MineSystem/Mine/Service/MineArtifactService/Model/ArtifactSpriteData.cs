using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    [Serializable]
    public sealed class ArtifactSpriteData
    {
        public Region region;
        public Site site;
        public List<ArtifactSpriteEntry> artifacts = new();
    }
}
