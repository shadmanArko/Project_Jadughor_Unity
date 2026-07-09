using UnityEngine;
using Zenject;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Zenject bindings for the museum data + placement systems. Create the asset
    /// via <c>Create ▸ Installers ▸ Museum Installer</c>, assign the two data assets,
    /// then add it to the Museum scene's SceneContext (ScriptableObject Installers).
    /// </summary>
    [CreateAssetMenu(fileName = "MuseumInstaller", menuName = "Installers/Museum Installer")]
    public sealed class MuseumInstaller : ScriptableObjectInstaller<MuseumInstaller>
    {
        [SerializeField] private MuseumDataAsset museumData;
        [SerializeField] private BuilderDatabase builderDatabase;

        public override void InstallBindings()
        {
            Container.Bind<MuseumDataAsset>()
                .FromScriptableObject(museumData)
                .AsSingle();

            Container.Bind<BuilderDatabase>()
                .FromScriptableObject(builderDatabase)
                .AsSingle();

            Container.BindInterfacesAndSelfTo<MuseumDataModel>().AsSingle();
        }
    }
}
