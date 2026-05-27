using System.Collections.Generic;
using Systems.MineSystem.ResourceSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.ResourceSystem.Config
{
    [CreateAssetMenu(fileName = "ResourceGenerationConfig", menuName = "Config/ResourceConfig")]
    public class ResourceGenerationConfig : ScriptableObject
    {
        public int minRootNodes;
        public int maxRootNodes;
        
        public List<ResourceGenData> resourceGenDatas;
    }
}