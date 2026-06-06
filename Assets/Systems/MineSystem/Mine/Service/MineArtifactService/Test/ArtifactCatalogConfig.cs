using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineArtifactService.Test
{
    [CreateAssetMenu(fileName = "ArtifactCatalogConfig", menuName = "Config/Artifact Catalog Config")]
    public sealed class ArtifactCatalogConfig : ScriptableObject
    {
        [SerializeField] private TextAsset functionalData;
        [SerializeField] private TextAsset descriptiveData;

        public TextAsset FunctionalData => functionalData;
        public TextAsset DescriptiveData => descriptiveData;
    }
}
