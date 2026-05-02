using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;

namespace Systems.MineSystem.MineGenerationSystem.Model
{
    [Serializable]
    public class MineRegionalTiles
    {
        public Region region;
        public List<BrokenEdgeTile> brokenEdgeTiles;
        public List<MineTile> mineTiles;
    }
}