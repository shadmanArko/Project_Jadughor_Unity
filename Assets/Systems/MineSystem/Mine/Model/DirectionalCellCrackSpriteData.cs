using System;
using System.Collections.Generic;

namespace Systems.MineSystem.Mine.Model
{
    [Serializable]
    public class DirectionalCellCrackSpriteData
    {
        public Direction direction;
        public List<CellCrackSpriteData> crackSpriteDataList;
    }
}