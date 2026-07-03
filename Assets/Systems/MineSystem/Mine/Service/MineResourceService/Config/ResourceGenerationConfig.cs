using System.Collections.Generic;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineResourceService.Config
{
    [CreateAssetMenu(fileName = "ResourceGenerationConfig", menuName = "Config/ResourceConfig")]
    public class ResourceGenerationConfig : ScriptableObject
    {
        public int minRootNodes;
        public int maxRootNodes;
        
        public List<ResourceGenData> resourceGenDatas;
    }
}