using System.Collections.Generic;
using Systems.MineSystem.Mine.Enum;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Config
{
    [CreateAssetMenu(fileName = "ArtifactGenerationConfig", menuName = "Config/ArtifactGenerationConfig")]
    public class ArtifactGenerationConfig : ScriptableObject
    {
        public Region region;
        public Site site;
        
        public int minNumberOfArtifacts;
        public int maxNumberOfArtifacts;

        public int minNumberOfRareArtifacts;
        public int maxNumberOfRareArtifacts;
        
        public int minNumberOfLegendaryArtifacts;
        public int maxNumberOfLegendaryArtifacts;

        public List<ArtifactGenerationData> artifactGenerationDatas;
    }
}
