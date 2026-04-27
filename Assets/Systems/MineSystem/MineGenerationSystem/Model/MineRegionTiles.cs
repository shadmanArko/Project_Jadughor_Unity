using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.MineGenerationSystem.Model
{
    [Serializable]
    public class MineRegionTiles
    {
        public Region region;
        public List<Sprite> tileSprites;
    }
}