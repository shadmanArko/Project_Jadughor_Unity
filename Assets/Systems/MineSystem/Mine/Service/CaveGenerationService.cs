using System;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.Mine.Service
{
    [Serializable]
    public class CaveGenerationService
    {

        public async UniTask GenerateBossCave(MineGenerationConfig config, MineData mineData)
        {
            var bossCaveWidth = config.bossCaveSizeX;
            var bossCaveHeight = config.bossCaveSizeY;
        }
    }
}