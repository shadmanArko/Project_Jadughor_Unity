using System.Collections.Generic;
using UnityEngine;

namespace Systems.MineSystem.Mine.Config.ResourceConfig
{
    [CreateAssetMenu(fileName = "ResourceGenerationConfig", menuName = "Config/ResourceConfig")]
    public class ResourceGenerationConfig : ScriptableObject
    {
        public int minRootNodes;
        public int maxRootNodes;
        
        public List<ResourceGenData> resourceGenDatas;
    }
}