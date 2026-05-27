using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.MineGenerationSystem.Model;

namespace Systems.MineSystem.ResourceSystem.Model
{
    [Serializable]
    public class ResourceSpriteData
    {
        public Region region;
        public Site site;
        public List<SpriteData> spriteDatas;
    }
}