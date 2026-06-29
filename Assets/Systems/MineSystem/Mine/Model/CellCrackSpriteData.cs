using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.MineGenerationSystem.Model;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class CellCrackSpriteData
    {
        public Region region;
        public Site site;
        public List<SpriteData> cellCrackSpriteDataList;
    }
}