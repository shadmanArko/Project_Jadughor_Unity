using System;
using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.MineGenerationSystem.Model;

namespace Systems.MineSystem.Mine.Database
{
    [Serializable]
    public class SpecialBackdropSpriteData
    {
        public Region region;
        public Site site;

        public List<SpecialBackdropSprite> specialBackdropSprites;
    }
}