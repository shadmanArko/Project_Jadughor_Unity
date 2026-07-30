using UnityEngine;
using Zenject;
using ProjectMuseum.Data;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Zenject bindings for the museum data + placement systems. Create the asset
    /// via <c>Create ▸ Installers ▸ Museum Installer</c>, assign the three data
    /// assets, then add it to the Museum scene's SceneContext (ScriptableObject
    /// Installers).
    /// </summary>
    [CreateAssetMenu(fileName = "MuseumInstaller", menuName = "Installers/Museum Installer")]
    public sealed class MuseumInstaller : ScriptableObjectInstaller<MuseumInstaller>
    {
        [SerializeField] private MuseumDataAsset museumData;
        [SerializeField] private BuilderDatabase builderDatabase;
        [SerializeField] private PlaceablePrefabConfig placeablePrefabConfig;
        [SerializeField] private MuseumArtifactDatabase artifactDatabase;

        public override void InstallBindings()
        {
            Container.Bind<MuseumDataAsset>()
                .FromScriptableObject(museumData)
                .AsSingle();

            Container.Bind<BuilderDatabase>()
                .FromScriptableObject(builderDatabase)
                .AsSingle();

            Container.Bind<PlaceablePrefabConfig>()
                .FromScriptableObject(placeablePrefabConfig)
                .AsSingle();

            Container.Bind<MuseumArtifactDatabase>()
                .FromScriptableObject(artifactDatabase)
                .AsSingle();

            Container.BindInterfacesAndSelfTo<MuseumDataModel>().AsSingle();
        }
    }
}
