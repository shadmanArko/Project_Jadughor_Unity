using Systems.MineSystem.MineTransitionSystem.Config;
using Systems.MineSystem.MineTransitionSystem.Controller;
using Systems.MineSystem.MineTransitionSystem.Service;
using Systems.MineSystem.MineTransitionSystem.View;
using Systems.MineSystem.Utilities.Camera;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MineTransitionSystem.Installer
{
    [CreateAssetMenu(fileName = "MineTransitionInstaller", menuName = "Installers/Mine Transition Installer")]
    public sealed class MineTransitionInstaller : ScriptableObjectInstaller<MineTransitionInstaller>
    {
        [SerializeField] private CampView campPrefab;
        [SerializeField] private MineTransitionCanvasView canvasPrefab;
        [SerializeField] private MineTransitionConfig transitionConfig;
        [SerializeField] private MineCameraConfig cameraConfig;

        public override void InstallBindings()
        {
            Container.Bind<CampView>().FromComponentInNewPrefab(campPrefab)
                .AsSingle().NonLazy();
            Container.Bind<MineTransitionCanvasView>()
                .FromComponentInNewPrefab(canvasPrefab).AsSingle().NonLazy();
            Container.Bind<MineTransitionConfig>()
                .FromScriptableObject(transitionConfig).AsSingle();
            Container.Bind<MineCameraConfig>()
                .FromScriptableObject(cameraConfig).AsSingle();
            Container.BindInterfacesAndSelfTo<MineCameraController>()
                .AsSingle().NonLazy();
            Container.Bind<CampToMineService>().AsSingle();
            Container.Bind<CampToMuseumService>().AsSingle();
            Container.Bind<MineToCampService>().AsSingle();
            Container.BindInterfacesAndSelfTo<MineTransitionController>()
                .AsSingle().NonLazy();
        }
    }
}
