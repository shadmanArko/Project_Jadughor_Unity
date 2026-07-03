using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class CellCrackData
    {
        public Region region;
        public Site site;
        public List<DirectionalCellCrackSpriteData> cellCrackSpriteDataList;
    }
}